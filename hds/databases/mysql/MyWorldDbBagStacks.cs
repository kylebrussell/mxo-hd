using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using hds.shared;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public StackMoves.Receipt SplitBagStack(uint character, StackMoves.Split split) =>
            MoveBagStack(character, split.Source, split, null);

        public StackMoves.Receipt MergeBagStack(uint character, StackMoves.Merge merge) =>
            MoveBagStack(character, merge.Source, null, merge);

        private StackMoves.Receipt MoveBagStack(uint character, ushort sourceSlot, StackMoves.Split split, StackMoves.Merge merge)
        {
            StackMoves.Stack source = default;
            StackMoves.Receipt Denied(string reason) => new StackMoves.Receipt(new StackMoves.Plan { Refusal = reason }, source);

            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                EnsureTransactionalInventory(connection);
                using var tx = connection.BeginTransaction();
                var bag = new Dictionary<int, (uint Id, StackMoves.Stack Item)>();
                using (var read = new MySqlCommand(
                    "SELECT invId,slot,goid,count,purity FROM inventory WHERE charId=@character AND slot BETWEEN 0 AND 96 " +
                    "ORDER BY slot,invId FOR UPDATE", connection, tx))
                {
                    read.Parameters.AddWithValue("@character", character);
                    using var rows = read.ExecuteReader();
                    while (rows.Read())
                    {
                        int slot = rows.GetByte(1);
                        long goid = rows.GetInt64(2);
                        var item = new StackMoves.Stack
                        {
                            Goid = goid > 0 && goid <= uint.MaxValue ? (uint)goid : 0,
                            Count = ItemStacks.CountFromDb(rows.GetInt32(3)),
                            Purity = unchecked((uint)rows.GetInt32(4))
                        };
                        if (!bag.TryAdd(slot, (rows.GetUInt32(0), item)))
                            return Denied("a backpack slot is ambiguous");
                    }
                }

                StackMoves.Stack At(int slot) => bag.TryGetValue(slot, out var row) ? row.Item : default;
                int? FirstFree() => TradeRules.FreeBagSlots(bag.Keys).Select(slot => (int?)slot).FirstOrDefault();
                source = At(sourceSlot);
                var plan = split != null ? StackMoves.PlanSplit(split, At, FirstFree, ItemStacks.MaxStack) :
                    StackMoves.PlanMerge(merge, At, ItemStacks.MaxStack);
                if (plan.Refusal != null) return new StackMoves.Receipt(plan, source);
                if (!ItemStacks.IsStackable(source.Goid)) return Denied("that item does not stack");
                if (plan.DestIsNew && bag.ContainsKey(plan.DestSlot)) return Denied("the destination is occupied");

                using var debit = new MySqlCommand(null, connection, tx);
                if (!WriteBagCount(debit, bag[sourceSlot].Id, plan.SourceCount))
                    return Denied("the source stack changed");
                using var credit = new MySqlCommand(null, connection, tx);
                if (plan.DestIsNew)
                {
                    credit.CommandText = "INSERT INTO inventory(charId,goid,slot,count,purity) " +
                        "VALUES(@character,@goid,@slot,@count,@purity)";
                    credit.Parameters.AddWithValue("@character", character);
                    credit.Parameters.AddWithValue("@goid", source.Goid);
                    credit.Parameters.AddWithValue("@slot", plan.DestSlot);
                    credit.Parameters.AddWithValue("@count", plan.DestCount == 1 ? 0 : plan.DestCount);
                    credit.Parameters.AddWithValue("@purity", unchecked((int)source.Purity));
                    if (credit.ExecuteNonQuery() != 1) return Denied("the destination stack changed");
                }
                else if (!WriteBagCount(credit, bag[plan.DestSlot].Id, plan.DestCount))
                {
                    return Denied("the destination stack changed");
                }

                tx.Commit();
                return new StackMoves.Receipt(plan, source);
            }
            catch (DbException error)
            {
                Output.WriteDebugLog($"[INVENTORY] Stack move rolled back char={character} source={sourceSlot}: {error.Message}");
                return Denied("the stack move could not be saved. Your items were kept");
            }
        }
    }
}
