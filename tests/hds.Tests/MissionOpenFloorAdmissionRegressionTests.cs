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
    private static readonly Lazy<string> QualifiedCase=new(()=>
    {
        // Reconstruct the exact normal catalog once. No source/scene/prototype injection or seed selection.
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
        var sites=MissionSites.Build(statics,1);
        var site=Assert.Single(sites.Where(s=>s.elevatorDoor!=null&&s.elevatorDoor.mxoStaticId==0x0d1003bb&&s.elevatorDoor.pos_y==2145&&
            s.objectSpots.Concat(s.containers).Concat(s.realObjects).Any(o=>o.mxoStaticId==0x0d1003dc&&o.pos_x==23940&&o.pos_y==2245&&o.pos_z==15050)));
        var source=Assert.Single(site.objectSpots.Concat(site.containers).Concat(site.realObjects).Where(o=>o.mxoStaticId==0x0d1003dc));
        return JsonSerializer.Serialize(new{Site=site,Object=new{Spot=source}},Fields);
    });
    private static (MissionSite Site,StaticWorldObject Source) QualifiedScene()
    {
        using var doc=JsonDocument.Parse(QualifiedCase.Value);return ReadScene(doc.RootElement);
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
        Assert.Throws<InvalidOperationException>(()=>MissionProgress.RestoreFromCatalog(p.mission,saved,new[]{site}));
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

    [Fact]
    public void ActualThreeObjectPhaseCapturesAndColdReplaysCanonicalWholeSetWithoutReselection()
    {
        LoadNav();var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data","missions","machines_1_2_2_poster_hunt.xml"));var m=MissionXml.Parse(xml.DocumentElement!);
        var (site,_)=QualifiedScene();var choices=MissionStaging.SelectObjects(m.phases[0],site,new Random(6045116)).Select(MissionOpenFloorChoice.From).ToArray();
        Assert.Equal(new uint[]{0x0d1003dc,0x0d100314,0x0d1002f1},choices.Select(c=>c.ReadSpot().mxoStaticId));
        var timer=System.Diagnostics.Stopwatch.StartNew();Assert.True(MissionOpenFloorAdmission.TryGeometry(m,0,site,choices,out var poses,out string why),why);long planMs=timer.ElapsedMilliseconds;
        var p=new MissionProgress(m){site=site,openFloorCheckpoint=true,openFloorResolvedPhase=0,startedAt=100,persistenceId="three-box-cold"};p.sites.Add(site);
        foreach(var choice in choices)p.objects.Add(choice.ObjectId,new MissionObjectState{def=m.phases[0].objects.Single(d=>d.id==choice.ObjectId),spot=choice.ReadSpot(),goid=choice.Goid,entity=(uint)(100+choice.ObjectId),OpenFloor=poses[choice.ObjectId]});
        var saved=RoundTrip(p);timer.Restart();var restored=MissionProgress.RestoreFromCatalog(m,saved,new[]{site});long restoreMs=timer.ElapsedMilliseconds;
        Assert.Equal(3,restored.objects.Count);Assert.Equal(JsonSerializer.Serialize(saved.Objects,Fields),JsonSerializer.Serialize(restored.Capture(23,Array.Empty<Mob>()).Objects,Fields));
        Assert.Equal(3,restored.objects.Values.Select(o=>o.OpenFloor!.InputFingerprint).Distinct().Count());
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
    public void ActualSingleBoxDoesNotSpendTheWholeGraphBudgetBeforeExpandedFallback()
    {
        LoadNav();var m=Definition();var (site,raw)=QualifiedScene();Assert.True(MissionOpenFloorRequests.TryCreate(raw,548,site,1,out var request,out string why,m.phases[0],1),why);
        var fullDirect=G.Plan(request,4096,32,out _,out int graphs);Assert.False(fullDirect.HasPlan);Assert.Equal("SearchExhausted:RouteGraphs",fullDirect.Refusal);Assert.Equal(32,graphs);
        Assert.True(MissionOpenFloorPhasePlanner.TryPlan(new[]{new MissionOpenFloorPhasePlanner.Input(1,request)},out var plan,out why,out var work,MissionOpenFloorPhasePlanner.Limits.Default),why);
        Assert.False(plan.DirectSingle);Assert.Equal(2,work.RouteGraphs);Assert.Equal(1,work.StaticRootGraphs);Assert.InRange(work.BodyNodes,2,4096);
        Assert.True(MissionOpenFloorPhasePlanner.TryPlan(new[]{new MissionOpenFloorPhasePlanner.Input(1,request)},out var oldExpanded,out why,out _,MissionOpenFloorPhasePlanner.Limits.Default,false),why);
        Assert.Equal(JsonSerializer.Serialize(MissionOpenFloorPlacement.From(m.id,0,1,raw,oldExpanded.Results[1]),MissionProgress.SaveJson),
            JsonSerializer.Serialize(MissionOpenFloorPlacement.From(m.id,0,1,raw,plan.Results[1]),MissionProgress.SaveJson));
    }
}
