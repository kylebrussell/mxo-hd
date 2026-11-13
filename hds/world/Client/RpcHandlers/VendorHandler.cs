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
            if (packet.Length < 4)
            {
                Output.WriteDebugLog("[VENDOR] Buy packet too short.");
                return;
            }

            byte[] goByteID = {packet[0],packet[1],packet[2],packet[3]};
            UInt32 itemGoID = NumericalUtils.ByteArrayToUint32(goByteID,1);
            if (Array.IndexOf(Store.currentClient.playerData.activeVendor.items, itemGoID) < 0)
            {
                ClientActionsHandler.Notice(Store.currentClient, "This vendor does not sell that item.");
                return;
            }

            // Native BuyBackLastItemSold is a separate operation; no normal-purchase fallback.
            if(packet.Length>4 && packet[4]!=0)
            { ClientActionsHandler.Notice(Store.currentClient,"Buying back the last sold item is not supported.");return; }
            if(AbilityProgression.IsAbilityCode(itemGoID))
            {
                if(packet.Length!=5){ClientActionsHandler.Notice(Store.currentClient,"Invalid ability purchase request.");return;}
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
            long currentCash = Store.currentClient.playerData.getInfo();
            ServerPackets packets = new ServerPackets();
            if (!VendorPricing.TryApplyBuy(currentCash, itemGoID, out UInt32 newCash, out UInt32 price))
            {
                packets.sendSystemChatMessage(Store.currentClient,
                    "You do not have enough information to buy this item.", "MODAL");
                return;
            }

            // Retail order: 5e (add item, sent by processItemAdd) -> 80 e7 (-price) -> 80 e4 (new cash)
            // -> 81 0f (buy complete), all with reason 8.
            InventoryHandler inventory = new InventoryHandler();
            inventory.processItemAdd(itemGoID,0x10);

            Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient, newCash);
            Store.currentClient.playerData.setInfo(newCash);
            packets.SendInfoDelta(Store.currentClient, (long)newCash - currentCash, ServerPackets.InfoReasonVendorBuy);
            packets.SendInfoCurrent(Store.currentClient, newCash, ServerPackets.InfoReasonVendorBuy);
            packets.SendVendorBuyAck(Store.currentClient, itemGoID, price);
        }

        public void processSellItem(ref byte[] packet)
        {
            WorldClient client=Store.currentClient;
            lock(client.playerData.craftingSync)
            {
                if(!HasSession(client))return;
                if(!VendorTrading.TrySellRequest(packet,out bool appraise,out byte slot))return;
                var db=Store.dbManager.WorldDbHandler;
                Crafting.InventoryItem item=db.GetInventoryItems(client.playerData.getCharID()).FirstOrDefault(i=>i.Slot==slot);
                if(item.Goid==0){ClientActionsHandler.Notice(client,"There is nothing to sell in that slot.");return;}
                uint quote=VendorPricing.GetSellPrice(item.Goid);
                if(quote==0){ClientActionsHandler.Notice(client,"This item cannot be sold.");return;}
                if(appraise)
                {
                    client.messageQueue.addRpcMessage(ServerPackets.BuildVendorItemSellAck(item.Goid,item.Count,quote,true));
                    return;
                }
                try
                {
                    InventoryPayout result=db.ExchangeInventoryForInfo(client.playerData.getCharID(),slot,item.Goid,item.Count,quote);
                    if(result==null){ClientActionsHandler.Notice(client,"That inventory item changed; try again.");return;}
                    client.playerData.setInfo(result.Cash);
                    var packets=new ServerPackets();
                    packets.SendInfoDelta(client,result.Credited,ServerPackets.InfoReasonVendorSell);
                    packets.SendInfoCurrent(client,result.Cash,ServerPackets.InfoReasonVendorSell);
                    packets.sendInventoryItemDelete(slot,client);
                    client.messageQueue.addRpcMessage(ServerPackets.BuildVendorItemSellAck(item.Goid,item.Count,result.Credited,false));
                }
                catch(Exception e)
                {
                    Output.WriteDebugLog("[VENDOR] Sale rolled back: "+e.Message);
                    ClientActionsHandler.Notice(client,"The sale could not be completed. Your item and information were kept.");
                }
            }
        }
    }
}
