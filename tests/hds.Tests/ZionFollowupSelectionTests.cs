using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class ZionFollowupSelectionTests
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
    [Fact] public void ConfiscationNormallySelectsTwoDistinctSupportedComputers()
    {
        MissionContacts.Load(Path.Combine(Root,"data","mission_contacts.csv"));SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));
        var all=Directory.GetFiles(Path.Combine(Root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
        var prior=new[]{1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202}.ToHashSet();
        var contact=MissionContacts.ForOrg(1,0);var offers=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),prior);
        var mission=Assert.Single(offers.Where(m=>m.plot));Assert.Equal(101203,mission.id);Assert.Contains(offers,m=>m.id==20001&&!m.plot);
        var sites=MissionSites.Build(SourceObjects(),1);var hoods=MissionSites.LoadNeighbourhoods(Path.Combine(Root,"data","slums_neighbourhoods.csv"));
        foreach(var site in sites)if(site.elevatorDoor!=null&&hoods.TryGetValue(site.elevatorDoor.sectorID,out string? name))site.neighbourhood=name;
        var phase=mission.phases[0];int npcs=mission.npcs.Count(n=>n.at==1)+MissionStaging.SpotsForWaves(phase);
        int effective=MissionStaging.QaSiteSeed("1","mxotest_offline_source","101203",mission.id,0)!.Value;
        var neighbourhoods=phase.neighbourhoods.Count>0?phase.neighbourhoods:mission.neighbourhoods;
        var siteChosen=MissionSites.Choose(sites,phase.site,17000,2300,npcs,MissionStaging.PlacesNeeded(phase),new Random(effective),neighbourhoods:neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase))
            ??MissionSites.Choose(sites,"Any",17000,2300,npcs,MissionStaging.PlacesNeeded(phase),new Random(effective),neighbourhoods:neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase));
        Assert.NotNull(siteChosen);var progress=new MissionProgress(mission){site=siteChosen};
        var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new(){missionInstanceCounter=47};client.viewMan=new();
        // Suppress transport only for these offline views; actual Choose and PlaceObjects remain unchanged.
        foreach(int id in new[]{1,2})client.viewMan.GetViewForEntityAndGo(987000047000UL+(ulong)id,3).viewCreated=true;
        typeof(MissionStaging).GetMethod("PlaceObjects",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{client,progress,new Random(effective)});
        using var reader=File.OpenText(Path.Combine(Root,"data","mission_computer_anchors.csv"));var index=MissionComputerAnchors.Read(reader);
        var states=progress.objects.OrderBy(p=>p.Key).Select(p=>new{Object=p.Key,Concrete=p.Value.goid,SourceGoid=MissionSites.Goid(p.Value.spot),Spot=p.Value.spot,Supported=index.TryResolve(p.Value.spot,p.Value.goid,out var pose),Pose=pose}).ToArray();
        string path=Path.Combine(Path.GetTempPath(),"mxo-zion-followup-selection.json");File.WriteAllText(path,JsonSerializer.Serialize(new{Mission=mission.id,Seed=101203,EffectiveSeed=effective,Approach=new[]{17000,495,2300},Site=siteChosen,Objects=states,Scope="Offline actual contact offer/Choose/PlaceObjects; no live acceptance, no forced source spots"},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Assert.Equal(2,states.Length);Assert.Equal(2,states.Select(x=>x.Spot.mxoStaticId).Distinct().Count());Assert.All(states,x=>Assert.True(x.Supported,$"Object{x.Object} selected unsupported source GOID{x.SourceGoid},key{x.Spot.mxoStaticId:x8}; see {path}"));
        Assert.Equal(0x0d100189u,states[0].Spot.mxoStaticId);Assert.Equal((28325d,1280d,12095d),(states[0].Pose!.X,states[0].Pose.Y,states[0].Pose.Z));
        Assert.Equal(0x0d100166u,states[1].Spot.mxoStaticId);Assert.Equal((28340d,1281d,12900d),(states[1].Pose!.X,states[1].Pose.Y,states[1].Pose.Z));
        Assert.All(states,x=>Assert.Equal("00000000f204353f00000000f40435bf",Convert.ToHexString(x.Pose!.Orientation).ToLowerInvariant()));
    }
    [Fact] public void EveryPhaseHasAnExplicitOfflineSourceAndPrototypeInventory()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));
        var sites=MissionSites.Build(SourceObjects(),1);var hoods=MissionSites.LoadNeighbourhoods(Path.Combine(Root,"data","slums_neighbourhoods.csv"));
        foreach(var site in sites)if(site.elevatorDoor!=null&&hoods.TryGetValue(site.elevatorDoor.sectorID,out string? name))site.neighbourhood=name;
        using var reader=File.OpenText(Path.Combine(Root,"data","mission_computer_anchors.csv"));var index=MissionComputerAnchors.Read(reader);
        var place=typeof(MissionStaging).GetMethod("PlaceObjects",BindingFlags.Static|BindingFlags.NonPublic)!;var rows=new List<object>();var sourceGoids=new List<ushort>();int objects=0;
        foreach(int id in new[]{101203,101204,101205,101206,101207})
        {
            var file=Directory.GetFiles(Path.Combine(Root,"data","missions"),"zion_1_2_*.xml").Single(f=>{var d=new XmlDocument();d.Load(f);return int.Parse(d.DocumentElement!.GetAttribute("id"))==id;});
            var doc=new XmlDocument();doc.Load(file);var mission=MissionXml.Parse(doc.DocumentElement!);var used=new List<MissionSite>();
            for(int phaseIndex=0;phaseIndex<mission.phases.Count;phaseIndex++)
            {
                var phase=mission.phases[phaseIndex];int effective=MissionStaging.QaSiteSeed("1","mxotest_offline_source","101203",id,phaseIndex)!.Value;var rng=new Random(effective);
                int npcs=mission.npcs.Count(n=>n.at==phaseIndex+1)+MissionStaging.SpotsForWaves(phase);var neighbourhoods=phase.neighbourhoods.Count>0?phase.neighbourhoods:mission.neighbourhoods;
                var site=MissionSites.Choose(sites,phase.site,17000,2300,npcs,MissionStaging.PlacesNeeded(phase),rng,used,neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase))
                    ??MissionSites.Choose(sites,"Any",17000,2300,npcs,MissionStaging.PlacesNeeded(phase),rng,used,neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase));
                Assert.NotNull(site);used.Add(site);var progress=new MissionProgress(mission){phaseIndex=phaseIndex,site=site};
                var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new(){missionInstanceCounter=47};client.viewMan=new();
                foreach(var def in phase.objects)foreach(ushort goid in site.containers.Concat(site.realObjects).Select(MissionSites.Goid).Append((ushort)3).Distinct())
                    client.viewMan.GetViewForEntityAndGo(987000047000UL+(ulong)(phaseIndex*50+def.id),goid).viewCreated=true;
                // Preview's first phase gets a fresh stage RNG; later phases consume Choose on that RNG.
                place.Invoke(null,new object[]{client,progress,phaseIndex==0?new Random(effective):rng});
                Assert.Equal(phase.objects.Count,progress.objects.Count);var selected=new List<object>();
                foreach(var pair in progress.objects.OrderBy(x=>x.Key))
                {
                    var state=pair.Value;ushort source=MissionSites.Goid(state.spot);sourceGoids.Add(source);objects++;
                    Assert.NotEqual((ushort)1410,source);bool supported=index.TryResolve(state.spot,state.goid,out var pose);
                    if(state.def.kind=="Computer")Assert.True(supported,"Every selected free computer requires the reviewed typed parent index");
                    selected.Add(new{Object=pair.Key,Kind=state.def.kind,SourceGoid=source,Concrete=state.goid,Bound=source==state.goid,Spot=state.spot,ComputerAnchorSupported=supported,Pose=pose});
                }
                rows.Add(new{Mission=id,Phase=phaseIndex,RequestedStyle=phase.site,Site=site,Selected=selected});
            }
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-zion-followup-all-phases.json"),JsonSerializer.Serialize(new{Input="Fixed source Mara17000/2300 for EACH offline phase; normal Choose/PlaceObjects and per-mission exclusions. Not a live transition replay; fresh actual selections may differ.",Phases=rows,SourceGoids=sourceGoids},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Assert.Equal(8,rows.Count);Assert.Equal(3,objects);Assert.DoesNotContain((ushort)1410,sourceGoids);
    }

}
