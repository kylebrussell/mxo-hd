using hds;
using System.Text.Json;
namespace hds.Tests;
public class WidowsMoorTests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void EveryKnownNativeKeyIsUniqueAndHasNoUnprovenPlacement()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/widowsmoor-static-audit.json")));
        var a=d.RootElement;Assert.Equal(70,a.GetProperty("rawReferences").GetInt32());Assert.Equal(70,a.GetProperty("uniqueKeys").GetInt32());Assert.Equal(70,a.GetProperty("exportedKeys").GetInt32());
        Assert.Empty(a.GetProperty("excludedKeys").EnumerateArray());Assert.Empty(a.GetProperty("conflicts").EnumerateObject());Assert.Equal(258,a.GetProperty("sourceSha256").EnumerateObject().Count());
        Assert.All(a.GetProperty("raw").EnumerateArray(),r=>Assert.Equal(0,r[5].GetInt32()));
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_widowsmoor.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(70,rows.Length);Assert.Equal(70,rows.Select(r=>r[2]).Distinct().Count());Assert.All(rows,r=>Assert.Equal("10",r[0]));
        Assert.Equal(new uint[]{587,2506,7342,38271,38272,38273,38488,38489},rows.Select(r=>BitConverter.ToUInt32(Convert.FromHexString(r[4]))).Distinct().Order().ToArray());
        Assert.DoesNotContain(rows,r=>r[4]=="50020000");
        var source=Assert.Single(rows,r=>r[4]=="7f950000");Assert.Equal(new[]{"-17550.0","145.0","-5500.0"},source[6..9]);
    }
    [Fact]
    public void ExistingArrivalIsProjectedAndCapturedNpcsAreNotRelocated()
    {
        NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));HardlineNetwork.Load(Path.Combine(Root,"data/hardline_network.csv"));
        Assert.False(HardlineNetwork.Covers(10));Assert.Equal(13u,PvpArchives.ConstructId(10));
        Assert.True(PvpArchives.Arrival(10,1,out var p));Assert.Equal(95,p.y);Assert.Equal(54,p.location.sector);
        Assert.True(NavMesh.Locate(10,p.x,p.y,p.z,out _));Assert.Equal(-18500,p.x);Assert.Equal(-7500,p.z);
        // These are the exact captured coordinates, not invented substitutes on a nearby polygon.
        var rows=File.ReadLines(Path.Combine(Root,"data/npc_spawns/Instance.csv")).Skip(1).Select(l=>l.Split(';')).Where(r=>r[0]=="10").ToArray();
        Assert.Equal(17,rows.Length);
        foreach(var r in rows)Assert.False(NavMesh.Locate(10,double.Parse(r[7]),double.Parse(r[8]),double.Parse(r[9]),out _));
    }
    [Fact]
    public void ModeUsesLevelFortyBookWithoutPrivilegeOrForcedRolls()
    {
        var p=new NativeQa.Modes.WidowsMoorMode().Plan;Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Equal(40,p.ExtraQaMetadata["FixtureLevel"].GetInt32());Assert.Contains("38536,3,1",Assert.Single(p.AfterLoadout));
        var final=Assert.Single(p.AfterHelpers);Assert.Contains("level=40",final);Assert.Contains($"exp={PlayerProgression.ExperienceForLevel(40)}",final);Assert.Contains("LEAST(level,40)",final);
        var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("widowsmoor");Assert.Equal("0",e["MXO_DEV_COMMANDS"]);Assert.Equal("",e["MXO_GM_CHARACTER_IDS"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);
    }
}
