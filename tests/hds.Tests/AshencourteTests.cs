using hds;
using System.Text.Json;
namespace hds.Tests;
public class AshencourteTests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void StaticInventoryPreservesEverySafeReferenceWithoutInventingActors()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/archive_ashencourte-static-audit.json")));
        var a=doc.RootElement;Assert.Equal(223,a.GetProperty("rawReferences").GetInt32());Assert.Equal(223,a.GetProperty("uniqueKeys").GetInt32());Assert.Equal(223,a.GetProperty("exportedKeys").GetInt32());
        Assert.Empty(a.GetProperty("excludedKeys").EnumerateArray());Assert.Empty(a.GetProperty("conflicts").EnumerateObject());
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_archive_ashencourte.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(223,rows.Length);Assert.Equal(223,rows.Select(r=>r[2]).Distinct().Count());Assert.All(rows,r=>Assert.Equal("6",r[0]));
        var ids=rows.Select(r=>BitConverter.ToUInt32(Convert.FromHexString(r[4]))).ToArray();
        Assert.Equal(new uint[]{587,2506,7342,38271,38272,38273,38298,38488,38489,39416,39418},ids.Distinct().Order().ToArray());
        Assert.Equal(40,ids.Count(id=>id==2506));Assert.DoesNotContain(592u,ids); // Source GangMaster markers, no fabricated hardline.
    }
    [Fact]
    public void ExistingSourceArrivalAndCapturedGoofFloorRemainGrounded()
    {
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        Assert.True(PvpArchives.Arrival(6,1,out var p));Assert.Equal((-18500d,95d,-7500d),(p.x,p.y,p.z));
        Assert.True(NavMesh.Locate(6,p.x,p.y,p.z,out var arrival));Assert.Equal(arrival.floorY+95,p.y,3);
        Assert.Equal(6,PvpArchives.MapFor(1));Assert.Equal(11u,PvpArchives.ConstructId(6));
        var book=PvpArchives.Find(38538)!;Assert.Equal(1,book.Id);Assert.Null(PvpArchives.Refusal(book,1,25,1));Assert.NotNull(PvpArchives.Refusal(book,1,31,1));
        Assert.True(NavMesh.Locate(6,11070,95,3974,out var goof));Assert.Equal(2,goof.sector);Assert.Equal(0,goof.floorY,3);
        Assert.True(NavMesh.Locate(6,11376,95,3846,out _));Assert.False(NavMesh.Locate(6,24088,495,-3502,out _));
        Assert.Equal(NpcSpawnScope.Scope.Instance,NpcSpawnScope.Classify(6,"Blackwood Goof",false));Assert.True(NpcSpawnScope.LoadAmbient(NpcSpawnScope.Scope.Instance));
    }
    [Fact]
    public void NativeFixtureUsesLevelTwentyFiveOwnedBookAndOrdinaryPopulation()
    {
        var plan=new NativeQa.Modes.AshencourteMode().Plan;Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(111600,plan.X);Assert.Equal(-6130,plan.Z);var sql=Assert.Single(plan.AfterLoadout);
        Assert.DoesNotContain("level=25",sql);Assert.Contains("districtId=1",sql);Assert.Contains("y=695",sql);Assert.Contains("38538,3,1,0",sql);Assert.DoesNotContain("38538,106",sql);
        Assert.Equal($"UPDATE characters SET level=25,exp={PlayerProgression.ExperienceForLevel(25)} WHERE charId IN (110,6111,6112,6113,6114); UPDATE char_abilities SET level=LEAST(level,25) WHERE char_id IN (110,6111,6112,6113,6114)",Assert.Single(plan.AfterHelpers));
        var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("ashencourte");Assert.Equal("",e["MXO_MOBS"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);
    }
}
