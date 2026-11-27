using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        /// <summary>Moves or swaps exact inventory rows. A worn slot can only take the legacy empty-slot move.</summary>
        public bool MoveInventorySlots(uint character, ushort source, ushort destination)
        {
            if (source == destination || source > PvpArchives.BookSlot || destination > PvpArchives.BookSlot)
                return false;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using var tx = connection.BeginTransaction();
            using var command = new MySqlCommand(
                "SELECT invId,slot FROM inventory WHERE charId=@character AND slot IN (@source,@dest) " +
                "ORDER BY slot,invId FOR UPDATE", connection, tx);
            command.Parameters.AddWithValue("@character", character);
            command.Parameters.AddWithValue("@source", source);
            command.Parameters.AddWithValue("@dest", destination);
            uint? selected = null, displaced = null;
            using (var rows = command.ExecuteReader())
            {
                while (rows.Read())
                {
                    uint id = rows.GetUInt32(0);
                    if (rows.GetByte(1) == source)
                    {
                        if (selected.HasValue) return false;
                        selected = id;
                    }
                    else
                    {
                        if (displaced.HasValue) return false;
                        displaced = id;
                    }
                }
            }

            if (!selected.HasValue || (displaced.HasValue && (source > LastBagSlot || destination > LastBagSlot)))
                return false;

            if (!MoveLockedInventoryRow(tx, selected.Value, destination)) return false;
            if (displaced.HasValue && !MoveLockedInventoryRow(tx, displaced.Value, source)) return false;
            tx.Commit();
            return true;
        }

        public (uint Goid, ushort BagSlot, ushort DisplacedSlot, string Refusal) MoveWeapon(
            uint character, byte bagSlot, bool equip)
        {
            const string fullBag = "Your inventory is full. Your weapons were kept.";
            const string changed = "That weapon changed. Your items were kept.";
            if (bagSlot > LastBagSlot || (equip && bagSlot == 0)) return (0, 0, 0, changed);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using var tx = connection.BeginTransaction();

            var bag = ReadLockedBagStacks(tx, character);
            if (bag == null) return (0, 0, 0, "A bag slot is ambiguous. Your items were kept.");
            using var hand = new MySqlCommand(
                "SELECT invId FROM inventory WHERE charId=@character AND slot=@hand FOR UPDATE", connection, tx);
            hand.Parameters.AddWithValue("@character", character);
            hand.Parameters.AddWithValue("@hand", InventoryHandler.WeaponSlot);
            uint? held = null;
            using (var rows = hand.ExecuteReader())
            {
                if (rows.Read()) held = rows.GetUInt32(0);
                if (rows.Read()) return (0, 0, 0, "The hand slot is ambiguous. Your items were kept.");
            }

            ushort FreeBagSlot() => TradeRules.FreeBagSlots(bag.Keys).FirstOrDefault();
            if (!equip)
            {
                if (!held.HasValue) return (0, 0, 0, changed);
                ushort destination = bagSlot == 0 ? FreeBagSlot() : bagSlot;
                if (destination == 0) return (0, 0, 0, fullBag);
                if (bag.ContainsKey(destination)) return (0, 0, 0, "Empty that bag slot first. Your items were kept.");
                if (!MoveLockedInventoryRow(tx, held.Value, destination)) return (0, 0, 0, changed);
                tx.Commit();
                return (0, destination, 0, null);
            }

            if (!bag.TryGetValue(bagSlot, out var row) || row.Item.Count != 1 || !RangedWeapons.IsGun(row.Item.Goid))
                return (0, 0, 0, changed);
            ushort displacedSlot = 0;
            if (held.HasValue)
            {
                displacedSlot = FreeBagSlot();
                if (displacedSlot == 0) return (0, 0, 0, fullBag);
                if (!MoveLockedInventoryRow(tx, held.Value, displacedSlot)) return (0, 0, 0, changed);
            }
            if (!MoveLockedInventoryRow(tx, row.Id, InventoryHandler.WeaponSlot)) return (0, 0, 0, changed);
            tx.Commit();
            return (row.Item.Goid, bagSlot, displacedSlot, null);
        }

        private static bool MoveLockedInventoryRow(MySqlTransaction tx, uint id, ushort destination)
        {
            using var command = new MySqlCommand("UPDATE inventory SET slot=@slot WHERE invId=@id", tx.Connection, tx);
            command.Parameters.AddWithValue("@slot", destination);
            command.Parameters.AddWithValue("@id", id);
            return command.ExecuteNonQuery() == 1;
        }
    }
}
