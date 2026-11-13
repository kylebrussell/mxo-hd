using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
public class ZionPropagandaSelectionTests
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
    [Fact] public void ExistingZionSeedNormallyAllocatesSixThenFourDistinctObjects()
    {
        var xml=new XmlDocument();xml.Load(Path.Combine(Root,"data","missions","zion_1_2_2_the_power_of_propaganda.xml"));var mission=MissionXml.Parse(xml.DocumentElement!);
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var sites=MissionSites.Build(SourceObjects(),1);var used=new List<MissionSite>();var records=new List<object>();double x=17000,z=2300;
        for(int phaseIndex=0;phaseIndex<2;phaseIndex++)
        {
            var phase=mission.phases[phaseIndex];int seed=MissionStaging.QaSiteSeed("1","mxotest_offline_source","101101",mission.id,phaseIndex)!.Value;var rng=new Random(seed);
            int count=mission.npcs.Count(n=>n.at==phaseIndex+1)+MissionStaging.SpotsForWaves(phase);
            var site=MissionSites.Choose(sites,phase.site,x,z,count,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase))
                ??MissionSites.Choose(sites,"Any",x,z,count,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase));
            Assert.NotNull(site);used.Add(site);var progress=new MissionProgress(mission){site=site,phaseIndex=phaseIndex};
            var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new ClientData{missionInstanceCounter=47};client.viewMan=new ViewManager();
            foreach(var def in phase.objects)client.viewMan.GetViewForEntityAndGo(987000047000UL+(ulong)(phaseIndex*50+def.id),548).viewCreated=true;
            // The first phase reuses its normal preview; Stage resets the RNG without re-choosing that cached site.
            var allocationRng=phaseIndex==0?new Random(seed):rng;
            typeof(MissionStaging).GetMethod("PlaceObjects",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{client,progress,allocationRng});
            records.Add(new{Phase=phaseIndex,Approach=new[]{x,z},EffectiveSeed=seed,Site=site,Objects=progress.objects.Values.Select(o=>new{o.def.id,o.goid,SourceGoid=MissionSites.Goid(o.spot),Spot=o.spot}).ToArray()});
            File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-zion-propaganda-offline-selection.json"),JsonSerializer.Serialize(new{LayoutSeed=101101,Mission=mission.id,Phases=records,Scope="Offline normal Choose/PlaceObjects; transport suppressed; no forced spots, live acceptance or native placement claim"},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
            int wanted=phaseIndex==0?6:4;Assert.Equal(wanted,progress.objects.Count);
            Assert.Equal(wanted,progress.objects.Values.Select(o=>o.spot.mxoStaticId).Distinct().Count());
            Assert.Equal(wanted,progress.objects.Values.Select(o=>(o.spot.pos_x,o.spot.pos_y,o.spot.pos_z)).Distinct().Count());
            Assert.All(progress.objects.Values,o=>Assert.Equal((ushort)548,o.goid));
            x=site.elevatorDoor.pos_x;z=site.elevatorDoor.pos_z;
        }
    }
}
