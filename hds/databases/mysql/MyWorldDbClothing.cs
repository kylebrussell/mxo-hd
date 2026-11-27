using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint Goid, ushort WornSlot, ushort BagSlot, ushort DisplacedSlot, int[] Rsi, string Refusal) MoveClothing(
            uint character, byte source, byte destination, bool equip)
        {
            const string changed = "That clothing move is unavailable. Your items were kept.";
            var invalid = (0u, (ushort)0, (ushort)0, (ushort)0, (int[])null, changed);
            if (equip ? source < 1 || source > LastBagSlot : source < 0x61 || source > 0x68 || destination > LastBagSlot)
                return invalid;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            // Startup prepares inventory and rsivalues for the same transaction.
            using var tx = connection.BeginTransaction();
            var bag = ReadLockedBagStacks(tx, character);
            if (bag == null) return invalid;

            ushort wornSlot = source, bagSlot = destination, displacedSlot = 0;
            uint goid = 0, selectedId = 0;
            ClothingItem clothing = null;
            (ushort Slot, string Model, string Color) part = default;
            if (equip)
            {
                if (!bag.TryGetValue(source, out var selected) || selected.Item.Count != 1) return invalid;
                goid = selected.Item.Goid;
                selectedId = selected.Id;
                clothing = DataLoader.getInstance().getItemValues(goid);
                part = ClothingPart(clothing.getClothesType());
                if (part.Slot == 0 || destination != 0x6b && destination != part.Slot) return invalid;
                wornSlot = part.Slot;
                bagSlot = source;
            }

            var worn = ReadLockedWornItem(tx, character, wornSlot);
            if (worn.Ambiguous) return invalid;
            if (equip)
            {
                if (worn.Id != 0)
                {
                    displacedSlot = TradeRules.FreeBagSlots(bag.Keys).FirstOrDefault();
                    if (displacedSlot == 0) return FullBag();
                    if (!MoveLockedInventoryRow(tx, worn.Id, displacedSlot)) return invalid;
                }
                if (!MoveLockedInventoryRow(tx, selectedId, wornSlot)) return invalid;
            }
            else
            {
                if (worn.Id == 0) return invalid;
                goid = worn.Goid;
                clothing = DataLoader.getInstance().getItemValues(goid);
                part = ClothingPart(clothing.getClothesType());
                if (part.Slot != wornSlot) return invalid;
                if (bagSlot == 0) bagSlot = TradeRules.FreeBagSlots(bag.Keys).FirstOrDefault();
                if (bagSlot == 0) return FullBag();
                if (bag.ContainsKey(bagSlot))
                    return (0, 0, 0, 0, null, "Empty that bag slot first. Your items were kept.");
                if (!MoveLockedInventoryRow(tx, worn.Id, bagSlot)) return invalid;
            }

            using var rsi = new MySqlCommand("UPDATE rsivalues SET " + part.Model + "=@model" +
                (part.Color == null ? "" : ", " + part.Color + "=@color") + " WHERE charid=@character", connection, tx);
            rsi.Parameters.AddWithValue("@character", character);
            rsi.Parameters.AddWithValue("@model", equip ? clothing.getModelId() : 0);
            if (part.Color != null) rsi.Parameters.AddWithValue("@color", equip ? clothing.getColorId() : 0);
            if (rsi.ExecuteNonQuery() != 1) return invalid;
            rsi.CommandText = "SELECT " + RsiColumns + " FROM rsivalues WHERE charid=@character";
            var appearance = new int[22];
            using (var row = rsi.ExecuteReader())
            {
                if (!row.Read()) return invalid;
                for (int i = 0; i < appearance.Length; i++) appearance[i] = row.GetInt16(i);
            }
            tx.Commit();
            return (goid, wornSlot, bagSlot, displacedSlot, appearance, null);

            static (uint, ushort, ushort, ushort, int[], string) FullBag() =>
                (0, 0, 0, 0, null, "Your inventory is full. Your clothes were kept.");
        }

        // The numeric and named categories are both present in the shipped clothing data.
        private static (ushort Slot, string Model, string Color) ClothingPart(string category) => category switch
        {
            "1950892361" => (0x61, "hat", null),
            "FEMALE HAT" => (0x61, "hat", null),
            "MALE HAT" => (0x61, "hat", null),
            "2001027401" => (0x62, "glasses", "glassescolor"),
            "FEMALE GLASSES" => (0x62, "glasses", "glassescolor"),
            "MALE GLASSES" => (0x62, "glasses", "glassescolor"),
            "1750286665" => (0x63, "shirt", "shirtcolor"),
            "FEMALE SHIRT" => (0x63, "shirt", "shirtcolor"),
            "MALE SHIRT" => (0x63, "shirt", "shirtcolor"),
            "1816609097" => (0x64, "gloves", null),
            "FEMALE GLOVES" => (0x64, "gloves", null),
            "MALE GLOVES" => (0x64, "gloves", null),
            "2001682761" => (0x65, "coat", "coatcolor"),
            "FEMALE COAT" => (0x65, "coat", "coatcolor"),
            "MALE COAT" => (0x65, "coat", "coatcolor"),
            "1397768521" => (0x66, "pants", null),
            "FEMALE PANTS" => (0x66, "pants", null),
            "MALE PANTS" => (0x66, "pants", null),
            "1733050697" => (0x67, "leggins", null),
            "FEMALE LEGGINGS" => (0x67, "leggins", null),
            "2001092937" => (0x68, "shoes", "shoecolor"),
            "FEMALE SHOES" => (0x68, "shoes", "shoecolor"),
            "MALE SHOES" => (0x68, "shoes", "shoecolor"),
            _ => default
        };
    }
}
