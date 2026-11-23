using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Timed effects: the tempmods (<see cref="TempMods"/>) an ability puts on a player or an NPC, run on a server tick.
    /// <list type="bullet">
    /// <item>Apply: each tempmod gets its own time (Vector: Powerless 8.94 s, ViralDeflection 30 s) and, on a player, its own
    /// 80 bc record, sent on.</item>
    /// <item>Refresh: the same ability again replaces what it put on before: the old records go off and new ones (new record
    /// ids, full time, values rolled again) go on, in the same packet. Crippling Shot (old_world Vector 25521: 6cf-6d1 off,
    /// 6d2-6d4 on, -113 -&gt; -112), Disarming Shot (medanon niobe3dualduality 19283), Plague Zone 2.0 re-infecting Zombie
    /// Lurch at 10:27:51 (actions_combat).</item>
    /// <item>Stack: different abilities stack, each with its own records, even on the same stat: Code Infection 4, Infect
    /// Area 1 and 2 and Code Infection 3 all ticked on Morpheuzzz at once (morpheuzzz_001 2544-4276).</item>
    /// <item>Control immunity: a stun, pacify or Powerless puts ControlImmunityTimer on, and another one can't land while it
    /// is (tempmods.py ApplyStunWithTimer / ApplyMezEffect, "Stun stacking detected").</item>
    /// <item>Expire: on the tick after the time is up, with the records off. Damage over time ticks every
    /// <see cref="TempMods.DotIntervalSeconds"/> while the time lasts; a tick due at the very end doesn't happen (Plague
    /// Zone 2.0's 15 s gave five ticks).</item>
    /// </list>
    /// Self buffs (<see cref="AbilityBuffs"/>) keep their own records and families and expire on the same tick.
    /// </summary>
    public static class TimedEffects
    {
        /// <summary>How often the server tick runs.</summary>
        public const double TickSeconds = 0.25;

        /// <summary>One tempmod on a subject.</summary>
        public sealed class Active
        {
            public UInt16 Parent;
            internal Action<Subject, Active> OnExpired;
            internal Action<Subject, Active,double> OnExpiredAt;
            internal Func<bool> CallbackCurrent,CallbackStateCurrent;
            internal bool UpdatesNpcEnrage;
            public UInt16 Kind;
            public UInt16 Target;
            public Int32 Value;
            internal int? SweepChance;
            public int DamageType = -1;
            public float Seconds;
            public TempMods.Proc Proc;
            public TempMods.BreakRule Break;
            public int BreakChance;
            public bool ProtectFirstHealthChange;
            public double Interval = TempMods.DotIntervalSeconds;
            public UInt16 RecordId;
            /// <summary>States: the state's own record (the state ability as parent, 0 s), sent after the state's.</summary>
            public UInt16 LinkedRecordId;
            public double Start, Expires;
            public int Ticks;
            /// <summary>Who put it on (the caster's client, for NPC targets) and their name for the log lines.</summary>
            public WorldClient Caster;
            public string CasterName;
            /// <summary>The target's view id at the caster's client.</summary>
            public UInt16 TargetView;

            /// <summary>Damage of one damage-over-time tick before resistances (<see cref="TempMods.DotTick"/>).</summary>
            public int TickDamage => Proc == TempMods.Proc.Hot ? TempMods.HotTick(Value, Seconds, Interval) : TempMods.DotTick(Value, Seconds, Interval);

            /// <summary>When the next damage-over-time tick is due, or +inf when there is none left.</summary>
            public double NextTick
            {
                get
                {
                    double at = (Ticks + 1) * Interval;
                    return (UpdatesNpcEnrage || Proc == TempMods.Proc.Dot || Proc == TempMods.Proc.Hot ||
                        Proc == TempMods.Proc.Pulse || Proc == TempMods.Proc.SeverState) &&
                        Interval > 0 && at < Seconds - 1e-6 ? Start + at : double.PositiveInfinity;
                }
            }
        }

        /// <summary>The tempmods on one subject. Pure bookkeeping: no packets.</summary>
        public sealed class Holder
        {
            private readonly List<Active> active = new List<Active>();

            public bool IsEmpty
            {
                get { lock (active) { return active.Count == 0; } }
            }

            public List<Active> Snapshot()
            {
                lock (active)
                {
                    return new List<Active>(active);
                }
            }

            internal bool IsCurrent(Active state, double now)
            {
                lock (active)
                    return now < state.Expires && active.Contains(state);
            }

            /// <summary>
            /// Puts the tempmods on for <paramref name="parent"/>, replacing everything that ability put on before
            /// (retail's refresh). Returns the replaced ones.
            /// </summary>
            public List<Active> Apply(UInt16 parent, IList<Active> mods, double now, bool preserveOtherTargets = false)
            {
                lock (active)
                {
                    //1125 has an independent caster681 marker and target710 modifier. Refresh its tuple, not both.
                    bool Replaced(Active a)=>a.Parent==parent && (!preserveOtherTargets || mods.Any(m=>m.Target==a.Target));
                    List<Active> replaced = active.FindAll(Replaced);
                    active.RemoveAll(Replaced);
                    foreach (Active m in mods)
                    {
                        m.Parent = parent;
                        m.Start = now;
                        m.Expires = now + m.Seconds;
                        m.Ticks = 0;
                        active.Add(m);
                    }
                    return replaced;
                }
            }

            // Callback-specific append: observations must be pure and never take the publication gate.
            internal bool AppendCallback(ushort parent,Active candidate,double now,Func<bool> current,out List<Active> replaced)
            {
                lock(active)
                {
                    replaced=new List<Active>();
                    if(!current())return false;
                    var previous=active.FindAll(a=>a.Parent==parent && a.Target==candidate.Target);
                    replaced=previous;active.RemoveAll(a=>previous.Contains(a));
                    candidate.Parent=parent;candidate.Start=now;candidate.Expires=now+candidate.Seconds;candidate.Ticks=0;active.Add(candidate);
                    if(current())return true;
                    RollbackCallback(candidate,replaced,now);
                    return false;
                }
            }
            internal void RollbackCallback(Active candidate,List<Active> replaced,double now)
            {
                lock(active)
                {
                    // A clear/newer replacement wins. Never resurrect records after ours disappeared.
                    if(!active.Remove(candidate))return;
                    if(!active.Any(a=>a.Parent==candidate.Parent && a.Target==candidate.Target))active.AddRange(replaced.Where(old=>old.Expires>now));
                }
            }

            internal bool ApplyCallback(Active owner,Active child,double now,out List<Active> replaced)
            {
                lock(active)
                {
                    replaced=null;
                    if(!active.Contains(owner)||now>=owner.Expires||owner.CallbackStateCurrent?.Invoke()!=true)return false;
                    if(!active.Contains(owner))return false;
                    replaced=Apply(owner.Parent,new[]{child},now,preserveOtherTargets:true);return true;
                }
            }
            internal void RollbackCallback(Active owner,Active child,IEnumerable<Active> replaced,double now)
            {
                lock(active)
                {
                    // Another apply/clear may already have retired this exact unpublished child.
                    if(!active.Remove(child)||!active.Contains(owner))return;
                    foreach(var old in replaced)
                        if(old.Expires>now&&!active.Any(a=>a.Parent==old.Parent&&a.Target==old.Target))active.Add(old);
                }
            }
            public List<Active> RemoveParent(UInt16 parent)
            {
                lock (active)
                {
                    if (parent == MobiusCode.Ability) MobiusCode.Removed(this);
                    List<Active> removed = active.FindAll(a => a.Parent == parent);
                    active.RemoveAll(a => a.Parent == parent);
                    return removed;
                }
            }

            public bool Remove(Active mod)
            {
                lock (active)
                {
                    return active.Remove(mod);
                }
            }

            public List<Active> RemoveAll()
            {
                lock (active)
                {
                    MobiusCode.Removed(this);
                    List<Active> removed = new List<Active>(active);
                    active.Clear();
                    return removed;
                }
            }

            /// <summary>
            /// Moves time on: <paramref name="ticks"/> gets a mod once for every damage-over-time tick due by
            /// <paramref name="now"/>, <paramref name="expired"/> the mods whose time is up (taken off).
            /// </summary>
            public void Advance(double now, List<Active> ticks, List<Active> expired)
            {
                lock (active)
                {
                    foreach (Active a in active)
                    {
                        while (a.NextTick <= now)
                        {
                            a.Ticks++;
                            ticks.Add(a);
                        }
                        if (now >= a.Expires)
                        {
                            expired.Add(a);
                        }
                    }
                    active.RemoveAll(a => now >= a.Expires);
                }
            }

            /// <summary>The sum of the stat tempmods on a target ability (the modifier the server would apply).</summary>
            public int Total(UInt16 target)
            {
                lock (active)
                {
                    int sum = 0;
                    foreach (Active a in active)
                    {
                        if (a.Proc == TempMods.Proc.Stat && a.Target == target)
                        {
                            sum += a.Value;
                        }
                    }
                    return sum;
                }
            }

            /// <summary>Whether a tempmod on the target ability is on (a state, ControlImmunityTimer's ISDrainTarget ...).</summary>
            public bool Has(UInt16 target)
            {
                lock (active)
                {
                    return active.Exists(a => a.Target == target && a.Proc != TempMods.Proc.Stat);
                }
            }

            /// <summary>The timer callback checks the parent and target tuple at the time of application.</summary>
            internal bool HasControlImmunity(double now, Active except = null)
            {
                lock (active) return active.Exists(a => !ReferenceEquals(a, except) && a.Parent == TempMods.ControlImmunityTimerAbility &&
                    a.Target == TempMods.ISDrainTargetAbility && now < a.Expires);
            }

            public bool HasParent(UInt16 parent)
            {
                lock (active)
                {
                    return active.Exists(a => a.Parent == parent);
                }
            }

            public bool HasAt(UInt16 target, double now)
            {
                lock (active) return active.Exists(a => a.Target == target && a.Proc == TempMods.Proc.State && now < a.Expires);
            }

            internal uint TacticStateFlags(double now)
            {
                lock (active)
                {
                    uint flags = 0;
                    foreach (Active mod in active)
                    {
                        if (mod.Proc == TempMods.Proc.State && now < mod.Expires)
                            flags |= CombatInterlock.TacticStateFlag(mod.Target);
                    }
                    return flags;
                }
            }

            /// <summary>Script health procs deactivate the whole parent ability, not just its state record.</summary>
            public List<Active> HealthChanged(int damageTaken, bool belowMaximum, Random rng)
            {
                lock (active)
                {
                    var parents = new HashSet<UInt16>();
                    foreach (Active a in active)
                    {
                        bool reduced = damageTaken > 0 && belowMaximum;
                        bool breaks = a.Break == TempMods.BreakRule.HealthChange || a.Break == TempMods.BreakRule.Damage && reduced;
                        if (a.Break == TempMods.BreakRule.Chance)
                        {
                            // The script flips a negative proc value after any health callback, including healing.
                            if (a.ProtectFirstHealthChange) a.ProtectFirstHealthChange = false;
                            else if (reduced && a.BreakChance > rng.Next(100)) breaks = true;
                        }
                        if (breaks) parents.Add(a.Parent);
                    }
                    List<Active> removed = active.FindAll(a => parents.Contains(a.Parent));
                    active.RemoveAll(a => parents.Contains(a.Parent));
                    return removed;
                }
            }
        }

        /// <summary>Something tempmods can be on: a player or an NPC.</summary>
        public abstract class Subject
        {
            public readonly Holder Effects = new Holder();

            /// <summary>The client whose thread-local Store.currentClient the subject's packets go out with.</summary>
            internal abstract WorldClient Context { get; }
            internal abstract void Applied(TempMods.Effect effect, List<Active> added, Active immunity, List<Active> replaced);
            /// <summary>A damage-over-time tick; false when it can't land any more (the mod is then taken off).</summary>
            internal abstract bool Ticked(Active mod, int damage);
            internal abstract void Ended(List<Active> ended);
            internal virtual void Due(double now) { }
            internal virtual bool Idle => Effects.IsEmpty;
            internal virtual uint ConditionFlags => 0;
            internal virtual bool Alive => true;
            internal virtual int Determination => Math.Max(0, Effects.Total(575));
            internal virtual int DeterminationBonus => DeflectCode.CappedBonus(Effects.Total(1047));
        }

        // ---- the server tick ----

        private static readonly List<Subject> live = new List<Subject>();
        private static Timer timer;

        private static void Register(Subject subject)
        {
            lock (live)
            {
                if (!live.Contains(subject))
                {
                    live.Add(subject);
                }
                if (timer == null)
                {
                    int ms = (int)(TickSeconds * 1000);
                    timer = new Timer(_ => TimerTick(), null, ms, ms);
                }
            }
        }

        private static int ticking;

        private static void TimerTick()
        {
            // A slow tick must not overlap the next one
            if (Interlocked.Exchange(ref ticking, 1) == 1)
            {
                return;
            }
            try
            {
                RunDue(TimeUtils.GetSimTimeSeconds());
            }
            finally
            {
                Interlocked.Exchange(ref ticking, 0);
            }
        }

        internal static void Track(Subject subject)
        {
            Register(subject);
        }

        /// <summary>One server tick: damage over time, expiries and self-buff ends that are due by <paramref name="now"/>.</summary>
        public static void RunDue(double now)
        {
            List<Subject> subjects;
            lock (live)
            {
                subjects = new List<Subject>(live);
            }

            foreach (Subject subject in subjects)
            {
                try
                {
                    WithClient(subject.Context, () => Advance(subject, now));
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("[EFFECTS] Tick failed: " + ex.Message);
                }

                lock (live)
                {
                    if (subject.Idle)
                    {
                        live.Remove(subject);
                    }
                }
            }
        }

        private static void Advance(Subject subject, double now)
        {
            subject.Due(now);

            List<Active> ticks = new List<Active>();
            List<Active> expired = new List<Active>();
            subject.Effects.Advance(now, ticks, expired);

            // Ticks due before the end land even when the end is in the same server tick
            HashSet<Active> dropped = new HashSet<Active>();
            foreach (Active tick in ticks)
            {
                if (dropped.Contains(tick))
                {
                    continue;
                }
                if (!(tick.Proc==TempMods.Proc.SeverState?AbilitySeverArteryStates.Execute(subject,tick,now):subject.Ticked(tick, tick.TickDamage)))
                {
                    dropped.Add(tick);
                    bool wasOn = subject.Effects.Remove(tick) | expired.Remove(tick);
                    if (wasOn)
                    {
                        subject.Ended(new List<Active> { tick });
                    }
                }
            }
            foreach (Active mod in expired)
            {
                mod.OnExpired?.Invoke(subject, mod);
                mod.OnExpiredAt?.Invoke(subject,mod,now);
                if (!AbilityConditionalControls.IsCapturedStunMarker(mod) && !AbilityConditionalControls.IsCodeShockTimer(mod))
                    AbilityConditionalControls.Term(subject, mod, now);
            }
            if (expired.Count > 0)
            {
                subject.Ended(expired);
                foreach (Active marker in expired.Where(mod => AbilityConditionalControls.IsCapturedStunMarker(mod) ||
                    AbilityConditionalControls.IsCodeShockTimer(mod)))
                    AbilityConditionalControls.Term(subject, marker, now);
            }
        }

        private static void WithClient(WorldClient client, Action action)
        {
            if (client == null)
            {
                action();
                return;
            }
            WorldClient previous = Store.currentClient;
            Store.currentClient = client;
            try
            {
                action();
            }
            finally
            {
                Store.currentClient = previous;
            }
        }

        // ---- applying ----

        /// <summary>The tempmods of one use of the effect, with values rolled in their ranges.</summary>
        public static List<Active> Instantiate(TempMods.Effect effect, Random random, bool fixtureProc = false)
        {
            List<Active> mods = new List<Active>();
            foreach (TempMods.Mod m in effect.Mods)
            {
                Int32 value;
                lock (random)
                {
                    if (m.Chance < 1 && (fixtureProc ? m.Chance <= 0 : random.NextDouble() >= m.Chance)) continue;
                    value = m.MaxValue > m.MinValue ? random.Next(m.MinValue, m.MaxValue + 1) : m.MinValue;
                }
                mods.Add(new Active { Kind = m.Kind, Target = m.Target, Value = value, Seconds = m.Seconds, Proc = m.Proc,
                    Break = m.Break, BreakChance = m.BreakChance, ProtectFirstHealthChange = m.ProtectFirstHealthChange, Interval = m.Interval, DamageType = m.Proc == TempMods.Proc.Dot ? m.DamageType : -1 });
            }
            return mods;
        }

        /// <summary>
        /// ControlImmunityTimer for a control state: parent 734, target ISDrainTarget 681, kind 01, the state's whole
        /// seconds unless the ability has its own (retail: Pistol Aerial 80 bc 01 .. de 02 .. a9 02 .. 00 00 80 40, 4 s).
        /// </summary>
        public static Active ImmunityFor(TempMods.Effect effect, Active controlState)
        {
            float seconds = effect.ImmunitySeconds > 0 ? effect.ImmunitySeconds : (float)Math.Floor(controlState.Seconds);
            return new Active
            {
                Kind = TempMods.KindPlain, Target = TempMods.ISDrainTargetAbility, Value = 0, Seconds = seconds,
                Proc = TempMods.Proc.State
            };
        }

        private static readonly Random random = new Random();

        /// <summary>
        /// Puts an ability's tempmods on a subject: refresh, control immunity, then the subject's packets. Returns the
        /// tempmods that went on (none when only control states were refused).
        /// </summary>
        public static List<Active> Apply(Subject subject, TempMods.Effect effect, double now, WorldClient caster = null,
            string casterName = null, UInt16 targetView = 0, Random rng = null, Action<Active> configure = null, bool fixtureProc = false)
        {
            List<Active> mods = Instantiate(effect, rng ?? random, fixtureProc);
            AbilityConditionalControls.Init(subject,effect,mods,now);
            for (int i = mods.Count - 1; i >= 0; i--)
            {
                Active m = mods[i];
                m.Caster = caster;
                if (caster != null && m.Proc == TempMods.Proc.Dot)
                    m.Value = AbilityStats.ScaleTypedAbilityDamage(caster, m.DamageType, m.Value);
                if (m.Proc == TempMods.Proc.State && m.Seconds > 0)
                {
                    m.Seconds = AbilityControlDurations.Scale(caster, m.Target, m.Seconds);
                    // Holder uses zero for indefinite effects. A fully reduced timed state must disappear instead.
                    if (m.Seconds <= 0) { mods.RemoveAt(i); continue; }
                }
                m.CasterName = casterName;
                m.TargetView = targetView;
                configure?.Invoke(m);
                if (MobAggro.UsesEnragePursuit(effect.Ability) && subject is MobEffects &&
                    m.Proc == TempMods.Proc.State && m.Target == TempMods.EnrageAbility)
                {
                    // State 93 binds GenericEnrageUpdate to a three-second cadence.
                    m.UpdatesNpcEnrage = true;
                    m.Interval = 3;
                }
                if(m.Proc==TempMods.Proc.SeverState)AbilitySeverArteryStates.Bind(subject,m);
            }

            // Native 625ec407..47c: positive duration, on-use (mask 4), UDB0 debuff, non-self target.
            // Snapshot immunity from the unmitigated control; its timer is a separate script effect.
            Active originalControl = mods.Find(m => m.Proc == TempMods.Proc.State && TempMods.IsControlState(m.Target) &&
                !AbilityConditionalControls.InitialPowerless(effect.Ability,m.Target));
            // Zone of Powerlessness has no immunity initializer. Both retained recipient captures add 399/96 alone.
            Active unmitigatedImmunity = originalControl == null || effect.Ability == 399 ||
                effect.Ability == AbilityConditionalControls.CodeShock ? null : ImmunityFor(effect, originalControl);
            if (caster != null && !(subject is PlayerEffects playerTarget && playerTarget.Context == caster) && AbilityHostile.IsDebuff(effect.Ability))
                foreach (Active m in mods)
                    if(effect.Ability!=AbilityReverseBulldog.Ability || m.Proc!=TempMods.Proc.Terminal)
                        m.Seconds = DeflectCode.Duration(m.Seconds, caster.playerInstance.Level.getValue()[0], subject.Determination,
                            subject.DeterminationBonus,m.Proc==TempMods.Proc.State&&m.Kind==TempMods.KindState?m.Target:(ushort)0);

            Active immunity = null;
            if (subject.Effects.HasControlImmunity(now))
            {
                int refused = mods.RemoveAll(m => m.Proc == TempMods.Proc.State && TempMods.IsControlState(m.Target));
                if (refused > 0)
                {
                    Output.WriteDebugLog("[EFFECTS] " + effect.Ability + ": control immunity, state refused");
                }
            }
            else if (unmitigatedImmunity != null)
            {
                Active control = mods.Find(m => m.Proc == TempMods.Proc.State && TempMods.IsControlState(m.Target) &&
                    !AbilityConditionalControls.InitialPowerless(effect.Ability,m.Target));
                if (control != null)
                {
                    immunity = unmitigatedImmunity;
                    immunity.Caster = caster;
                    immunity.CasterName = casterName;
                    immunity.TargetView = targetView;
                }
            }

            if (mods.Count == 0)
            {
                return mods;
            }

            bool preserveOtherTargets = effect.Ability == StealthShieldAbilities.DisableEvasion ||
                effect.Ability == AbilityReverseBulldog.Ability || AbilityConditionalControls.UsesStunCallbackPresentation(effect.Ability);
            List<Active> replaced = subject.Effects.Apply(effect.Ability, mods, now, preserveOtherTargets);
            if (immunity != null)
            {
                replaced.AddRange(subject.Effects.Apply(TempMods.ControlImmunityTimerAbility, new List<Active> { immunity }, now));
            }
            Register(subject);
            foreach (Active old in replaced) AbilityConditionalControls.Term(subject, old, now);
            var visible = effect.Ability == AbilityConditionalControls.CodeShock
                ? mods.Where(mod => !AbilityConditionalControls.IsCodeShockTimer(mod)).ToList() : mods;
            subject.Applied(effect, visible, immunity, replaced);
            foreach(var marker in mods.Where(m=>m.Proc==TempMods.Proc.SeverState))AbilitySeverArteryStates.Execute(subject,marker,now);
            if (subject is PlayerEffects shieldTarget && effect.Ability == StealthShieldAbilities.DisableEvasion)
                EvasionShield.Refresh(shieldTarget.Context);
            return mods;
        }

        /// <summary>Takes everything off a subject (death), with its packets.</summary>
        public static void Clear(Subject subject)
        {
            if (subject is PlayerEffects player)
            {
                CraftingSession.End(player.Context);
                Stealth.Cleared(player.Context);
                AbilityAuras.Clear(player.Context, announce: true);
            }
            AbilityReverseBulldog.Cancel(subject);
            List<Active> removed = subject.Effects.RemoveAll();
            if (removed.Count > 0)
            {
                subject.Ended(removed);
            }
        }

        /// <summary>Remove one parent ability and send every linked modifier/state's off record.</summary>
        public static bool RemoveParent(Subject subject, UInt16 parent)
        {
            if(parent==AbilityReverseBulldog.Ability)AbilityReverseBulldog.Cancel(subject);
            List<Active> removed = subject.Effects.RemoveParent(parent);
            if (removed.Count > 0) WithClient(subject.Context, () => {
                foreach (Active mod in removed) AbilityConditionalControls.Term(subject, mod, TimeUtils.GetSimTimeSeconds());
                subject.Ended(removed);
            });

            return removed.Count > 0;
        }

        public static void HealthChanged(Subject subject, int damageTaken, bool belowMaximum, bool damageEvent = false)
        {
            if (subject is PlayerEffects player)
            {
                if (damageTaken > 0) CraftingSession.End(player.Context);
                PrisonDevices.Damage(player.Context, damageTaken);
                if (damageEvent && damageTaken > 0) AbilityCastInterrupts.OnDamage(player.Context,damageTaken);
                Stealth.HealthChanged(player.Context, damageTaken, belowMaximum);
            }
            List<Active> removed;
            lock (random) removed = subject.Effects.HealthChanged(damageTaken, belowMaximum, random);
            if (removed.Count > 0) WithClient(subject.Context, () => {
                foreach (Active mod in removed) AbilityConditionalControls.Term(subject, mod, TimeUtils.GetSimTimeSeconds());
                subject.Ended(removed);
            });
            if(subject is PlayerEffects callbackPlayer)
                WithClient(subject.Context,()=>AbilityHealthCallbacks.HealthChanged(callbackPlayer.Context,damageTaken,belowMaximum));
        }

        // ---- subjects ----

        private static readonly ConditionalWeakTable<WorldClient, PlayerEffects> players = new ConditionalWeakTable<WorldClient, PlayerEffects>();
        private static readonly ConditionalWeakTable<Mob, MobEffects> mobs = new ConditionalWeakTable<Mob, MobEffects>();

        public static PlayerEffects ForPlayer(WorldClient client)
        {
            return players.GetValue(client, c => new PlayerEffects(c));
        }

        internal static uint PlayerTacticStateFlags(WorldClient client) =>
            players.TryGetValue(client, out PlayerEffects subject) && subject.Alive
                ? subject.Effects.TacticStateFlags(TimeUtils.GetSimTimeSeconds()) : 0u;

        public static MobEffects ForMob(Mob mob)
        {
            return mobs.GetValue(mob, m => new MobEffects(m));
        }

        /// <summary>The ability lands on an NPC: its tempmods go on (<see cref="TempMods.Retail"/>). False when it has none.</summary>
        public static bool ApplyToMob(WorldClient caster, Mob mob, UInt16 targetView, UInt16 abilityId,AbilityReverseBulldog.Cast reverseBulldog=null)
        {
            if (!TempMods.TryGet(abilityId, out TempMods.Effect effect, npcCaster: caster == null) || mob == null || mob.getIsDead())
            {
                return false;
            }
            var subject=ForMob(mob);
            Apply(subject, effect, TimeUtils.GetSimTimeSeconds(), caster, PlayerName(caster), targetView,configure:AbilityReverseBulldog.Bind(reverseBulldog,subject,abilityId));
            return true;
        }

        /// <summary>An ability (an NPC's, another player's) lands on a player: its records go to them.</summary>
        public static bool ApplyToPlayer(WorldClient client, UInt16 abilityId, string casterName, WorldClient caster = null,AbilityReverseBulldog.Cast reverseBulldog=null)
        {
            if (!TempMods.TryGet(abilityId, out TempMods.Effect effect, npcCaster: caster == null))
            {
                return false;
            }
            // The casting player, when there is one, is who a death by the damage over time is owed to (PvpCombat)
            var subject=ForPlayer(client);
            Apply(subject, effect, TimeUtils.GetSimTimeSeconds(), caster, casterName,
                configure:AbilityReverseBulldog.Bind(reverseBulldog,subject,abilityId),
                fixtureProc:MartialPlayerControlQa.ModifierProc(caster, client, abilityId) ||
                    StarterInterlockControlQa.ModifierProc(caster, client, abilityId));
            return true;
        }

        internal static string PlayerName(WorldClient client)
        {
            try
            {
                return client?.playerInstance != null
                    ? StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue())
                    : "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }

    /// <summary>
    /// Tempmods on a player: retail's 80 bc records on and off, the combat-log lines, and damage over time on their own
    /// view; also the expiries of their self buffs (<see cref="AbilityBuffs"/>).
    /// </summary>
    public sealed class PlayerEffects : TimedEffects.Subject
    {
        internal override uint ConditionFlags => client.playerData.conditionStateFlags;
        internal override bool Alive => !client.playerData.isDead && !client.playerData.duelDeath;
        internal override int Determination => AbilityControlDurations.Determination(client);
        internal override int DeterminationBonus => DeflectCode.PlayerBonus(client);
        private readonly WorldClient client;
        private readonly Dictionary<UInt16, KeyValuePair<int, double>> buffEnds = new Dictionary<UInt16, KeyValuePair<int, double>>();

        public PlayerEffects(WorldClient client)
        {
            this.client = client;
        }

        internal override WorldClient Context => client;

        internal override bool Idle
        {
            get
            {
                lock (buffEnds)
                {
                    return buffEnds.Count == 0 && Effects.IsEmpty;
                }
            }
        }

        /// <summary>A timed self buff ends at <paramref name="at"/> unless that application was replaced or removed before.</summary>
        public void TrackBuff(UInt16 abilityId, int generation, double at)
        {
            lock (buffEnds)
            {
                buffEnds[abilityId] = new KeyValuePair<int, double>(generation, at);
            }
            TimedEffects.Track(this);
        }

        public void UntrackBuff(UInt16 abilityId, int generation)
        {
            lock (buffEnds)
            {
                if (buffEnds.TryGetValue(abilityId, out var end) && end.Key == generation)
                    buffEnds.Remove(abilityId);
            }
        }

        internal override void Due(double now)
        {
            List<KeyValuePair<UInt16, int>> due = new List<KeyValuePair<UInt16, int>>();
            lock (buffEnds)
            {
                foreach (KeyValuePair<UInt16, KeyValuePair<int, double>> end in buffEnds)
                {
                    if (end.Value.Value <= now)
                    {
                        due.Add(new KeyValuePair<UInt16, int>(end.Key, end.Value.Key));
                    }
                }
                foreach (KeyValuePair<UInt16, int> d in due)
                {
                    buffEnds.Remove(d.Key);
                }
            }
            foreach (KeyValuePair<UInt16, int> d in due)
            {
                if (AbilityBuffs.Remove(client, d.Key, true, false, d.Value))
                {
                    client.FlushQueue();
                }
            }
        }

        private string Name => TimedEffects.PlayerName(client);

        public static byte[] Record(TimedEffects.Active m, bool off)
        {
            // Present the targetless terminal marker and the Stun template's linked stat as captured.
            ushort target = AbilityConditionalControls.IsCapturedStunMarker(m) ? (ushort)0 : m.Target;
            bool stunTactics = AbilityConditionalControls.IsLinkedStunTactics(m);
            ushort parent = stunTactics ? TempMods.StunAbility : m.Parent;
            float seconds = stunTactics || float.IsPositiveInfinity(m.Seconds) ? 0 : m.Seconds;
            return AbilityBuffs.BuildModifier(m.Kind, m.RecordId, parent, target, m.Value, off, seconds);
        }

        /// <summary>A state's own record: the state ability as parent, its linked stat and value, no time.</summary>
        public static byte[] LinkedRecord(TimedEffects.Active m, bool off)
        {
            TempMods.LinkedRecord(m.Target, out UInt16 target, out Int32 value);
            return AbilityBuffs.BuildModifier(TempMods.KindLinked, m.LinkedRecordId, m.Target, target, value, off, 0f);
        }

        private void SendOff(IEnumerable<TimedEffects.Active> mods)
        {
            foreach (TimedEffects.Active m in mods)
            {
                if (AbilityConditionalControls.IsCodeShockTimer(m)) continue;
                client.messageQueue.addRpcMessage(Record(m, true));
                if (m.Kind == TempMods.KindState)
                {
                    client.messageQueue.addRpcMessage(LinkedRecord(m, true));
                }
            }
        }

        private void FlushStateUpdates(IEnumerable<TimedEffects.Active> changed)
        {
            if (changed.Any(mod => mod.Proc == TempMods.Proc.State && CombatInterlock.TacticStateFlag(mod.Target) != 0))
                PvpFlag.SendFlags(client);
            else
                client.FlushQueue();
        }

        internal override void Applied(TempMods.Effect effect, List<TimedEffects.Active> added, TimedEffects.Active immunity,
            List<TimedEffects.Active> replaced)
        {
            bool linkedStun = immunity != null && AbilityConditionalControls.UsesStunCallbackPresentation(effect.Ability);
            // Record ids in retail's order (morpheuzzz_001 1829, Vector: Powerless 0x14, ControlImmunityTimer 0x15,
            // Powerless's own record 0x16, ViralDeflection 0x17)
            foreach (TimedEffects.Active m in added)
            {
                m.RecordId = AbilityBuffs.NextRecordId(client.playerData);
                if (!linkedStun && immunity != null && immunity.RecordId == 0 &&
                    m.Proc == TempMods.Proc.State && TempMods.IsControlState(m.Target))
                {
                    immunity.RecordId = AbilityBuffs.NextRecordId(client.playerData);
                }
                if (m.Kind == TempMods.KindState)
                {
                    m.LinkedRecordId = AbilityBuffs.NextRecordId(client.playerData);
                }
            }

            if (immunity != null && immunity.RecordId == 0)
                immunity.RecordId = AbilityBuffs.NextRecordId(client.playerData);
            SendOff(replaced);
            // Vector sent immunity first; ApplyStunWithTimer adds the Stun before immunity.
            if (immunity != null && !linkedStun)
            {
                client.messageQueue.addRpcMessage(Record(immunity, false));
            }
            foreach (TimedEffects.Active m in added)
            {
                client.messageQueue.addRpcMessage(Record(m, false));
                if (m.Kind == TempMods.KindState)
                {
                    client.messageQueue.addRpcMessage(LinkedRecord(m, false));
                }
            }

            if (linkedStun)
                client.messageQueue.addRpcMessage(Record(immunity, false));

            string caster = (added.Count > 0 ? added[0].CasterName : immunity?.CasterName) ?? "";
            if (immunity != null && !linkedStun)
            {
                client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(AbilityMessages.ImmunityTarget, true,
                    effect.Ability, caster, Name, (Int32)immunity.Seconds));
                if ((effect.Ability == AbilityHostile.FreezeSystem3 || effect.Ability == AbilityConditionalControls.CodeShock) &&
                    immunity.Caster is WorldClient source && source != client)
                {
                    source.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(AbilityMessages.ImmunityCaster, true,
                        effect.Ability, caster, Name, (Int32)immunity.Seconds));
                    source.FlushQueue();
                }
            }
            // A virus says "infects you" only (Vector, Zone of Powerlessness); other abilities name the state
            // ("Madbent's Spinning Flower has rendered you powerless!", entilsar lastday2 143646)
            foreach (TimedEffects.Active m in added)
            {
                if (m.Proc == TempMods.Proc.State && !effect.Virus)
                {
                    if (AbilityMessages.RootWithBreakChance(m, false, caster, Name) is { } rootOutput)
                    {
                        client.messageQueue.addRpcMessage(rootOutput);
                        if (m.Caster is WorldClient source && source != client)
                        {
                            source.messageQueue.addRpcMessage(AbilityMessages.RootWithBreakChance(m, true, caster, Name));
                            source.FlushQueue();
                        }
                        continue;
                    }
                    UInt32 line = AbilityMessages.TargetStateLine(m.Target, out Int32[] values, out bool flag);
                    if (line != 0)
                    {
                        client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(line, flag, effect.Ability, caster, Name, values));
                    }
                }
            }
            if (linkedStun)
            {
                client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(AbilityMessages.ImmunityTarget, true,
                    effect.Ability, caster, Name, (Int32)immunity.Seconds));
                if (immunity.Caster is WorldClient source && source != client)
                {
                    source.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(AbilityMessages.StunCaster, true,
                        effect.Ability, caster, Name, 0));
                    source.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(AbilityMessages.ImmunityCaster, true,
                        effect.Ability, caster, Name, (Int32)immunity.Seconds));
                    source.FlushQueue();
                }
            }
            if (effect.Virus && added.Count > 0)
            {
                // "%2s's %1z infects you!" with the last tempmod's value (Code Infection 4: [866, 0]; UI Lag: [-14, 0])
                client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(AbilityMessages.InfectsTarget, false,
                    effect.Ability, caster, Name, added[added.Count - 1].Value, 0));
            }
            else if (effect.TargetLine != 0 && added.Count > 0)
            {
                int value = effect.Ability == CombatSpecials.CripplingShot
                    ? added.FirstOrDefault(modifier => modifier.Proc == TempMods.Proc.Stat && modifier.Target == TempMods.MovementSpeedAbility)?.Value ?? 0
                    : 0;
                client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(effect.TargetLine, false, effect.Ability, caster, Name, value, 0));
            }
            AbilityHostile.TellCaster(added.Count > 0 ? added[0].Caster : null, client, effect, added, Name);
            if (AbilityHostile.ChangesVitals(added) || AbilityHostile.ChangesVitals(replaced)) AbilityBuffs.RefreshVitals(client);
            var changed = added.Concat(replaced);
            AbilityBuffs.PresentTimedMovementChange(client, changed);
            FlushStateUpdates(changed);
            Output.WriteDebugLog("[EFFECTS] " + effect.Ability + " on " + Name + ": " + added.Count + " tempmods");
        }

        /// <summary>
        /// Own view after a damage-over-time tick: Health and the tick's effect with the view's counter,
        /// <c>02 00 02 80 80 80 c0 &lt;health u16&gt; 30 &lt;effect u32&gt; &lt;counter&gt;</c> (morpheuzzz_001 2699:
        /// <c>02 00 02 80 80 80 c0 77 09 30 30 04 00 28 07</c>).
        /// </summary>
        public static byte[] BuildSelfTickUpdate(UInt16 health, UInt32 effectId, byte counter)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("020002808080c0");
            pak.AddUint16(health, 1);
            pak.AddByte(0x30);
            pak.AddUint32(effectId, 1);
            pak.AddByte(counter);
            return pak.ReturnFinalPacket();
        }

        internal override bool Ticked(TimedEffects.Active mod, int damage)
        {
            if (client.playerData.isDead || client.playerData.duelDeath)
            {
                return false;
            }
            if (mod.Proc == TempMods.Proc.Pulse)
            {
                if (mod.Parent == AbilityBuffs.ForceMultiplierUpgrade &&
                    (!Alive || !AbilityBuffs.IsActive(client, mod.Parent) || !Effects.Snapshot().Contains(mod)))
                    return false;
                return AbilityHostile.Pulse(this, mod);
            }
            if (mod.Proc == TempMods.Proc.Hot)
            {
                ushort before = PvpCombat.HealthOf(client);
                ushort maximum = NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1);
                int heal = TempMods.HotAmount(before, maximum, damage);
                if (heal > 0)
                {
                    ushort after = AbilityHandler.HealSelf(client, (ushort)heal);
                    Output.WriteDebugLog($"[EFFECTS] {mod.Parent} heals {Name} for {heal}");
                    Output.WriteDebugLog($"[VITALS] char={client.playerData.getCharID()} ability={mod.Parent} HOT raw={damage} HP {before}->{after} max={maximum}");
                }
                return true;
            }
            damage = PunishingBlows.Player(mod.Caster, client, damage);
            int rawDamage = damage;
            damage = TypedAbilityResistance.Player(mod.Caster, client, mod.Parent, damage, mod.DamageType);
            UInt16 health = NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1);
            UInt16 next = (UInt16)Math.Max(0, health - damage);
            client.playerInstance.Health.setValue(next);
            TimedEffects.HealthChanged(this, health - next, next < NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1), damageEvent:true);
            next = PvpCombat.HealthOf(client); // callbacks complete before presentation/death decisions
            PvpFlag.ToWatchers(client, PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(next))));
            byte counter = (byte)client.playerData.assignSpawnIdCounter();
            client.messageQueue.addObjectMessage(BuildSelfTickUpdate(next, TempMods.VirusCrashFx, counter), false);
            client.messageQueue.addRpcMessage(AbilityMessages.Damage(false, mod.Parent, mod.CasterName ?? "", Name, damage, rawDamage - damage));
            client.FlushQueue();
            Output.WriteDebugLog("[EFFECTS] " + mod.Parent + " ticks on " + Name + " for " + damage + ": " + health + " -> " + next);
            if (next == 0)
            {
                if (mod.Caster != null && mod.Caster != client)
                {
                    PvpCombat.Defeated(client, mod.Caster); // another player's virus: a PvP death, or a duel lost
                }
                else
                {
                    CombatHandler.HandlePlayerDeath(client);
                }
                return false;
            }
            return true;
        }

        internal override void Ended(List<TimedEffects.Active> ended)
        {
            SendOff(ended);
            if (ended.Exists(m => m.Parent == StealthShieldAbilities.DisableEvasion)) EvasionShield.Refresh(client);
            if (AbilityHostile.ChangesVitals(ended)) AbilityBuffs.RefreshVitals(client);
            foreach (TimedEffects.Active m in ended)
            {
                if (m.Kind == TempMods.KindState && TempMods.StateNameId(m.Target) != 0)
                {
                    client.messageQueue.addRpcMessage(AbilityMessages.StateEnd(m.Target, Name));
                }
            }
            AbilityBuffs.PresentTimedMovementChange(client, ended);
            FlushStateUpdates(ended);
        }
    }

    /// <summary>
    /// Tempmods on an NPC. The NPC's records aren't sent to anyone (retail showed them only to the victim); stuns and
    /// pacifies stop its attacks, Dazed / Off Balance / Staggered are its tactic conditions, damage over time hits it
    /// through the caster's fight with its effect (<see cref="TempMods.VirusCrashFx"/>), and the caster gets retail's
    /// log lines.
    /// </summary>
    public sealed class MobEffects : TimedEffects.Subject
    {
        private readonly Mob mob;
        private WorldClient lastCaster;

        public MobEffects(Mob mob)
        {
            this.mob = mob;
        }

        internal override WorldClient Context => lastCaster;

        public Mob Mob => mob;
        internal override uint ConditionFlags => mob.ConditionFlags;
        internal override bool Alive => !mob.getIsDead();

        private static void Tell(WorldClient caster, byte[] line)
        {
            if (caster?.messageQueue == null)
            {
                return;
            }
            caster.messageQueue.addRpcMessage(line);
        }

        internal override void Applied(TempMods.Effect effect, List<TimedEffects.Active> added, TimedEffects.Active immunity,
            List<TimedEffects.Active> replaced)
        {
            WorldClient caster = added.Count > 0 ? added[0].Caster : immunity?.Caster;
            lastCaster = caster ?? lastCaster;
            string casterName = (added.Count > 0 ? added[0].CasterName : immunity?.CasterName) ?? "";
            string name = mob.getName() ?? "";
            LogModifiers(replaced, added: false);
            LogModifiers(added, added: true);
            if (immunity != null) LogModifier(immunity, "add");

            foreach (TimedEffects.Active m in added)
            {
                if (m.Proc != TempMods.Proc.State)
                {
                    continue;
                }
                uint flag = CombatInterlock.TacticStateFlag(m.Target);
                if (flag != 0) mob.ApplyCondition(flag, m.Seconds);
                if (effect.Virus) continue;
                if (AbilityMessages.RootWithBreakChance(m, true, casterName, name) is { } rootOutput)
                {
                    Tell(caster, rootOutput);
                    continue;
                }
                UInt32 line = AbilityMessages.CasterStateLine(m.Target, out Int32[] values);
                if (line != 0)
                {
                    Tell(caster, AbilityMessages.AboutAbility(line, true, effect.Ability, casterName, name, values));
                }
            }
            if (immunity != null)
            {
                // "%3s will be immune to stun and pacify for %4I seconds." (Rajko SomethingMaraC 54689: [4])
                Tell(caster, AbilityMessages.AboutAbility(AbilityMessages.ImmunityCaster, true, effect.Ability, casterName,
                    name, (Int32)immunity.Seconds));
            }
            if (effect.Virus && added.Count > 0)
            {
                // "Your Plague Zone 2.0 infects Zombie Lurch!" [424, 0] (actions_combat 16472)
                Tell(caster, AbilityMessages.AboutAbility(AbilityMessages.InfectsCaster, true, effect.Ability, casterName,
                    name, added[added.Count - 1].Value, 0));
            }
            else if (effect.CasterLine != 0 && added.Count > 0)
            {
                Tell(caster, AbilityMessages.AboutAbility(effect.CasterLine, true, effect.Ability, casterName, name, 0, 0));
            }
            caster?.FlushQueue();
            Output.WriteDebugLog("[EFFECTS] " + effect.Ability + " on " + name + ": " +
                                 string.Join(", ", added.ConvertAll(a => a.Proc + " " + a.Target + " " + a.Value + " " + a.Seconds + " s")));
        }

        internal override bool Ticked(TimedEffects.Active mod, int damage)
        {
            WorldClient caster = mod.Caster;
            if (mod.UpdatesNpcEnrage)
            {
                MobAggro.EnragePulse(mob, mod);
                return !mob.getIsDead();
            }
            if (mod.Proc == TempMods.Proc.Pulse) return !mob.getIsDead() && AbilityHostile.Pulse(this, mod);
            if (caster == null || mob.getIsDead())
            {
                return false;
            }
            damage = PunishingBlows.Npc(caster, mob, damage);
            int rawDamage = damage;
            damage = TypedAbilityResistance.Npc(caster, mob, mod.Parent, damage, mod.DamageType);
            // Each target keeps its DOT even when the caster's current fight changes. Death claiming and rewards
            // use the same path as a direct ability, so a secondary target cannot award its kill twice.
            if (!CombatManager.GetHandler(caster).ApplyAbilityToNpc(mob, mod.TargetView, (UInt16)Math.Min(damage, UInt16.MaxValue),
                    TempMods.VirusCrashFx))
            {
                return false;
            }
            Tell(caster, AbilityMessages.Damage(true, mod.Parent, mod.CasterName ?? "", mob.getName() ?? "", damage, rawDamage - damage));
            caster.FlushQueue();
            Output.WriteDebugLog("[EFFECTS] " + mod.Parent + " ticks on " + mob.getName() + " for " + damage);
            return !mob.getIsDead();
        }

        private void LogModifiers(List<TimedEffects.Active> mods, bool added)
        {
            string operation = added ? "add" : "remove";
            foreach (var mod in mods.Where(mod => !AbilityConditionalControls.IsCodeShockTimer(mod))) LogModifier(mod, operation);
        }

        private void LogModifier(TimedEffects.Active mod, string operation) =>
            Output.WriteDebugLog(FormattableString.Invariant(
                $"[MODIFIER] mob='{mob.getName()}' entity={mob.getEntityId()} source={mod.Parent} stat={mod.Target} {operation} value={mod.Value:+0;-0;0} duration={mod.Seconds:G}s"));

        internal override void Ended(List<TimedEffects.Active> ended)
        {
            LogModifiers(ended, added: false);
            // Enrage expiry only ends its pursuit. Other expirations may resume ordinary retaliation
            // once nothing else is fighting the NPC.
            if (mob.CombatantCount == 0 && mob.getHealthC() > 0 && mob.getHealthC() < mob.getHealthM() &&
                ended.Exists(mod => !mod.UpdatesNpcEnrage && !AbilityConditionalControls.IsCodeShockTimer(mod)))
                MobAggro.Retaliate(lastCaster, mob);
        }
    }
}
