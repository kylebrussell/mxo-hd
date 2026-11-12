using hds;
using System.Globalization;
using System.Text.Json;
namespace hds.Tests;
public class YukiTests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void EveryKnownVariantInMissingInteriorSectorIsOmittedWithoutClaimingCompleteness()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/yuki-static-audit.json")));
        var a=d.RootElement;Assert.Equal(272,a.GetProperty("rawReferences").GetInt32());Assert.Equal(272,a.GetProperty("uniqueKeys").GetInt32());Assert.Equal(246,a.GetProperty("exportedKeys").GetInt32());
        Assert.False(a.GetProperty("rawReferencesAreComplete").GetBoolean());Assert.True(a.GetProperty("unquantifiedMissingStaticData").GetBoolean());Assert.False(a.GetProperty("navigationCompletenessProven").GetBoolean());
        Assert.Equal(146,a.GetProperty("sourceSha256").EnumerateObject().Count());
        var m=Assert.Single(a.GetProperty("missingStaticLayers").EnumerateArray());Assert.Equal(5,m.GetProperty("sector").GetInt32());Assert.Equal("SIDI",m.GetProperty("kind").GetString());Assert.Equal(0x0c00a800,m.GetProperty("declaredResourceId").GetInt32());
        Assert.False(m.GetProperty("packedRidPresent").GetBoolean());Assert.False(m.GetProperty("packedPathPresent").GetBoolean());Assert.False(m.GetProperty("loosePathPresent").GetBoolean());
        var omitted=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();Assert.Equal(26,omitted.Count);
        foreach(var r in a.GetProperty("raw").EnumerateArray().Where(r=>r[0].GetInt32()==5))Assert.Contains((5,r[1].GetInt32()),omitted);
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_yuki.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();Assert.Equal(246,rows.Length);Assert.All(rows,r=>Assert.Equal("11",r[0]));Assert.DoesNotContain(rows,r=>r[1]=="5");
        // Classify every source GOID, rather than testing an incomplete list of familiar door IDs.
        var contracts=a.GetProperty("nativeGoidContracts");Assert.Equal(272,contracts.EnumerateObject().Sum(c=>c.Value.GetProperty("placements").GetInt32()));
        foreach(var c in contracts.EnumerateObject())foreach(var u in c.Value.GetProperty("usables").EnumerateArray())
        { Assert.Equal("292",u.GetProperty("class").GetString());Assert.Equal("2",u.GetProperty("use").GetString());Assert.Equal("9007 TeleporterOrgCheck",u.GetProperty("execute_rule").GetString()); }
    }
    [Fact]
    public void ExistingActualProjectedArrivalIsGroundedAndAllCapturesRemainOutsideWorld()
    {
        NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));HardlineNetwork.Load(Path.Combine(Root,"data/hardline_network.csv"));
        Assert.False(HardlineNetwork.Covers(11));Assert.Equal(14u,PvpArchives.ConstructId(11));Assert.Equal((ushort)11,PvpArchives.MapFor(4));
        Assert.True(PvpArchives.Arrival(11,1,out var p));Assert.Equal(15500,p.x);Assert.Equal(295,p.y);Assert.Equal(15500,p.z);Assert.Equal(27,p.location.sector);
        Assert.True(NavMesh.Locate(11,p.x,p.y,p.z,out _));
        var rows=File.ReadLines(Path.Combine(Root,"data/npc_spawns/Instance.csv")).Skip(1).Select(l=>l.Split(';')).Where(r=>r[0]=="11").ToArray();Assert.Equal(179,rows.Length);
        foreach(var r in rows)Assert.False(NavMesh.Locate(11,double.Parse(r[7],CultureInfo.InvariantCulture),double.Parse(r[8],CultureInfo.InvariantCulture),double.Parse(r[9],CultureInfo.InvariantCulture),out _));
    }
    [Fact]
    public void ModeUsesActualLevelTwentyAndSafeHeadingAfterGenericXpStage()
    {
        var p=new NativeQa.Modes.YukiMode().Plan;Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Equal(20,p.ExtraQaMetadata["FixtureLevel"].GetInt32());
        Assert.Contains("38535,3,1",Assert.Single(p.AfterLoadout));Assert.Contains("rotation=0",Assert.Single(p.AfterLoadout));
        var final=Assert.Single(p.AfterHelpers);Assert.Contains("level=20",final);Assert.Contains($"exp={PlayerProgression.ExperienceForLevel(20)}",final);Assert.Contains("LEAST(level,20)",final);
        var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("yuki");Assert.Equal("0",e["MXO_DEV_COMMANDS"]);Assert.Equal("",e["MXO_GM_CHARACTER_IDS"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);
    }
}
