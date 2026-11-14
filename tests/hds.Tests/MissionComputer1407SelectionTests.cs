using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using hds;
using HeadlessClient;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class MissionComputer1407SelectionTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static IEnumerable<StaticWorldObject> SourceObjects()
    {
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Root,"data",file)).Skip(1))
        {
            if(string.IsNullOrWhiteSpace(line))continue;
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            yield return new StaticWorldObject{metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]};
        }
    }
    private sealed class PoolIndexRandom(int index):Random
    {
        public int Max;
        public override int Next(int maxValue){Max=maxValue;return index%maxValue;}
    }
    [Fact] public void ARealNeighbourhoodRetrievalCanNormallySelect1407()
    {
        MissionContacts.Load(Path.Combine(Root,"data","mission_contacts.csv"));MissionContactNpcs.Load(Path.Combine(Root,"data","mission_contact_npcs.csv"));
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        var sites=MissionSites.Build(SourceObjects(),1);var hoods=MissionSites.LoadNeighbourhoods(Path.Combine(Root,"data","slums_neighbourhoods.csv"));
        foreach(var site in sites)if(site.elevatorDoor!=null&&hoods.TryGetValue(site.elevatorDoor.sectorID,out string? name))site.neighbourhood=name;
        var place=typeof(MissionStaging).GetMethod("PlaceObjects",BindingFlags.Static|BindingFlags.NonPublic)!;
        var inventory=new List<object>();object? result=null;
        foreach(var contact in MissionContactNpcs.All.Where(c=>c.metrId==1&&c.Contact.IsNeighbourhood).OrderBy(c=>c.contactId))
        {
            var approach=MissionContactNpcs.InFrontOf(contact);
            if(!NavMesh.Locate(1,approach.x,approach.y,approach.z,out var nav)||Math.Abs(nav.floorY+95-approach.y)>150)
            {inventory.Add(new{Contact=contact.contactId,Name=contact.Contact.name,Reason="source approach has no same-floor NAV"});continue;}
            var mission=NeighbourhoodMissions.Build(contact.Contact,contact).Single(m=>m.id==3000+contact.contactId*10+2);var phase=mission.phases[0];
            int npcs=mission.npcs.Count(n=>n.at==1)+MissionStaging.SpotsForWaves(phase);var neighbourhoods=phase.neighbourhoods.Count>0?phase.neighbourhoods:mission.neighbourhoods;
            MissionSite? Choose(Random rng)=>MissionSites.Choose(sites,phase.site,approach.x,approach.z,npcs,MissionStaging.PlacesNeeded(phase),rng,neighbourhoods:neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase))
                ??MissionSites.Choose(sites,"Any",approach.x,approach.z,npcs,MissionStaging.PlacesNeeded(phase),rng,neighbourhoods:neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase));
            var probe=new PoolIndexRandom(0);Choose(probe);var pool=Enumerable.Range(0,probe.Max).Select(i=>Choose(new PoolIndexRandom(i))).ToArray();
            int possible=pool.Count(s=>s!=null&&s.objectSpots.Any(o=>MissionSites.Goid(o)==1407));
            inventory.Add(new{Contact=contact.contactId,Name=contact.Contact.name,Mission=mission.id,Approach=new[]{(double)approach.x,nav.floorY+95,approach.z},Pool=pool.Length,With1407=possible});
            if(possible==0)continue;
            for(int seed=0;seed<1000;seed++)
            {
                int effective=MissionStaging.QaSiteSeed("1","mxotest_offline_source",seed.ToString(CultureInfo.InvariantCulture),mission.id,0)!.Value;
                var site=Choose(new Random(effective));if(site==null||!site.objectSpots.Any(o=>MissionSites.Goid(o)==1407))continue;
                var progress=new MissionProgress(mission){site=site};
                var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new ClientData{missionInstanceCounter=47};client.viewMan=new ViewManager();
                // Offline transport suppression only; source spot selection is the unchanged production picker.
                client.viewMan.GetViewForEntityAndGo(987000047001UL,3).viewCreated=true;
                place.Invoke(null,new object[]{client,progress,new Random(effective)});
                var state=progress.objects[1];if(MissionSites.Goid(state.spot)!=1407)continue;
                Assert.Equal((ushort)3,state.goid);
                result=new{Seed=seed,Mission=mission.id,Contact=contact.contactId,ContactName=contact.Contact.name,ContactOrg=contact.Contact.org,Approach=new[]{(double)approach.x,nav.floorY+95,approach.z},SourceSpot=state.spot,ConcreteGoid=state.goid,Site=site,
                    Scope="Actual offline canonical Retrieval/Choose/PlaceObjects; transport suppressed, no live acceptance/completion claim"};break;
            }
            if(result!=null)break;
        }
        string path=Path.Combine(Path.GetTempPath(),"mxo-computer-1407-real-contact-selection.json");
        File.WriteAllText(path,JsonSerializer.Serialize(new{Inventory=inventory,Result=result},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Console.WriteLine("1407 contact selection proof: "+path);Assert.NotNull(result);
        var proof=JsonSerializer.SerializeToElement(result,new JsonSerializerOptions{IncludeFields=true});
        Assert.Equal(8u,proof.GetProperty("Contact").GetUInt32());Assert.Equal(3082,proof.GetProperty("Mission").GetInt32());Assert.Equal(2,proof.GetProperty("Seed").GetInt32());
        Assert.Equal(133170595u,proof.GetProperty("SourceSpot").GetProperty("mxoStaticId").GetUInt32());
    }
}
