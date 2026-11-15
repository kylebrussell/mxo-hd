using hds;
using System.Text.Json;
namespace hds.Tests;
public class Medium02Tests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void NativeRotationsRetainHistoricalKeysAndOmitEveryAmbiguousVariant()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","medium02-static-audit.json")));
        var a=d.RootElement;Assert.Equal(1061,a.GetProperty("rawReferences").GetInt32());Assert.Equal(1023,a.GetProperty("uniqueKeys").GetInt32());
        Assert.Equal(997,a.GetProperty("exportedKeys").GetInt32());Assert.Equal(584,a.GetProperty("historicallyUnprovenRotationKeys").GetArrayLength());
        Assert.Equal(26,a.GetProperty("conflicts").EnumerateObject().Count());
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();Assert.Equal(26,excluded.Count);
        foreach(var c in a.GetProperty("conflicts").EnumerateObject())foreach(var v in c.Value.EnumerateArray())Assert.Contains((v[0].GetInt32(),v[1].GetInt32()),excluded);
        var rows=File.ReadLines(Path.Combine(Root,"data","staticObjects_medium02.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(997,rows.Length);Assert.Equal(997,rows.Select(r=>r[2]).Distinct().Count());Assert.All(rows,r=>Assert.Equal("15",r[0]));
        Assert.Empty(rows.Select(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3])))).Intersect(excluded));
        var booth=Assert.Single(rows,r=>r[4]=="50020000");Assert.Equal("07008003",booth[2]);Assert.Equal(new[]{"-1000.0","145.0","2400.0"},booth[6..9]);
    }
    [Fact]
    public void ShippedArrivalAndSupportedNativeDoorHaveGroundedNavigation()
    {
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(HardlineNetwork.Covers(15));Assert.False(HardlineNetwork.Covers(99));Assert.Equal(new ushort[]{1},HardlineNetwork.Ids(15));
        var h=HardlineNetwork.Get(15,1);Assert.Equal(-1000,h.spotX);Assert.Equal(95,h.spotY);Assert.Equal(2500,h.spotZ);
        Assert.True(NavMesh.Locate(15,h.spotX,h.spotY,h.spotZ,out var arrival));Assert.Equal(56,arrival.sector);
        Assert.True(NavMesh.Locate(15,-5700,95,14900,out var door));Assert.Equal(76,door.sector);Assert.True(door.indoors);
        Assert.Equal(typeof(AttributeClass364),typeof(GameObjectDefinitions).GetField("Object414")!.FieldType);
        Assert.Equal((byte?)3,ObjectStates.UseNumber(414,"Open"));Assert.Contains((byte)3,ObjectStates.Offered(414,DoorLifecycle.StateClosed));
        Assert.Equal(DoorLifecycle.StateOpen,ObjectStates.StateAfterUse(414,DoorLifecycle.StateClosed,3));
    }
    [Fact]
    public void CapturedPopulationRemainsUnvalidatedWithoutRelocationOrVisibilityClaim()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var rows=File.ReadLines(Path.Combine(Root,"data","npc_spawns","Instance.csv")).Skip(1).Select(l=>l.Split(';')).Where(r=>r[0]=="15").ToArray();
        Assert.Equal(54,rows.Length);
        var inside=rows.Where(r=>SectorProperties.At(15,double.Parse(r[7]),double.Parse(r[9]))!=null).ToArray();Assert.Equal(23,inside.Length);
        Assert.All(inside,r=>Assert.False(NavMesh.Locate(15,double.Parse(r[7]),double.Parse(r[8]),double.Parse(r[9]),out _)));
        using var a=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","medium02-static-audit.json")));
        Assert.False(a.RootElement.GetProperty("capturedPopulationValidated").GetBoolean());
    }
    [Fact]
    public void ModeGrantsNoDevelopmentOrRandomPrivileges()
    {
        var p=new NativeQa.Modes.Medium02Mode().Plan;Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Empty(p.EnvironmentOverrides);
        Assert.Contains("districtId=15",Assert.Single(p.AfterLoadout));var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("medium02");
        Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);
    }
}
