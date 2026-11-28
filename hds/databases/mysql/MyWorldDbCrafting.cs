using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private readonly object recipesTableLock = new object();
        private volatile bool recipesTableEnsured;

        private void EnsureRecipes()
        {
            if (recipesTableEnsured) return;
            lock (recipesTableLock)
            {
                if (recipesTableEnsured) return;
                NonQuery("CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, " +
                    "goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY (charId,goid)) ENGINE=InnoDB");
                recipesTableEnsured = true;
            }
        }

        public HashSet<UInt32> GetLearnedRecipes(UInt32 charId)
        {
            EnsureRecipes();
            var result = new HashSet<UInt32>();
            QueryRows("SELECT goid FROM char_recipes WHERE charId = @charId",
                reader => result.Add(Convert.ToUInt32(reader.GetValue(0))),
                P("@charId", charId));
            return result;
        }

        /// <summary>Consumes an owned source and either learns its recipe or places recovered fragments in one transaction.</summary>
        public Crafting.DecompileResult DecompileItem(uint charId, ushort slot, byte toolSlot, uint expectedGoid,
            bool knownByDefault, IReadOnlyList<uint> recovered)
        {
            EnsureRecipes();
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.Parameters.AddWithValue("@char", charId);
            command.Parameters.AddWithValue("@goid", expectedGoid);
            command.CommandText = "SELECT invId, slot, goid, count, purity FROM inventory " +
                "WHERE charId=@char AND slot BETWEEN 1 AND 96 ORDER BY slot FOR UPDATE";
            var bag = new SortedDictionary<ushort, (uint Id, uint Goid, int Count, int Purity)>();
            var refused = new Crafting.DecompileResult { Outcome = Crafting.DecompileOutcome.SourceChanged };
            using (var rows = command.ExecuteReader())
            {
                while (rows.Read())
                    if (!bag.TryAdd(rows.GetUInt16(1), (rows.GetUInt32(0), unchecked((uint)rows.GetInt64(2)),
                        ItemStacks.CountFromDb(rows.GetInt32(3)), rows.GetInt32(4))))
                        return refused;
            }
            if (!bag.TryGetValue(slot, out var source) || source.Goid != expectedGoid ||
                !bag.TryGetValue(toolSlot, out var tool) || tool.Goid != Crafting.DecompilerTool)
                return refused;

            command.CommandText = "SELECT goid FROM char_recipes WHERE charId=@char AND goid=@goid FOR UPDATE";
            bool learned = !knownByDefault && command.ExecuteScalar() == null;
            int remaining = source.Count - 1;
            if (remaining == 0) bag.Remove(slot);
            else bag[slot] = (source.Id, source.Goid, remaining, source.Purity);

            var destinations = new SortedSet<ushort>();
            if (!learned)
            {
                foreach (uint fragment in recovered)
                {
                    ushort destination = bag.FirstOrDefault(entry => entry.Value.Goid == fragment &&
                        entry.Value.Purity == 0 && entry.Value.Count < ItemStacks.MaxStack).Key;
                    if (destination == 0)
                    {
                        destination = Enumerable.Range(1, 96).Select(s => (ushort)s).FirstOrDefault(s => !bag.ContainsKey(s));
                        if (destination == 0)
                            return new Crafting.DecompileResult { Outcome = Crafting.DecompileOutcome.BagFull };
                    }
                    var existing = bag.GetValueOrDefault(destination);
                    bag[destination] = (existing.Id, fragment, existing.Count + 1, 0);
                    destinations.Add(destination);
                }
            }

            var id = command.Parameters.AddWithValue("@id", source.Id);
            var count = command.Parameters.AddWithValue("@count", remaining);
            command.CommandText = remaining == 0 ? "DELETE FROM inventory WHERE invId=@id" :
                "UPDATE inventory SET count=@count WHERE invId=@id";
            if (command.ExecuteNonQuery() != 1) return refused;
            if (learned)
            {
                command.CommandText = "INSERT INTO char_recipes (charId,goid) VALUES (@char,@goid)";
                if (command.ExecuteNonQuery() != 1) return refused;
            }

            var bagSlot = command.Parameters.AddWithValue("@slot", slot);
            var fragmentGoid = command.Parameters.AddWithValue("@fragment", 0u);
            var grants = new List<(Crafting.InventoryItem Item, bool Added)>();
            foreach (ushort destination in destinations)
            {
                var grant = bag[destination];
                bool added = grant.Id == 0;
                id.Value = grant.Id;
                count.Value = grant.Count;
                bagSlot.Value = destination;
                fragmentGoid.Value = grant.Goid;
                command.CommandText = added
                    ? "INSERT INTO inventory(charId,goid,slot,count,purity,created) VALUES(@char,@fragment,@slot,@count,0,NOW())"
                    : "UPDATE inventory SET count=@count WHERE invId=@id";
                if (command.ExecuteNonQuery() != 1) return refused;
                grants.Add((new Crafting.InventoryItem(destination, grant.Goid, grant.Count), added));
            }
            transaction.Commit();
            return new Crafting.DecompileResult
            {
                Outcome = Crafting.DecompileOutcome.Succeeded, Remaining = remaining, Learned = learned, Grants = grants
            };
        }
    }
}
