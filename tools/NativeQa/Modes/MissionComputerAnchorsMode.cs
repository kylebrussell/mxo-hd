using HeadlessClient;
namespace NativeQa.Modes;
public sealed class MissionComputerAnchorsMode : NativeQaMode
{
    public override string Name=>"mission-computer-anchors";
    public override NativeQaModePlan Plan {get;}=new(NeighbourhoodMissionFixture.Abilities,NeighbourhoodMissionFixture.X,NeighbourhoodMissionFixture.Z,
        Array.Empty<string>(),Array.Empty<string>(),new[]{NeighbourhoodMissionFixture.NativeActorSql},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="3072"},
        extraQaMetadata:new(){["NeighbourhoodMissionId"]=3072,["MissionSiteSeed"]=3072,["SourceComputerAnchors"]=true},
        readyMessages:new[]{"Default source computer anchors; NO pose-probe override. CSR110: The Chessman Use > Retrieval > accept; use ordinary mission elevator. Relog before Take, return to the same upright computer at91865/4300/12660 (approach91865/4295/12740), radial Take, verify Search then Leave area and reward. Repeat once, verify one payout per acceptance. Source spot/checkpoint/site unchanged; no direct completion. See docs/MISSION-COMPUTER-ANCHORS.md."});
}
