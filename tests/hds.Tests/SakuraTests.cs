using hds;
using System.Text.Json;
namespace hds.Tests;
public class SakuraTests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void StaticInventoryPreservesEverySafeReferenceWithoutInventingActors()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/archive_sakura-static-audit.json")));
        var a=doc.RootElement;Assert.Equal(177,a.GetProperty("rawReferences").GetInt32());Assert.Equal(177,a.GetProperty("uniqueKeys").GetInt32());Assert.Equal(177,a.GetProperty("exportedKeys").GetInt32());
        Assert.Empty(a.GetProperty("excludedKeys").EnumerateArray());Assert.Empty(a.GetProperty("conflicts").EnumerateObject());
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_archive_sakura.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(177,rows.Length);Assert.Equal(177,rows.Select(r=>r[2]).Distinct().Count());Assert.All(rows,r=>Assert.Equal("8",r[0]));
        var ids=rows.Select(r=>BitConverter.ToUInt32(Convert.FromHexString(r[4]))).ToArray();
        Assert.Equal(new uint[]{2506,38271,38272,38273,38478,38480,38482},ids.Distinct().Order().ToArray());
        Assert.Equal(65,ids.Count(id=>id==2506));Assert.DoesNotContain(592u,ids); // GangMaster markers, not 65 ambient actors or a hardline.
    }
    [Fact]
    public void ExistingSourceArrivalAndBookAuthorityRemainUnchanged()
    {
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        Assert.True(PvpArchives.Arrival(8,1,out var p));Assert.Equal((15500d,295d,15500d),(p.x,p.y,p.z));
        Assert.True(NavMesh.Locate(8,p.x,p.y,p.z,out var arrival));Assert.Equal(arrival.floorY+95,p.y,3);
        Assert.Equal(8,PvpArchives.MapFor(2));Assert.Equal(12u,PvpArchives.ConstructId(8));
        var book=PvpArchives.Find(38537)!;Assert.Equal(2,book.Id);Assert.Null(PvpArchives.Refusal(book,1,35,1));Assert.NotNull(PvpArchives.Refusal(book,1,29,1));Assert.NotNull(PvpArchives.Refusal(book,1,41,1));
        Assert.True(NavMesh.Locate(8,17700,95,15550,out _)); // Raw SID returner is a different source spot/height, left unchanged.
    }
    [Fact]
    public void FixtureSetsActualLevelAndExperienceAfterGenericHelperSetup()
    {
        var plan=new NativeQa.Modes.SakuraMode().Plan;Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(111600,plan.X);Assert.Equal(-6130,plan.Z);var sql=Assert.Single(plan.AfterLoadout);
        Assert.Contains("districtId=1",sql);Assert.Contains("y=695",sql);Assert.Contains("38537,3,1,0",sql);Assert.DoesNotContain("38537,106",sql);
        Assert.Equal($"UPDATE characters SET level=35,exp={PlayerProgression.ExperienceForLevel(35)} WHERE charId=110",Assert.Single(plan.AfterHelpers));
        var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("sakura");Assert.Equal("",e["MXO_MOBS"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);
    }
}
