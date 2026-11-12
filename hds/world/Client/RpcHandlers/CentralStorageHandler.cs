using System;
using System.Collections.Generic;
using System.Linq;
using hds.databases;
using hds.databases.interfaces;
using hds.shared;

namespace hds
{
    /// <summary>
    /// The Loading Area's central storage window: <c>62</c> moves between the bag and storage, <c>67</c>/<c>68</c> with
    /// IsCentralStorageItem inside storage, and <c>5d</c> with PersonalInventoryItem 0 (recycling a storage entry). The
    /// rules are in <see cref="CentralStorage"/>; this applies them to the database and the client.
    /// </summary>
    public static class CentralStorageHandler
    {
        private static IWorldDBHandler Db => Store.dbManager.WorldDbHandler;

        /// <summary>The window is a Loading Area view (laviewinventory.cpp); nothing in the world sends these.</summary>
        public static bool IsOpenFor(WorldClient client) => client.playerData.DistrictIsLA();

        private static Func<int, CentralStorage.Entry> StorageOf(UInt32 charId)
        {
            Dictionary<int, CentralStorage.Entry> entries = new Dictionary<int, CentralStorage.Entry>();
            foreach (int slot in Db.GetAbilityInventorySlots(charId))
            {
                entries[slot] = CentralStorage.Entry.Ability;
            }
            foreach (CodeStorage.Entry entry in Db.GetCodeStorage(charId))
            {
                entries[entry.Slot] = CentralStorage.Entry.Code(entry.Goid, entry.Count);
            }
            return slot => entries.TryGetValue(slot, out CentralStorage.Entry entry) ? entry : default(CentralStorage.Entry);
        }

        private static Func<int?> FirstFreeStorage(UInt32 charId) =>
            () => CentralStorage.FirstFreeSlot(Db.GetAbilityInventorySlots(charId));

        private static Func<int, StackMoves.Stack> BagOf(UInt32 charId)
        {
            Dictionary<int, StackMoves.Stack> bag = new Dictionary<int, StackMoves.Stack>();
            foreach (Crafting.InventoryItem item in Db.GetInventoryItems(charId))
            {
                bag[item.Slot] = new StackMoves.Stack { Goid = item.Goid, Count = Math.Max(1, item.Count) };
            }
            return slot => bag.TryGetValue(slot, out StackMoves.Stack stack) ? stack : default(StackMoves.Stack);
        }

        private static int? FirstFreeBag(UInt32 charId)
        {
            HashSet<int> used = new HashSet<int>(Db.GetInventoryItems(charId).Select(i => (int)i.Slot));
            List<UInt16> free = TradeRules.FreeBagSlots(used);
            return free.Count > 0 ? free[0] : (int?)null;
        }

        /// <summary>Client 62.</summary>
        public static void ProcessMove(WorldClient client, byte[] rpcData)
        {
            if (!CentralStorage.TryParseMove(rpcData, out CentralStorage.Move move))
            {
                return;
            }
            UInt32 charId = client.playerData.getCharID();
            CentralStorage.Plan plan;
            if (!IsOpenFor(client))
            {
                plan = CentralStorage.Plan.Refuse(CentralStorage.InvalidTransaction);
            }
            else if (move.FromBag)
            {
                plan = CentralStorage.PlanUpload(move, BagOf(charId), StorageOf(charId), FirstFreeStorage(charId),
                    goid => Db.HasAbility(charId, unchecked((Int32)goid)));
            }
            else
            {
                plan = CentralStorage.PlanDownload(move, BagOf(charId), StorageOf(charId),
                    Enumerable.Range(1, MyWorldDbAccess.LastBagSlot), () => FirstFreeBag(charId));
            }

            string what = (move.FromBag ? "Bag slot " : "Storage slot ") + move.Source + " to " +
                          (move.FromBag ? "storage slot " : "bag slot ") + move.Dest;
            if (plan.Refused)
            {
                Output.WriteDebugLog("[CENTRAL STORAGE] " + what + " refused: " + CentralStorage.NameOf(plan.Status));
                if (move.FromBag)
                {
                    client.messageQueue.addRpcMessage(CentralStorage.BuildUploadResult(0, false, plan.Status));
                }
                else
                {
                    new ServerPackets().sendSystemChatMessage(client, "You can't do that: " + CentralStorage.Describe(plan.Status) + ".", "SYSTEM");
                }
                client.FlushQueue();
                return;
            }

            if (move.FromBag)
            {
                ApplyUpload(client, charId, plan);
            }
            else
            {
                ApplyDownload(client, charId, plan);
            }
            client.FlushQueue();
            Output.WriteDebugLog("[CENTRAL STORAGE] " + what + ": moved " + plan.Moved + " (" + CentralStorage.NameOf(plan.Status) + ")");
        }

