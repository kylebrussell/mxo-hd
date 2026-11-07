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
            client.messageQueue.addRpcMessage(BuildInventoryItemAdd(freeSlot, itemId, amount, type));
        }

        /// <summary>
        /// 5e "add item to slot": slot (u16 LE), item id (u32 LE), 4-byte descriptor "00 00 amount type",
        /// then 01. Matches retail exactly; a 30,000 weapon bought into slot 0 was
        /// "5e 00 00 c1 09 00 00 00 00 00 10 01".
        /// </summary>
        internal static byte[] BuildInventoryItemAdd(UInt16 slot, UInt32 itemId, ushort amount, UInt16 type)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5e);
            pak.AddUint16(slot, 1);
            pak.AddUint32(itemId, 1);
            pak.AddUint16(0,1);
            pak.AddUShort(amount);
            pak.AddUShort(type);
            pak.AddUShort(1);
            return pak.ReturnFinalPacket();
        }

        public void sendInventoryItemDelete(UInt16 slot, WorldClient client)
        {
            client.messageQueue.addRpcMessage(BuildInventoryItemDelete(slot));
        }

        /// <summary>
        /// 5f "remove item from slot": slot as u16 little-endian, then 01. All 70 retail 5f messages
        /// in the captures (after sells, recycles and marketplace listings) end in 01; we used to
        /// send 00, and the client left the item in the grid. Retail FM-13SK sell, slot 88:
        /// "5f 58 00 01".
        /// </summary>
        internal static byte[] BuildInventoryItemDelete(UInt16 slot)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5f);
            pak.AddUint16(slot, 1);
            pak.AddByte(0x01);
            return pak.ReturnFinalPacket();
        }
        
    }
}