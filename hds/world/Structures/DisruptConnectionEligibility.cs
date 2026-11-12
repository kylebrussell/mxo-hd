using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    /// <summary>1124's explicit RequiresTool prerequisite; no executor or alias to passive384 is implied.</summary>
    public static class DisruptConnectionEligibility
    {
        public const ushort Ability = 1124;
        private static readonly Lazy<IReadOnlyDictionary<uint, ushort>> tools = new Lazy<IReadOnlyDictionary<uint, ushort>>(() => Read(DataLoader.DataPath("ability_tools.json")));
        public static IReadOnlyDictionary<uint, ushort> Tools => tools.Value;
        public static IReadOnlyDictionary<uint, ushort> Read(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.GetProperty("tools").EnumerateArray()
                .Where(r => r[2].ValueKind == JsonValueKind.Number)
                .ToDictionary(r => r[0].GetUInt32(), r => AbilityCatalog.IdOf(r[2].GetUInt32()));
        }
        // Native625f12c0 scans inventory slots0..106, checks component10, and compares the helper625f0be0's
        // compact ReqAbilityID. The generated map contains only actual component10 templates.
        public static bool HasTool(IEnumerable<Crafting.InventoryItem> inventory) => inventory.Any(i =>
            i.Slot < 107 && Tools.TryGetValue(i.Goid, out ushort prerequisite) && prerequisite == Ability);
        public static bool Refused(ushort ability, IEnumerable<Crafting.InventoryItem> inventory) =>
            AbilityCatalog.Canonical(ability) == Ability && !HasTool(inventory);
    }
}
