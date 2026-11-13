using hds;
using HeadlessClient;
using System.Text.Json;
namespace hds.Tests;
public class Medium02PrefabRotationTests
{
    private static string Root {get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact]
    public void NewRestoredDoorHasLiteralPrimaryPoseModelUsableAndSameFloorNav()
    {
        var f=NativeWorldDoorFixtures.Medium02Prefab;
        Assert.Equal(0x04c00038u,f.StaticId);Assert.Equal("00000000f304353f00000000f204353f",f.ExpectedQuaternion);
        var row=Assert.Single(File.ReadLines(Path.Combine(Root,"data/staticObjects_medium02.csv")).Skip(1).Select(l=>l.Split(',')),r=>r[2]=="3800c004");
        Assert.Equal(new[]{"-6000.0","145.0","17100.0"},row[6..9]);Assert.Equal(f.ExpectedQuaternion,row[10]);
        Assert.Equal(typeof(AttributeClass364),typeof(GameObjectDefinitions).GetField("Object414")!.FieldType);
        ObjectStates.Load(Path.Combine(Root,"data"));Assert.Equal(4004u,ObjectStates.FindUsable(414,3).executeRule);
        Assert.Equal(0x835u,ObjectStates.DefaultState(414));Assert.Equal(20u,ObjectStates.Find(414,0x834).seconds);
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        Assert.True(NavMesh.Locate(15,f.X,f.Y,f.Z,out var nav));Assert.Equal(76,nav.sector);Assert.True(nav.indoors);Assert.Equal(0,nav.floorY);Assert.Equal(24,nav.polygon);
        Assert.False(NavMesh.Locate(15,-6000,95,17000,out _)); // rejected ungrounded convenient point
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/medium02-prefab-door.json")));
        Assert.Equal("0x2800005a",source.RootElement.GetProperty("modelDescriptionId").GetString());
        Assert.EndsWith("sl_push_knob_graymetal01texvar.moa",source.RootElement.GetProperty("modelPath").GetString());
        Assert.Equal(902,source.RootElement.GetProperty("modelBytes").GetInt32());
    }
    [Fact]
    public void OldSafeKeysAndEveryRawConflictRemainAccountedFor()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/medium02-static-audit.json")));var a=d.RootElement;
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();
        var raw=a.GetProperty("raw").EnumerateArray().ToArray();
        var historical=raw.GroupBy(r=>(r[0].GetInt32(),r[1].GetInt32())).Where(g=>g.All(r=>r[5].GetInt32()==0)&&!excluded.Contains(g.Key)).Select(g=>g.Key).ToHashSet();
        Assert.Equal(426,historical.Count);
        var keys=File.ReadLines(Path.Combine(Root,"data/staticObjects_medium02.csv")).Skip(1).Select(l=>l.Split(',')).Select(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3])))).ToHashSet();
        Assert.True(historical.IsSubsetOf(keys));Assert.Equal(571,keys.Count(k=>!historical.Contains(k)));Assert.Empty(keys.Intersect(excluded));
        Assert.Contains((76,56),keys);Assert.DoesNotContain((76,56),historical);
        var placements=a.GetProperty("nativePrefabRotations").GetProperty("placements").EnumerateArray().ToArray();Assert.Equal(25,placements.Length);
        Assert.All(placements,p=>{Assert.Equal("direct-sector",p.GetProperty("parent").GetString());Assert.InRange(p.GetProperty("rotation").GetInt32(),0,3);Assert.Equal(64,p.GetProperty("resourceSha256").GetString()!.Length);});
    }
    [Fact]
    public void NewModeReusesDoorHelperWithoutPrivilegeOrRandomOverride()
    {
        var f=NativeWorldDoorFixtures.Medium02Prefab;var p=NativeQa.NativeQaModes.Find(f.Mode).Plan;
        Assert.Equal(f.X,p.X);Assert.Equal(f.Z,p.Z);Assert.Contains("y=95,rotation=0",Assert.Single(p.AfterLoadout));
        Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Empty(p.EnvironmentOverrides);Assert.Contains(p.ReadyMessages,m=>m.Contains("WORLD-MEDIUM02-PREFAB-ROTATIONS.md"));
        Assert.Equal("00000000f30435b300000000ffff7f3f",NativeWorldDoorFixtures.Medium03Prefab.ExpectedQuaternion);
    }
}
