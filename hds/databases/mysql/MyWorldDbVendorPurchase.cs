using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (ushort Slot, int Count, uint Cash, uint Price) PurchaseVendorItem(uint character, uint goid)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.Parameters.AddWithValue("@char", character);
            command.CommandText = "SELECT cash FROM characters WHERE charId=@char FOR UPDATE";
            object balance = command.ExecuteScalar();
            if (balance == null || !VendorPricing.TryApplyBuy(Convert.ToInt64(balance), goid, out uint cash, out uint price))
                throw new AbilityAcquisition.Refusal("You do not have enough information to buy this item.");

            command.CommandText = "SELECT invId,slot,goid,count FROM inventory WHERE charId=@char AND slot BETWEEN 1 AND 96 ORDER BY slot FOR UPDATE";
            var bag = new List<AbilityAcquisition.BagRow>();
            using (var rows = command.ExecuteReader())
                while (rows.Read())
                    bag.Add(new AbilityAcquisition.BagRow(rows.GetUInt32(0), rows.GetInt32(1), rows.GetInt64(2), rows.GetInt32(3)));
            if (bag.GroupBy(item => item.Slot).Any(group => group.Count() > 1))
                throw new AbilityAcquisition.Refusal("Your bag has ambiguous slots. The purchase was refused.");
            var stack = ItemStacks.IsStackable(goid) ? bag.FirstOrDefault(item => item.Goid == goid && item.Count >= 0 &&
                ItemStacks.CountFromDb(item.Count) < ItemStacks.MaxStack) : null;
            ushort slot = stack != null ? (ushort)stack.Slot : AbilityAcquisition.FreeBag(bag);
            int count = stack != null ? ItemStacks.CountFromDb(stack.Count) + 1 : 1;
            command.Parameters.AddWithValue("@slot", slot);
            command.Parameters.AddWithValue("@goid", goid);
            command.Parameters.AddWithValue("@count", count);
            if (stack != null) command.Parameters.AddWithValue("@id", stack.Id);
            command.CommandText = stack == null
                ? "INSERT INTO inventory(charId,goid,slot,count,created) VALUES(@char,@goid,@slot,@count,NOW())"
                : "UPDATE inventory SET count=@count WHERE invId=@id";
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The vendor inventory write failed.");
            command.Parameters.AddWithValue("@cash", cash);
            command.CommandText = "UPDATE characters SET cash=@cash WHERE charId=@char";
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The vendor wallet write failed.");
            transaction.Commit();
            return (slot, count, cash, price);
        }
    }
}
