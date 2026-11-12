using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Spy stealth: Sneak, Escape and Evasion, Concealment, Invisibility (own and imparted), Vanish and Master Shadow.
    /// See docs/STEALTH.md for the evidence behind each number and what is our choice.
    /// <list type="bullet">
    /// <item>The state reaches the client as DissemblingType (own view attribute 36, other views 27), a bit mask whose
    /// values are client.dll's own Python constants (0x62521e2f): kSneak 0x02, kInvisible 0x04, kDisguised 0x08,
    /// kShadowObject 0x10 (kSpeeding 0x400 and kHyperSpeed 0x1000 match the retail Hyper-Sprint and Hyper-Speed
    /// captures). The client's CharMvt handler (0x625bcfa4) plays "invis_self_loop" on any character with kSneak or
    /// kInvisible and "shadow_object_loop" for kShadowObject, on whichever view carries the bit.</item>
    /// <item>The StealthState record also sets ConditionStateFlags ST_STEALTHED 0x02000000 and Concealed sets
    /// ST_CONCEALMENT 0x00040000 (client.dll condition table 0x6269ef26; the same table gives ST_PVP 0x01000000 and
    /// ST_SITTING_EFFECT 0x00200000, which retail captures confirm).</item>
    /// <item>Detection is abilitylib.sneakTest (client.dll 0x62513950), applied by ability/utility.py SneakTest every
    /// UpdateFuncInterval (5 s) while sneaking.</item>
    /// </list>
    /// </summary>
    public static class Stealth
    {
        // DissemblingType bits (client.dll 0x62521e2f)
        public const UInt16 Sneak = 0x02, Invisible = 0x04, Disguised = 0x08, ShadowObject = 0x10;
        // ConditionStateFlags (client.dll 0x6269f270, 0x6269f41b)
        public const UInt32 ConcealmentFlag = 0x00040000, StealthedFlag = 0x02000000;

        // Ability ids (data/abilityIDs.csv)
        public const UInt16 SneakGrantAbility = 1111, ImprovedSneakAbility = 186, EscapeAndEvasionAbility = 507,
            ConcealmentGrantAbility = 1137, InvisibilityGrantAbility = 1116, InvisibilityImpartedAbility = 629,
            ImpartInvisibilityAbility = 366, VanishAbility = 368, MasterShadowAbility = 1118;
        public const UInt16 StealthAbility = 209, StealthDetectionAbility = 204, NoiseLevelBuffTargetAbility = 509,
            ConcealedAbility = 623, StealthStateAbility = 881, MovementSpeedAbility = 115, SneakMovementAbility = 877,
            ISDrainTargetAbility = 681;

        // ability/utility.py SneakTest / InitialSneakTest
        public const double FrustumDegrees = 60.0, MaxDetectUnits = 2000.0, MinDetectUnits = 300.0, MaxSneakLevel = 50.0;
        /// <summary>UpdateFuncInterval of Sneak's StatModifier (catalog), SneakUpdate's period.</summary>
        public const double UpdateSeconds = 5.0;
        /// <summary>misc.py InvisibilityGrant_Test / DisguiseGrant_Test: getEnemiesInRadius(800).</summary>
        public const double InvisibilityProximityUnits = 800.0;
        /// <summary>misc.py INVISIBILITY_BASE_TIME and InvisibilityImparted_DirectObject's duration.</summary>
        public const double InvisibilityBaseSeconds = 30.0, ImpartedSeconds = 120.0;
        /// <summary>VanishAbility's StatModifier Duration: VanishInit (its term function) runs 2 s after the use.</summary>
        public const double VanishDelaySeconds = 2.0;
        /// <summary>MasterShadowActive: 180 s, ISDrainWithAbilityDeactivation every 5 s taking its value 25.</summary>
        public const double ShadowSeconds = 180.0, ShadowDrainSeconds = 5.0;
        public const int ShadowDrain = 25;
        /// <summary>NPCDetection_DistanceFactor and NPCDetection_NoiseFactor console variables, both 1.0 by default (0x628190b0).</summary>
        public const float DistanceFactor = 1.0f, NoiseFactor = 1.0f;

        /// <summary>
        /// abilitylib.sneakTest(StealthAwareness, StealthLevel, NoiseLevel, distance), client.dll 0x62513950: with no
        /// stealth level the sneaker is always seen; otherwise seen when
        /// <c>max(0, dist/100 - 3) * DistanceFactor - noise * NoiseFactor + 2 * SL + roll &lt; 2 * SA + 50</c>,
        /// roll = (rand() * 100) &gt;&gt; 15, 0..99. Arguments are bytes in the native call.
        /// </summary>
        public static bool SneakTest(int awareness, int stealthLevel, int noise, float distanceUnits, int roll)
        {
            byte sa = (byte)awareness, sl = (byte)stealthLevel, nl = (byte)noise;
            if (sl == 0) return true;
            float metres = distanceUnits / 100f - 3f;
            if (metres <= 0) metres = 0;
            float left = metres * DistanceFactor - nl * NoiseFactor + 2 * sl + roll;
            return left < 2 * sa + 50;
        }

        /// <summary>The native roll: rand() is 0..32767.</summary>
        public static int Roll(Random random) { lock (random) return random.Next(32768) * 100 >> 15; }

        /// <summary>utility.py: <c>300 + (2000 - 2000 * (min(level, 50) / 50))</c> units.</summary>
        public static double DetectionRangeUnits(int sneakLevel)
        {
            double level = Math.Min(MaxSneakLevel, Math.Max(0, sneakLevel));
            return MinDetectUnits + (MaxDetectUnits - MaxDetectUnits * (level / MaxSneakLevel));
        }

        /// <summary>
        /// utility.py isObjWithinFrust: the target is ahead of the observer (dot &gt; 0) and less than
        /// <paramref name="degrees"/> off its facing (the script compares with the full width, not half). Yaw is the
        /// 256-step byte of <see cref="MobAggro.YawTowards"/>.
        /// </summary>
        public static bool InFrustum(double ox, double oz, byte yaw, double tx, double tz, double degrees = FrustumDegrees)
        {
            double dx = tx - ox, dz = tz - oz, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) return true;
            double angle = yaw / 256.0 * 2 * Math.PI;
            double dot = (dx * Math.Sin(angle) + dz * Math.Cos(angle)) / len;
            if (dot < 0) return false;
            return Math.Acos(Math.Min(1, dot)) * 180 / Math.PI < degrees;
        }

        /// <summary>misc.py InvisibilityGrant_DirectObject: 30 s + 2 s a level (+ InvisDuration, not a player ability here).</summary>
        public static double InvisibilitySeconds(int level) => InvisibilityBaseSeconds + Math.Max(0, level) * 2;

        // ---- definitions ----

        public enum Kind { Sneak, Invisibility, Imparted, Shadow }

        public sealed class Def
        {
            public UInt16 Ability;
            public Kind Kind;
            public UInt16 Bits;
            public UInt32 Flags;
            /// <summary>OnHealthReduceDeactivateAbility on one of its modifiers.</summary>
            public bool BreaksOnDamage;
            /// <summary>SneakUpdate / ConcealUpdate: Utility.SneakTest every 5 s.</summary>
            public bool Periodic;
            public bool Toggle;
        }

        public static readonly Dictionary<UInt16, Def> Defs = new Dictionary<UInt16, Def>
        {
            // Sneak: Stealth (fn 2), Noise +10, StealthState, Movement Speed -200; SneakInit activateSneak (kSneak)
            { SneakGrantAbility, new Def { Ability = SneakGrantAbility, Kind = Kind.Sneak, Bits = Sneak, Flags = StealthedFlag, BreaksOnDamage = true, Periodic = true, Toggle = true } },
            // Improved Sneak: a toggle with no stored modifier; our choice is Sneak's behaviour under its own id
            { ImprovedSneakAbility, new Def { Ability = ImprovedSneakAbility, Kind = Kind.Sneak, Bits = Sneak, Flags = StealthedFlag, BreaksOnDamage = true, Periodic = true, Toggle = true } },
            // Escape and Evasion: activateSneak, Sneak Movement 25, Stealth (fn 3), StealthState, SneakUpdate
            { EscapeAndEvasionAbility, new Def { Ability = EscapeAndEvasionAbility, Kind = Kind.Sneak, Bits = Sneak, Flags = StealthedFlag, BreaksOnDamage = true, Periodic = true, Toggle = true } },
            // Concealment: addDissemblingType(kSneak), Stealth (fn 2), Concealed + StealthState, ConcealUpdate
            { ConcealmentGrantAbility, new Def { Ability = ConcealmentGrantAbility, Kind = Kind.Sneak, Bits = Sneak, Flags = StealthedFlag | ConcealmentFlag, BreaksOnDamage = true, Periodic = true, Toggle = true } },
            // Invisibility: kInvisible, MovementSpeed -300 and StealthState for 30 + 2 x level s; no health callback
            { InvisibilityGrantAbility, new Def { Ability = InvisibilityGrantAbility, Kind = Kind.Invisibility, Bits = Invisible, Flags = StealthedFlag, Toggle = true } },
            // Imparted: kInvisible for 120 s, nothing else
            { InvisibilityImpartedAbility, new Def { Ability = InvisibilityImpartedAbility, Kind = Kind.Imparted, Bits = Invisible, Toggle = true } },
            // Master Shadow: kShadowObject, evade state, 180 s with the 25 IS drain every 5 s
            { MasterShadowAbility, new Def { Ability = MasterShadowAbility, Kind = Kind.Shadow, Bits = ShadowObject, Toggle = true } },
        };

        /// <summary>Abilities this module runs (the states above plus Impart Invisibility and Vanish, which put them on).</summary>
        public static bool Handles(UInt16 id) => Defs.ContainsKey(id) || id == ImpartInvisibilityAbility || id == VanishAbility;

        // ---- per-player state ----

        public sealed class State
        {
            public Def Def;
            public int Level;
            public int Generation;
            public double Started, Expires;
            public List<AbilityBuffs.Modifier> Mods = new List<AbilityBuffs.Modifier>();
            public UInt16[] RecordIds = new UInt16[0];
            public float Seconds;
            public double NextNpcCheck;
            public int StealthLevel => Mods.Where(m => m.Stat == StealthAbility).Sum(m => m.Value);
        }

        private sealed class Holder { public State Current; public int Generation; }

        private static readonly ConditionalWeakTable<WorldClient, Holder> holders = new ConditionalWeakTable<WorldClient, Holder>();
        private static Holder HolderOf(WorldClient c) => holders.GetValue(c, _ => new Holder());
        private static readonly Random random = new Random();

        public static State Current(WorldClient client)
        {
            Holder h = HolderOf(client);
            lock (h) return h.Current;
        }

        public static UInt16 BitsOf(WorldClient client) => Current(client)?.Def.Bits ?? 0;
        public static bool IsOn(WorldClient client, UInt16 ability) => Current(client)?.Def.Ability == ability;
        /// <summary>Sneaking or invisible: kept out of enemies' sight and ignored by NPCs.</summary>
        public static bool IsHidden(WorldClient client) => (BitsOf(client) & (Sneak | Invisible)) != 0;

        /// <summary>
        /// Whether <paramref name="observer"/> is kept from seeing <paramref name="target"/>: the target is sneaking or
        /// invisible and the observer could attack them. Others (team, unflagged, same side) keep the view and see the
        /// client's own invisibility effect. Our choice: the scripts only ever reveal a sneaker to everyone at once
        /// (SneakUpdate turns the ability off), so there is no per-observer partial detection to model.
        /// </summary>
        public static bool HiddenFrom(WorldClient observer, WorldClient target)
        {
            if (observer == null || target == null || observer == target || !IsHidden(target)) return false;
            try { return PvpCombat.Check(observer, target) == PvpRules.Verdict.Allowed; }
            catch { return false; }
        }

        /// <summary>The stealth level the native test reads: the state's Stealth modifier plus any passive Stealth.</summary>
        public static int StealthLevelOf(WorldClient client) => (Current(client)?.StealthLevel ?? 0) + AbilityPassives.Total(client, StealthAbility);

        /// <summary>StealthAwareness: the creation value (1, BootingHelper) plus Stealth Detection totals (Awakened, Stealth Awareness).</summary>
        public static int AwarenessOf(WorldClient client)
        {
            byte[] raw = client.playerInstance.StealthAwareness.getValue();
            int baseline = raw != null && raw.Length > 0 ? raw[0] : 0;
            return baseline + AbilityStats.Player(client, StealthDetectionAbility);
        }

        /// <summary>Our choice: an NPC's StealthAwareness is its level (retail creation records carried per-NPC values we can't recover).</summary>
        public static int AwarenessOf(Mob mob) => mob.getLevel();

        public static int AbilityLevel(WorldClient client, UInt16 ability)
        {
            int level = AbilityPassives.Level(client, ability);
            if (level <= 0) level = client.playerInstance.Level.getValue()[0];
            return Math.Max(1, level);
        }

        /// <summary>The records a state sends, from the catalog's stored modifiers at this ability and character level.</summary>
        public static List<AbilityBuffs.Modifier> ModifiersFor(Def def, int abilityLevel, int characterLevel)
        {
            var mods = new List<AbilityBuffs.Modifier>();
            switch (def.Ability)
            {
                case InvisibilityGrantAbility:
                    // misc.py: addTempMod(.., MovementSpeedAbility, -300, duration), addStateMod(.., StealthStateAbility)
                    mods.Add(new AbilityBuffs.Modifier(AbilityBuffs.KindStat, MovementSpeedAbility, -300));
                    mods.Add(new AbilityBuffs.Modifier(AbilityBuffs.KindState, StealthStateAbility, 0));
                    return mods;
                case InvisibilityImpartedAbility:
                    return mods;
                case MasterShadowAbility:
                    mods.Add(new AbilityBuffs.Modifier(AbilityBuffs.KindStat, ISDrainTargetAbility, ShadowDrain));
                    return mods;
            }
            var entry = AbilityCatalog.Find(def.Ability == ImprovedSneakAbility ? SneakGrantAbility : def.Ability);
            if (entry == null) return mods;
            foreach (var m in entry.Modifiers)
            {
                if (m.State != 0) { mods.Add(new AbilityBuffs.Modifier(AbilityBuffs.KindState, m.State, 0)); continue; }
                if (m.Target == 0) continue;
                if (!AbilityValues.TryValue(m, abilityLevel, characterLevel, out int value)) continue;
                mods.Add(new AbilityBuffs.Modifier(AbilityBuffs.KindStat, m.Target, value));
            }
            return mods;
        }

        // ---- using the abilities ----

        /// <summary>Why the ability can't be used now (the script's _Test), or null.</summary>
        public static string Refusal(WorldClient client, UInt16 id, UInt16 targetView)
        {
            if (id == ImpartInvisibilityAbility)
            {
                // ImpartInvisibility_Test and ImpartInvisibilityInit
                WorldClient target = AbilitySupport.PlayerInView(client, targetView);
                if (target == null || target == client) return "You cannot impart invisibility on yourself.";
                if (CombatManager.GetHandler(target).isCombatRunning) return "Your target is in combat.";
                if ((BitsOf(target) & Invisible) != 0) return "Your target is already invisible.";
                return null;
            }
            if (id == VanishAbility) return null; // "Vanish Prox Override": no enemy test
            if (!Defs.TryGetValue(id, out Def def)) return null;
            if (def.Kind == Kind.Sneak && EnemyWatching(client, DetectionRangeUnits(AbilityLevel(client, SneakGrantAbility))))
                return "You cannot sneak while an enemy is watching."; // InitialSneakTest
            if (def.Kind == Kind.Invisibility && EnemyWatching(client, InvisibilityProximityUnits))
                return "You cannot become invisible near enemies."; // InvisibilityGrant_Test
            if (def.Kind == Kind.Imparted && (BitsOf(client) & Invisible) != 0) return "You are already invisible.";
            return null;
        }

        /// <summary>The cast finished: the state goes on (or, for Impart and Vanish, the state they give).</summary>
        public static void Land(WorldClient client, UInt16 id, UInt16 targetView)
        {
            if (id == ImpartInvisibilityAbility)
            {
                WorldClient target = AbilitySupport.PlayerInView(client, targetView);
                if (target == null || Refusal(client, id, targetView) != null) return;
                Begin(target, Defs[InvisibilityImpartedAbility], AbilityLevel(client, id));
                return;
            }
            if (id == VanishAbility)
            {
                // VanishInit runs as the 2 s modifier's term function: reapplyAbility(InvisibilityGrant), no proximity test.
                // Our choice: the fight the Spy vanishes from ends with it.
                DelayedActions.AfterFor(client, VanishDelaySeconds, () =>
                {
                    if (client.playerData.isDead) return;
                    CombatManager.GetHandler(client).StopCombatIfActive(CombatSession.CombatEndReason.AttackerFled);
                    Begin(client, Defs[InvisibilityGrantAbility], AbilityLevel(client, InvisibilityGrantAbility));
                });
                return;
            }
            if (Defs.TryGetValue(id, out Def def)) Begin(client, def, AbilityLevel(client, id));
        }

        /// <summary>Puts a stealth state on the player (replacing any other: every one of them is AbilityIsExclusive).</summary>
        public static State Begin(WorldClient client, Def def, int abilityLevel)
        {
            End(client, "replaced", false);
            int characterLevel = client.playerInstance.Level.getValue()[0];
            var state = new State
            {
                Def = def, Level = abilityLevel, Started = TimeUtils.GetSimTimeSeconds(),
                Mods = ModifiersFor(def, abilityLevel, characterLevel),
                Seconds = def.Kind == Kind.Invisibility ? (float)InvisibilitySeconds(abilityLevel) :
                    def.Kind == Kind.Imparted ? (float)ImpartedSeconds : def.Kind == Kind.Shadow ? (float)ShadowSeconds : 0f,
            };
            state.Expires = state.Seconds > 0 ? state.Started + state.Seconds : double.PositiveInfinity;
            state.RecordIds = state.Mods.Select(_ => AbilityBuffs.NextRecordId(client.playerData)).ToArray();
            Holder h = HolderOf(client);
            lock (h) { state.Generation = ++h.Generation; h.Current = state; }

            if (def.Toggle) client.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(def.Ability, true));
            for (int i = 0; i < state.Mods.Count; i++)
                client.messageQueue.addRpcMessage(AbilityBuffs.BuildModifier(state.Mods[i].Kind, state.RecordIds[i], def.Ability,
                    state.Mods[i].Stat, state.Mods[i].Value, false, state.Seconds));
            Present(client, def.Bits, def.Flags, true);
            Output.WriteDebugLog("[STEALTH] " + TimedEffects.PlayerName(client) + " " + def.Ability + " on (bits " + def.Bits +
                ", SL " + state.StealthLevel + (state.Seconds > 0 ? ", " + state.Seconds + " s" : "") + ")");

            int generation = state.Generation;
            if (state.Seconds > 0)
                DelayedActions.AfterFor(client, state.Seconds, () => End(client, "expired", true, generation));
            if (def.Periodic) Schedule(client, generation, UpdateSeconds, Update);
            if (def.Kind == Kind.Shadow) Schedule(client, generation, ShadowDrainSeconds, Drain);
            return state;
        }

        private static void Schedule(WorldClient client, int generation, double seconds, Action<WorldClient, State> step)
        {
            DelayedActions.AfterFor(client, seconds, () =>
            {
                State s = Current(client);
                if (s == null || s.Generation != generation || !client.Alive) return;
                step(client, s);
                if (Current(client)?.Generation == generation) Schedule(client, generation, seconds, step);
            });
        }

        /// <summary>SneakUpdate: an enemy player in range, facing the sneaker, who passes sneakTest reveals them.</summary>
        private static void Update(WorldClient client, State state)
        {
            if (DetectedByPlayer(client, state)) End(client, "revealed", true);
        }

        /// <summary>ISDrainWithAbilityDeactivation: take the value; when there isn't enough Inner Strength the ability ends.</summary>
        private static void Drain(WorldClient client, State state)
        {
            if (AbilityHandler.CurrentInnerStrength(client) < ShadowDrain) { End(client, "out of Inner Strength", true); return; }
            UInt16 left = AbilityHandler.SpendInnerStrength(client, ShadowDrain);
            new ServerPackets().sendISCurrent(client, left);
        }

        /// <summary>Takes the state off: records off, toggle off, DissemblingType and flags cleared for everyone.</summary>
        public static bool End(WorldClient client, string why, bool flush = true, int generation = -1)
        {
            Holder h = HolderOf(client);
            State state;
            lock (h)
            {
                state = h.Current;
                if (state == null || generation >= 0 && state.Generation != generation) return false;
                h.Current = null;
            }
            for (int i = 0; i < state.Mods.Count; i++)
                client.messageQueue.addRpcMessage(AbilityBuffs.BuildModifier(state.Mods[i].Kind, state.RecordIds[i], state.Def.Ability,
                    state.Mods[i].Stat, state.Mods[i].Value, true, state.Seconds));
            if (state.Def.Toggle) client.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(state.Def.Ability, false));
            Present(client, 0, state.Def.Flags, false);
            if (state.Def.Kind == Kind.Shadow)
                AbilityCooldowns.Start(client, state.Def.Ability, TimeUtils.GetSimTimeSeconds()); // AddRecastTimer on term
            Output.WriteDebugLog("[STEALTH] " + TimedEffects.PlayerName(client) + " " + state.Def.Ability + " off (" + why + ")");
            if (flush) client.FlushQueue();
            return true;
        }

        /// <summary>DissemblingType and ConditionStateFlags to the own view and to everyone who has a view of the player.</summary>
        private static void Present(WorldClient client, UInt16 bits, UInt32 flags, bool on)
        {
            client.playerInstance.DissemblingType.setValue(bits);
            lock (client.playerData.conditionTokens)
            {
                if (on) client.playerData.conditionStateFlags |= flags;
                else client.playerData.conditionStateFlags &= ~flags;
            }
            UInt32 all = PvpFlag.FlagsOf(client);
            client.playerInstance.ConditionStateFlags.setValue(all);
            client.messageQueue.addObjectMessage(BuildSelf(bits, all), false);
            client.FlushQueue();
            PvpFlag.ToWatchers(client, BuildOther(bits, all));
        }

        /// <summary>Own view: DissemblingType (36) and ConditionStateFlags (42): <c>02 00 02 80 80 80 80 80 82 &lt;u16&gt; 01 &lt;u32&gt;</c>.</summary>
        public static byte[] BuildSelf(UInt16 bits, UInt32 flags) => PlayerViewAttributes.BuildSelf(
            (36, PlayerViewAttributes.U16(bits)), (PlayerViewAttributes.SelfConditionStateFlags, PlayerViewAttributes.U32(flags)));

        /// <summary>Another player's view: DissemblingType (27) and ConditionStateFlags (30): <c>02 80 80 80 c0 &lt;u16&gt; 04 &lt;u32&gt;</c>.</summary>
        public static byte[] BuildOther(UInt16 bits, UInt32 flags) => PlayerViewAttributes.Build(
            (27, PlayerViewAttributes.U16(bits)), (PlayerViewAttributes.OtherConditionStateFlags, PlayerViewAttributes.U32(flags)));

        /// <summary>A view of the player was just created: give it the stealth state the creation record doesn't carry.</summary>
        public static void AfterSpawn(WorldClient observer, WorldClient target, UInt16 viewId)
        {
            UInt16 bits = BitsOf(target);
            if (bits == 0) return;
            observer.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(viewId, BuildOther(bits, PvpFlag.FlagsOf(target))), false);
            observer.FlushQueue();
        }

        // ---- what breaks it ----

        /// <summary>OnHealthReduceDeactivateAbility: damage taken while below maximum health ends Sneak, E&amp;E and Concealment.</summary>
        public static void HealthChanged(WorldClient client, int damageTaken, bool belowMaximum)
        {
            State s = Current(client);
            if (s != null && s.Def.BreaksOnDamage && damageTaken > 0 && belowMaximum) End(client, "damaged");
        }

        /// <summary>
        /// The player attacks or uses another ability. Our choice (no script names the rule; the abilities are all
        /// AbilityIsExclusive and serverrules.py BreakExclusiveAbility ends the exclusive ability on interactions):
        /// any attack or non-stealth ability ends the state.
        /// </summary>
        public static void BreakForAction(WorldClient client, string why)
        {
            if (Current(client) != null) End(client, why);
        }

        /// <summary>Death takes every state off (as AbilityBuffs.RemoveAll does buffs).</summary>
        public static void Cleared(WorldClient client) => End(client, "cleared", false);

        // ---- detection ----

        private static bool Enemy(WorldClient sneaker, WorldClient other)
        {
            // utility.py: getEnemiesInRadius and CanFight(subject, opponent)
            try { return PvpCombat.Check(other, sneaker) == PvpRules.Verdict.Allowed || PvpCombat.Check(sneaker, other) == PvpRules.Verdict.Allowed; }
            catch { return false; }
        }

        private static void Position(WorldClient c, out double x, out double y, out double z)
        {
            x = y = z = 0;
            NumericalUtils.LtVector3dToDoubles(c.playerInstance.Position.getValue(), ref x, ref y, ref z);
        }

        private static IEnumerable<(WorldClient Other, double Distance)> EnemiesFacing(WorldClient client, double range)
        {
            Position(client, out double x, out double y, out double z);
            foreach (WorldClient other in SocialHandler.OnlineWhere(c => c != client && c.playerData.getDistrictId() == client.playerData.getDistrictId()
                && !c.playerData.isDead && c.playerData.getOnWorld()))
            {
                Position(other, out double ox, out double oy, out double oz);
                double d = Math.Sqrt((ox - x) * (ox - x) + (oz - z) * (oz - z));
                if (d > range || !MobAggro.SameFloor(oy, y) || !Enemy(client, other)) continue;
                if (!InFrustum(ox, oz, other.playerInstance.YawInterval.getValue()[0], x, z)) continue;
                yield return (other, d);
            }
        }

        /// <summary>InitialSneakTest / InvisibilityGrant_Test: an enemy in range, facing the player (line of sight is not checked).</summary>
        public static bool EnemyWatching(WorldClient client, double range) => EnemiesFacing(client, range).Any();

        public static bool DetectedByPlayer(WorldClient client, State state)
        {
            double range = DetectionRangeUnits(state.Level);
            int sl = StealthLevelOf(client);
            foreach (var (other, distance) in EnemiesFacing(client, range))
            {
                int roll = Roll(random);
                if (SneakTest(AwarenessOf(other), sl, 0, (float)distance, roll))
                {
                    Output.WriteDebugLog("[STEALTH] " + TimedEffects.PlayerName(other) + " sees " + TimedEffects.PlayerName(client) +
                        " (SA " + AwarenessOf(other) + " SL " + sl + " d " + (int)distance + " roll " + roll + ")");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// MobAggro asks whether an NPC may attack the player. Invisible and shadowed players are ignored. A sneaker is
        /// tested against willing NPCs facing them once per <see cref="UpdateSeconds"/>, as SneakUpdate does; one that sees
        /// them ends the sneak (and then attacks as usual).
        /// </summary>
        public static bool IgnoredByNpcs(WorldClient client, IEnumerable<(Mob Mob, double Distance)> willing, double now)
        {
            State s = Current(client);
            if (s == null) return false;
            if (!s.Def.Periodic) return true;
            if (now < s.NextNpcCheck) return true;
            s.NextNpcCheck = now + UpdateSeconds;
            Position(client, out double x, out _, out double z);
            double range = DetectionRangeUnits(s.Level);
            int sl = StealthLevelOf(client);
            foreach (var (mob, distance) in willing)
            {
                if (distance > range || !InFrustum(mob.getXPos(), mob.getZPos(), (byte)mob.getRotation(), x, z)) continue;
                if (SneakTest(AwarenessOf(mob), sl, 0, (float)distance, Roll(random)))
                {
                    End(client, mob.getName() + " sees through the sneak");
                    return false;
                }
            }
            return true;
        }
    }
}
