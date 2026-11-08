using System;
using System.Collections.Generic;
using hds;
using hds.shared;
using hds.world.Structures;

namespace hds.Tests;

// Every expected byte string below is a retail packet, copied from the capture named next to it (all under
// research/packet-dumps/community-intake/). "two-person" = 20260607T022135Z-hdneo-public-two-person-actions-*:
// AfterWhoruNeo (char 0x1d475) invites mxoemu3 (char 0x50003) to his mission team and his crew "Reservoir Dogs".
public class SocialPacketTests
{
    private static byte[] Hex(string hex) => StringUtils.hexStringToBytes(hex.Replace(" ", ""));
    private static string H(byte[] bytes) => Convert.ToHexString(bytes);
    private static string H(string hex) => hex.Replace(" ", "").ToUpperInvariant();

    private const uint AfterWhoruNeo = 0x0001d475;
    private const uint Mxoemu3 = 0x00050003;
    private const string TeamName = "AfterWhoruNeo's Mission Team";

    [Fact]
    public void JoinedGroupMatchesRetailTeamAndCrewJoin()
    {
        // two-person, AfterWhoruNeo's side 09:40:45 and 09:41:13.
        Assert.Equal(H("70 03 03 00 05 00 09 00 01 08 00 6d 78 6f 65 6d 75 33 00"),
            H(SocialPackets.JoinedGroup(SocialPackets.GroupTeam, Mxoemu3, 1, "mxoemu3")));
        Assert.Equal(H("70 02 03 00 05 00 09 00 00 08 00 6d 78 6f 65 6d 75 33 00"),
            H(SocialPackets.JoinedGroup(SocialPackets.GroupCrew, Mxoemu3, 0, "mxoemu3")));
    }

    [Fact]
    public void LeftGroupAndDisbandMatchRetail()
    {
        Assert.Equal(H("72 02 03 00 05 00"), H(SocialPackets.LeftGroup(SocialPackets.GroupCrew, Mxoemu3)));
        // mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain 07:30:43, after the client's "71 03".
        Assert.Equal(H("72 03 bb 9a 06 00"), H(SocialPackets.LeftGroup(SocialPackets.GroupTeam, 0x00069abb)));
        Assert.Equal(H("77 03"), H(SocialPackets.GroupDisbanded(SocialPackets.GroupTeam)));
    }

    [Fact]
    public void InviteAnswerRelayMatchesRetailDecline()
    {
        // two-person, AfterWhoruNeo's side 09:37:05: mxoemu3 declined the team invite.
        Assert.Equal(H("6f 03 08 00 2e 00 00 0d 08 00 6d 78 6f 65 6d 75 33 00"),
            H(SocialPackets.InviteAnswer(SocialPackets.GroupTeam, SocialPackets.TeamInviteDeclined, "mxoemu3")));
    }

    [Fact]
    public void InvitesMatchRetail()
    {
        // two-person, mxoemu3's side 09:36:59 (team) and 09:41:10 (crew).
        Assert.Equal(H("80 8f 0c 00 1c 00 00 00 00 00 00 00 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00 1d 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 27 73 20 4d 69 73 73 69 6f 6e 20 54 65 61 6d 00"),
            H(SocialPackets.TeamInvite("AfterWhoruNeo", TeamName)));
        Assert.Equal(H("80 88 07 00 17 00 01 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00 0f 00 52 65 73 65 72 76 6f 69 72 20 44 6f 67 73 00"),
            H(SocialPackets.CrewInvite("AfterWhoruNeo", "Reservoir Dogs", 1)));
        // ProxyM2_m2_packets 18:55:40: Midnight1 invites Warboy to the faction "Fallen Horizon".
        Assert.Equal(H("6e 01 06 00 12 00 0a 00 4d 69 64 6e 69 67 68 74 31 00 0f 00 46 61 6c 6c 65 6e 20 48 6f 72 69 7a 6f 6e 00"),
            H(SocialPackets.FactionInvite("Midnight1", "Fallen Horizon")));
    }

    [Fact]
    public void TeamCreatedMatchesRetail()
    {
        // two-person, AfterWhoruNeo's side 09:34:12 (answer to his mission list request).
        Assert.Equal(H("80 8d 69 00 00 02 0e 00 04 00 00 00 00 00 1d 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 27 73 20 4d 69 73 73 69 6f 6e 20 54 65 61 6d 00"),
            H(SocialPackets.TeamCreated(0x02000069, TeamName)));
        // mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline 02:31:27: "/invite Warboty".
        Assert.Equal(H("80 8d 00 00 00 00 00 00 00 00 31 00 00 0d"),
            H(SocialPackets.TeamRequestFailed(SocialPackets.TeamInvalidCharacterName)));
    }

    [Fact]
    public void TeamInfoMatchesRetail()
    {
        // two-person, mxoemu3's side 09:40:44, right after accepting.
        string retail = "80 8e 03 00 05 00 69 00 00 02 13 00 75 d4 01 00 04 32 00 1d 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 27 73 20 4d 69 73 73 69 6f 6e 20 54 65 61 6d 00 02 00"
                        + " 00 75 d4 01 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f" + Zeros(33 - 13)
                        + " 00 03 00 05 00 6d 78 6f 65 6d 75 33" + Zeros(33 - 7);
        var members = new List<KeyValuePair<uint, string>>
        {
            new KeyValuePair<uint, string>(AfterWhoruNeo, "AfterWhoruNeo"),
            new KeyValuePair<uint, string>(Mxoemu3, "mxoemu3")
        };
        byte[] built = SocialPackets.TeamInfo(Mxoemu3, 0x02000069, AfterWhoruNeo, TeamName, members);
        Assert.Equal(128, built.Length); // retail length
        Assert.Equal(H(retail), H(built));
    }

