using hds;
using System.Text.Json;
namespace hds.Tests;
public class Archive01Tests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void CompleteStaticInventoryHasNoInventedActorsOrAmbiguousKeys()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/archive01-static-audit.json")));
        var a=doc.RootElement;Assert.Equal(156,a.GetProperty("rawReferences").GetInt32());Assert.Equal(156,a.GetProperty("uniqueKeys").GetInt32());
        Assert.Equal(156,a.GetProperty("exportedKeys").GetInt32());Assert.Empty(a.GetProperty("excludedKeys").EnumerateArray());Assert.Empty(a.GetProperty("conflicts").EnumerateObject());
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_archive01.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(156,rows.Length);Assert.Equal(156,rows.Select(r=>r[2]).Distinct().Count());Assert.All(rows,r=>Assert.Equal("4",r[0]));
        var ids=rows.Select(r=>BitConverter.ToUInt32(Convert.FromHexString(r[4]))).ToArray();
        Assert.Equal(new uint[]{405,502,504,2506,37910,38272,38273},ids.Distinct().Order().ToArray());
        Assert.Equal(83,ids.Count(id=>id==2506)); // GangMaster source markers, not 83 spawned actors.
        Assert.DoesNotContain(592u,ids);
    }
    [Fact]
    public void ExistingArchiveSourceArrivalsAndReturnAuthorityAreRetained()
    {
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        for(uint org=1;org<=6;org++){Assert.True(PvpArchives.Arrival(4,org,out var p));Assert.True(NavMesh.Locate(4,p.x,p.y,p.z,out var at));Assert.Equal(at.floorY+95,p.y,3);}
        Assert.True(PvpArchives.Arrival(4,1,out var zion));Assert.Equal((-4020d,95d,-46020d),(zion.x,zion.y,zion.z));
        Assert.Equal(4,PvpArchives.MapFor(5));Assert.Equal(15u,PvpArchives.ConstructId(4));
        var book=PvpArchives.Find(38534)!;Assert.Equal(5,book.Id);Assert.Null(PvpArchives.Refusal(book,1,50,1));Assert.NotNull(PvpArchives.Refusal(book,1,39,1));
        Assert.True(NavMesh.Locate(1,111600,695,-6130,out _));
        Assert.True(NavMesh.Locate(4,26000,-105,5000,out _));Assert.True(NavMesh.Locate(4,-46000,895,5000,out _));
    }
    [Fact]
    public void FixtureUsesOwnedBookAtCitySenderWithoutPrivilegedShortcuts()
    {
        var plan=new NativeQa.Modes.Archive01Mode().Plan;Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(111600,plan.X);Assert.Equal(-6130,plan.Z);var sql=Assert.Single(plan.AfterLoadout);
        Assert.Contains("districtId=1",sql);Assert.Contains("y=695",sql);Assert.Contains("38534,3,1,0",sql);Assert.DoesNotContain("38534,106",sql);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("archive01")["MXO_MOBS"]);
    }
}
