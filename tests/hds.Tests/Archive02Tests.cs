using hds;
using System.Text.Json;
namespace hds.Tests;
public class Archive02Tests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void MissingExteriorSectorIsExplicitAndEveryKnownVariantIsOmitted()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/archive02-static-audit.json")));
        var a=doc.RootElement;
        Assert.Equal(80,a.GetProperty("rawReferences").GetInt32());Assert.Equal(77,a.GetProperty("exportedKeys").GetInt32());
        Assert.False(a.GetProperty("rawReferencesAreComplete").GetBoolean());Assert.True(a.GetProperty("unquantifiedMissingStaticData").GetBoolean());
        Assert.Equal(326,a.GetProperty("sourceSha256").EnumerateObject().Count());
        var m=Assert.Single(a.GetProperty("missingStaticLayers").EnumerateArray());Assert.Equal(108,m.GetProperty("sector").GetInt32());
        Assert.Equal(0x0c00b400,m.GetProperty("declaredResourceId").GetInt32());
        Assert.False(m.GetProperty("packedRidPresent").GetBoolean());Assert.False(m.GetProperty("packedPathPresent").GetBoolean());Assert.False(m.GetProperty("loosePathPresent").GetBoolean());
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();
        Assert.Equal(3,excluded.Count);
        foreach(var v in a.GetProperty("raw").EnumerateArray().Where(v=>v[0].GetInt32()==108))Assert.Contains((108,v[1].GetInt32()),excluded);
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_archive02.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(77,rows.Length);Assert.All(rows,r=>Assert.Equal("5",r[0]));Assert.DoesNotContain(rows,r=>r[1]=="108");
    }
    [Fact]
    public void ActualArchiveRouteUsesBookAndNavigationWithoutInventingHardlines()
    {
        NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        HardlineNetwork.Load(Path.Combine(Root,"data/hardline_network.csv"));
        Assert.False(HardlineNetwork.Covers(5));Assert.Equal(16u,PvpArchives.ConstructId(5));
        Assert.True(PvpArchives.Arrival(5,1,out var p));Assert.Equal(-105,p.y);
        Assert.True(NavMesh.Locate(5,p.x,p.y,p.z,out var nav));Assert.Equal(108,nav.sector);
    }
    [Fact]
    public void LevelThirtyIsAppliedAfterDefaultXpAndToClonedLoadouts()
    {
        var p=new NativeQa.Modes.Archive02Mode().Plan;
        Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Equal(30,p.ExtraQaMetadata["FixtureLevel"].GetInt32());
        var final=Assert.Single(p.AfterHelpers);Assert.Contains($"exp={PlayerProgression.ExperienceForLevel(30)}",final);
        Assert.Contains("level=30",final);Assert.Contains("level=LEAST(level,30)",final);Assert.Contains("110,6111,6112,6113,6114",final);
        Assert.Contains("38533,3,1",Assert.Single(p.AfterLoadout));
        var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("archive02");Assert.Equal("0",e["MXO_DEV_COMMANDS"]);Assert.Equal("",e["MXO_GM_CHARACTER_IDS"]);
        Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);
    }
}
