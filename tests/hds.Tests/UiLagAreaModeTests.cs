using hds;
using NativeQa;
using HeadlessClient;
namespace hds.Tests;
[Collection("DelayedActions")]
public class UiLagAreaModeTests
{
    [Theory] [InlineData(true)] [InlineData(false)]
    public void ProfilesAreDiscoveredAndOnlyForceTheExistingContest(bool hit)
    {
        string name="ui-lag-area-"+(hit?"hit":"deflect");var plan=NativeQaModes.Find(name).Plan;
        Assert.Contains((ushort)97,plan.Abilities);Assert.DoesNotContain((ushort)345,plan.Abilities);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        var e=NativeQaFixturePlan.EnvironmentFor(name);Assert.Equal("1",e["MXO_PVP_SERVER"]);Assert.Equal("15",e["MXO_PVP_MAX_SAFE_LEVEL"]);
        Assert.Equal(hit?"32766":"0",e["MXO_FREECAST_VIRAL_QA_ATTACK15"]);Assert.Equal(hit?"0":"32766",e["MXO_FREECAST_VIRAL_QA_DEFENSE15"]);
        Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);
        Assert.Equal(UiLagAreaFixture.HelpersSql(6111,6112,6113,6114),plan.AfterHelpers[1]);
        Assert.Equal("",NativeQaFixturePlan.EnvironmentFor("")["MXO_FREECAST_VIRAL_QA"]);
        Assert.NotNull(Scenarios.All.Single(s=>s.Name==name));
    }
    [Fact] public void SameFloorNativePositionsHaveShippedNavAndExplicitUnwalkedFloorControl()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"data","sector_properties.csv")))root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        foreach(int x in new[]{17000,17100,17200,17301})
        {Assert.True(NavMesh.Locate(1,x,495,2300,out var p));Assert.Equal(915,p.sector);Assert.Equal(400,p.floorY);}
        Assert.False(AbilityTargets.WithinArea(17000,495,2300,17100,695,2300,3));
    }
}
