using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    public static class CraftingRules
    {
        public sealed class Item
        {
            public uint Goid, Category, ToolAbility;
            public string Name;
            public int? Complexity, Price;
            public bool CantCraft, CantDecompile, Learnable;
        }
        private static readonly Lazy<Dictionary<uint, Item>> items = new Lazy<Dictionary<uint, Item>>(() =>
        {
            using var data = JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("crafting_rules.json")));
            return data.RootElement.GetProperty("items").EnumerateArray().Select(row => new Item {
                Goid = row[0].GetUInt32(), Name = row[1].GetString(), Complexity = Number(row[2]), Price = Number(row[3]),
                Category = row[4].ValueKind == JsonValueKind.Null ? 0 : row[4].GetUInt32(), CantCraft = Number(row[5]) == 1,
                CantDecompile = Number(row[6]) == 1, Learnable = Number(row[7]) == 1,
                ToolAbility = row[8].ValueKind == JsonValueKind.Null ? 0 : row[8].GetUInt32()
            }).ToDictionary(i => i.Goid);
        });
        private static int? Number(JsonElement value) => value.ValueKind == JsonValueKind.Null ? (int?)null : value.GetInt32();
        public static Item Find(uint goid) => items.Value.TryGetValue(goid, out var item) ? item : null;

        // constants.Crafting.CompileCompileCostMultiplier = .12, client.dll 6269fd01..fd70.
        public static long InfoCost(Item item, int discount = 0) => (long)((item.Price ?? 1000) * .12 *
            (1 - Math.Clamp(discount, 0, 100) / 100.0));
        public static bool Succeeds(int skill, int complexity, int roll, int specialization = 0) =>
            skill > 0 && 4 * (skill + specialization / 10) + roll > 4 * complexity + 50;

        /// <summary>Preflight result capacity after ingredients have gone, including a stack that loses its last item.</summary>
        public static bool HasResultSpace(uint result, IList<Crafting.InventoryItem> inventory, IList<ushort> consumed)
        {
            var used = consumed.Where(s => (s & Crafting.StorageSlotFlag) == 0).GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
            var remaining = inventory.Where(i => i.Slot > 0 && i.Slot < 97)
                .Select(i => (i.Slot, i.Goid, Count: i.Count - (used.TryGetValue(i.Slot, out int n) ? n : 0))).Where(i => i.Count > 0).ToList();
            return remaining.Count < 96 || ItemStacks.IsStackable(result) && remaining.Any(i => i.Goid == result && i.Count < ItemStacks.MaxStack);
        }
    }
}
