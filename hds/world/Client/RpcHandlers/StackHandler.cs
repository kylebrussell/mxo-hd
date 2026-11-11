using System;
using System.Collections.Generic;
using System.Linq;
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
    /// between the bag and central storage, which this server doesn't have. Not handled.</item>
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
        }

        /// <summary>What a request does: counts after it (a 0 empties the slot), and the slot a new stack goes to.</summary>
        public class Plan
        {
            public string Refusal;
            public int SourceCount, DestCount;
            public UInt16 DestSlot;
            public bool DestIsNew;
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
            if (dest.Count + amount > maxStack) return new Plan { Refusal = "the destination stack is full" };
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
            int moving = Math.Min(source.Count, maxStack - dest.Count);
            if (moving <= 0) return new Plan { Refusal = "the destination stack is full" };
            return new Plan { SourceCount = source.Count - moving, DestCount = dest.Count + moving, DestSlot = merge.Dest };
        }
    }

    public static class StackHandler
    {
        private static StackMoves.Stack At(UInt32 charId, int slot)
        {
            MarketPlaceHandler.Db.GetInventoryItem(charId, (UInt16)slot, out UInt32 goid, out UInt32 ignored);
            return new StackMoves.Stack
            {
                Goid = goid, Count = goid == 0 ? 0 : ItemStacks.CountFromDb(MarketPlaceHandler.Db.GetInventoryCount(charId, (UInt16)slot))
            };
        }

        private static int? FirstFree(UInt32 charId)
        {
            HashSet<int> used = new HashSet<int>(Store.dbManager.WorldDbHandler.GetInventoryItems(charId).Select(i => (int)i.Slot));
            List<UInt16> free = TradeRules.FreeBagSlots(used);
            return free.Count > 0 ? free[0] : (int?)null;
        }

        private static void Refuse(WorldClient client, string what, StackMoves.Plan plan, UInt16 source)
        {
            Output.WriteDebugLog("[INVENTORY] " + what + " refused: " + plan.Refusal);
            // Put the client's view of the source back, in case it moved anything itself
            StackMoves.Stack stack = At(client.playerData.getCharID(), source);
            if (stack.Goid != 0 && ItemStacks.IsStackable(stack.Goid))
            {
                client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(source, stack.Count));
            }
            new ServerPackets().sendSystemChatMessage(client, "You can't do that: " + plan.Refusal + ".", "SYSTEM");
            client.FlushQueue();
        }

        private static void SetCount(WorldClient client, UInt16 slot, int count)
        {
            UInt32 charId = client.playerData.getCharID();
            if (count <= 0)
            {
                MarketPlaceHandler.Db.RemoveInventoryItem(charId, slot);
                client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(slot));
                return;
            }
            Store.dbManager.WorldDbHandler.SetInventoryCount(slot, charId, count);
            client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(slot, count));
        }

        /// <summary>Client 68: split a stack.</summary>
        public static void ProcessSplit(WorldClient client, byte[] rpcData)
        {
            if (!StackMoves.TryParseSplit(rpcData, out StackMoves.Split split))
            {
                return;
            }
            UInt32 charId = client.playerData.getCharID();
            StackMoves.Plan plan = StackMoves.PlanSplit(split, slot => At(charId, slot), () => FirstFree(charId), ItemStacks.MaxStack);
            if (plan.Refusal != null)
            {
                Refuse(client, "Split of slot " + split.Source, plan, split.Source);
                return;
            }
            MarketPlaceHandler.Db.GetInventoryItem(charId, split.Source, out UInt32 goid, out UInt32 purity);
            SetCount(client, split.Source, plan.SourceCount);
            if (plan.DestIsNew)
            {
                MarketPlaceHandler.Db.AddInventoryItem(charId, plan.DestSlot, goid, purity);
                if (plan.DestCount > 1)
                {
                    Store.dbManager.WorldDbHandler.SetInventoryCount(plan.DestSlot, charId, plan.DestCount);
                }
                UInt32 descriptor = TradePackets.Descriptor(purity, plan.DestCount);
                client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemAdd(plan.DestSlot, goid,
                    (byte)((descriptor >> 16) & 0xff), (byte)(descriptor >> 24)));
            }
            else
            {
                SetCount(client, plan.DestSlot, plan.DestCount);
            }
            client.FlushQueue();
            Output.WriteDebugLog("[INVENTORY] Split " + split.Amount + " of slot " + split.Source + " to slot " + plan.DestSlot);
        }

        /// <summary>Client 67: merge two stacks of the same item.</summary>
        public static void ProcessMerge(WorldClient client, byte[] rpcData)
        {
            if (!StackMoves.TryParseMerge(rpcData, out StackMoves.Merge merge))
            {
                return;
            }
            UInt32 charId = client.playerData.getCharID();
            StackMoves.Plan plan = StackMoves.PlanMerge(merge, slot => At(charId, slot), ItemStacks.MaxStack);
            if (plan.Refusal != null)
            {
                Refuse(client, "Merge of slot " + merge.Source + " into " + merge.Dest, plan, merge.Source);
                return;
            }
            SetCount(client, merge.Dest, plan.DestCount);
            SetCount(client, merge.Source, plan.SourceCount);
            client.FlushQueue();
            Output.WriteDebugLog("[INVENTORY] Merged slot " + merge.Source + " into " + merge.Dest + " (" + plan.DestCount + ")");
        }
    }
}
