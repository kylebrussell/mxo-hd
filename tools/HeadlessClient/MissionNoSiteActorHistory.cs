using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
namespace HeadlessClient;
// QA history continuity only. NextPhase clears current hostiles/waves, not the owned actor objects.
public static class MissionNoSiteActorHistory
{
    static void Need(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
    public static void RequireHistoricalContext(MissionProgress.SavedState state)
    {
        Need(state!=null&&state.Version==2&&state.Mission>0&&Guid.TryParseExact(state.Identity,"N",out _)&&state.Started>0&&state.Instance>0&&
            state.OpenFloorCurrentSiteIndex==-1&&state.Waypoints.Length==0&&state.Exits.Length==0&&state.Objects.Count==0,"No-site progress has a current owner and no current geometry");
        void History(JsonElement envelope,bool npc)
        {
            if(envelope.ValueKind==JsonValueKind.Undefined)return;
            Need(envelope.ValueKind==JsonValueKind.Object,"Historical envelope must retain its shape");
            Need(!envelope.TryGetProperty("Pending",out var pending)||pending.ValueKind==JsonValueKind.Null,"No pending current placement in no-site phase");
            if(npc)Need(!envelope.TryGetProperty("CurrentPhase",out var current)||current.ValueKind==JsonValueKind.Null,"No current NPC witness in a no-site phase");
            foreach(var witness in envelope.GetProperty("Witnesses").EnumerateArray())
            {
                var owner=npc?witness.GetProperty("Selection"):witness;
                Need(owner.GetProperty("Mission").GetInt32()==state.Mission&&owner.GetProperty("Identity").GetString()==state.Identity&&owner.GetProperty("Started").GetInt64()==state.Started&&
                    owner.GetProperty("Phase").GetInt32()>=0&&owner.GetProperty("Phase").GetInt32()<state.Phase,"No current/future/foreign witness may masquerade as historical geometry");
            }
        }
        History(state.NpcInterior,true);History(state.StreetAccess,false);
    }
    static Dictionary<string,MissionProgress.SavedMob> Actors(MissionProgress.SavedState state)
    {
        var result=new Dictionary<string,MissionProgress.SavedMob>(StringComparer.Ordinal);
        foreach(var actor in state.Mobs)
        {
            Need(actor!=null&&Guid.TryParseExact(actor.Identity,"N",out _)&&double.IsFinite(actor.X)&&double.IsFinite(actor.Y)&&double.IsFinite(actor.Z),"Historical actor identity/pose required");
            Need(result.TryAdd(actor.Identity,actor),"Duplicate historical actor identity refused");
        }
        return result;
    }
    static string Snapshot(MissionProgress.SavedMob actor,bool transition)
    {
        var value=JsonSerializer.SerializeToNode(actor)!.AsObject();
        if(transition){value.Remove(nameof(actor.Hostile));value.Remove(nameof(actor.Wave));}
        return value.ToJsonString();
    }
    static void Compare(MissionProgress.SavedState before,MissionProgress.SavedState after,bool transition)
    {
        var old=Actors(before);var current=Actors(after);Need(old.Keys.Order().SequenceEqual(current.Keys.Order()),"No actor may be added/removed/replaced by a no-site continuation");
        foreach(var pair in current)
        {
            var actor=pair.Value;Need(!actor.Hostile&&actor.Wave==null,"No-site actors cannot retain current hostiles/wave membership");
            Need(Snapshot(old[pair.Key],transition)==Snapshot(actor,transition),"Historical actor fields changed across no-site continuation");
        }
    }
    public static void RequireTransition(MissionProgress.SavedState before,MissionProgress.SavedState after)
    {
        RequireHistoricalContext(after);
        Need(before.Mission==after.Mission&&before.Identity==after.Identity&&before.Started==after.Started&&before.Instance==after.Instance&&after.Phase==before.Phase+1,"Historical actor handoff requires the exact committed next phase");
        Compare(before,after,true); // Only derived Hostile->false and Wave->null may clear at NextPhase.
    }
    public static void RequireStable(MissionProgress.SavedState baseline,MissionProgress.SavedState current)
    {
        RequireHistoricalContext(baseline);RequireHistoricalContext(current);
        Need(baseline.Mission==current.Mission&&baseline.Identity==current.Identity&&baseline.Started==current.Started&&baseline.Instance==current.Instance&&baseline.Phase==current.Phase,"Historical actor token owner/phase changed");
        Compare(baseline,current,false);
    }
}
