using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class SatiPrefabRotationTests
{
    private static string Root { get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    private static string Hash(JsonElement e)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(e.GetRawText().Replace("\r\n","\n")))).ToLowerInvariant();
    [Fact]
    public void NativeRolloutRetainsAllHistoricalTuplesConflictsAndSafeKeys()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/archive_sati-static-audit.json")));var a=doc.RootElement;
        Assert.Equal("49ab0994710a2e73110559555ee91c9e56d2e55de4276d1fe96aedae8c60df9f",Hash(a.GetProperty("raw")));
        Assert.Equal("aee9e06d2a477ef49022b494d88359524195231449e1bdf9b5b30b61434fc3fb",Hash(a.GetProperty("conflicts")));
        var omitted=a.GetProperty("excludedKeys").EnumerateArray().Select(r=>(r[0].GetInt32(),r[1].GetInt32())).ToHashSet();Assert.Equal(70,omitted.Count);
        var historic=a.GetProperty("historicallyUnprovenRotationKeys").EnumerateArray().Select(r=>(r[0].GetInt32(),r[1].GetInt32())).ToHashSet();Assert.Equal(5381,historic.Count);Assert.Equal(63,historic.Intersect(omitted).Count());Assert.Contains((0,126),historic);Assert.DoesNotContain((0,126),omitted);
        var groups=a.GetProperty("raw").EnumerateArray().GroupBy(r=>(r[0].GetInt32(),r[1].GetInt32())).ToArray();Assert.Equal(140,groups.Where(g=>omitted.Contains(g.Key)).Sum(g=>g.Count()));
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_archive_sati.csv")).Skip(1).Select(l=>l.Split(',')).ToDictionary(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3]))));Assert.Equal(7917,rows.Count);Assert.Empty(rows.Keys.Intersect(omitted));
        var old=groups.Where(g=>!historic.Contains(g.Key)&&!omitted.Contains(g.Key)).ToArray();Assert.Equal(2599,old.Length);int changed=0;double maximum=0;
        foreach(var g in old)
        {
            Assert.True(rows.TryGetValue(g.Key,out var row));var source=g.First();bool different=false;
            for(int i=0;i<3;i++){double d=Math.Abs(double.Parse(row![6+i],CultureInfo.InvariantCulture)-source[3][i].GetDouble());maximum=Math.Max(maximum,d);different|=d!=0;}
            if(different)changed++;
            byte[] quat=source[4].EnumerateArray().SelectMany(q=>BitConverter.GetBytes(q.GetSingle())).ToArray();Assert.Equal(Convert.ToHexString(quat).ToLowerInvariant(),row![10]);
        }
        Assert.Equal(42,changed);Assert.Equal(.001953125,maximum);
        var placements=a.GetProperty("nativePrefabRotations").GetProperty("placements").EnumerateArray().ToArray();Assert.Equal(268,placements.Length);Assert.Equal(121,placements.Count(p=>p.GetProperty("prefab").GetString()==""));
        Assert.Equal(new[]{62,73,45,88},Enumerable.Range(0,4).Select(i=>placements.Count(p=>p.GetProperty("rotation").GetInt32()==i)));
        Assert.All(placements,p=>{Assert.Equal("direct-sector",p.GetProperty("parent").GetString());Assert.Equal(64,p.GetProperty("resourceSha256").GetString()!.Length);});
    }
    [Fact]
    public void Restored580DoorUsesIndependentLiteralQuaternionAndExistingNavFloor()
    {
        var f=NativeWorldDoorFixtures.SatiPrefab;Assert.Equal(580,f.Goid);Assert.Equal(126u,f.StaticId);Assert.Equal("00000000f304353f00000000f204353f",f.ExpectedQuaternion);
        var row=Assert.Single(File.ReadLines(Path.Combine(Root,"data/staticObjects_archive_sati.csv")).Skip(1).Select(l=>l.Split(',')),r=>r[2]=="7e000000");Assert.Equal(new[]{"35500.0","945.0","-17200.0"},row[6..9]);Assert.Equal(f.ExpectedQuaternion,row[10]);
        Assert.Equal(typeof(AttributeClass364),typeof(GameObjectDefinitions).GetField("Object580")!.FieldType);
        ObjectStates.Load(Path.Combine(Root,"data"));Assert.Equal(4004u,ObjectStates.FindUsable(580,3).executeRule);Assert.Equal(0x835u,ObjectStates.DefaultState(580));Assert.Equal(20u,ObjectStates.Find(580,0x834).seconds);Assert.Equal(1u,ObjectStates.Find(580,0x838).seconds);
        NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));Assert.True(NavMesh.Locate(9,f.X,f.Y,f.Z,out var nav));Assert.Equal(0,nav.sector);Assert.Equal(42,nav.region);Assert.Equal(74,nav.polygon);Assert.True(nav.indoors);Assert.Equal(800,nav.floorY);
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/sati-prefab-door.json")));Assert.Equal("0x6800001a",source.RootElement.GetProperty("modelDescriptionId").GetString());Assert.Equal(897,source.RootElement.GetProperty("modelBytes").GetInt32());Assert.True(source.RootElement.GetProperty("nativeRenderingPending").GetBoolean());
    }
    [Fact]
    public void DiagnosticModeUsesActualSourceWorldWithoutAuthorityOrArrivalChanges()
    {
        var f=NativeWorldDoorFixtures.SatiPrefab;var p=NativeQa.NativeQaModes.Find(f.Mode).Plan;Assert.Equal("archive_sati/sati.metr",f.MetrResource);Assert.Equal(f.X,p.X);Assert.Equal(f.Z,p.Z);Assert.Contains("y=895,rotation=0",Assert.Single(p.AfterLoadout));Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Empty(p.EnvironmentOverrides);Assert.Contains("WORLD-SATI-PREFAB-ROTATIONS.md",Assert.Single(p.ReadyMessages));Assert.Contains("WORLD-MEDIUM03-PREFAB-ROTATIONS.md",Assert.Single(NativeQa.NativeQaModes.Find("medium03-prefab-door").Plan.ReadyMessages));Assert.Equal("00000000f30435b300000000ffff7f3f",NativeWorldDoorFixtures.Medium03Prefab.ExpectedQuaternion);
        var normal=NativeQa.NativeQaModes.Find("sati").Plan;Assert.Equal(7917,normal.ExtraQaMetadata["StaticKeys"].GetInt32());Assert.Equal(70,normal.ExtraQaMetadata["ExcludedStaticKeys"].GetInt32());Assert.True(normal.ExtraQaMetadata["RetailArrivalUnrecovered"].GetBoolean());Assert.False(normal.ExtraQaMetadata["GenericSourceReturnersBound"].GetBoolean());
    }
}
