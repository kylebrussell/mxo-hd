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

            if (packet.Length < 2)
            {
                Output.WriteDebugLog("[VENDOR] Sell packet too short.");
                return;
            }

            // The CLIENT_VENDOR_SELL (81 11) request field is SESSION-RELATIVE, not a global item code.
            // Evidence: the decoder's "Vendor Sell Request Addressing" pass (docs/generated/
            // packet-research-summary.md) shows distinct request-field values collapsing onto the SAME
            // item (00 51 and 00 55 both -> Ragaini Combat Shotgun; 00 52 and 00 53 both -> Dada shoes).
            // A global item code could not behave that way, so the "short item/ability code" reading is
            // ruled out. The field is consistent with session-relative addressing -- either a DB
            // inventory slot or a vendor-window list index -- which matches every other proven inventory
            // mutation (processItemMove/processMountItem/processUnmountItem reads raw slot bytes from
            // packet[0..]). We treat packet[0..1] as the inventory slot. Remaining capture-pending
            // ambiguity: DB inventory slot vs vendor-list index (both session-relative).
            byte[] slotBytes = {packet[0], packet[1]};
            UInt16 slot = NumericalUtils.ByteArrayToUint16(slotBytes, 1);

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
