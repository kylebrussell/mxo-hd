using hds;
using System.Text.Json;
namespace hds.Tests;
public class Large01Tests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void EveryVariantOfRotatedOrAmbiguousNativeKeysIsAbsent()
    {
        using var audit=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","large01-static-audit.json")));
        var a=audit.RootElement;
        Assert.Equal(1025,a.GetProperty("rawReferences").GetInt32());Assert.Equal(1001,a.GetProperty("uniqueKeys").GetInt32());
        Assert.Equal(953,a.GetProperty("exportedKeys").GetInt32());Assert.Equal(20,a.GetProperty("rotationAmbiguityOverlap").GetInt32());
        Assert.Equal(46,a.GetProperty("unprovenRotationKeys").GetArrayLength());Assert.Equal(22,a.GetProperty("conflicts").EnumerateObject().Count());
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();
        Assert.Equal(48,excluded.Count);
        foreach(var conflict in a.GetProperty("conflicts").EnumerateObject())
            foreach(var variant in conflict.Value.EnumerateArray())
                Assert.Contains((variant[0].GetInt32(),variant[1].GetInt32()),excluded);
        var rows=File.ReadLines(Path.Combine(Root,"data","staticObjects_large01.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(953,rows.Length);Assert.Equal(953,rows.Select(r=>r[2]).Distinct().Count());
        var keys=rows.Select(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3])))).ToHashSet();
        Assert.Empty(keys.Intersect(excluded));Assert.All(rows,r=>Assert.Equal("12",r[0]));
        foreach(var raw in a.GetProperty("raw").EnumerateArray())
            if(raw[5].GetInt32()!=0)Assert.Contains((raw[0].GetInt32(),raw[1].GetInt32()),excluded);
        var booth=Assert.Single(rows,r=>r[4]=="50020000");
        Assert.Equal("07002003",booth[2]);Assert.Equal(new[]{"13000.0","-1252.0","-7500.0"},booth[6..9]);
    }
    [Fact]
    public void ExactShippedHardlineAndExistingUnrotatedFloorsHaveNavigation()
    {
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(HardlineNetwork.Covers(12));Assert.True(HardlineNetwork.Covers(14)); Assert.False(HardlineNetwork.Covers(99));Assert.True(HardlineNetwork.Covers(17));
        Assert.Equal(new ushort[]{1},HardlineNetwork.Ids(12));var h=HardlineNetwork.Get(12,1);
        Assert.Equal(13000,h.spotX);Assert.Equal(-1302,h.spotY);Assert.Equal(-7400,h.spotZ);
        Assert.True(NavMesh.Locate(12,h.spotX,h.spotY,h.spotZ,out var arrival));Assert.Equal(50,arrival.sector);Assert.False(arrival.indoors);
        Assert.True(NavMesh.Locate(12,12900,95,15500,out var door));Assert.Equal(136,door.sector);Assert.True(door.indoors);
        // Existing capture lies in the footprint but its recorded Y has no matching nav floor.
        Assert.False(NavMesh.Locate(12,7713,-705,53769,out _));
        Assert.True(NavMesh.Locate(12,7713,-105,53769,out _));
        Assert.NotNull(SectorProperties.At(12,7713,53769));
    }
    [Fact]
    public void ModeDoesNotGrantDevelopmentOrRandomPrivileges()
    {
        var plan=new NativeQa.Modes.Large01Mode().Plan;
        Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Contains("districtId=12",Assert.Single(plan.AfterLoadout));
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor("large01");
        Assert.Equal("",env["MXO_MOBS"]);
        Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);
    }
}
