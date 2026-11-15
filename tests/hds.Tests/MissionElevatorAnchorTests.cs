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
        Assert.True(MissionElevatorPoses.TryOverlay(progress,site.elevatorDoor,out var position,out _));Assert.Equal(28500,position.X);
        var raw=Ops();raw.staticId++;Assert.False(MissionElevatorPoses.TryOverlay(progress,raw,out _,out _));
        raw=Ops();raw.metrId=2;Assert.False(MissionElevatorPoses.TryOverlay(progress,raw,out _,out _));
        raw=Ops();raw.pos_x++;Assert.False(MissionElevatorPoses.TryOverlay(progress,raw,out _,out _));
        progress.restoreRefusal="retained incompatible checkpoint";Assert.False(MissionElevatorPoses.TryOverlay(progress,site.elevatorDoor,out _,out _));
    }
}