    [Fact]
    public void ChatMatchesRetailFactionChat()
    {
        // mxopackets1_entilsar_enti_lastday2 21:43:31, faction channel 0x13.
        Assert.Equal(H("2e 13 00 75 d4 01 00 24 00 00 00 34 00 00 00" + Zeros(21) + " 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00 04 00 6c 6f 6c 00"),
            H(SocialPackets.ChatMessage(SocialPackets.ChatFaction, AfterWhoruNeo, "AfterWhoruNeo", "lol")));
    }

    [Fact]
    public void FriendPacketsMatchRetail()
    {
        string handle = "SOE+MXO+Vector-Hostile+Medanon";
        // mxopackets1_entilsar_enti_lastday2 21:43:23 (online) and 22:07:42 (offline).
        Assert.Equal(H("80 de 05 00 00 1f 00 53 4f 45 2b 4d 58 4f 2b 56 65 63 74 6f 72 2d 48 6f 73 74 69 6c 65 2b 4d 65 64 61 6e 6f 6e 00"),
            H(SocialPackets.FriendOnline(handle)));
        Assert.Equal(H("80 df 04 00 1f 00 53 4f 45 2b 4d 58 4f 2b 56 65 63 74 6f 72 2d 48 6f 73 74 69 6c 65 2b 4d 65 64 61 6e 6f 6e 00"),
            H(SocialPackets.FriendOffline(handle)));
        // two-person, AfterWhoruNeo's login friend list.
        Assert.Equal(H("80 d7 08 00 3c 00 00 8e 1f 00 53 4f 45 2b 4d 58 4f 2b 56 65 63 74 6f 72 2d 48 6f 73 74 69 6c 65 2b 4d 65 64 61 6e 6f 6e 00"),
            H(SocialPackets.FriendListStatus(SocialPackets.FriendListed, handle)));
    }

    [Fact]
    public void NoCrewInfoMatchesRetail()
    {
        // two-person, mxoemu3's login (no crew yet).
        Assert.Equal(H("80 86 03 00 05 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 21 00 00 00 00 00 23 00 00 00 00 00"),
            H(SocialPackets.NoCrewInfo(Mxoemu3)));
    }

    [Fact]
    public void CrewInfoMatchesRetail()
    {
        // two-person, AfterWhoruNeo's login: "Reservoir Dogs", captain TheAvenNigera, first mate AfterWhoruNeo.
        string retail = "80 86 75 d4 01 00 4b b1 00 00 01 21 00 93 f5 01 00 75 d4 01 00 00 00 00 00 32 00 14 02 00 00 3e 01 0f 00 52 65 73 65 72 76 6f 69 72 20 44 6f 67 73 00 07 00"
                        + Member(AfterWhoruNeo, "AfterWhoruNeo", 1) + Member(0x00011099, "Cherberus", 0)
                        + Member(0x0001f593, "TheAvenNigera", 0) + Member(0x000255ee, "L1zZ", 0)
                        + Member(0x000298c2, "Karthos", 0) + Member(0x0002f962, "TheWhoruNeo", 0)
                        + Member(0x000476f9, "Architekt", 0) + " 00 00";
        var crew = new Crew {crewId = 0xb14b, crewName = "Reservoir Dogs", org = 1, money = 0};
        var members = new List<CrewMember>
        {
            M(AfterWhoruNeo, "AfterWhoruNeo", 1), M(0x00011099, "Cherberus", 0), M(0x0001f593, "TheAvenNigera", 0),
            M(0x000255ee, "L1zZ", 0), M(0x000298c2, "Karthos", 0), M(0x0002f962, "TheWhoruNeo", 0),
            M(0x000476f9, "Architekt", 0)
        };
        byte[] built = ServerPackets.BuildCrewInfoPacket(AfterWhoruNeo, crew, members, 0x0001f593, AfterWhoruNeo);
        Assert.Equal(H(retail), H(built));
    }

    [Fact]
    public void LootModeEchoesMode()
    {
        Assert.Equal(H("80 90 02"), H(SocialPackets.LootMode(2)));
    }

    [Fact]
    public void SelfViewUpdatePrefixesOwnViewAndUpdateType()
    {
        // two-person, mxoemu3's side 09:40:44: MissionTeamID (self-view group 1, bit 5) set to the team id.
        Assert.Equal(H("02 00 02 80 20 69 00 00 02"), H(SocialPackets.SelfViewUpdate(Hex("80 20 69 00 00 02"))));
    }

    private static CrewMember M(uint id, string handle, ushort online) =>
        new CrewMember {charId = id, handle = handle, isOnline = online};

    private static string Member(uint id, string handle, int online)
    {
        byte[] name = SocialPackets.FixedString(handle, 32);
        return " 00 " + Convert.ToHexString(BitConverter.GetBytes(id)) + Convert.ToHexString(name) + online.ToString("X2");
    }

    private static string Zeros(int count) => string.Concat(System.Linq.Enumerable.Repeat(" 00", count));
}
