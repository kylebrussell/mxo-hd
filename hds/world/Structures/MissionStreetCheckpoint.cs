using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace hds
{
    internal sealed class MissionStreetSelection
    {
        public int Version {get;}public int Mission {get;}public string Identity {get;}public long Started {get;}public int Phase {get;}
        public MissionOpenFloorSite Site {get;}public string Kind {get;}public MissionStreetContexts.Context Context {get;}public string Refusal {get;}
        [JsonConstructor]public MissionStreetSelection(int version,int mission,string identity,long started,int phase,MissionOpenFloorSite site,string kind,MissionStreetContexts.Context context,string refusal)
        {Version=version;Mission=mission;Identity=identity;Started=started;Phase=phase;Site=site;Kind=kind;Context=context;Refusal=refusal;}
    }
    internal sealed class MissionStreetPending
    {
        public MissionStreetSelection Selection {get;}private readonly MissionOpenFloorChoice[] choices;
        public MissionOpenFloorChoice[] Choices=>(MissionOpenFloorChoice[])choices?.Clone();
        [JsonConstructor]public MissionStreetPending(MissionStreetSelection selection,MissionOpenFloorChoice[] choices){Selection=selection;this.choices=(MissionOpenFloorChoice[])choices?.Clone();}
    }
    internal sealed class MissionStreetEnvelope
    {
        public int Version {get;}public string Kind {get;}public bool Enabled {get;}[JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public MissionStreetPending Pending {get;}private readonly MissionStreetSelection[] witnesses;
        public MissionStreetSelection[] Witnesses=>(MissionStreetSelection[])witnesses?.Clone();
        [JsonConstructor]public MissionStreetEnvelope(int version,string kind,bool enabled,MissionStreetSelection[] witnesses,MissionStreetPending pending=null){Version=version;Kind=kind;Enabled=enabled;Pending=pending;this.witnesses=(MissionStreetSelection[])witnesses?.Clone();}
    }
    public partial class MissionProgress
    {
        internal readonly Dictionary<int,MissionStreetSelection> streetWitnesses=new Dictionary<int,MissionStreetSelection>();
        internal string streetShownWaypoint;internal bool streetPolicy;internal MissionStreetPending streetPending;
    }
    internal static class MissionStreetCheckpoint
    {
        private static void Need(MissionProgress p,bool value,string reason){if(!value)throw new MissionPlacementRestoreException(p,"Invalid street metadata:"+reason);}
        private static void Fields(MissionProgress p,JsonElement e,params string[] names)
        {Need(p,e.ValueKind==JsonValueKind.Object&&e.EnumerateObject().Count()==names.Length&&names.All(n=>e.TryGetProperty(n,out _)),"StreetFields");}
        private static void EntryShape(MissionProgress p,JsonElement e)
        {Fields(p,e,"RawDoorJson","EvidenceDigest","Quaternion","Node","Position","Standing");Fields(p,e.GetProperty("Node"),"Region","Polygon");}
        private static void SelectionShape(MissionProgress p,JsonElement e)
        {
            Fields(p,e,"Version","Mission","Identity","Started","Phase","Site","Kind","Context","Refusal");
            Fields(p,e.GetProperty("Site"),"District","DoorKey","DoorX","DoorY","DoorZ","Fingerprint");
            JsonElement c=e.GetProperty("Context");if(c.ValueKind==JsonValueKind.Null)return;
            Fields(p,c,"Key","Kind","Refusal","StreetDigest","Floor","Lobby","Door","Outside","Inside");EntryShape(p,c.GetProperty("Floor"));EntryShape(p,c.GetProperty("Lobby"));
            Fields(p,c.GetProperty("Door"),"Key","Goid","RawDoorJson","Quaternion","Position");
            foreach(string key in new[]{"Outside","Inside"})
            {Need(p,c.GetProperty(key).ValueKind==JsonValueKind.Array,"StreetNodeArray");foreach(var node in c.GetProperty(key).EnumerateArray())Fields(p,node,"Region","Polygon");}
        }
        internal static string KindFor(Mission mission,int phase)
        {
            if(mission?.district!=1||phase<0||phase>=mission.phases.Count||mission.phases[phase].site==null)return null;
            if(mission.phases[phase].objects.Count>0)return "Objects";
            return MissionNpcInteriorPlanning.Roles(mission,phase,out var roles,out _)&&roles.Length>0?"Npc":null;
        }
        private static bool SameEntry(MissionNpcInteriorEntry a,MissionNpcInteriorEntry b)=>JsonSerializer.Serialize(a,MissionProgress.SaveJson)==JsonSerializer.Serialize(b,MissionProgress.SaveJson);
        private static MissionSite Selection(MissionProgress p,MissionStreetSelection s,IReadOnlyList<MissionSite> catalogue,bool pending)
        {
            Need(p,s!=null&&s.Version==1&&s.Mission==p.mission.id&&s.Identity==p.persistenceId&&s.Started==p.startedAt&&s.Phase>=0&&s.Phase<p.mission.phases.Count&&
                (pending?s.Phase==p.phaseIndex+1:s.Phase<=p.phaseIndex)&&s.Site!=null&&s.Site.District==p.mission.district&&s.Kind==KindFor(p.mission,s.Phase),"StreetOwnerPhase");
            MissionSite site=s.Site.Resolve(catalogue);Need(p,site!=null,"MissingExactStreetSite");
            Need(p,s.Refusal==null||pending&&!string.IsNullOrWhiteSpace(s.Refusal),"StreetRefusalScope");
            if(s.Context==null){Need(p,pending&&s.Refusal!=null,"MissingStreetContext");return site;}
            Need(p,MissionStreetContexts.Valid(s.Context)&&s.Context.Kind==s.Kind&&s.Context.Floor.RawDoorJson==MissionNpcInteriorPlanning.RawJson(site.elevatorDoor)&&
                s.Context.Lobby.RawDoorJson==MissionNpcInteriorPlanning.RawJson(site.lobbyDoor)&&s.Context.Door.Key!=site.elevatorDoor.mxoStaticId&&s.Context.Door.Key!=site.lobbyDoor.mxoStaticId,"StreetSelectedContext");
            StaticWorldObject raw=MissionNpcInteriorPlanning.ReadRaw(s.Context.Door.RawDoorJson);
            Need(p,raw!=null&&raw.metrId==site.metrId&&raw.mxoStaticId==s.Context.Door.Key,"StreetPublicRawIdentity");return site;
        }
        internal static MissionStreetSelection Pending(MissionProgress p,JsonElement json,IReadOnlyList<MissionSite> catalogue)
        {
            if(json.ValueKind==JsonValueKind.Undefined)return null;
            try{SelectionShape(p,json);var selected=JsonSerializer.Deserialize<MissionStreetSelection>(json.GetRawText(),MissionProgress.SaveJson);Selection(p,selected,catalogue,true);return selected;}
            catch(Exception ex) when(ex is JsonException||ex is ArgumentException){throw new MissionPlacementRestoreException(p,"Invalid street pending metadata:"+ex.Message);}
        }
        internal static MissionStreetSelection Prepared(MissionProgress p,JsonElement json,MissionSite site,int phase)
        {
            SelectionShape(p,json);var selected=JsonSerializer.Deserialize<MissionStreetSelection>(json.GetRawText(),MissionProgress.SaveJson);
            Selection(p,selected,new[]{site},phase==p.phaseIndex+1);Need(p,selected.Phase==phase&&selected.Context!=null&&selected.Refusal==null,"RefusedStreetCannotPrepare");return selected;
        }
        internal static MissionStreetSelection Staging(MissionProgress p,JsonElement json,MissionSite site)
        {
            SelectionShape(p,json);var selected=JsonSerializer.Deserialize<MissionStreetSelection>(json.GetRawText(),MissionProgress.SaveJson);
            Selection(p,selected,new[]{site},false);Need(p,selected.Phase==p.phaseIndex&&selected.Refusal==null&&selected.Context!=null,"RefusedStreetCannotStage");return selected;
        }
        internal static JsonElement Capture(MissionProgress p)=>!p.streetPolicy&&p.streetWitnesses.Count==0&&p.streetPending==null?default:JsonSerializer.SerializeToElement(new MissionStreetEnvelope(1,"StreetAccess",p.streetPolicy,p.streetWitnesses.OrderBy(k=>k.Key).Select(k=>k.Value).ToArray(),p.streetPending),MissionProgress.SaveJson);
        internal static bool Present(MissionProgress.SavedState state)=>state.StreetAccess.ValueKind!=JsonValueKind.Undefined||state.PendingOpenFloor!=null&&state.PendingOpenFloor.Street.ValueKind!=JsonValueKind.Undefined;
        internal static MissionStreetEnvelope PrepareRestore(MissionProgress p,MissionProgress.SavedState state,IReadOnlyList<MissionSite> catalogue,MissionNpcInteriorEnvelope npc)
        {
            bool present=Present(state)||npc?.Pending!=null&&npc.Pending.Street.ValueKind!=JsonValueKind.Undefined;
            if(!present)return null;Need(p,state.Version==2,"StreetRequiresVersion2");Need(p,!string.IsNullOrWhiteSpace(state.Identity),"StreetOwnerIdentity");catalogue=catalogue??MissionSites.For(p.mission.district);
            Need(p,state.OpenFloorSites!=null&&state.OpenFloorCurrentSiteIndex.HasValue&&state.OpenFloorCurrentSiteIndex.Value>=-1&&state.OpenFloorCurrentSiteIndex.Value<state.OpenFloorSites.Length&&
                state.OpenFloorSites.All(s=>s!=null&&s.Resolve(catalogue)!=null),"StreetExactHistory");
            if(state.PendingOpenFloor!=null)
            {
                var selected=Pending(p,state.PendingOpenFloor.Street,catalogue);
                if(selected!=null)Need(p,selected.Kind=="Objects"&&selected.Phase==state.PendingOpenFloor.Phase&&selected.Site.Fingerprint==state.PendingOpenFloor.Site?.Fingerprint,"StreetPendingObjectParent");
            }
            if(npc?.Pending!=null)
            {
                var selected=Pending(p,npc.Pending.Street,catalogue);
                if(selected!=null)
                {
                    Need(p,selected.Kind=="Npc"&&selected.Phase==npc.Pending.Selection.Phase&&selected.Site.Fingerprint==npc.Pending.Selection.Site.Fingerprint,"StreetPendingNpcParent");
                    if(selected.Refusal==null)Need(p,npc.Pending.Witness!=null&&SameEntry(selected.Context.Floor,npc.Pending.Witness.Floor)&&SameEntry(selected.Context.Lobby,npc.Pending.Witness.Lobby),"StreetPendingNpcEntry");
                }
            }
            if(state.StreetAccess.ValueKind==JsonValueKind.Undefined)return null;
            try
            {
                if(state.StreetAccess.ValueKind==JsonValueKind.Object&&state.StreetAccess.TryGetProperty("Pending",out var genericPending))
                {
                    Fields(p,state.StreetAccess,"Version","Kind","Enabled","Witnesses","Pending");Fields(p,genericPending,"Selection","Choices");SelectionShape(p,genericPending.GetProperty("Selection"));
                    Need(p,genericPending.GetProperty("Choices").ValueKind==JsonValueKind.Array,"StreetOrdinaryChoiceArray");
                    foreach(var choice in genericPending.GetProperty("Choices").EnumerateArray())Fields(p,choice,"ObjectId","Goid","RawSpotJson");
                }
                else Fields(p,state.StreetAccess,"Version","Kind","Enabled","Witnesses");Need(p,state.StreetAccess.GetProperty("Witnesses").ValueKind==JsonValueKind.Array,"StreetWitnessArray");
                foreach(var row in state.StreetAccess.GetProperty("Witnesses").EnumerateArray())SelectionShape(p,row);
                var envelope=JsonSerializer.Deserialize<MissionStreetEnvelope>(state.StreetAccess.GetRawText(),MissionProgress.SaveJson);
                Need(p,envelope!=null&&envelope.Version==1&&envelope.Kind=="StreetAccess"&&envelope.Enabled&&envelope.Witnesses!=null&&envelope.Witnesses.Select(w=>w?.Phase).Distinct().Count()==envelope.Witnesses.Length,"StreetEnvelope");
                if(envelope.Pending!=null)ValidateCarrier(p,envelope.Pending,catalogue);
                if(state.OpenFloorCurrentSiteIndex.Value>=0&&KindFor(p.mission,p.phaseIndex)!=null)
                {
                    var current=state.OpenFloorSites[state.OpenFloorCurrentSiteIndex.Value];
                    Need(p,envelope.Witnesses.Any(w=>w!=null&&w.Phase==p.phaseIndex&&w.Site?.Fingerprint==current.Fingerprint),"MissingCurrentStreetContext");
                }
                foreach(var row in envelope.Witnesses)
                {
                    Selection(p,row,catalogue,false);Need(p,state.OpenFloorSites?.Count(s=>s.Fingerprint==row.Site.Fingerprint)==1,"StreetSiteHistory");
                    if(row.Kind=="Npc")
                    {
                        var parent=npc?.Witnesses.SingleOrDefault(w=>w.Selection.Phase==row.Phase&&w.Selection.Site.Fingerprint==row.Site.Fingerprint);
                        Need(p,parent!=null&&SameEntry(row.Context.Floor,parent.Floor)&&SameEntry(row.Context.Lobby,parent.Lobby),"StreetFixedNpcEntry");
                    }
                }
                return envelope;
            }
            catch(Exception ex) when(ex is JsonException||ex is ArgumentException){throw new MissionPlacementRestoreException(p,"Invalid fixed street metadata:"+ex.Message);}
        }
        internal static MissionSite ValidateCarrier(MissionProgress p,MissionStreetPending pending,IReadOnlyList<MissionSite> catalogue)
        {
            Need(p,pending!=null&&pending.Selection!=null,"MissingStreetOrdinarySelection");var site=Selection(p,pending.Selection,catalogue,true);
            if(pending.Selection.Refusal!=null&&pending.Selection.Context==null&&pending.Choices?.Length==0)return site; // Exact refusal-only carrier has no staging authority.
            Need(p,pending.Selection.Kind=="Objects"&&!MissionOpenFloorAdmission.Requires(p.mission.phases[pending.Selection.Phase])&&pending.Choices!=null&&
                MissionOpenFloorAdmission.ChoicesValid(p.mission.phases[pending.Selection.Phase],site,pending.Choices)&&pending.Choices.All(c=>c.Goid==MissionSites.Goid(c.ReadSpot())),"StreetOrdinarySourceChoices");
            return site;
        }
        internal static void Apply(MissionProgress p,MissionStreetEnvelope envelope)
        {
            if(envelope==null)return;p.streetPolicy=envelope.Enabled;p.streetPending=envelope.Pending;foreach(var row in envelope.Witnesses)p.streetWitnesses.Add(row.Phase,row);
            if(p.streetWitnesses.TryGetValue(p.phaseIndex,out var current))Need(p,p.site!=null&&current.Site.Fingerprint==MissionOpenFloorSite.CatalogFingerprint(p.site),"StreetCurrentSite");
        }
        internal static void ValidateGeometry(MissionProgress p,MissionStreetEnvelope envelope,MissionPublicDoors.Dataset dataset,Func<uint,StaticWorldObject> lookup,IReadOnlyList<MissionSite> catalogue=null)
        {
            if(envelope==null)return;foreach(var row in envelope.Witnesses)
            {
                var site=row.Site.Resolve(p.sites);Need(p,site!=null,"StreetFixedHistorySite");
                if(!MissionStreetContexts.ValidateSelected(row.Context,site,dataset,lookup,out string why))throw new MissionPlacementRestoreException(p,"Invalid fixed street context:"+why);
            }
            if(envelope.Pending?.Selection.Refusal==null&&envelope.Pending!=null)
            {
                var pending=envelope.Pending;var site=ValidateCarrier(p,pending,catalogue??MissionSites.For(p.mission.district));
                if(!MissionStreetContexts.ValidateSelected(pending.Selection.Context,site,dataset,lookup,out string why))throw new MissionPlacementRestoreException(p,"Invalid pending street context:"+why);
                try{foreach(var choice in pending.Choices)MissionBoundObjectPoses.RequireCurrent(new MissionObjectState{spot=choice.ReadSpot(),goid=choice.Goid});}
                catch(MissionCheckpointException ex){throw new MissionPlacementRestoreException(p,"Invalid pending street source:"+ex.Message);}
            }
        }
    }
}
