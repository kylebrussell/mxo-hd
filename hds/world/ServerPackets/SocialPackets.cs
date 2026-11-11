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
        // sent 84 for 27 handles and 41 (with flag 0e instead of 8e) for two. The same 84/41 pair answers tells
        // (2c), where 41 came back for a mistyped handle, so 41 = "no such handle".
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

        /// <summary>The flag byte after a chat-service result: 8e normally, 0e with the failure code 41
        /// (both in 80 d7 friend answers and in 2c tell acknowledgements).</summary>
        private static byte ResultFlag(byte status) => status == FriendNotFound ? (byte) 0x0e : (byte) 0x8e;

        /// <summary>80 d7 08 00 &lt;status&gt; 00 00 &lt;8e|0e&gt; &lt;sized handle&gt;: one friend list entry / add result.</summary>
        public static byte[] FriendListStatus(byte status, string handleWithPrefix)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_FRIENDLIST_STATUS_ADD, 0);
            pak.AddUint16(8, 1);
            pak.AddByteArray(new byte[] {status, 0x00, 0x00, ResultFlag(status)});
            pak.AddSizedTerminatedString(handleWithPrefix);
            return pak.ReturnFinalPacket();
        }

        /// <summary>2c &lt;counter&gt; &lt;84|41&gt; 00 00 &lt;8e|0e&gt;: answer to the sender's tell (client 29), echoing the
        /// client's per-tell counter. Retail: 84/8e when delivered (entilsar capture, counters 00..07), 41/0e for a
        /// handle nobody has (mxopackets1_medanon_niobe3dualduality 03:12:51, "/tell afterehoruneo").</summary>
        public static byte[] TellResult(byte counter, bool delivered)
        {
            byte status = delivered ? FriendAdded : FriendNotFound;
            return new byte[] {0x2c, counter, status, 0x00, 0x00, ResultFlag(status)};
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

        /// <summary>The same change as other players see it on their view of me: 02 &lt;group masks&gt; &lt;values&gt;, with
        /// the other-view attribute order. Takes GameObject.GetUpdateAttributes output, whose leading attribute count
        /// is replaced by the update type 02. Retail (two-person, AfterWhoruNeo's side): mxoemu3's view
        /// <c>17 00 02 80 80 c0 &lt;FactionID&gt; 80 02 &lt;CrewID&gt;</c> on joining the crew and <c>17 00 02 40 00 00 00 00</c>
        /// (MissionTeamID 0) when the team was disbanded.</summary>
        public static byte[] OtherViewUpdate(byte[] countedAttributeBlock)
        {
            byte[] result = (byte[]) countedAttributeBlock.Clone();
            result[0] = 0x02;
            return result;
        }

        // ---- membership changes (client.dll layouts; no retail capture has these answers) ---------------------------

        // Faction rank change refusals (80 81), as the client's handler 0x621ba610 names them.
        public const uint FactionPromoteeNotFound = 0x0d00000b;
        public const uint FactionPromoterNotFound = 0x0d000010;
        public const uint FactionPromoterNotHigher = 0x0d000014;
        public const uint FactionIllegalRanks = 0x0d000015;

        /// <summary>Faction ranks run from 1 (the leading crew) to 5.</summary>
        public const ushort TopFactionRank = 1;
        public const ushort BottomFactionRank = 5;

        /// <summary>75 &lt;type&gt; &lt;charId&gt; (crew, team) or &lt;crewId&gt; (faction): the group has a new leader
        /// (handler 0x62178d5e: "%s is now the leader of your mission team.", "... the captain of your crew.").</summary>
        public static byte[] NewLeader(byte groupType, UInt32 id)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x75);
            pak.AddByte(groupType);
            pak.AddUint32(id, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 87 &lt;FirstMateID&gt;: "%s is now the first mate of your crew." (0x621a9bb0); 0 = none, no text.</summary>
        public static byte[] FirstMate(UInt32 charId)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x8087, 0);
            pak.AddUint32(charId, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>7f &lt;crewId&gt; &lt;rank u8&gt;: "Faction member %s is now Rank %i." (0x621bbab0).</summary>
        public static byte[] FactionRank(UInt32 crewId, byte rank)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x7f);
            pak.AddUint32(crewId, 1);
            pak.AddByte(rank);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 81 &lt;crewId&gt; &lt;bPromote&gt; &lt;Status&gt;: only ever an error popup (0x621ba610).</summary>
        public static byte[] FactionRankFailed(UInt32 crewId, bool promote, UInt32 status)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x8081, 0);
            pak.AddUint32(crewId, 1);
            pak.AddByte((byte) (promote ? 1 : 0));
            pak.AddUint32(status, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>7e &lt;crewId&gt; &lt;captain charId&gt; &lt;captain handle, 32 bytes&gt; &lt;online u8&gt; (42 bytes): a
        /// crew of the faction has a new captain (0x621ba500).</summary>
        public static byte[] FactionCrewCaptain(UInt32 crewId, UInt32 captainCharId, string captainHandle, bool online)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x7e);
            pak.AddUint32(crewId, 1);
            pak.AddUint32(captainCharId, 1);
            pak.AddByteArray(FixedString(captainHandle, 32));
            pak.AddByte((byte) (online ? 1 : 0));
            return pak.ReturnFinalPacket();
        }

        /// <summary>73 &lt;crewId&gt;: a crew left the faction ("%s has left the faction."; the crew's own members lose
        /// the faction, 0x621be610).</summary>
        public static byte[] FactionCrewRemoved(UInt32 crewId)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x73);
            pak.AddUint32(crewId, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>74 &lt;crewId&gt; &lt;Status&gt;: "Failed to eject faction."</summary>
        public static byte[] FactionRemoveFailed(UInt32 crewId, UInt32 status)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x74);
            pak.AddUint32(crewId, 1);
            pak.AddUint32(status, 1);
            return pak.ReturnFinalPacket();
        }

        // Crew creation answers (80 85, handler 0x621a9520). 0 sets the crew up with the creator as its captain.
        public const uint CrewCreateDeclined = 0x0d00001e;       // "... Invitation was declined."
        public const uint CrewCreateInProgress = 0x0d00001f;     // "... Previous crew creation or invitation request in progress."
        public const uint CrewCreateAlreadyMember = 0x0d000020;  // "... You are already a member of a crew."
        public const uint CrewCreateInvalidName = 0x0d000021;    // "... The crew name is not valid."

        /// <summary>80 85 &lt;crewId&gt; 0d 00 &lt;org u8&gt; &lt;Status&gt; &lt;sized name&gt;: the answer to the crew creation
        /// window's 80 84. Status 0: "Crew created." and the creator is the captain of a one-member crew.</summary>
        public static byte[] CrewCreated(UInt32 crewId, string crewName, byte org, UInt32 status)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x8085, 0);
            pak.AddUint32(crewId, 1);
            pak.AddUint16(0x0d, 1);
            pak.AddByte(org);
            pak.AddUint32(status, 1);
            pak.AddSizedTerminatedString(crewName ?? "");
            return pak.ReturnFinalPacket();
        }

        /// <summary>One player in a team search result (80 93): 63 bytes, read by 0x620dad30.</summary>
        public struct Seeker
        {
            public UInt32 CharId;
            public string Handle;
            public byte Level;
            public UInt32 BestDiscipline;
        }

        /// <summary>80 93 04 00 &lt;count u16&gt; then per player 00 &lt;charId&gt; &lt;handle, 32 bytes&gt; &lt;level u8&gt;
        /// &lt;flag u8&gt; &lt;best discipline ability u32&gt; &lt;5 x u32&gt;. A count of 0 shows "No matching players could be
        /// found!". The flag and the five u32s aren't shown; they go as 0.</summary>
        public static byte[] SeekerResults(IList<Seeker> seekers)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x8093, 0);
            pak.AddUint16(4, 1);
            pak.AddUint16((UInt16) seekers.Count, 1);
            foreach (Seeker seeker in seekers)
            {
                pak.AddByte(0x00);
                pak.AddUint32(seeker.CharId, 1);
                pak.AddByteArray(FixedString(seeker.Handle, 32));
                pak.AddByte(seeker.Level);
                pak.AddByte(0x00);
                pak.AddUint32(seeker.BestDiscipline, 1);
                pak.AddByteArray(new byte[20]);
            }

            return pak.ReturnFinalPacket();
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
