using HeadlessClient;
using hds;
using System.Text.Json;
namespace hds.Tests;

public class DataMinePrefabRotationTests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p; } }
    [Fact]
    public void ExpandedDataKeepsEveryReturnerSourceRowByteIdentical()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/datamine-prefab-door.json")));
        var expected=d.RootElement.GetProperty("preservedReturnerCsvRows").EnumerateArray().Select(x=>x.GetString()).ToArray();
        var actual=File.ReadLines(Path.Combine(Root,"data/staticObjects_datamine.csv")).Skip(1).Where(l=>BitConverter.ToUInt32(Convert.FromHexString(l.Split(',')[4]))==DataMineReturners.GoId).ToArray();
        Assert.Equal(8,actual.Length);Assert.Equal(expected,actual);
        Assert.Equal(157,d.RootElement.GetProperty("changedOldRows").GetInt32());Assert.Equal(0,d.RootElement.GetProperty("changedOldQuaternionRows").GetInt32());
        Assert.Equal(0.0009765625,d.RootElement.GetProperty("maximumOldPositionDelta").GetDouble());
    }
    [Fact]
    public void EveryActualParentAndBothResourceReferencesAreProven()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/datamine-static-audit.json")));
        var n=d.RootElement.GetProperty("nativePrefabRotations");Assert.Equal(7,n.GetProperty("reviewedWorld").GetInt32());
        var placements=n.GetProperty("placements").EnumerateArray().ToArray();Assert.Equal(252,placements.Length);
        Assert.Equal(new[]{125,53,65,9},Enumerable.Range(0,4).Select(r=>placements.Count(p=>p.GetProperty("rotation").GetInt32()==r)));
        Assert.All(placements,p=>{Assert.Equal("direct-sector",p.GetProperty("parent").GetString());Assert.Equal(0,p.GetProperty("rotationUpperWord").GetInt32());Assert.Equal(4,p.GetProperty("resourceIndexFlags").GetInt32());Assert.Equal(6400,p.GetProperty("resourceType").GetInt32());Assert.Equal(4,p.GetProperty("secondaryResourceIndexFlags").GetInt32());Assert.Equal(12544,p.GetProperty("secondaryResourceType").GetInt32());Assert.Equal(64,p.GetProperty("resourceSha256").GetString()!.Length);Assert.Equal(64,p.GetProperty("secondaryResourceSha256").GetString()!.Length);Assert.True(p.GetProperty("resourceSize").GetInt32()>0);Assert.True(p.GetProperty("secondaryResourceSize").GetInt32()>0);});
    }
    [Fact]
    public void RealDoorUsesIndependentQuaternionModelAndNativeFloorWithoutNewAuthority()
    {
        var f=NativeWorldDoorFixtures.DataMinePrefab;Assert.Equal(0x01200015u,f.StaticId);Assert.Equal("00000000f304353f00000000f304353f",f.ExpectedQuaternion);
        var row=Assert.Single(File.ReadLines(Path.Combine(Root,"data/staticObjects_datamine.csv")).Skip(1).Select(l=>l.Split(',')),r=>r[2]=="15002001");
        Assert.Equal(new[]{"-10900.0","3745.0","-3000.0"},row[6..9]);Assert.Equal(f.ExpectedQuaternion,row[10]);
        Assert.Equal(typeof(AttributeClass364),typeof(GameObjectDefinitions).GetField("Object355")!.FieldType);
        ObjectStates.Load(Path.Combine(Root,"data"));Assert.Equal(4004u,ObjectStates.FindUsable(355,3).executeRule);Assert.Equal(0x835u,ObjectStates.DefaultState(355));Assert.Equal(20u,ObjectStates.Find(355,0x834).seconds);Assert.Equal(0x838u,ObjectStates.Find(355,0x834).next);Assert.Equal(1u,ObjectStates.Find(355,0x838).seconds);
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));Assert.True(NavMesh.Locate(7,f.X,f.Y,f.Z,out var at));Assert.Equal(18,at.sector);Assert.Equal(131,at.region);Assert.Equal(224,at.polygon);Assert.Equal(3600,at.floorY);Assert.True(at.indoors);
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/datamine-prefab-door.json")));Assert.Equal("0x280001d1",d.RootElement.GetProperty("modelDescriptionId").GetString());Assert.Equal(891,d.RootElement.GetProperty("modelBytes").GetInt32());Assert.Equal("7160c8744cf539c82fd33b7a11c89e48ea7b7fa506c920966e0a7443c22b44e3",d.RootElement.GetProperty("modelSha256").GetString());
        var plan=NativeQa.NativeQaModes.Find(f.Mode).Plan;Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Contains("y=3695,rotation=0",Assert.Single(plan.AfterLoadout));Assert.Equal("archive_datamine/datamine.metr",f.MetrResource);Assert.Equal("medium03/medium03.metr",NativeWorldDoorFixtures.Medium03Prefab.MetrResource);Assert.Equal("WORLD-MEDIUM03-PREFAB-ROTATIONS.md",NativeWorldDoorFixtures.Medium03Prefab.EvidenceDocument);Assert.Contains(plan.ReadyMessages,m=>m.Contains(f.EvidenceDocument));Assert.Equal("0",Scenarios.All.Single(s=>s.Name==f.Mode).Environment!["MXO_DEV_COMMANDS"]);
    }
}
