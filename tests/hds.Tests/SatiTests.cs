using System.Globalization;
using System.Text.Json;
using hds;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;
public class SatiTests
{
    private static string Root { get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void EveryVariantOfRotatedOrConflictingNativeKeysIsOmitted()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/archive_sati-static-audit.json")));
        var a=doc.RootElement;Assert.Equal(8177,a.GetProperty("rawReferences").GetInt32());Assert.Equal(7987,a.GetProperty("uniqueKeys").GetInt32());Assert.Equal(2599,a.GetProperty("exportedKeys").GetInt32());
        Assert.Equal(5381,a.GetProperty("unprovenRotationKeys").GetArrayLength());Assert.Equal(70,a.GetProperty("conflicts").EnumerateObject().Count());Assert.Equal(63,a.GetProperty("rotationAmbiguityOverlap").GetInt32());
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(r=>(r[0].GetInt32(),r[1].GetInt32())).ToHashSet();Assert.Equal(5388,excluded.Count);
        var groups=a.GetProperty("raw").EnumerateArray().GroupBy(r=>(r[0].GetInt32(),r[1].GetInt32())).ToArray();
        foreach(var g in groups)
        {
            var values=g.Select(r=>(r[2].GetUInt32(),r[3][0].GetDouble(),r[3][1].GetDouble(),r[3][2].GetDouble(),r[4][0].GetDouble(),r[4][1].GetDouble(),r[4][2].GetDouble(),r[4][3].GetDouble(),r[5].GetInt32())).Distinct().Count();
            Assert.Equal(g.Any(r=>r[5].GetInt32()!=0)||values>1,excluded.Contains(g.Key));
        }
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_archive_sati.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();Assert.Equal(2599,rows.Length);
        var keys=rows.Select(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3])))).ToHashSet();Assert.Equal(2599,keys.Count);Assert.Empty(keys.Intersect(excluded));
        Assert.Equal(groups.Where(g=>!excluded.Contains(g.Key)).Select(g=>g.Key).Order(),keys.Order());
        Assert.Equal(4,rows.Count(r=>BitConverter.ToUInt32(Convert.FromHexString(r[4]))==47267)); // Source proxies stay UNBOUND, no fabricated executor.
    }
    [Fact]
    public void AllResourceLayersAndNinetyFiveNativeContractsAreAudited()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/sati-resources.json")));
        var a=doc.RootElement;Assert.Equal(31,a.GetProperty("sectorCount").GetInt32());Assert.All(a.GetProperty("layerCounts").EnumerateObject(),r=>Assert.Equal(31,r.Value.GetInt32()));
        Assert.Equal(4,a.GetProperty("layerCounts").EnumerateObject().Count());Assert.Equal(58,a.GetProperty("prefabCount").GetInt32());Assert.Equal(58,a.GetProperty("prefabSourceSha256").EnumerateObject().Count());
        Assert.Equal(0,a.GetProperty("hardlineCount").GetInt32());Assert.Equal(0,a.GetProperty("cnetCount").GetInt32());Assert.Empty(a.GetProperty("capturedNpcRows").EnumerateObject());
        Assert.Equal(95,a.GetProperty("safeGoidContracts").GetArrayLength());Assert.Equal(4,a.GetProperty("unboundGenericSourceProxyKeys").GetArrayLength());
        var door=Assert.Single(a.GetProperty("safeGoidContracts").EnumerateArray(),r=>r.GetProperty("goid").GetInt32()==365);Assert.Equal(364,door.GetProperty("schema").GetInt32());
        var proxy=Assert.Single(a.GetProperty("safeGoidContracts").EnumerateArray(),r=>r.GetProperty("goid").GetInt32()==47267);Assert.Equal(3602,proxy.GetProperty("schema").GetInt32());
    }
    [Fact]
    public void BookAuthorityAndSatiOnlySeparatedFallbackAreValidated()
    {
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        for(uint org=1;org<=6;org++){Assert.True(PvpArchives.Arrival(9,org,out var p));Assert.Equal((14225d,95d,-5225d),(p.x,p.y,p.z));Assert.True(NavMesh.Locate(9,p.x,p.y,p.z,out _));}
        Assert.Equal(9,PvpArchives.MapFor(8));Assert.Equal(18u,PvpArchives.ConstructId(9));var book=PvpArchives.Find(47726)!;Assert.Null(PvpArchives.Refusal(book,1,50,1));Assert.NotNull(PvpArchives.Refusal(book,2,50,1));
        var plan=NativeQaModes.Find("sati").Plan;Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Empty(plan.AfterHelpers);
        Assert.Contains("rotation=0",Assert.Single(plan.AfterLoadout));Assert.Contains("47726,3,1,0",Assert.Single(plan.AfterLoadout));Assert.False(plan.ExtraQaMetadata["GenericSourceReturnersBound"].GetBoolean());Assert.True(plan.ExtraQaMetadata["RetailArrivalUnrecovered"].GetBoolean());
    }
    [Fact]
    public void OptionalQaResourcePathPreservesTheOldDoorFixtureConvention()
    {
        foreach(var f in new[]{NativeWorldDoorFixtures.Large01,NativeWorldDoorFixtures.Large02}){Assert.Null(f.MetrResourceSuffix);Assert.Equal($"{f.District}/{f.District}.metr",f.MetrResource);}
        Assert.Equal("archive_sati",NativeWorldDoorFixtures.Sati.District);Assert.Equal("archive_sati/sati.metr",NativeWorldDoorFixtures.Sati.MetrResource);
        Assert.False(NativeQaModes.Find("sati").Plan.ExtraQaMetadata.ContainsKey("WorldDoorDiagnostic"));Assert.True(NativeQaModes.Find("sati-door").Plan.ExtraQaMetadata["WorldDoorDiagnostic"].GetBoolean());
    }
}
