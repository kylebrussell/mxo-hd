using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;
using G = hds.MissionOpenFloorResolver;
using P = hds.MissionOpenFloorPhasePlanner;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionComputerOfficeRouteTests
{
    private static string Root
    {
        get
        {
            string p = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(p, "hds.sln"))) p = Path.GetDirectoryName(p)!;
            return p;
        }
    }

    private static IEnumerable<StaticWorldObject> Statics()
    {
        foreach (string name in new[] { "staticObjects_slums_1.csv", "staticObjects_slums_2.csv" })
        foreach (string line in File.ReadLines(Path.Combine(Root, "data", name)).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] f = line.Split(',');
            double D(int i) => double.Parse(f[i], CultureInfo.InvariantCulture);
            yield return new StaticWorldObject
            {
                metrId = ushort.Parse(f[0]), sectorID = ushort.Parse(f[1]),
                mxoStaticId = BitConverter.ToUInt32(Convert.FromHexString(f[2])),
                staticId = BitConverter.ToUInt32(Convert.FromHexString(f[3])),
                type = Convert.FromHexString(f[4][..4]), exterior = bool.Parse(f[5]),
                pos_x = D(6), pos_y = D(7), pos_z = D(8), rot = D(9), quat = f[10]
            };
        }
    }

    [Fact]
    public void NativeReportedSealedOfficeRejectsOutdoorComputerAndNpcSpots()
    {
        SectorProperties.Load(Path.Combine(Root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(Root, "data", "navmesh"));
        var sites = MissionSites.Build(Statics(), 1);
        var site = Assert.Single(sites, s => s.style == "Office" && s.elevatorDoor?.mxoStaticId == 219152420 &&
            s.elevatorDoor.pos_x == 28800 && s.elevatorDoor.pos_y == 945 && s.elevatorDoor.pos_z == 14700 &&
            s.minX == 28900 && s.maxX == 31100 && s.minZ == 12350 && s.maxZ == 15100);
        Assert.Equal((28800d, 945d, 14700d), (site.elevatorDoor.pos_x, site.elevatorDoor.pos_y, site.elevatorDoor.pos_z));
        var raw = Assert.Single(site.objectSpots.Concat(site.containers).Concat(site.realObjects), s => s.mxoStaticId == 0x0d1001ed);
        Assert.Equal((ushort)1407, MissionSites.Goid(raw));
        Assert.Equal((ushort)209, raw.sectorID);
        Assert.Equal(493u, raw.staticId);
        Assert.Equal((29000d, 1026d, 13460d), (raw.pos_x, raw.pos_y, raw.pos_z));
        string sourceBefore = JsonSerializer.Serialize(raw, MissionProgress.SaveJson);
        var xml = new XmlDocument();
        xml.Load(Path.Combine(Root, "data", "missions", "machines_1_2_1_disturbing_trends.xml"));
        var mission = MissionXml.Parse(xml.DocumentElement!);
        Assert.True(MissionOpenFloorRequests.TryCreate(raw, 3, site, 1, out var request, out string why, mission.phases[1], 1), why);
        bool ok = P.TryPlan(new[] { new P.Input(1, request) }, out var plan, out why, out var work, P.Limits.Default);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "mxo-pr204-office-route.json"), JsonSerializer.Serialize(new
        {
            Passed = ok, Refusal = why, work.BodyNodes, work.RouteGraphs, work.StaticRootGraphs,
            Request = request, Results = plan?.Results.Values,
            SourceUnchanged = sourceBefore == JsonSerializer.Serialize(raw, MissionProgress.SaveJson)
        }, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        Assert.False(ok);Assert.Null(plan);
        Assert.False(MissionInteriorSites.TryAllocate(site,2,out _,out string interiorRefusal));
        Assert.Equal("InteriorEntryHasNoSiteAnchor",interiorRefusal);
        Assert.True(G.TryReachableRegion(request.World,request.Surfaces,request.Entry,request.Scene,out var region,out why),why);
        Assert.True(region.Contains(request.Entry.Standing));
        Assert.False(region.Contains(new G.Position(29101.4375,895,14592.57421875)));
        Assert.False(region.Covers(new[]{new G.Point(29060,14550),new G.Point(29145,14550),new G.Point(29145,14635),new G.Point(29060,14635)}));
        Assert.Equal(sourceBefore, JsonSerializer.Serialize(raw, MissionProgress.SaveJson));
    }

    [Fact]
    public void NativeReportedMerovingianSiteIsExcludedBeforeSelectingItsComputer()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var site=Assert.Single(MissionSites.Build(Statics(),1),s=>s.elevatorDoor?.mxoStaticId==249561713u&&
            s.elevatorDoor.pos_x==11100&&s.elevatorDoor.pos_y==2945&&s.elevatorDoor.pos_z==5600);
        var source=Assert.Single(site.objectSpots,s=>s.mxoStaticId==0x0ee0004b);
        Assert.Equal((12620d,3045d,6355d),(source.pos_x,source.pos_y,source.pos_z));
        string before=JsonSerializer.Serialize(site,new JsonSerializerOptions{IncludeFields=true});
        Assert.False(MissionInteriorSites.TryRegion(site,out _,out string why));
        Assert.NotNull(why);
        Assert.Equal(before,JsonSerializer.Serialize(site,new JsonSerializerOptions{IncludeFields=true}));
    }
    [Fact]
    public void AlreadyBoundComputerOutsideTheArrivalRoomCannotSupplyItsNativeUsePoint()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var site=Assert.Single(MissionSites.Build(Statics(),1),s=>s.elevatorDoor?.mxoStaticId==0x0ee000fa&&s.elevatorDoor.pos_y==545&&s.CentreX==10925&&s.CentreZ==5662.5);
        var source=Assert.Single(site.realObjects,s=>s.mxoStaticId==0x0ee00451);
        Assert.Equal((10955d,631d,7120d),(source.pos_x,source.pos_y,source.pos_z));
        var xml=new XmlDocument();xml.Load(Path.Combine(Root,"data","missions","merovingian_1_2_3_turf_of_vengeance.xml"));var mission=MissionXml.Parse(xml.DocumentElement!);
        string before=MissionOpenFloorSite.CatalogFingerprint(site);
        Assert.True(MissionInteriorSites.TryRegion(site,out var region,out string why),why);
        Assert.True(MissionOpenFloorProfiles.TryUsable(3,out var usable));
        Assert.False(region.CouldReachNative(new(source.pos_x,source.pos_y,source.pos_z),usable,new OpenFloorOrientation(source.quat)));
        Assert.False(MissionInteriorSites.Eligible(mission.phases[1],site,out why,1));Assert.Equal("NoInteriorNativeUse:Computer",why);
        Assert.Equal(before,MissionOpenFloorSite.CatalogFingerprint(site));
    }
    [Fact]
    public void HardlineHunterFiltersTheExactTooShallowSiteBeforeItsNormalPreviewPick()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var sites=MissionSites.Build(Statics(),1);
        var old=Assert.Single(sites,s=>s.elevatorDoor?.mxoStaticId==0x0d100401&&s.minX==22100&&s.maxX==24100&&s.minZ==14200&&s.maxZ==15950);
        var source=Assert.Single(old.objectSpots,s=>s.mxoStaticId==0x0d100402);Assert.Equal((ushort)1412,MissionSites.Goid(source));
        var xml=new XmlDocument();xml.Load(Path.Combine(Root,"data","missions","zion_1_2_9_hardline_hunter.xml"));var mission=MissionXml.Parse(xml.DocumentElement!);
        Assert.True(MissionInteriorSites.TryRegion(old,out _,out string why),why);
        string fingerprint=MissionOpenFloorSite.CatalogFingerprint(old);
        Assert.False(MissionInteriorSites.Eligible(mission.phases[0],old,out why,0));Assert.Equal("NoInteriorFootprint:Box",why);
        Assert.Equal(fingerprint,MissionOpenFloorSite.CatalogFingerprint(old));
        int seed=MissionStaging.QaSiteSeed("1","mxotest_exact218","101208",101209,0)!.Value;
        bool Eligible(MissionSite s)=>MissionInteriorSites.Eligible(mission.phases[0],s,out _,0);
        var selected=MissionSites.Choose(sites,"Normal",7800,13795,0,1,new Random(seed),admissible:Eligible);
        Assert.NotNull(selected);Assert.NotEqual(fingerprint,MissionOpenFloorSite.CatalogFingerprint(selected));
        // Preview caches only the site. Acceptance starts the same seeded source stream afresh.
        var choices=MissionStaging.SelectObjects(mission.phases[0],selected,new Random(seed)).Select(MissionOpenFloorChoice.From).ToArray();
        Assert.True(MissionOpenFloorAdmission.TryGeometry(mission,0,selected,choices,out var poses,out why),why);
        Assert.Single(poses);
    }
}
