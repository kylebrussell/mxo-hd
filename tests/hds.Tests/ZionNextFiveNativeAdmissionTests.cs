using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class ZionNextFiveNativeAdmissionTests
{
    const string FailedSite="1FE1942ACA2AE9613733B4707110719E1162CE2BE57471E9109419D6F739B856";
    const string DepartedSite="816F1908C13EB0EAFA9FBEC10FC00F24D75D112AA3783FEEF017A3389CDDFF0B";
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static readonly Lazy<List<MissionSite>> Catalog=new(()=>
    {
        var rows=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Skip(1).Where(l=>!string.IsNullOrWhiteSpace(l)))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            rows.Add(new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),
                staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        var sites=MissionSites.Build(rows,1);var hoods=MissionSites.LoadNeighbourhoods(Path.Combine(Repo,"data/slums_neighbourhoods.csv"));
        foreach(var site in sites)if(site.elevatorDoor!=null&&hoods.TryGetValue(site.elevatorDoor.sectorID,out string? hood))site.neighbourhood=hood;
        return sites;
    });
    static Mission Mission()
    {
        SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
        var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data/missions/zion_1_2_9_hardline_hunter.xml"));return MissionXml.Parse(xml.DocumentElement!);
    }
    static MissionSite Site(string fingerprint)=>Catalog.Value.Single(s=>MissionOpenFloorSite.CatalogFingerprint(s)==fingerprint);
    [Fact] public void Captured47e00129CannotQualifyForThe47e00247MeasuredFloorOrRepairItsPending()
    {
        var mission=Mission();var site=Site(FailedSite);var raw=site.objectSpots.Single(s=>s.mxoStaticId==0x47e00129);
        Assert.Equal(0x47e00247u,site.elevatorDoor.mxoStaticId);Assert.Equal((ushort)1412,MissionSites.Goid(raw));
        Assert.Equal((44870d,3065d,-13770d),(raw.pos_x,raw.pos_y,raw.pos_z));
        var choice=new MissionOpenFloorChoice(1,548,JsonSerializer.Serialize(raw,Fields));
        Assert.True(MissionElevatorPoses.TrySelectionContext(site,out var context,out string why),why);Assert.NotNull(context);
        Assert.False(MissionElevatorPoses.CompatibleSource(site,raw,context,out why));Assert.Equal("ElevatorSourceRegionDisagreement",why);
        var pending=new MissionOpenFloorPendingAdmission(1,101209,"b9b212099176422ba5a8ffed88d0eb31",1,1794756844,
            MissionOpenFloorSite.From(site),new[]{choice},"MissingMissionElevatorWitness");
        string before=JsonSerializer.Serialize(pending,Fields);
        Assert.False(MissionOpenFloorAdmission.TryGeometry(mission,1,site,pending.Choices,out _,out why,pending));
        Assert.Equal("ElevatorSourceRegionDisagreement",why);Assert.Null(pending.Elevator);
        Assert.Equal(before,JsonSerializer.Serialize(pending,Fields));Assert.Equal(0x47e00129u,pending.Choices.Single().ReadSpot().mxoStaticId);
    }
    [Fact] public void SameNormal101208SeedAndCapturedExitChooseACompleteCompatibleNextPhase()
    {
        var mission=Mission();var phase=mission.phases[1];var history=new[]{Site(DepartedSite)};
        int npcs=mission.npcs.Count(n=>n.at==2)+MissionStaging.SpotsForWaves(phase);
        int? seed=MissionStaging.QaSiteSeed("1","mxotest_run_1_6e4d6b7521","101208",101209,1);Assert.Equal(3132806,seed);
        var rng=new Random(seed!.Value);var hoods=phase.neighbourhoods.Count>0?phase.neighbourhoods:mission.neighbourhoods;
        bool Eligible(MissionSite site)=>MissionInteriorSites.Eligible(phase,site,out _,npcs);
        var selected=MissionSites.Choose(Catalog.Value,phase.site,30278.80849421055,-6998.874131254861,npcs,MissionStaging.PlacesNeeded(phase),rng,history,hoods,
            requiredFurniture:MissionStaging.FurnitureNeeded(phase),admissible:Eligible)
            ??MissionSites.Choose(Catalog.Value,"Any",30278.80849421055,-6998.874131254861,npcs,MissionStaging.PlacesNeeded(phase),rng,history,hoods,
                requiredFurniture:MissionStaging.FurnitureNeeded(phase),admissible:Eligible);
        Assert.NotNull(selected);Assert.True(MissionStaging.TrySourcePools(phase,selected,npcs,out var pools,out string why),why);
        var choices=MissionStaging.SelectObjectsFromPools(phase,selected,rng,pools).Select(MissionOpenFloorChoice.From).ToArray();
        var choice=Assert.Single(choices);Assert.Equal((ushort)548,choice.Goid);
        Assert.False(selected.elevatorDoor.mxoStaticId==0x47e00247&&choice.ReadSpot().mxoStaticId==0x47e00129);
        Assert.True(MissionElevatorPoses.TryCreate(selected,choices,out var elevator,out why),why);
        var pending=new MissionOpenFloorPendingAdmission(1,101209,new string('a',32),1,1794756844,MissionOpenFloorSite.From(selected),choices,null,elevator);
        Assert.True(MissionOpenFloorAdmission.TryGeometry(mission,1,selected,choices,out var poses,out why,pending),why);
        Assert.Single(poses);Assert.Equal((ushort)548,poses[1].ConcreteGoid);
        Assert.Equal(DepartedSite,MissionOpenFloorSite.CatalogFingerprint(history[0])); // No history/source/site mutation.
    }
}
