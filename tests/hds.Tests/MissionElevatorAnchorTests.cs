using System.IO;
using System.Text;
using System.Text.Json;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MissionElevatorAnchorTests
{
    private const string Identity="0000000000000000000000000000803f";
    private static StaticWorldObject Ops()=>new(){metrId=1,sectorID=209,staticId=32,mxoStaticId=0x0d100020,type=new byte[]{0xa3,1,0,0},
        pos_x=28800,pos_y=1345,pos_z=14700,quat="00000000f20435bf00000000f304353f"};
    [Fact] public void ActualLegacyCoreSignatureBelongsToGroupButOfficeRegionOwnsOnlyResolvedOrientation()
    {
        var raw=Ops();string before=JsonSerializer.Serialize(raw,MissionProgress.SaveJson);
        Assert.True(MissionElevatorAnchors.TryCurrent(raw,out var index));Assert.True(index.NeedsCorrection(raw,out bool changed,out _));Assert.True(changed);
        Assert.True(index.TryResolve(raw,41,out var core,out var why),why);Assert.True(index.TryResolve(raw,47,out var office,out why),why);
        Assert.Equal((28500d,1200d,14400d),(office.X,office.Y,office.Z));Assert.Equal((core.X,core.Y,core.Z),(office.X,office.Y,office.Z));
        Assert.NotEqual(core.Quaternion,office.Quaternion);Assert.Equal("00000000f304353300000000ffff7f3f",office.Quaternion);
        Assert.Equal(before,JsonSerializer.Serialize(raw,MissionProgress.SaveJson));Assert.False(index.TryResolve(raw,42,out _,out _));
        raw.pos_x++;Assert.False(index.TryResolve(raw,47,out _,out var refused));Assert.Equal("UnlistedElevatorLegacySignature",refused);
    }
    [Theory][InlineData(1352,945,64,65)][InlineData(1403,145,68,69)]
    public void WorkingPhase1AndIndependentLobbyKeepLegacyYawDespiteNativeQuaternionDifference(uint id,double y,ushort coreRegion,ushort roomRegion)
    {
        var raw=new StaticWorldObject{metrId=1,sectorID=1214,staticId=id,mxoStaticId=(1214u<<20)|id,type=new byte[]{0xa1,1,0,0},pos_x=7700,pos_y=y,pos_z=15400,quat=Identity};
        Assert.True(MissionElevatorAnchors.TryCurrent(raw,out var index));Assert.True(index.NeedsCorrection(raw,out bool changed,out _));Assert.False(changed);
        Assert.True(index.TryResolve(raw,coreRegion,out var core,out var why),why);Assert.True(index.TryResolve(raw,roomRegion,out var room,out why),why);
        Assert.Equal((7700d,y-145,15400d),(room.X,room.Y,room.Z));Assert.Equal(core.Quaternion,room.Quaternion);Assert.NotEqual(Identity,room.Quaternion);
        var site=new MissionSite{metrId=1,elevatorDoor=raw,lobbyDoor=raw};Assert.True(MissionElevatorPoses.TryCreate(site,null,out var witness,out why),why);Assert.Null(witness);Assert.Equal(Identity,raw.quat);
    }
    [Fact] public void ExactOriginalSourceRegionRejectsMovedSourceAndOtherRegion()
    {
        var raw=Ops();Assert.True(MissionElevatorAnchors.TryCurrent(raw,out var index));
        // Shipped source0d100166 is GO1407, distinct from the eventual free GO3 DTO.
        var doc=ReadDocument(209);var source=doc.Sources.Single(g=>g.StaticId==358);var sig=source.Legacy.Single().Signature;
        var s=new StaticWorldObject{metrId=1,sectorID=209,staticId=358,mxoStaticId=0x0d100166,type=BitConverter.GetBytes((uint)sig.Goid),pos_x=sig.Position[0],pos_y=sig.Position[1],pos_z=sig.Position[2],quat=sig.Quaternion};
        Assert.Equal(1407,sig.Goid);Assert.True(index.SourceRegion(s,47));Assert.False(index.SourceRegion(s,41));s.pos_x++;Assert.False(index.SourceRegion(s,47));
    }
    private static MissionElevatorAnchors.Document ReadDocument(ushort sector)
    {
        using var f=File.OpenRead(DataLoader.DataPath("mission_elevators","1",sector+".json.gz"));using var gz=new System.IO.Compression.GZipStream(f,System.IO.Compression.CompressionMode.Decompress);
        return JsonSerializer.Deserialize<MissionElevatorAnchors.Document>(gz)!;
    }
    private static MissionElevatorAnchors Index(MissionElevatorAnchors.Document doc)
    {using var stream=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(doc));return MissionElevatorAnchors.Read(stream,doc.Sector);}
    [Fact] public void PositionDisagreementAndDuplicateRegionRefuseWithoutLookingForAnotherOccurrence()
    {
        var doc=ReadDocument(209);var group=doc.Groups.Single(g=>g.StaticId==32);group.Occurrences[1].Position[0]++;
        var index=Index(doc);Assert.False(index.TryResolve(Ops(),47,out _,out var why));Assert.Equal("ElevatorNativePositionDisagreement",why);
        doc=ReadDocument(209);group=doc.Groups.Single(g=>g.StaticId==32);group.Occurrences=group.Occurrences.Append(group.Occurrences.Single(o=>o.Region==47)).ToArray();
        index=Index(doc);Assert.False(index.TryResolve(Ops(),47,out _,out why));Assert.Equal("MissingOrAmbiguousElevatorRegion",why);
    }
    [Fact] public void UnchangedPoseDoesNotAcquireNewAmbiguityGateFromUnusedOccurrences()
    {
        var doc=ReadDocument(209);var raw=Ops();var group=doc.Groups.Single(g=>g.StaticId==32);
        group.Occurrences[0].Position=new[]{raw.pos_x,raw.pos_y-145,raw.pos_z};
        var index=Index(doc);Assert.True(index.NeedsCorrection(raw,out bool changed,out _));Assert.False(changed);
        Assert.False(index.TryResolve(raw,47,out _,out _)); // An explicit correction would still be forbidden.
    }
    [Fact] public void PendingRefusalPreservesOldNullWitnessAndExactRawSourceWithoutInventingEvidence()
    {
        var c=new MissionOpenFloorChoice(1,3,JsonSerializer.Serialize(Ops(),MissionProgress.SaveJson));
        var old=new MissionOpenFloorPendingAdmission(1,201201,"same",1,123,null,new[]{c},null);
        var refused=old.WithRefusal("ChangedPendingMissionElevatorWitness");Assert.Null(old.Elevator);Assert.Null(refused.Elevator);Assert.Equal(old.Choices[0].RawSpotJson,refused.Choices[0].RawSpotJson);
        Assert.Equal(old.Started,refused.Started);Assert.Equal(old.Identity,refused.Identity);
        Assert.False(MissionElevatorPoses.Same(old.Elevator,new MissionElevatorWitness(1,"fixed",47,new[]{1d,2,3},new[]{4d,5,6},Identity,Identity,"real",new[]{c.RawSpotJson})));
    }
    [Fact] public void FixedWitnessOwnsItsArraysAndRoundtripsWithoutRewritingRawSite()
    {
        var floor=new[]{28500d,1345,14400};var raw=new[]{"exact raw source"};var witness=new MissionElevatorWitness(1,"site",47,floor,new[]{28500d,545,14400},Identity,Identity,"evidence",raw);
        floor[0]=0;raw[0]="other";var exposed=witness.Floor;exposed[0]=1;var sources=witness.Sources;sources[0]="changed";
        Assert.Equal(28500,witness.Floor[0]);Assert.Equal("exact raw source",witness.Sources[0]);
        Assert.True(MissionElevatorPoses.Same(witness,JsonSerializer.Deserialize<MissionElevatorWitness>(JsonSerializer.Serialize(witness,MissionProgress.SaveJson),MissionProgress.SaveJson)));
    }

    private static string Repo {get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds","Hardline Dreams MxO server.csproj")))p=Path.GetDirectoryName(p)!;return p;}}
    private static (MissionSite Site,MissionOpenFloorChoice[] Choices) ExactOps()
    {
        SectorProperties.Load(Path.Combine(Repo,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data","navmesh"));
        StaticWorldObject Raw(MissionElevatorAnchors.Group g)
        {var s=g.Legacy[0].Signature;return new(){metrId=1,sectorID=209,staticId=g.StaticId,mxoStaticId=g.Key,type=BitConverter.GetBytes((uint)s.Goid),pos_x=s.Position[0],pos_y=s.Position[1],pos_z=s.Position[2],quat=s.Quaternion};}
        var doc=ReadDocument(209);var source=Raw(doc.Sources.Single(g=>g.StaticId==358));
        var site=new MissionSite{metrId=1,style="Office",floorY=1346,minX=27100,maxX=31300,minZ=12300,maxZ=15100,elevatorDoor=Raw(doc.Groups.Single(g=>g.StaticId==32)),lobbyDoor=Raw(doc.Groups.Single(g=>g.StaticId==40))};
        site.shaft.Add(site.lobbyDoor);site.shaft.Add(site.elevatorDoor);site.objectSpots.Add(source);site.npcSpots.Add(new[]{29300d,1295,13700,0});
        return(site,new[]{new MissionOpenFloorChoice(1,3,JsonSerializer.Serialize(source,MissionProgress.SaveJson))});
    }
    [Fact] public void ActualFloor47AndIndependentLobby49ResolveWithoutEquatingLegacyFallbackOrSearchingRegions()
    {
        var (site,choices)=ExactOps();string before=JsonSerializer.Serialize(site,MissionProgress.SaveJson);
        Assert.True(MissionElevatorPoses.TryCreate(site,choices,out var witness,out var why),why);Assert.NotNull(witness);
        Assert.Equal(new[]{28500d,1345,14400},witness.Floor);Assert.Equal(new[]{28500d,545,14400},witness.Lobby);Assert.Equal(47,witness.Region);
        Assert.Equal(0x0d100020u,witness.FloorKey);Assert.Equal(0x0d100028u,witness.LobbyKey);
        var old=MissionOpenFloorEntry.Calculate(1,new(28800,545,14700),site.CentreX,site.CentreZ,"original lobby control");
        var measured=MissionOpenFloorEntry.Calculate(1,MissionElevatorPoses.Position(witness.Lobby),site.CentreX,site.CentreZ,"native position consensus");
        Assert.NotNull(old.Root);Assert.NotNull(measured.Root);Assert.Equal(49,measured.Root.Surface.Region);Assert.NotEqual(old.Root.Surface.Region,measured.Root.Surface.Region);
        Assert.Equal(before,JsonSerializer.Serialize(site,MissionProgress.SaveJson));
        Assert.True(MissionElevatorAnchors.TryCurrent(site.lobbyDoor,out var index));Assert.True(index.TryPosition(site.lobbyDoor,out var p,out why),why);
        Assert.Equal((28500d,400d,14400d),(p.X,p.Y,p.Z));Assert.False(index.TryResolve(site.lobbyDoor,50,out _,out why));Assert.Equal("MissingOrAmbiguousElevatorRegion",why);
    }
    [Fact] public void AlreadyCommittedPendingWithoutCorrectedWitnessRemainsExactlyRefusedOnRepeatedAttempts()
    {
        var (site,choices)=ExactOps();var xml=new System.Xml.XmlDocument();xml.LoadXml("<mission id='901' title='Pending identity' description='D'><phase><objective goal='1' text='Wait' type='Survive' seconds='1'/></phase><phase site='Office'><object id='1' kind='Computer'/><objective goal='1' type='Search' object='1' item='6783' text='Take'/></phase></mission>");
        var mission=MissionXml.Parse(xml.DocumentElement!);var pending=new MissionOpenFloorPendingAdmission(1,901,"old",1,123,MissionOpenFloorSite.From(site),choices,null);
        string before=JsonSerializer.Serialize(pending,MissionProgress.SaveJson);
        for(int i=0;i<2;i++)
        {
            Assert.False(MissionOpenFloorAdmission.TryGeometry(mission,1,site,choices,out _,out var why,pending));
            Assert.Equal("ChangedPendingMissionElevatorWitness",why);Assert.Equal(before,JsonSerializer.Serialize(pending,MissionProgress.SaveJson));
        }
        var wrong=choices[0].ReadSpot();wrong.mxoStaticId=0x0d100023;wrong.staticId=35;wrong.type=BitConverter.GetBytes(2545u);
        Assert.False(MissionElevatorPoses.TryCreate(site,new[]{new MissionOpenFloorChoice(1,3,JsonSerializer.Serialize(wrong,MissionProgress.SaveJson))},out _,out var refusal));Assert.Equal("ElevatorSourceRegionDisagreement",refusal);
    }
    [Fact] public void ModeAndActualRegisteredScenarioUseEarlierReceiptsAndNoSourceOrGameplayOverride()
    {
        var mode=NativeQa.NativeQaModes.Find("mission-elevator-native-pose");Assert.IsType<NativeQa.Modes.MissionElevatorNativePoseMode>(mode);
        Assert.False(mode.Plan.FixedCallbackRolls);Assert.False(mode.Plan.GmCharacter110);Assert.Empty(mode.Plan.Bootstrap);Assert.Contains(mode.Plan.ReadyMessages,x=>x.Contains("STOP/HOLD"));
        var scenario=Assert.Single(HeadlessClient.Scenarios.All,x=>x.Name==mode.Name);Assert.NotNull(scenario.Environment);
        Assert.Equal("201101",scenario.Environment!["MXO_MISSION_SITE_QA_SEED"]);Assert.DoesNotContain(mode.Plan.AfterHelpers,x=>x.Contains("char_active_missions"));
        Assert.DoesNotContain(201201,HeadlessClient.InteriorAdmissionFixture.ElevatorNativePose.Prerequisites);
    }

    [Fact] public void ExistingFixedRouteCannotBeRewrittenToNewMeasuredRootAndUntouchedRouteStillReplays()
    {
        var raw=Ops();raw.type=BitConverter.GetBytes((ushort)1407);raw.staticId=358;raw.mxoStaticId=0x0d100166;
        var state=new MissionObjectState{def=new MissionObjectDef{id=1},spot=raw,goid=3};
        var rect=new[]{new hds.MissionOpenFloorResolver.Point(27000,12000),new hds.MissionOpenFloorResolver.Point(31000,12000),new hds.MissionOpenFloorResolver.Point(31000,15000),new hds.MissionOpenFloorResolver.Point(27000,15000)};
        hds.MissionOpenFloorResolver.Request Request(bool corrected)
        {
            var id=new hds.MissionOpenFloorResolver.Id(209,47,(ushort)(corrected?200:18));
            return new(1,hds.MissionOpenFloorResolver.SourceModelPresence.Model6Absent,"literal model absence",new(new(raw.pos_x,raw.pos_y,raw.pos_z),Identity,raw.mxoStaticId,1407),new(new(28500,1200,14000),Identity,raw.mxoStaticId,1407),"literal original source",
                new OpenFloorModelBounds(3,new(-10,-10,-10),new(10,10,10),"literal body"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal usable"),
                new[]{new hds.MissionOpenFloorResolver.Surface(id,rect,0,0,1200)},new(id,corrected?new(28641.42135623731,1295,14258.57864376269):new(28874.278135270823,1295,14514.304661822947),"exact old/new entry arithmetic"),
                new(27000,31000,12000,15000),new(1,true,new(27000,31000,12000,15000),1100,2000,Array.Empty<hds.MissionOpenFloorResolver.StaticObstacle>(),"literal complete scene"));
        }
        var old=Request(false);var result=hds.MissionOpenFloorResolver.Plan(old);Assert.True(result.HasPlan,result.Refusal);
        var fixedPose=MissionOpenFloorPlacement.From(201201,1,1,raw,result);string saved=JsonSerializer.Serialize(fixedPose,MissionProgress.SaveJson);string rawBefore=JsonSerializer.Serialize(raw,MissionProgress.SaveJson);
        Assert.True(fixedPose.TryReplay(old,201201,1,state,out _,out var why),why);
        Assert.False(fixedPose.TryReplay(Request(true),201201,1,state,out _,out _));
        Assert.Equal(saved,JsonSerializer.Serialize(fixedPose,MissionProgress.SaveJson));Assert.Equal(rawBefore,JsonSerializer.Serialize(raw,MissionProgress.SaveJson));
        Assert.True(fixedPose.TryReplay(old,201201,1,state,out _,out why),why);
    }

    private static Mission ActualMission()
    {var xml=new System.Xml.XmlDocument();xml.Load(Path.Combine(Repo,"data","missions","machines_1_2_1_disturbing_trends.xml"));return MissionXml.Parse(xml.DocumentElement!);}
    private static MissionProgress.SavedState CapturedHistory(out Mission mission,out MissionSite site,out MissionElevatorWitness witness)
    {
        var pair=ExactOps();site=pair.Site;mission=ActualMission();Assert.True(MissionElevatorPoses.TryCreate(site,pair.Choices,out witness,out var why),why);
        var progress=new MissionProgress(mission){phaseIndex=2,openFloorCheckpoint=true,site=null};progress.sites.Add(site);
        progress.elevatorWitnesses.Add(MissionOpenFloorSite.CatalogFingerprint(site),witness);progress.itemSlots[6783]=6;
        var saved=progress.Capture(1,Array.Empty<Mob>());progress.elevatorWitnesses.Clear();progress.itemSlots.Clear();return saved;
    }
    [Fact] public void CapturedHistoryRestoresExactMeasuredWitnessAndOwnedItemAfterInMemoryRollback()
    {
        var saved=CapturedHistory(out var mission,out var site,out var witness);string before=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);
        var restored=MissionProgress.RestoreFromCatalog(mission,saved,new[]{site});Assert.True(MissionElevatorPoses.Same(witness,restored.ElevatorFor(site)));
        Assert.Equal((ushort)6,restored.itemSlots[6783]);Assert.Single(restored.sites);Assert.Null(restored.site);
        Assert.Equal(before,JsonSerializer.Serialize(saved,MissionProgress.SaveJson));Assert.Equal(witness.Sources,restored.ElevatorFor(site)!.Sources);
    }
    [Fact] public void InvalidPendingIdentityIsRejectedBeforeRestoredDoorGeometry()
    {
        var saved=CapturedHistory(out var mission,out var site,out var witness);saved.Done=new[]{1};saved.Flags["waitingForExit"]=true;
        saved.PendingOpenFloor=new MissionOpenFloorPendingAdmission(1,mission.id,saved.Identity,3,saved.Started,MissionOpenFloorSite.From(site),Array.Empty<MissionOpenFloorChoice>(),null);
        // Valid metadata shape, but deliberately wrong measured position: this would fail geometry if it were reached.
        saved.ElevatorWitnesses=new[]{new MissionElevatorWitness(1,witness.SiteFingerprint,witness.Region,new[]{1d,1345,14400},witness.Lobby,witness.FloorQuaternion,witness.LobbyQuaternion,witness.Evidence,witness.Sources,witness.FloorKey,witness.LobbyKey)};
        var error=Assert.Throws<InvalidOperationException>(()=>MissionProgress.RestoreFromCatalog(mission,saved,new[]{site}));
        Assert.Equal("Invalid pending open-floor checkpoint choice",error.Message);
    }

    [Fact] public void ActiveOverlayRequiresActualRawMembershipWorldAndNonQuarantinedOwner()
    {
        var (site,choices)=ExactOps();Assert.True(MissionElevatorPoses.TryCreate(site,choices,out var witness,out var why),why);
        var progress=new MissionProgress(ActualMission());progress.sites.Add(site);progress.elevatorWitnesses.Add(MissionOpenFloorSite.CatalogFingerprint(site),witness);
        var overlay=MissionElevatorPoses.ResolveOverlay(progress,site.elevatorDoor);Assert.Equal(MissionElevatorPoses.OverlayKind.Resolved,overlay.Kind);Assert.Equal(28500,overlay.Position.X);
        var raw=Ops();raw.staticId++;Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionElevatorPoses.ResolveOverlay(progress,raw).Kind);
        raw=Ops();raw.metrId=2;Assert.Equal(MissionElevatorPoses.OverlayKind.Legacy,MissionElevatorPoses.ResolveOverlay(progress,raw).Kind);
        raw=Ops();raw.pos_x++;Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionElevatorPoses.ResolveOverlay(progress,raw).Kind);
        progress.restoreRefusal="retained incompatible checkpoint";Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionElevatorPoses.ResolveOverlay(progress,site.elevatorDoor).Kind);
    }

    [Fact] public void KnownCorrectedDoorWithoutWitnessAndConflictingHistoryNeverFallBackToRawOverlay()
    {
        var (site,choices)=ExactOps();Assert.True(MissionElevatorPoses.TryCreate(site,choices,out var witness,out var why),why);
        var progress=new MissionProgress(ActualMission());progress.sites.Add(site);
        Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionElevatorPoses.ResolveOverlay(progress,site.elevatorDoor).Kind);
        progress.elevatorWitnesses.Add(MissionOpenFloorSite.CatalogFingerprint(site),witness);
        Assert.Equal(MissionElevatorPoses.OverlayKind.Resolved,MissionElevatorPoses.ResolveOverlay(progress,site.elevatorDoor).Kind);
        var other=new MissionSite{metrId=1,elevatorDoor=site.elevatorDoor,lobbyDoor=site.lobbyDoor,minX=site.minX+1,maxX=site.maxX,minZ=site.minZ,maxZ=site.maxZ};progress.sites.Add(other);
        Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionElevatorPoses.ResolveOverlay(progress,site.elevatorDoor).Kind); // No convenient valid-history pick.
        Assert.True(MissionElevatorAnchors.TryCurrent(site.elevatorDoor,out var index));Assert.True(index.TryResolve(site.elevatorDoor,41,out var core,out why),why);
        progress.elevatorWitnesses.Add(MissionOpenFloorSite.CatalogFingerprint(other),new MissionElevatorWitness(1,MissionOpenFloorSite.CatalogFingerprint(other),41,witness.Floor,witness.Lobby,core.Quaternion,witness.LobbyQuaternion,witness.Evidence,witness.Sources,witness.FloorKey,witness.LobbyKey,witness.LobbyRegion));
        var conflict=MissionElevatorPoses.ResolveOverlay(progress,site.elevatorDoor);Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,conflict.Kind);Assert.Equal("ConflictingMissionElevatorHistory",conflict.Refusal);
    }
    [Fact] public async Task DoorPublicationHoldsOwnerLockAndRejectsOwnerReplacedWhileWaiting()
    {
        var (site,choices)=ExactOps();Assert.True(MissionElevatorPoses.TryCreate(site,choices,out var witness,out var why),why);
        var progress=new MissionProgress(ActualMission());progress.sites.Add(site);progress.elevatorWitnesses.Add(MissionOpenFloorSite.CatalogFingerprint(site),witness);
        var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));var owner=new ClientData();client.playerData=owner;owner.missionProgress=progress;
        bool published=false;var normal=MissionElevatorPoses.WithDoorOwner(client,site.elevatorDoor,_=>{Assert.True(System.Threading.Monitor.IsEntered(owner.missionSync));published=true;});
        Assert.True(published);Assert.Equal(MissionElevatorPoses.OverlayKind.Resolved,normal.Kind);published=false;
        using var started=new System.Threading.ManualResetEventSlim();Task<MissionElevatorPoses.Overlay> waiting;
        lock(owner.missionSync)
        {
            waiting=Task.Run(()=>{started.Set();return MissionElevatorPoses.WithDoorOwner(client,site.elevatorDoor,_=>published=true);});
            Assert.True(started.Wait(TimeSpan.FromSeconds(30)));Assert.False(waiting.IsCompleted);client.playerData=new ClientData();
        }
        var stale=await waiting.WaitAsync(TimeSpan.FromSeconds(30));Assert.False(published);Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,stale.Kind);Assert.Equal("ChangedMissionDoorOwner",stale.Refusal);
    }
    [Fact] public void BoundLegacySelectionCannotAdoptCorrectedMetadataWithoutFreePropPlanner()
    {
        var (site,choices)=ExactOps();var mission=ActualMission();var legacy=new[]{new MissionOpenFloorChoice(1,1407,choices[0].RawSpotJson)};
        Assert.True(MissionOpenFloorAdmission.TryGeometry(mission,1,site,legacy,out var poses,out var why),why);Assert.Empty(poses);
        Assert.True(MissionElevatorPoses.TryCreate(site,choices,out var witness,out why),why);
        var pending=new MissionOpenFloorPendingAdmission(1,mission.id,"bound",1,123,MissionOpenFloorSite.From(site),legacy,null,witness);
        Assert.False(MissionOpenFloorAdmission.TryGeometry(mission,1,site,legacy,out _,out why,pending));Assert.Equal("UnexpectedLegacyMissionElevatorWitness",why);
    }

    [Theory][InlineData("missing")][InlineData("truncated")][InlineData("hash-mismatch")][InlineData("invalid-json")]
    public void DeclaredPayloadFailureRefusesCreationSelectedOverlayAndRestoreWithoutMutation(string fault)
    {
        var saved=CapturedHistory(out var mission,out var site,out var witness);
        var restored=MissionProgress.RestoreFromCatalog(mission,saved,new[]{site});
        var choices=witness.Sources.Select((raw,i)=>new MissionOpenFloorChoice((ushort)i,3,raw)).ToArray();
        string before=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);
        string directory=Path.Combine(Path.GetTempPath(),"mxo-elevator-declared-data-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        byte[] payload=File.ReadAllBytes(DataLoader.DataPath("mission_elevators","1","209.json.gz"));
        string expected=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
        if(fault=="truncated")File.WriteAllBytes(Path.Combine(directory,"209.json.gz"),payload.Take(12).ToArray());
        if(fault=="hash-mismatch")File.WriteAllBytes(Path.Combine(directory,"209.json.gz"),payload.Concat(new byte[]{1}).ToArray());
        if(fault=="invalid-json")
        {
            using var stream=new MemoryStream();using(var gz=new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionMode.Compress,true))gz.Write(System.Text.Encoding.UTF8.GetBytes("{invalid"));
            payload=stream.ToArray();File.WriteAllBytes(Path.Combine(directory,"209.json.gz"),payload);expected=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
        }
        File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonSerializer.Serialize(new{Format=1,World=1,Sectors=new[]{new{Sector=209,Groups=21,Sources=226,Sha256=expected}}}));
        string manifestHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(directory,"manifest.json")).Replace("\r\n","\n"))));
        var dataset=new MissionElevatorAnchors.Dataset(directory,manifestHash);
        for(int retry=0;retry<2;retry++)
        {
            Assert.Equal(MissionElevatorAnchors.LoadKind.Unavailable,dataset.Load(site.elevatorDoor).Kind);
            Assert.False(MissionElevatorPoses.TryCreate(site,choices,out var created,out var refusal,dataset));Assert.Null(created);Assert.NotNull(refusal);
            var overlay=MissionElevatorPoses.ResolveOverlay(restored,site.elevatorDoor,dataset);Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,overlay.Kind);
            var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new ClientData();client.playerData.missionProgress=restored;
            bool published=false;var decision=MissionElevatorPoses.WithDoorOwner(client,site.elevatorDoor,_=>published=true,dataset);
            Assert.False(published);Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,decision.Kind);
            var blocked=Assert.Throws<MissionPlacementRestoreException>(()=>MissionOpenFloorAdmission.ValidateRestored(restored,new[]{site},dataset));
            Assert.Contains("Invalid fixed mission elevator witness",blocked.Message);
            Assert.Equal(before,JsonSerializer.Serialize(saved,MissionProgress.SaveJson));Assert.True(MissionElevatorPoses.Same(witness,restored.ElevatorFor(site)));
        }
        var unrelated=Ops();unrelated.staticId++;unrelated.mxoStaticId++;
        Assert.Equal(MissionElevatorPoses.OverlayKind.Legacy,MissionElevatorPoses.ResolveOverlay(restored,unrelated,dataset).Kind);
        var undeclared=Ops();undeclared.sectorID=2;undeclared.mxoStaticId=(2u<<20)|undeclared.staticId;
        Assert.Equal(MissionElevatorAnchors.LoadKind.Undeclared,dataset.Load(undeclared).Kind);
    }
    [Fact] public void MissingOrInvalidManifestCannotUndeclareSupportedData()
    {
        foreach(string? manifest in new string?[]{null,"{broken", "{\"Format\":1,\"World\":2,\"Sectors\":[]}","{\"Format\":1,\"World\":1,\"Sectors\":[]}"})
        {
            string directory=Path.Combine(Path.GetTempPath(),"mxo-elevator-manifest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            if(manifest!=null)File.WriteAllText(Path.Combine(directory,"manifest.json"),manifest);
            Assert.Equal(MissionElevatorAnchors.LoadKind.Unavailable,new MissionElevatorAnchors.Dataset(directory).Load(Ops()).Kind);
        }
    }

    [Fact] public void HealthyDeclaredSectorWithAbsentGroupKeepsLegacyPath()
    {
        var (site,choices)=ExactOps();site.elevatorDoor.staticId=0xffffe;site.elevatorDoor.mxoStaticId=(209u<<20)|site.elevatorDoor.staticId;
        Assert.True(MissionElevatorPoses.TryCreate(site,choices,out var witness,out var why),why);Assert.Null(witness);
        var progress=new MissionProgress(ActualMission());progress.sites.Add(site);
        Assert.Equal(MissionElevatorPoses.OverlayKind.Legacy,MissionElevatorPoses.ResolveOverlay(progress,site.elevatorDoor).Kind);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void CommittedManifestPinAcceptsOnlyEquivalentLfOrCrLfPackage(bool crlf)
    {
        string directory=Path.Combine(Path.GetTempPath(),"mxo-elevator-manifest-eol-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string manifest=File.ReadAllText(DataLoader.DataPath("mission_elevators","1","manifest.json")).Replace("\r\n","\n");
        File.WriteAllText(Path.Combine(directory,"manifest.json"),crlf?manifest.Replace("\n","\r\n"):manifest,new System.Text.UTF8Encoding(false));
        File.Copy(DataLoader.DataPath("mission_elevators","1","209.json.gz"),Path.Combine(directory,"209.json.gz"));
        Assert.Equal(MissionElevatorAnchors.LoadKind.Available,new MissionElevatorAnchors.Dataset(directory).Load(Ops()).Kind);
    }
}
