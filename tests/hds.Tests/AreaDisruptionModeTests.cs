using hds;
using NativeQa;
using HeadlessClient;
using System.Globalization;
namespace hds.Tests;
[Collection("DelayedActions")]
public class AreaDisruptionModeTests
{
    [Theory][InlineData(true)][InlineData(false)]
    public void ModeOwns38AndOnlyForcesExistingPerRecipientContest(bool hit)
    {
        var mode="area-disruption-"+(hit?"hit":"deflect");var p=NativeQaModes.Find(mode).Plan;
        Assert.Contains((ushort)38,p.Abilities);Assert.DoesNotContain((ushort)97,p.Abilities);Assert.DoesNotContain((ushort)345,p.Abilities);
        Assert.True(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        var e=NativeQaFixturePlan.EnvironmentFor(mode);Assert.Equal(hit?"32766":"0",e["MXO_FREECAST_VIRAL_QA_ATTACK15"]);Assert.Equal(hit?"0":"32766",e["MXO_FREECAST_VIRAL_QA_DEFENSE15"]);
        Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);
        Assert.Equal(AreaDisruptionFixture.HelpersSql(6111,6112,6113,6114),p.AfterHelpers[1]);Assert.NotNull(Scenarios.All.Single(s=>s.Name==mode));
    }
    [Fact]public void SourceNpcInventoryProvesPlayerFixtureIsolationAndGroundedBoundaries()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"data","sector_properties.csv")))root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        foreach(int x in new[]{17000,17100,17200,17800,17801}){Assert.True(NavMesh.Locate(1,x,495,2300,out var p));Assert.Equal(400,p.floorY);}
        var rows=File.ReadLines(Path.Combine(root,"data","mob_just_some_and_winter.csv")).Skip(1).Select(l=>l.Split(';')).Where(r=>r.Length>9&&r[0]=="1").ToArray();Assert.NotEmpty(rows);
        foreach(var r in rows)Assert.False(AbilityTargets.WithinArea(17000,495,2300,double.Parse(r[7],CultureInfo.InvariantCulture),double.Parse(r[8],CultureInfo.InvariantCulture),double.Parse(r[9],CultureInfo.InvariantCulture),8),"Unexpected sourceNPC inside fixture: "+r[2]);
        Assert.False(AbilityTargets.WithinArea(17000,495,2300,17100,695,2300,8));
    }
}
