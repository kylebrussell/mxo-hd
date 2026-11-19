using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using hds.shared;

namespace hds
{
    /// <summary>DisguiseGrant's indirect tool: shipped Tool/Disguise resource, never a model supplied by the client.</summary>
    public static class AbilityDisguise
    {
        public const ushort Parent = 120;
        public const ushort Ability = 1114;

        internal static bool TryGrantLevel(ushort parent, ushort learned, out ushort level)
        {
            level = 0;
            if (parent != Parent) return false;
            var entry = AbilityCatalog.Find(parent);
            var grant = entry.Modifiers.SingleOrDefault(m => m.Has("TargetAbilityGrant"));
            if (grant == null || AbilityCatalog.IdOf((uint)grant.Number("TargetAbilityGrant")) != Ability ||
                grant.Number("WhenEnabled", -1) != 0 || grant.Number("DurationType", -1) != 2 ||
                grant.Number("ValueFunction", -1) != 2) return false;
            int rank = Math.Min(learned, (int)entry.Number("Ability/AbilityLevelCap", 50));
            if (!AbilityValues.TryValue(grant, rank, 0, out int value) || value < 0 || value > 50) return false;
            level = (ushort)value;
            return true;
        }

        private static readonly Lazy<IReadOnlyDictionary<uint,uint>> models = new Lazy<IReadOnlyDictionary<uint,uint>>(() =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("disguise_tools.json")));
            return doc.RootElement.GetProperty("tools").EnumerateArray().ToDictionary(
                e => e.GetProperty("goid").GetUInt32(), e => e.GetProperty("model").GetUInt32());
        });
        public static bool TryModel(uint goid, out uint model) => models.Value.TryGetValue(goid, out model);
        public static uint ModelFor(IEnumerable<Crafting.InventoryItem> inventory, ushort? selectedSlot = null)
        {
            // Inventory.findToolForAbility convention: a supplied indirect slot is authoritative; otherwise first carried tool.
            foreach (var item in inventory.OrderBy(i => i.Slot))
                if (item.Slot < 97 && (!selectedSlot.HasValue || item.Slot == selectedSlot) && TryModel(item.Goid, out uint model)) return model;
            return 0;
        }
        public static uint ModelFor(WorldClient client, ushort? selectedSlot = null) => ModelFor(
            Store.dbManager.WorldDbHandler.GetInventoryItems(client.playerData.getCharID()), selectedSlot);
        public static double Seconds(int learnedLevel, int durationBonus) => 31 + Math.Max(0,learnedLevel) / 2 + durationBonus;
    }
}
