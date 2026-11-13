using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Hostile templates (viruses, fields, debuffs) and the scripts that remove effect families, built on the
    /// <see cref="TimedEffects"/> tempmod holder rather than a parallel system.
    /// <list type="bullet">
    /// <item>Family: <c>abilitylib.isADebuffAbility</c> is client.dll 62515f30 (Python binding 625167b0): StatusCategory
    /// "UDB0" (0x30424455). <c>isABuffAbility</c> is 62515ef0: "UBF0" (<see cref="AbilityCancellation.BuffCategory"/>).</item>
    /// <item>Numeric debuffs: each fixed, timed stat modifier of a UDB0 template becomes a stat tempmod (kind 0x15, as UI Lag
    /// 1.0's captured records); a <c>*DOT</c> update function becomes damage over time on DOTPlaceHolder (kind 0x1d, as Code
    /// Infection), ticking at the template's UpdateFuncInterval with <see cref="TempMods.DotTick"/>.</item>
    /// <item>Lines: a template whose ActivationStringTarget is 0xc600001a ("%2s's %1z infects you!") is a virus, like the
    /// captured UI Lag 1.0, Vector and Code Infection 4. Others send their own Subject/Target strings
    /// (Miasma 2.0: 0xc4000036 "%3s is caught in your Miasma field!").</item>
    /// <item>Sweeps (tempmods.NegativeConditionSweepPulse): every UpdateFuncInterval each debuff parent on the recipient
    /// is removed with Value% chance, except dispelExemptAbilities.</item>
    /// <item>Delete/Cancel Upgrade (RemoveRandomBuff, UpgradeSweepPulse): removable buff parents only (UBF0,
    /// CanNOTRemoveBuff 0, not exempt), including toggled auras (deactivateAbility).</item>
    /// </list>
    /// </summary>
    public static class AbilityHostile
    {
        public const uint DebuffCategory = 0x30424455; // "UDB0"

        // stringtable_client (unnamed rows; text matches the script's Send*Output arguments)
        public const uint RemoveDebuffTarget = 0xc60022fe;       // "%1z removes %4o from you!" (ID_CLIENT_ABILITY_REMOVE_DEBUFF_ABILITY)
        public const uint CounterUpgradeCaster = 0xc4000026;     // "Your %1z removes %4o from %3s!"
        public const uint CounterUpgradeTarget = 0xc4000027;     // "%2s's %1z removes %4o permanently!"
        public const uint CancelUpgradeFailCaster = 0xc6000197;  // "Your %1z could not find any upgrades to remove from %3s!"
        public const uint CancelUpgradeFailTarget = 0xc6000198;  // "%2s's %1z could not find any upgrades on you to remove!"
        /// <summary>FX_CHARACTER_ANTIVIRAL_SWEEP (fxlisthex.txt), played by a sweep that removes something.</summary>
        public const uint AntiviralSweepFx = 0x28000630;

        /// <summary>tempmods.py line 8 dispelExemptAbilities, resolved by name from the shipped catalog.</summary>
        private static readonly Lazy<HashSet<ushort>> exempt = new Lazy<HashSet<ushort>>(() =>
        {
            var names = new HashSet<string> { "DeathEffectAbility", "SittingEffectAbility", "WeaponReqsNotMetEffectAbility",
                "CombatEffectAbility", "NPCShowInterlockImmuneAbility", "Event2_0FlyVirusImmunityAbility",
                "ControlImmunityTimerAbility", "KarateAbility", "SelfDefenseAbility", "HackerCombatStyleAbility",
                "AikidoAbility", "HandgunsAbility", "KungFuAbility", "RiflesAbility", "SubmachinegunsAbility" };
            var ids = new HashSet<ushort>(AbilityCatalog.All.Values.Where(e => names.Contains(e.Name)).Select(e => e.Id));
            ids.Add(TempMods.ControlImmunityTimerAbility);
            return ids;
        });

        public static bool Exempt(ushort id) => exempt.Value.Contains(AbilityCatalog.Canonical(id));
        public static bool IsDebuff(ushort id) => AbilityCatalog.Find(id)?.Number("Ability/StatusCategory") == DebuffCategory;
        public static bool IsBuff(ushort id) => AbilityCatalog.Find(id)?.Number("Ability/StatusCategory") == AbilityCancellation.BuffCategory;
        /// <summary>RemoveRandomBuff's candidate test: isABuffAbility, canRemoveBuff, not dispel-exempt.</summary>
        public static bool RemovableBuff(ushort id) => IsBuff(id) && AbilityCatalog.Find(id)?.Number("Ability/CanNOTRemoveBuff") == 0 && !Exempt(id);
        public static bool Cleansable(ushort id) => IsDebuff(id) && !Exempt(id);

        private static bool Fixed(AbilityCatalog.Modifier m) => m.Has("MinValue") && m.Number("MinValue") == m.Number("MaxValue", m.Number("MinValue")) &&
            !m.Has("ValueScale") && m.Number("ValueFunction") == 0;
        private static bool Scripted(AbilityCatalog.Modifier m, bool allowUpdate = false) =>
            !string.IsNullOrEmpty(m.Text("StatModInitFunc")) || !string.IsNullOrEmpty(m.Text("StatModTermFunc")) ||
            !string.IsNullOrEmpty(m.Text("StatModHealthChangedFunc")) || !allowUpdate && !string.IsNullOrEmpty(m.Text("StatModUpdateFunc"));
        private static bool Timed(AbilityCatalog.Modifier m) => m.Number("DurationType", -1) == 0 && m.Number("Duration") > 0;

        /// <summary>One modifier of a hostile template as a tempmod, or null when it is not a numeric/DOT/buffer modifier.</summary>
        public static TempMods.Mod ModOf(ushort ability, AbilityCatalog.Modifier m)
        {
            if (m.Number("WhenEnabled", -1) != 2 || m.State != 0 || !Timed(m)) return null;
            float seconds = (float)m.Number("Duration");
            int value = (int)m.Number("MinValue");
            if (m.Text("StatModUpdateFunc").EndsWith("DOT", StringComparison.Ordinal) && !Scripted(m, true) && Fixed(m))
                return new TempMods.Mod { Kind = TempMods.KindLinked, Target = TempMods.DOTPlaceHolderAbility, MinValue = value, MaxValue = value,
                    Seconds = seconds, Proc = TempMods.Proc.Dot, Interval = m.Number("UpdateFuncInterval", TempMods.DotIntervalSeconds), DamageType = TypedAbilityResistance.ModifierType(m,true) };
            // BufferOverflowInit/Update/Term fill the target's native buff buffer; the tempmod and its record are kept here
            if (m.Text("StatModInitFunc") == "BufferOverflowInit" && Fixed(m))
                return new TempMods.Mod { Kind = TempMods.KindFx, Target = ability, MinValue = value, MaxValue = value, Seconds = seconds,
                    Proc = TempMods.Proc.State };
            ushort target = m.Target;
            if (target == 0 || target == 108 || target == TempMods.DOTPlaceHolderAbility || Scripted(m) || !Fixed(m)) return null;
            return new TempMods.Mod { Kind = TempMods.KindStat, Target = target, MinValue = value, MaxValue = value, Seconds = seconds,
                Proc = TempMods.Proc.Stat };
        }

        /// <summary>The numeric and DOT tempmods of a UDB0 template (states stay in <see cref="AbilityCatalog.Controls"/>).</summary>
        public static TempMods.Effect Catalog(ushort id)
        {
            var entry = AbilityCatalog.Find(id);
            if (entry == null || entry.Number("Ability/StatusCategory") != DebuffCategory) return null;
            var mods = new List<TempMods.Mod>();
            uint subject = 0, target = 0;
            foreach (var m in entry.Modifiers)
            {
                if (m.Number("WhenEnabled", -1) != 2) continue;
                if (subject == 0) subject = (uint)m.Number("ActivationStringSubject");
                if (target == 0) target = (uint)m.Number("ActivationStringTarget");
                var mod = ModOf(entry.Id, m);
                if (mod != null) mods.Add(mod);
            }
            if (mods.Count == 0) return null;
            bool virus = target == AbilityMessages.InfectsTarget && !entry.DirectDamage;
            return new TempMods.Effect { Ability = entry.Id, Mods = mods.ToArray(), Virus = virus,
                CasterLine = virus ? 0 : subject, TargetLine = virus ? 0 : target };
        }

        public static TempMods.Effect Merge(TempMods.Effect controls, TempMods.Effect hostile)
        {
            if (hostile == null) return controls;
            if (controls == null) return hostile;
            return new TempMods.Effect { Ability = controls.Ability, Mods = controls.Mods.Concat(hostile.Mods).ToArray(),
                ImmunitySeconds = controls.ImmunitySeconds, Virus = hostile.Virus, CasterLine = hostile.CasterLine, TargetLine = hostile.TargetLine };
        }

        public static bool ChangesVitals(IEnumerable<TimedEffects.Active> mods) =>
            mods != null && mods.Any(m => m.Proc == TempMods.Proc.Stat && (m.Target == 106 || m.Target == 109));

        /// <summary>The caster's line when their hostile effect lands on another player.</summary>
        internal static void TellCaster(WorldClient caster, WorldClient victim, TempMods.Effect effect, List<TimedEffects.Active> added, string victimName)
        {
            if (caster?.messageQueue == null || caster == victim || added.Count == 0) return;
            string name = TimedEffects.PlayerName(caster);
            if (effect.Virus)
                caster.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(AbilityMessages.InfectsCaster, true, effect.Ability, name,
                    victimName, added[added.Count - 1].Value, 0));
            else if (effect.CasterLine != 0)
                caster.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(effect.CasterLine, true, effect.Ability, name, victimName, 0, 0));
            else return;
            caster.FlushQueue();
        }

        // ---- instant scripted effects ----

        private static IEnumerable<AbilityCatalog.Modifier> Enabled(ushort id) =>
            AbilityCatalog.Find(id)?.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2) ?? Enumerable.Empty<AbilityCatalog.Modifier>();

        private static bool HasInit(ushort id, string func) => Enabled(id).Any(m => m.Text("StatModInitFunc") == func);

        /// <summary>Hostile abilities whose only effect may be an instant script (Delete/Cancel Upgrade) still land on their target.</summary>
        public static bool Lands(ushort id) => IsDebuff(id) && (HasInit(id, "RemoveRandomBuff") || HasInit(id, "UpgradeSweepPulse") ||
            Enabled(id).Any(m => m.Number("DurationType", -1) == 1 && m.Target == 111 && Fixed(m)));

        private static readonly Random random = new Random();

        private static int Roll(Random rng, int max) { lock (rng) return rng.Next(max); }

        /// <summary>The hostile ability has landed on another player (after its tempmods).</summary>
        public static void LandedOnPlayer(WorldClient caster, WorldClient target, ushort id, Random rng = null)
        {
            // Existing disposable dev-fixture guard only; ordinary servers retain this exact random object/path.
            rng = DispelCallbackRandom(rng);
            foreach (var m in Enabled(id))
            {
                // Destroy Resolve: Current Inner Strength -58, DurationType 1 (instant)
                if (m.Number("DurationType", -1) == 1 && m.Target == 111 && Fixed(m) && !target.playerData.isDead)
                {
                    int current = AbilityHandler.CurrentInnerStrength(target);
                    ushort next = (ushort)Math.Clamp(current + (int)m.Number("MinValue"), 0, AbilityHandler.MaxInnerStrength(target));
                    target.playerInstance.InnerStrengthAvailable.setValue(next);
                    new ServerPackets().sendISCurrent(target, next);
                    Output.WriteDebugLog("[HOSTILE] " + id + " IS " + current + " -> " + next + " on " + TimedEffects.PlayerName(target));
                }
            }
            foreach (var m in Enabled(id))
            {
                string callback = m.Text("StatModInitFunc");
                if (callback != "RemoveRandomBuff" && callback != "UpgradeSweepPulse") continue;
                // Machinegun Fist Combo 261 explicitly stores 0.2. Use the same probability convention
                // as TimedEffects.Apply; omitted ProcTriggerChance retains the existing always-on policy.
                if (!DispelProcs(m, rng)) continue;
                if (callback == "RemoveRandomBuff") RemoveRandomBuff(caster, target, id, rng);
                else UpgradeSweep(caster, target, id, (int)m.Number("MinValue"), rng);
            }
        }

        // This override belongs only to positive dispel callbacks. It never reaches combat rolls,
        // special-move contests, health callbacks or negative-condition term sweeps.
        internal static Random DispelCallbackRandom(Random supplied = null)
        {
            if (supplied != null) return supplied;
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1" &&
                (Environment.GetEnvironmentVariable("MXO_DB_NAME") ?? "").StartsWith("mxotest_",StringComparison.Ordinal) &&
                int.TryParse(Environment.GetEnvironmentVariable("MXO_BUFF_DISPEL_QA_ROLL100"),out int roll) && roll >= 0 && roll <= 99)
                return new DispelQaRandom(roll);
            return random;
        }
        private sealed class DispelQaRandom : Random
        {
            private readonly int roll;
            public DispelQaRandom(int roll) { this.roll=roll; }
            public override double NextDouble()=>roll/100.0;
            public override int Next(int maximum)=>Math.Min(roll,Math.Max(0,maximum-1));
        }

        private static bool DispelProcs(AbilityCatalog.Modifier modifier, Random rng)
        {
            double chance = modifier.Number("ProcTriggerChance", 1);
            lock (rng) return chance >= 1 || rng.NextDouble() < chance;
        }

        /// <summary>The hostile ability has landed on an NPC. NPCs carry no removable buffs here, so a triggered removal script fails.</summary>
        public static void LandedOnMob(WorldClient caster, Mob mob, ushort id, Random rng = null)
        {
            var modifier = Enabled(id).FirstOrDefault(m => m.Text("StatModInitFunc") == "RemoveRandomBuff");
            if (modifier == null || caster?.messageQueue == null || !DispelProcs(modifier, DispelCallbackRandom(rng))) return;
            caster.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(CancelUpgradeFailCaster, true, id, TimedEffects.PlayerName(caster), mob.getName() ?? "", 0));
            caster.FlushQueue();
        }

        /// <summary>The removable buff parents on a player: self/friendly buffs, buff tempmods and toggled auras.</summary>
        public static List<ushort> RemovableBuffs(WorldClient target)
        {
            var ids = new List<ushort>();
            lock (target.playerData.activeBuffs) ids.AddRange(target.playerData.activeBuffs.Keys);
            ids.AddRange(TimedEffects.ForPlayer(target).Effects.Snapshot().Select(a => a.Parent));
            ids.AddRange(AbilityAuras.Active(target));
            return ids.Distinct().Where(RemovableBuff).ToList();
        }

        /// <summary>Takes a buff parent off a player (subject.AbilityInv.removeTempMod + deactivateAbility).</summary>
        public static bool RemoveBuff(WorldClient target, ushort id)
        {
            bool removed = AbilityBuffs.Remove(target, id);
            removed |= TimedEffects.RemoveParent(TimedEffects.ForPlayer(target), id);
            removed |= AbilityAuras.Stop(target, id);
            target.FlushQueue();
            return removed;
        }

        /// <summary>
        /// tempmods.RemoveRandomBuff: one buff, picked by <c>random.randrange(0, n - 1)</c> when there are several (the script
        /// never picks the last candidate), or the fail lines when there is none.
        /// </summary>
        public static ushort RemoveRandomBuff(WorldClient caster, WorldClient target, ushort ability, Random rng)
        {
            var buffs = RemovableBuffs(target);
            string casterName = TimedEffects.PlayerName(caster), targetName = TimedEffects.PlayerName(target);
            if (buffs.Count == 0)
            {
                caster?.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(CancelUpgradeFailCaster, true, ability, casterName, targetName, 0));
                target.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(CancelUpgradeFailTarget, false, ability, casterName, targetName, 0));
                caster?.FlushQueue(); target.FlushQueue();
                Output.WriteDebugLog("[HOSTILE] " + ability + " found no upgrade on " + targetName);
                return 0;
            }
            ushort chosen = buffs[buffs.Count > 1 ? Roll(rng, buffs.Count - 1) : 0];
            Removed(caster, target, ability, chosen);
            return chosen;
        }

        /// <summary>tempmods.UpgradeSweepPulse: each removable buff with Value% chance.</summary>
        public static List<ushort> UpgradeSweep(WorldClient caster, WorldClient target, ushort ability, int chance, Random rng)
        {
            var removed = new List<ushort>();
            foreach (ushort id in RemovableBuffs(target))
                if (Roll(rng, 100) < chance) { Removed(caster, target, ability, id); removed.Add(id); }
            return removed;
        }

        private static void Removed(WorldClient caster, WorldClient target, ushort ability, ushort buff)
        {
            RemoveBuff(target, buff);
            string casterName = TimedEffects.PlayerName(caster), targetName = TimedEffects.PlayerName(target);
            int goid = unchecked((int)AbilityMessages.AbilityGameObject(buff));
            caster?.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(CounterUpgradeCaster, true, ability, casterName, targetName, goid));
            target.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(CounterUpgradeTarget, false, ability, casterName, targetName, goid));
            caster?.FlushQueue(); target.FlushQueue();
            Output.WriteDebugLog("[HOSTILE] " + ability + " removes buff " + buff + " from " + targetName);
        }

        // ---- sweeps ----

        private static AbilityCatalog.Modifier SweepMod(ushort id) => Enabled(id).FirstOrDefault(m => m.Text("StatModUpdateFunc") == "NegativeConditionSweepPulse");

        public static bool IsSweep(ushort id) => id == 868 || SweepMod(id) != null;

        /// <summary>
        /// Puts a sweep on a friendly player: a pulse tempmod (kind 0x09, as DeflectVirus1's proc record) for the stored
        /// Duration, every UpdateFuncInterval. Sweeps of the same StackingType replace each other.
        /// </summary>
        public static bool ApplySweep(WorldClient caster, WorldClient target, ushort id)
        {
            if (id == 868)
            {
                // Purity's one init callback, not a persistent periodic sweep.
                Pulse(TimedEffects.ForPlayer(target),new TimedEffects.Active { Parent=id, Value=25 });
                return true;
            }
            var m = SweepMod(id);
            if (m == null || !Timed(m)) return false;
            var subject = TimedEffects.ForPlayer(target);
            double stacking = AbilityCatalog.Find(id).Number("Ability/StackingType");
            foreach (ushort other in subject.Effects.Snapshot().Select(a => a.Parent).Distinct().ToList())
                if (other != id && IsSweep(other) && AbilityCatalog.Find(other)?.Number("Ability/StackingType") == stacking)
                    TimedEffects.RemoveParent(subject, other);
            int value = (int)m.Number("MinValue");
            var effect = new TempMods.Effect { Ability = AbilityCatalog.Canonical(id), Mods = new[] { new TempMods.Mod
            {
                Kind = TempMods.KindFx, Target = AbilityCatalog.Canonical(id), MinValue = value, MaxValue = value, Seconds = (float)m.Number("Duration"),
                Proc = TempMods.Proc.Pulse, Interval = m.Number("UpdateFuncInterval", 5)
            } } };
            TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds(), caster, TimedEffects.PlayerName(caster));
            if (m.Text("StatModInitFunc") == "NegativeConditionSweepPulse")
                Pulse(subject, new TimedEffects.Active { Parent = id, Value = value });
            return true;
        }

        public static Random PulseRandom = random;

        /// <summary>tempmods.NegativeConditionSweepPulse on one subject.</summary>
        internal static bool Pulse(TimedEffects.Subject subject, TimedEffects.Active pulse, Random rng=null, Action<string> removedLog=null)
        {
            var parents = subject.Effects.Snapshot().Select(a => a.Parent).Distinct().Where(Cleansable).ToList();
            WorldClient client = subject is PlayerEffects ? subject.Context : null;
            string name = client != null ? TimedEffects.PlayerName(client) : "";
            foreach (ushort parent in parents)
            {
                if (Roll(rng??PulseRandom, 100) >= pulse.Value) continue;
                bool live = subject.Effects.Snapshot().Any(m => m.Parent == parent && m.Expires > TimeUtils.GetSimTimeSeconds());
                if (!TimedEffects.RemoveParent(subject, parent)) continue;
                string target = client != null ? name : subject is MobEffects mob ? mob.Mob.getName() : subject.GetType().Name;
                if (live) (removedLog ?? Output.WriteDebugLog)("[SWEEP] " + pulse.Parent + " removed " + parent + " from " + target);
                if (client == null) continue;
                client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(RemoveDebuffTarget, false, pulse.Parent, name, name,
                    unchecked((int)AbilityMessages.AbilityGameObject(parent))));
                ServerPackets.SendPlayerAbilityEffect(client, AntiviralSweepFx);
                client.FlushQueue();
            }
            return true;
        }

        // ---- area ----

        /// <summary>Other players inside an area who are legal hostile targets (PvP rules) and visible to the caster.</summary>
        public static List<WorldClient> PlayersInArea(WorldClient caster, double x, double y, double z, double radiusMetres, WorldClient skip = null)
        {
            var trace=AreaTargetDiagnostics.Enabled?new List<string>():null;
            var found=SocialHandler.OnlineWhere(c=>
            {
                string reason=PlayerAreaReason(caster,c,x,y,z,radiusMetres,skip,out var verdict);
                if(trace!=null)trace.Add(AreaTargetDiagnostics.Describe(caster,c,reason,verdict,x,y,z,radiusMetres));
                return reason=="included";
            });
            // OnlineWhere holds the client-list lock; keep diagnostic I/O outside it.
            if(trace!=null)foreach(string line in trace)AreaTargetDiagnostics.Write(line);
            return found;
        }

        /// <summary>The original area predicate, in its original short-circuit order; no PvP override.</summary>
        public static string PlayerAreaReason(WorldClient caster,WorldClient candidate,double x,double y,double z,
            double radiusMetres,WorldClient skip,out PvpRules.Verdict? verdict)
        {
            verdict=null;
            if(candidate==caster)return "caster";
            if(candidate==skip)return "primary";
            if(candidate.playerData.isDead)return "dead";
            if(candidate.playerData.duelDeath)return "duel-death";
            if(candidate.playerData.getDistrictId()!=caster.playerData.getDistrictId())return "district";
            double tx=0,ty=0,tz=0;
            NumericalUtils.LtVector3dToDoubles(candidate.playerInstance.Position.getValue(),ref tx,ref ty,ref tz);
            if(!AbilityTargets.WithinArea(x,y,z,tx,ty,tz,radiusMetres))
                return Math.Abs(ty-y)>=150?"floor":"radius";
            if(caster.viewMan.FindViewForEntity(candidate.playerData.getEntityId())==null)return "invisible";
            verdict=PvpCombat.Check(caster,candidate);
            return verdict==PvpRules.Verdict.Allowed?"included":"pvp-"+verdict.Value;
        }

        /// <summary>Hostile live NPCs inside an area that the caster can see, with their view ids.</summary>
        public static List<(Mob Mob, ushort View)> MobsInArea(WorldClient caster, double x, double y, double z, double radiusMetres, Mob skip = null)
        {
            var found = new List<(Mob, ushort)>();
            uint district = caster.playerData.getDistrictId();
            lock (WorldServer.mobs.SyncRoot)
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (mob == skip || mob.isFriendly || mob.getIsDead() || mob.getDistrict() != district) continue;
                    var view = caster.viewMan.FindViewForEntity(mob.getEntityId());
                    if (view != null && AbilityTargets.WithinArea(x, y, z, mob.getXPos(), mob.getYPos(), mob.getZPos(), radiusMetres))
                        found.Add((mob, view.ViewID));
                }
            return found;
        }

        /// <summary>An instant health modifier (Static Field's effect: viral, DurationType 1) as raw damage, scaled by the caster's influence.</summary>
        public static ushort InstantDamage(WorldClient caster, ushort id)
        {
            var m = Enabled(id).FirstOrDefault(p => p.Target == 108 && p.Number("DamageType", -1) != 3 && p.Number("DurationType", -1) == 1 && Fixed(p));
            return m == null ? (ushort)0 : AbilityStats.ScaleAbilityDamage(caster, id, (int)m.Number("MinValue"));
        }
    }
}
