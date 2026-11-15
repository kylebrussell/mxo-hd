using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using hds;
using hds.shared;
using hds.world.Structures;

namespace hds.Tests;

[CollectionDefinition("MissionTeamRestore", DisableParallelization = true)]
public class MissionTeamRestoreCollection { }

[Collection("MissionTeamRestore")]
public class MissionTeamRestoreTests
{
    private static WorldClient Client(uint id, string name, bool active = true)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData(); c.playerData.setCharID(id);
        c.playerInstance = new PlayerCharacter(); c.messageQueue = new MessageQueue();
        var bytes = new byte[32]; Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
        c.playerInstance.CharacterName.setValue(bytes);
        if (active) c.playerData.activeMission = new Mission { id = 2202 };
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, long.MaxValue);
        return c;
    }
    private static byte[][] Rpcs(WorldClient c) => c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
        .Select(m => m.content[(m.content[0] >= 128 ? 2 : 1)..]).ToArray();

    [Fact]
    public void NoActiveMissionDoesNotCreateATeam()
    {
        const uint id = 60081;
        var c = Client(id, "NoMission", false);
        SocialHandler.RestoreMissionTeam(c);
        Assert.Null(SocialGroups.Instance.FindTeamOf(id)); Assert.Empty(Rpcs(c));
    }
    [Fact]
    public void RestoredSoloMissionGetsOwnLeaderMemberAndTeamId()
    {
        const uint id = 60082;
        var previousClients = WorldServer.Clients;
        Store.world ??= new WorldServer(); WorldServer.Clients = new();
        try
        {
            var c = Client(id, "RestoreSolo");
            SocialHandler.RestoreMissionTeam(c);
            var team = SocialGroups.Instance.FindTeamOf(id);
            Assert.NotNull(team); Assert.Equal(id, team.LeaderCharId); Assert.Single(team.Members);
            var info = Assert.Single(Rpcs(c), r => r[0] == 0x80 && r[1] == 0x8e);
            Assert.Equal(id, BitConverter.ToUInt32(info, 2)); Assert.Equal(team.Id, BitConverter.ToUInt32(info, 6));
            Assert.Equal(id, BitConverter.ToUInt32(info, 12));
            Assert.Equal(team.Id, BitConverter.ToUInt32(c.playerInstance.MissionTeamID.getValue()));
        }
        finally { SocialGroups.Instance.RemoveTeamMember(SocialGroups.Instance.FindTeamOf(id)?.Id ?? 0, id); WorldServer.Clients = previousClients; }
    }
    [Fact]
    public void ReplayingAnExistingTeamPreservesOtherLeaderAndMembership()
    {
        const uint leader = 60083, member = 60084;
        var previousClients = WorldServer.Clients;
        Store.world ??= new WorldServer(); WorldServer.Clients = new();
        var team = SocialGroups.Instance.CreateTeam(leader, "OtherLeader");
        SocialGroups.Instance.AddTeamMember(team.Id, member, "RestoreMember");
        try
        {
            var c = Client(member, "RestoreMember"); SocialHandler.RestoreMissionTeam(c);
            Assert.Same(team, SocialGroups.Instance.FindTeamOf(member)); Assert.Equal(leader, team.LeaderCharId);
            Assert.Equal(2, team.Members.Count);
            var records = Rpcs(c); Assert.DoesNotContain(records, r => r[0] == 0x80 && r[1] == 0x8d);
            var info = Assert.Single(records, r => r[0] == 0x80 && r[1] == 0x8e);
            Assert.Equal(leader, BitConverter.ToUInt32(info, 12));
        }
        finally { SocialGroups.Instance.RemoveTeamMember(team.Id, member); SocialGroups.Instance.RemoveTeamMember(team.Id, leader); WorldServer.Clients = previousClients; }
    }
}
