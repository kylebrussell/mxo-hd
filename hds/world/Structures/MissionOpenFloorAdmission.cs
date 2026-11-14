using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace hds
{
    internal static class MissionOpenFloorAdmission
    {
        internal const string UnavailableMessage="The next mission area cannot be placed safely. Retry the exit or abort the mission.";
        internal static bool Requires(MissionPhase phase) => phase?.site!=null&&phase.objects.Any(o=>new[]{"Box","Computer","Safe"}.Contains(o.kind,StringComparer.OrdinalIgnoreCase));
        internal static bool Target(ushort goid,StaticWorldObject raw) => raw?.type!=null&&raw.type.Length>=2&&(goid==3||goid==548||goid==552)&&MissionSites.Goid(raw)!=0&&MissionSites.Goid(raw)!=goid;
        internal static string MeshFingerprint(IReadOnlyList<MissionOpenFloorResolver.Surface> surfaces) => MissionOpenFloorSite.Hash(JsonSerializer.Serialize(
            surfaces.OrderBy(s=>s.Identity).Select(s=>new {s.Identity.Sector,s.Identity.Region,s.Identity.Polygon,s.FloorX,s.FloorZ,s.FloorC,
                Points=s.Vertices.Select(p=>new[]{p.X,p.Z}).ToArray()}).ToArray()));
        internal static bool ValidSite(MissionSite s) => s!=null&&ValidSpot(s.elevatorDoor)&&ValidSpot(s.lobbyDoor)&&s.shaft!=null&&s.npcSpots.Count>0&&
            new MissionOpenFloorResolver.Bounds(s.minX,s.maxX,s.minZ,s.maxZ).Valid;
        internal static bool ValidSpot(StaticWorldObject s) => s!=null&&s.type!=null&&(s.type.Length==2||s.type.Length==4)&&
            double.IsFinite(s.pos_x)&&double.IsFinite(s.pos_y)&&double.IsFinite(s.pos_z)&&double.IsFinite(s.rot)&&s.quat!=null;
        private static bool MatchesChoice(MissionPhase phase,MissionSite site,MissionOpenFloorChoice choice,int index)
        {
            if(choice==null||string.IsNullOrWhiteSpace(choice.RawSpotJson)||index>=phase.objects.Count||choice.ObjectId!=phase.objects[index].id||string.IsNullOrWhiteSpace(phase.objects[index].kind))return false;
            StaticWorldObject spot;try {spot=choice.ReadSpot();}catch(JsonException){return false;}
            if(!ValidSpot(spot)||spot.metrId!=site.metrId||choice.Goid!=MissionStaging.ExpectedConcrete(phase.objects[index],spot))return false;
            string fingerprint=MissionOpenFloorSite.SpotFingerprint(spot);
            return site.objectSpots.Concat(site.containers).Concat(site.realObjects).Count(s=>MissionOpenFloorSite.SpotFingerprint(s)==fingerprint)==1;
        }
        internal static bool ChoicesValid(MissionPhase phase,MissionSite site,IReadOnlyList<MissionOpenFloorChoice> choices) =>
            phase!=null&&site!=null&&choices!=null&&choices.Count==phase.objects.Count&&choices.Select(c=>c?.ObjectId).Distinct().Count()==choices.Count&&
            choices.Select((c,i)=>MatchesChoice(phase,site,c,i)).All(x=>x)&&
            choices.Select(c=>MissionOpenFloorSite.SpotFingerprint(c.ReadSpot())).Distinct().Count()==choices.Count;

        internal sealed class Plan
        {
            internal readonly MissionProgress Progress;
            internal readonly MissionSite Site;
            internal readonly MissionOpenFloorPendingAdmission Selection;
            internal readonly IReadOnlyDictionary<ushort,MissionOpenFloorPlacement> Poses;
            private readonly ClientData owner;
            private readonly MissionProgress before;
            private readonly int beforePhase,world,life;
            private readonly long transport;
            private readonly uint character;
            internal Plan(WorldClient c,MissionProgress progress,int phase,MissionSite site,MissionOpenFloorPendingAdmission selection,
                IDictionary<ushort,MissionOpenFloorPlacement> poses,MissionProgress expectedBefore,int expectedPhase,ClientData expectedOwner,int expectedWorld,int expectedLife,long expectedTransport,uint expectedCharacter)
            {
                owner=expectedOwner;before=expectedBefore;beforePhase=expectedPhase;Progress=progress;Site=site;Selection=selection;
                Poses=new System.Collections.ObjectModel.ReadOnlyDictionary<ushort,MissionOpenFloorPlacement>(new Dictionary<ushort,MissionOpenFloorPlacement>(poses));
                world=expectedWorld;life=expectedLife;transport=expectedTransport;character=expectedCharacter;
            }
            internal bool Adopted => Poses.Count>0;
            internal bool BindOutput(WorldClient c) => !Adopted||MissionOutput.BindCurrent(c.messageQueue,c.TransportFence,transport,world);
            internal bool WorldCurrent(WorldClient c) => ReferenceEquals(c.playerData,owner)&&owner.getCharID()==character&&c.Alive&&owner.getOnWorld()&&
                !owner.worldChangePending&&!owner.waitForRPCShutDown&&owner.worldGeneration==world&&owner.lifeGeneration==life&&
                owner.getDistrictId()==Progress.mission.district&&c.TransportFence.Capture()==transport;
            internal bool BeforeCurrent(WorldClient c) => WorldCurrent(c)&&ReferenceEquals(owner.missionProgress,before)&&
                (before==null||before.phaseIndex==beforePhase)&&Selection.Started==Progress.startedAt&&Selection.Mission==Progress.mission.id&&Selection.Identity==Progress.persistenceId&&
                Progress.phaseIndex==(ReferenceEquals(before,Progress)?beforePhase:Selection.Phase)&&
                (Site==null?Selection.Site==null:Selection.Site!=null&&MissionOpenFloorSite.CatalogFingerprint(Site)==Selection.Site.Fingerprint);
            internal void RequireCurrent(WorldClient c,MissionProgress p)
            {
                if(!WorldCurrent(c)||!ReferenceEquals(owner.missionProgress,p)||!ReferenceEquals(p,Progress)||p.phaseIndex!=Selection.Phase||
                    p.startedAt!=Selection.Started||MissionOpenFloorSite.CatalogFingerprint(Site)!=Selection.Site.Fingerprint)
                    throw new MissionCheckpointException("Stale open-floor placement admission");
            }
        }
        internal static bool TryPrepare(WorldClient client,MissionProgress progress,int phaseIndex,MissionProgress before,
            out Plan plan,out MissionOpenFloorPendingAdmission selected,out string refusal,IReadOnlyList<MissionSite> siteCatalog=null)
        {
            plan=null;selected=null;refusal=null;ClientData data=client.playerData;
            int world=data.worldGeneration,life=data.lifeGeneration,beforePhase=before?.phaseIndex??-1;long transport=client.TransportFence.Capture();uint character=data.getCharID();
            bool Current() => ReferenceEquals(client.playerData,data)&&client.Alive&&data.getOnWorld()&&!data.worldChangePending&&!data.waitForRPCShutDown&&
                data.worldGeneration==world&&data.lifeGeneration==life&&data.getCharID()==character&&data.getDistrictId()==progress.mission.district&&
                client.TransportFence.Capture()==transport&&ReferenceEquals(data.missionProgress,before)&&(before==null||before.phaseIndex==beforePhase);
            if(!Current()) {refusal="StaleWorld";return false;}
            Mission mission=progress.mission;if(phaseIndex<0||phaseIndex>=mission.phases.Count||!Requires(mission.phases[phaseIndex])) {refusal="NotAopen-floorPhase";return false;}
            MissionPhase phase=mission.phases[phaseIndex];MissionSite site;MissionOpenFloorChoice[] choices;
            if(progress.pendingOpenFloor!=null)
            {
                selected=progress.pendingOpenFloor;
                if(!PendingValid(progress,selected,siteCatalog)||selected.Phase!=phaseIndex) {refusal="InvalidPendingChoice";return false;}
                site=selected.Site?.Resolve(siteCatalog??MissionSites.For(mission.district));choices=selected.Choices;
            }
            else
            {
                double x=0,y=0,z=0;NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(),ref x,ref y,ref z);
                int at=phaseIndex+1;Random rng=MissionStaging.LayoutRandom(mission.id,phaseIndex,
                    unchecked((int)data.getCharID()*7919+mission.id*31+at+(int)progress.startedAt));
                var sites=siteCatalog??MissionSites.For(mission.district);site=phaseIndex==0&&data.missionPreviewFor==mission?data.missionPreview:null;
                var hoods=phase.neighbourhoods.Count>0?phase.neighbourhoods:mission.neighbourhoods;
                int npcs=mission.npcs.Count(n=>n.at==at)+MissionStaging.SpotsForWaves(phase),objects=MissionStaging.PlacesNeeded(phase),furniture=MissionStaging.FurnitureNeeded(phase);
                site=site??MissionSites.Choose(sites,phase.site,x,z,npcs,objects,rng,progress.sites,hoods,requiredFurniture:furniture)
                    ??MissionSites.Choose(sites,"Any",x,z,npcs,objects,rng,progress.sites,hoods,requiredFurniture:furniture);

                choices=site==null?Array.Empty<MissionOpenFloorChoice>():MissionStaging.SelectObjects(phase,site,rng).Select(MissionOpenFloorChoice.From).ToArray();
                selected=new MissionOpenFloorPendingAdmission(1,mission.id,progress.persistenceId,phaseIndex,progress.startedAt,site==null?null:MissionOpenFloorSite.From(site),choices,site==null?"NoSite":null);
            }
            if(!Current()) {selected=null;refusal="StaleWorld";return false;}
            plan=new Plan(client,progress,phaseIndex,site,selected,new Dictionary<ushort,MissionOpenFloorPlacement>(),before,beforePhase,data,world,life,transport,character);
            if(site==null) {refusal="NoSite";return false;}
            bool hasTargets=choices.Any(c=>Target(c.Goid,c.ReadSpot()));
            if(hasTargets&&(!ValidSite(site)||selected.Site.Resolve(siteCatalog??MissionSites.For(mission.district))==null||!ChoicesValid(phase,site,choices))) {refusal="MissingAmbiguousOrRepeatedObjectSource";return false;}
            bool geometry=TryGeometry(mission,phaseIndex,site,choices,out var poses,out refusal,selected);
            if(!Current()||MissionOpenFloorSite.CatalogFingerprint(site)!=selected.Site.Fingerprint) {selected=null;refusal="StaleWorldOrSource";return false;}
            if(!geometry)return false;
            plan=new Plan(client,progress,phaseIndex,site,selected,poses,before,beforePhase,data,world,life,transport,character);return true;
        }
        internal static bool TryGeometry(Mission mission,int phaseIndex,MissionSite site,IReadOnlyList<MissionOpenFloorChoice> choices,
            out Dictionary<ushort,MissionOpenFloorPlacement> poses,out string refusal,MissionOpenFloorPendingAdmission pending=null)
        {
            poses=new Dictionary<ushort,MissionOpenFloorPlacement>();refusal=null;
            if(mission==null||phaseIndex<0||phaseIndex>=mission.phases.Count||choices==null||choices.Any(c=>c==null)){refusal="InvalidPhaseChoices";return false;}
            if(!choices.Any(c=>Target(c.Goid,c.ReadSpot())))return true; // Same-prototype bound selections remain legacy, with no metadata adoption.
            if(!ValidSite(site)||!ChoicesValid(mission.phases[phaseIndex],site,choices)){refusal="InvalidObjectChoices";return false;}
            var inputs=new List<MissionOpenFloorPhasePlanner.Input>();
            foreach(var choice in choices.Where(c=>Target(c.Goid,c.ReadSpot())))
            {
                var raw=choice.ReadSpot();
                if(!MissionOpenFloorRequests.TryCreate(raw,choice.Goid,site,mission.district,out var request,out refusal,mission.phases[phaseIndex],choice.ObjectId))return false;
                inputs.Add(new MissionOpenFloorPhasePlanner.Input(choice.ObjectId,request));
            }
            string geometryKey=MissionOpenFloorSite.Hash(MissionOpenFloorPhasePlanner.SearchPolicyVersion+":"+string.Join(";",inputs.OrderBy(i=>i.ObjectId).Select(i=>i.ObjectId+":"+i.Request.Fingerprint)));
            if(pending?.TryFailedGeometry(geometryKey,out refusal)==true)return false;
            if(!MissionOpenFloorPhasePlanner.TryPlan(inputs,out var planned,out refusal)){pending?.RememberFailedGeometry(geometryKey,refusal);return false;}
            foreach(var choice in choices.Where(c=>Target(c.Goid,c.ReadSpot())))
                poses.Add(choice.ObjectId,MissionOpenFloorPlacement.From(mission.id,phaseIndex,choice.ObjectId,choice.ReadSpot(),planned.Results[choice.ObjectId]));
            Output.WriteDebugLog("[MISSION] Open-floor phase "+mission.id+"/"+phaseIndex+" order="+string.Join(",",planned.Order)+" attempts="+planned.Attempts+" bodyNodes="+planned.BodyNodes+" routeGraphs="+planned.RouteGraphs+" staticRootGraphs="+planned.StaticRootGraphs+" directSingle="+planned.DirectSingle+" geometryMs="+planned.Milliseconds);
            return true;
        }
        private static bool FixedSetClear(MissionProgress progress,out string refusal)
        {
            refusal=null;var results=new List<MissionOpenFloorResolver.Result>();
            foreach(var state in progress.Phase.objects.Where(d=>progress.objects.TryGetValue(d.id,out var s)&&s.OpenFloor!=null).Select(d=>progress.objects[d.id]))
            {
                if(!MissionOpenFloorRequests.TryCreate(state.spot,state.goid,progress.site,progress.mission.district,out var request,out refusal,progress.Phase,state.def.id)||
                    !state.OpenFloor.TryReplay(request,progress.mission.id,progress.phaseIndex,state,out var result,out refusal))return false;
                results.Add(result);
            }
            return results.Count==0||MissionOpenFloorResolver.ValidateSet(results,out refusal);
        }
        internal static bool PendingDurable(ClientData data,MissionOpenFloorPendingAdmission expected)
        {
            if(data?.missionCheckpoint==null||expected==null)return false;
            try
            {
                var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(data.missionCheckpoint,MissionProgress.SaveJson);
                return saved.Version==2&&saved.PendingOpenFloor!=null&&
                    JsonSerializer.Serialize(saved.PendingOpenFloor,MissionProgress.SaveJson)==JsonSerializer.Serialize(expected,MissionProgress.SaveJson);
            }
            catch(JsonException){return false;}
        }
        internal static bool PendingValid(MissionProgress progress,MissionOpenFloorPendingAdmission p,IReadOnlyList<MissionSite> siteCatalog=null)
        {
            if(p==null||!progress.AllDone||!progress.waitingForExit||p.Policy!=1||p.Mission!=progress.mission.id||p.Identity!=progress.persistenceId||p.Started!=progress.startedAt||p.Phase!=progress.phaseIndex+1||p.Phase>=progress.mission.phases.Count||!Requires(progress.mission.phases[p.Phase]))return false;
            if(p.Site==null)return p.Choices.Length==0&&p.Refusal=="NoSite";
            var site=p.Site.Resolve(siteCatalog??MissionSites.For(progress.mission.district));return site!=null&&!progress.sites.Contains(site)&&ChoicesValid(progress.mission.phases[p.Phase],site,p.Choices);
        }
        internal static void MarkPendingCheckpoint(MissionProgress progress)
        {
            if(!progress.openFloorCheckpoint&&progress.objects.Values.Any(s=>Target(s.goid,s.spot)&&s.OpenFloor==null))
                progress.openFloorLegacyPhase=progress.phaseIndex;
            progress.openFloorCheckpoint=true;
        }
        internal static void ValidateRestored(MissionProgress progress,IReadOnlyList<MissionSite> siteCatalog=null)
        {
            if(progress.objects.Values.Any(s=>!ValidSpot(s.spot)))throw new InvalidOperationException("Invalid open-floor checkpoint raw source");
            var boxes=progress.objects.Values.Where(s=>s.OpenFloor!=null).ToArray();
            bool freeCurrent=progress.objects.Values.Any(s=>Target(s.goid,s.spot));
            if(progress.openFloorLegacyPhase.HasValue)
            {
                if(progress.openFloorLegacyPhase!=progress.phaseIndex||!freeCurrent||boxes.Length!=0||progress.openFloorResolvedPhase.HasValue||progress.pendingOpenFloor==null||!Requires(progress.Phase))
                    throw new InvalidOperationException("Invalid legacy-current open-floor upgrade provenance");
                var legacyChoices=progress.objects.Values.OrderBy(s=>progress.Phase.objects.IndexOf(s.def))
                    .Select(s=>new MissionOpenFloorChoice(s.def.id,s.goid,JsonSerializer.Serialize(s.spot,MissionProgress.SaveJson))).ToArray();
                if(!ChoicesValid(progress.Phase,progress.site,legacyChoices))throw new InvalidOperationException("Invalid legacy-current open-floor source choices");
            }
            else if(freeCurrent&&!progress.openFloorResolvedPhase.HasValue)throw new InvalidOperationException("Missing current open-floor adoption metadata");
            if(progress.openFloorResolvedPhase.HasValue)
            {
                if(progress.openFloorResolvedPhase!=progress.phaseIndex||!Requires(progress.Phase)||progress.objects.Count!=progress.Phase.objects.Count)
                    throw new InvalidOperationException("Invalid adopted open-floor phase identity/capacity");
                foreach(var state in progress.objects.Values)
                    if(Target(state.goid,state.spot)&&state.OpenFloor==null)
                        throw new InvalidOperationException("Missing adopted Open-floor resolved pose");
                var selected=progress.objects.Values.OrderBy(s=>progress.Phase.objects.IndexOf(s.def))
                    .Select(s=>new MissionOpenFloorChoice(s.def.id,s.goid,JsonSerializer.Serialize(s.spot,MissionProgress.SaveJson))).ToArray();
                if(!ChoicesValid(progress.Phase,progress.site,selected))throw new InvalidOperationException("Invalid adopted open-floor source choices");
            }
            else if(boxes.Length>0)throw new InvalidOperationException("Resolved open-floor without adopted phase marker");
            if(!FixedSetClear(progress,out string fixedRefusal))throw new InvalidOperationException("Invalid fixed open-floor checkpoint:"+fixedRefusal);
            if(progress.pendingOpenFloor!=null&&!PendingValid(progress,progress.pendingOpenFloor,siteCatalog))throw new InvalidOperationException("Invalid pending open-floor checkpoint choice");
        }
    }
    public static partial class MissionStaging
    {
        // Classify the reviewed1410 generated Safe as552 before proof. Missing anchor/scene then refuses in preflight; never silently falls back1416.
        internal static ushort GeneratedConcrete(StaticWorldObject spot,ushort fallback)=>fallback==1416&&spot?.type!=null&&spot.type.Length>=2&&MissionSites.Goid(spot)==1410?(ushort)552:fallback;
        internal static ushort ExpectedConcrete(MissionObjectDef def,StaticWorldObject spot)
        {
            ushort source=MissionSites.Goid(spot);
            if(MissionSites.RealObjects.TryGetValue(source,out string kind)&&string.Equals(kind,def.kind,StringComparison.OrdinalIgnoreCase))return source;
            return ObjectGoids.TryGetValue(def.kind,out ushort replacement)?GeneratedConcrete(spot,replacement):source;
        }
        private static void PlacePreparedObjects(WorldClient client,MissionProgress progress,MissionOpenFloorAdmission.Plan plan)
        {
            foreach(var c in plan.Selection.Choices)
            {
                var def=progress.Phase.objects.Single(o=>o.id==c.ObjectId);var raw=c.ReadSpot();
                if(raw==null){Output.WriteLine("[MISSION] Nowhere to put object "+def.id+" ("+def.kind+")");continue;}
                var state=new MissionObjectState {def=def,spot=raw,goid=c.Goid,
                    entity=EntityBase+(ulong)client.playerData.missionInstanceCounter*1000UL+(ulong)(progress.phaseIndex*50+def.id),
                    OpenFloor=plan.Poses.TryGetValue(c.ObjectId,out var pose)?pose:null};
                progress.objects.Add(c.ObjectId,state);SpawnObject(client,progress,state);var at=MissionObjectPoses.For(state);
                Output.WriteDebugLog("[MISSION] Object "+def.id+" ("+def.kind+") is a "+c.Goid+" at "+at.X+","+at.Y+","+at.Z);
                if(state.OpenFloor!=null)Output.WriteDebugLog("[MISSION] Open-floor resolved source "+state.spot.mxoStaticId.ToString("x8")+" raw "+state.spot.pos_x+","+state.spot.pos_y+","+state.spot.pos_z+" fixed "+at.X+","+at.Y+","+at.Z);
            }
        }
    }
}
