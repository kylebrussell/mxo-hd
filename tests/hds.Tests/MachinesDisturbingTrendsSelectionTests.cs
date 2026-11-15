using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
public class MachinesDisturbingTrendsSelectionTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static IEnumerable<StaticWorldObject> SourceObjects()
    {
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Root,"data",file)).Skip(1))
        {
            if(string.IsNullOrWhiteSpace(line))continue;var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            yield return new StaticWorldObject{metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]};
        }
    }
    [Fact] public void ExistingNormalLayoutSeedSelectsAnAuditedComputerWithoutForcingSource()
    {
        var xml=new XmlDocument();xml.Load(Path.Combine(Root,"data","missions","machines_1_2_1_disturbing_trends.xml"));var mission=MissionXml.Parse(xml.DocumentElement!);
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var sites=MissionSites.Build(SourceObjects(),1);var used=new List<MissionSite>();var records=new List<object>();MissionOpenFloorChoice? actual=null;
        double x=17000,z=2300;
        for(int phaseIndex=0;phaseIndex<2;phaseIndex++)
        {
            var phase=mission.phases[phaseIndex];int seed=MissionStaging.QaSiteSeed("1","mxotest_offline_source","201101",mission.id,phaseIndex)!.Value;var rng=new Random(seed);
            int count=mission.npcs.Count(n=>n.at==phaseIndex+1)+MissionStaging.SpotsForWaves(phase);
            bool Eligible(MissionSite s)=>MissionInteriorSites.Eligible(phase,s,out _,count);
            var site=MissionSites.Choose(sites,phase.site,x,z,count,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase),admissible:Eligible)
                ??MissionSites.Choose(sites,"Any",x,z,count,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase),admissible:Eligible);
            Assert.NotNull(site);used.Add(site);records.Add(new{Phase=phaseIndex,Approach=new[]{x,z},Site=site,EffectiveSeed=seed});
            if(phaseIndex==1)
            {
                var choices=MissionStaging.SelectObjects(phase,site,rng).Select(MissionOpenFloorChoice.From).ToArray();actual=Assert.Single(choices);
                Assert.NotEqual(219152420u,site.elevatorDoor.mxoStaticId); // exact native-failed sealed lobby is independently covered
                Assert.True(MissionOpenFloorAdmission.TryGeometry(mission,phaseIndex,site,choices,out var poses,out string why),why);
                Assert.True(MissionInteriorSites.TryRegion(site,out var region,out why),why);
                Assert.True(MissionInteriorSites.TryAllocate(site,count,out var actors,out why),why);
                foreach(var marker in actors)
                {
                    var projected=MissionInteriorSites.Project(site,marker,800);Assert.True(projected.HasValue);
                    Assert.True(region.Contains(new(projected.Value.x,projected.Value.y,projected.Value.z)));
                }
                records.Add(new{Phase=phaseIndex,Choices=choices,Poses=poses});
            }
            // Phase 1 is Talk then ordinary exit; phase 2 selection occurs at that same selected elevator waypoint.
            x=site.elevatorDoor.pos_x;z=site.elevatorDoor.pos_z;
        }
        Assert.NotNull(actual);Assert.Equal((ushort)3,actual.Goid);
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-machines-disturbing-trends-selection.json"),JsonSerializer.Serialize(new{LayoutSeed=201101,Mission=mission.id,Phases=records,
            Scope="Offline unchanged seed, normal eligible-site/source selection; no native walking or picking claim"},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
    }
}
