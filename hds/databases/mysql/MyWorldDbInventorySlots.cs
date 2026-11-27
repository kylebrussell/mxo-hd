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

            command.CommandText = "UPDATE inventory SET slot=@slot WHERE invId=@id";
            var slot = command.Parameters.AddWithValue("@slot", destination);
            var row = command.Parameters.AddWithValue("@id", selected.Value);
            if (command.ExecuteNonQuery() != 1) return false;
            if (displaced.HasValue)
            {
                slot.Value = source;
                row.Value = displaced.Value;
                if (command.ExecuteNonQuery() != 1) return false;
            }
            tx.Commit();
            return true;
        }
    }
}
