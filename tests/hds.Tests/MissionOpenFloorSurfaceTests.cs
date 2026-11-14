using hds;
using System.Security.Cryptography;
using System.Text.Json;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorSurfaceTests
{
    private static string Repo {get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds","Hardline Dreams MxO server.csproj")))p=Path.GetDirectoryName(p)!;return p;}}
    private static void Load(){SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));}
    [Fact]
    public void EightIndependentLiteralSourceSurfacesAreCopiedWithTheirExactNavIdentityAndPlane()
    {
        Load();string path=Path.Combine(Repo,"data/navmesh/1.bin.gz");string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/MissionOpenFloorGeometryCases.json")));
        var cases=doc.RootElement.GetProperty("Cases").EnumerateArray().ToArray();Assert.Equal(8,cases.Length);
        foreach(var row in cases)
        {
            var raw=row.GetProperty("SurfaceId");var id=new G.Id(raw[0].GetUInt16(),raw[1].GetUInt16(),raw[2].GetUInt16());
            Assert.True(NavMesh.TryOpenFloorSurfaces(1,new[]{id.Sector},out var surfaces,out string why),why);
            var surface=Assert.Single(surfaces.Where(s=>s.Identity.Equals(id)));var literal=row.GetProperty("SurfaceVertices").EnumerateArray().ToArray();
            Assert.Equal(literal.Length,surface.Vertices.Count);for(int i=0;i<literal.Length;i++){Assert.Equal(literal[i][0].GetDouble(),surface.Vertices[i].X);Assert.Equal(literal[i][1].GetDouble(),surface.Vertices[i].Z);}
            Assert.Equal(row.GetProperty("Floor").GetDouble(),surface.FloorC);Assert.Equal(0,surface.FloorX);Assert.Equal(0,surface.FloorZ);
        }
        Assert.Equal(before,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }
    [Fact]
    public void MissingScopeOrAnyMissingSectorReturnsNoPartialSurfaceSnapshot()
    {
        Load();Assert.False(NavMesh.TryOpenFloorSurfaces(1,null!,out var missing,out string why));Assert.Empty(missing);Assert.Equal("MissingSectorScope",why);
        Assert.False(NavMesh.TryOpenFloorSurfaces(1,new ushort[]{209,ushort.MaxValue},out var partial,out why));Assert.Empty(partial);Assert.Equal("MissingSectorOrOrigin",why);
    }
}
