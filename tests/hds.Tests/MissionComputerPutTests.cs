using hds;
namespace hds.Tests;
public class MissionComputerPutTests
{
    [Fact]
    public void SearchThenPutOpensTheActualTakerStateWithoutChangingOtherComputerGoals()
    {
        var search=new MissionObjective{goal=1,type=MissionGoalType.Search,objectId=1,item=6786};
        var put=new MissionObjective{goal=2,type=MissionGoalType.Put,objectId=1,item=6807,afterGoal=1};
        var phase=new MissionPhase();phase.missionObjectives.AddRange(new[]{search,put});
        var mission=new Mission();mission.phases.Add(phase);var progress=new MissionProgress(mission);
        var state=new MissionObjectState{def=new MissionObjectDef{id=1,kind="Computer"},goid=3};
        Assert.Equal(MissionStaging.ComputerTake,MissionStaging.ComputerStateFor(progress,state));
        progress.Complete(search);
        Assert.Equal(MissionStaging.ComputerPut,MissionStaging.ComputerStateFor(progress,state));
        Assert.Equal((byte)8,ObjectStates.UseNumber(3,"Put"));
        Assert.Contains((byte)8,ObjectStates.Offered(3,MissionStaging.ComputerPut));
        Assert.DoesNotContain((byte)4,ObjectStates.Offered(3,MissionStaging.ComputerPut));
        progress.Complete(put);Assert.Equal(MissionStaging.ComputerIdle,MissionStaging.ComputerStateFor(progress,state));
    }
    [Theory]
    [InlineData(MissionGoalType.Search,0,0x963u)] [InlineData(MissionGoalType.Use,0,0x963u)]
    [InlineData(MissionGoalType.Search,6786,0x961u)] [InlineData(MissionGoalType.Put,6807,0x962u)]
    public void SingleGoalUsesItsSourcedNativeState(MissionGoalType type,int item,uint expected)
    {
        var phase=new MissionPhase();phase.missionObjectives.Add(new MissionObjective{goal=1,type=type,objectId=1,item=(uint)item});
        var mission=new Mission();mission.phases.Add(phase);
        Assert.Equal(expected,MissionStaging.ComputerStateFor(new MissionProgress(mission),new MissionObjectState{def=new MissionObjectDef{id=1}}));
    }
}
