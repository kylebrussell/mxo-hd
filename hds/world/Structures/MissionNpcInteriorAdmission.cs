using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using G = hds.MissionOpenFloorResolver;
namespace hds
{
    // NPC-only adoption observes the ONE unchanged Stage selection. It never filters or rerolls it.
    internal static class MissionNpcInteriorAdmission
    {
        internal static bool Candidate(MissionPhase phase) => phase?.site != null && phase.objects.Count == 0;
        internal static bool Declared(MissionSite site) => site?.metrId == 1 && (site.elevatorDoor?.sectorID == 550 || site.elevatorDoor?.sectorID == 1349);
        internal sealed class Plan
        {
            internal readonly MissionProgress Progress;
            internal readonly MissionStaging.SiteSelection Selected;
            internal readonly MissionNpcInteriorPending Pending;
            private readonly MissionNpcInteriorBefore before;
            private readonly ClientData owner;
            private readonly long transport;
            private readonly int world, life, phase;
            private readonly uint character;
            private readonly string identity;
            private readonly long started;
            internal Plan(WorldClient c, MissionProgress p, int phase, MissionStaging.SiteSelection selected, MissionNpcInteriorPending pending)
            { Progress=p;Selected=selected;Pending=pending;this.phase=phase;identity=p.persistenceId;started=p.startedAt;before=new MissionNpcInteriorBefore(c,p,phase);owner=c.playerData;transport=c.TransportFence.Capture();world=owner.worldGeneration;life=owner.lifeGeneration;character=owner.getCharID(); }
            internal bool Adopted => Pending?.Witness != null;
            internal bool BeforeCurrent(WorldClient c) => before.Current(c,Pending);
            internal bool WorldCurrent(WorldClient c) => ReferenceEquals(c.playerData,owner)&&owner.getCharID()==character&&owner.worldGeneration==world&&owner.lifeGeneration==life&&
                c.Alive&&owner.getOnWorld()&&!owner.isDead&&!owner.worldChangePending&&!owner.waitForRPCShutDown&&owner.getDistrictId()==Progress.mission.district&&c.TransportFence.IsCurrent(transport);
            internal bool BindOutput(WorldClient c) => MissionOutput.BindCurrent(c.messageQueue,c.TransportFence,transport,world);
            // Deliberately distinct from BeforeCurrent: called AFTER the intentional NextPhase/Start owner change.
            internal void RequireStaging(WorldClient c)
            {
                if(!WorldCurrent(c)||!ReferenceEquals(owner.missionProgress,Progress)||!ReferenceEquals(owner.activeMission,Progress.mission)||
                    Progress.phaseIndex!=phase||Progress.persistenceId!=identity||Progress.startedAt!=started||
                    (Pending!=null&&(Progress.phaseIndex!=Pending.Selection.Phase||Pending.Selection.Identity!=Progress.persistenceId||Pending.Selection.Started!=Progress.startedAt))||
                    Adopted&&Selected.Site==null)throw new MissionCheckpointException("Stale NPC interior staging owner");
            }
            internal void RequirePublished(WorldClient c)
            {
                RequireStaging(c);
                if(!ReferenceEquals(Progress.site,Selected.Site)||Adopted&&!ReferenceEquals(Current(Progress),Pending.Witness))
                    throw new MissionCheckpointException("Changed NPC interior published site/witness");
            }
        }
        internal static bool TryPrepare(WorldClient c,MissionProgress p,int phase,out Plan plan,out string refusal)
        {
            plan=null;refusal=null;
            if(!Candidate(p.mission.phases[phase]))return true;
            var before=new MissionNpcInteriorBefore(c,p,phase);
            if(!before.Current(c)){refusal="StaleNpcBeforeState";return false;}
            if(p.npcInteriorPending!=null)
            {
                return Replay(c,p,phase,MissionSites.For(p.mission.district),MissionNpcInteriorEvidenceProvider.Instance,out plan,out refusal);
            }
            var selected=MissionStaging.ChooseSite(c,p,phase);
            if(!before.Current(c)){refusal="StaleNpcBeforeSelection";return false;}
            plan=new Plan(c,p,phase,selected,null);
            if(selected.Site==null||!Declared(selected.Site))return true;
            var load=MissionElevatorAnchors.Current(selected.Site.elevatorDoor);
            if(load.Kind==MissionElevatorAnchors.LoadKind.Unavailable){refusal=load.Refusal;plan=Refused(c,p,phase,selected,refusal);return false;}
            if(load.Kind!=MissionElevatorAnchors.LoadKind.Available||!load.Index.HasDoor(selected.Site.elevatorDoor))return true;
            if(!load.Index.NeedsCorrection(selected.Site.elevatorDoor,out bool corrected,out refusal)){plan=Refused(c,p,phase,selected,refusal);return false;}
            if(!corrected)return true;
            bool ok=MissionNpcInteriorPlanning.Prepare(c,p,phase,selected.Site,MissionNpcInteriorEvidenceProvider.Instance,out _,out var pendingSelection,out refusal);
            if(!before.Current(c)){plan=null;refusal="StaleNpcBeforeAdmission";return false;}
            plan=pendingSelection==null?Refused(c,p,phase,selected,refusal):new Plan(c,p,phase,selected,pendingSelection);return ok;
        }
        internal static bool Replay(WorldClient c,MissionProgress p,int phase,System.Collections.Generic.IReadOnlyList<MissionSite> catalog,IMissionNpcInteriorEvidenceProvider provider,out Plan plan,out string refusal)
        {
            plan=null;refusal="InvalidCommittedNpcPending";var pending=p.npcInteriorPending;
            var exact=pending?.Selection?.Site?.Resolve(catalog);
            if(exact==null||phase!=p.phaseIndex+1||!p.AllDone||!p.waitingForExit||pending.Selection.Phase!=phase||pending.Selection.Identity!=p.persistenceId||pending.Selection.Started!=p.startedAt)return false;
            plan=new Plan(c,p,phase,new MissionStaging.SiteSelection(exact,null),pending);
            if(!plan.BeforeCurrent(c))return false;
            if(pending.Witness==null){refusal=pending.Refusal;return false;}
            MissionNpcInteriorCheckpoint.ValidateGeometry(p,new MissionNpcInteriorEnvelope(2,MissionNpcInteriorPlanning.Kind,pending,null,Array.Empty<MissionNpcInteriorWitness>()),provider);
            refusal=null;return plan.BeforeCurrent(c);
        }
        private static Plan Refused(WorldClient c,MissionProgress p,int phase,MissionStaging.SiteSelection selected,string refusal) =>
            new Plan(c,p,phase,selected,new MissionNpcInteriorPending(2,MissionNpcInteriorPlanning.Kind,
                new MissionNpcInteriorSelection(p.mission.id,p.persistenceId,p.startedAt,phase,MissionOpenFloorSite.From(selected.Site),Array.Empty<MissionNpcInteriorActor>()),null,refusal??"NpcAdmissionRefused"));
        internal static bool PendingDurable(ClientData owner,MissionNpcInteriorPending expected)
        {
            if(owner?.missionCheckpoint==null||expected==null)return false;
            try {var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(owner.missionCheckpoint,MissionProgress.SaveJson);
                if(state.NpcInterior.ValueKind!=JsonValueKind.Object)return false;
                var envelope=JsonSerializer.Deserialize<MissionNpcInteriorEnvelope>(state.NpcInterior.GetRawText(),MissionProgress.SaveJson);
                return state.Version==2&&JsonSerializer.Serialize(envelope?.Pending,MissionProgress.SaveJson)==JsonSerializer.Serialize(expected,MissionProgress.SaveJson);}
            catch(JsonException){return false;}
        }
        internal static MissionNpcInteriorWitness Current(MissionProgress p) => p!=null&&p.npcInteriorResolvedPhase==p.phaseIndex&&p.npcInteriorWitnesses.TryGetValue(p.phaseIndex,out var w)&&
            w.Selection.Site.Fingerprint==MissionOpenFloorSite.CatalogFingerprint(p.site)?w:null;
        internal static MissionNpcInteriorWitness ForSite(MissionProgress p,MissionSite site)
        {
            if(p==null||site==null)return null;
            var found=p.npcInteriorWitnesses.Values.Where(w=>w.Selection.Site.Fingerprint==MissionOpenFloorSite.CatalogFingerprint(site)).ToArray();
            if(found.Length>1&&found.Any(w=>JsonSerializer.Serialize(new[]{w.Floor,w.Lobby},MissionProgress.SaveJson)!=JsonSerializer.Serialize(new[]{found[0].Floor,found[0].Lobby},MissionProgress.SaveJson)))throw new MissionCheckpointException("Conflicting NPC interior history");
            return found.FirstOrDefault();
        }
        internal static MissionElevatorPoses.Overlay Overlay(MissionProgress p,StaticWorldObject raw,IMissionNpcInteriorEvidenceProvider provider=null)
        {
            if(p==null||raw==null)return null;
            if(p.npcInteriorResolvedPhase==p.phaseIndex&&p.site!=null&&ForSite(p,p.site)==null&&
                (raw.mxoStaticId==p.site.elevatorDoor.mxoStaticId||raw.mxoStaticId==p.site.lobbyDoor.mxoStaticId))
                return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:"MissingNpcInteriorWitness");
            MissionElevatorPoses.Overlay result=null;
            foreach(var w in p.npcInteriorWitnesses.Values)
            {
                MissionNpcInteriorEntry entry=null;MissionNpcInteriorDoor wood=null;
                var floor=MissionNpcInteriorPlanning.ReadRaw(w.Floor.RawDoorJson);var lobby=MissionNpcInteriorPlanning.ReadRaw(w.Lobby.RawDoorJson);
                if(raw.metrId!=floor.metrId)continue;
                if(raw.mxoStaticId==floor.mxoStaticId)entry=w.Floor;
                else if(raw.mxoStaticId==lobby.mxoStaticId)entry=w.Lobby;
                else wood=w.Doors.SingleOrDefault(d=>d.Key==raw.mxoStaticId);
                if(entry==null&&wood==null)continue;
                if(p.restoreRefusal!=null)return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:"QuarantinedNpcInteriorDoor");
                string selected=entry?.RawDoorJson??wood.RawDoorJson;
                if(MissionNpcInteriorPlanning.RawJson(raw)!=selected)return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:"ChangedNpcDoorRawIdentityOrGoid");
                try {MissionNpcInteriorCheckpoint.ValidateGeometry(p,new MissionNpcInteriorEnvelope(2,MissionNpcInteriorPlanning.Kind,null,null,new[]{w}),provider??MissionNpcInteriorEvidenceProvider.Instance);}
                catch(MissionPlacementRestoreException ex){return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:ex.Message);}
                var position=entry?.Position??wood.Position;string q=entry?.Quaternion??wood.Quaternion;
                var candidate=new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Resolved,MissionElevatorPoses.Position(position),q);
                if(result!=null&&(!result.Position.Equals(candidate.Position)||result.Quaternion!=candidate.Quaternion))return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:"ConflictingNpcInteriorDoorHistory");
                result=candidate;
            }
            return result;
        }
        internal static bool OwnsDoor(MissionProgress p,StaticWorldObject raw) => p!=null&&raw!=null&&
            p.npcInteriorWitnesses.Values.Any(w=>w.Selection.Site.District==raw.metrId&&
                (MissionNpcInteriorPlanning.ReadRaw(w.Floor.RawDoorJson).mxoStaticId==raw.mxoStaticId||MissionNpcInteriorPlanning.ReadRaw(w.Lobby.RawDoorJson).mxoStaticId==raw.mxoStaticId||w.Doors.Any(d=>d.Key==raw.mxoStaticId)));
        internal static NavPoint? ActorPoint(MissionProgress p,string kind,int id,int index,double[] desired)
        {
            var w=Current(p);if(w==null){if(p!=null&&p.npcInteriorResolvedPhase==p.phaseIndex)throw new MissionCheckpointException("Missing current NPC interior actor witness");return null;}
            var actor=w.Selection.Actors.SingleOrDefault(a=>a.Kind==kind&&a.Id==id&&a.Index==index);
            if(actor==null||!actor.Standing.Take(3).SequenceEqual(desired.Take(3)))throw new MissionCheckpointException("Changed NPC interior actor role/position");
            var proof=w.Actors.Single(a=>a.Role==actor.Key);
            return new NavPoint {x=desired[0],y=desired[1],z=desired[2],location=new NavLocation {sector=p.site.elevatorDoor.sectorID,region=proof.Target.Region,polygon=proof.Target.Polygon,floorY=desired[1]-95,indoors=true}};
        }
    }
}
