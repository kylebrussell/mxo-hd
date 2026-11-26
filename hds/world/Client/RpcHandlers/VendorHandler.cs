using System;
using System.Collections;
using System.Text;
using System.Linq;
using hds.shared;

namespace hds{

    class VendorHandler{
        private static bool HasSession(WorldClient client)
        {
            var data = client.playerData;
            if (data.activeVendor != null && data.activeVendor.metrId == data.getDistrictId() &&
                data.activeVendorGeneration == data.worldGeneration && data.getOnWorld() && !data.worldChangePending) return true;
            data.activeVendor = null;
            ClientActionsHandler.Notice(client, "Open a vendor before trading.");
            return false;
        }

        /// <summary>Native6210ab10 reads Appraise and Slot as separate bytes. Normal sales are00+slot.</summary>
        internal static UInt16 ParseSellSlot(byte[] packet) => packet[1];

        public void processBuyItem(ref byte[] packet)
        {
            lock(Store.currentClient.playerData.craftingSync) BuyItem(ref packet);
        }
        private void BuyItem(ref byte[] packet){
            if (!HasSession(Store.currentClient)) return;
            if (VendorBuyback.TryBuyback(packet))
            {
                VendorBuyback.Handle(Store.currentClient,true);return;
            }
            if (packet.Length != 5 || packet[4] != 0)
            {
                ClientActionsHandler.Notice(Store.currentClient,"This vendor purchase request is not supported.");return;
            }
            byte[] goByteID = {packet[0],packet[1],packet[2],packet[3]};
            UInt32 itemGoID = NumericalUtils.ByteArrayToUint32(goByteID,1);
            if (Array.IndexOf(Store.currentClient.playerData.activeVendor.items, itemGoID) < 0)
            {
                ClientActionsHandler.Notice(Store.currentClient, "This vendor does not sell that item.");
                return;
            }

            if(AbilityProgression.IsAbilityCode(itemGoID))
            {
                var publication=new AbilityAcquisition.Publication(Store.currentClient);
                AbilityAcquisition.Receipt receipt;
                try { receipt=Store.dbManager.WorldDbHandler.AcquireAbility(Store.currentClient.playerData.getCharID(),itemGoID,VendorPricing.GetBuyPrice(itemGoID)); }
                catch(Exception error)
                {
                    Output.WriteDebugLog("[ABILITY ACQUISITION] Purchase refused/rolled back: "+error.Message);
                    publication.Notice(Store.currentClient,error is AbilityAcquisition.Refusal?error.Message:"The purchase could not be completed. Your information was kept.");return;
                }
                byte[][] acquiredPackets={ServerPackets.BuildAbilityCodeAdd(receipt.BagSlot,itemGoID),
                    ServerPackets.BuildInfoDelta((long)receipt.Cash-receipt.BeforeCash,ServerPackets.InfoReasonVendorBuy),
                    ServerPackets.BuildInfoCurrent(receipt.Cash,ServerPackets.InfoReasonVendorBuy),
                    ServerPackets.BuildAbilityCodeBuyAck(itemGoID,receipt.Price)};
                publication.PublishPurchase(Store.currentClient,receipt.Cash,acquiredPackets);
                return;
            }
            var client = Store.currentClient;
            var stamp = new AbilityAcquisition.Publication(client);
            (ushort Slot, int Count, uint Cash, uint Price) receiptItem;
            try
            {
                receiptItem = Store.dbManager.WorldDbHandler.PurchaseVendorItem(stamp.Character, itemGoID);
            }
            catch (Exception error) when (error is AbilityAcquisition.Refusal || error is System.Data.Common.DbException || error is InvalidOperationException)
            {
                string text = error is AbilityAcquisition.Refusal ? error.Message : "The purchase could not be completed. Your information was kept.";
                Output.WriteDebugLog($"[VENDOR] Purchase refused char={stamp.Character} item={itemGoID}: {error.Message}");
                if (stamp.Publish(client, ServerPackets.BuildSystemChatMessage(text, "MODAL"), VendorBuyback.RefusalAck()))
                    client.FlushQueue();
                return;
            }
            var (slot, count, cash, price) = receiptItem;
            const uint reason = ServerPackets.InfoReasonVendorBuy;
            var added = count == 1 ? ServerPackets.BuildInventoryItemAdd(slot, itemGoID, 0, 0x10) : ItemStacks.BuildStackCount(slot, count);
            Output.WriteDebugLog($"[VENDOR] Bought char={stamp.Character} item={itemGoID} slot={slot} count={count} price={price} cash={cash}");
            if (stamp.PublishPurchase(client, cash, added, ServerPackets.BuildInfoDelta(-(long)price, reason),
                ServerPackets.BuildInfoCurrent(cash, reason), ServerPackets.BuildVendorBuyAck(itemGoID, ServerPackets.DefaultItemDescriptor, price)))
                client.FlushQueue();
        }

        public void processSellItem(ref byte[] packet)
        {
            WorldClient client=Store.currentClient;
            lock(client.playerData.craftingSync)
            {
                if(!HasSession(client))return;
                if(!VendorTrading.TrySellRequest(packet,out bool appraise,out byte slot))return;
                if(!appraise){VendorBuyback.Handle(client,false,slot);return;}
                var db=Store.dbManager.WorldDbHandler;
                Crafting.InventoryItem item=db.GetInventoryItems(client.playerData.getCharID()).FirstOrDefault(i=>i.Slot==slot);
                if(item.Goid==0){ClientActionsHandler.Notice(client,"There is nothing to sell in that slot.");return;}
                uint quote=VendorPricing.GetSellPrice(item.Goid);
                if(quote==0){ClientActionsHandler.Notice(client,"This item cannot be sold.");return;}
                client.messageQueue.addRpcMessage(ServerPackets.BuildVendorItemSellAck(item.Goid,item.Count,quote,true));
            }
        }
    }
}
