using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using hds.shared;
namespace hds
{
    public partial class ServerPackets 
    {

        public void SendVendorWindow(WorldClient client, Vendor vendor)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_VENDOR_OPEN,0);
            pak.AddByteArray(new byte[]{0x7c, 0xad, 0xd9, 0x43});
            // Decode findings (docs/PACKET-RESEARCH.md): the 81 0d SERVER_VENDOR_OPEN payload
            // begins `7c ad d9 43` (decode-confirmed, emitted above) and observed payload lengths
            // are 60/72/100/200/208/480/760 bytes. Whether the position block here should carry the
            // vendor's position or the player's stayed UNRESOLVED during decoding; we keep emitting
            // the player position (current behavior) pending a definitive capture.
            pak.AddByteArray(Store.currentClient.playerInstance.Position.getValue());
            pak.AddByteArray(new byte[]{0x20,0x00}); // Always 20 00 (i think its an offset)

            pak.AddUint16((UInt16)vendor.items.Length,1);
            foreach (UInt32 item in vendor.items)
            {
                pak.AddUint32(item,1);
            }
            //pak.addHexBytes("7cadd94300000000a0b7f6c000000000003c9440000000000051c8c020000a0000140080002000800018008000040080002c008000f41180009806800090078000c4068000640680");

            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        /// <summary>
        /// Item descriptor we put in 81 12 for items we added ourselves. Retail used "00 00 00 XX" for
        /// normal items, with XX matching the trailing type byte of the 5e item-add message (0x14,
        /// 0x18, 0x28, 0x34, 0x38, 0x44 seen). Our 5e add sends type 0x10. The meaning of XX isn't
        /// fully decoded.
        /// </summary>
        public static readonly byte[] DefaultSellItemDescriptor = {0x00, 0x00, 0x00, 0x10};

        // Retail answers every CLIENT_VENDOR_SELL (81 11) with one RPC list, in this order
        // (11 sells in research/packet-dumps, see docs/TODO.md):
        //   80 e7 (+price, reason 7) -> 80 e4 (new cash, reason 7) -> 5f (remove slot) -> 81 12.
        public void SendVendorSellAck(WorldClient client, UInt32 goId, UInt32 price)
        {
            client.messageQueue.addRpcMessage(BuildVendorSellAck(goId, DefaultSellItemDescriptor, price));
        }

        /// <summary>
        /// 81 12 "sell complete": status 00, item GoID (u32 LE), 4-byte item descriptor, sell price
        /// (u32 LE). Retail FM-13SK sell (GoID 43060, 5000):
        /// "81 12 00 34 a8 00 00 00 00 00 34 88 13 00 00".
        /// </summary>
        internal static byte[] BuildVendorSellAck(UInt32 goId, byte[] descriptor, UInt32 price)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x8112, 0);
            pak.AddByte(0x00);
            pak.AddUint32(goId, 1);
            pak.AddByteArray(descriptor);
            pak.AddUint32(price, 1);
            return pak.ReturnFinalPacket();
        }

    }
}