using HeadlessClient;
namespace NativeQa.Modes;
public sealed class MissionElevatorNativePoseMode:NativeQaMode
{
    public override string Name=>"mission-elevator-native-pose";
    public override NativeQaModePlan Plan {get;}
    public MissionElevatorNativePoseMode()
    {
        var c=InteriorAdmissionFixture.ElevatorNativePose;var original=InteriorAdmissionPlans.For(c);
        Plan=new NativeQaModePlan(original.Abilities.ToArray(),original.X,original.Z,original.BeforeLoadout.ToArray(),original.AfterLoadout.ToArray(),original.AfterHelpers.ToArray(),
            DevCommands:original.DevCommands,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:original.EnvironmentOverrides.ToDictionary(x=>x.Key,x=>x.Value),
            extraQaMetadata:new(){["MissionId"]=201201,["EarlierReceiptsOnly"]=true,["SelectedMissionCompletionSeeded"]=false,["NativeDoorHoverRequired"]=true,["FullCompletionProven"]=false},
            readyMessages:new[]{"CSR110. Missions/Phone > Agent Gray > Critical Disturbing Trends. Ordinary offer/accept; earlier receipts only. Ordinary first elevator entry, Talk Ophiuchi, ordinary exit. Enter the operations post through its actual metal elevator Door/Open. Inspect current persisted raw site/source separately from ElevatorWitness and fixed Root/Route/Standing. Walk the published route and Take MapDisk6783 normally. Then find the actual metal Door/Open at the CURRENT measured exit waypoint and leave through the ordinary door. STOP/HOLD if no native hover/pick; ?missionfloor cannot establish this proof. Historical204 office-side diagnostic28500/1295/14300 is separately attributed and never a forced source/route. Require actual picked key, measured overlay, arrival, delivery phase, retained disk and zero payout. No Give/completion claim. --fixture-plan is read-only inspection, not reset. See docs/MISSION-ELEVATOR-NATIVE-POSE.md."});
    }
}
