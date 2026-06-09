using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using hds.shared;
namespace hds
{
    public partial class ServerPackets 
    {
        // Place Methods here for Inventory

        public void sendInventoryItemMove(UInt16 sourceSlot, UInt16 destSlot, WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x65);
            pak.AddUint16(sourceSlot,1);
            pak.AddUint16(destSlot,1);
            pak.AddByte(0x00);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        public void sendInventoryItemAdd(UInt16 freeSlot, UInt32 itemId, ushort amount, UInt16 type, WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5e);
            pak.AddUint16(freeSlot, 1);
            pak.AddUint32(itemId, 1);
            pak.AddUint16(0,1);
            pak.AddUShort(amount);
            pak.AddUShort(type);
            pak.AddUShort(1);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            
        }

        public void sendInventoryItemDelete(UInt16 slot, WorldClient client)
        {
            // LEAD (capture-pending): the exact server->client "remove item from slot" opcode is
            // not proven. Decode notes place a `5f` reply in the vendor sell sequence
            // (80 e7 -> 80 e4 cash -> 5f sell ack -> 81 12 sell complete), so we use 0x5f here.
            // Shape is modelled on sendInventoryItemMove (single-byte opcode + slot uint16 +
            // trailing 0x00) since the move packet is the closest proven inventory mutation.
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5f);
            pak.AddUint16(slot, 1);
            pak.AddByte(0x00);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }
        
    }
}