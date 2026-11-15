namespace NativeQa.Modes;
public sealed class MissionLeaveTransitionMode : NativeQaMode
{
    public override string Name=>"mission-leave-transition";
    public override NativeQaModePlan Plan {get;}=CreatePlan();
    private static NativeQaModePlan CreatePlan()
    {
        var p=new MissionOpenFloorBoxMode().Plan;
        return new(p.Abilities.ToArray(),p.X,p.Z,p.BeforeLoadout.ToArray(),p.AfterLoadout.ToArray(),p.AfterHelpers.ToArray(),
            DevCommands:p.DevCommands,GmCharacter110:p.GmCharacter110,FixedCallbackRolls:p.FixedCallbackRolls,
            environmentOverrides:p.EnvironmentOverrides.ToDictionary(x=>x.Key,x=>x.Value),
            extraQaMetadata:new(){["MissionId"]=201202,["DepartureTransitionRequired"]=true},
            readyMessages:new[]{"CSR110 only. Agent Gray > Critical > Poster Hunt201202, ordinary elevator and published Box routes. After the final ordinary Open, remain motionless inside: the area-leave line must appear and phase0 must remain active. Relog at that standing point: still waiting, no duplicate goals/reward. Open the actual elevator door, take the normal ride to the matching street lobby: only then may phase1 begin. Repeat after abort/reaccept. Separately test ordinary panel floor selection8140 to the matching lobby; same floor/intermediate floor must not finish. No QA teleport is departure proof. See docs/MISSION-LEAVE-TRANSITION.md. This is authored behavior correction; universal retail boundary remains unproven."});
    }
}
