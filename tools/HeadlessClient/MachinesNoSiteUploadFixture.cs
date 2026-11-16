using System.Text.Json;
using hds;
namespace HeadlessClient;
public static class MachinesNoSiteUploadFixture
{
    public static bool IsNoSiteUpload(Mission mission,int index)
    {
        if(index<0||index>=mission.phases.Count)return false;var p=mission.phases[index];
        return p.site==null&&p.objects.Count==0&&p.enemies==0&&p.waves.Count==0&&
            !mission.npcs.Any(n=>n.at==0||n.at==index+1)&&p.missionObjectives.Count>0&&p.missionObjectives.All(o=>o.type==MissionGoalType.Upload);
    }
    public static void RequireState(MissionProgress.SavedState state,Mission authored)
    {
        if(state.Mission!=authored.id||!IsNoSiteUpload(authored,state.Phase)||state.Waypoints.Length!=0||state.Exits.Length!=0||state.Objects.Count!=0)
            throw new InvalidDataException("Only an authored no-site/zero-geometry/all-Upload current phase can omit entry");
    }
    public static void RequireTransition(MissionProgress.SavedState before,MissionProgress.SavedState after,Mission authored)
    {
        RequireState(after,authored);
        if(before.Mission!=after.Mission||before.Identity!=after.Identity||before.Instance!=after.Instance||before.Started!=after.Started||after.Phase!=before.Phase+1)
            throw new InvalidDataException("No-site Upload must follow the exact committed previous-phase exit");
        var fields=new JsonSerializerOptions{IncludeFields=true};
        string Retained(MissionProgress.SavedState s)=>JsonSerializer.Serialize(new{Items=s.Items.OrderBy(p=>p.Key).ToArray(),Following=s.Following.Order().ToArray(),s.SiteDoors,s.OpenFloorSites},fields);
        if(Retained(before)!=Retained(after))throw new InvalidDataException("Completed exit changed earned items/followers or raw site history");
    }
}
