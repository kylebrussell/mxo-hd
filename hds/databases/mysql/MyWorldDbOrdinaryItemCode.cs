using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public CraftingRequests.WrittenCode WriteRecipeCode(uint character, CraftingRequests.Compile request, long cost, long requiredInfo)
        {
            var refused = new CraftingRequests.WrittenCode { Result = CraftingRequests.WriterRefused };
            bool abilityCode = CraftingRules.CanWriteAbilityCode(request.Item);
            if (request.SelectedIngredients.Count == 0 ||
                (!abilityCode && !OrdinaryItemCode.Supported(request.Item)) || cost < 0 || requiredInfo < cost)
                return refused;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.Parameters.AddWithValue("@char", character);
            command.Parameters.AddWithValue("@cost", cost);
            command.CommandText = "SELECT cash FROM characters WHERE charId=@char FOR UPDATE";
            object balance = command.ExecuteScalar();
            if (balance == null) return refused;
            long cash = Convert.ToInt64(balance);
            if (cash < requiredInfo) return new CraftingRequests.WrittenCode { Result = CraftingRequests.WriterNoInfo };

            var usedCodeSlots = new HashSet<int>();
            var ambiguousCodeSlots = new HashSet<int>();
            var available = new List<Crafting.InventoryItem>();
            bool writingLoaded = false;
            command.CommandText = "SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id=@char AND slot<520 FOR UPDATE";
            using (var rows = command.ExecuteReader())
            {
                while (rows.Read())
                {
                    ushort slot = rows.GetUInt16(0);
                    uint value = unchecked((uint)Convert.ToInt64(rows[1]));
                    if (!usedCodeSlots.Add(slot)) ambiguousCodeSlots.Add(slot);
                    if (value == (0x80000000u | 37u << 10) && rows.GetBoolean(3)) writingLoaded = true;
                    if (CodeStorage.IsCode(value))
                        available.Add(new Crafting.InventoryItem((ushort)(slot | Crafting.StorageSlotFlag),
                            CodeStorage.GoidOf(value), rows.GetInt32(2) + 1));
                }
            }
            if (!writingLoaded) return refused;
            command.CommandText = "SELECT slot,goid,count FROM inventory WHERE charId=@char AND slot BETWEEN 0 AND 96 FOR UPDATE";
            using (var rows = command.ExecuteReader())
                while (rows.Read())
                    available.Add(new Crafting.InventoryItem(rows.GetUInt16(0), rows.GetUInt32(1), ItemStacks.CountFromDb(rows.GetInt32(2))));

            if (!available.Any(item => item.Slot == request.ToolSlot && item.Goid == CraftingSession.WriterTool)) return refused;
            var receipt = new CraftingRequests.WrittenCode { Result = CraftingRequests.WriterSuccess, Cash = cash - cost };
            if (!abilityCode)
            {
                int? codeSlot = CentralStorage.FirstFreeSlot(usedCodeSlots);
                if (!codeSlot.HasValue) return new CraftingRequests.WrittenCode { Result = CraftingRequests.WriterNoSpace };
                receipt.Slot = (ushort)codeSlot.Value;
            }
            command.Parameters.AddWithValue("@slot", 0);
            command.Parameters.AddWithValue("@id", 0);
            command.Parameters.AddWithValue("@count", 0);
            foreach (var (ingredient, quantity) in request.SelectedIngredients)
            {
                var matches = available.Where(item => item.Slot == ingredient.Slot).ToList();
                if (matches.Count != 1 || matches[0].Goid != ingredient.Goid || matches[0].Count < quantity)
                    return refused;
                var item = matches[0];
                int slot = ingredient.Slot & ~Crafting.StorageSlotFlag;
                bool stored = slot != ingredient.Slot;
                if (stored && ambiguousCodeSlots.Contains(slot)) return refused;
                int remaining = item.Count - quantity;
                command.Parameters["@slot"].Value = slot;
                command.Parameters["@id"].Value = stored ? (object)CodeStorage.BaseValue(item.Goid) : item.Goid;
                command.Parameters["@count"].Value = stored ? remaining - 1 : remaining;
                if (stored)
                    command.CommandText = remaining == 0
                        ? "DELETE FROM char_abilities WHERE char_id=@char AND slot=@slot AND ability_id=@id"
                        : "UPDATE char_abilities SET level=@count WHERE char_id=@char AND slot=@slot AND ability_id=@id";
                else
                    command.CommandText = remaining == 0
                        ? "DELETE FROM inventory WHERE charId=@char AND slot=@slot AND goid=@id"
                        : "UPDATE inventory SET count=@count WHERE charId=@char AND slot=@slot AND goid=@id";
                if (command.ExecuteNonQuery() != 1) return refused;
                receipt.Ingredients.Add((item, remaining));
            }

            if (abilityCode)
            {
                var occupied = available.Where(item => Crafting.IsBagSlot(item.Slot)).Select(item => item.Slot).ToHashSet();
                foreach (var (ingredient, remaining) in receipt.Ingredients)
                    if (remaining == 0 && Crafting.IsBagSlot(ingredient.Slot))
                        occupied.Remove(ingredient.Slot);
                receipt.Slot = Enumerable.Range(1, 96).Select(slot => (ushort)slot).FirstOrDefault(slot => !occupied.Contains(slot));
                if (receipt.Slot == 0) return new CraftingRequests.WrittenCode { Result = CraftingRequests.WriterNoSpace };
                command.Parameters["@slot"].Value = receipt.Slot;
                command.Parameters["@id"].Value = request.Item;
                command.CommandText = "INSERT INTO inventory(charId,goid,slot,count,created) VALUES(@char,@id,@slot,1,NOW())";
            }
            else
            {
                command.Parameters["@slot"].Value = receipt.Slot;
                command.Parameters["@id"].Value = request.Item << 7;
                command.Parameters["@count"].Value = CraftingRules.Find(request.Item).DefaultCodeCharges.Value;
                command.CommandText = "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name,added) " +
                    "VALUES(@char,@slot,@id,@count,0,'Written item code',NOW())";
            }
            if (command.ExecuteNonQuery() != 1) return refused;
            if (cost != 0)
            {
                command.CommandText = "UPDATE characters SET cash=cash-@cost WHERE charId=@char AND cash>=@cost";
                if (command.ExecuteNonQuery() != 1) return refused;
            }
            transaction.Commit();
            return receipt;
        }

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
