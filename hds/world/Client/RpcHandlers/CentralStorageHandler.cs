using System;
using System.Collections.Generic;
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

        /// <summary>Client 62.</summary>
        public static void ProcessMove(WorldClient client, byte[] rpcData)
        {
            if (!CentralStorage.TryParseMove(rpcData, out var move)) return;
            lock (client.playerData.craftingSync)
            {
                var publication = new AbilityAcquisition.Publication(client);
                var result = IsOpenFor(client) ? Db.MoveCentralStorage(publication.Character, move) :
                    new CentralStorage.Receipt(CentralStorage.Plan.Refuse(CentralStorage.InvalidTransaction));
                var plan = result.Plan;
                if (plan.Refused)
                {
                    PublishRefusal(client, publication, result, move.FromBag);
                    return;
                }
                if (plan.LearnAbility)
                {
                    UploadAbility(client, publication, plan, result.Goid);
                    client.FlushQueue();
                    return;
                }

                byte[] bag = BagUpdate(plan, result.Goid);
                byte[] archive = StorageUpdate(plan.StorageSlot, result.Goid, plan.StorageCount, plan.StorageIsNew);
                byte[][] packets = move.FromBag ? new[] { bag, archive, CentralStorage.BuildUploadResult(plan.Moved, false, plan.Status) }
                    : new[] { archive, bag };
                if (publication.Publish(client, packets)) client.FlushQueue();
                Output.WriteDebugLog($"[CENTRAL STORAGE] {(move.FromBag ? "Upload" : "Download")} committed " +
                    $"char={publication.Character} bag={plan.BagSlot} count={plan.BagCount} " +
                    $"archive={plan.StorageSlot} count={plan.StorageCount} moved={plan.Moved}");
            }
        }

        private static void UploadAbility(WorldClient client, AbilityAcquisition.Publication publication,
            CentralStorage.Plan plan, uint goid)
        {
            AbilityAcquisition.Receipt receipt;
            try { receipt = Db.AcquireAbility(publication.Character, goid, 0, plan.BagSlot, plan.StorageSlot); }
            catch (Exception error)
            {
                Output.WriteDebugLog("[ABILITY ACQUISITION] LA upload refused/rolled back: " + error.Message);
                publication.Notice(client, error is AbilityAcquisition.Refusal ? error.Message :
                    "The ability upload could not be completed. Its code was kept.");
                publication.Publish(client, CentralStorage.BuildUploadResult(0, false, CentralStorage.InvalidTransaction));
                return;
            }
            CodeStorageHandler.PublishLearn(client, receipt, publication, true);
        }

        private static byte[] BagUpdate(CentralStorage.Plan plan, uint goid)
        {
            if (plan.BagCount == 0) return ServerPackets.BuildInventoryItemDelete(plan.BagSlot);
            if (!plan.BagIsNew) return ItemStacks.BuildStackCount(plan.BagSlot, plan.BagCount);
            return ServerPackets.BuildInventoryItemAdd(plan.BagSlot, goid, TradePackets.Descriptor(0, plan.BagCount));
        }

        private static byte[] StorageUpdate(ushort slot, uint goid, int count, bool isNew)
        {
            if (count == 0) return CentralStorage.BuildRemove(slot);
            return isNew ? CodeStorage.BuildAdd(slot, goid, count) : CodeStorage.BuildUpdate(slot, goid, count);
        }

        private static void PublishRefusal(WorldClient client, AbilityAcquisition.Publication publication,
            CentralStorage.Receipt result, bool upload)
        {
            uint status = result.Plan.Status;
            Output.WriteDebugLog($"[CENTRAL STORAGE] Move refused char={publication.Character}: {CentralStorage.NameOf(status)}");
            string notice = result.RolledBack ? "The storage move could not be saved. Your items were kept." :
                "You can't do that: " + CentralStorage.Describe(status) + ".";
            byte[] message = ServerPackets.BuildSystemChatMessage(notice, "BROADCAST");
            byte[][] packets;
            if (!upload) packets = new[] { message };
            else
            {
                byte[] statusPacket = CentralStorage.BuildUploadResult(0, false, status);
                packets = result.RolledBack ? new[] { message, statusPacket } : new[] { statusPacket };
            }
            if (publication.Publish(client, packets)) client.FlushQueue();
        }

        /// <summary>Client 67 with IsCentralStorageItem 1.</summary>
        public static void ProcessMerge(WorldClient client, ushort source, ushort dest) =>
            MoveStorageStack(client, source, dest, null);

        /// <summary>Client 68 with IsCentralStorageItem 1.</summary>
        public static void ProcessSplit(WorldClient client, ushort source, ushort dest, uint amount) =>
            MoveStorageStack(client, source, dest, amount);

        private static void MoveStorageStack(WorldClient client, ushort source, ushort dest, uint? amount)
        {
            lock (client.playerData.craftingSync)
            {
                var publication = new AbilityAcquisition.Publication(client);
                var result = IsOpenFor(client) ? Db.MoveCentralStorageStack(publication.Character, source, dest, amount) :
                    new CentralStorage.Receipt(CentralStorage.Plan.Refuse(CentralStorage.InvalidTransaction));
                var plan = result.Plan;
                if (plan.Refused)
                {
                    PublishRefusal(client, publication, result, upload: false);
                    return;
                }

                byte[] from = StorageUpdate(plan.SourceSlot, result.Goid, plan.SourceCount, false);
                byte[] to = StorageUpdate(plan.StorageSlot, result.Goid, plan.StorageCount, plan.StorageIsNew);
                byte[][] packets = amount.HasValue ? new[] { from, to } : new[] { to, from };
                if (publication.Publish(client, packets)) client.FlushQueue();
                Output.WriteDebugLog($"[CENTRAL STORAGE] {(amount.HasValue ? "Split" : "Merge")} committed " +
                    $"char={publication.Character} source={source} count={plan.SourceCount} " +
                    $"archive={plan.StorageSlot} count={plan.StorageCount}");
            }
        }

        private static void Refuse(WorldClient client, string what, uint status)
        {
            Output.WriteDebugLog("[CENTRAL STORAGE] " + what + " refused: " + CentralStorage.NameOf(status));
            new ServerPackets().sendSystemChatMessage(client, "You can't do that: " + CentralStorage.Describe(status) + ".", "SYSTEM");
        }

        /// <summary>
        /// Client <c>5d &lt;slot u16&gt; 00 &lt;RecycleItem&gt;</c>: the window's Recycle button on a storage entry (0x621d4ae0 takes slots
        /// below 0x208 when PersonalInventoryItem is 0). Native type0x2e quote retains one item's price, answered as retail
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

            UInt32 quote=VendorPricing.GetRecyclePrice(entry.Goid);
            try
            {
                InventoryPayout result=Db.ExchangeInventoryForInfo(charId,slot,entry.Goid,entry.Count,quote,storage:true);
                if(result==null){ClientActionsHandler.Notice(client,"That storage item changed; try again.");return;}
                client.playerData.setInfo(result.Cash);
                ServerPackets pak=new ServerPackets();
                pak.SendInfoDelta(client,result.Credited,ServerPackets.InfoReasonRecycle);
                pak.SendInfoCurrent(client,result.Cash,ServerPackets.InfoReasonRecycle);
                client.messageQueue.addRpcMessage(CentralStorage.BuildRemove(slot));
                client.FlushQueue();
                Output.WriteDebugLog("[CENTRAL STORAGE] Recycled storage slot "+slot+" for "+result.Credited);
            }
            catch(Exception e)
            {
                Output.WriteDebugLog("[CENTRAL STORAGE] Recycle rolled back: "+e.Message);
                ClientActionsHandler.Notice(client,"The recycle could not be completed. Your item and information were kept.");
            }
        }
    }
}
