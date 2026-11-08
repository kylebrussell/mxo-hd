using System;
using System.Collections.Generic;
using hds;
using hds.shared;
using hds.world.Structures;

namespace hds.Tests;

// Client RPC bytes are retail (two-person capture, see SocialPacketTests); RpcManager strips the one-byte header.
public class SocialHandlerTests
{
    private static byte[] Rpc(string hexWithHeader) =>
        StringUtils.hexStringToBytes(hexWithHeader.Replace(" ", "")).AsSpan(1).ToArray();

    [Fact]
    public void ParsesRetailTeamInvite()
    {
        Assert.True(SocialRequests.TryParseGroupInvite(
            Rpc("6e 03 06 00 10 00 08 00 6d 78 6f 65 6d 75 33 00 1d 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 27 73 20 4d 69 73 73 69 6f 6e 20 54 65 61 6d 00"),
            out SocialRequests.GroupInvite invite));
        Assert.Equal(SocialPackets.GroupTeam, invite.GroupType);
        Assert.Equal("mxoemu3", invite.InviteeHandle);
        Assert.Equal("AfterWhoruNeo's Mission Team", invite.GroupName);
    }

    [Fact]
    public void ParsesRetailCrewInviteWithNameFirst()
    {
        // The crew name comes first on the wire; the offsets say which string is which.
        Assert.True(SocialRequests.TryParseGroupInvite(
            Rpc("6e 02 17 00 06 00 0f 00 52 65 73 65 72 76 6f 69 72 20 44 6f 67 73 00 08 00 6d 78 6f 65 6d 75 33 00"),
            out SocialRequests.GroupInvite invite));
        Assert.Equal(SocialPackets.GroupCrew, invite.GroupType);
        Assert.Equal("mxoemu3", invite.InviteeHandle);
        Assert.Equal("Reservoir Dogs", invite.GroupName);
    }

    [Fact]
    public void BuiltInviteParsesBack()
    {
        byte[] rpc = SocialRequests.BuildGroupInvite(SocialPackets.GroupFaction, "Warboy", "Fallen Horizon");
        Assert.True(SocialRequests.TryParseGroupInvite(rpc, out SocialRequests.GroupInvite invite));
        Assert.Equal(SocialPackets.GroupFaction, invite.GroupType);
        Assert.Equal("Warboy", invite.InviteeHandle);
        Assert.Equal("Fallen Horizon", invite.GroupName);
    }

    [Fact]
    public void ParsesRetailInviteAnswers()
    {
        Assert.True(SocialRequests.TryParseInviteAnswer(
            Rpc("6f 03 08 00 2e 00 00 0d 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00"), out var declined));
        Assert.False(declined.Accepted);
        Assert.Equal(SocialPackets.TeamInviteDeclined, declined.Result);
        Assert.Equal("AfterWhoruNeo", declined.InviterHandle);

        Assert.True(SocialRequests.TryParseInviteAnswer(
            Rpc("6f 02 08 00 00 00 00 00 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00"), out var accepted));
        Assert.True(accepted.Accepted);
        Assert.Equal(SocialPackets.GroupCrew, accepted.GroupType);
    }

    [Fact]
    public void ParsesRetailGroupChat()
    {
        // mxopackets1_crashover2k_afterwhoruneo_proxy_024_chatting_hyperjump_fight 13:33:21 (crew channel).
        Assert.True(SocialRequests.TryParseChat(Rpc("28 12 00 08 00 00 00 00 09 00 66 69 6e 69 73 68 65 64 00"), out var line));
        Assert.Equal(SocialPackets.ChatCrew, line.Channel);
        Assert.Equal("finished", line.Text);
    }

    [Fact]
    public void ChatLongerThan255CharactersParses()
    {
        string text = new string('x', 300);
        byte[] rpc = new byte[9 + text.Length];
        rpc[0] = SocialPackets.ChatArea; rpc[2] = 0x08;
        rpc[7] = (byte) ((text.Length + 1) & 0xff); rpc[8] = (byte) ((text.Length + 1) >> 8);
        System.Text.Encoding.ASCII.GetBytes(text).CopyTo(rpc, 9);
        Array.Resize(ref rpc, rpc.Length + 1);
        Assert.True(SocialRequests.TryParseChat(rpc, out var line));
        Assert.Equal(text, line.Text);
    }

    [Fact]
    public void RejectsTruncatedRequests()
    {
        Assert.False(SocialRequests.TryParseGroupInvite(new byte[] {3, 6, 0}, out _));
        Assert.False(SocialRequests.TryParseInviteAnswer(new byte[] {3, 8, 0, 0}, out _));
        Assert.False(SocialRequests.TryParseGroupInvite(Rpc("6e 03 06 00 10 00 30 00 6d 78"), out _));
    }

