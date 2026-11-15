using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionNativeFloorPreselectionTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static readonly Lazy<List<MissionSite>> Sites=new(()=>
    {
        var objects=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Skip(1).Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            objects.Add(new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(objects,1);
    });
    static void Nav(){SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));}
    static Mission Authored(int mission)
    {
        string name=mission==301204?"merovingian_1_2_4_opportunity_knocks.xml":"merovingian_1_2_12_researching_the_black_arts.xml";
        var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data/missions",name));return MissionXml.Parse(xml.DocumentElement!);
    }
    static MissionSite Exact(int mission)=>Sites.Value.Single(s=>MissionOpenFloorSite.CatalogFingerprint(s)==(mission==301204?
        "1E7943AC49C451F1B85BC2CD7F7E30FEE3D208049A2361C6DBAAE722A60F9945":"8E4A5E0B6AF92666DFF81E812A054EA42783E5E42482C5FD7EFA2832177E7623"));
    static MissionOpenFloorChoice[] FailedChoices(int mission,MissionSite site)=>mission==301204?
        new[]{new MissionOpenFloorChoice(1,326,JsonSerializer.Serialize(site.containers.Single(o=>o.mxoStaticId==0x226006b1),Fields)),new MissionOpenFloorChoice(2,552,JsonSerializer.Serialize(site.objectSpots.Single(o=>o.mxoStaticId==0x2260066b),Fields))}:
        new[]{new MissionOpenFloorChoice(1,548,JsonSerializer.Serialize(site.objectSpots.Single(o=>o.mxoStaticId==0x023003ff),Fields)),new MissionOpenFloorChoice(2,3,JsonSerializer.Serialize(site.objectSpots.Single(o=>o.mxoStaticId==0x023003db),Fields))};
    [Fact] public void Exact213UnsupportedDoorIsRejectedBeforeSelectionWithoutRngOrSiteMutation()
    {
        Nav();var mission=Authored(301204);var phase=mission.phases[1];var site=Exact(301204);string before=MissionOpenFloorSite.CatalogFingerprint(site);
        Assert.Equal(0x2260281au,site.elevatorDoor.mxoStaticId);
        Assert.False(MissionElevatorPoses.TrySelectionContext(site,out var context,out string why));Assert.Null(context);Assert.Equal("MissingOrAmbiguousElevatorRegion",why);
        Assert.False(MissionStaging.TrySourcePools(phase,site,7,out var pools,out why));Assert.Null(pools);Assert.Equal("MissingOrAmbiguousElevatorRegion",why);
        Assert.False(MissionInteriorSites.Eligible(phase,site,out why,7));Assert.Equal("MissingOrAmbiguousElevatorRegion",why);
        Assert.Equal(before,MissionOpenFloorSite.CatalogFingerprint(site));
    }
    [Fact] public void Exact245SourcesFrom57CannotEnterThe78CandidatePool()
    {
        Nav();var phase=Authored(301212).phases[1];var site=Exact(301212);string before=MissionOpenFloorSite.CatalogFingerprint(site);
        Assert.True(MissionElevatorPoses.TrySelectionContext(site,out var context,out string why),why);Assert.Equal((ushort)78,context.Region);
        var bad=FailedChoices(301212,site);foreach(var choice in bad){Assert.False(MissionElevatorPoses.CompatibleSource(site,choice.ReadSpot(),context,out why));Assert.Equal("ElevatorSourceRegionDisagreement",why);}
        Assert.False(MissionStaging.TrySourcePools(phase,site,3,out var pools,out why));Assert.Null(pools);
        Assert.Equal("InsufficientCompatibleElevatorSources",why);
        Assert.False(MissionInteriorSites.Eligible(phase,site,out why,3));Assert.Equal("InsufficientCompatibleElevatorSources",why);
        Assert.Equal(before,MissionOpenFloorSite.CatalogFingerprint(site));
    }
    sealed class Keys:Random
    {
        readonly Queue<int> values;public int Draws;public Keys(params int[] keys){values=new(keys);}public override int Next(){Draws++;return values.Dequeue();}
    }
    static Mission OneComputer(){var xml=new XmlDocument();xml.LoadXml("<mission id='9800' title='Pools' description='D'><phase site='Normal'><object id='1' kind='Computer'/></phase></mission>");return MissionXml.Parse(xml.DocumentElement!);}
    [Fact] public void FilteringUsesOriginalDrawBudgetAndMatchingKindPriority()
    {
        var phase=OneComputer().phases[0];var site=new MissionSite();
        StaticWorldObject Row(uint key,ushort goid)=>new(){mxoStaticId=key,type=BitConverter.GetBytes(goid)};
        var bad=Row(1,1407);var good=Row(2,1407);var other=Row(3,1412);site.objectSpots.AddRange(new[]{bad,good,other});
        var keys=new Keys(1,30,2);var pools=new MissionStaging.SourcePools(null,new[]{good,other});
        var result=Assert.Single(MissionStaging.SelectObjectsFromPools(phase,site,keys,pools));
        Assert.Same(good,result.Spot);Assert.Equal(3,keys.Draws);Assert.Equal(new[]{bad,good,other},site.objectSpots);
        var original=new Keys(1,30,2);var unfiltered=Assert.Single(MissionStaging.SelectObjectsFromPools(phase,site,original,null));Assert.Same(bad,unfiltered.Spot);Assert.Equal(keys.Draws,original.Draws);
        var all=new Keys(1,30,2);Assert.Same(unfiltered.Spot,Assert.Single(MissionStaging.SelectObjectsFromPools(phase,site,all,new(null,site.objectSpots))).Spot);Assert.Equal(original.Draws,all.Draws);
    }
    [Fact] public void Native326RegionComesOnlyFromItsAuditedSignature()
    {
        var raw=Exact(301204).containers.Single(o=>o.mxoStaticId==0x226006b1);var clone=JsonSerializer.Deserialize<StaticWorldObject>(JsonSerializer.Serialize(raw,Fields),Fields)!;
        var state=new MissionObjectState{spot=clone,goid=326};Assert.True(MissionBoundObjectPoses.TryCurrentRegion(state,out var pose,out int region,out string why),why);Assert.Equal(44,region);Assert.Equal((39760d,5682d,28155.490234375d),(pose.X,pose.Y,pose.Z));
        clone.pos_x++;Assert.False(MissionBoundObjectPoses.TryCurrentRegion(state,out _,out _,out why));Assert.Equal("BoundPoseRawSignature",why);
    }
    [Fact] public void NpcOnlyAndEntirelyBoundPhasesDoNotAcquireANewSelectionPolicy()
    {
        var xml=new XmlDocument();xml.LoadXml("<mission id='9801' title='NPC' description='D'><phase site='Normal'><objective goal='1' type='Talk' npc='1' text='Talk'/></phase></mission>");
        var phase=MissionXml.Parse(xml.DocumentElement!).phases[0];var unsupported=Exact(301204);
        Assert.True(MissionStaging.TrySourcePools(phase,unsupported,7,out var npcPools,out _));Assert.Null(npcPools);
        var computer=OneComputer().phases[0];var site=new MissionSite();site.realObjects.Add(new(){type=BitConverter.GetBytes((ushort)3)});
        Assert.True(MissionStaging.TrySourcePools(computer,site,0,out var boundPools,out _));Assert.Null(boundPools);
        var rng=new Keys();Assert.Same(site.realObjects[0],Assert.Single(MissionStaging.SelectObjectsFromPools(computer,site,rng,boundPools)).Spot);Assert.Equal(0,rng.Draws);
    }
    [Fact] public void UndeclaredWorldKeepsTheExistingLegacySelectionContract()
    {
        var site=new MissionSite{metrId=2,elevatorDoor=new(){metrId=2},lobbyDoor=new(){metrId=2}};
        var row=new StaticWorldObject{metrId=2,type=BitConverter.GetBytes((ushort)1407)};site.objectSpots.Add(row);
        Assert.True(MissionStaging.TrySourcePools(OneComputer().phases[0],site,0,out var pools,out _));Assert.Null(pools);
        Assert.Same(row,Assert.Single(MissionStaging.SelectObjectsFromPools(OneComputer().phases[0],site,new Keys(5),pools)).Spot);
    }
    [Fact] public void PreselectionNpcCapacityUsesTheMeasuredComponentInsteadOfTheLegacyRoom()
    {
        Nav();var original=Sites.Value.Single(s=>MissionOpenFloorSite.CatalogFingerprint(s)=="FE1A4A0FAD5BF68CC4633B517E0E855108E109A0FAB99E2D3B5C9BC31ABCAADA");
        // Local fixture isolates the qualified root46 marker; the committed catalog is unchanged.
        var site=new MissionSite{metrId=original.metrId,style=original.style,floorY=original.floorY,minX=original.minX,maxX=original.maxX,minZ=original.minZ,maxZ=original.maxZ,elevatorDoor=original.elevatorDoor,lobbyDoor=original.lobbyDoor,shaft=original.shaft};
        site.objectSpots.AddRange(original.objectSpots);site.containers.AddRange(original.containers);site.realObjects.AddRange(original.realObjects);
        site.npcSpots.Add(new[]{28413.453786483045,1695,14219.695388506345,0d});
        var xml=new XmlDocument();xml.LoadXml("<mission id='9802' title='Measured NPC' description='D'><phase site='Normal'><object id='1' kind='Box'/></phase></mission>");var phase=MissionXml.Parse(xml.DocumentElement!).phases[0];
        Assert.True(MissionElevatorPoses.TrySelectionContext(site,out var context,out string why),why);Assert.Equal((ushort)46,context.Region);
        // Both can allocate under the unchanged800-unit projection; compare their actual destinations.
        Assert.True(MissionInteriorSites.TryAllocate(site,1,out var legacy,out why),why);
        Assert.True(MissionInteriorSites.TryAllocate(site,1,out var measured,out why,context),why);
        var a=Assert.Single(legacy);var b=Assert.Single(measured);Assert.False(a.SequenceEqual(b));
        Assert.True(MissionInteriorSites.Contains(site,new(){x=a[0],y=a[1],z=a[2]}));
        Assert.True(MissionInteriorSites.Contains(site,new(){x=b[0],y=b[1],z=b[2]},context));
        Assert.False(MissionInteriorSites.Contains(site,new(){x=b[0],y=b[1],z=b[2]}));
        Assert.True(NavMesh.Locate(1,b[0],b[1],b[2],out var nav));Assert.Equal((ushort)46,nav.region);
        Assert.True(MissionStaging.TrySourcePools(phase,site,1,out var pools,out why),why);Assert.Equal(context.Region,pools.Elevator.Region);
    }
    [Theory][InlineData(301204,false)][InlineData(301204,true)][InlineData(301212,false)][InlineData(301212,true)]
    public void DurableUnsupportedPendingRemainsByteExactAndReportsSpecificRefusal(int id,bool entered)
    {
        Nav();var actual=Authored(id);var phase=actual.phases[1];var site=Exact(id);var choices=FailedChoices(id,site);
        var xml=new XmlDocument();xml.LoadXml($"<mission id='{id}' title='Pending' description='D'><phase><objective goal='1' type='Survive' seconds='20' text='Wait'/></phase><phase site='Normal'>"+
            string.Concat(phase.objects.Select(o=>$"<object id='{o.id}' kind='{o.kind}'/>"))+"</phase></mission>");var mission=MissionXml.Parse(xml.DocumentElement!);
        var progress=new MissionProgress(mission){persistenceId=new string('a',32),startedAt=100,entered=entered};progress.itemSlots[6791]=7;
        progress.Complete(progress.Phase.missionObjectives.Single());progress.waitingForExit=true;
        progress.pendingOpenFloor=new(1,id,progress.persistenceId,1,100,MissionOpenFloorSite.From(site),choices,"MissingMissionElevatorWitness");MissionOpenFloorAdmission.MarkPendingCheckpoint(progress);
        var saved=progress.Capture(49,Array.Empty<Mob>());string before=JsonSerializer.Serialize(saved,Fields);var restored=MissionProgress.RestoreFromCatalog(mission,saved,new[]{site});
        Assert.Equal(before,JsonSerializer.Serialize(restored.Capture(49,Array.Empty<Mob>()),Fields));Assert.Equal((ushort)7,restored.itemSlots[6791]);Assert.Null(restored.pendingOpenFloor.Elevator);
        Assert.False(MissionOpenFloorAdmission.TryGeometry(mission,1,site,choices,out _,out string why,restored.pendingOpenFloor));
        Assert.Equal(id==301204?"MissingOrAmbiguousElevatorRegion":"ElevatorSourceRegionDisagreement",why);
        Assert.Equal(before,JsonSerializer.Serialize(restored.Capture(49,Array.Empty<Mob>()),Fields));
    }
}
