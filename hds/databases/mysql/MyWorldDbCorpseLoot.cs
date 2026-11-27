using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint Cash, (int Index, ushort Slot, int Count, bool Added)[] Items) ClaimCorpseLoot(
            uint character, uint information, IReadOnlyList<uint> items)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using var tx = connection.BeginTransaction();
            using var wallet = new MySqlCommand("SELECT cash FROM characters WHERE charId=@char FOR UPDATE", connection, tx);
            wallet.Parameters.AddWithValue("@char", character);
            object balance = wallet.ExecuteScalar();
            if (balance == null) return default;
            int before = Convert.ToInt32(balance);
            if (before < 0) return default;
            uint cash = (uint)Math.Min(before + (long)information, int.MaxValue);
            var bag = ReadLockedBagStacks(tx, character);
            if (bag == null) return default;

            if (cash != before)
            {
                wallet.CommandText = "UPDATE characters SET cash=@cash WHERE charId=@char";
                wallet.Parameters.AddWithValue("@cash", cash);
                if (wallet.ExecuteNonQuery() != 1) return default;
            }

            var granted = new List<(int Index, ushort Slot, int Count, bool Added)>();
            for (int i = 0; i < items.Count; i++)
            {
                uint goid = items[i];
                ushort stackSlot = ItemStacks.IsStackable(goid) ? bag
                    .Where(row => row.Key > 0 && row.Value.Item.Goid == goid && row.Value.Item.Count < ItemStacks.MaxStack)
                    .Select(row => (ushort)row.Key).DefaultIfEmpty().Min() : (ushort)0;
                using var write = new MySqlCommand(null, connection, tx);
                if (stackSlot != 0)
                {
                    var stack = bag[stackSlot];
                    stack.Item.Count++;
                    if (!WriteBagCount(write, stack.Id, stack.Item.Count)) return default;
                    bag[stackSlot] = stack;
                    granted.Add((i, stackSlot, stack.Item.Count, false));
                }
                else
                {
                    ushort slot = TradeRules.FreeBagSlots(bag.Keys).FirstOrDefault();
                    if (slot == 0) continue;
                    if (!InsertBagStack(write, character, slot, goid, 1, 0)) return default;
                    bag.Add(slot, (checked((uint)write.LastInsertedId), new StackMoves.Stack { Goid = goid, Count = 1 }));
                    granted.Add((i, slot, 1, true));
                }
            }

            tx.Commit();
            return (cash, granted.ToArray());
        }
    }
}
