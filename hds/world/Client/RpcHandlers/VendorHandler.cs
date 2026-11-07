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
            if (!VendorPricing.TryApplyBuy(currentCash, itemGoID, out UInt32 newCash, out UInt32 _))
            {
                packets.sendSystemChatMessage(Store.currentClient,
                    "You do not have enough information to buy this item.", "MODAL");
                return;
            }

            InventoryHandler inventory = new InventoryHandler();
            inventory.processItemAdd(itemGoID,0x10);

            Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient, newCash);
            Store.currentClient.playerData.setInfo(newCash);
            packets.SendInfoCurrent(Store.currentClient, newCash);

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
            if (!VendorPricing.TryApplySell(currentCash, itemGoID, out UInt32 newCash, out UInt32 _))
            {
                packets.sendSystemChatMessage(Store.currentClient,
                    "This item cannot be sold.", "MODAL");
                return;
            }

            // Remove the sold item from the inventory (DB row delete + client delete packet).
            InventoryHandler inventory = new InventoryHandler();
            inventory.removeItemAtSlot(slot);

            // Credit the player (mirrors processBuyItem's cash flow).
            Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient, newCash);
            Store.currentClient.playerData.setInfo(newCash);
            packets.SendInfoCurrent(Store.currentClient, newCash);

            // Best-effort sell acknowledgement (lead: 5f ack already sent via the item delete,
            // 81 12 sell complete here). See SendVendorSellAck for the capture-pending caveat.
            packets.SendVendorSellAck(Store.currentClient);
        }
    }
}
