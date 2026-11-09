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
                missionListPak.AddByte(0x00); // chained
                missionListPak.AddByte((byte)(mission.plot ? 1 : 0)); // retail: 1 on story missions, 0 on the generic types
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

            // Retail: how far the player is from the mission's first and last locations.
            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            float firstLocationDistance = MissionDistance(mission.FirstWaypoint, px, pz);
            float maxLocationDistance = MissionDistance(mission.LastWaypoint, px, pz);

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
            client.messageQueue.addRpcMessage(BuildMissionSetName(txn, instance, mission.title, mission.id));
            client.FlushQueue();
        }

        private static float MissionDistance(MissionWaypoint waypoint, double x, double z)
        {
            if (waypoint == null)
            {
                return 0;
            }

            double dx = waypoint.x - x, dz = waypoint.z - z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// 809e: mission waypoint. Retail: 80 9e 00 &lt;x y z f64&gt; 01 00 00 00 22 01 80 07
        /// (27800, 8945, -4600 in the capture). The trailing id is kept as retail sent it.
        /// </summary>
        public static byte[] BuildMissionWaypoint(double x, double y, double z, byte index = 0, string doorHex = null)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("809E");
            pak.AddByte(index);
            pak.AddDouble(x, 1);
            pak.AddDouble(y, 1);
            pak.AddDouble(z, 1);
            pak.AddHexBytes("01000000" + (string.IsNullOrEmpty(doorHex) ? "22018007" : doorHex));
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 80a1: NPC dialog window. Retail (first-mission capture, "Armor"):
        /// 80 a1 1f 12 00 28 | 00 00 00 00 | 14 00 | 00 00 | 00 00 | &lt;text offset u16&gt; | 01 00 | sized speaker | sized text.
        /// Offsets count from the 80 a1 header; the speaker always starts at 0x14.
        /// </summary>
        public static byte[] BuildNpcDialog(string speaker, string text)
        {
            return BuildDialog(0x2800121f, 0, 0, 1, 0, speaker, text);
        }

        /// <summary>
        /// The same message for any speaker: image u32, name string id u32, name offset u16, text string id u32, text offset
        /// u16, display u8, client flag u8, sized name, sized text. Retail's operator: image 0x28000a70, name 0x26000014, no
        /// name text. A text string id (with the client flag set) has the client show its own string table's line.
        /// </summary>
        public static byte[] BuildDialog(UInt32 image, UInt32 nameId, UInt32 stringId, byte display, byte clientFlag, string speaker, string text)
        {
            speaker = speaker ?? "";
            const int speakerOffset = 0x14;
            int textOffset = speakerOffset + 2 + speaker.Length + 1;

            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80A1");
            pak.AddUint32(image, 1);
            pak.AddUint32(nameId, 1);
            pak.AddUint16(speakerOffset, 1);
            pak.AddUint32(stringId, 1);
            pak.AddUint16((UInt16)textOffset, 1);
            pak.AddByte(display);
            pak.AddByte(clientFlag);
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
        public static byte[] BuildMissionSetName(UInt16 txn, UInt16 instance, string title, int templateId)
        {
            return BuildMissionSetName(txn, instance, title, 1, "02D0070000310000B4C00C0028", "010000", new[] { templateId });
        }

        /// <summary>
        /// The tutorial: no contact, and template 986, which is how the client finds its Mission986 script. Retail:
        /// 80 9c 00000000 0000 2b00 0000 d701 02 00000000 00000000 00000000 14000026 700a0028 0000ff 3c00 3900 00
        /// 0c00 "Tutorial v2" 010000 0100 da030000.
        /// </summary>
        public static byte[] BuildTutorialSetName(UInt16 instance)
        {
            return BuildMissionSetName(0, instance, "Tutorial v2", 0, "02000000000000000000000000", "0000FF", new[] { TutorialMission.MissionId });
        }

        /// <summary>Org u16 (1 Zion, 0 none); then the key byte and contact ids, and three flag bytes, as retail sent them.</summary>
        private static byte[] BuildMissionSetName(UInt16 txn, UInt16 instance, string title, UInt16 org, string keyAndContactHex,
            string flagsHex, int[] templates)
        {
            const int titleOffset = 0x2b;
            int emptyOffset = titleOffset + 2 + title.Length + 1;
            int listOffset = emptyOffset + 3;

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_MISSION_SET_NAME, 0);
            pak.AddUint32(0, 1);
            pak.AddUint16(txn, 1);
            pak.AddUint16(titleOffset, 1);
            pak.AddUint16(org, 1);
            pak.AddUint16(instance, 1);
            pak.AddHexBytes(keyAndContactHex + "14000026700A0028" + flagsHex);
            pak.AddUint16((UInt16)listOffset, 1);
            pak.AddUint16((UInt16)emptyOffset, 1);
            pak.AddByte(0x00);
            pak.AddSizedTerminatedString(title);
            pak.AddSizedTerminatedString("");
            pak.AddUint16((UInt16)templates.Length, 1);
            foreach (int template in templates)
            {
                pak.AddUint32((UInt32)template, 1);
            }

            return pak.ReturnFinalPacket();
        }

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
            return BuildMissionBriefing(text, null, new[] { 30600f, -255f, -7300f }, new[] { 28100f, -255f, -5500f });
        }

        public static byte[] BuildMissionBriefing(string text, string headerHex, float[] areaA, float[] areaB)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80A3");
            pak.AddHexBytes(string.IsNullOrEmpty(headerHex) ? "300003007800" + "1A000000" + "03000000" + "3C00" : headerHex);
            foreach (float value in areaA)
            {
                pak.AddFloat(value, 1);
            }

            foreach (float value in areaB)
            {
                pak.AddFloat(value, 1);
            }

            pak.AddHexBytes("000000000000");
            pak.AddSizedTerminatedString(text ?? "");
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 80 a2: mission end, 24 bytes after the header. Retail's completed first mission (crashover2k
        /// create_char_devoted, 06:10:51): 80 a2 00 | 01000000 | 2d020000 | 08520000 | b80b0000 | 00x7 =
        /// status 00 (complete), enemy kills u16, team killed u16, seconds 557, info 21,000, EXP 3,000; then reputation i16,
        /// two empty arrays and the intro state (01 on Zion's early missions). The client's Mission summary shows
        /// "Payment: $i info, +EXP XP" from those. Status 08 is the abort.
        /// </summary>
        public static byte[] BuildMissionComplete(UInt32 exp, UInt32 info, UInt16 kills = 1, UInt32 seconds = 0)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80A2");
            pak.AddByte(0x00);
            pak.AddUint16(kills, 1);
            pak.AddUint16(0, 1);
            pak.AddUint32(seconds, 1);
            pak.AddUint32(info, 1);
            pak.AddUint32(exp, 1);
            pak.AddHexBytes("00000000000001");
            return pak.ReturnFinalPacket();
        }

        public void SendMissionComplete(WorldClient client, UInt32 exp, UInt32 info, UInt16 kills = 1, UInt32 seconds = 0)
        {
            // Retail cleared the own-view MissionKey when the mission ended (02 00 02 80 40 00 00 00 00).
            PacketContent clear = new PacketContent();
            clear.AddHexBytes("020002804000000000");
            client.messageQueue.addObjectMessage(clear.ReturnFinalPacket(), false);
            client.messageQueue.addRpcMessage(BuildMissionComplete(exp, info, kills, seconds));
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



        /// <summary>81 45: the client plays a voiced line from its own audio. Id u32 (0x86xxxxxx).</summary>
        public static byte[] BuildNarration(UInt32 id)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8145");
            pak.AddUint32(id, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 02: shows or hides a panel of the client's interface. Retail: 81 02 0e 00 01 turned the target panel on.</summary>
        public static byte[] BuildToggleView(UInt16 view, byte show)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8102");
            pak.AddUint16(view, 1);
            pak.AddByte(show);
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 91 server to client: a scripted mission event. Retail: 81 91 01 00 00 00.</summary>
        public static byte[] BuildMissionEvent(UInt32 eventId)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8191");
            pak.AddUint32(eventId, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 42: FX id u32, token u32, user data u32. With a token, the client answers 81 91 &lt;token&gt; when the FX ends.</summary>
        public static byte[] BuildLocalFx(UInt32 fx, UInt32 token)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8142");
            pak.AddUint32(fx, 1);
            pak.AddUint32(token, 1);
            pak.AddUint32(0, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 8f: a two-button prompt, answered with 81 90 &lt;message&gt; &lt;left chosen&gt;. Retail's pill choice:
        /// 81 8f 00000000 15000088 1c00 03000000 050000c2 010000c2 00000000 010000.</summary>
        public static byte[] BuildModalMessage(UInt32 image, UInt32 stringId, UInt32 message, UInt32 left, UInt32 right)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("818F");
            pak.AddUint32(image, 1);
            pak.AddUint32(stringId, 1);
            pak.AddUint16(0x1c, 1);
            pak.AddUint32(message, 1);
            pak.AddUint32(left, 1);
            pak.AddUint32(right, 1);
            pak.AddUint32(0, 1);
            pak.AddSizedTerminatedString("");
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 99: teleports to a mission element. x, y, z f64, then the quaternion's x, y, z, w f32.</summary>
        public static byte[] BuildMissionTeleport(double[] element)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8199");
            pak.AddDouble(element[0], 1);
            pak.AddDouble(element[1], 1);
            pak.AddDouble(element[2], 1);
            pak.AddFloat((float)element[3], 1);
            pak.AddFloat((float)element[4], 1);
            pak.AddFloat((float)element[5], 1);
            pak.AddFloat((float)element[6], 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 19 for a container: StringID u32 (its title), array offset 0x10, info 0, its MissionKey, count u16, then
        /// (instance data, item) pairs. Retail's wardrobe: 81 19 87 02 00 76 10 00 00 00 00 00 05 d7 01 02 03 00 ...</summary>
        public static byte[] BuildContainerLoot(UInt32 titleString, byte element, UInt16 instance, byte keyByte, (byte[] Data, UInt32 Item)[] items)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8119");
            pak.AddUint32(titleString, 1);
            pak.AddUint16(0x10, 1);
            pak.AddUint32(0, 1);
            pak.AddByte(element);
            pak.AddUint16(instance, 1);
            pak.AddByte(keyByte);
            pak.AddUint16((UInt16)items.Length, 1);
            foreach ((byte[] data, UInt32 item) in items)
            {
                pak.AddByteArray(data);
                pak.AddUint32(item, 1);
            }

            return pak.ReturnFinalPacket();
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