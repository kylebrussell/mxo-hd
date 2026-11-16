using System;
using System.Linq;
using System.Text.Json;
namespace hds
{
    // Ordinary source choices for interior furniture/bound-only stages. No free-proxy or NPC witness is manufactured.
    internal static class MissionStreetAdmission
    {
        internal static bool Candidate(MissionProgress p,int phase)=>p.streetPolicy&&MissionStreetCheckpoint.KindFor(p.mission,phase)=="Objects"&&!MissionOpenFloorAdmission.Requires(p.mission.phases[phase]);
        internal sealed class Plan
        {
            internal readonly MissionProgress Progress;internal readonly MissionStaging.SiteSelection Selected;internal readonly MissionStreetPending Pending;
            private readonly MissionNpcInteriorBefore before;private readonly MissionStreetPending previous;private readonly ClientData owner;private readonly int world,life;
            private readonly long transport;private readonly uint character;
            internal Plan(WorldClient c,MissionProgress p,int phase,MissionStaging.SiteSelection selected,MissionStreetPending pending)
            {Progress=p;Selected=selected;Pending=pending;before=new MissionNpcInteriorBefore(c,p,phase);previous=p.streetPending;owner=c.playerData;world=owner.worldGeneration;life=owner.lifeGeneration;transport=c.TransportFence.Capture();character=owner.getCharID();}
            internal bool BeforeCurrent(WorldClient c)=>before.Current(c)&&(ReferenceEquals(Progress.streetPending,previous)||ReferenceEquals(Progress.streetPending,Pending));
            internal bool WorldCurrent(WorldClient c)=>ReferenceEquals(c.playerData,owner)&&owner.worldGeneration==world&&owner.lifeGeneration==life&&owner.getCharID()==character&&
                owner.getDistrictId()==Progress.mission.district&&owner.getOnWorld()&&!owner.isDead&&!owner.worldChangePending&&!owner.waitForRPCShutDown&&c.Alive&&c.TransportFence.IsCurrent(transport);
            internal bool BindOutput(WorldClient c)=>MissionOutput.BindCurrent(c.messageQueue,c.TransportFence,transport,world);
            internal void RequireStaging(WorldClient c)
            {
                if(!WorldCurrent(c)||!ReferenceEquals(owner.missionProgress,Progress)||!ReferenceEquals(owner.activeMission,Progress.mission)||Pending.Selection.Phase!=Progress.phaseIndex||
                    Pending.Selection.Mission!=Progress.mission.id||Pending.Selection.Identity!=Progress.persistenceId||Pending.Selection.Started!=Progress.startedAt||Pending.Selection.Refusal!=null||
                    Pending.Selection.Context==null||Pending.Selection.Site.Fingerprint!=MissionOpenFloorSite.CatalogFingerprint(Selected.Site))throw new MissionCheckpointException("Stale or refused ordinary street admission");
            }
            internal MissionStaging.ObjectChoice[] Choices()=>Pending.Choices.Select(c=>new MissionStaging.ObjectChoice(Progress.Phase.objects.Single(d=>d.id==c.ObjectId),c.ReadSpot(),c.Goid)).ToArray();
            internal void RequirePublished(WorldClient c)
            {RequireStaging(c);if(!ReferenceEquals(Progress.site,Selected.Site)||!Progress.streetWitnesses.TryGetValue(Progress.phaseIndex,out var saved)||saved.Context.Key!=Pending.Selection.Context.Key)throw new MissionCheckpointException("Changed ordinary street publication");}
        }
        internal static bool TryPrepare(WorldClient c,MissionProgress p,int phase,out Plan plan,out string refusal)
        {
            plan=null;refusal=null;var before=new MissionNpcInteriorBefore(c,p,phase);if(!before.Current(c)){refusal="StaleOrdinaryStreetOwner";return false;}
            if(p.streetPending!=null)
            {
                var pending=p.streetPending;var site=MissionStreetCheckpoint.ValidateCarrier(p,pending,MissionSites.For(p.mission.district));
                plan=new Plan(c,p,phase,new MissionStaging.SiteSelection(site,null),pending);if(!plan.BeforeCurrent(c))return false;
                if(pending.Selection.Refusal!=null){refusal=pending.Selection.Refusal;return false;}
                if(!MissionStreetContexts.ValidateSelected(pending.Selection.Context,site,MissionPublicDoors.CurrentDataset(),key=>DataLoader.getInstance().getObjectValues(key,p.mission.district),out refusal))return false;
                foreach(var choice in pending.Choices)MissionBoundObjectPoses.RequireCurrent(new MissionObjectState{spot=choice.ReadSpot(),goid=choice.Goid});
                return plan.BeforeCurrent(c);
            }
            var selected=MissionStaging.ChooseSite(c,p,phase);if(!before.Current(c)||selected.Site==null){refusal="NoCertifiedOrdinaryStreetSite";return false;}
            var definitions=p.mission.phases[phase];int npcs=p.mission.npcs.Count(n=>n.at==phase+1)+MissionStaging.SpotsForWaves(definitions);
            MissionOpenFloorChoice[] choices=Array.Empty<MissionOpenFloorChoice>();JsonElement json=default;bool accepted=false;
            try
            {
                if(!MissionStaging.TrySourcePools(definitions,selected.Site,npcs,out var pools,out string sourceRefusal))throw new MissionCheckpointException(sourceRefusal);
                choices=MissionStaging.SelectObjectsFromPools(definitions,selected.Site,selected.Random,pools).Select(MissionOpenFloorChoice.From).ToArray();
                if(!MissionOpenFloorAdmission.ChoicesValid(definitions,selected.Site,choices)||choices.Any(choice=>choice.Goid!=MissionSites.Goid(choice.ReadSpot())))throw new MissionCheckpointException("Ordinary street choices require unchanged native prototypes");
                foreach(var choice in choices)MissionBoundObjectPoses.RequireCurrent(new MissionObjectState{spot=choice.ReadSpot(),goid=choice.Goid});
                accepted=MissionStreetCoverage.TryPrepare(p,phase,selected.Site,out json,out refusal);
            }
            catch(Exception ex) when(ex is MissionCheckpointException||ex is System.IO.InvalidDataException||ex is JsonException||ex is ArgumentException)
            {refusal="OrdinaryStreetSourceRefused:"+ex.Message;if(!MissionOpenFloorAdmission.ChoicesValid(definitions,selected.Site,choices))choices=Array.Empty<MissionOpenFloorChoice>();}
            if(!before.Current(c)){refusal="StaleOrdinaryStreetSelection";return false;}
            MissionStreetSelection selection=json.ValueKind==JsonValueKind.Undefined?new MissionStreetSelection(1,p.mission.id,p.persistenceId,p.startedAt,phase,MissionOpenFloorSite.From(selected.Site),"Objects",null,refusal??"OrdinaryStreetContextUnavailable"):
                JsonSerializer.Deserialize<MissionStreetSelection>(json.GetRawText(),MissionProgress.SaveJson);
            plan=new Plan(c,p,phase,selected,new MissionStreetPending(selection,choices));return accepted&&plan.BeforeCurrent(c);
        }
        internal static bool PendingDurable(ClientData owner,MissionStreetPending expected)
        {
            if(owner?.missionCheckpoint==null||expected==null)return false;
            try{var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(owner.missionCheckpoint,MissionProgress.SaveJson);if(saved.StreetAccess.ValueKind!=JsonValueKind.Object)return false;
                var envelope=JsonSerializer.Deserialize<MissionStreetEnvelope>(saved.StreetAccess.GetRawText(),MissionProgress.SaveJson);return JsonSerializer.Serialize(envelope?.Pending,MissionProgress.SaveJson)==JsonSerializer.Serialize(expected,MissionProgress.SaveJson);}
            catch(JsonException){return false;}
        }
    }
}
