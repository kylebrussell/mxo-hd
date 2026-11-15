using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public OrdinaryItemCode.Entry GetOrdinaryItemCode(uint character, ushort slot)
        {
            using var connection = new MySqlConnection(connectionString); connection.Open();
            using var c = connection.CreateCommand();
            c.CommandText = "SELECT ability_id,level FROM char_abilities WHERE char_id=@char AND slot=@slot";
            c.Parameters.AddWithValue("@char", character); c.Parameters.AddWithValue("@slot", slot);
            using var r = c.ExecuteReader();
            if (!r.Read()) return null;
            var source = new OrdinaryItemCode.Entry { Slot = slot, Base = unchecked((uint)Convert.ToInt64(r[0])), Charges = Convert.ToInt32(r[1]) };
            return !r.Read() && OrdinaryItemCode.Valid(source.Base, source.Charges) ? source : null;
        }

        /// <summary>OURS: one item per successful charge; failure/cancellation never spends anything.</summary>
        public ClientActionRequests.Materialized MaterializeOrdinaryItemCode(uint character, OrdinaryItemCode.Entry source, byte tool, long cost)
        {
            var result = new ClientActionRequests.Materialized();
            if (source == null || !OrdinaryItemCode.Valid(source.Base, source.Charges) || !OrdinaryItemCode.Supported(source.Goid) || cost < 0) return result;
            using var connection = new MySqlConnection(connectionString); connection.Open();
            EnsureTransactionalInventory(connection);
            using var tx = connection.BeginTransaction(); using var c = connection.CreateCommand(); c.Transaction = tx;
            c.Parameters.AddWithValue("@char", character); c.Parameters.AddWithValue("@source", source.Slot);
            c.Parameters.AddWithValue("@code", (long)source.Base); c.Parameters.AddWithValue("@goid", source.Goid);
            c.Parameters.AddWithValue("@charges", source.Charges); c.Parameters.AddWithValue("@cost", cost);
            c.CommandText = "SELECT cash FROM characters WHERE charId=@char FOR UPDATE";
            object cash = c.ExecuteScalar(); if (cash == null) return result;
            result.Cash = Convert.ToInt64(cash);
            c.CommandText = "SELECT ability_id,level FROM char_abilities WHERE char_id=@char AND slot=@source FOR UPDATE";
            using (var r = c.ExecuteReader())
                if (!r.Read() || unchecked((uint)Convert.ToInt64(r[0])) != source.Base || Convert.ToInt32(r[1]) != source.Charges || r.Read()) return result;
            c.CommandText = "SELECT ability_id FROM char_abilities WHERE char_id=@char AND (ability_id & 0xffffffff)=2147767296 AND is_loaded=1 LIMIT 1 FOR UPDATE";
            if (c.ExecuteScalar() == null) return result;
            if (result.Cash < cost) { result.Result = 4; return result; }
            c.CommandText = "SELECT slot,goid,count FROM inventory WHERE charId=@char AND slot BETWEEN 1 AND 96 FOR UPDATE";
            var bag = new List<Crafting.InventoryItem>();
            using (var r = c.ExecuteReader()) while (r.Read()) bag.Add(new Crafting.InventoryItem(r.GetUInt16(0), r.GetUInt32(1), ItemStacks.CountFromDb(r.GetInt32(2))));
            if (!bag.Any(i => i.Slot == tool && i.Goid == 1271)) return result;
            var stack = bag.FirstOrDefault(i => i.Goid == source.Goid && ItemStacks.IsStackable(source.Goid) && i.Count < ItemStacks.MaxStack);
            ushort slot = stack.Slot == 0 ? Enumerable.Range(1,96).Select(i => (ushort)i).FirstOrDefault(i => bag.All(b => b.Slot != i)) : stack.Slot;
            if (slot == 0) { result.Result = 3; return result; }
            result.Slot = slot; result.BagCount = stack.Slot == 0 ? 1 : stack.Count + 1;
            c.Parameters.AddWithValue("@slot", slot); c.Parameters.AddWithValue("@count", result.BagCount);
            c.CommandText = stack.Slot == 0 ? "INSERT INTO inventory(charId,goid,slot,count,created) VALUES(@char,@goid,@slot,@count,NOW())" : "UPDATE inventory SET count=@count WHERE charId=@char AND slot=@slot AND goid=@goid";
            if (c.ExecuteNonQuery() != 1) return new ClientActionRequests.Materialized();
            c.CommandText = source.Charges == 1 ? "DELETE FROM char_abilities WHERE char_id=@char AND slot=@source AND ability_id=@code AND level=@charges" : "UPDATE char_abilities SET level=level-1 WHERE char_id=@char AND slot=@source AND ability_id=@code AND level=@charges";
            if (c.ExecuteNonQuery() != 1) return new ClientActionRequests.Materialized();
            c.CommandText = "UPDATE characters SET cash=cash-@cost WHERE charId=@char AND cash>=@cost";
            if (c.ExecuteNonQuery() != 1) return new ClientActionRequests.Materialized();
            tx.Commit(); result.Cash -= cost; result.Remaining = source.Charges - 1; result.Result = 0; return result;
        }
    }
}
