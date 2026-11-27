using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        // Must run in the same transaction as WriteBagConsumption.
        private static int LockOwnedBagItem(MySqlCommand command, uint charId, ushort slot, uint goid, out uint id)
        {
            id = 0;
            command.Parameters.AddWithValue("@char", charId);
            command.Parameters.AddWithValue("@slot", slot);
            command.CommandText = "SELECT invId, goid, count FROM inventory WHERE charId=@char AND slot=@slot FOR UPDATE";
            using var row = command.ExecuteReader();
            if (!row.Read() || row.GetInt64(1) != goid) return -1;
            id = row.GetUInt32(0);
            int remaining = ItemStacks.CountFromDb(row.GetInt32(2)) - 1;
            return row.Read() ? -1 : remaining;
        }

        private static bool WriteBagConsumption(MySqlCommand command, uint id, int remaining)
        {
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@remaining", remaining);
            command.CommandText = remaining == 0 ? "DELETE FROM inventory WHERE invId=@id" :
                "UPDATE inventory SET count=@remaining WHERE invId=@id";
            return command.ExecuteNonQuery() == 1;
        }

        public int ConsumeOwnedBagItem(uint charId, ushort slot, uint goid)
        {
            if (slot < 1 || slot > LastBagSlot) return -1;
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var tx = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = tx;
            int remaining = LockOwnedBagItem(command, charId, slot, goid, out uint id);
            if (remaining < 0 || !WriteBagConsumption(command, id, remaining)) return -1;
            tx.Commit();
            return remaining;
        }
    }
}
