using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;
using HeadlessClient;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
[Collection("DelayedActions")]
public class Mainframe357AdmissionTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get {string p=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(p,"data","missions")))p=Path.GetDirectoryName(p)!;return p;}}
    static Mission Mission(){var d=new XmlDocument();d.Load(Path.Combine(Repo,"data","missions","merovingian_1_2_6_electromagnetic.xml"));return MissionXml.Parse(d.DocumentElement!);}
    static readonly Lazy<List<MissionSite>> Sites=new(()=>
    {
        var objects=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Skip(1).Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            objects.Add(new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=NumericalUtils.ByteArrayToUint32(Convert.FromHexString(f[2]),1),staticId=NumericalUtils.ByteArrayToUint32(Convert.FromHexString(f[3]),1),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(objects,1);
    });
    static void Nav(){SectorProperties.Load(Path.Combine(Repo,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data","navmesh"));}
    [Fact] public void ExactInstalledModelAndClass20RangeAreSeparateFromActionOffers()
    {
        Assert.True(MissionOpenFloorProfiles.TryModel(357,out var m));Assert.Equal((-60d,-110d,-40d),(m.Min.X,m.Min.Y,m.Min.Z));Assert.Equal((60d,110d,40d),(m.Max.X,m.Max.Y,m.Max.Z));Assert.Equal(-110,m.SupportMinY);
        Assert.True(MissionOpenFloorProfiles.TryUsable(357,out var u));Assert.Equal((60d,110d,40d),(u.HalfExtents.X,u.HalfExtents.Y,u.HalfExtents.Z));Assert.True(u.AssumesInitialClientConfiguration);Assert.Equal(400,u.LowerBound);
        var p=MissionOpenFloorUsableRange.Profile.InitialComputerActions(357,new byte[]{8,4,5},u.HalfExtents,"sameclass20 geometric ownership");Assert.Equal(new byte[]{4,5,8},p.Uses);
        Assert.Throws<ArgumentException>(()=>MissionOpenFloorUsableRange.Profile.InitialClient400(357,3,u.HalfExtents,"not a proven computer action"));
        Assert.True(MissionOpenFloorAdmission.Requires(Mission().phases[0]));
        Assert.True(MissionOpenFloorAdmission.Target(357,new(){type=BitConverter.GetBytes((ushort)1415)}));Assert.False(MissionOpenFloorAdmission.Target(357,new(){type=BitConverter.GetBytes((ushort)357)}));
    }
    [Fact] public void ExistingProxyEvidenceIsUnchanged()
    {
        Assert.Equal("c38150d02111f6cfa51e1f2ffd4b18b0b8e0ea9aaf9783caa18287ffbfcd0402",MissionOpenFloorProfiles.ModelSourceSha256);
        foreach(ushort id in new ushort[]{3,548,552}){Assert.True(MissionOpenFloorProfiles.TryModel(id,out var m));Assert.Equal("actual asset/clip conservative profile:"+MissionOpenFloorProfiles.ModelSourceSha256,m.Evidence);}
        Assert.True(MissionOpenFloorProfiles.TryUsable(3,out var c));Assert.Equal("625d6b20 omitted/nonpositive branch; startup configurable UsableExecuteMaxDist400; current client setting unobserved",c.Evidence);
    }
    [Fact] public void Normal301206CatalogChoiceObtainsAReplayableWitnessWithoutForcingSource()
    {
        Nav();var m=Mission();var phase=m.phases[0];var random=new Random(301101);int npcs=m.npcs.Count+MissionStaging.SpotsForWaves(phase);
        var site=MissionSites.Choose(Sites.Value,phase.site,17000,2300,npcs,MissionStaging.PlacesNeeded(phase),random,admissible:s=>MissionInteriorSites.Eligible(phase,s,out _,npcs))
            ??MissionSites.Choose(Sites.Value,"Any",17000,2300,npcs,MissionStaging.PlacesNeeded(phase),random,admissible:s=>MissionInteriorSites.Eligible(phase,s,out _,npcs));Assert.NotNull(site);
        var choices=MissionStaging.SelectObjects(phase,site!,random).Select(MissionOpenFloorChoice.From).ToArray();Assert.Equal((ushort)357,Assert.Single(choices).Goid);
        // Mirror normal TryPrepare's selected elevator context, without changing chooser/source/RNG.
        Assert.True(MissionElevatorPoses.TryCreate(site!,choices,out var elevator,out string why),why);Assert.NotNull(elevator);
        var selected=new MissionOpenFloorPendingAdmission(1,m.id,"mainframe-fixed",0,100,MissionOpenFloorSite.From(site!),choices,null,elevator);
        Assert.True(MissionOpenFloorAdmission.TryGeometry(m,0,site!,choices,out var poses,out why,selected),why);var fixedPose=Assert.Single(poses).Value;
        var state=new MissionObjectState{def=phase.objects[0],spot=choices[0].ReadSpot(),goid=357,OpenFloor=fixedPose};
        Assert.True(MissionOpenFloorRequests.TryCreate(state.spot,357,site!,1,out var request,out why,phase,1,elevator),why);Assert.True(fixedPose.Replay(request,301206,0,state,out why),why);
        var at=MissionObjectPoses.For(state);double d2=Enumerable.Range(0,3).Sum(i=>Math.Pow(fixedPose.Standing[i]-fixedPose.Centre[i],2));Assert.True(d2<=MissionRuntime.EscortArrivalUnits*MissionRuntime.EscortArrivalUnits);
        var surface=request.Surfaces.Single(s=>s.Identity.Equals(new G.Id(fixedPose.Surface[0],fixedPose.Surface[1],fixedPose.Surface[2])));
        Assert.Equal(surface.FloorX*at.X+surface.FloorZ*at.Z+surface.FloorC,request.Model.SupportMinY+at.Y,6);
        var p=new MissionProgress(m){site=site,openFloorCheckpoint=true,openFloorResolvedPhase=0,persistenceId="mainframe-fixed",startedAt=100};p.sites.Add(site!);p.objects.Add(1,state);
        // MissionStaging.Stage stores this same selected witness before Capture/restore.
        p.elevatorWitnesses.Add(elevator!.SiteFingerprint,elevator);
        var saved=p.Capture(23,Array.Empty<Mob>());Assert.Single(saved.ElevatorWitnesses!);
        var missing=JsonSerializer.Deserialize<MissionProgress.SavedState>(JsonSerializer.Serialize(saved,Fields),Fields)!;missing.ElevatorWitnesses=null;
        var missingBefore=JsonSerializer.Serialize(missing,Fields);
        var missingError=Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreFromCatalog(m,missing,new[]{site!}));
        Assert.Contains("MissingMissionElevatorWitness",missingError.Message);Assert.Equal(missingBefore,JsonSerializer.Serialize(missing,Fields));
        var before=JsonSerializer.Serialize(saved,Fields);var restored=MissionProgress.RestoreFromCatalog(m,saved,new[]{site!});Assert.Equal(before,JsonSerializer.Serialize(restored.Capture(23,Array.Empty<Mob>()),Fields));
    }
    [Theory][InlineData(false)][InlineData(true)] public void LegacyFree357FailsClosedWithoutChangingEnteredStateOrItems(bool entered)
    {
        var m=Mission();var source=new StaticWorldObject{metrId=1,sectorID=550,staticId=8139,mxoStaticId=0x22601fcb,type=BitConverter.GetBytes((ushort)1415),pos_x=36980,pos_y=3877,pos_z=31260,quat="0000000000000000000000000000803f"};
        var site=Sites.Value.First(s=>s.objectSpots.Any(o=>o.mxoStaticId==source.mxoStaticId));
        var p=new MissionProgress(m){site=site,entered=entered,persistenceId="old-mainframe",startedAt=100};p.sites.Add(site);p.itemSlots[6807]=7;p.objects.Add(1,new(){def=m.phases[0].objects[0],spot=source,goid=357,shownState=2400});
        var saved=p.Capture(23,Array.Empty<Mob>());Assert.Equal(1,saved.Version);var before=JsonSerializer.Serialize(saved,Fields);Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreFromCatalog(m,saved,new[]{site}));Assert.Equal(before,JsonSerializer.Serialize(saved,Fields));Assert.Equal((ushort)7,p.itemSlots[6807]);Assert.Equal(entered,p.entered);
        p.openFloorCheckpoint=true;var v2=p.Capture(23,Array.Empty<Mob>());Assert.Equal(2,v2.Version);var v2Before=JsonSerializer.Serialize(v2,Fields);Assert.Throws<InvalidOperationException>(()=>MissionProgress.RestoreFromCatalog(m,v2,new[]{site}));Assert.Equal(v2Before,JsonSerializer.Serialize(v2,Fields));
    }
    [Fact] public void ExactFailingTyped1415SourceUsesItsExistingAuditedAnchor()
    {
        var source=new StaticWorldObject{metrId=1,sectorID=550,staticId=8139,mxoStaticId=0x22601fcb,type=BitConverter.GetBytes((ushort)1415),pos_x=36980,pos_y=3877,pos_z=31260,quat="0000000000000000000000000000803f"};
        Assert.True(MissionOpenFloorAnchors.TryCurrent(source,out var anchor,out _));Assert.Equal((38540d,3732d,32820d),(anchor.X,anchor.Y,anchor.Z));Assert.Equal("00000000F30435BF00000000F304353F",Convert.ToHexString(anchor.Orientation));
        Assert.True(MissionOpenFloorSourceModels.TryGet(1,1415,out var presence,out _));Assert.Equal(G.SourceModelPresence.Model6Absent,presence);
        source.pos_x++;Assert.False(MissionOpenFloorAnchors.TryCurrent(source,out _,out _));
    }
    [Fact] public void PendingNextMainframeKeepsItsExactChoiceAndItemsWithoutAdoptingCurrentPhase()
    {
        var d=new XmlDocument();d.LoadXml("<mission id='901' title='Pending Mainframe' description='D'><phase><objective goal='1' type='Survive' seconds='20' text='Wait'/></phase><phase site='Normal'><object id='1' kind='Mainframe'/><objective goal='2' type='Use' object='1' text='Mainframe'/></phase></mission>");var m=MissionXml.Parse(d.DocumentElement!);
        var site=Sites.Value.First(s=>s.objectSpots.Any(o=>o.mxoStaticId==0x22601fcb));var source=site.objectSpots.Single(s=>s.mxoStaticId==0x22601fcb);
        var choice=new MissionOpenFloorChoice(1,357,JsonSerializer.Serialize(source,Fields));
        var p=new MissionProgress(m){persistenceId="pending-next-mainframe",startedAt=100};p.Complete(p.Phase.missionObjectives[0]);p.itemSlots[6807]=7;
        p.pendingOpenFloor=new MissionOpenFloorPendingAdmission(1,m.id,p.persistenceId,1,p.startedAt,MissionOpenFloorSite.From(site),new[]{choice},"deferred");MissionOpenFloorAdmission.MarkPendingCheckpoint(p);
        var saved=p.Capture(23,Array.Empty<Mob>());var before=JsonSerializer.Serialize(saved,Fields);var restored=MissionProgress.RestoreFromCatalog(m,saved,new[]{site});
        Assert.Equal(before,JsonSerializer.Serialize(restored.Capture(23,Array.Empty<Mob>()),Fields));Assert.Empty(restored.objects);Assert.Null(restored.openFloorResolvedPhase);Assert.NotNull(restored.pendingOpenFloor);Assert.Equal((ushort)7,restored.itemSlots[6807]);
    }
    [Fact] public void LegacyNativeBound357RemainsOutsideFreeAdmission()
    {
        var m=Mission();var site=Sites.Value.First(s=>s.objectSpots.Any(o=>o.mxoStaticId==0x22601fcb));var source=new StaticWorldObject{metrId=1,sectorID=550,staticId=8139,mxoStaticId=0x22601fcb,type=BitConverter.GetBytes((ushort)357),pos_x=36980,pos_y=3877,pos_z=31260,quat="0000000000000000000000000000803f"};
        var p=new MissionProgress(m){site=site,persistenceId="native-bound-mainframe",startedAt=100};p.sites.Add(site);p.objects.Add(1,new(){def=m.phases[0].objects[0],spot=source,goid=357});var saved=p.Capture(23,Array.Empty<Mob>());
        Assert.Equal(1,saved.Version);var restored=MissionProgress.RestoreFromCatalog(m,saved,new[]{site});Assert.Null(restored.objects[1].OpenFloor);Assert.Equal(source.pos_x,restored.objects[1].spot.pos_x);
    }
    [Fact] public void ReceiptGuardRejectsSelectedAndDuplicateSeedsBeforeDatabase()
    {
        var c=InteriorAdmissionFixture.Mainframe;Assert.Equal(25,c.Prerequisites.Length);InteriorAdmissionFixture.ValidateReceipts(c);
        Assert.Throws<InvalidDataException>(()=>InteriorAdmissionFixture.ValidateReceipts(c with {Prerequisites=c.Prerequisites.Append(301206).ToArray()}));
        Assert.Throws<InvalidDataException>(()=>InteriorAdmissionFixture.ValidateReceipts(c with {Prerequisites=c.Prerequisites.Append(c.Prerequisites[0]).ToArray()}));
    }
}