        private static void ApplyUpload(WorldClient client, UInt32 charId, CentralStorage.Plan plan)
        {
            MarketPlaceHandler.Db.GetInventoryItem(charId, plan.BagSlot, out UInt32 goid, out UInt32 ignored);

            SetBagCount(client, charId, plan.BagSlot, plan.BagCount);
            if (plan.LearnAbility)
            {
                Db.AddAbility(charId, plan.StorageSlot, unchecked((Int32)goid), 1);
                client.messageQueue.addRpcMessage(ServerPackets.BuildKnownAbilityAdd(plan.StorageSlot, AbilityProgression.AbilityCodeValue(goid)));
            }
            else
            {
                SetStorage(client, charId, plan.StorageSlot, goid, plan.StorageCount, plan.StorageIsNew);
            }
            client.messageQueue.addRpcMessage(CentralStorage.BuildUploadResult(plan.LearnAbility ? 1 : plan.Moved, plan.LearnAbility, plan.Status));
        }

        private static void ApplyDownload(WorldClient client, UInt32 charId, CentralStorage.Plan plan)
        {
            UInt32 goid = StorageOf(charId)(plan.StorageSlot).Goid;
            SetStorage(client, charId, plan.StorageSlot, goid, plan.StorageCount, false);
            if (plan.BagIsNew)
            {
                MarketPlaceHandler.Db.AddInventoryItem(charId, plan.BagSlot, goid, 0);
                if (plan.BagCount > 1)
                {
                    Db.SetInventoryCount(plan.BagSlot, charId, plan.BagCount);
                }
                UInt32 descriptor = TradePackets.Descriptor(0, plan.BagCount);
                client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemAdd(plan.BagSlot, goid,
                    (byte)((descriptor >> 16) & 0xff), (byte)(descriptor >> 24)));
            }
            else
            {
                SetBagCount(client, charId, plan.BagSlot, plan.BagCount);
            }
        }

