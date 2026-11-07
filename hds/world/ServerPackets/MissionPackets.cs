using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using hds.shared;
using System.Collections;
namespace hds
{
    public partial class ServerPackets 
    {

        public void sendMissionList(UInt16 contactId, uint orgID, List<Mission> missions, WorldClient client)
        {
            /*
             * ToDo: check if mission team was created or not
             */

            /* 
             * ToDo: Figure the "unknowns" out
             * Examples:
             * Neo: 18 80 95 00 00 00 00 06 00 07 00 00 01 d0 07 00 00 31 00 00 b4 c0 0c 00 28 
             * Luz: 18 80 95 00 00 00 00 05 00 01 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00
             * Raj: 18 80 95 00 00 00 00 00 00 01 00 01 00 22 00 00 00 f3 13 00 58 a5 11 00 0a (just one mission in list "The Hunter" - PB i think)
             * Raj: 18 80 95 00 00 00 00 00 00 01 00 01 00 34 00 00 00 15 14 00 58 a8 11 00 0a (just one mission in list "Pretty Retribution" - PB i think)
             */
            // contactId is the client's request number (retail copied it back), see MissionHandler.
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_MISSION_RESPONSE_LIST,0);
            pak.AddUint32(0, 0); // Unknown UInt32
            pak.AddUint16(contactId, 1);
            pak.AddUint16((ushort) missions.Count,1); // len of missions "categorys" to list
            pak.AddUShort(0);
            pak.AddUShort((ushort)orgID);
            pak.AddHexBytes("d0070000310000b4c00c0028"); // as in the retail (Neo) list header
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());

            
            // ArrayList possibleMissions = new ArrayList();
            // possibleMissions.Add("Change the Organisation");
            // possibleMissions.Add("Welcome to HDS Experimental!");
            // possibleMissions.Add("Party like '99");

            UInt16 i = 0;
            foreach (Mission mission in missions)
            {
                PacketContent missionListPak = new PacketContent();
                missionListPak.AddUint16((UInt16)RPCResponseHeaders.SERVER_MISSION_RESPONSE_NAME, 0);
                missionListPak.AddUint16(contactId, 1);
                missionListPak.AddUint16(i, 1);
                missionListPak.AddHexBytes("0f0001"); // curently unknown
                missionListPak.AddUint32(0x07d0,1); // every retail capture has d0 07 00 00 here
                missionListPak.AddUint16(0,1); // Unknown
                missionListPak.AddSizedTerminatedString(mission.title);
                client.messageQueue.addRpcMessage(missionListPak.ReturnFinalPacket());
                i++;
            }

