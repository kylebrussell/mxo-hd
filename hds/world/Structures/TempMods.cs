using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Retail's timed effects ("tempmods"): the records the server put on a character for an ability's debuffs, states
    /// and damage over time, with the numbers from the retail captures in research/packet-dumps and the rules of the
    /// client's own scripts (research/client-scripts/tempmods.py, ability/hacker/virii.py, virii_defines.py).
    ///
    /// <para>A tempmod is the 80 bc record (see <see cref="AbilityBuffs.BuildModifier"/>): <c>&lt;kind&gt; &lt;record id&gt;
    /// &lt;parent ability&gt; &lt;target ability&gt; &lt;value&gt; &lt;off&gt; &lt;seconds&gt;</c>. The "stat" field is the
    /// tempmod's TargetAbility: MezzingTerm's <c>addTempMod(MezAbility, MovementSpeedAbility, 0, 2)</c>
    /// (tempmods.py 862) is retail's <c>80 bc 01 00 dc 00 00 55 00 00 00 73 00 00 00 00 00 00 00 00 00 40</c> (Rajko MaraCThenTimeDOut
    /// 63411: parent 85, target 115, 2 s), and DeflectVirus1's <c>addTempModProcs(.., 0.2, .., FX)</c> (virii.py 31) is
    /// <c>80 bc 09 00 d8 00 00 7a 01 00 00 7a 01 20 08 00 28 00 cd cc 4c 3e</c> (crashover2k proxy_024 13034: parent and target
    /// 378, the effect id as the value, 0.2 s).</para>
    ///
    /// <para>The captures show these records on the player they hit (the victim's log), e.g. Code Infection 4 on
    /// Morpheuzzz: <c>80 bc 1d 00 18 00 00 da 01 00 00 85 04 62 03 00 00 00 00 00 f0 41</c> (morpheuzzz_001 2394: parent
    /// 474, DOTPlaceHolderAbility 1157, 866 damage, 30 s) and "KaosDevil's Code Infection 4 infects you!".</para>
    /// </summary>
    public static class TempMods
    {
        // Record kinds (the u16 after 80 bc) as retail used them. 0x15/0x19/0x1d are AbilityBuffs' KindStat/KindState/KindLinked.
        /// <summary>Plain tempmod with no stat (ControlImmunityTimer 734 -&gt; 681, Mez's 2 s MovementSpeed, the tactic conditions).</summary>
        public const UInt16 KindPlain = 0x01;
        /// <summary>Tempmod with procs and no stat (DeflectVirus1's PlayFX, DeathEffect).</summary>
        public const UInt16 KindFx = 0x09;
        /// <summary>State tempmod (addStateMod): Stun, Mez, Root, Powerless, Blind, Enrage, Disarmed, Daze ... with a time.</summary>
        public const UInt16 KindState = 0x11;
        public const UInt16 KindStat = AbilityBuffs.KindStat;
        public const UInt16 KindLinked = AbilityBuffs.KindLinked;

        // Target abilities (data/abilityIDs.csv)
        public const UInt16 MezAbility = 85, BlindAbility = 90, ConfuseAbility = 91, EnrageAbility = 93, PowerlessAbility = 96,
            RootedAbility = 99, StunAbility = 102, MovementSpeedAbility = 115, CombatTacticsAbility = 163, DazeAbility = 199,
            OffBalanceAbility = 200, StaggeredAbility = 201, DisarmedAbility = 231, ISDrainTargetAbility = 681,
            ControlImmunityTimerAbility = 734, DOTPlaceHolderAbility = 1157;

        // tempmods.py 18-26
        public const UInt32 ConstRegenFx = 0x280006ae;          // FX_NPC_REGEN
        /// <summary>VIRUSCRASH_FX = FX_CHARACTER_HEALTH_DRAIN, played on every damage-over-time tick (ViralDOT ... NeutralDOT).</summary>
        public const UInt32 VirusCrashFx = 0x28000430;
        public const int ConfuseCombatMod = -10;
        public const int GenericStunAttackMod = -20;
        public const int VirusCrashCorrodeValue = 2;
        public const int FlitGunDamage = 20;
        public const int VirusCrashCorrodeChanceValue = 100;
        public const int MobiusImmunityChance = 1;

        /// <summary>
        /// The damage-over-time UpdateInterval. Not in the scripts (it is the ability data's); every retail tick is 2.5 s
        /// apart: Plague Zone 2 on Zombie Lurch at 10:27:39, :41, :44, :46, :49 (actions_combat 16649-17445), and the four
        /// viruses on Morpheuzzz (morpheuzzz_001 2544-4276). With it ViralDOT's formula gives retail's ticks before
        /// resistances exactly: see <see cref="DotTick"/>.
        /// </summary>
        public const double DotIntervalSeconds = 2.5;

        /// <summary>
        /// ViralDOT / PhysicalDOT / RangedDOT / ThrownDOT / StealthDOT / NeutralDOT (tempmods.py 896-972), the damage of
        /// one tick: <c>int(Value / Duration * UpdateInterval + 0.5)</c>, Duration 0 counting as 1. Retail's tick messages
        /// carry the damage done and the raw tick less it ("does %4I damage", values [done, raw - done]): Code Infection 4,
        /// 866 over 30 s, ticks 72 = [91, -19]; Infect Area 1, 450/30: 38 = [48, -10]; Infect Area 2, 624/30: 52 = [66, -14];
        /// Code Infection 3, 716/30: 60 = [76, -16] (morpheuzzz_001); Plague Zone 2, 424/15: 71 = [69, 2] (actions_combat).
        /// </summary>
        public static int DotTick(int value, double duration, double updateInterval)
        {
            if (duration == 0)
            {
                duration = 1;
            }
            double dps = value / duration;
            return (int)(dps * updateInterval + 0.5);
        }

        /// <summary>HOT (tempmods.py 975): the same formula for a heal over time, with no Duration 0 guard.</summary>
        public static int HotTick(int value, double duration, double updateInterval)
        {
            return (int)(value / duration * updateInterval + 0.5);
        }

        /// <summary>HOT's cap: never above the maximum (tempmods.py 983-991 heal only up to MaxHealth).</summary>
        public static int HotAmount(int health, int maxHealth, int boost)
        {
            if (health >= maxHealth)
            {
                return 0;
            }
            return health + boost > maxHealth ? maxHealth - health : boost;
        }

        /// <summary>ApplyStunWithCodedTimer (tempmods.py 1674): stun seconds in the high bits, immunity in the low byte.</summary>
        public static void UnpackStun(int value, out int stunSeconds, out int immunitySeconds)
        {
            stunSeconds = value >> 8;
            immunitySeconds = value & 255;
        }

        /// <summary>packVal / unpackVal (tempmods.py 509-517, BufferOverflow's value).</summary>
        public static int PackVal(int hi, int lo)
        {
            return hi << 16 | lo;
        }

        public static void UnpackVal(int v, out int hi, out int lo)
        {
            hi = v >> 16;
            lo = v & 65535;
        }

        /// <summary>What a tempmod does on the server.</summary>
        public enum Proc
        {
            /// <summary>A stat modifier: the client applies it to the character; the server keeps the sum (<see cref="TimedEffects.Holder.Total"/>).</summary>
            Stat,
            /// <summary>A state (Stun, Mez, Root, Powerless ...): on while the record is.</summary>
            State,
            /// <summary>Damage over time on DOTPlaceHolderAbility, <see cref="DotTick"/> every <see cref="DotIntervalSeconds"/>.</summary>
            Dot,
            Hot,
            /// <summary>A periodic script callback (NegativeConditionSweepPulse) every Interval while it lasts (<see cref="AbilityHostile.Pulse"/>).</summary>
            Pulse,
        }

        public sealed class Mod
        {
            public UInt16 Kind;
            public UInt16 Target;
            public Int32 MinValue, MaxValue;
            public float Seconds;
            public Proc Proc;
            public float Chance = 1;
            public BreakRule Break;
            public int BreakChance;
            public bool ProtectFirstHealthChange;
            public double Interval = DotIntervalSeconds;

            public override string ToString()
            {
                return Proc + " " + Target + " " + MinValue + (MaxValue != MinValue ? ".." + MaxValue : "") + " " + Seconds + " s";
            }
        }

        public enum BreakRule { None, Damage, HealthChange, Chance }

        /// <summary>The tempmods one use of an ability puts on its target, in retail's record order.</summary>
        public sealed class Effect
        {
            public UInt16 Ability;
            public Mod[] Mods;
            /// <summary>
            /// Seconds of ControlImmunityTimer after a stun, pacify (Mez) or Powerless (ApplyStunWithTimer, tempmods.py
            /// 1659: the stun's own time). 0 = the state's whole seconds (Pistol Aerial 4 s -&gt; 4, Vector 8.94 s -&gt; 8,
            /// 7.44 s -&gt; 7); Freeze Area 2.0 gave 12 for a 10.02 s pacify.
            /// </summary>
            public float ImmunitySeconds;
            /// <summary>The ability is a virus: the caster is told "Your %1z infects %3s!".</summary>
            public bool Virus;
            /// <summary>Non-virus catalog effects: the template's ActivationStringSubject / ActivationStringTarget lines (0: none).</summary>
            public UInt32 CasterLine, TargetLine;

            public bool HasControl
            {
                get
                {
                    foreach (Mod m in Mods)
                    {
                        if (m.Proc == Proc.State && IsControlState(m.Target))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        /// <summary>States that ControlImmunityTimer guards ("immune to stun and pacify"); retail gave it for Powerless too (Vector).</summary>
        public static bool IsControlState(UInt16 target)
        {
            return target == StunAbility || target == MezAbility || target == PowerlessAbility;
        }

        /// <summary>
        /// Each state's own record, sent right after the state's (retail: the state ability as parent, 0 s), and its
        /// value: Blind's is CombatTactics -30 (proxy_020 143154: 80 bc 1d .. 5a 00 00 00 a3 00 e2 ff ff ff), the others
        /// name no stat (Root, Stun, Powerless, Mez, Enrage, Disarmed: target 0, value 0). The Blind value is
        /// BlindAbility's ValueFrom in abilityIDs.csv, like OffBalance -10 and Staggered -5.
        /// </summary>
        public static void LinkedRecord(UInt16 state, out UInt16 target, out Int32 value)
        {
            switch (state)
            {
                case BlindAbility:
                    target = CombatTacticsAbility;
                    value = -30;
                    break;
                default:
                    target = 0;
                    value = 0;
                    break;
            }
        }

        /// <summary>"You are no longer %4z." (ID_CLIENT_STATE_DISABLE_TARGET) names the state with these strings.</summary>
        public static UInt32 StateNameId(UInt16 state)
        {
            switch (state)
            {
                case StunAbility: return 0x7a000036;      // ID_STATE_STUNNED
                case MezAbility: return 0x3a00005b;       // ID_STATE_MEZZED
                case PowerlessAbility: return 0x7a000048; // ID_STATE_POWERLESS (morpheuzzz_001 2406: 48 00 00 7a)
                case RootedAbility: return 0x7a000155;    // ID_STATE_ROOTING
                case BlindAbility: return 0x7a000034;     // ID_STATE_BLIND
                case ConfuseAbility: return 0x7a000037;   // ID_STATE_CONFUSED
                case EnrageAbility: return 0x7a000038;    // ID_STATE_ENRAGED
                default: return 0;
            }
        }

        private static Mod Stat(UInt16 target, Int32 value, float seconds) =>
            new Mod { Kind = KindStat, Target = target, MinValue = value, MaxValue = value, Seconds = seconds, Proc = Proc.Stat };

        private static Mod Stat(UInt16 target, Int32 min, Int32 max, float seconds) =>
            new Mod { Kind = KindStat, Target = target, MinValue = min, MaxValue = max, Seconds = seconds, Proc = Proc.Stat };

        private static Mod State(UInt16 state, float seconds) =>
            new Mod { Kind = KindState, Target = state, Seconds = seconds, Proc = Proc.State };

        private static Mod Dot(Int32 min, Int32 max, float seconds) =>
            new Mod { Kind = KindLinked, Target = DOTPlaceHolderAbility, MinValue = min, MaxValue = max, Seconds = seconds, Proc = Proc.Dot };

        private static Effect E(UInt16 ability, params Mod[] mods) => new Effect { Ability = ability, Mods = mods };

        private static Effect Virus(UInt16 ability, params Mod[] mods) => new Effect { Ability = ability, Mods = mods, Virus = true };

        /// <summary>
        /// Ability id to the tempmods it puts on its target: every record retail sent the victim, from the first capture
        /// found (file and line). Values that differed between captures (they grow with the caster) are ranges; times
        /// that differed are the longest (shorter ones were cut by the victim's Determination,
        /// combat_calculations.GetAbilityMitigationDuration).
        /// </summary>
        public static readonly Dictionary<UInt16, Effect> Retail = new Dictionary<UInt16, Effect>
        {
            // ---- Hacker viruses ----
            // UI Lag 1.0, morpheuzzz_001 2490: 1032 -10, 183 -17, 1011 -12, 342 -14 for 27 s
            { 97, Virus(97, Stat(1032, -10, 27), Stat(183, -17, 27), Stat(1011, -12, 27), Stat(342, -14, 27)) },
            // Plague Zone 2.0: "Your Plague Zone 2.0 infects Zombie Lurch!" with 424 (actions_combat 16472) and 444
            // (abilities_use_plague_zone_2_0 146); ticks of 71 (69 done, 2 resisted) every 2.5 s, five of them: 15 s
            // (424 / 15 x 2.5 = 70.7). The record itself isn't captured (only the victim gets it); kind and target as
            // Code Infection's.
            { 466, Virus(466, Dot(424, 444, 15)) },
            // Code Infection 3, morpheuzzz_001 3021: 716 over 30 s
            { 472, Virus(472, Dot(716, 716, 30)) },
            // Code Infection 4, morpheuzzz_001 2394 (866) and crashover2k_008 40520 (880), 30 s
            { 474, Virus(474, Dot(866, 880, 30)) },
            // Infect Area 1, morpheuzzz_001 2590: 450 over 30 s
            { 353, Virus(353, Dot(450, 450, 30)) },
            // Infect Area 2, morpheuzzz_001 2808: 624 over 30 s
            { 475, Virus(475, Dot(624, 624, 30)) },
            // Vector, crashover2k_008 40322: Powerless 8.94 s (7.44 s on Morpheuzzz, 1829), ViralDeflection -60 for 30 s;
            // ControlImmunityTimer 8 s (7 s)
            { 355, Virus(355, State(PowerlessAbility, 8.94f), Stat(183, -60, 30)) },
            // Freeze Area 2.0, Rajko MaraCThenTimeDOut 60983: ViralResistance -100 for 12 s, pacified 10.02 s, immunity 12 s
            { 479, new Effect { Ability = 479, Virus = true, ImmunitySeconds = 12,
                Mods = new[] { Stat(157, -100, 12), State(MezAbility, 10.02f) } } },
            // Zone of Powerlessness, crashover2k proxy_unsorted_Packets1 20393 (9.9 s) and entilsar lastday2 74159 (10.14 s)
            { 399, Virus(399, State(PowerlessAbility, 10.14f)) },

            // ---- Spy ----
            // Neuro Dart, old_world Vector 33458: 15 s of slows and defence debuffs and 264 damage over time
            { 141, E(141, Stat(877, -85, 15), Stat(878, -72, 15), Dot(264, 264, 15), Stat(524, -15, 15), Stat(1009, -13, 15),
                Stat(1010, -21, 15), Stat(522, -15, 15), Stat(519, -20, 15), Stat(115, -57, 15)) },
            // Deadly Throw, old_world Vector 33977
            { 279, E(279, Stat(877, -85, 15), Stat(519, -15, 15), Stat(878, -72, 15), Stat(115, -57, 15)) },
            // Sneak Attack Suplex, old_world Vector 15915: OffBalance, Staggered, Dazed for 15 s
            { 287, E(287, State(OffBalanceAbility, 15), State(StaggeredAbility, 15), State(DazeAbility, 15)) },
            // Throat Cutting Throw, crashover2k proxy_003 5145 (OffBalance 8 s; Vector 32880 had Dazed 8 s)
            { 340, E(340, State(OffBalanceAbility, 8)) },
            // Staggering Throw, old_world Vector 16171
            { 348, E(348, State(StaggeredAbility, 12)) },
            // Subduing Throw, old_world Vector 33209: stunned 4 s, immunity 4 s
            { 347, E(347, State(StunAbility, 4)) },

            // ---- Soldier ----
            // Crippling Shot, trinity proxy_011 1787 (ten captures: -103 .. -113, MovementSpeed -75 .. -124), 8 s
            { 450, E(450, Stat(877, -113, -103, 8), Stat(878, -113, -103, 8), Stat(115, -124, -75, 8)) },
            // Immobilizing Shot, old_world Vector 15571: ForceCombat -15, rooted, and a stat-0 record of 50, 8 s
            { 254, E(254, Stat(519, -15, 8), State(RootedAbility, 8), Stat(0, 50, 8)) },
            // Disarming Shot (pistol), -18 .. -34 RangeCombatTactics for 10 s (old_world second_pc 9248, proxy_026 75345)
            { 126, E(126, Stat(524, -34, -18, 10)) },
            // Wounding Shot, old_world Vector 83347: 14 over 10 s
            { 253, E(253, Dot(14, 14, 10)) },
            // Pistol Execution, proxy_020 140397: MeleeCombatTactics -34 for 8 s (Rajko ZombiesPortingEtc 5615 had
            // RangedDefenseTactics -44 instead)
            { 502, E(502, Stat(522, -34, 8)) },
            // Pistol Aerial, crashover2k proxy_003 8687; Deadly Shot, Vector 31513; Sniper Shot, proxy_003 login 20114: stun 4 s
            { 243, E(243, State(StunAbility, 4)) },
            { 454, E(454, State(StunAbility, 4)) },
            { 505, E(505, State(StunAbility, 4)) },
            // Suppression Fire, ProxyM2_m2_packets_5 28162: rooted 21.12 s; Pinning Fire, Rajko MaraCCyphs 51874: 14.08 s
            { 248, E(248, State(RootedAbility, 21.12f)) },
            { 492, E(492, State(RootedAbility, 14.08f)) },
            // Pistol Dash, Rajko AgentsAndIntPorting 40215: enraged 8 s
            { 500, E(500, State(EnrageAbility, 8)) },
            // Pistol Whip, proxy_020 143154: blinded 10.08 s
            { 501, E(501, State(BlindAbility, 10.08f)) },

            // ---- Martial arts ----
            // Sky High Sidekick, ProxyM2_m2_packets 77358: melee and ranged damage resistance down for 12 s
            { 267, E(267, Stat(180, -64, 12), Stat(181, -62, 12)) },
            // Tomo Nage, crashover2k_008 38417: MeleeDamageResistance -140 for 12 s, enraged 6 s
            { 412, E(412, Stat(180, -140, 12), State(EnrageAbility, 6)) },
            // Spinning Flower, crashover2k proxy_unsorted_Packets1 18620: CombatTactics -10 for 15 s, Powerless 22.5 s
            // (18.9 - 24.6 s in other captures)
            { 870, E(870, Stat(CombatTacticsAbility, -10, 15), State(PowerlessAbility, 24.6f)) },
            // Sweep the Floor, ProxyM2_m2_packets 143380: disarmed 20 s
            { 869, E(869, State(DisarmedAbility, 20)) },

            // ---- Stuns that AbilityTargets already had ----
            // Stun 1 / Stun 2.0: no retail capture of a landed one; 4 s like retail's other stuns (StunAbility, 102)
            { 315, new Effect { Ability = 315, Mods = new[] { State(StunAbility, 7) }, ImmunitySeconds = 14 } },
            { 333, new Effect { Ability = 333, Mods = new[] { State(StunAbility, 5) }, ImmunitySeconds = 10 } },
        };

        public static bool Has(UInt16 abilityId)
        {
            return TryGet(abilityId, out _);
        }

        public static bool TryGet(UInt16 id, out Effect effect)
        {
            id = AbilityCatalog.Canonical(id);
            // AbilityHostile's catalog debuffs also give the thrown specials (Poison Knife, Wounding Throw, Sever Artery,
            // CombatSpecials) their stored ThrownDOT and timed debuffs
            Effect source = AbilityHostile.Merge(AbilityCatalog.Controls(id), AbilityHostile.Catalog(id));
            if (!Retail.TryGetValue(id, out var captured)) { effect = source; return effect != null; }
            var mods = new List<Mod>();
            foreach (Mod m in captured.Mods)
            {
                Mod rule = source == null ? null : Array.Find(source.Mods, p => p.Target == m.Target && p.Proc == m.Proc);
                mods.Add(new Mod { Kind = m.Kind, Target = m.Target, MinValue = m.MinValue, MaxValue = m.MaxValue,
                    Seconds = m.Seconds, Proc = m.Proc, Chance = rule?.Chance ?? m.Chance,
                    Break = rule?.Break ?? (m.Target == MezAbility ? BreakRule.Damage : m.Break),
                    BreakChance = rule?.BreakChance ?? m.BreakChance, ProtectFirstHealthChange = rule?.ProtectFirstHealthChange ?? m.ProtectFirstHealthChange,
                    Interval = m.Interval });
            }
            effect = new Effect { Ability = id, Mods = mods.ToArray(), Virus = captured.Virus, ImmunitySeconds = captured.ImmunitySeconds };
            return true;
        }
    }
}
