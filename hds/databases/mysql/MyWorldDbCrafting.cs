using System;
using System.Collections.Generic;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private void EnsureRecipes()
        {
            ExecuteNonResultQuery("CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, " +
                "goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY (charId,goid)) ENGINE=InnoDB");
        }

        public HashSet<UInt32> GetLearnedRecipes(UInt32 charId)
        {
            EnsureRecipes();
            OpenConnection();
            try
            {
                using var command = conn.CreateCommand();
                command.CommandText = "SELECT goid FROM char_recipes WHERE charId=" + charId;
                using var reader = command.ExecuteReader();
                var result = new HashSet<UInt32>();
                while (reader.Read()) result.Add(Convert.ToUInt32(reader.GetValue(0)));
                return result;
            }
            finally { CloseConnection(); }
        }

        /// <summary>Consumes an owned source and persists its recipe together. Negative means the source changed.</summary>
        public int DecompileItem(UInt32 charId, UInt16 slot, UInt32 expectedGoid)
        {
            EnsureRecipes();
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.Parameters.AddWithValue("@char", charId);
            command.Parameters.AddWithValue("@slot", slot);
            command.Parameters.AddWithValue("@goid", expectedGoid);
            command.CommandText = "SELECT invId, goid, count FROM inventory WHERE charId=@char AND slot=@slot FOR UPDATE";
            uint inventoryId;
            int count;
            using (var rows = command.ExecuteReader())
            {
                if (!rows.Read() || rows.GetUInt32(1) != expectedGoid) return -1;
                inventoryId = rows.GetUInt32(0);
                count = ItemStacks.CountFromDb(rows.GetInt32(2));
                if (rows.Read()) return -1;
            }
            command.Parameters.AddWithValue("@id", inventoryId);
            command.Parameters.AddWithValue("@count", count - 1);
            command.CommandText = count == 1 ? "DELETE FROM inventory WHERE invId=@id" :
                "UPDATE inventory SET count=@count WHERE invId=@id";
            if (command.ExecuteNonQuery() != 1) return -1;
            command.CommandText = "INSERT INTO char_recipes (charId,goid) VALUES (@char,@goid) ON DUPLICATE KEY UPDATE goid=VALUES(goid)";
            command.ExecuteNonQuery();
            transaction.Commit();
            return count - 1;
        }
    }
}
