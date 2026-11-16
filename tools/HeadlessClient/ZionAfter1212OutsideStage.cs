using hds;
namespace HeadlessClient;
// Only an authored no-area, actor-free Upload stage may stay outdoors without a new car trip.
public static class ZionAfter1212OutsideStage
{
    public static bool Matches(Mission mission,int phase)
    {
        if(phase<0||phase>=mission.phases.Count)return false;
        var p=mission.phases[phase];
        return p.site==null&&p.objects.Count==0&&!p.Waves().Any(w=>w.count>0)&&
            !mission.npcs.Any(n=>n.at==0||n.at==phase+1)&&p.missionObjectives.Count>0&&
            p.missionObjectives.All(g=>g.type==MissionGoalType.Upload);
    }
    public static void Require(Mission mission,int phase,MissionProgress.SavedState s,MissionProgress.SavedState? historicalBaseline=null)
    {
        if(!Matches(mission,phase)||s.Version!=2||s.Mission!=mission.id||s.Phase!=phase||
            !Guid.TryParseExact(s.Identity,"N",out _)||s.Started<=0||s.OpenFloorCurrentSiteIndex!=-1||
            s.Objects.Count!=0||s.Waypoints.Length!=0||s.Exits.Length!=0)
            throw new InvalidDataException("Authored outdoor Upload stage must retain owned progress without a current generated area");
        MissionNoSiteActorHistory.RequireHistoricalContext(s);
        if(historicalBaseline!=null)MissionNoSiteActorHistory.RequireStable(historicalBaseline,s);
        else if(s.Mobs.Count!=0)throw new InvalidDataException("Historical actors need the actual prior-departure baseline");
    }
    // Invoked only after CarryRelogAsync has qualified normal Quit/reservation/new authentication.
    // Mint a fresh live receipt from unchanged durable authority; never mutate/reuse the old owner.
    public static MissionOutsideUploadProof.Token AfterVerifiedCarry(MissionOutsideUploadProof.Token before,MissionProgress.SavedState restored,OwnViewLifeSnapshot fresh)
    {
        if(!fresh.Current||!fresh.OwnViewReady||ReferenceEquals(before.Life.Owner,fresh.Owner))throw new InvalidDataException("Qualified carry must have a fresh authenticated own life");
        MissionOutsideContinuation.Require(before.Checkpoint,restored);
        MissionNoSiteActorHistory.RequireStable(before.Checkpoint,restored);
        return new(restored,fresh);
    }

}