        /// <summary>A bag slot's new count: <c>69 01</c>, or <c>5f &lt;slot&gt; 01</c> when it empties.</summary>
        private static void SetBagCount(WorldClient client, UInt32 charId, UInt16 slot, int count)
        {
            if (count <= 0)
            {
                MarketPlaceHandler.Db.RemoveInventoryItem(charId, slot);
                client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(slot));
                return;
            }
            Db.SetInventoryCount(slot, charId, count);
            client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(slot, count));
        }

        /// <summary>A storage entry's new count: <c>5e</c> for a new one, <c>69 00</c> for a change, <c>5f &lt;slot&gt; 00</c> when it empties.</summary>
        private static void SetStorage(WorldClient client, UInt32 charId, UInt16 slot, UInt32 goid, int count, bool isNew)
        {
            if (count <= 0)
            {
                Db.RemoveCodeStorage(charId, slot);
                client.messageQueue.addRpcMessage(CentralStorage.BuildRemove(slot));
            }
            else if (isNew)
            {
                Db.AddCodeStorage(charId, new CodeStorage.Entry(slot, goid, count));
                client.messageQueue.addRpcMessage(CodeStorage.BuildAdd(slot, goid, count));
            }
            else
            {
                Db.SetCodeStorageCount(charId, slot, count);
                client.messageQueue.addRpcMessage(CodeStorage.BuildUpdate(slot, goid, count));
            }
        }

        private static void Refuse(WorldClient client, string what, UInt32 status)
        {
            Output.WriteDebugLog("[CENTRAL STORAGE] " + what + " refused: " + CentralStorage.NameOf(status));
            new ServerPackets().sendSystemChatMessage(client, "You can't do that: " + CentralStorage.Describe(status) + ".", "SYSTEM");
            client.FlushQueue();
        }

        /// <summary>Client 67 with IsCentralStorageItem 1.</summary>
        public static void ProcessMerge(WorldClient client, UInt16 source, UInt16 dest)
        {
            UInt32 charId = client.playerData.getCharID();
            Func<int, CentralStorage.Entry> storage = StorageOf(charId);
            CentralStorage.Plan plan = IsOpenFor(client)
                ? CentralStorage.PlanMerge(source, dest, storage)
                : CentralStorage.Plan.Refuse(CentralStorage.InvalidTransaction);
            if (plan.Refused)
            {
                Refuse(client, "Merge of storage slot " + source + " into " + dest, plan.Status);
                return;
            }
            UInt32 goid = storage(source).Goid;
            SetStorage(client, charId, plan.StorageSlot, goid, plan.StorageCount, false);
            SetStorage(client, charId, plan.SourceSlot, goid, plan.SourceCount, false);
            client.FlushQueue();
            Output.WriteDebugLog("[CENTRAL STORAGE] Merged storage slot " + source + " into " + dest + " (" + plan.StorageCount + ")");
        }

        /// <summary>Client 68 with IsCentralStorageItem 1.</summary>
        public static void ProcessSplit(WorldClient client, UInt16 source, UInt16 dest, UInt32 amount)
        {
            UInt32 charId = client.playerData.getCharID();
            Func<int, CentralStorage.Entry> storage = StorageOf(charId);
            CentralStorage.Plan plan = IsOpenFor(client)
                ? CentralStorage.PlanSplit(source, dest, amount, storage, FirstFreeStorage(charId))
                : CentralStorage.Plan.Refuse(CentralStorage.InvalidTransaction);
            if (plan.Refused)
            {
                Refuse(client, "Split of storage slot " + source, plan.Status);
                return;
            }
            UInt32 goid = storage(source).Goid;
            SetStorage(client, charId, plan.SourceSlot, goid, plan.SourceCount, false);
            SetStorage(client, charId, plan.StorageSlot, goid, plan.StorageCount, plan.StorageIsNew);
            client.FlushQueue();
            Output.WriteDebugLog("[CENTRAL STORAGE] Split " + amount + " of storage slot " + source + " to " + plan.StorageSlot);
        }

        /// <summary>
        /// Client <c>5d &lt;slot u16&gt; 00 &lt;RecycleItem&gt;</c>: the window's Recycle button on a storage entry (0x621d4ae0 takes slots
        /// below 0x208 when PersonalInventoryItem is 0). Paid as a bag recycle is (one item's price), answered as retail
        /// answered a bag recycle (<c>80 e7</c>, <c>80 e4</c>) with the storage removal <c>5f &lt;slot&gt; 00</c>.
        /// </summary>
        public static void ProcessRecycle(WorldClient client, UInt16 slot)
        {
            UInt32 charId = client.playerData.getCharID();
            CentralStorage.Entry entry = StorageOf(charId)(slot);
            UInt32 status = !IsOpenFor(client) ? CentralStorage.InvalidTransaction
                : !CentralStorage.IsStorageSlot(slot) ? CentralStorage.InvalidStorageLocation
                : entry.Kind == CentralStorage.Kind.Empty ? CentralStorage.StorageEmpty
                : entry.Kind == CentralStorage.Kind.Ability ? CentralStorage.InvalidStorageItem
                : CentralStorage.Success;
            if (status != CentralStorage.Success)
            {
                Refuse(client, "Recycle of storage slot " + slot, status);
                return;
            }

            long currentCash = client.playerData.getInfo();
            UInt32 value = VendorPricing.GetRecyclePrice(entry.Goid);
            UInt32 newCash = (UInt32)Math.Min(Math.Max(currentCash, 0) + (long)value, UInt32.MaxValue);
            Db.RemoveCodeStorage(charId, slot);
            Db.SaveInfo(client, newCash);
            client.playerData.setInfo(newCash);

            ServerPackets pak = new ServerPackets();
            pak.SendInfoDelta(client, (long)newCash - currentCash, ServerPackets.InfoReasonRecycle);
            pak.SendInfoCurrent(client, newCash, ServerPackets.InfoReasonRecycle);
            client.messageQueue.addRpcMessage(CentralStorage.BuildRemove(slot));
            client.FlushQueue();
            Output.WriteDebugLog("[CENTRAL STORAGE] Recycled storage slot " + slot + " (" + entry.Goid + " x" + entry.Count + ") for " + value);
        }
    }
}
