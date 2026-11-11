using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    /// <summary>Sourced static Self-Defense animations. Other styles and special-move choreography currently
    /// fall back to these unarmed exchanges; selecting the full ILDB rule language remains separate.</summary>
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
        public static char Suffix(CombatTactic tactic) => tactic == CombatTactic.Power ? 'P' :
            tactic == CombatTactic.Grab ? 'D' : tactic == CombatTactic.Block ? 'B' : 'S';
        public static uint Transition(CombatTactic tactic) => (uint)(21012 + (tactic == CombatTactic.Power ? 1 :
            tactic == CombatTactic.Grab ? 2 : tactic == CombatTactic.Block ? 3 : 0));
        public static Move Choose(CombatTactic first, CombatTactic second, uint flags, uint variant)
        {
            if ((flags & CombatRules.OpportunityAttack) != 0)
                return All.First(m => m.Id == ((flags & CombatRules.Draw) != 0 ? 8198u : 9062u));
            uint result = flags & (CombatRules.AttackerHits | CombatRules.NoHits | CombatRules.BothHit | CombatRules.FinishingMove);
            if ((result & (CombatRules.AttackerHits | CombatRules.BothHit)) == 0) result = CombatRules.NoHits;
            string prefix = $"S{Suffix(first)} vs S{Suffix(second)}:";
            Move[] candidates = All.Where(m => m.Group.StartsWith(prefix, StringComparison.Ordinal) && m.Flags == result).ToArray();
            if (candidates.Length == 0) candidates = All.Where(m => m.Group.StartsWith("SS vs SS:", StringComparison.Ordinal) && m.Flags == result).ToArray();
            if (candidates.Length == 0) candidates = All.Where(m => m.Group.StartsWith("SS vs SS:", StringComparison.Ordinal) && m.Flags == CombatRules.NoHits).ToArray();
            return candidates[variant % candidates.Length];
        }
    }
}
