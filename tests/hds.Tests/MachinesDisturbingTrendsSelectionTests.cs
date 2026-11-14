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
        var sites=MissionSites.Build(SourceObjects(),1);var used=new List<MissionSite>();var records=new List<object>();MissionObjectState? actual=null;
        double x=17000,z=2300;
        for(int phaseIndex=0;phaseIndex<2;phaseIndex++)
        {
            var phase=mission.phases[phaseIndex];int seed=MissionStaging.QaSiteSeed("1","mxotest_offline_source","201101",mission.id,phaseIndex)!.Value;var rng=new Random(seed);
            int count=mission.npcs.Count(n=>n.at==phaseIndex+1)+MissionStaging.SpotsForWaves(phase);
            var site=MissionSites.Choose(sites,phase.site,x,z,count,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase))
                ??MissionSites.Choose(sites,"Any",x,z,count,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase));
            Assert.NotNull(site);used.Add(site);records.Add(new{Phase=phaseIndex,Approach=new[]{x,z},Site=site,EffectiveSeed=seed});
            if(phaseIndex==1)
            {
                var progress=new MissionProgress(mission){site=site,phaseIndex=phaseIndex};
                var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new ClientData{missionInstanceCounter=47};client.viewMan=new ViewManager();
                // Suppress only offline transport; use the unchanged source picker and its actual post-Choose RNG state.
                client.viewMan.GetViewForEntityAndGo(987000047051UL,3).viewCreated=true;
                typeof(MissionStaging).GetMethod("PlaceObjects",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{client,progress,rng});actual=progress.objects[1];
            }
            // Phase 1 is Talk then ordinary exit; phase 2 selection occurs at that same selected elevator waypoint.
            x=site.elevatorDoor.pos_x;z=site.elevatorDoor.pos_z;
        }
        Assert.NotNull(actual);var pose=MissionComputerAnchors.For(actual);
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-machines-disturbing-trends-selection.json"),JsonSerializer.Serialize(new{LayoutSeed=201101,Mission=mission.id,Phases=records,Source=actual.spot,SourceGoid=MissionSites.Goid(actual.spot),ConcreteGoid=actual.goid,Pose=new{pose.X,pose.Y,pose.Z,Orientation=Convert.ToHexString(pose.Orientation)},Scope="Offline actual Choose/PlaceObjects; no acceptance, transport, completion or forced object"},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Assert.Equal((ushort)3,actual.goid);
        bool eligible=MissionSites.Goid(actual.spot)==3;
        using var file=File.OpenText(Path.Combine(Root,"data","mission_computer_anchors.csv"));eligible|=MissionComputerAnchors.Read(file).TryResolve(actual.spot,actual.goid,out _);
        Assert.True(eligible,"Normal source selection must be bound or covered by merged native anchors; otherwise stop this package");
        Assert.Equal((ushort)1407,MissionSites.Goid(actual.spot));Assert.Equal(0x0d1001edu,actual.spot.mxoStaticId);
        Assert.Equal((28740d,881d,13200d),(pose.X,pose.Y,pose.Z));Assert.Equal("00000000F204353F00000000F40435BF",Convert.ToHexString(pose.Orientation));
        Assert.Equal((29000d,1026d,13460d),(actual.spot.pos_x,actual.spot.pos_y,actual.spot.pos_z));
    }
}
