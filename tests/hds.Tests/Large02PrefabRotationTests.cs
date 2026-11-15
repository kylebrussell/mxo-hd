using HeadlessClient;
using hds;
using System.Text.Json;
namespace hds.Tests;

public class Large02PrefabRotationTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact]
    public void RestoredDoorUsesIndependentLiteralQuaternionAndSourceStandingPoint()
    {
        var f=NativeWorldDoorFixtures.Large02Prefab;
        Assert.Equal(13,f.World);Assert.Equal(414,f.Goid);Assert.Equal(27,f.Sector);Assert.Equal(572,f.LocalId);
        Assert.Equal("00000000f304353f00000000f204353f",f.ExpectedQuaternion);
        var row=Assert.Single(File.ReadLines(Path.Combine(Root,"data/staticObjects_large02.csv")).Skip(1).Select(l=>l.Split(',')),r=>r[2]=="3c02b001");
        Assert.Equal(new[]{"17000.0","145.0","7100.0"},row[6..9]);Assert.Equal(f.ExpectedQuaternion,row[10]);
        Assert.Equal(typeof(AttributeClass364),typeof(GameObjectDefinitions).GetField("Object414")!.FieldType);
        ObjectStates.Load(Path.Combine(Root,"data"));Assert.Equal(4004u,ObjectStates.FindUsable(414,3).executeRule);
        Assert.Equal(0x835u,ObjectStates.DefaultState(414));Assert.Equal(20u,ObjectStates.Find(414,0x834).seconds);
        Assert.Equal(0x838u,ObjectStates.Find(414,0x834).next);Assert.Equal(1u,ObjectStates.Find(414,0x838).seconds);
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        Assert.True(NavMesh.Locate(13,f.X,f.Y,f.Z,out var n));Assert.Equal(27,n.sector);Assert.Equal(88,n.region);Assert.Equal(24,n.polygon);Assert.Equal(0,n.floorY);Assert.True(n.indoors);
        Assert.False(NavMesh.Locate(13,17000,95,7000,out _));
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/large02-prefab-door.json")));
        var d=source.RootElement;Assert.Equal("0x2800005a",d.GetProperty("modelDescriptionId").GetString());
        Assert.EndsWith("sl_push_knob_graymetal01texvar.moa",d.GetProperty("modelPath").GetString());Assert.Equal(902,d.GetProperty("modelBytes").GetInt32());
        Assert.Equal("1a23438fafd7cf1ca1d4a785bef908689f3c4fc6c8a69534066bd75e31fdc559",d.GetProperty("modelSha256").GetString());
        Assert.True(d.GetProperty("nativeRenderingPending").GetBoolean());
    }
    [Fact]
    public void EveryActualPlacementRetainsDirectParentAndBothReadableResourceProofs()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/large02-static-audit.json")));
        var a=doc.RootElement;var native=a.GetProperty("nativePrefabRotations");Assert.Equal(13,native.GetProperty("reviewedWorld").GetInt32());
        var placements=native.GetProperty("placements").EnumerateArray().ToArray();Assert.Equal(34,placements.Length);
        Assert.Equal(20,placements.Count(p=>p.GetProperty("rotation").GetInt32()==0));Assert.Equal(13,placements.Count(p=>p.GetProperty("rotation").GetInt32()==1));Assert.Single(placements,p=>p.GetProperty("rotation").GetInt32()==2);
        Assert.All(placements,p=>
        {
            Assert.Equal("direct-sector",p.GetProperty("parent").GetString());Assert.Equal(0,p.GetProperty("rotationUpperWord").GetInt32());
            Assert.Equal(4,p.GetProperty("resourceIndexFlags").GetInt32());Assert.Contains(p.GetProperty("resourceType").GetInt32(),new[]{6400,137472});
            Assert.Equal(12544,p.GetProperty("secondaryResourceType").GetInt32());Assert.Equal(4,p.GetProperty("secondaryResourceIndexFlags").GetInt32());
            Assert.Contains(p.GetProperty("opaqueMetadataU16").GetInt32(),new[]{6,80,81});
            Assert.Equal(64,p.GetProperty("resourceSha256").GetString()!.Length);Assert.Equal(64,p.GetProperty("secondaryResourceSha256").GetString()!.Length);
            Assert.True(p.GetProperty("resourceSize").GetInt32()>0);Assert.True(p.GetProperty("secondaryResourceSize").GetInt32()>0);
        });
        Assert.Equal(741,a.GetProperty("sourceSha256").EnumerateObject().Count());
    }
    [Fact]
    public void DiscoveredDiagnosticHasNoNewAuthorityAndKeepsExistingPlansAndEvidenceDefaults()
    {
        var f=NativeWorldDoorFixtures.Large02Prefab;var p=NativeQa.NativeQaModes.Find(f.Mode).Plan;
        Assert.Equal(16900,p.X);Assert.Equal(7100,p.Z);Assert.Contains("y=95,rotation=0",Assert.Single(p.AfterLoadout));
        Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.False(p.ExtraQaMetadata["WalkRouteProven"].GetBoolean());Assert.False(p.ExtraQaMetadata["LoadingAreaUiReturnProven"].GetBoolean());
        Assert.Contains(p.ReadyMessages,m=>m.Contains("WORLD-LARGE02-PREFAB-ROTATIONS.md"));
        Assert.Equal("WORLD-MEDIUM03-PREFAB-ROTATIONS.md",NativeWorldDoorFixtures.Medium03Prefab.EvidenceDocument);
        Assert.Null(NativeWorldDoorFixtures.Large02.ExpectedQuaternion);Assert.Equal(669,NativeWorldDoorFixtures.Large02.LocalId);
        var scenario=Scenarios.All.Single(s=>s.Name=="large02-prefab-door");Assert.NotNull(scenario.Environment);Assert.Equal("0",scenario.Environment["MXO_DEV_COMMANDS"]);
    }
}
