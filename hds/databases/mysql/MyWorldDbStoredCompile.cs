using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public ClientActionRequests.Materialized MaterializeStoredCode(uint character, ushort sourceSlot, uint goid, long cost)
        {
            var result = new ClientActionRequests.Materialized();
            if (!CodeStorage.IsUploadable(goid) || cost < 0) return result;
            using var connection = new MySqlConnection(connectionString); connection.Open();
            EnsureTransactionalInventory(connection);
            using var tx = connection.BeginTransaction(); using var c = connection.CreateCommand(); c.Transaction = tx;
            c.Parameters.AddWithValue("@char", character); c.Parameters.AddWithValue("@source", sourceSlot);
            c.Parameters.AddWithValue("@code", CodeStorage.BaseValue(goid)); c.Parameters.AddWithValue("@goid", goid);
            c.Parameters.AddWithValue("@cost", cost);
            c.CommandText = "SELECT cash FROM characters WHERE charId=@char FOR UPDATE";
            object cash = c.ExecuteScalar(); if (cash == null) return result;
            result.Cash = Convert.ToInt64(cash);
            c.CommandText = "SELECT level FROM char_abilities WHERE char_id=@char AND slot=@source AND ability_id=@code FOR UPDATE";
            object source = c.ExecuteScalar(); if (source == null) return result;
            int count = Convert.ToInt32(source) + 1; if (count < 1 || count > 128) return result;
            if (result.Cash < cost) { result.Result = 4; return result; }
            c.CommandText = "SELECT slot,goid,count FROM inventory WHERE charId=@char AND slot BETWEEN 1 AND 96 FOR UPDATE";
            var bag = new List<Crafting.InventoryItem>();
            using (var r = c.ExecuteReader()) while (r.Read()) bag.Add(new Crafting.InventoryItem(r.GetUInt16(0), r.GetUInt32(1), ItemStacks.CountFromDb(r.GetInt32(2))));
            var stack = bag.FirstOrDefault(i => i.Goid == goid && ItemStacks.IsStackable(goid) && i.Count < ItemStacks.MaxStack);
            ushort slot = stack.Slot;
            if (slot == 0) slot = Enumerable.Range(1,96).Select(i => (ushort)i).FirstOrDefault(i => bag.All(b => b.Slot != i));
            if (slot == 0) { result.Result = 3; return result; }
            result.Slot = slot; result.BagCount = stack.Slot == 0 ? 1 : stack.Count + 1;
            c.Parameters.AddWithValue("@slot", slot); c.Parameters.AddWithValue("@count", result.BagCount);
            c.CommandText = stack.Slot == 0 ? "INSERT INTO inventory(charId,goid,slot,count,created) VALUES(@char,@goid,@slot,@count,NOW())" :
                "UPDATE inventory SET count=@count WHERE charId=@char AND slot=@slot AND goid=@goid";
            if (c.ExecuteNonQuery() != 1) return new ClientActionRequests.Materialized();
            c.CommandText = count == 1 ? "DELETE FROM char_abilities WHERE char_id=@char AND slot=@source AND ability_id=@code" :
                "UPDATE char_abilities SET level=level-1 WHERE char_id=@char AND slot=@source AND ability_id=@code";
            if (c.ExecuteNonQuery() != 1) return new ClientActionRequests.Materialized();
            c.CommandText = "UPDATE characters SET cash=cash-@cost WHERE charId=@char AND cash>=@cost";
            c.ExecuteNonQuery(); tx.Commit();
            result.Cash -= cost; result.Remaining = count - 1; result.Result = 0; return result;
        }
    }
}
