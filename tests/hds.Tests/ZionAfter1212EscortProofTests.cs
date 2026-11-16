using hds;
using HeadlessClient;

namespace hds.Tests;

public class ZionAfter1212EscortProofTests
{
    private static Mission Authored()
    {
        var mission=new Mission{id=101215};
        var phase=new MissionPhase();
        phase.missionObjectives.Add(new(){goal=3,type=MissionGoalType.Escort,npcId=1,escortTo="exit",afterGoal=2});
        mission.phases.Add(phase);mission.phases.Add(new());mission.phases.Add(new());
        return mission;
    }
    private static MissionProgress.SavedState Before()=>new()
    {
        Mission=101215,Phase=0,Identity="fissure-mission",Started=123,Done=new[]{1,2},Following=new ushort[]{1},
        Flags=new(){{"entered",true},{"failed",false}},
        Mobs=new(){new(){Npc=1,Name="Fissure",Friendly=true,Health=50,Dead=false}}
    };
    private static MissionProgress.SavedState Next()=>new()
    {Mission=101215,Phase=1,Identity="fissure-mission",Started=123,EverDone=new[]{1,2,3,4}};

    [Theory]
    [InlineData("valid")][InlineData("old-ack")][InlineData("other-goal")][InlineData("pending-ack")]
    [InlineData("identity")][InlineData("start")][InlineData("mission")][InlineData("no-commit")]
    [InlineData("skipped-phase")][InlineData("no-history")][InlineData("not-exit")][InlineData("prerequisite")]
    public void FissureAdvanceRequiresFreshGoalAndExactOwnedPersistedNextPhase(string change)
    {
        var before=Before();var current=Next();var mission=Authored();int cursor=0;
        // Literal goal acknowledgement from the retained failing normal-wire run.
        var records=new[]{Convert.FromHexString("80A00306000121004C6561642046697373757265206F7574206F6620746865206275696C64696E6700")};
        switch(change)
        {
            case "old-ack":cursor=1;break;
            case "other-goal":records[0][2]=4;break;
            case "pending-ack":records[0][5]=0;break;
            case "identity":current.Identity="different";break;
            case "start":current.Started++;break;
            case "mission":current.Mission++;break;
            case "no-commit":current.Phase=0;break;
            case "skipped-phase":current.Phase=2;break;
            case "no-history":current.EverDone=new[]{1,2};break;
            case "not-exit":mission.phases[0].missionObjectives[0].escortTo="object:1";break;
            case "prerequisite":before.Done=new[]{1};break;
        }
        Assert.Equal(change=="valid",ZionAfter1212EscortProof.Advanced(before,current,mission,3,records,cursor));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,null,mission,3,records,cursor));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,current,mission,3,records,-1));
    }

    [Theory]
    [InlineData("valid")][InlineData("dead")][InlineData("unfollowed")][InlineData("entered-flag-cleared")]
    [InlineData("failed")][InlineData("already-done")][InlineData("wrong-mission")]
    public void DoorApproachRequiresActualCurrentEscortCapability(string change)
    {
        var state=Before();var mission=Authored();
        switch(change)
        {
            case "dead":state.Mobs[0].Dead=true;break;
            case "unfollowed":state.Following=Array.Empty<ushort>();break;
            case "entered-flag-cleared":state.Flags["entered"]=false;break;
            case "failed":state.Flags["failed"]=true;break;
            case "already-done":state.Done=new[]{1,2,3};break;
            case "wrong-mission":state.Mission++;break;
        }
        // The persisted entered flag is not a position or current-view witness.
        // Master permits this observation during ordinary car transitions; movement
        // remains separately fenced by current-life and saved route/door evidence.
        Assert.Equal(change=="valid"||change=="entered-flag-cleared",ZionAfter1212EscortProof.CanApproach(state,mission,3));
    }
}
