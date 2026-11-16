using System.Text.Json;
using hds;
namespace hds.Tests;

public partial class MissionNpcInteriorCheckpointTests
{
    [Fact] public void WitnessedReplayWithFurnitureUsesActualStageChoicesWithoutNewRandomOrSelection()
    {
        var p=new MissionProgress(Definition()){phaseIndex=1,persistenceId="furniture-replay",startedAt=123};var c=Client(p);var site=Site();var provider=new Provider();
        site.containers.Add(Wood());site.objectSpots.Add(Marker());
        Assert.True(MissionNpcInteriorPlanning.Prepare(c,p,2,site,provider,out _,out var selected,out var why),why);
        p.npcInteriorPending=selected;p.waitingForExit=true;
        Assert.True(MissionNpcInteriorAdmission.Replay(c,p,2,new[]{site},provider,out var replay,out why),why);
        Assert.Null(replay.Selected.Random);Assert.Same(selected,replay.Pending);int reads=provider.Reads;
        p.NextPhase();replay.RequireStaging(c);
        var replayRandom=new CountingRandom(724);
        Assert.Empty(MissionStaging.StageSourceChoices(p.mission.phases[2],replay.Selected.Site,replayRandom,replay,99));
        Assert.Equal(0,replayRandom.Draws);
        Assert.Equal(reads,provider.Reads);Assert.Same(selected,p.npcInteriorPending);Assert.Equal(2,p.phaseIndex);
    }
    private sealed class CountingRandom : Random
    {
        internal int Draws;
        internal CountingRandom(int seed):base(seed){}
        public override int Next(){Draws++;return base.Next();}
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void FreshAndNonAdoptedNpcStageChoicesKeepOriginalFurnitureShuffleDraws(bool adopted)
    {
        var prepared=Prepared();var p=prepared.Progress;var c=Client(p);var site=prepared.Site;site.containers.Add(Wood());site.objectSpots.Add(Marker());
        var expected=new CountingRandom(724);var actual=new CountingRandom(724);
        var plan=new MissionNpcInteriorAdmission.Plan(c,p,2,new MissionStaging.SiteSelection(site,actual),adopted?prepared.Pending:null!);
        Assert.False(plan.Replayed);Assert.Empty(MissionStaging.PrepareSourcePoolChoices(p.mission.phases[2],site,expected,0));
        Assert.Empty(MissionStaging.StageSourceChoices(p.mission.phases[2],site,actual,plan,0));
        Assert.True(actual.Draws>0);Assert.Equal(expected.Draws,actual.Draws);Assert.Equal(expected.Next(),actual.Next());
    }
    [Fact] public void ExitEscortUsesTheSameOwnedNpcFloorAndRefusesConflictingHistory()
    {
        var prepared=Prepared();var p=prepared.Progress;p.NextPhase();p.site=prepared.Site;p.sites.Add(p.site);
        var witness=prepared.Pending.Witness;p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=witness;
        p.Phase.missionObjectives.Add(new MissionObjective{goal=1,type=MissionGoalType.Escort,npcId=6,escortTo="exit"});p.following.Add(6);
        var c=Client(p);var floor=witness.Floor.Position;var mob=new Mob();mob.missionNpcId=6;mob.setHealthM(100);mob.setHealthC(100);
        mob.setXPos(floor[0]);mob.setYPos(floor[1]-50);mob.setZPos(floor[2]);c.playerData.missionMobs.Add(mob);
        var used=MissionNpcInteriorPlanning.ReadRaw(witness.Floor.RawDoorJson);
        Assert.True(MissionStaging.ExitEscortRide(c,p,p.site,used,floor[0],floor[1]-50,floor[2]));
        // Corrupted cached history must refuse through the existing authority, never guess legacy raw pose.
        var changed=(double[])floor.Clone();changed[1]++;
        var other=new MissionNpcInteriorEntry(witness.Floor.RawDoorJson,witness.Floor.EvidenceDigest,witness.Floor.Quaternion,witness.Floor.Node,changed,witness.Floor.Standing);
        p.npcInteriorWitnesses[1]=new MissionNpcInteriorWitness(witness.Version,witness.Kind,witness.Selection,other,witness.Lobby,witness.Actors,witness.Doors);
        Assert.Throws<MissionCheckpointException>(()=>MissionStaging.ExitEscortRide(c,p,p.site,used,floor[0],floor[1]-50,floor[2]));
    }
    [Fact] public void RuntimePostAdvanceGuardIsDistinctAndRequiresExactPublishedSiteWitness()
    {
        var prepared=Prepared();var p=prepared.Progress;var c=Client(p);
        var plan=new MissionNpcInteriorAdmission.Plan(c,p,2,new MissionStaging.SiteSelection(prepared.Site,null!),prepared.Pending);
        Assert.True(plan.BeforeCurrent(c));p.npcInteriorPending=prepared.Pending;
        Assert.True(plan.BeforeCurrent(c)); // Installing only THIS selected pending is the intentional allowed change.
        p.NextPhase();Assert.False(plan.BeforeCurrent(c));plan.RequireStaging(c);
        Assert.Throws<MissionCheckpointException>(()=>plan.RequirePublished(c));
        p.site=prepared.Site;p.sites.Add(prepared.Site);p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=prepared.Pending.Witness;
        plan.RequirePublished(c);p.site=Site();Assert.Throws<MissionCheckpointException>(()=>plan.RequirePublished(c));
        p.site=prepared.Site;c.playerData.lifeGeneration++;Assert.Throws<MissionCheckpointException>(()=>plan.RequirePublished(c));
    }
    [Theory] [InlineData("world")] [InlineData("reset")] [InlineData("death")] [InlineData("owner")] [InlineData("phase")]
    public void RuntimeStagingRejectsStaleOwnerWorldLifeAndTransport(string change)
    {
        var prepared=Prepared();var p=prepared.Progress;var c=Client(p);var plan=new MissionNpcInteriorAdmission.Plan(c,p,2,new MissionStaging.SiteSelection(prepared.Site,null!),prepared.Pending);
        p.NextPhase();plan.RequireStaging(c);
        if(change=="world")c.playerData.worldGeneration++;
        else if(change=="reset")c.TransportFence.PublishIncoming(true,()=>{});
        else if(change=="death")c.playerData.isDead=true;
        else if(change=="owner")c.playerData=new ClientData();
        else p.phaseIndex=1;
        Assert.Throws<MissionCheckpointException>(()=>plan.RequireStaging(c));
    }
    [Fact] public void DurableRefusalReplaysTheSameSelectionWithoutGeometryOrChooserAndKeepsItemsPhase()
    {
        var prepared=Prepared();var p=prepared.Progress;var c=Client(p);p.waitingForExit=true;p.itemSlots[6783]=6;
        var refused=new MissionNpcInteriorPending(2,MissionNpcInteriorPlanning.Kind,prepared.Pending.Selection,null!,"OriginalDeclaredEvidenceFailure");p.npcInteriorPending=refused;
        c.playerData.missionCheckpoint=JsonSerializer.Serialize(p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{prepared.Site}),MissionProgress.SaveJson);
        string original=c.playerData.missionCheckpoint;int reads=prepared.Provider.Reads;
        for(int i=0;i<2;i++)
        {
            Assert.False(MissionNpcInteriorAdmission.Replay(c,p,2,new[]{prepared.Site},prepared.Provider,out var plan,out var why));
            Assert.Equal(refused.Refusal,why);Assert.Same(refused,plan.Pending);Assert.True(plan.BeforeCurrent(c));
        }
        Assert.Equal(reads,prepared.Provider.Reads);Assert.Equal(1,p.phaseIndex);Assert.Equal((ushort)6,p.itemSlots[6783]);Assert.Equal(original,c.playerData.missionCheckpoint);
        Assert.True(MissionNpcInteriorAdmission.PendingDurable(c.playerData,refused));
        Assert.False(MissionNpcInteriorAdmission.PendingDurable(c.playerData,refused.WithRefusal("changed")));
    }
    [Fact] public void EmptyActorRefusalIsDurableButCannotBecomeAStagingWitness()
    {
        var prepared=Prepared();var p=prepared.Progress;var c=Client(p);p.waitingForExit=true;p.itemSlots[6783]=6;
        var selection=new MissionNpcInteriorSelection(p.mission.id,p.persistenceId,p.startedAt,2,MissionOpenFloorSite.From(prepared.Site),Array.Empty<MissionNpcInteriorActor>());
        p.npcInteriorPending=new MissionNpcInteriorPending(2,MissionNpcInteriorPlanning.Kind,selection,null!,"UnsupportedNpcRelativeArrival");
        var saved=p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{prepared.Site});string original=saved.NpcInterior.GetRawText();int reads=prepared.Provider.Reads;
        var restored=MissionProgress.RestoreWithNpcCatalog(p.mission,saved,new[]{prepared.Site},prepared.Provider);
        Assert.Null(restored.npcInteriorPending.Witness);Assert.Equal(original,restored.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{prepared.Site}).NpcInterior.GetRawText());
        Assert.Equal(reads,prepared.Provider.Reads);Assert.Equal((ushort)6,restored.itemSlots[6783]);
        Assert.False(MissionNpcInteriorAdmission.Replay(Client(restored),restored,2,new[]{prepared.Site},prepared.Provider,out var plan,out _));Assert.False(plan.Adopted);
        restored.NextPhase();Assert.Throws<MissionCheckpointException>(()=>plan.RequireStaging(Client(restored)));
    }
    [Fact] public void CertifiedActorUsesExactOwnRoleTargetAndDoesNotProjectOrBorrowAnotherSpot()
    {
        var prepared=Prepared();var p=prepared.Progress;p.NextPhase();p.site=prepared.Site;p.sites.Add(prepared.Site);p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=prepared.Pending.Witness;
        var actor=prepared.Pending.Selection.Actors[0];var point=MissionNpcInteriorAdmission.ActorPoint(p,"Npc",6,0,actor.Standing)!.Value;
        Assert.Equal(actor.Standing.Take(3),point.Position);Assert.Equal((ushort)43,point.location.region);Assert.Equal((ushort)245,point.location.polygon);
        var changed=actor.Standing;changed[0]++;Assert.Throws<MissionCheckpointException>(()=>MissionNpcInteriorAdmission.ActorPoint(p,"Npc",6,0,changed));
        Assert.Throws<MissionCheckpointException>(()=>MissionNpcInteriorAdmission.ActorPoint(p,"Npc",999,0,actor.Standing));
        p.npcInteriorWitnesses.Clear();Assert.Throws<MissionCheckpointException>(()=>MissionNpcInteriorAdmission.ActorPoint(p,"Npc",6,0,actor.Standing));
    }
    [Theory] [InlineData("pendingWitness")] [InlineData("historyWitness")] [InlineData("blankRefusal")] [InlineData("wrongOwner")]
    public void RefusalOnlyEmptyActorsCannotAuthorizeWitnessHistoryOrInvalidIdentity(string malformed)
    {
        var prepared=Prepared();var p=prepared.Progress;p.phaseIndex=2;p.site=prepared.Site;p.sites.Add(prepared.Site);p.itemSlots[6783]=6;
        var empty=new MissionNpcInteriorSelection(p.mission.id,p.persistenceId,p.startedAt,2,MissionOpenFloorSite.From(prepared.Site),Array.Empty<MissionNpcInteriorActor>());
        p.npcInteriorPending=new MissionNpcInteriorPending(2,MissionNpcInteriorPlanning.Kind,empty,null!,"OriginalRefusal");
        var saved=p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{prepared.Site});
        var node=System.Text.Json.Nodes.JsonNode.Parse(saved.NpcInterior.GetRawText())!;
        var emptyWitness=new MissionNpcInteriorWitness(2,MissionNpcInteriorPlanning.Kind,empty,prepared.Pending.Witness.Floor,prepared.Pending.Witness.Lobby,Array.Empty<MissionNpcInteriorActorProof>(),Array.Empty<MissionNpcInteriorDoor>());
        if(malformed=="pendingWitness")node["Pending"]!["Witness"]=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(emptyWitness,MissionProgress.SaveJson));
        else if(malformed=="historyWitness") {node["Pending"]=null;node["Witnesses"]=new System.Text.Json.Nodes.JsonArray(System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(emptyWitness,MissionProgress.SaveJson)));node["CurrentPhase"]=2;}
        else if(malformed=="blankRefusal")node["Pending"]!["Refusal"]=" ";
        else node["Pending"]!["Selection"]!["Identity"]="another-owner";
        saved.NpcInterior=JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());string raw=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);int reads=prepared.Provider.Reads;
        var ex=Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreWithNpcCatalog(p.mission,saved,new[]{prepared.Site},prepared.Provider));
        if(malformed.EndsWith("Witness"))Assert.Contains("NpcSelectionActorPrefix",ex.Message);
        Assert.Equal((ushort)6,ex.Progress.itemSlots[6783]);Assert.Equal(raw,JsonSerializer.Serialize(saved,MissionProgress.SaveJson));Assert.Equal(reads,prepared.Provider.Reads);
    }
    [Fact] public void DoorOverlayUsesItsOwnSelectedGoidAndRefusesChangedCatalogOrMissingWitness()
    {
        var prepared=Prepared();var p=prepared.Progress;p.NextPhase();p.site=prepared.Site;p.sites.Add(prepared.Site);p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=prepared.Pending.Witness;
        var wood=Wood();var resolved=MissionNpcInteriorAdmission.Overlay(p,wood,prepared.Provider)!;
        Assert.Equal(MissionElevatorPoses.OverlayKind.Resolved,resolved.Kind);Assert.Equal(38500,resolved.Position.X);Assert.Equal(6145,resolved.Position.Y);Assert.Equal(25800,resolved.Position.Z);
        Assert.Equal(prepared.Pending.Witness.Doors.Single().Quaternion,resolved.Quaternion);Assert.NotEqual(wood.quat,resolved.Quaternion);
        var other=Wood();other.type=BitConverter.GetBytes(343u);Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionNpcInteriorAdmission.Overlay(p,other,prepared.Provider)!.Kind);
        prepared.Provider.MissingMarker=true;Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionNpcInteriorAdmission.Overlay(p,wood,prepared.Provider)!.Kind);
        p.npcInteriorWitnesses.Clear();Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionNpcInteriorAdmission.Overlay(p,prepared.Site.elevatorDoor,prepared.Provider)!.Kind);
    }
    [Fact] public void LegacyNpcOnlyStateRemainsUnadoptedOutsideExplicitSectorScope()
    {
        var p=new MissionProgress(Definition()){phaseIndex=1};string before=JsonSerializer.Serialize(p.Capture(47,Array.Empty<Mob>()),MissionProgress.SaveJson);
        Assert.Null(MissionNpcInteriorAdmission.Current(p));Assert.Null(MissionNpcInteriorAdmission.ActorPoint(p,"Npc",6,0,new[]{1d,2,3,4}));Assert.Null(MissionNpcInteriorAdmission.Overlay(p,Wood()));
        var site=Site();site.elevatorDoor.sectorID=1;Assert.False(MissionNpcInteriorAdmission.Declared(site));Assert.True(MissionNpcInteriorAdmission.Candidate(p.Phase));
        Assert.Equal(before,JsonSerializer.Serialize(p.Capture(47,Array.Empty<Mob>()),MissionProgress.SaveJson));
    }
}
