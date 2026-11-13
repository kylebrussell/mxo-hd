using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hds.shared;

namespace hds
{
    public partial class ServerPackets
    {
        public void SendChatMessage(WorldClient fromClient, string message, UInt32 charId, string handle, UInt16 scopeId)
        {
            
            UInt32 offsetMessage = (uint) handle.Length + 36 + 3;
            PacketContent pak = new PacketContent();
            pak.AddByte((byte) RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE);
            pak.AddUint16(scopeId,1);
            pak.AddUint32(charId, 1);
            pak.AddUint32(36, 1);
            pak.AddUint32(offsetMessage, 1);
            pak.AddHexBytes("000000000000000000000000000000000000000000"); // Unknown Zeros currently
            pak.AddSizedTerminatedString(handle);
            pak.AddSizedTerminatedString(message);

            switch (scopeId)
            {
                // 20 = Faction_Broadcast - never used ?
                case 18:
                    UInt32 crewId =
                        NumericalUtils.ByteArrayToUint32(Store.currentClient.playerInstance.CrewID.getValue(),
                            1);
                    Store.world.SendRPCToCrewMembers(crewId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    break;
                case 19:
                case 20:
                    UInt32 factionId =
                        NumericalUtils.ByteArrayToUint32(Store.currentClient.playerInstance.FactionID.getValue(),
                            1);
                    Store.world.SendRPCToFactionMembers(factionId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    break;

                case 21:
                    UInt32 missionTeamId =
                        NumericalUtils.ByteArrayToUint32(Store.currentClient.playerInstance.MissionTeamID.getValue(),
                            1);
                    Store.world.SendRPCToMissionTeamMembers(missionTeamId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    break;

                default:
                    Store.world.SendRPCToAllOtherPlayers(Store.currentClient.playerData, pak.ReturnFinalPacket());
                    break;
            }
        }

        public void sendSystemChatMessage(WorldClient client, string message, string type)
        {
            client.messageQueue.addRpcMessage(BuildSystemChatMessage(message,type));
            client.FlushQueue();
        }
        internal static byte[] BuildSystemChatMessage(string message,string type)
        {
            byte typeByte;
            switch (type)
            {
                case "SYSTEM":
                    typeByte = 0x07;
                    break;
                case "MODAL":
                    typeByte = 0x17;
                    break;
                case "FRAMEMODAL":
                    typeByte = 0xd7;
                    break;
                case "BROADCAST":
                    typeByte = 0xc7;
                    break;

                default:
                    typeByte = 0x07;
                    break;
            }


            UInt16 messageSize = (UInt16) (message.Length + 1);

            byte[] hexContents =
                StringUtils.hexStringToBytes("00000000000000000024000000000000000000000000000000000000000000000000");

            PacketContent pak = new PacketContent();
            pak.AddByte((byte) RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE);
            pak.AddByte(typeByte);
            pak.AddByteArray(hexContents);
            pak.AddSizedTerminatedString(message);

            return pak.ReturnFinalPacket();
        }
    }
}