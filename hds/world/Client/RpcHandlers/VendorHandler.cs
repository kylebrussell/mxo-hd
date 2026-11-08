using System;
using System.Collections;
using System.Text;
using hds.shared;

namespace hds{

    class VendorHandler{

        /// <summary>
        /// Inventory slot addressed by a CLIENT_VENDOR_SELL (81 11) request: the first two bytes,
        /// big-endian. Confirmed with the real 7.6005 client on 2026-11-06: selling the item in DB
        /// slot 0 sent "00 00" and the item in DB slot 11 sent "00 0b". The retail captures'
        /// "00 45".."00 58" are slots 69..88, i.e. items on later inventory pages. Reading these bytes
        /// little-endian (the old parse) turned slot 11 into 2816, so only slot 0 could be sold.
        /// </summary>
        internal static UInt16 ParseSellSlot(byte[] packet)
        {
            return NumericalUtils.ByteArrayToUint16(new[] {packet[0], packet[1]}, 0);
        }

        public void processBuyItem(ref byte[] packet){
            if (packet.Length < 4)
            {
                Output.WriteDebugLog("[VENDOR] Buy packet too short.");
                return;
            }

            byte[] goByteID = {packet[0],packet[1],packet[2],packet[3]};
            UInt32 itemGoID = NumericalUtils.ByteArrayToUint32(goByteID,1);

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
            bool abilityCode = AbilityProgression.IsAbilityCode(itemGoID);
            if (abilityCode)
            {
                UInt16 freeSlot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
                if (freeSlot == 0)
                {
                    packets.sendSystemChatMessage(Store.currentClient, "Your inventory is full.", "BROADCAST");
                    return;
                }
                Store.dbManager.WorldDbHandler.AddItemToInventory(freeSlot, itemGoID);
                Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityCodeAdd(freeSlot, itemGoID));
            }
            else
            {
                InventoryHandler inventory = new InventoryHandler();
                inventory.processItemAdd(itemGoID,0x10);
            }

            Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient, newCash);
            Store.currentClient.playerData.setInfo(newCash);
            packets.SendInfoDelta(Store.currentClient, (long)newCash - currentCash, ServerPackets.InfoReasonVendorBuy);
            packets.SendInfoCurrent(Store.currentClient, newCash, ServerPackets.InfoReasonVendorBuy);
            if (abilityCode)
            {
                Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityCodeBuyAck(itemGoID, price));
            }
            else
            {
                packets.SendVendorBuyAck(Store.currentClient, itemGoID, price);
            }
        }

        public void processSellItem(ref byte[] packet){
            ServerPackets packets = new ServerPackets();

            if (packet.Length < 2)
            {
                Output.WriteDebugLog("[VENDOR] Sell packet too short.");
                return;
            }

            UInt16 slot = ParseSellSlot(packet);

            UInt32 itemGoID = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot);
            if (itemGoID == 0)
            {
                packets.sendSystemChatMessage(Store.currentClient,
                    "There is nothing to sell in that slot.", "MODAL");
                return;
            }

            long currentCash = Store.currentClient.playerData.getInfo();
            if (!VendorPricing.TryApplySell(currentCash, itemGoID, out UInt32 newCash, out UInt32 price))
            {
                packets.sendSystemChatMessage(Store.currentClient,
                    "This item cannot be sold.", "MODAL");
                return;
            }

            // Update the DB first, then answer in the retail order:
            // 80 e7 (+price) -> 80 e4 (new cash) -> 5f (remove slot) -> 81 12 (sell complete).
            UInt32 charId = Store.currentClient.playerData.getCharID();
            Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, charId);
            Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient, newCash);
            Store.currentClient.playerData.setInfo(newCash);

            packets.SendInfoDelta(Store.currentClient, (long)newCash - currentCash, ServerPackets.InfoReasonVendorSell);
            packets.SendInfoCurrent(Store.currentClient, newCash, ServerPackets.InfoReasonVendorSell);
            packets.sendInventoryItemDelete(slot, Store.currentClient);
            packets.SendVendorSellAck(Store.currentClient, itemGoID, price);
        }
    }
}
