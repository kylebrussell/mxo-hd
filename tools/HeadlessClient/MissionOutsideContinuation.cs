using System.Text.Json;
using hds;
namespace HeadlessClient;
// QA continuity only; natural entered/departed/timer fields are never written or normalized.
public static class MissionOutsideContinuation
{
    public static void Require(MissionProgress.SavedState before,MissionProgress.SavedState after,bool preserveGoalsAndItems=true)
    {
        if(before==null||after==null||before.Mission<=0)throw new InvalidDataException("Owned current mission required");
        string Context(MissionProgress.SavedState s)=>JsonSerializer.Serialize(new{
            s.Version,s.Identity,s.Mission,s.Instance,s.Started,s.Phase,s.Objects,s.SiteDoors,s.OpenFloorSites,s.OpenFloorCurrentSiteIndex,
            s.Waypoints,s.Exits,s.ElevatorWitnesses,s.OpenFloorPhase,s.OpenFloorLegacyPhase,
            NpcInterior=s.NpcInterior.ValueKind==JsonValueKind.Undefined?null:s.NpcInterior.GetRawText(),
            StreetAccess=s.StreetAccess.ValueKind==JsonValueKind.Undefined?null:s.StreetAccess.GetRawText()
        },new JsonSerializerOptions{IncludeFields=true});
        if(Context(before)!=Context(after))throw new InvalidDataException("Outside continuation changed current mission/site/source/witness identity");
        string Earned(MissionProgress.SavedState s)=>JsonSerializer.Serialize(new{
            Done=s.Done.Order().ToArray(),EverDone=s.EverDone.Order().ToArray(),Items=s.Items.OrderBy(p=>p.Key).ToArray(),Following=s.Following.Order().ToArray()
        });
        if(preserveGoalsAndItems&&Earned(before)!=Earned(after))throw new InvalidDataException("Ordinary downride changed unfinished goals or earned items/followers before outside aid");
    }
}
