using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Splitting and merging stacks in the bag. The records are client.dll's (no retail capture has either); the
    /// client sends them from its drag-and-drop handler (0x620a0000):
    /// <list type="bullet">
    /// <item><c>68 &lt;SourceSlot u8&gt; &lt;DestSlot u8&gt; &lt;AmountToSplitOff u32&gt; &lt;IsCentralStorageItem u8&gt;</c>
    /// (class 0x6209b8e0, size 8, DestSlot defaults to 6b = none): the amount from the split dialog dropped on a
    /// slot (DestSlot is that slot) or on a bag page (DestSlot is the page's first free slot, 6b when it has none).</item>
    /// <item><c>67 &lt;Slot1 u16&gt; &lt;Slot2 u16&gt; &lt;IsCentralStorageItem u8&gt;</c> (0x6209b7c0, size 6): a stack dropped
    /// on a stack of the same item (0x620a0504), or on a bag page that has one with room (0x620a01e6).</item>
    /// <item><c>62 &lt;SourceSlot u16&gt; &lt;DestinationSlot u16&gt; &lt;PersonalInventoryItem u8&gt;</c> (0x621d4c00, 0x621e2000):
    /// between the bag and central storage: CentralStorageHandler, as are 67 and 68 with IsCentralStorageItem set.</item>
    /// </list>
    /// The answers are the records the server already uses for stacks, which the client took from retail:
    /// <c>69 01 &lt;slot u32&gt; &lt;(count - 1) * 4 u32&gt;</c> for a stack's new count, <c>5e</c> for a new stack and
    /// <c>5f</c> for an emptied slot.
    /// </summary>
    public static class StackMoves
    {
        public const byte NoSlot = 0x6b;
        public const int FirstWornSlot = 97;

        public class Split
        {
            public byte Source, Dest;
            public UInt32 Amount;
            public bool CentralStorage;
        }

        public class Merge
        {
            public UInt16 Source, Dest;
            public bool CentralStorage;
        }

        /// <summary>A bag slot's contents: goid 0 for empty.</summary>
        public struct Stack
        {
            public UInt32 Goid;
            public int Count;
            public uint Purity;
        }

        /// <summary>What a request does: counts after it (a 0 empties the slot), and the slot a new stack goes to.</summary>
        public class Plan
        {
            public string Refusal;
            public int SourceCount, DestCount;
            public UInt16 DestSlot;
            public bool DestIsNew;
        }

        /// <summary>The locked source and the outcome of attempting to persist both slot changes.</summary>
        public sealed class Receipt
        {
            public Plan Plan { get; }
            public Stack Source { get; }

            internal Receipt(Plan plan, Stack source)
            {
                Plan = plan;
                Source = source;
            }
        }

        public static bool TryParseSplit(byte[] rpcData, out Split split)
        {
            split = null;
            if (rpcData == null || rpcData.Length < 7)
            {
                return false;
            }
            split = new Split
            {
                Source = rpcData[0], Dest = rpcData[1], Amount = BitConverter.ToUInt32(rpcData, 2), CentralStorage = rpcData[6] != 0
            };
            return true;
        }

        public static bool TryParseMerge(byte[] rpcData, out Merge merge)
        {
            merge = null;
            if (rpcData == null || rpcData.Length < 5)
            {
                return false;
            }
            merge = new Merge
            {
                Source = BitConverter.ToUInt16(rpcData, 0), Dest = BitConverter.ToUInt16(rpcData, 2), CentralStorage = rpcData[4] != 0
            };
            return true;
        }

        private static bool IsBagSlot(int slot) => slot >= 0 && slot < FirstWornSlot;

        /// <summary>
        /// Splits the amount off the source (all of it moves the whole stack). Into an empty destination it makes a new stack; onto the same
        /// item it adds while the stack has room; with no destination (6b) it takes the first free bag slot.
        /// </summary>
        public static Plan PlanSplit(Split split, Func<int, Stack> at, Func<int?> firstFreeSlot, int maxStack)
        {
            Stack source = at(split.Source);
            if (split.CentralStorage) return new Plan { Refusal = "central storage is not supported" };
            if (!IsBagSlot(split.Source) || source.Goid == 0) return new Plan { Refusal = "no item in the source slot" };
            if (split.Amount < 1 || split.Amount > source.Count) return new Plan { Refusal = "the stack doesn't hold that many" };

            int amount = (int)split.Amount;
            if (amount > maxStack) return new Plan { Refusal = "the destination stack is full" };
            if (split.Dest == NoSlot || split.Dest == split.Source)
            {
                int? free = firstFreeSlot();
                if (free == null) return new Plan { Refusal = "no free bag slot" };
                return new Plan { SourceCount = source.Count - amount, DestCount = amount, DestSlot = (UInt16)free.Value, DestIsNew = true };
            }
            if (!IsBagSlot(split.Dest)) return new Plan { Refusal = "not a bag slot" };
            Stack dest = at(split.Dest);
            if (dest.Goid == 0)
            {
                return new Plan { SourceCount = source.Count - amount, DestCount = amount, DestSlot = split.Dest, DestIsNew = true };
            }
            if (dest.Goid != source.Goid) return new Plan { Refusal = "the destination holds another item" };
            if (TradePackets.Descriptor(source.Purity, 1) != TradePackets.Descriptor(dest.Purity, 1)) return new Plan { Refusal = "the items have different properties" };
            if (amount > maxStack - dest.Count) return new Plan { Refusal = "the destination stack is full" };
            return new Plan { SourceCount = source.Count - amount, DestCount = dest.Count + amount, DestSlot = split.Dest };
        }

        /// <summary>Moves as much of the source stack onto the destination stack of the same item as fits.</summary>
        public static Plan PlanMerge(Merge merge, Func<int, Stack> at, int maxStack)
        {
            if (merge.CentralStorage) return new Plan { Refusal = "central storage is not supported" };
            if (!IsBagSlot(merge.Source) || !IsBagSlot(merge.Dest) || merge.Source == merge.Dest) return new Plan { Refusal = "not two bag slots" };
            Stack source = at(merge.Source), dest = at(merge.Dest);
            if (source.Goid == 0 || dest.Goid == 0) return new Plan { Refusal = "an empty slot" };
            if (source.Goid != dest.Goid) return new Plan { Refusal = "different items" };
            if (TradePackets.Descriptor(source.Purity, 1) != TradePackets.Descriptor(dest.Purity, 1)) return new Plan { Refusal = "the items have different properties" };
            int moving = Math.Min(source.Count, maxStack - dest.Count);
            if (moving <= 0) return new Plan { Refusal = "the destination stack is full" };
            return new Plan { SourceCount = source.Count - moving, DestCount = dest.Count + moving, DestSlot = merge.Dest };
        }
    }

    public static class StackHandler
    {
        private static void Publish(WorldClient client, ushort sourceSlot, StackMoves.Receipt result, bool split)
        {
            var plan = result.Plan;
            var source = result.Source;
            string movement = split ? "Split" : "Merge";
            if (plan.Refusal != null)
            {
                Output.WriteDebugLog($"[INVENTORY] {movement} of slot {sourceSlot} refused: {plan.Refusal}");
                if (source.Goid != 0 && ItemStacks.IsStackable(source.Goid))
                    client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(sourceSlot, source.Count));
                new ServerPackets().sendSystemChatMessage(client, "You can't do that: " + plan.Refusal + ".", "BROADCAST");
                client.FlushQueue();
                return;
            }

            byte[] sourceUpdate = plan.SourceCount == 0 ? ServerPackets.BuildInventoryItemDelete(sourceSlot) :
                ItemStacks.BuildStackCount(sourceSlot, plan.SourceCount);
            byte[] destinationUpdate = plan.DestIsNew ? ServerPackets.BuildInventoryItemAdd(plan.DestSlot,
                source.Goid, TradePackets.Descriptor(source.Purity, plan.DestCount)) :
                ItemStacks.BuildStackCount(plan.DestSlot, plan.DestCount);
            client.messageQueue.addRpcMessage(split ? sourceUpdate : destinationUpdate);
            client.messageQueue.addRpcMessage(split ? destinationUpdate : sourceUpdate);
            client.FlushQueue();
            Output.WriteDebugLog($"[INVENTORY] {movement} committed char={client.playerData.getCharID()} " +
                $"source={sourceSlot} count={plan.SourceCount} destination={plan.DestSlot} count={plan.DestCount}");
        }

        /// <summary>Client 68: split a stack.</summary>
        public static void ProcessSplit(WorldClient client, byte[] rpcData)
        {
            if (!StackMoves.TryParseSplit(rpcData, out StackMoves.Split split))
            {
                return;
            }
            if (split.CentralStorage)
            {
                CentralStorageHandler.ProcessSplit(client, split.Source, split.Dest, split.Amount);
                return;
            }
            var result = Store.dbManager.WorldDbHandler.SplitBagStack(client.playerData.getCharID(), split);
            Publish(client, split.Source, result, split: true);
        }

        /// <summary>Client 67: merge two stacks of the same item.</summary>
        public static void ProcessMerge(WorldClient client, byte[] rpcData)
        {
            if (!StackMoves.TryParseMerge(rpcData, out StackMoves.Merge merge))
            {
                return;
            }
            if (merge.CentralStorage)
            {
                CentralStorageHandler.ProcessMerge(client, merge.Source, merge.Dest);
                return;
            }
            var result = Store.dbManager.WorldDbHandler.MergeBagStack(client.playerData.getCharID(), merge);
            Publish(client, merge.Source, result, split: false);
        }
    }
}
