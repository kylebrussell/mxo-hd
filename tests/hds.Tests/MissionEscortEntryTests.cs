using hds;
namespace hds.Tests;
public class MissionEscortEntryTests
{
    static MissionProgress Phase()
    {
        var m=new Mission{id=2310,district=1};m.phases.Add(new MissionPhase{site="Normal"});
        var p=new MissionProgress(m){site=new MissionSite{metrId=1,
            elevatorDoor=new(){metrId=1,mxoStaticId=1,pos_x=100,pos_y=2145,pos_z=200},
            lobbyDoor=new(){metrId=1,mxoStaticId=2,pos_x=100,pos_y=145,pos_z=200}}};
        p.waypoints.Add(new(){x=100,y=2145,z=200});p.exits.Add(new[]{100d,145,200});return p;
    }
    [Fact] public void CarryingFollowersAtThePreviousBuildingsLobbyDoesNotCompleteTheNextAreasExitGoal()
    {
        var p=Phase();Assert.False(p.AtExit(10000,95,10000));
        p.entered=true;Assert.True(p.AtExit(10000,95,10000));Assert.False(p.AtExit(100,2095,200));
    }
    [Fact] public void ACommittedDownRideIsEnoughEvenWhenTheCarIsOutsideTheStyleBounds()
    {
        var p=Phase();p.CommitGeneratedFloor(0,p.site,p.site.elevatorDoor);Assert.False(p.AtExit(100,95,200));
        p.CommitGeneratedFloor(0,p.site,p.site.lobbyDoor);Assert.True(p.AtExit(100,95,200));
        p.CommitGeneratedFloor(0,p.site,p.site.elevatorDoor);Assert.False(p.AtExit(100,95,200));
    }
}