            // And finally again a resposne
            PacketContent finalResponse = new PacketContent();
            finalResponse.AddUint16((UInt16)RPCResponseHeaders.SERVER_MISSION_LIST_END, 0);
            finalResponse.AddUint16(contactId, 1);
            client.messageQueue.addRpcMessage(finalResponse.ReturnFinalPacket());
            
        }

        public void sendMissionInfo(Mission mission, UInt16 txn, byte difficulty, WorldClient client)
        {
            // 8099 00000000 <txn> 2f 00 01 00 00 00 <difficulty echoed> ... (retail)
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_MISSION_INFO_RESPONSE, 0);
            pak.AddUint32(0, 1);
            pak.AddUint16(txn, 1);
            pak.AddHexBytes("2f0001000000");
            pak.AddByte(difficulty);

            // ToDo: calculate distance for the first and last location 
            float firstLocationDistance = 120 * 100;
            float maxLocationDistance = 230 * 100;
            
            pak.AddFloat(firstLocationDistance,1);
            pak.AddFloat(maxLocationDistance,1);
            pak.AddHexBytes("000000000000000000d0070000310000b4c00c0028");
            pak.AddHexBytes("42000001000002000D001300020002030200FFFFFDFF");
            pak.AddSizedTerminatedString(mission.description);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            client.FlushQueue();
        }

        public void sendMissionAccept(WorldClient client, UInt16 txn, UInt16 instance, Mission mission)
        {
            client.messageQueue.addObjectMessage(BuildMissionKeyUpdate(instance), false);
            client.messageQueue.addRpcMessage(BuildMissionSetName(txn, instance, mission.title));
            client.messageQueue.addRpcMessage(BuildMissionBriefing(mission.description));

            client.FlushQueue();
        }

        /// <summary>
        /// 809e: mission waypoint. Retail: 80 9e 00 &lt;x y z f64&gt; 01 00 00 00 22 01 80 07
        /// (27800, 8945, -4600 in the capture). The trailing id is kept as retail sent it.
        /// </summary>
        public static byte[] BuildMissionWaypoint(double x, double y, double z)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("809E00");
            pak.AddDouble(x, 1);
            pak.AddDouble(y, 1);
            pak.AddDouble(z, 1);
            pak.AddHexBytes("0100000022018007");
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 80a1: NPC dialog window. Retail (first-mission capture, "Armor"):
        /// 80 a1 1f 12 00 28 | 00 00 00 00 | 14 00 | 00 00 | 00 00 | &lt;text offset u16&gt; | 01 00 | sized speaker | sized text.
        /// Offsets count from the 80 a1 header; the speaker always starts at 0x14.
        /// </summary>
        public static byte[] BuildNpcDialog(string speaker, string text)
        {
            speaker = speaker ?? "";
            const int speakerOffset = 0x14;
            int textOffset = speakerOffset + 2 + speaker.Length + 1;

            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80A1");
            pak.AddHexBytes("1F120028" + "00000000");
            pak.AddUint16(speakerOffset, 1);
            pak.AddUint16(0, 1);
            pak.AddUint16(0, 1);
            pak.AddUint16((UInt16)textOffset, 1);
            pak.AddUint16(1, 1);
            pak.AddSizedTerminatedString(speaker);
            pak.AddSizedTerminatedString(text ?? "");
            return pak.ReturnFinalPacket();
        }

        public void SendNpcDialog(WorldClient client, string speaker, string text)
        {
            client.messageQueue.addRpcMessage(BuildNpcDialog(speaker, text));
        }

        /// <summary>
        /// Own-view MissionKey (attribute 13: group 1, bit 6) = 00 &lt;instance u16&gt; 02, as retail set it on accept
        /// (<c>02 00 02 80 40 00 2f 00 02</c>). This used to carry other instance bytes plus a stray 00 00.
        /// </summary>
        public static byte[] BuildMissionKeyUpdate(UInt16 instance)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("0200028040");
            pak.AddByte(0x00);
            pak.AddUint16(instance, 1);
            pak.AddByte(0x02);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 809c: the accepted mission's name. Layout from the retail Assassination capture, with the offsets
        /// recomputed for the title length: title at 0x2b, then an empty string, then a u32 list.
        /// </summary>
        public static byte[] BuildMissionSetName(UInt16 txn, UInt16 instance, string title)
        {
            const int titleOffset = 0x2b;
            int emptyOffset = titleOffset + 2 + title.Length + 1;
            int listOffset = emptyOffset + 3;

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_MISSION_SET_NAME, 0);
            pak.AddUint32(0, 1);
            pak.AddUint16(txn, 1);
            pak.AddUint16(titleOffset, 1);
            pak.AddHexBytes("0100");
            pak.AddUint16(instance, 1);
            pak.AddHexBytes("02D0070000310000B4C00C002814000026700A0028010000");
            pak.AddUint16((UInt16)listOffset, 1);
            pak.AddUint16((UInt16)emptyOffset, 1);
            pak.AddByte(0x00);
            pak.AddSizedTerminatedString(title);
            pak.AddSizedTerminatedString("");
            pak.AddHexBytes("0300AA0600009C070000DC050000");
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 80a3: the mission briefing. Header fields (text offset 0x30, two mission-area vectors) from the retail
        /// capture, then the text as a sized string. The old hex declared a 306-byte string but stopped after
        /// 206 bytes, so the client read past the message.
        /// </summary>
        public static byte[] BuildMissionBriefing(string text)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80A3");
            pak.AddHexBytes("300003007800" + "1A000000" + "03000000" + "3C00" +
                            "0010EF4600007FC30020E4C5" + "0088DB4600007FC300E0ABC5" + "000000000000");
            pak.AddSizedTerminatedString(text ?? "");
            return pak.ReturnFinalPacket();
        }

        public void SendMissionAbort(WorldClient client)
        {
            // Retail cleared the own-view MissionKey on abort (02 00 02 80 40 00 00 00 00).
            PacketContent clear = new PacketContent();
            clear.AddHexBytes("020002804000000000");
            client.messageQueue.addObjectMessage(clear.ReturnFinalPacket(), false);

            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80a20800000000b4658db5000000000000000000000000000000");
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            client.FlushQueue();
        }



        public void sendMissionDownloadMap()
        {

        }

        public void SendSetMissionObjective(ushort id, ushort state, string missionObjective, WorldClient client)
        {
            //format : rpcsize+uint16header+ uint8(0) + uint16(0600), + uint8 state + sizedString (uint16 size + string + 00?)
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_MISSION_SET_OBJECTIVE, 0);
            pak.AddUShort(id);
            pak.AddUint16(6, 1);
            pak.AddUShort(state);
            pak.AddSizedTerminatedString(missionObjective);
            
            // Now send the message to the player queue
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            
        }
    }
}