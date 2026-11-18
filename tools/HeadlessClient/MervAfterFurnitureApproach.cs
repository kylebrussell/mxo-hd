using System.Text.Json;
using hds;
namespace HeadlessClient;
// V3 arrival authority is distinct from the legacy GO326 sampler. No Project/source selection occurs here.
public static class MervAfterFurnitureApproach
{
    public sealed record Arrival(MissionBoundFurnitureRouteProof.Plan Plan,double[] Root,ushort Sector,ushort Region,double FloorY,long Life)
    { internal OwnViewLifeSnapshot? Token {get;init;} }
    public static bool NeedsArrival(MissionProgress.SavedState state)
    {
        if(state.StreetAccess.ValueKind==JsonValueKind.Undefined)return false;
        var matches=state.StreetAccess.GetProperty("Witnesses").EnumerateArray().Where(w=>w.GetProperty("Phase").GetInt32()==state.Phase).ToArray();
        if(matches.Length>1)throw new InvalidDataException("Duplicate current street selection");
        return matches.Length==1&&matches[0].GetProperty("Version").GetInt32()==3;
    }
    public static void RequireRoot(MissionBoundFurnitureRouteProof.Plan plan,double[] root,ushort sector,ushort region,double floor)
        =>MissionBoundFurnitureRouteProof.RequireArrival(plan,root,sector,region,floor);
    public static Arrival Capture(string repo,MissionProgress.SavedState state,double[] root,NavLocation nav,OwnViewLifeSnapshot life)
    {
        if(!NeedsArrival(state)||!life.Current||!life.OwnViewReady)throw new InvalidDataException("No current V3 arrival life");
        var row=state.StreetAccess.GetProperty("Witnesses").EnumerateArray().Single(w=>w.GetProperty("Phase").GetInt32()==state.Phase);
        int id=row.GetProperty("FurnitureRoutes").GetProperty("Routes")[0].GetProperty("ObjectId").GetInt32();
        var plan=MissionBoundFurnitureRouteProof.Read(repo,state,id)??throw new InvalidDataException("Missing V3 fixed route");
        RequireRoot(plan,root,nav.sector,nav.region,nav.floorY);
        return new(plan,root.ToArray(),nav.sector,nav.region,nav.floorY,life.Life){Token=life};
    }
    public static void Require(MissionBoundFurnitureRouteProof.Plan plan,MissionProgress.SavedState state,Arrival arrival,OwnViewLifeSnapshot life)
    {
        MissionBoundFurnitureRouteProof.RequireCurrent(arrival.Plan,state);MissionBoundFurnitureRouteProof.RequireCurrent(plan,state);
        if(arrival.Token==null||!arrival.Token.Current||!life.Current||!life.OwnViewReady||!ReferenceEquals(arrival.Token.Owner,life.Owner)||arrival.Life!=life.Life)
            throw new InvalidDataException("V3 route lost original ordinary-arrival owner/life");
        RequireRoot(plan,arrival.Root,arrival.Sector,arrival.Region,arrival.FloorY);
    }
}
