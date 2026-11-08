using System;
using System.Collections.Generic;
using System.Text;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Byte layouts of the mission team / crew / faction / friend list / group chat RPCs, built from the retail
    /// captures. Every builder is pure so the layouts are checked against retail bytes in SocialPacketTests.
    ///
    /// Main source: research/packet-dumps/community-intake/20260607T022135Z-hdneo-public-two-person-actions-*
    /// (both sides of AfterWhoruNeo inviting mxoemu3 to his mission team and to the crew "Reservoir Dogs",
    /// a crew deposit, leaving the crew, changing loot mode and disbanding the team), plus
    /// mxopackets1_entilsar_enti_lastday2 (faction chat, friends online/offline), the Rajko captures (team chat,
    /// team leave, /invite of a bad handle), ProxyM2_m2_packets (faction invite) and
    /// mxopackets1_crashover2k_009_session_complete (friend list adds).
    /// </summary>
    internal static class SocialPackets
    {
        // Group types, the first byte of 6e/6f/70/71/72/77 and of the money RPCs.
        public const byte GroupFaction = 1;
        public const byte GroupCrew = 2;
        public const byte GroupTeam = 3;

        // Chat channels (first byte of client 28 / server 2e).
        public const byte ChatArea = 0x10;
        public const byte ChatTell = 0x11;
        public const byte ChatCrew = 0x12;
        public const byte ChatFaction = 0x13;
        public const byte ChatTeam = 0x15;

        // Result codes. The client resolves 0x0d0000NN to its LTTRGS_ string table entry NN-1 (in client.dll order):
        // retail sent 0x0d00002e when an invite to a mission team was declined (entry 0x2d MTINVITEDECLINED) and
        // 0x0d000031 for "/invite Warboty", a handle that did not exist (entry 0x30 MTINVALIDCHARACTERNAME).
        public const uint ResultOk = 0;
        public const uint FactionInviteDeclined = 0x0d00000a;
        public const uint FactionInviteeNotFound = 0x0d00000b;
        public const uint FactionCurrentMember = 0x0d00000d;
        public const uint FactionInvalidCharacterName = 0x0d00000e;
        public const uint FactionInvalidFounder = 0x0d00000f;
        public const uint FactionInvalidMemberCode = 0x0d000010;
        public const uint FactionInvalidTeamName = 0x0d000011;
        public const uint FactionNotAuthorized = 0x0d000014;
        public const uint CrewFull = 0x0d00001d;
        public const uint CrewInviteDeclined = 0x0d00001e;
        public const uint CrewPreviousRequestInProgress = 0x0d00001f;
        public const uint CrewCurrentMember = 0x0d000020;
        public const uint CrewInvalidCharacterName = 0x0d000021;
        public const uint CrewInvalidTeamName = 0x0d000022;
        public const uint TeamFull = 0x0d00002d;
        public const uint TeamInviteDeclined = 0x0d00002e;
        public const uint TeamPreviousRequestInProgress = 0x0d00002f;
        public const uint TeamCurrentMember = 0x0d000030;
        public const uint TeamInvalidCharacterName = 0x0d000031;
        public const uint TeamNotTeamLeader = 0x0d000039;

        /// <summary>Retail's loot mode in every team packet (80 8d / 80 8e); the client's own 80 90 picks 1..4.</summary>
        public const byte DefaultLootMode = 4;

        // Friend list (80 d7) status byte: in the list sent at login 3b for a friend who is online and 3c for one who
        // is not (entilsar capture: the 3b handles are exactly those that also got 80 de). Answering adds, retail
        // sent 84 for 27 handles and 41 for two; we read 41 as "no such handle" (not confirmed).
        public const byte FriendOnlineListed = 0x3b;
        public const byte FriendListed = 0x3c;
        public const byte FriendAdded = 0x84;
        public const byte FriendNotFound = 0x41;

        public static uint InviteDeclinedCode(byte groupType)
        {
            switch (groupType)
            {
                case GroupFaction: return FactionInviteDeclined;
                case GroupCrew: return CrewInviteDeclined;
                default: return TeamInviteDeclined;
            }
        }

        public static uint InvalidCharacterNameCode(byte groupType)
        {
            switch (groupType)
            {
                case GroupFaction: return FactionInvalidCharacterName;
                case GroupCrew: return CrewInvalidCharacterName;
                default: return TeamInvalidCharacterName;
            }
        }

        public static uint CurrentMemberCode(byte groupType)
        {
            switch (groupType)
            {
                case GroupFaction: return FactionCurrentMember;
                case GroupCrew: return CrewCurrentMember;
                default: return TeamCurrentMember;
            }
        }

        /// <summary>70 &lt;type&gt; &lt;charId&gt; 09 00 &lt;flag&gt; &lt;sized handle&gt;: someone joined my group. Retail flag: 00 for
        /// a crew; for a team the newcomer's place in the member list (01 for the second member, 02 for the third).</summary>
        public static byte[] JoinedGroup(byte groupType, UInt32 charId, byte flag, string handle)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x70);
            pak.AddByte(groupType);
            pak.AddUint32(charId, 1);
            pak.AddUint16(9, 1);
            pak.AddByte(flag);
            pak.AddSizedTerminatedString(handle);
            return pak.ReturnFinalPacket();
        }

        /// <summary>72 &lt;type&gt; &lt;charId&gt;: someone left my group.</summary>
        public static byte[] LeftGroup(byte groupType, UInt32 charId)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x72);
            pak.AddByte(groupType);
            pak.AddUint32(charId, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>77 &lt;type&gt;: the group was disbanded (retail sent 77 03 to the leader and the member).</summary>
        public static byte[] GroupDisbanded(byte groupType)
        {
            return new byte[] {0x77, groupType};
        }

        /// <summary>6f &lt;type&gt; 08 00 &lt;result u32&gt; &lt;sized handle&gt;: the answer to my invite (relayed from the invitee).</summary>
        public static byte[] InviteAnswer(byte groupType, UInt32 result, string otherHandle)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x6f);
            pak.AddByte(groupType);
            pak.AddUint16(8, 1);
            pak.AddUint32(result, 1);
            pak.AddSizedTerminatedString(otherHandle);
            return pak.ReturnFinalPacket();
        }

        /// <summary>6e 01 06 00 &lt;offset&gt; &lt;sized inviter&gt; &lt;sized faction&gt;: invite to a faction (ProxyM2_m2_packets).</summary>
        public static byte[] FactionInvite(string inviterHandle, string factionName)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x6e);
            pak.AddByte(GroupFaction);
            pak.AddUint16(6, 1);
            pak.AddUint16((UInt16) (6 + 2 + inviterHandle.Length + 1), 1);
            pak.AddSizedTerminatedString(inviterHandle);
            pak.AddSizedTerminatedString(factionName);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 88 07 00 &lt;offset&gt; &lt;org&gt; &lt;sized inviter&gt; &lt;sized crew&gt;: invite to a crew.</summary>
        public static byte[] CrewInvite(string inviterHandle, string crewName, byte org)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_CREW_INVITE, 0);
            pak.AddUint16(7, 1);
            pak.AddUint16((UInt16) (7 + 2 + inviterHandle.Length + 1), 1);
            pak.AddByte(org);
            pak.AddSizedTerminatedString(inviterHandle);
            pak.AddSizedTerminatedString(crewName);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 8f 0c 00 &lt;offset&gt; 00x6 &lt;sized inviter&gt; &lt;sized team&gt;: invite to a mission team.</summary>
        public static byte[] TeamInvite(string inviterHandle, string teamName)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_TEAM_INVITE_MEMBER, 0);
            pak.AddUint16(12, 1);
            pak.AddUint16((UInt16) (12 + 2 + inviterHandle.Length + 1), 1);
            pak.AddByteArray(new byte[6]);
            pak.AddSizedTerminatedString(inviterHandle);
            pak.AddSizedTerminatedString(teamName);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 8d &lt;teamId&gt; 0e 00 &lt;loot u16&gt; &lt;result u32&gt; &lt;sized name&gt;: my mission team exists.</summary>
        public static byte[] TeamCreated(UInt32 teamId, string teamName)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_TEAM_CREATE, 0);
            pak.AddUint32(teamId, 1);
            pak.AddUint16(14, 1);
            pak.AddUint16(DefaultLootMode, 1);
            pak.AddUint32(ResultOk, 1);
            pak.AddSizedTerminatedString(teamName);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 8d with team id 0 and a result code: retail's answer to "/invite Warboty" (no such handle).</summary>
        public static byte[] TeamRequestFailed(UInt32 result)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_TEAM_CREATE, 0);
            pak.AddUint32(0, 1);
            pak.AddUint16(0, 1);
            pak.AddUint16(0, 1);
            pak.AddUint32(result, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 8e: the whole team, sent to a player who joined it.
        /// &lt;my charId&gt; &lt;teamId&gt; 13 00 &lt;leader charId&gt; &lt;loot u8&gt; &lt;member list offset&gt; &lt;sized name&gt;
        /// &lt;count u16&gt; then per member 00 &lt;charId&gt; &lt;handle, 33 bytes&gt;.</summary>
        public static byte[] TeamInfo(UInt32 charId, UInt32 teamId, UInt32 leaderCharId, string teamName,
            IList<KeyValuePair<UInt32, string>> members)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x808e, 0);
            pak.AddUint32(charId, 1);
            pak.AddUint32(teamId, 1);
            pak.AddUint16(19, 1);
            pak.AddUint32(leaderCharId, 1);
            pak.AddByte(DefaultLootMode);
            pak.AddUint16((UInt16) (19 + 2 + teamName.Length + 1), 1);
            pak.AddSizedTerminatedString(teamName);
            pak.AddUint16((UInt16) members.Count, 1);
            foreach (KeyValuePair<UInt32, string> member in members)
            {
                pak.AddByte(0x00);
                pak.AddUint32(member.Key, 1);
                pak.AddByteArray(FixedString(member.Value, 33));
            }

            return pak.ReturnFinalPacket();
        }

        /// <summary>80 90 &lt;mode&gt;: loot mode changed; retail echoed the client's own 80 90 back.</summary>
        public static byte[] LootMode(byte mode)
        {
            return new byte[] {0x80, 0x90, mode};
        }

        /// <summary>2e &lt;channel&gt; 00 &lt;charId&gt; 24 00 00 00 &lt;message offset u32&gt; 00x21 &lt;sized handle&gt; &lt;sized text&gt;.</summary>
        public static byte[] ChatMessage(byte channel, UInt32 charId, string handle, string message)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte((byte) RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE);
            pak.AddByte(channel);
            pak.AddByte(0x00);
            pak.AddUint32(charId, 1);
            pak.AddUint32(36, 1);
            pak.AddUint32((UInt32) (36 + 2 + handle.Length + 1), 1);
            pak.AddByteArray(new byte[21]);
            pak.AddSizedTerminatedString(handle);
            pak.AddSizedTerminatedString(message);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 d7 08 00 &lt;status&gt; 00 00 8e &lt;sized handle&gt;: one friend list entry / add result.</summary>
        public static byte[] FriendListStatus(byte status, string handleWithPrefix)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_FRIENDLIST_STATUS_ADD, 0);
            pak.AddUint16(8, 1);
            pak.AddByteArray(new byte[] {status, 0x00, 0x00, 0x8e});
            pak.AddSizedTerminatedString(handleWithPrefix);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 de 05 00 00 &lt;sized handle&gt;: a friend came online.</summary>
        public static byte[] FriendOnline(string handleWithPrefix)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_FRIEND_ONLINE, 0);
            pak.AddUint16(5, 1);
            pak.AddByte(0x00);
            pak.AddSizedTerminatedString(handleWithPrefix);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 df 04 00 &lt;sized handle&gt;: a friend went offline (no pad byte, unlike 80 de).</summary>
        public static byte[] FriendOffline(string handleWithPrefix)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_FRIEND_OFFLINE, 0);
            pak.AddUint16(4, 1);
            pak.AddSizedTerminatedString(handleWithPrefix);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 86 for a player without a crew; retail sent exactly these bytes (with the player's char id)
        /// to mxoemu3 at login.</summary>
        public static byte[] NoCrewInfo(UInt32 charId)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_CREW_MEMBERS_LIST, 0);
            pak.AddUint32(charId, 1);
            pak.AddHexBytes("000000000000000000000000000000000000000000210000000000230000000000");
            return pak.ReturnFinalPacket();
        }

        /// <summary>Own-view (view 2) attribute update: 02 00 02 &lt;group masks&gt; &lt;values&gt;. Retail set FactionID and
        /// CrewID this way on joining a crew (02 00 02 80 80 80 80 82 &lt;faction&gt; 20 &lt;crew&gt;) and MissionTeamID on
        /// joining or creating a team (02 00 02 80 20 &lt;team id&gt;).</summary>
        public static byte[] SelfViewUpdate(byte[] attributeBlock)
        {
            byte[] result = new byte[3 + attributeBlock.Length];
            result[0] = 0x02;
            result[1] = 0x00;
            result[2] = 0x02;
            Array.Copy(attributeBlock, 0, result, 3, attributeBlock.Length);
            return result;
        }

        /// <summary>Exactly <paramref name="size"/> bytes: the text, then zeros (cut so a terminator always fits).</summary>
        public static byte[] FixedString(string value, int size)
        {
            byte[] result = new byte[size];
            byte[] text = Encoding.ASCII.GetBytes(value ?? "");
            Array.Copy(text, 0, result, 0, Math.Min(text.Length, size - 1));
            return result;
        }
    }
}