    [Fact]
    public void TeamsGetRetailShapedIdsAndKeepMemberOrder()
    {
        var groups = new SocialGroups();
        LiveTeam team = groups.CreateTeam(1, "Leader");
        Assert.Equal(0x02000000u, team.Id & 0xff000000u);
        Assert.Equal("Leader's Mission Team", team.Name);
        Assert.Equal(1, groups.AddTeamMember(team.Id, 2, "Two"));
        Assert.Equal(2, groups.AddTeamMember(team.Id, 3, "Three"));
        Assert.Equal(1, groups.AddTeamMember(team.Id, 2, "Two")); // already on it
        Assert.Same(team, groups.FindTeamOf(3));
        Assert.Null(groups.FindTeamOf(4));
    }

    [Fact]
    public void LeaderLeavingHandsTeamOnAndLastMemberDeletesIt()
    {
        var groups = new SocialGroups();
        LiveTeam team = groups.CreateTeam(1, "Leader");
        groups.AddTeamMember(team.Id, 2, "Two");
        LiveTeam after = groups.RemoveTeamMember(team.Id, 1);
        Assert.Equal(2u, after.LeaderCharId);
        Assert.Null(groups.RemoveTeamMember(team.Id, 2));
        Assert.Null(groups.FindTeam(team.Id));
    }

    [Fact]
    public void InvitesBlockDuplicatesUntilAnsweredOrExpired()
    {
        var groups = new SocialGroups();
        DateTime t0 = new DateTime(2026, 11, 7, 22, 0, 0, DateTimeKind.Utc);
        var invite = new PendingInvite {GroupType = SocialPackets.GroupCrew, InviterCharId = 1, InviterHandle = "Cap", InviteeHandle = "Joe", GroupName = "Crew"};
        Assert.True(groups.AddInvite(invite, t0));
        Assert.False(groups.AddInvite(new PendingInvite {GroupType = SocialPackets.GroupTeam, InviterHandle = "Other", InviteeHandle = "joe"}, t0.AddSeconds(5)));

        // An answer naming a different inviter or group type does not consume it.
        Assert.Null(groups.TakeInvite("Joe", SocialPackets.GroupTeam, "Cap", t0.AddSeconds(6)));
        Assert.Null(groups.TakeInvite("Joe", SocialPackets.GroupCrew, "Someone", t0.AddSeconds(6)));
        Assert.Same(invite, groups.TakeInvite("JOE", SocialPackets.GroupCrew, "cap", t0.AddSeconds(7)));
        Assert.Null(groups.TakeInvite("Joe", SocialPackets.GroupCrew, "Cap", t0.AddSeconds(8)));

        Assert.True(groups.AddInvite(invite, t0));
        Assert.Null(groups.TakeInvite("Joe", SocialPackets.GroupCrew, "Cap", t0 + SocialGroups.InviteLifetime));
        Assert.True(groups.AddInvite(invite, t0 + SocialGroups.InviteLifetime));
    }

    [Fact]
    public void BankMovesNeedTheMoney()
    {
        Assert.True(FCHandler.CanMoveMoney(true, 100, 0, 100));    // deposit all you have
        Assert.False(FCHandler.CanMoveMoney(true, 99, 5000, 100)); // the bank's money does not count
        Assert.True(FCHandler.CanMoveMoney(false, 0, 100, 100));
        Assert.False(FCHandler.CanMoveMoney(false, 5000, 99, 100));
        Assert.False(FCHandler.CanMoveMoney(true, 100, 100, 0));
    }

    [Fact]
    public void OwnViewGroupIdsMatchRetail()
    {
        var player = new PlayerCharacter();
        player.FactionID.setValue(0x5e31u);
        player.CrewID.setValue(0xb14bu);
        byte[] block = player.GetSelfUpdateAttributes(new List<hds.Attribute> {player.FactionID, player.CrewID}, false);
        // two-person, mxoemu3's side 09:41:13 (joined "Reservoir Dogs" of faction "The Duality").
        Assert.Equal("02000280808080823" + "15E0000204BB10000",
            Convert.ToHexString(SocialPackets.SelfViewUpdate(block)));

        player.MissionTeamID.setValue(0x02000069u);
        block = player.GetSelfUpdateAttributes(new List<hds.Attribute> {player.MissionTeamID}, false);
        // two-person, mxoemu3's side 09:40:44.
        Assert.Equal("020002802069000002", Convert.ToHexString(SocialPackets.SelfViewUpdate(block)));
    }
}
