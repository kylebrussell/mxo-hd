using hds;
using System.Text.Json;
namespace hds.Tests;
public class Large02Tests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void EveryAmbiguousVariantRemainsExcludedAfterReviewedRotationRollout()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/large02-static-audit.json")));
        var a=doc.RootElement;
        Assert.Equal(2164,a.GetProperty("rawReferences").GetInt32());Assert.Equal(2096,a.GetProperty("uniqueKeys").GetInt32());
        Assert.Equal(2052,a.GetProperty("exportedKeys").GetInt32());Assert.Equal(44,a.GetProperty("conflicts").EnumerateObject().Count());
        var historical=a.GetProperty("historicallyUnprovenRotationKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();
        Assert.Equal(442,historical.Count);
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();
        Assert.Equal(44,excluded.Count);
        foreach(var c in a.GetProperty("conflicts").EnumerateObject())foreach(var v in c.Value.EnumerateArray())Assert.Contains((v[0].GetInt32(),v[1].GetInt32()),excluded);
        var rawKeys=a.GetProperty("raw").EnumerateArray().Select(v=>(v[0].GetInt32(),v[1].GetInt32())).ToHashSet();
        var oldSafe=rawKeys.Except(historical.Union(excluded)).ToHashSet();Assert.Equal(1619,oldSafe.Count);
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_large02.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(2052,rows.Length);Assert.Equal(2052,rows.Select(r=>r[2]).Distinct().Count());Assert.All(rows,r=>Assert.Equal("13",r[0]));
        var keys=rows.Select(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3])))).ToHashSet();
        Assert.Empty(keys.Intersect(excluded));Assert.True(oldSafe.IsSubsetOf(keys));Assert.True(keys.SetEquals(rawKeys.Except(excluded)));
        Assert.Equal(433,keys.Count(historical.Contains));
        var booth=Assert.Single(rows,r=>r[4]=="50020000");Assert.Equal("0300f00c",booth[2]);
        Assert.Equal(new[]{"-7600.0","145.0","-2000.0"},booth[6..9]);Assert.DoesNotContain(rows,r=>r[1]=="47");
    }
    [Fact]
    public void ShippedHardlineAndDoorNavigateButUnavailableSectorIsNotFabricated()
    {
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(HardlineNetwork.Covers(13));Assert.True(HardlineNetwork.Covers(14)); Assert.False(HardlineNetwork.Covers(99));
        Assert.Equal(new ushort[]{1},HardlineNetwork.Ids(13));var h=HardlineNetwork.Get(13,1);
        Assert.Equal(-7500,h.spotX);Assert.Equal(95,h.spotY);Assert.Equal(-2000,h.spotZ);
        Assert.True(NavMesh.Locate(13,h.spotX,h.spotY,h.spotZ,out var arrival));Assert.Equal(207,arrival.sector);
        Assert.True(NavMesh.Locate(13,12300,95,-4100,out var door));Assert.Equal(27,door.sector);Assert.True(door.indoors);
        Assert.Equal(47,SectorProperties.At(13,34500,5000)!.sector);Assert.False(NavMesh.Locate(13,34500,95,5000,out _));
        using var a=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","large02-static-audit.json")));
        var missing=Assert.Single(a.RootElement.GetProperty("missingNavigationResources").EnumerateArray());
        Assert.Equal(47,missing.GetProperty("sector").GetInt32());Assert.Equal("1",missing.GetProperty("enableNavigationMesh").GetString());
    }
    [Fact]
    public void ModeHasNoPrivilegeOrEncounterPopulationOverride()
    {
        var p=new NativeQa.Modes.Large02Mode().Plan;Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Empty(p.EnvironmentOverrides);Assert.Contains("districtId=13",Assert.Single(p.AfterLoadout));
        Assert.False(NpcSpawnScope.LoadAmbient(NpcSpawnScope.Scope.Encounter));
        var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("large02");Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);
    }
}
