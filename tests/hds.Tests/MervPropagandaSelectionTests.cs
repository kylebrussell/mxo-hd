using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class MervPropagandaSelectionTests
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
    [Fact] public void FivePostersHaveDistinctNormallySelectedSources()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));
        var sites=MissionSites.Build(SourceObjects(),1);var hoods=MissionSites.LoadNeighbourhoods(Path.Combine(Root,"data","slums_neighbourhoods.csv"));
        foreach(var s in sites)if(s.elevatorDoor!=null&&hoods.TryGetValue(s.elevatorDoor.sectorID,out string? name))s.neighbourhood=name;
        var doc=new XmlDocument();doc.Load(Path.Combine(Root,"data","missions","merovingian_1_2_2_propaganda.xml"));var mission=MissionXml.Parse(doc.DocumentElement!);
        var rows=new List<object>();var used=new List<MissionSite>();
        for(int phaseIndex=0;phaseIndex<2;phaseIndex++)
        {
            var phase=mission.phases[phaseIndex];int seed=MissionStaging.QaSiteSeed("1","mxotest_offline_source","301101",mission.id,phaseIndex)!.Value;var rng=new Random(seed);
            int npcs=mission.npcs.Count(n=>n.at==phaseIndex+1)+MissionStaging.SpotsForWaves(phase);
            var site=MissionSites.Choose(sites,phase.site,17000,2300,npcs,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase))
                ??MissionSites.Choose(sites,"Any",17000,2300,npcs,MissionStaging.PlacesNeeded(phase),rng,used,requiredFurniture:MissionStaging.FurnitureNeeded(phase));
            Assert.NotNull(site);used.Add(site);var progress=new MissionProgress(mission){phaseIndex=phaseIndex,site=site};
            var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new(){missionInstanceCounter=47};client.viewMan=new();
            foreach(var def in phase.objects)client.viewMan.GetViewForEntityAndGo(987000047000UL+(ulong)(phaseIndex*50+def.id),548).viewCreated=true;
            typeof(MissionStaging).GetMethod("PlaceObjects",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{client,progress,phaseIndex==0?new Random(seed):rng});
            Assert.Equal(phaseIndex==0?3:5,progress.objects.Count);
            Assert.Equal(progress.objects.Count,progress.objects.Values.Select(o=>o.spot.mxoStaticId).Distinct().Count());
            Assert.All(progress.objects.Values,o=>Assert.Equal((ushort)548,o.goid));
            rows.Add(new{Phase=phaseIndex+1,Seed=seed,Site=site,Selected=progress.objects.OrderBy(x=>x.Key).Select(x=>new{Object=x.Key,SourceGoid=MissionSites.Goid(x.Value.spot),Concrete=x.Value.goid,Spot=x.Value.spot})});
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-merv-propaganda-offline-selection.json"),JsonSerializer.Serialize(new{Input="Fixed17000/2300 per offline phase; actual normal Choose/PlaceObjects, seed301101, no forced spots. Live transition choices may differ.",Phases=rows},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
    }
}
