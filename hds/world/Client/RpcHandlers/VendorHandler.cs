using System;
using System.Collections;
using System.Text;
using hds.shared;

namespace hds{

    class VendorHandler{

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
            packets.sendSystemChatMessage(Store.currentClient,
                "Vendor selling is not available until the sell packet format is decoded.", "MODAL");
        }
    }
}
