using System.Text.Json;
using hds;
namespace HeadlessClient;

/// <summary>Read-only entry authority from an already owned NPC-v2 witness; no chooser/repair.</summary>
public static class MissionNpcInteriorRide
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static void Need(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    static double[] Vector(JsonElement e,string name){var a=e.GetProperty(name).EnumerateArray().Select(v=>v.GetDouble()).ToArray();Need(a.Length==3&&a.All(double.IsFinite),"NPC ride finite triple");return a;}
    static StaticWorldObject Raw(string text)=>JsonSerializer.Deserialize<StaticWorldObject>(text,Fields)??throw new InvalidDataException("Missing NPC raw entry");
    static void Shape(JsonElement e,params string[] names)=>Need(e.ValueKind==JsonValueKind.Object&&e.EnumerateObject().Select(p=>p.Name).Distinct().Count()==e.EnumerateObject().Count()&&names.All(n=>e.TryGetProperty(n,out _)),"NPC ride object shape");
    static MissionNpcInteriorProof.Door Entry(JsonElement e,StaticWorldObject? expected)
    {
        Shape(e,"RawDoorJson","Position","Quaternion","Standing","Node","EvidenceDigest");string text=e.GetProperty("RawDoorJson").GetString()!;var raw=Raw(text);
        Need(raw.metrId==1&&raw.type?.Length>=2&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(raw.type))&&raw.mxoStaticId==((uint)raw.sectorID<<20|raw.staticId),"NPC ride exact entry world/key/type");
        if(expected!=null)Need(JsonSerializer.Serialize(raw,Fields)==JsonSerializer.Serialize(expected,Fields),"NPC floor raw identity differs from selected saved door");
        string q=e.GetProperty("Quaternion").GetString()!;byte[] bytes;try{bytes=Convert.FromHexString(q);}catch(FormatException ex){throw new InvalidDataException("NPC ride quaternion",ex);}
        Need(bytes.Length==16&&Enumerable.Range(0,4).All(i=>float.IsFinite(BitConverter.ToSingle(bytes,i*4))),"NPC ride finite quaternion");
        Shape(e.GetProperty("Node"),"Region","Polygon");string digest=e.GetProperty("EvidenceDigest").GetString()!;Need(digest.Length==64&&digest.All(Uri.IsHexDigit),"NPC entry evidence digest");
        return new(raw.mxoStaticId,BitConverter.ToUInt16(raw.type),Vector(e,"Position"),q,Vector(e,"Standing"),text);
    }
    public static MissionNpcInteriorProof.Plan? ForSite(MissionProgress.SavedState state)
    {
        var e=state.NpcInterior;if(e.ValueKind==JsonValueKind.Undefined)return null;
        Shape(e,"Version","Kind","Pending","CurrentPhase","Witnesses");Need(e.GetProperty("Version").GetInt32()==2&&e.GetProperty("Kind").GetString()=="NpcInterior","NPC envelope version/kind");
        Need(state.Version==2&&state.OpenFloorSites!=null&&state.OpenFloorCurrentSiteIndex.HasValue&&Guid.TryParseExact(state.Identity,"N",out _)&&state.Started>0,"NPC ride current owned site scope");
        int index=state.OpenFloorCurrentSiteIndex!.Value;Need(index>=0&&index<state.OpenFloorSites!.Length&&index<state.SiteDoors.Length,"NPC current site index");
        var site=state.OpenFloorSites[index];var raw=state.SiteDoors[index];
        Need(site.District==1&&raw.metrId==1&&site.DoorKey==raw.mxoStaticId&&site.DoorX==raw.pos_x&&site.DoorY==raw.pos_y&&site.DoorZ==raw.pos_z,"NPC saved raw/site identity");
        var matches=e.GetProperty("Witnesses").EnumerateArray().Where(w=>w.GetProperty("Selection").GetProperty("Site").GetProperty("Fingerprint").GetString()==site.Fingerprint).ToArray();
        var currentPhase=e.GetProperty("CurrentPhase");
        Need(currentPhase.ValueKind==JsonValueKind.Null||currentPhase.ValueKind==JsonValueKind.Number&&currentPhase.GetInt32()>=0&&currentPhase.GetInt32()<=state.Phase,"NPC current marker is not future/invalid");
        bool current=currentPhase.ValueKind==JsonValueKind.Number&&currentPhase.GetInt32()==state.Phase;
        if(matches.Length==0){Need(!current,"Current NPC phase missing its selected site witness");return null;}
        foreach(var candidate in matches)
        {
            Shape(candidate,"Version","Kind","Selection","Floor","Lobby","Actors","Doors");
            Need(candidate.GetProperty("Version").GetInt32()==2&&candidate.GetProperty("Kind").GetString()=="NpcInterior","NPC witness version/kind");
            var c=candidate.GetProperty("Selection");Shape(c,"Mission","Identity","Started","Phase","Site","Actors");
            Need(c.GetProperty("Mission").GetInt32()==state.Mission&&c.GetProperty("Identity").GetString()==state.Identity&&c.GetProperty("Started").GetInt64()==state.Started&&
                c.GetProperty("Phase").GetInt32()>=0&&c.GetProperty("Phase").GetInt32()<=state.Phase,"Foreign/future NPC witness history");
            var captured=JsonSerializer.Deserialize<MissionOpenFloorSite>(c.GetProperty("Site").GetRawText())!;
            Need(captured.District==site.District&&captured.DoorKey==site.DoorKey&&captured.DoorX==site.DoorX&&captured.DoorY==site.DoorY&&captured.DoorZ==site.DoorZ,"NPC witness selected raw site metadata");
        }
        string Pair(JsonElement w)=>w.GetProperty("Floor").GetRawText()+w.GetProperty("Lobby").GetRawText();
        Need(matches.All(w=>Pair(w)==Pair(matches[0])),"Conflicting owned NPC entry history");
        var active=matches.Where(w=>w.GetProperty("Selection").GetProperty("Phase").GetInt32()==state.Phase).ToArray();
        Need(!current||active.Length==1,"Missing/duplicate current NPC witness");var w=current?active[0]:matches[0];
        Shape(w,"Version","Kind","Selection","Floor","Lobby","Actors","Doors");Need(w.GetProperty("Version").GetInt32()==2&&w.GetProperty("Kind").GetString()=="NpcInterior","NPC witness version/kind");
        var selection=w.GetProperty("Selection");Shape(selection,"Mission","Identity","Started","Phase","Site","Actors");
        Need(selection.GetProperty("Phase").GetInt32()>=0&&selection.GetProperty("Phase").GetInt32()<=state.Phase,"Future NPC witness is not owned history");
        Need(selection.GetProperty("Mission").GetInt32()==state.Mission&&selection.GetProperty("Identity").GetString()==state.Identity&&selection.GetProperty("Started").GetInt64()==state.Started,"NPC witness owner/mission/start");
        var capturedSite=JsonSerializer.Deserialize<MissionOpenFloorSite>(selection.GetProperty("Site").GetRawText())!;
        Need(capturedSite.District==site.District&&capturedSite.DoorKey==site.DoorKey&&capturedSite.DoorX==site.DoorX&&capturedSite.DoorY==site.DoorY&&capturedSite.DoorZ==site.DoorZ,"NPC witness selected raw site metadata");
        var floor=Entry(w.GetProperty("Floor"),raw);var lobby=Entry(w.GetProperty("Lobby"),null);
        Need(state.Waypoints.Length==1&&state.Exits.Length==1&&BitConverter.ToUInt32(Convert.FromHexString(state.Waypoints[0].doorHex))==floor.Key&&
            new[]{state.Waypoints[0].x,state.Waypoints[0].y,state.Waypoints[0].z}.SequenceEqual(floor.Position)&&state.Exits[0].SequenceEqual(lobby.Position),"NPC measured floor/lobby must equal current saved waypoints/exits");
        return new(state.Identity,state.Started,state.Phase,site.Fingerprint,w.GetRawText(),w.Clone(),floor,lobby);
    }
}
