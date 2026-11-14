using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class ZionNextFiveSelectionTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static IEnumerable<StaticWorldObject> SourceObjects()
    {
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Root,"data",file)).Skip(1))
        {
            if(string.IsNullOrWhiteSpace(line))continue;var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            yield return new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]};
        }
    }
    [Fact] public void AllSevenPhasesHaveNormallySelectedSourceInventories()
    {
        ObjectStates.Load(Path.Combine(Root,"data"));SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var sites=MissionSites.Build(SourceObjects(),1);var hoods=MissionSites.LoadNeighbourhoods(Path.Combine(Root,"data","slums_neighbourhoods.csv"));
        foreach(var s in sites)if(s.elevatorDoor!=null&&hoods.TryGetValue(s.elevatorDoor.sectorID,out string? name))s.neighbourhood=name;
        var rows=new List<object>();int objectCount=0;
        foreach(int id in HeadlessClient.ZionNextFiveFixture.Missions)
        {
            var file=Directory.GetFiles(Path.Combine(Root,"data","missions"),"zion_1_2_*.xml").Single(f=>{var d=new XmlDocument();d.Load(f);return int.Parse(d.DocumentElement!.GetAttribute("id"))==id;});
            var xml=new XmlDocument();xml.Load(file);var mission=MissionXml.Parse(xml.DocumentElement!);var used=new List<MissionSite>();
            for(int phaseIndex=0;phaseIndex<mission.phases.Count;phaseIndex++)
            {
                var phase=mission.phases[phaseIndex];int seed=MissionStaging.QaSiteSeed("1","mxotest_offline_source","101208",id,phaseIndex)!.Value;var rng=new Random(seed);
                int npcs=mission.npcs.Count(n=>n.at==phaseIndex+1)+MissionStaging.SpotsForWaves(phase);var neighbourhoods=phase.neighbourhoods.Count>0?phase.neighbourhoods:mission.neighbourhoods;
                var site=MissionSites.Choose(sites,phase.site,17000,2300,npcs,MissionStaging.PlacesNeeded(phase),rng,used,neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase))
                    ??MissionSites.Choose(sites,"Any",17000,2300,npcs,MissionStaging.PlacesNeeded(phase),rng,used,neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase));
                Assert.NotNull(site);used.Add(site);var progress=new MissionProgress(mission){phaseIndex=phaseIndex,site=site};
                var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new(){missionInstanceCounter=47};client.viewMan=new();
                foreach(var def in phase.objects)foreach(ushort goid in site.containers.Concat(site.realObjects).Select(MissionSites.Goid).Concat(new ushort[]{3,1416,552,357}).Distinct())
                    client.viewMan.GetViewForEntityAndGo(987000047000UL+(ulong)(phaseIndex*50+def.id),goid).viewCreated=true;
                typeof(MissionStaging).GetMethod("PlaceObjects",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{client,progress,phaseIndex==0?new Random(seed):rng});
                Assert.Equal(phase.objects.Count,progress.objects.Count);Assert.Equal(progress.objects.Count,progress.objects.Values.Select(o=>o.spot.mxoStaticId).Distinct().Count());
                var selected=new List<object>();
                foreach(var pair in progress.objects.OrderBy(x=>x.Key))
                {
                    var state=pair.Value;objectCount++;var pose=MissionComputerAnchors.For(state);bool projected=NavMesh.Project(1,pose.X,site.floorY,pose.Z,out var approach,maxDistance:200);
                    selected.Add(new{Object=pair.Key,Kind=state.def.kind,SourceGoid=MissionSites.Goid(state.spot),Concrete=state.goid,State=MissionStaging.IsComputer(state.goid)?MissionStaging.ComputerStateFor(progress,state):ObjectStates.DefaultState(state.goid),Bound=MissionSites.Goid(state.spot)==state.goid,Spot=state.spot,Pose=new{pose.X,pose.Y,pose.Z,Orientation=Convert.ToHexString(pose.Orientation)},ApproachProjected=projected,Approach=approach,ApproachValidated=projected,SiteFloorMeaning="Median NPC spawner-marker Y; NPC stand point is marker minus50, not the NAV polygon surface",CentreAboveCandidateSurface=pose.Y-approach.location.floorY,MarkerAboveCandidateSurface=site.floorY-approach.location.floorY,HorizontalCandidateDistance=Math.Sqrt(Math.Pow(approach.x-pose.X,2)+Math.Pow(approach.z-pose.Z,2)),NativeReachabilityProven=false});
                }
                rows.Add(new{Mission=id,Phase=phaseIndex+1,Seed=seed,Site=site,Selected=selected});
            }
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-zion-next-five-offline-selection.json"),JsonSerializer.Serialize(new{Input="Fixed17000/2300 EACH offline phase; normal Choose/PlaceObjects seed101208/per-mission exclusions, no forced sites. Live transition selections may differ; NAV projection is not a physical/walked path proof.",Phases=rows},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Assert.Equal(7,rows.Count);Assert.Equal(2,objectCount);
    }
}
