using System.Runtime.CompilerServices;
using hds;
using hds.world.Structures;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionTeamProgressTests
{
    static WorldClient Client(uint id)
    {
        var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData=new ClientData();client.playerData.setCharID(id);client.playerData.setDistrictId(1);client.playerData.setOnWorld(true);
        client.LivenessClock=()=>1;client.Alive=true;return client;
    }
    [Fact]
    public void TeamContributionUsesTheActualHoldersActorAndCurrentMembership()
    {
        var prior=WorldServer.Clients;var owner=Client(90001);var helper=Client(90002);var outsider=Client(90003);
        var mission=new Mission{id=399911,district=1};mission.phases.Add(new MissionPhase());
        owner.playerData.activeMission=mission;owner.playerData.missionProgress=new MissionProgress(mission);
        owner.playerData.missionInstanceCounter=47;
        var mob=new Mob{missionInstance=47,missionNpcId=1};mob.setDistrict(1);owner.playerData.missionMobs.Add(mob);
        var groups=SocialGroups.Instance;var team=groups.CreateTeam(90001,"Holder");groups.AddTeamMember(team.Id,90002,"Helper");
        try
        {
            WorldServer.Clients=new(){{"holder",owner},{"helper",helper},{"outsider",outsider}};
            Assert.Same(owner,MissionTeamProgress.Holder(helper,mob));
            Assert.Null(MissionTeamProgress.Holder(outsider,mob));
            var twin=new Mob{missionInstance=47,missionNpcId=1};twin.setDistrict(1);
            Assert.Null(MissionTeamProgress.Holder(helper,twin));
            owner.playerData.setOnWorld(false);Assert.Null(MissionTeamProgress.Holder(helper,mob));owner.playerData.setOnWorld(true);
            helper.playerData.setDistrictId(2);Assert.Null(MissionTeamProgress.Holder(helper,mob));helper.playerData.setDistrictId(1);
            groups.RemoveTeamMember(team.Id,90002);Assert.Null(MissionTeamProgress.Holder(helper,mob));
        }
        finally
        {
            groups.RemoveTeamMember(team.Id,90001);groups.RemoveTeamMember(team.Id,90002);WorldServer.Clients=prior;
        }
    }
}
