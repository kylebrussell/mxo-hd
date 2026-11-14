using System;
using hds.shared;
namespace hds
{
    public partial class ServerPackets
    {
        public void SendLootAccepted(WorldClient client, (byte[] Data, UInt32 Item)[] claimed = null)
        {
            client.messageQueue.addRpcMessage(BuildLootAccepted(claimed ?? Array.Empty<(byte[], UInt32)>()));
            client.FlushQueue();
        }

        /// <summary>Server 81 1a closes the client's container window (client dispatcher 62179a05).</summary>
        internal static byte[] BuildLootClosed() => new byte[] { 0x81, 0x1a };

        /// <summary>81 20 removes each listed claimed identity; unclaimed items must not be listed.</summary>
        internal static byte[] BuildLootAccepted((byte[] Data, UInt32 Item)[] claimed)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOOT_ACCEPTED_RESPONSE,0);
            pak.AddUint16(12, 1); // ItemArray offset
            pak.AddUint32(0, 1); // Status
            pak.AddUint32(0, 1); // InfoAmount
            pak.AddUint16((UInt16)claimed.Length, 1);
            foreach ((byte[] data, UInt32 item) in claimed)
            {
                pak.AddByteArray(data);
                pak.AddUint32(item, 1);
            }
            return pak.ReturnFinalPacket();
        }
    }
}
