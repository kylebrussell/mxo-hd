using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    /// <summary>Sourced static tactic exchanges for the styles with their own ILDB sets: Self-Defense (S), Aikido (A),
    /// Karate (K) and Kung-Fu (W); see <see cref="CombatStyles.Letter"/>. A pair the ILDB lacks falls back to
    /// Self-Defense. Named special choreography uses the extracted ILDB; the full rule language remains separate.</summary>
    public static class CombatAnimations
    {
        public sealed class Move
        {
            public uint Id, Flags;
            public ushort DurationMs;
            public string Group, Name;
            public ulong Reference => CombatRoundEntry.Reference(Id);
        }
        private static readonly Lazy<IReadOnlyList<Move>> moves = new Lazy<IReadOnlyList<Move>>(() =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("combat_animations.json")));
            return doc.RootElement.GetProperty("moves").EnumerateArray().Select(row => new Move
            {
                Id = row.GetProperty("id").GetUInt32(), Flags = row.GetProperty("flags").GetUInt32(),
                DurationMs = row.GetProperty("duration_ms").GetUInt16(),
                Group = row.GetProperty("group").GetString(), Name = row.GetProperty("name").GetString()
            }).ToArray();
        });
        public static IReadOnlyList<Move> All => moves.Value;
        // Name matches/aliases between the GOB ability and ILDB choreography. Aliases are server policy;
        // IDs/durations are extracted, never inferred from ability IDs. Unmapped moves retain tactic fallback.
        private static readonly Dictionary<ushort, string> specialNames = new Dictionary<ushort, string>
        {
            {134,"LegSweep"},{138,"KarateAbility GuardBreaker"},{139,"AikidoAbility IronGuard"},
            {145,"DoubleOverheadSmash"},{197,"HeadButt"},{198,"CheapShot"},{605,"LowBlow"},{618,"Bash"},
            {152,"Backsweep"},{153,"Drop Kick"},{286,"Twist Throw"},{287,"Suplex"},{288,"Topsy Turvy"},
            {289,"Punt"},{385,"Diving Frankensteiner"},{386,"Freedom to Joints"},{387,"Running Bulldog"},{388,"Reverse Pulldown"},
            {241,"Pistol Slide"},{242,"Pistol Evasive MR"},{243,"Pistol Aerial"},{244,"Pistol Barrage MR"},
            {126,"Pistol Disarming Shot"},{501,"Pistol Whip"},{502,"Pistol Execution MR"},
            {105,"Rifle Body Shot MR"},{253,"Rifle WoundingShot"},{254,"Rifle ImmobilizingShot"},
            {255,"Rifle DisablingShot"},{256,"Rifle 3RoundBurst"},{453,"Rifle RifleButt"},{454,"Rifle DeadlyShot"},
            {176,"SubM CoveringFire"},{248,"SubM SupressionFire"},{249,"SubM SlidingVolley"},
            {494,"SubM FullAuto"},{496,"SubM FullAutoRedux"},
            {259,"Dim_Mak"},{260,"Misdirect_Finger_Rake"},{261,"Machinegun_First_Combo"},{262,"Piston_Kicks"},
            {265,"Ki-Charged Reverse Punch"},{266,"Jump Sidekick Spin Kick Combo"},{267,"Sky High Sidekick"},
            {268,"Swirling Ki-summon"},{273,"AikidoAbility CntrThrowVsSMPunch"},{274,"AikidoAbility MakiOfootsweep"},
            {296,"AikidoAbility SpinClayPigeon"},{407,"AikidoAbility KickRevBlowout"},{411,"AikidoAbility AerialTakedown"},
            {412,"AikidoAbility TomoNage"},{427,"Triple_Front_Kick"},{428,"Suicidal_Butterfly"},
            {429,"Extrteme_Falling_Kick"},{430,"Wooden_Dummy_Drill"},{532,"Karate MachGunKick"},
            {534,"AikidoAbility ArmBarToKotoG"},{535,"Beserker Attack"},{536,"Foot Sweep Ki Punch"},
            {282,"Blinding Throw SR"},{1135,"Paralyzing Throw SR"}
        };
        public static IEnumerable<ushort> MappedSpecials => specialNames.Keys;
        public static Move Special(ushort ability, bool landed, CombatTactic targetTactic, char targetStyle = 'S')
        {
            if (!specialNames.TryGetValue(AbilityCatalog.Canonical(ability), out string name)) return null;
            if (!landed)
            {
                if (SneakAttacks.Bonus(ability, true) > 0)
                {
                    string style = targetStyle == 'W' ? "KungFu" : targetStyle == 'K' ? "Karate" : targetStyle == 'A' ? "Aikido" : "Self-Defense";
                    return All.FirstOrDefault(m => m.Group == "Sneak Attacks" && m.Name == "Failed Sneak v " + style);
                }
                // Failed choreography is only selected where the ILDB actually names it.
                return All.FirstOrDefault(m => m.Flags == 0x100 && m.Name == name + "_Failed");
            }
            string versus = name + " vs " + targetStyle + Suffix(targetTactic);
            return All.FirstOrDefault(m => m.Flags == 0x100 && m.Name == versus) ??
                All.FirstOrDefault(m => m.Flags == 0x100 && m.Name == name) ??
                All.FirstOrDefault(m => m.Flags == 0x100 && m.Name.StartsWith(name + " vs S", StringComparison.Ordinal));
        }
        public static char Suffix(CombatTactic tactic) => tactic == CombatTactic.Power ? 'P' :
            tactic == CombatTactic.Grab ? 'D' : tactic == CombatTactic.Block ? 'B' : 'S';
        /// <summary>The style's "Stand Transitions:To ..." move for the tactic (e.g. Trans Stand-WP for Kung-Fu Power).</summary>
        public static uint Transition(CombatTactic tactic, char style = 'S')
        {
            string name = "Trans Stand-" + style + Suffix(tactic);
            return All.FirstOrDefault(m => m.Name == name)?.Id ?? (uint)(21012 + (tactic == CombatTactic.Power ? 1 :
                tactic == CombatTactic.Grab ? 2 : tactic == CombatTactic.Block ? 3 : 0));
        }
        public static Move Choose(CombatTactic first, CombatTactic second, uint flags, uint variant,
            char firstStyle = 'S', char secondStyle = 'S')
        {
            if ((flags & CombatRules.OpportunityAttack) != 0)
                return All.First(m => m.Id == ((flags & CombatRules.Draw) != 0 ? 8198u : 9062u));
            uint result = flags & (CombatRules.AttackerHits | CombatRules.NoHits | CombatRules.BothHit | CombatRules.FinishingMove);
            if ((result & (CombatRules.AttackerHits | CombatRules.BothHit)) == 0) result = CombatRules.NoHits;
            string prefix = $"{firstStyle}{Suffix(first)} vs {secondStyle}{Suffix(second)}:";
            Move[] candidates = All.Where(m => m.Group.StartsWith(prefix, StringComparison.Ordinal) && m.Flags == result).ToArray();
            if (candidates.Length == 0)
            {
                prefix = $"S{Suffix(first)} vs S{Suffix(second)}:";
                candidates = All.Where(m => m.Group.StartsWith(prefix, StringComparison.Ordinal) && m.Flags == result).ToArray();
            }
            if (candidates.Length == 0) candidates = All.Where(m => m.Group.StartsWith("SS vs SS:", StringComparison.Ordinal) && m.Flags == result).ToArray();
            if (candidates.Length == 0) candidates = All.Where(m => m.Group.StartsWith("SS vs SS:", StringComparison.Ordinal) && m.Flags == CombatRules.NoHits).ToArray();
            return candidates[variant % candidates.Length];
        }
    }
}
