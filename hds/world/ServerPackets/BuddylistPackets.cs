using System;
using System.Collections;
using hds.shared;

namespace hds
{
    public partial class ServerPackets
    {

        public void SendRemoveFriend(WorldClient client, string handleWithPrefix)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_FRIENDLIST_STATUS_DELETE,0);
            pak.AddUint16(8,1); // Unknonw 08 00 (but should be just reponse)
            pak.AddByteArray(new byte[]{ 0x84, 0x00, 0x00, 0x8e});
            pak.AddSizedTerminatedString(handleWithPrefix);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

    }
}