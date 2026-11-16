using System.Text.Json;
using hds;
namespace HeadlessClient;
// Reads only an existing saved Street context. Never certifies, chooses or repairs a site.
public static class MervFollowupStreetEntry
{
    public sealed record Plan(JsonElement Raw,MissionNpcInteriorProof.Door Floor,MissionNpcInteriorProof.Door Lobby);
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static void Need(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    static double[] V(JsonElement e,string name){var v=e.GetProperty(name).EnumerateArray().Select(x=>x.GetDouble()).ToArray();Need(v.Length==3&&v.All(double.IsFinite),"Street entry finite triple");return v;}
    static MissionNpcInteriorProof.Door Entry(JsonElement e,StaticWorldObject? expected)
    {
        string text=e.GetProperty("RawDoorJson").GetString()!;var raw=JsonSerializer.Deserialize<StaticWorldObject>(text,Fields)!;
        Need(raw!=null&&raw.metrId==1&&raw.type?.Length>=2&&new ushort[]{417,419}.Contains(MissionSites.Goid(raw))&&raw.mxoStaticId==((uint)raw.sectorID<<20|raw.staticId),"Street entry own world/key/GO");
        if(expected!=null)Need(JsonSerializer.Serialize(raw,Fields)==JsonSerializer.Serialize(expected,Fields),"Street entry selected raw identity");
        byte[] q=Convert.FromHexString(e.GetProperty("Quaternion").GetString()!);Need(q.Length==16&&Enumerable.Range(0,4).All(i=>float.IsFinite(BitConverter.ToSingle(q,i*4))),"Street entry quaternion");
        string digest=e.GetProperty("EvidenceDigest").GetString()!;Need(digest.Length==64&&digest.All(Uri.IsHexDigit),"Street entry evidence digest");
        _=e.GetProperty("Node").GetProperty("Region").GetUInt16();_=e.GetProperty("Node").GetProperty("Polygon").GetUInt16();
        return new(raw.mxoStaticId,MissionSites.Goid(raw),V(e,"Position"),Convert.ToHexString(q),V(e,"Standing"),text);
    }
    public static Plan? ForSite(MissionProgress.SavedState state)
    {
        var e=state.StreetAccess;if(e.ValueKind==JsonValueKind.Undefined)return null;
        Need(e.ValueKind==JsonValueKind.Object&&e.GetProperty("Version").GetInt32()==1&&e.GetProperty("Kind").GetString()=="StreetAccess"&&e.GetProperty("Enabled").GetBoolean(),"Street envelope identity");
        Need(state.Version==2&&Guid.TryParseExact(state.Identity,"N",out _)&&state.Started>0&&state.OpenFloorCurrentSiteIndex.HasValue&&state.OpenFloorSites!=null,"Street owned selected site");
        int i=state.OpenFloorCurrentSiteIndex!.Value;Need(i>=0&&i<state.OpenFloorSites!.Length&&i<state.SiteDoors.Length,"Street current index");var site=state.OpenFloorSites[i];var raw=state.SiteDoors[i];
        var all=e.GetProperty("Witnesses").EnumerateArray().ToArray();
        foreach(var w in all)Need((w.GetProperty("Version").GetInt32()==1||w.GetProperty("Version").GetInt32()==2)&&w.GetProperty("Mission").GetInt32()==state.Mission&&w.GetProperty("Identity").GetString()==state.Identity&&w.GetProperty("Started").GetInt64()==state.Started&&w.GetProperty("Phase").GetInt32()>=0&&w.GetProperty("Phase").GetInt32()<=state.Phase&&w.GetProperty("Refusal").ValueKind==JsonValueKind.Null,"Street exact owner/history");
        var matches=all.Where(w=>w.GetProperty("Site").GetProperty("Fingerprint").GetString()==site.Fingerprint).ToArray();
        if(matches.Length==0){Need(!all.Any(w=>w.GetProperty("Phase").GetInt32()==state.Phase),"Street current witness missing selected site");return null;}
        Need(matches.All(w=>w.GetProperty("Context").GetRawText()==matches[0].GetProperty("Context").GetRawText()),"Street conflicting context history");
        foreach(var selected in matches)MissionBound326RouteProof.SelectionVersion(selected);
        var current=matches.Where(w=>w.GetProperty("Phase").GetInt32()==state.Phase).ToArray();Need(current.Length<=1,"Street ambiguous current selection");
        var w0=current.Length==1?current[0]:matches[0];var savedSite=JsonSerializer.Deserialize<MissionOpenFloorSite>(w0.GetProperty("Site").GetRawText())!;
        Need(savedSite.District==1&&site.District==1&&savedSite.DoorKey==site.DoorKey&&savedSite.DoorX==site.DoorX&&savedSite.DoorY==site.DoorY&&savedSite.DoorZ==site.DoorZ,"Street saved site identity");
        var c=w0.GetProperty("Context");Need(c.GetProperty("Refusal").ValueKind==JsonValueKind.Null&&c.GetProperty("Kind").GetString()==w0.GetProperty("Kind").GetString(),"Street valid saved context");
        var floor=Entry(c.GetProperty("Floor"),raw);var lobby=Entry(c.GetProperty("Lobby"),null);
        Need(state.Waypoints.Length==1&&state.Exits.Length==1&&site.DoorKey==floor.Key&&BitConverter.ToUInt32(Convert.FromHexString(state.Waypoints[0].doorHex))==floor.Key&&new[]{state.Waypoints[0].x,state.Waypoints[0].y,state.Waypoints[0].z}.SequenceEqual(floor.Position)&&state.Exits[0].SequenceEqual(lobby.Position),"Street exact persisted floor/lobby");
        return new(w0.Clone(),floor,lobby);
    }
}
