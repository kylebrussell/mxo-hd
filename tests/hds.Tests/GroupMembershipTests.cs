using System;
using System.Collections.Generic;
using hds;
using hds.world.Structures;

namespace hds.Tests;

// Removing members, new leaders, first mates, faction ranks, crew creation and the team seekers list. The layouts are
// client.dll's (docs/CLIENT-ACTIONS.md, "Teams, crews and factions in detail"); no retail capture has these answers.
public class GroupMembershipTests
{
    private static string H(byte[] bytes) => Convert.ToHexString(bytes);
    private static string H(string hex) => hex.Replace(" ", "").ToUpperInvariant();

    [Fact]
    public void LeaderFirstMateAndRankRecordsAreTheClientsSizes()
    {
        Assert.Equal(H("75 02 03 00 05 00"), H(SocialPackets.NewLeader(SocialPackets.GroupCrew, 0x00050003)));
        Assert.Equal(H("80 87 03 00 05 00"), H(SocialPackets.FirstMate(0x00050003)));
        Assert.Equal(H("80 87 00 00 00 00"), H(SocialPackets.FirstMate(0)));
        Assert.Equal(H("7f 31 5e 00 00 04"), H(SocialPackets.FactionRank(0x5e31, 4)));
        Assert.Equal(H("80 81 31 5e 00 00 01 14 00 00 0d"),
            H(SocialPackets.FactionRankFailed(0x5e31, true, SocialPackets.FactionPromoterNotHigher)));
        Assert.Equal(H("73 31 5e 00 00"), H(SocialPackets.FactionCrewRemoved(0x5e31)));
        Assert.Equal(H("74 31 5e 00 00 14 00 00 0d"), H(SocialPackets.FactionRemoveFailed(0x5e31, SocialPackets.FactionNotAuthorized)));

        byte[] captain = SocialPackets.FactionCrewCaptain(0x5e31, 0x00050003, "mxoemu3", true);
        Assert.Equal(42, captain.Length);
        Assert.Equal(H("7e 31 5e 00 00 03 00 05 00 6d 78 6f 65 6d 75 33 00"), H(captain[..17]));
        Assert.Equal(1, captain[0x29]);
    }

    [Fact]
    public void CrewCreatedPutsTheNameAtOffset13()
    {
        // 80 85 <crewId> 0d 00 <org> <status> <sized name> (handler 0x621a9520)
        Assert.Equal(H("80 85 0a 00 00 00 0d 00 01 00 00 00 00 06 00 41 6c 70 68 61 00"),
            H(SocialPackets.CrewCreated(10, "Alpha", 1, SocialPackets.ResultOk)));
        Assert.Equal(H("80 85 00 00 00 00 0d 00 02 1e 00 00 0d 06 00 41 6c 70 68 61 00"),
            H(SocialPackets.CrewCreated(0, "Alpha", 2, SocialPackets.CrewCreateDeclined)));
    }

    [Fact]
    public void SeekerResultsAre63BytesAPlayer()
    {
        byte[] none = SocialPackets.SeekerResults(new List<SocialPackets.Seeker>());
        Assert.Equal(H("80 93 04 00 00 00"), H(none));

        byte[] one = SocialPackets.SeekerResults(new List<SocialPackets.Seeker>
        {
            new() { CharId = 0x1234, Handle = "Bob", Level = 12, BestDiscipline = 0x2a }
        });
        Assert.Equal(6 + 63, one.Length);
        Assert.Equal(H("80 93 04 00 01 00 00 34 12 00 00 42 6f 62 00"), H(one[..15]));
        Assert.Equal(12, one[6 + 0x25]);
        Assert.Equal(0x2au, BitConverter.ToUInt32(one, 6 + 0x27));
    }

    [Fact]
    public void FactionRankChangesFollowTheClientsRules()
    {
        // A captain changes only crews ranked below its own, one step at a time, within 1..5.
        Assert.Equal(SocialPackets.ResultOk, FactionRanks.Change(1, 5, true, out ushort promoted));
        Assert.Equal((ushort)4, promoted);
        Assert.Equal(SocialPackets.ResultOk, FactionRanks.Change(1, 2, true, out ushort level));
        Assert.Equal((ushort)1, level);
        Assert.Equal(SocialPackets.ResultOk, FactionRanks.Change(2, 3, false, out ushort demoted));
        Assert.Equal((ushort)4, demoted);
        Assert.Equal(SocialPackets.FactionPromoterNotHigher, FactionRanks.Change(3, 3, true, out _));
        Assert.Equal(SocialPackets.FactionPromoterNotHigher, FactionRanks.Change(4, 2, false, out _));
        Assert.Equal(SocialPackets.FactionIllegalRanks, FactionRanks.Change(1, 5, false, out _));

        // Crews stored before ranks were kept (0): the master's crew leads, the others are at the bottom
        Assert.Equal((ushort)1, FactionRanks.Effective(0, true));
        Assert.Equal((ushort)5, FactionRanks.Effective(0, false));
        Assert.Equal((ushort)3, FactionRanks.Effective(3, true));
    }

    [Fact]
    public void TeamLeaderMovesOnlyToAMember()
    {
        SocialGroups groups = new SocialGroups();
        LiveTeam team = groups.CreateTeam(1, "Alice");
        groups.AddTeamMember(team.Id, 2, "Bob");
        Assert.False(groups.SetLeader(team.Id, 3));
        Assert.True(groups.SetLeader(team.Id, 2));
        Assert.Equal(2u, groups.FindTeam(team.Id).LeaderCharId);
    }

    [Fact]
    public void SeekersAreFilteredByLevelFactionDisciplineAndOrganisation()
    {
        SocialGroups groups = new SocialGroups();
        groups.AddSeeker(new TeamSeeker { CharId = 1, Handle = "Me", Level = 10 });
        groups.AddSeeker(new TeamSeeker { CharId = 2, Handle = "Bob", Level = 12, Org = 1, FactionId = 7, BestDiscipline = 100 });
        groups.AddSeeker(new TeamSeeker { CharId = 3, Handle = "Carol", Level = 30, Org = 2, BestDiscipline = 200 });
        groups.AddSeeker(new TeamSeeker { CharId = 4, Handle = "Dave", Level = 12, Org = 2, BestDiscipline = 100 });

        List<string> Names(SeekerSearch search) => groups.FindSeekers(1, search).Select(s => s.Handle).ToList();

        Assert.Equal(new[] { "Bob", "Dave", "Carol" }, Names(new SeekerSearch { MinLevel = 1, MaxLevel = 50 }));
        Assert.Equal(new[] { "Bob", "Dave" }, Names(new SeekerSearch { MinLevel = 5, MaxLevel = 15 }));
        Assert.Equal(new[] { "Bob", "Dave" }, Names(new SeekerSearch { MinLevel = 15, MaxLevel = 5 })); // swapped
        Assert.Equal(new[] { "Bob" }, Names(new SeekerSearch { MyFactionOnly = true, FactionId = 7 }));
        Assert.Empty(Names(new SeekerSearch { MyFactionOnly = true, FactionId = 0 }));
        Assert.Equal(new[] { "Carol" }, Names(new SeekerSearch { Disciplines = { 200 } }));
        Assert.Equal(new[] { "Dave", "Carol" }, Names(new SeekerSearch { Orgs = { 2 } }));

        Assert.True(groups.RemoveSeeker(2));
        Assert.False(groups.IsSeeking(2));
        Assert.Equal(new[] { "Dave", "Carol" }, Names(new SeekerSearch()));
    }
}
