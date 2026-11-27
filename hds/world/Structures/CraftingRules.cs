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
            public int? Complexity, Price, DefaultCodeCharges;
            public bool CantCraft, CantDecompile, Learnable;
        }
        private static readonly Lazy<Dictionary<uint, Item>> items = new Lazy<Dictionary<uint, Item>>(() =>
        {
            using var data = JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("crafting_rules.json")));
            var charges = data.RootElement.TryGetProperty("ordinary_code_charges", out var codeCharges)
                ? codeCharges.EnumerateObject().ToDictionary(row => uint.Parse(row.Name), row => (int?)row.Value.GetInt32())
                : new Dictionary<uint, int?>();
            return data.RootElement.GetProperty("items").EnumerateArray().Select(row => new Item {
                Goid = row[0].GetUInt32(), Name = row[1].GetString(), Complexity = Number(row[2]), Price = Number(row[3]),
                DefaultCodeCharges = charges.GetValueOrDefault(row[0].GetUInt32()),
                Category = row[4].ValueKind == JsonValueKind.Null ? 0 : row[4].GetUInt32(), CantCraft = Number(row[5]) == 1,
                CantDecompile = Number(row[6]) == 1, Learnable = Number(row[7]) == 1,
                ToolAbility = row[8].ValueKind == JsonValueKind.Null ? 0 : row[8].GetUInt32()
            }).ToDictionary(i => i.Goid);
        });
        private static int? Number(JsonElement value) => value.ValueKind == JsonValueKind.Null ? (int?)null : value.GetInt32();
        public static Item Find(uint goid) => items.Value.TryGetValue(goid, out var item) ? item : null;

        public static bool CanWriteAbilityCode(uint goid)
        {
            var item = Find(goid);
            return AbilityProgression.IsAbilityCode(goid) && item is { CantCraft: false } && item.Complexity >= 0 &&
                (item.Category & 0xff) == (byte)'A' &&
                AbilityCatalog.Find(AbilityCatalog.IdOf(goid))?.Number("Ability/AbilityClass") == 0 &&
                Crafting.Recipes.ContainsKey(goid);
        }

        // constants.Crafting.CompileCompileCostMultiplier = .12, client.dll 6269fd01..fd70.
        public static long InfoCost(Item item, int discount = 0) => (long)((item.Price ?? 1000) * .12 *
            (1 - Math.Clamp(discount, 0, 100) / 100.0));
        // WriteCodeTest checks the fractional price before debiting the whole-Info amount.
        public static (long Debit, long Required) WriterCost(Item item, int compileDiscount, int writerDiscount)
        {
            double price = .01 * (100 - Math.Clamp(writerDiscount, 0, 100)) * InfoCost(item, compileDiscount);
            return ((long)price, (long)Math.Ceiling(price));
        }
        public static bool Succeeds(int skill, int complexity, int roll, int specialization = 0)
        {
            // CompileTest adds the positive category bonus before checking the effective skill.
            int effectiveSkill = skill + Math.Max(0, specialization) / 10;
            return effectiveSkill > 0 && 4 * effectiveSkill + roll > 4 * complexity + 50;
        }

        public static IReadOnlyList<uint> RecoverFragments(Crafting.Recipe recipe, int skill, Func<int> roll)
        {
            var recovered = new List<uint>();
            // Write Code can turn a single recipe's ingredients into several physical copies.
            // Bound recovery per copy until we track that provenance across inventory transfers.
            int limit = recipe.Ingredients.Length / Math.Max(1, Find(recipe.ResultGoid)?.DefaultCodeCharges ?? 1);
            if (limit == 0) return recovered;
            foreach (uint goid in recipe.Ingredients)
            {
                var fragment = Find(goid);
                // Shipped DecompileFragmentRecoveryTest rolls against the fragment's own complexity.
                if (fragment?.Complexity > 0 && (fragment.Category & 0x00ff00ff) == 0x00430046 && // F?C: code fragment
                    CodeStorage.IsUploadable(goid) && Succeeds(skill, fragment.Complexity.Value, roll()))
                {
                    recovered.Add(goid);
                    if (recovered.Count == limit) break;
                }
            }
            return recovered;
        }

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
