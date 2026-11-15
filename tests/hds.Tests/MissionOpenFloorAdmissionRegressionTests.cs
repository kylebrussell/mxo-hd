using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using hds;
using hds.shared;
using G=hds.MissionOpenFloorResolver;

namespace hds.Tests;
[Collection("DelayedActions")]
public class MissionOpenFloorAdmissionRegressionTests
{
    private static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    private static string Repo {get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds","Hardline Dreams MxO server.csproj")))p=Path.GetDirectoryName(p)!;return p;}}
    private static void LoadNav(){SectorProperties.Load(Path.Combine(Repo,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data","navmesh"));}
    private static Mission Definition(bool boxes=true)
    {
        var xml=new XmlDocument();xml.LoadXml("<mission id='901' title='Legacy Box snapshot boundary' description='D' plot='1' exp='50' info='60'>"+
            (boxes?"<phase site='Normal'><object id='1' kind='Box'/><objective goal='1' type='Use' object='1' text='Box'/></phase><phase site='Normal'><object id='1' kind='Box'/><objective goal='1' type='Use' object='1' text='Box'/></phase><phase><objective goal='2' type='Survive' seconds='20' text='Wait'/></phase>":
                "<phase><objective goal='1' text='Wait' type='Survive' seconds='20'/></phase>")+"</mission>");return MissionXml.Parse(xml.DocumentElement!);
    }
    private static (MissionSite Site,StaticWorldObject Source) Scene(int index=2)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"tests","hds.Tests","MissionOpenFloorAdmissionLegacySites.json")));
        return ReadScene(doc.RootElement.GetProperty("Cases")[index]);
    }
    private static (MissionSite Site,StaticWorldObject Source) ReadScene(JsonElement c)
    {
        var j=c.GetProperty("Site");
        T Read<T>(JsonElement e)=>JsonSerializer.Deserialize<T>(e.GetRawText(),Fields)!;
        var site=new MissionSite{style=j.GetProperty("style").GetString()!,metrId=j.GetProperty("metrId").GetUInt16(),floorY=j.GetProperty("floorY").GetDouble(),
            minX=j.GetProperty("minX").GetDouble(),maxX=j.GetProperty("maxX").GetDouble(),minZ=j.GetProperty("minZ").GetDouble(),maxZ=j.GetProperty("maxZ").GetDouble(),
            elevatorDoor=Read<StaticWorldObject>(j.GetProperty("elevatorDoor")),lobbyDoor=Read<StaticWorldObject>(j.GetProperty("lobbyDoor")),shaft=Read<List<StaticWorldObject>>(j.GetProperty("shaft")),neighbourhood=j.GetProperty("neighbourhood").GetString()!};
        site.npcSpots.AddRange(Read<double[][]>(j.GetProperty("npcSpots")));site.objectSpots.AddRange(Read<StaticWorldObject[]>(j.GetProperty("objectSpots")));
        site.containers.AddRange(Read<StaticWorldObject[]>(j.GetProperty("containers")));site.realObjects.AddRange(Read<StaticWorldObject[]>(j.GetProperty("realObjects")));
        return(site,Read<StaticWorldObject>(c.GetProperty("Object").GetProperty("Spot")));
    }
    private static readonly Lazy<List<MissionSite>> Catalog=new(()=>
    {
        // Reconstruct the exact normal catalog once. This object-only floor is the PR203 native
        // compatibility case at the ordinary Ride root Y2095. Claude retracted the PR202 Y2145
        // teleport report; unused NPC markers in other prefabs cannot invalidate this empty phase.
        var statics=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Skip(1))
        {
            if(string.IsNullOrWhiteSpace(line))continue;var f=line.Split(',');Assert.Equal(11,f.Length);
            double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            statics.Add(new StaticWorldObject{metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=NumericalUtils.ByteArrayToUint32(Convert.FromHexString(f[2]),1),
                staticId=NumericalUtils.ByteArrayToUint32(Convert.FromHexString(f[3]),1),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),
                pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(statics,1);
    });
    private static readonly Lazy<string> QualifiedCase=new(()=>
    {
        var site=Assert.Single(Catalog.Value.Where(s=>s.elevatorDoor!=null&&s.elevatorDoor.mxoStaticId==0x0d1003bb&&s.elevatorDoor.pos_y==2145&&
            s.objectSpots.Concat(s.containers).Concat(s.realObjects).Any(o=>o.mxoStaticId==0x0d1003dc&&o.pos_x==23940&&o.pos_y==2245&&o.pos_z==15050)));
        var source=Assert.Single(site.objectSpots.Concat(site.containers).Concat(site.realObjects).Where(o=>o.mxoStaticId==0x0d1003dc));
        return JsonSerializer.Serialize(new{Site=site,Object=new{Spot=source}},Fields);
    });
    private static (MissionSite Site,StaticWorldObject Source) QualifiedScene()
    {
        using var doc=JsonDocument.Parse(QualifiedCase.Value);return ReadScene(doc.RootElement);
    }
    private static MissionSite ExactMixedSourceSite()
    {
        var site=Assert.Single(Catalog.Value.Where(s=>s.elevatorDoor?.mxoStaticId==0x47e00161&&s.elevatorDoor.pos_x==46500&&s.elevatorDoor.pos_z==-12000&&s.floorY==2145&&
            s.objectSpots.Any(o=>o.mxoStaticId==0x47e00167)));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{Site=site,Object=new{Spot=site.objectSpots[0]}},Fields));
        return ReadScene(doc.RootElement).Site;
    }
    private static Mission HardlineHunter()
    {
        var xml=new XmlDocument();xml.Load(Directory.GetFiles(Path.Combine(Repo,"data","missions"),"zion_1_2_9*").Single());
        return MissionXml.Parse(xml.DocumentElement!);
    }
    private static Dictionary<uint,bool> KnownLegacyFootprints(MissionSite site)
    {
        Assert.True(MissionInteriorSites.TryRegion(site,out var region,out string why),why);
        Assert.True(MissionOpenFloorProfiles.TryModel(548,out var model));var fits=new Dictionary<uint,bool>();
        foreach(var raw in site.objectSpots.Where(s=>MissionSites.Goid(s)==1412))
            if(MissionOpenFloorAnchors.TryCurrent(raw,out var anchor,out _))
                fits.Add(raw.mxoStaticId,region.CanFit(model,new OpenFloorOrientation(Convert.ToHexString(anchor.Orientation)),new(site.minX,site.maxX,site.minZ,site.maxZ)));
        return fits;
    }
    [Fact] public void ExactHardlineHunterMixedSourcePoolRejectsBeforeOrdinarySelection()
    {
        LoadNav();var site=ExactMixedSourceSite();var phase=HardlineHunter().phases[1];
        Assert.Equal(4,MissionStaging.SpotsForWaves(phase));
        Assert.True(MissionInteriorSites.TryAllocate(site,5,out _,out string allocation),allocation);
        Assert.True(MissionInteriorSites.TryRegion(site,out var region,out string why),why);
        var results=new Dictionary<uint,bool>();
        foreach(var raw in site.objectSpots.Where(s=>MissionSites.Goid(s)==1412))
        {
            Assert.True(MissionOpenFloorRequests.TryCreate(raw,548,site,1,out var request,out why,phase,1),why);
            results.Add(raw.mxoStaticId,region.CanFit(request.Model,new(request.Anchor.QuaternionHex),request.SiteBounds));
        }
        Assert.Equal(4,results.Count);Assert.False(results[0x47e00167]);Assert.Equal(3,results.Count(x=>x.Value));
        Assert.False(MissionElevatorPoses.TrySelectionContext(site,out _,out string nativeWhy));Assert.Equal("ElevatorResolvedEntryRegionDisagreement",nativeWhy);
        Assert.False(MissionInteriorSites.Eligible(phase,site,out why,5));Assert.Equal(nativeWhy,why);
        // Chooser semantics remain unchanged: the impossible row is still normally selectable.
        Assert.Contains(Enumerable.Range(0,64).Select(seed=>MissionStaging.SelectObjects(phase,site,new Random(seed)).Single().Spot.mxoStaticId),id=>id==0x47e00167);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void UnknownSourceAndFittingAlternativeCannotHideProvedImpossibleChoice(bool reverse)
    {
        LoadNav();var site=ExactMixedSourceSite();var phase=HardlineHunter().phases[1];
        // Audit fingerprint no longer matches: this row is unknown, not a negative proof.
        var unknown=site.objectSpots.First(o=>o.mxoStaticId==0x47e00377);unknown.pos_x+=1;
        if(reverse)site.objectSpots.Reverse();
        Assert.False(MissionOpenFloorAnchors.TryCurrent(unknown,out _,out _));
        var m=HardlineHunter();
        Assert.False(MissionOpenFloorAdmission.TryGeometry(m,1,site,new[]{Choice(m,unknown,1)},out _,out string why));Assert.Equal("MissingOrAmbiguousAuditedAnchor",why);
        var bad=site.objectSpots.Single(o=>o.mxoStaticId==0x47e00167);
        Assert.False(MissionOpenFloorAdmission.TryGeometry(m,1,site,new[]{Choice(m,bad,1)},out _,out why));Assert.Equal("NoGeometricBodyCandidates",why);
        var fits=KnownLegacyFootprints(site);var good=site.objectSpots.Single(o=>o.mxoStaticId==fits.First(pair=>pair.Value).Key);
        Assert.True(MissionOpenFloorAdmission.TryGeometry(m,1,site,new[]{Choice(m,good,1)},out _,out why),why);
        Assert.False(fits[bad.mxoStaticId]);Assert.True(fits[good.mxoStaticId]);Assert.DoesNotContain(unknown.mxoStaticId,fits.Keys);
        Assert.False(MissionElevatorPoses.TrySelectionContext(site,out _,out string nativeWhy));Assert.Equal("ElevatorResolvedEntryRegionDisagreement",nativeWhy);
        Assert.False(MissionInteriorSites.Eligible(phase,site,out why,5));Assert.Equal(nativeWhy,why);
        site.objectSpots.Remove(bad);Assert.All(KnownLegacyFootprints(site).Values,Assert.True);
        Assert.False(MissionInteriorSites.Eligible(phase,site,out why,5));Assert.Equal(nativeWhy,why);

    }
    [Fact] public void CommittedImpossiblePendingKeepsLegacyNegativeAndSpecificNativeRefusal()
    {
        LoadNav();var site=ExactMixedSourceSite();var m=HardlineHunter();var p=new MissionProgress(m){startedAt=100,persistenceId="retained-mixed-source"};
        p.Complete(p.Phase.missionObjectives.Single());p.waitingForExit=true;
        var choice=Choice(m,site.objectSpots.Single(o=>o.mxoStaticId==0x47e00167),1);
        p.pendingOpenFloor=new(1,m.id,p.persistenceId,1,p.startedAt,MissionOpenFloorSite.From(site),new[]{choice},"NoGeometricBodyCandidates");
        var c=Client();c.playerData.missionProgress=p;var pending=p.pendingOpenFloor;string raw=JsonSerializer.Serialize(pending,Fields);
        Assert.True(MissionOpenFloorAdmission.PendingValid(p,pending,new[]{site}));
        Assert.False(MissionOpenFloorAdmission.TryGeometry(m,1,site,new[]{choice},out _,out string legacyWhy));Assert.Equal("NoGeometricBodyCandidates",legacyWhy);
        Assert.False(MissionElevatorPoses.TrySelectionContext(site,out _,out string nativeWhy));Assert.Equal("ElevatorResolvedEntryRegionDisagreement",nativeWhy);
        for(int retry=0;retry<2;retry++)
        {
            Assert.False(MissionOpenFloorAdmission.TryPrepare(c,p,1,p,out var plan,out var selected,out var why,new[]{site}));
            Assert.Equal(nativeWhy,why);Assert.Same(pending,selected);Assert.Same(pending,plan.Selection);
            Assert.Same(pending,p.pendingOpenFloor);Assert.Equal(raw,JsonSerializer.Serialize(selected,Fields));Assert.Equal(0,p.phaseIndex);
        }
    }
    [Fact] public void LegacyExactSourceGeometryReplaysDespiteImpossibleUnusedAlternative()
    {
        LoadNav();var site=ExactMixedSourceSite();var m=HardlineHunter();
        Assert.False(MissionInteriorSites.Eligible(m.phases[1],site,out _,5));
        var source=site.objectSpots.Single(s=>s.mxoStaticId==0x47e00377);var choice=Choice(m,source,1);
        Assert.True(MissionOpenFloorAdmission.TryGeometry(m,1,site,new[]{choice},out var poses,out string why),why);
        var p=new MissionProgress(m){phaseIndex=1,site=site,openFloorCheckpoint=true,openFloorResolvedPhase=1,startedAt=100,persistenceId="valid-mixed-source"};
        p.sites.Add(site);p.objects.Add(1,new(){def=m.phases[1].objects.Single(),spot=source,goid=548,entity=123,OpenFloor=poses[1]});
        var saved=RoundTrip(p);string before=JsonSerializer.Serialize(saved,Fields);
        Assert.True(MissionOpenFloorRequests.TryCreate(source,548,site,1,out var request,out why,m.phases[1],1),why);
        Assert.True(poses[1].TryReplay(request,m.id,1,p.objects[1],out _,out why),why);
        Assert.Equal(before,JsonSerializer.Serialize(saved,Fields));Assert.Equal(0x47e00377u,p.objects[1].spot.mxoStaticId);
    }
    [Fact] public void PositiveLegacyGeometryCannotAuthorizeUnsupportedPendingElevator()
    {
        LoadNav();var site=ExactMixedSourceSite();var m=HardlineHunter();var p=new MissionProgress(m){startedAt=100,persistenceId="old-positive-pending"};
        p.Complete(p.Phase.missionObjectives.Single());p.waitingForExit=true;
        var choice=Choice(m,site.objectSpots.Single(o=>o.mxoStaticId==0x47e00377),1);
        Assert.True(MissionOpenFloorAdmission.TryGeometry(m,1,site,new[]{choice},out var legacy,out string why),why);Assert.NotEmpty(legacy);
        var pending=new MissionOpenFloorPendingAdmission(1,m.id,p.persistenceId,1,p.startedAt,MissionOpenFloorSite.From(site),new[]{choice},null);p.pendingOpenFloor=pending;
        string before=JsonSerializer.Serialize(pending,Fields);var c=Client();c.playerData.missionProgress=p;
        Assert.False(MissionElevatorPoses.TrySelectionContext(site,out _,out string nativeWhy));Assert.Equal("ElevatorResolvedEntryRegionDisagreement",nativeWhy);
        for(int retry=0;retry<2;retry++)
        {
            Assert.False(MissionOpenFloorAdmission.TryPrepare(c,p,1,p,out var plan,out var selected,out why,new[]{site}));
            Assert.Equal(nativeWhy,why);Assert.Same(pending,selected);Assert.Empty(plan.Poses);Assert.Equal(0,p.phaseIndex);
            Assert.Equal(before,JsonSerializer.Serialize(pending,Fields));Assert.Null(pending.Elevator);
        }
    }
    [Fact] public void LegacyFixedCorrectedDoorWithoutWitnessRefusesRestoreAndPublication()
    {
        LoadNav();var site=ExactMixedSourceSite();var m=HardlineHunter();var source=site.objectSpots.Single(s=>s.mxoStaticId==0x47e00377);
        Assert.True(MissionOpenFloorAdmission.TryGeometry(m,1,site,new[]{Choice(m,source,1)},out var poses,out string why),why);
        var p=new MissionProgress(m){phaseIndex=1,site=site,openFloorCheckpoint=true,openFloorResolvedPhase=1,startedAt=100,persistenceId="old-fixed-missing-door"};
        p.sites.Add(site);p.objects.Add(1,new(){def=m.phases[1].objects.Single(),spot=source,goid=548,entity=123,OpenFloor=poses[1]});p.itemSlots[6783]=6;
        var saved=RoundTrip(p);string before=JsonSerializer.Serialize(saved,Fields);
        var refused=Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreFromCatalog(m,saved,new[]{site}));Assert.Equal("MissingMissionElevatorWitness",refused.Message);
        Assert.Equal((ushort)6,refused.Progress.itemSlots[6783]);Assert.Equal(before,JsonSerializer.Serialize(saved,Fields));
        Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionElevatorPoses.ResolveOverlay(refused.Progress,site.elevatorDoor).Kind);
    }
    [Fact] public void LowerPriorityImpossibleFurnitureIsNotInTheMatchingSpawnerPool()
    {
        LoadNav();var site=ExactMixedSourceSite();var phase=HardlineHunter().phases[1];
        var impossible=site.objectSpots.Single(s=>s.mxoStaticId==0x47e00167);site.objectSpots.Remove(impossible);site.containers.Add(impossible);
        Assert.False(MissionElevatorPoses.TrySelectionContext(site,out _,out string nativeWhy));Assert.Equal("ElevatorResolvedEntryRegionDisagreement",nativeWhy);
        Assert.False(MissionInteriorSites.Eligible(phase,site,out var why,5));Assert.Equal(nativeWhy,why);
        Assert.All(Enumerable.Range(0,16),seed=>
        {
            var source=MissionStaging.SelectObjects(phase,site,new Random(seed)).Single().Spot;Assert.NotEqual(impossible.mxoStaticId,source.mxoStaticId);
            Assert.True(MissionOpenFloorAdmission.TryGeometry(HardlineHunter(),1,site,new[]{Choice(HardlineHunter(),source,1)},out _,out string legacyWhy),legacyWhy);
        });
    }
    [Fact] public void NativeFirstChoiceIsDeterministicRatherThanAnyReachableAlternative()
    {
        LoadNav();var site=ExactMixedSourceSite();var m=Definition();m.phases[0].objects.Single().kind="Computer";
        Assert.True(MissionInteriorSites.TryRegion(site,out var region,out string why),why);
        var far=new StaticWorldObject{type=BitConverter.GetBytes((ushort)3),quat="0000000000000000000000000000803f",pos_x=1e9,pos_y=2095,pos_z=1e9};
        var near=new StaticWorldObject{type=BitConverter.GetBytes((ushort)3),quat=far.quat,pos_x=46323.223304703366,pos_y=2095,pos_z=-12176.776695296638};
        site.realObjects.Clear();site.realObjects.AddRange(new[]{far,near});
        Assert.False(MissionInteriorSites.Eligible(m.phases[0],site,out why,1));Assert.Equal("NoInteriorNativeUse:Computer",why);
        Assert.Same(far,MissionStaging.SelectObjects(m.phases[0],site,new Random(1)).Single().Spot);
        site.realObjects.Reverse();Assert.True(MissionInteriorSites.Eligible(m.phases[0],site,out why,1),why);
        Assert.Same(near,MissionStaging.SelectObjects(m.phases[0],site,new Random(1)).Single().Spot);
    }
    private static MissionOpenFloorChoice Choice(Mission m,StaticWorldObject source,int phase=0) =>
        new(1,548,JsonSerializer.Serialize(source,Fields));
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerData.setCharID(110);
        c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerInstance=new PlayerCharacter();c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000,495,2300));
        c.messageQueue=new MessageQueue();c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    private static readonly Lazy<MissionOpenFloorPlacement> QualifiedPose=new(()=>
    {
        LoadNav();var m=Definition();var (site,source)=QualifiedScene();
        Assert.True(MissionOpenFloorAdmission.TryGeometry(m,0,site,new[]{Choice(m,source)},out var poses,out string why),why);
        return poses[1]; // Immutable and source/scene-qualified; each test owns separate mutable site/source/state.
    });
    private static (MissionProgress Progress,MissionSite Site) Adopted()
    {
        LoadNav();var m=Definition();var (site,source)=QualifiedScene();var choice=Choice(m,source);
        var p=new MissionProgress(m){site=site,openFloorCheckpoint=true,openFloorResolvedPhase=0,startedAt=100,persistenceId="box-fixture"};p.sites.Add(site);
        p.objects.Add(1,new MissionObjectState{def=m.phases[0].objects[0],spot=choice.ReadSpot(),goid=548,entity=123,OpenFloor=QualifiedPose.Value});return(p,site);
    }
    private static MissionProgress.SavedState RoundTrip(MissionProgress p)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(JsonSerializer.Serialize(p.Capture(23,Array.Empty<Mob>()),Fields),Fields)!;

    [Fact]
    public void V1GoldenJsonAndLegacyReplayRemainByteEquivalent()
    {
        var p=new MissionProgress(Definition(false)){persistenceId="legacy-fixture",startedAt=100};var saved=p.Capture(23,Array.Empty<Mob>());
        string json=JsonSerializer.Serialize(saved,Fields);Assert.Equal(File.ReadAllText(Path.Combine(Repo,"tests","hds.Tests","MissionOpenFloorLegacyV1.json")).TrimEnd(),json);
        Assert.DoesNotContain("Box",json);Assert.DoesNotContain("OpenFloor",json);var again=MissionProgress.Restore(p.mission,JsonSerializer.Deserialize<MissionProgress.SavedState>(json,Fields)!);
        Assert.Equal(json,JsonSerializer.Serialize(again.Capture(23,Array.Empty<Mob>()),Fields));Assert.False(again.openFloorCheckpoint);
    }

    [Fact]
    public void V2ColdRestoreKeepsExactSourceResolvedPoseAndSiteDescriptor()
    {
        var (p,site)=Adopted();var snapshot=RoundTrip(p);Assert.Equal(2,snapshot.Version);Assert.Equal(0,snapshot.OpenFloorCurrentSiteIndex);Assert.Equal(0,snapshot.OpenFloorPhase);
        var restored=MissionProgress.RestoreFromCatalog(p.mission,snapshot,new[]{site});
        Assert.Equal(JsonSerializer.Serialize(snapshot,Fields),JsonSerializer.Serialize(restored.Capture(23,Array.Empty<Mob>()),Fields));
        var a=MissionObjectPoses.For(p.objects[1]);var b=MissionObjectPoses.For(restored.objects[1]);Assert.Equal((a.X,a.Y,a.Z),(b.X,b.Y,b.Z));
        Assert.Equal(JsonSerializer.Serialize(p.objects[1].spot,Fields),JsonSerializer.Serialize(restored.objects[1].spot,Fields));
        Assert.Equal(0UL,restored.objects[1].entity);Assert.True(restored.openFloorCheckpoint);
    }
    [Theory]
    [InlineData("centre")] [InlineData("raw")] [InlineData("mesh")] [InlineData("route")] [InlineData("missing")] [InlineData("site")] [InlineData("policy")] [InlineData("missing-both")] [InlineData("legacy-marker")]
    public void InvalidV2RefusesWithoutLegacyFallbackOrReplanning(string fault)
    {
        var (p,site)=Adopted();var json=JsonNode.Parse(JsonSerializer.Serialize(RoundTrip(p),Fields))!;var pose=json["Objects"]![0]!["OpenFloor"]!;
        if(fault=="centre")pose["Centre"]![0]=999999;
        if(fault=="raw")json["Objects"]![0]!["Spot"]!["pos_x"]=999999;
        if(fault=="mesh")pose["InputFingerprint"]=new string('A',64); // valid SHA shape, wrong canonical input identity
        if(fault=="route")pose["Route"]![0]=999999;
        if(fault=="missing")json["Objects"]![0]!.AsObject().Remove("OpenFloor");
        if(fault=="missing-both") {json["Objects"]![0]!.AsObject().Remove("OpenFloor");json.AsObject().Remove("OpenFloorPhase");}
        if(fault=="legacy-marker")json["OpenFloorLegacyPhase"]=0;
        if(fault=="site")json["OpenFloorSites"]![0]!["Fingerprint"]="not-current";
        if(fault=="policy")pose["Policy"]=99;
        var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(json.ToJsonString(),Fields)!;
        Assert.ThrowsAny<InvalidOperationException>(()=>MissionProgress.RestoreFromCatalog(p.mission,saved,new[]{site}));
    }
    [Fact]
    public void InvalidFixedGeometryCanEnterWorldForExplicitAbortWithoutChangingTheCheckpoint()
    {
        var (p,site)=Adopted();p.itemSlots[6783]=7;
        var json=JsonNode.Parse(JsonSerializer.Serialize(RoundTrip(p),Fields))!;
        json["Objects"]![0]!["OpenFloor"]!["Centre"]![0]=999999;
        var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(json.ToJsonString(),Fields)!;
        string original=JsonSerializer.Serialize(saved,Fields);
        Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreFromCatalog(p.mission,saved,new[]{site}));
        var restored=MissionPersistence.RestoreForWorld(p.mission,saved,new[]{site});
        Assert.NotNull(restored.restoreRefusal);Assert.Equal(p.persistenceId,restored.persistenceId);
        Assert.Equal(p.startedAt,restored.startedAt);Assert.Equal((ushort)7,restored.itemSlots[6783]);
        Assert.Equal(999999,restored.objects[1].OpenFloor!.Centre[0]);
        Assert.Equal(original,JsonSerializer.Serialize(saved,Fields));
        var c=Client();c.playerData.activeMission=p.mission;c.playerData.missionProgress=restored;c.playerData.missionCheckpoint=original;
        bool invoked=false;Assert.False(MissionPersistence.Mutate(c,()=>{invoked=true;return true;}));Assert.False(invoked);
        MissionPersistence.Save(c);Assert.Equal(original,c.playerData.missionCheckpoint);
        MissionRuntime.Reannounce(c);Assert.Equal(original,c.playerData.missionCheckpoint);
        Assert.Empty(c.playerData.missionMobs);Assert.Equal(999999,restored.objects[1].OpenFloor.Centre[0]);
        string moved=MissionPersistence.MoveQuarantinedItem(original,6783,7,8);
        var expected=JsonNode.Parse(original)!;expected["Items"]!["6783"]=8;
        Assert.True(JsonNode.DeepEquals(expected,JsonNode.Parse(moved)));
        Assert.Throws<InvalidOperationException>(()=>MissionPersistence.MoveQuarantinedItem(original,6783,9,8));
    }
    [Fact]
    public void AdoptedHistoryPersistsAcrossLaterNonOpenFloorPhaseButNoCurrentSiteIsInvented()
    {
        var (p,site)=Adopted();p.objects.Clear();p.NextPhase();p.NextPhase();Assert.Null(p.site);
        var saved=RoundTrip(p);Assert.Equal(2,saved.Version);Assert.Null(saved.OpenFloorPhase);Assert.Equal(-1,saved.OpenFloorCurrentSiteIndex);
        var restored=MissionProgress.RestoreFromCatalog(p.mission,saved,new[]{site});Assert.Null(restored.site);Assert.Single(restored.sites);Assert.True(restored.openFloorCheckpoint);
    }
    [Fact]
    public void PendingChoiceRoundTripsAndDurableMetadataCanBeRetriedOrDiscardedWithoutAdvancing()
    {
        var (p,first)=Adopted();p.Complete(p.Phase.missionObjectives[0]);var (next,source)=Scene(3);var choice=Choice(p.mission,source,1);
        p.pendingOpenFloor=new MissionOpenFloorPendingAdmission(1,p.mission.id,p.persistenceId,1,p.startedAt,MissionOpenFloorSite.From(next),new[]{choice},"blocked");
        var saved=RoundTrip(p);var restored=MissionProgress.RestoreFromCatalog(p.mission,saved,new[]{first,next});
        Assert.Equal(0,restored.phaseIndex);Assert.Equal("blocked",restored.pendingOpenFloor!.Refusal);Assert.Equal(choice.RawSpotJson,Assert.Single(restored.pendingOpenFloor.Choices).RawSpotJson);
        var data=new ClientData();Assert.False(MissionOpenFloorAdmission.PendingDurable(data,p.pendingOpenFloor));data.missionCheckpoint=JsonSerializer.Serialize(saved,Fields);
        Assert.True(MissionOpenFloorAdmission.PendingDurable(data,p.pendingOpenFloor));Assert.False(MissionOpenFloorAdmission.PendingDurable(data,p.pendingOpenFloor.WithRefusal("changed")));
        p.pendingOpenFloor=null;Assert.Null(p.Capture(23,Array.Empty<Mob>()).PendingOpenFloor);Assert.Equal(0,p.phaseIndex);
    }
    [Fact]
    public void LegacyV1FreeBoxUpgradesOnlyForPendingChoiceWithoutReplanningItsCurrentPose()
    {
        var (p,first)=Adopted();p.openFloorCheckpoint=false;p.openFloorResolvedPhase=null;p.objects[1].OpenFloor=null;
        var legacy=RoundTrip(p);Assert.Equal(1,legacy.Version);var oldPose=MissionObjectPoses.For(p.objects[1]);
        p.Complete(p.Phase.missionObjectives[0]);var (next,source)=Scene(3);
        p.pendingOpenFloor=new MissionOpenFloorPendingAdmission(1,p.mission.id,p.persistenceId,1,p.startedAt,MissionOpenFloorSite.From(next),new[]{Choice(p.mission,source,1)},"deferred");
        MissionOpenFloorAdmission.MarkPendingCheckpoint(p);var saved=RoundTrip(p);
        Assert.Equal(2,saved.Version);Assert.Equal(0,saved.OpenFloorLegacyPhase);Assert.Null(saved.OpenFloorPhase);Assert.Null(saved.Objects[0].OpenFloor);
        var restored=MissionProgress.RestoreFromCatalog(p.mission,saved,new[]{first,next});var actual=MissionObjectPoses.For(restored.objects[1]);
        Assert.Equal((oldPose.X,oldPose.Y,oldPose.Z),(actual.X,actual.Y,actual.Z));Assert.Null(restored.objects[1].OpenFloor);
        Assert.Equal(JsonSerializer.Serialize(saved,Fields),JsonSerializer.Serialize(restored.Capture(23,Array.Empty<Mob>()),Fields));
        var wrong=RoundTrip(restored);wrong.OpenFloorLegacyPhase=1;
        Assert.Throws<InvalidOperationException>(()=>MissionProgress.RestoreFromCatalog(p.mission,wrong,new[]{first,next}));
        restored.NextPhase();Assert.Null(restored.openFloorLegacyPhase);
    }
    [Fact]
    public void PendingV2UpgradeOfNonOpenFloorPhaseNeedsNoLegacyBoxOrAdoptionMarker()
    {
        var m=Definition();m.phases[0].objects.Clear();var (next,source)=Scene(3);
        var p=new MissionProgress(m){persistenceId="legacy-nonbox",startedAt=100};p.Complete(p.Phase.missionObjectives[0]);
        p.pendingOpenFloor=new MissionOpenFloorPendingAdmission(1,m.id,p.persistenceId,1,p.startedAt,MissionOpenFloorSite.From(next),new[]{Choice(m,source,1)},"deferred");
        MissionOpenFloorAdmission.MarkPendingCheckpoint(p);var saved=RoundTrip(p);Assert.Equal(2,saved.Version);Assert.Null(saved.OpenFloorLegacyPhase);Assert.Null(saved.OpenFloorPhase);
        var restored=MissionProgress.RestoreFromCatalog(m,saved,new[]{next});Assert.Empty(restored.objects);Assert.NotNull(restored.pendingOpenFloor);
    }
    [Theory] [InlineData("world")] [InlineData("life")] [InlineData("transport")] [InlineData("character")] [InlineData("phase")] [InlineData("source")]
    public void StalePreflightCannotBeCommittedAndDoesNotMutateCurrentMission(string fault)
    {
        LoadNav();var c=Client();var m=Definition();var progress=new MissionProgress(m){startedAt=100};var (site,_)=QualifiedScene();
        c.playerData.missionPreview=site;c.playerData.missionPreviewFor=m;ushort counter=c.playerData.missionInstanceCounter;
        Assert.True(MissionOpenFloorAdmission.TryPrepare(c,progress,0,null!,out var plan,out _,out string why,new[]{site}),why);Assert.True(plan.BeforeCurrent(c));
        if(fault=="world")c.playerData.worldGeneration++;
        if(fault=="life")c.playerData.lifeGeneration++;
        if(fault=="transport")c.TransportFence.PublishIncoming(true,()=>{});
        if(fault=="character")c.playerData.setCharID(111);
        if(fault=="phase")progress.phaseIndex=1;
        if(fault=="source")site.minX--;
        Assert.False(plan.BeforeCurrent(c));Assert.Null(c.playerData.missionProgress);Assert.Null(c.playerData.activeMission);Assert.Empty(c.playerData.missionMobs);
        Assert.Equal(counter,c.playerData.missionInstanceCounter);Assert.Empty(c.messageQueue.RPCMessagesQueue);Assert.Empty(c.messageQueue.ObjectMessagesQueue);
    }
    [Fact]
    public void ActualAcceptanceStaleWorldRefusesBeforeCounterTeamAcceptOrNpcMutation()
    {
        var c=Client();c.playerData.setOnWorld(false);ushort counter=c.playerData.missionInstanceCounter;
        Assert.False(MissionHandler.Accept(c,Definition(),0));Assert.Equal(counter,c.playerData.missionInstanceCounter);Assert.Null(c.playerData.activeMission);Assert.Null(c.playerData.missionProgress);Assert.Empty(c.playerData.missionMobs);
        var records=c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(r=>r.content.Skip(r.content[0]>=0x80?2:1).ToArray()).ToArray();
        Assert.DoesNotContain(records,b=>b.Length>1&&b[0]==0x80&&(b[1]==0x8d||b[1]==0x9c));
        Assert.All(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),r=>Assert.Equal(-1,r.rpcNumber));
    }
    [Fact]
    public void WholePhaseMissingCapacityOrDuplicateSourceHasNoPartialPlan()
    {
        LoadNav();var m=Definition();var (site,source)=QualifiedScene();m.phases[0].objects.Add(new MissionObjectDef{id=2,kind="Box"});
        var one=Choice(m,source);Assert.False(MissionOpenFloorAdmission.TryGeometry(m,0,site,new[]{one},out var poses,out _));Assert.Empty(poses);
        var same=new MissionOpenFloorChoice(2,548,one.RawSpotJson);Assert.False(MissionOpenFloorAdmission.TryGeometry(m,0,site,new[]{one,same},out poses,out _));Assert.Empty(poses);
    }
    [Fact]
    public void FailedStagingBatchRestoresDurablePendingChoiceWithoutReplanning()
    {
        var (p,first)=Adopted();p.Complete(p.Phase.missionObjectives[0]);var (next,source)=Scene(3);
        p.pendingOpenFloor=new MissionOpenFloorPendingAdmission(1,p.mission.id,p.persistenceId,1,p.startedAt,MissionOpenFloorSite.From(next),new[]{Choice(p.mission,source,1)},null);
        var durable=RoundTrip(p);var queue=new MessageQueue();
        using(MissionOutput.Begin(queue))
        {
            p.objects.Clear();p.NextPhase();p.site=next;p.sites.Add(next);p.pendingOpenFloor=null;
            queue.addRpcMessage(new byte[]{0x80,0x9e});queue.addObjectMessage(new byte[]{0x03,0x01},false);
            // Existing mutation recovery reloads the LAST committed checkpoint:
            // after a successful metadata admission that is the pending choice.
        }
        var restored=MissionProgress.RestoreFromCatalog(p.mission,durable,new[]{first,next});
        Assert.Empty(queue.RPCMessagesQueue);Assert.Empty(queue.ObjectMessagesQueue);Assert.Equal(0,restored.phaseIndex);
        Assert.NotNull(restored.objects[1].OpenFloor);Assert.NotNull(restored.pendingOpenFloor);
        Assert.Equal(JsonSerializer.Serialize(durable.PendingOpenFloor,Fields),JsonSerializer.Serialize(restored.pendingOpenFloor,Fields));
        Assert.Equal(JsonSerializer.Serialize(durable.Objects,Fields),JsonSerializer.Serialize(restored.Capture(23,Array.Empty<Mob>()).Objects,Fields));
    }

    [Fact]
    public void ObjectRelativeArrivalAndNearTriggerUseResolvedPoseWithoutChangingLegacy()
    {
        var (p,_)=Adopted();var state=p.objects[1];var pose=MissionObjectPoses.For(state);var c=Client();
        var arrival=new MissionArrival{at="object:1"};var actual=MissionRuntime.ArrivalSpot(c,p,arrival,0,0,0,0);
        var expected=MissionRuntime.Beside(pose.X,pose.Y+55,pose.Z,0);Assert.Equal(expected,actual);
        var near=typeof(MissionRuntime).GetMethod("Near",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(WorldClient),typeof(MissionProgress),typeof(string),typeof(double),typeof(double),typeof(double),typeof(double)},null)!;
        Assert.True((bool)near.Invoke(null,new object[]{c,p,"object:1",1d,pose.X,pose.Y,pose.Z})!);
        Assert.False((bool)near.Invoke(null,new object[]{c,p,"object:1",1d,state.spot.pos_x,state.spot.pos_y,state.spot.pos_z})!);
        var legacy=new MissionObjectState{def=state.def,spot=state.spot,goid=548};var at=MissionObjectPoses.For(legacy);
        Assert.Equal((legacy.spot.pos_x,legacy.spot.pos_y,legacy.spot.pos_z),(at.X,at.Y,at.Z));
    }

    [Fact]
    public void DevRelocationCannotOverwriteAnAdoptedRawSpot()
    {
        var (p,_)=Adopted();var c=Client();c.playerData.missionProgress=p;string before=JsonSerializer.Serialize(p.objects[1].spot,Fields);
        Assert.False(MissionStaging.MoveObjectToPlayer(c,1));Assert.Equal(before,JsonSerializer.Serialize(p.objects[1].spot,Fields));
    }

    // Literal persisted pending from normal201202 Poster Hunt run283bd051; no chooser or RNG replay.
    private static (Mission Mission,MissionSite Site,MissionOpenFloorPendingAdmission Pending,MissionOpenFloorPhasePlanner.Input[] Inputs) PosterHuntFourBoxes()
    {
        LoadNav();var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data","missions","machines_1_2_2_poster_hunt.xml"));var mission=MissionXml.Parse(xml.DocumentElement!);
        var pending=JsonSerializer.Deserialize<MissionOpenFloorPendingAdmission>("{\"Policy\":1,\"Mission\":201202,\"Identity\":\"82e91593dc3947a8a5004d2e7caa1d1b\",\"Phase\":2,\"Started\":1794779654,\"Site\":{\"District\":1,\"DoorKey\":219152412,\"DoorX\":27800,\"DoorY\":1745,\"DoorZ\":13700,\"Fingerprint\":\"FE1A4A0FAD5BF68CC4633B517E0E855108E109A0FAB99E2D3B5C9BC31ABCAADA\"},\"Choices\":[{\"ObjectId\":1,\"Goid\":548,\"RawSpotJson\":\"{\\\"exterior\\\":false,\\\"pos_x\\\":27950,\\\"pos_y\\\":1746,\\\"pos_z\\\":13350,\\\"rot\\\":1.1620496565E-05,\\\"quat\\\":\\\"00000000f20435bf00000000f304353f\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152592,\\\"staticId\\\":208,\\\"type\\\":\\\"hAU=\\\",\\\"strType\\\":null}\"},{\"ObjectId\":2,\"Goid\":548,\"RawSpotJson\":\"{\\\"exterior\\\":false,\\\"pos_x\\\":27700,\\\"pos_y\\\":1746,\\\"pos_z\\\":13350,\\\"rot\\\":90,\\\"quat\\\":\\\"0000000000000000000000000000803f\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152586,\\\"staticId\\\":202,\\\"type\\\":\\\"gAU=\\\",\\\"strType\\\":null}\"},{\"ObjectId\":3,\"Goid\":548,\"RawSpotJson\":\"{\\\"exterior\\\":false,\\\"pos_x\\\":28905,\\\"pos_y\\\":1826,\\\"pos_z\\\":12875,\\\"rot\\\":270,\\\"quat\\\":\\\"000000000000803f000000002ebd3bb3\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152622,\\\"staticId\\\":238,\\\"type\\\":\\\"hwU=\\\",\\\"strType\\\":null}\"},{\"ObjectId\":4,\"Goid\":548,\"RawSpotJson\":\"{\\\"exterior\\\":false,\\\"pos_x\\\":27260,\\\"pos_y\\\":1825,\\\"pos_z\\\":13100,\\\"rot\\\":179.99998838,\\\"quat\\\":\\\"00000000f204353f00000000f304353f\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152575,\\\"staticId\\\":191,\\\"type\\\":\\\"fwU=\\\",\\\"strType\\\":null}\"}],\"Refusal\":\"SearchExhausted:RouteGraphs\",\"Elevator\":{\"Version\":1,\"SiteFingerprint\":\"FE1A4A0FAD5BF68CC4633B517E0E855108E109A0FAB99E2D3B5C9BC31ABCAADA\",\"Region\":46,\"LobbyRegion\":49,\"FloorKey\":219152412,\"LobbyKey\":219152424,\"Floor\":[28500,1745,14400],\"Lobby\":[28500,545,14400],\"FloorQuaternion\":\"00000000f304353300000000ffff7f3f\",\"LobbyQuaternion\":\"00000000f304353300000000ffff7f3f\",\"Evidence\":\"original-elevator-group:0d10001c:legacy:0C806401BD7591F18C95A061C12F413E7F535EE152B13E5BF35FD74CEA30D9F8:selected-record:9991AFBF8121A0D346DD82C0A51F77F4FD302300FCF17D272379CCB4D4F85D6E;original-elevator-group:0d100028:legacy:5F9AAB53C5012FB4759EFF7BCEFA2C94D5BAF6B65F2B0CBC558A7F2CD249F5B3:selected-record:CE7647A84BBFCE742314CCC5B41AF985F8177844BFC8388BCF2EE89F71C84B40\",\"Sources\":[\"{\\\"exterior\\\":false,\\\"pos_x\\\":27950,\\\"pos_y\\\":1746,\\\"pos_z\\\":13350,\\\"rot\\\":1.1620496565E-05,\\\"quat\\\":\\\"00000000f20435bf00000000f304353f\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152592,\\\"staticId\\\":208,\\\"type\\\":\\\"hAU=\\\",\\\"strType\\\":null}\",\"{\\\"exterior\\\":false,\\\"pos_x\\\":27700,\\\"pos_y\\\":1746,\\\"pos_z\\\":13350,\\\"rot\\\":90,\\\"quat\\\":\\\"0000000000000000000000000000803f\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152586,\\\"staticId\\\":202,\\\"type\\\":\\\"gAU=\\\",\\\"strType\\\":null}\",\"{\\\"exterior\\\":false,\\\"pos_x\\\":28905,\\\"pos_y\\\":1826,\\\"pos_z\\\":12875,\\\"rot\\\":270,\\\"quat\\\":\\\"000000000000803f000000002ebd3bb3\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152622,\\\"staticId\\\":238,\\\"type\\\":\\\"hwU=\\\",\\\"strType\\\":null}\",\"{\\\"exterior\\\":false,\\\"pos_x\\\":27260,\\\"pos_y\\\":1825,\\\"pos_z\\\":13100,\\\"rot\\\":179.99998838,\\\"quat\\\":\\\"00000000f204353f00000000f304353f\\\",\\\"sectorID\\\":209,\\\"metrId\\\":1,\\\"mxoStaticId\\\":219152575,\\\"staticId\\\":191,\\\"type\\\":\\\"fwU=\\\",\\\"strType\\\":null}\"]}}",Fields)!;
        var site=Assert.Single(Catalog.Value.Where(s=>MissionOpenFloorSite.CatalogFingerprint(s)==pending.Site.Fingerprint));
        Assert.Equal(0x0d10001cu,site.elevatorDoor.mxoStaticId);Assert.Equal(46,pending.Elevator.Region);
        Assert.Equal(new uint[]{0x0d1000d0,0x0d1000ca,0x0d1000ee,0x0d1000bf},pending.Choices.Select(c=>c.ReadSpot().mxoStaticId));
        var inputs=pending.Choices.Select(c=>{Assert.True(MissionOpenFloorRequests.TryCreate(c.ReadSpot(),c.Goid,site,1,out var request,out string why,mission.phases[2],c.ObjectId,pending.Elevator),why);return new MissionOpenFloorPhasePlanner.Input(c.ObjectId,request);}).ToArray();
        Assert.All(inputs,i=>{Assert.Equal(new G.Id(209,46,111),i.Request.Entry.Surface);Assert.Equal(1695,i.Request.Entry.Standing.Y);});
        return(mission,site,pending,inputs);
    }
    [Fact]
    public void ActualPosterHuntFourBoxesShareBudgetsAndReachExistingThirdOrderWithoutStarvation()
    {
        var (_,_,_,inputs)=PosterHuntFourBoxes();var limits=MissionOpenFloorPhasePlanner.Limits.Default;
        Assert.True(MissionOpenFloorPhasePlanner.TryPlan(inputs,out var plan,out string why,out var work,limits),why);
        Assert.Equal(3,work.RouteGraphs);Assert.InRange(work.BodyNodes,1,4096);Assert.Equal(new ushort[]{3,4,1,2},plan.Order);
        Assert.True(G.ValidateSet(plan.Results.Values.ToArray(),out why),why);
        Assert.All(plan.Results.Values,result=>{Assert.True(G.ValidateRouteIds(result.Input,result.RoutePolygons,result.Route));Assert.True(result.Range.Within);Assert.True(result.NativeCastRange.Within);});
        // A graph cap belongs to the entire scheduler, never separately to each order.
        Assert.False(MissionOpenFloorPhasePlanner.TryPlan(inputs,out _,out why,out work,new MissionOpenFloorPhasePlanner.Limits(4096,2)));
        Assert.Equal("SearchExhausted:RouteGraphs",why);Assert.Equal(2,work.RouteGraphs);Assert.InRange(work.BodyNodes,1,4096);
        Assert.False(MissionOpenFloorPhasePlanner.TryPlan(inputs,out _,out why,out work,new MissionOpenFloorPhasePlanner.Limits(1,32)));
        Assert.Equal("SearchExhausted:BodyNodes",why);Assert.Equal(1,work.BodyNodes);Assert.Equal(0,work.RouteGraphs);
    }
    [Fact]
    public void ActualPosterHuntOldDepthFirstSpendsAllGraphsOnFourthBody()
    {
        var (_,_,_,inputs)=PosterHuntFourBoxes();var sequences=inputs.Select(i=>{Assert.True(G.TryBodySequence(i.Request,out var sequence,out string why,out _),why);return sequence;}).ToArray();
        var bodies=new G.BodyCandidate[4];int graphs=0;var prefixes=new HashSet<string>();
        bool Visit(int depth)
        {
            if(depth==4){graphs++;prefixes.Add(JsonSerializer.Serialize(bodies.Take(3).Select(b=>b.Centre)));Assert.False(G.RouteBodySet(bodies,out _,out string why));Assert.Equal("NoWholePhaseStandingRoute:4",why);return graphs==32;}
            foreach(var body in sequences[depth].Enumerate(true)){if(Enumerable.Range(0,depth).Any(i=>!G.BodiesClear(body,bodies[i])))continue;bodies[depth]=body;if(Visit(depth+1))return true;}return false;
        }
        Assert.True(Visit(0));Assert.Equal(32,graphs);Assert.Single(prefixes);
    }
    [Fact]
    public void RefusedPendingRecomputesOnlySameImmutableChoicesUnderNewSearchVersion()
    {
        var (mission,site,pending,inputs)=PosterHuntFourBoxes();string before=JsonSerializer.Serialize(pending,Fields);
        string oldKey=MissionOpenFloorSite.Hash("4:standing-interior:"+MissionOpenFloorResolver.StandingSelectionVersion+":"+string.Join(";",inputs.OrderBy(i=>i.ObjectId).Select(i=>i.ObjectId+":"+i.Request.Fingerprint)));
        pending.RememberFailedGeometry(oldKey,"SearchExhausted:RouteGraphs");
        Assert.True(MissionOpenFloorAdmission.TryGeometry(mission,2,site,pending.Choices,out var poses,out string why,pending),why);
        Assert.Equal(4,poses.Count);Assert.Equal(before,JsonSerializer.Serialize(pending,Fields));Assert.Equal("SearchExhausted:RouteGraphs",pending.Refusal);
        // Fixed placements validate directly and do not consult fresh-order scheduling.
        foreach(var choice in pending.Choices){var pose=poses[choice.ObjectId];Assert.True(MissionOpenFloorRequests.TryCreate(choice.ReadSpot(),548,site,1,out var request,out why,mission.phases[2],choice.ObjectId,pending.Elevator),why);Assert.True(pose.Replay(request,mission.id,2,new MissionObjectState{def=mission.phases[2].objects.Single(d=>d.id==choice.ObjectId),spot=choice.ReadSpot(),goid=548,OpenFloor=pose},out why),why);}
    }

    [Fact]
    public void ActualThreeObjectPhaseCapturesAndColdReplaysCanonicalWholeSetWithoutReselection()
    {
        LoadNav();var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data","missions","machines_1_2_2_poster_hunt.xml"));var m=MissionXml.Parse(xml.DocumentElement!);
        var (site,_)=QualifiedScene();var choices=MissionStaging.SelectObjects(m.phases[0],site,new Random(6045116)).Select(MissionOpenFloorChoice.From).ToArray();
        Assert.Equal(new uint[]{0x0d1003dc,0x0d100314,0x0d1002f1},choices.Select(c=>c.ReadSpot().mxoStaticId));
        var timer=System.Diagnostics.Stopwatch.StartNew();Assert.True(MissionOpenFloorAdmission.TryGeometry(m,0,site,choices,out var poses,out string why),why);long planMs=timer.ElapsedMilliseconds;
        var p=new MissionProgress(m){site=site,openFloorCheckpoint=true,openFloorResolvedPhase=0,startedAt=100,persistenceId="three-box-cold"};p.sites.Add(site);
        foreach(var choice in choices)p.objects.Add(choice.ObjectId,new MissionObjectState{def=m.phases[0].objects.Single(d=>d.id==choice.ObjectId),spot=choice.ReadSpot(),goid=choice.Goid,entity=(uint)(100+choice.ObjectId),OpenFloor=poses[choice.ObjectId]});
        var inputs=choices.Select(c=>
        {
            Assert.True(MissionOpenFloorRequests.TryCreate(c.ReadSpot(),c.Goid,site,1,out var request,out string reason,m.phases[0],c.ObjectId),reason);
            return new MissionOpenFloorPhasePlanner.Input(c.ObjectId,request);
        }).ToArray();
        Assert.True(MissionOpenFloorPhasePlanner.TryPlan(inputs,out var independent,out why),why);
        // Policy4's search-work baseline; the exact published poses and independent geometry checks below stay unchanged.
        Assert.Equal(new ushort[]{1,2,3},independent.Order);Assert.Equal(329,independent.BodyNodes);Assert.Equal(5,independent.RouteGraphs);
        Assert.True(G.ValidateSet(independent.Results.Values.ToArray(),out why),why); // original + native-cast geometry, route and range
        foreach(var c in choices)Assert.Equal(JsonSerializer.Serialize(poses[c.ObjectId],Fields),JsonSerializer.Serialize(MissionOpenFloorPlacement.From(m.id,0,c.ObjectId,c.ReadSpot(),independent.Results[c.ObjectId]),Fields));
        Assert.Equal(new[]{25585.388671875,2095,14143.16796875},poses[1].Standing);
        Assert.Equal(new[]{25800d,2095,13860},poses[2].Standing);
        Assert.Equal(new[]{25841.595703125,2040,14214.611328125},poses[3].Centre);
        Assert.Equal(new[]{25689.16015625,2095,14143.16796875},poses[3].Standing);
        Assert.Equal(new ushort[]{209,63,3,209,63,4},poses[3].RouteSurfaces);
        var saved=RoundTrip(p);timer.Restart();var restored=MissionProgress.RestoreFromCatalog(m,saved,new[]{site});long restoreMs=timer.ElapsedMilliseconds;
        Assert.Equal(3,restored.objects.Count);Assert.Equal(JsonSerializer.Serialize(saved.Objects,Fields),JsonSerializer.Serialize(restored.Capture(23,Array.Empty<Mob>()).Objects,Fields));
        Assert.Equal(3,restored.objects.Values.Select(o=>o.OpenFloor!.InputFingerprint).Distinct().Count());
        // Old admitted Policy3 wall witnesses remain valid fixed geometry. Selection is
        // never rerun during restore, even when a preferred route is now available.
        var current=poses[3];
        p.objects[3].OpenFloor=new MissionOpenFloorPlacement(current.Policy,current.World,current.Mission,current.Phase,current.ObjectId,current.SourceKey,current.SourceGoid,current.ConcreteGoid,
            current.RawFingerprint,current.InputFingerprint,current.Quaternion,current.Centre,new[]{25940d,2095,14214.611328125},
            new[]{25579.31146596289,2095,13959.481277120463,25689.16015625,2095,13860,25940,2095,13860,25940,2095,14000,25940,2095,14200,25940,2095,14214.611328125},
            current.Surface,current.RootSurface,new ushort[]{209,63,3,209,63,3,209,63,3,209,63,9,209,63,5});
        var historical=RoundTrip(p);var oldRestored=MissionProgress.RestoreFromCatalog(m,historical,new[]{site});
        Assert.Equal(JsonSerializer.Serialize(historical.Objects,Fields),JsonSerializer.Serialize(oldRestored.Capture(23,Array.Empty<Mob>()).Objects,Fields));
        System.Diagnostics.Trace.WriteLine($"Normal three-box planning {planMs}ms; fixed restore {restoreMs}ms");
    }
    [Theory] [InlineData(3,-25.00377655029297)] [InlineData(548,-40)] [InlineData(552,-50)]
    public void AdoptedObjectRelativeNpcArrivalUsesPublishedStandingFloorForEveryModel(int goid,double support)
    {
        var (p,_)=Adopted();var state=p.objects[1];state.goid=(ushort)goid;var raw=state.spot;double floor=2000,centre=floor-support;
        state.OpenFloor=new MissionOpenFloorPlacement(G.PolicyVersion,1,p.mission.id,0,1,raw.mxoStaticId,MissionSites.Goid(raw),(ushort)goid,
            MissionOpenFloorPlacement.RawHash(raw),new string('A',64),"0000000000000000000000000000803f",new[]{100d,centre,300d},new[]{200d,floor+95,300d},new[]{0d,floor+95,0d,200d,floor+95,300d},new ushort[]{209,1,1},new ushort[]{209,1,1},new ushort[]{209,1,1});
        var actual=MissionRuntime.ArrivalSpot(Client(),p,new MissionArrival{at="object:1"},0,0,0,0);
        Assert.Equal(MissionRuntime.Beside(100,floor+95,300,0),actual);
    }
    [Theory] [InlineData(3)] [InlineData(548)] [InlineData(552)]
    public void SamePrototypeAndExplicitCustomSourcesStayLegacyWithoutAdoption(int goid)
    {
        var m=Definition();foreach(ushort rawGoid in new[]{(ushort)0,(ushort)goid})
        {
            var raw=new StaticWorldObject{metrId=1,mxoStaticId=0,type=BitConverter.GetBytes(rawGoid),quat="0000000000000000000000000000803f"};
            Assert.False(MissionOpenFloorAdmission.Target((ushort)goid,raw));var choices=new[]{new MissionOpenFloorChoice(1,(ushort)goid,JsonSerializer.Serialize(raw,Fields))};
            Assert.True(MissionOpenFloorAdmission.TryGeometry(m,0,null!,choices,out var poses,out _));Assert.Empty(poses);
        }
        var staticZero=new StaticWorldObject{metrId=1,mxoStaticId=0,type=BitConverter.GetBytes((ushort)1415)};Assert.True(MissionOpenFloorAdmission.Target((ushort)goid,staticZero));
    }
    [Fact]
    public void AdoptedAdmissionDropsBothKindsOfOutputIfResetOccursAfterValidation()
    {
        LoadNav();var c=Client();var m=Definition();var p=new MissionProgress(m){startedAt=100};var (site,_)=QualifiedScene();c.playerData.missionPreview=site;c.playerData.missionPreviewFor=m;
        Assert.True(MissionOpenFloorAdmission.TryPrepare(c,p,0,null!,out var plan,out _,out string why,new[]{site}),why);
        using(var batch=MissionOutput.Begin(c.messageQueue))
        {
            Assert.True(plan.BindOutput(c));c.messageQueue.addRpcMessage(new byte[]{0x80,0x9e});c.messageQueue.addObjectMessage(new byte[]{0x03,1},false);
            c.TransportFence.PublishIncoming(true,()=>{});batch.Commit();
        }
        Assert.Empty(c.messageQueue.RPCMessagesQueue);Assert.Empty(c.messageQueue.ObjectMessagesQueue);
    }
    [Fact]
    public void FailureMemoSurvivesRefusalCopiesButNotColdRestoreOrChangedInputs()
    {
        var p=new MissionOpenFloorPendingAdmission(1,901,"memo",1,100,null,Array.Empty<MissionOpenFloorChoice>(),null);int executions=0;
        bool Run(MissionOpenFloorPendingAdmission pending,string key){if(pending.TryFailedGeometry(key,out _))return false;executions++;pending.RememberFailedGeometry(key,"NoWholePhasePlacement");return false;}
        Run(p,"A");Run(p.WithRefusal("NoWholePhasePlacement"),"A");Assert.Equal(1,executions);
        Run(p,"B");Assert.Equal(2,executions);string json=JsonSerializer.Serialize(p,Fields);Assert.DoesNotContain("Memo",json);
        var cold=JsonSerializer.Deserialize<MissionOpenFloorPendingAdmission>(json,Fields)!;Run(cold,"B");Assert.Equal(3,executions);
        Assert.Equal(json,JsonSerializer.Serialize(cold,Fields));
    }

    [Fact]
    public void ActualSingleBoxFiltersDisconnectedBodiesBeforeSpendingRouteGraphs()
    {
        LoadNav();var m=Definition();var (site,raw)=QualifiedScene();Assert.True(MissionOpenFloorRequests.TryCreate(raw,548,site,1,out var request,out string why,m.phases[0],1),why);
        var fullDirect=G.Plan(request,4096,32,out _,out int graphs);Assert.True(fullDirect.HasPlan,fullDirect.Refusal);Assert.InRange(graphs,1,32);
        Assert.True(MissionOpenFloorPhasePlanner.TryPlan(new[]{new MissionOpenFloorPhasePlanner.Input(1,request)},out var plan,out why,out var work,MissionOpenFloorPhasePlanner.Limits.Default),why);
        Assert.InRange(work.RouteGraphs,1,2);Assert.InRange(work.BodyNodes,1,4096);
        Assert.True(MissionOpenFloorPhasePlanner.TryPlan(new[]{new MissionOpenFloorPhasePlanner.Input(1,request)},out var oldExpanded,out why,out _,MissionOpenFloorPhasePlanner.Limits.Default,false),why);
        Assert.Equal(JsonSerializer.Serialize(MissionOpenFloorPlacement.From(m.id,0,1,raw,oldExpanded.Results[1]),MissionProgress.SaveJson),
            JsonSerializer.Serialize(MissionOpenFloorPlacement.From(m.id,0,1,raw,plan.Results[1]),MissionProgress.SaveJson));
    }
}
