using HeadlessClient;
namespace hds.Tests;
public class MissionComputerAutomaticExitTests
{
    static RpcRecord Tick()=>new(1,0,Convert.FromHexString("80A001060001"));
    static RpcRecord End()=>new(2,0,Convert.FromHexString("80A20000000000000000000000000000000000"));
    [Fact] public void OldCompletionCannotSatisfyCurrentTakeAndCurrentCompletionMustFollowItsTick()
    {
        var rows=new List<RpcRecord>{Tick(),End()};int cursor=rows.Count;
        Assert.Null(MissionComputer1407Fixture.AutomaticExitSince(rows,cursor));
        rows.Add(Tick());Assert.Null(MissionComputer1407Fixture.AutomaticExitSince(rows,cursor));
        rows.Add(End());Assert.Same(rows[^1].Bytes,MissionComputer1407Fixture.AutomaticExitSince(rows,cursor));
        rows.Add(End());Assert.Null(MissionComputer1407Fixture.AutomaticExitSince(rows,cursor));
        Assert.Null(MissionComputer1407Fixture.AutomaticExitSince(new[]{End(),Tick()},0));
        Assert.Null(MissionComputer1407Fixture.AutomaticExitSince(rows,-1));
    }
}
