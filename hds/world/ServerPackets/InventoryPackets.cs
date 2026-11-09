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
        /// A mission item: the 4-byte descriptor is the mission key of the NPC it's for, with 0x80 on the last byte. Retail's
        /// statue for Gagarin (key 01 9a 02 02): 5e 07 00 f2 04 00 00 01 9a 02 82 01. The client offers Give only with this.
        /// </summary>
        internal static byte[] BuildMissionItemAdd(UInt16 slot, UInt32 itemId, byte element, UInt16 instance, byte keyByte = 0x02)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5e);
            pak.AddUint16(slot, 1);
            pak.AddUint32(itemId, 1);
            pak.AddByte(element);
            pak.AddUint16(instance, 1);
            pak.AddByte((byte)(keyByte | 0x80));
            pak.AddUShort(1);
            return pak.ReturnFinalPacket();
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