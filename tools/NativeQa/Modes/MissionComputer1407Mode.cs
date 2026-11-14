using HeadlessClient;
namespace NativeQa.Modes;
public sealed class MissionComputer1407Mode : NativeQaMode
{
    public override string Name=>"mission-computer-1407";
    public override NativeQaModePlan Plan {get;}=new(NeighbourhoodMissionFixture.Abilities,MissionComputer1407Fixture.X,MissionComputer1407Fixture.Z,
        Array.Empty<string>(),Array.Empty<string>(),new[]{MissionComputer1407Fixture.NativeActorSql},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="2",["MXO_MISSION_COMPUTER_1407_QA"]="1"},
        extraQaMetadata:new(){["ContactId"]=8,["MissionId"]=3082,["MissionSiteSeed"]=2,["ComputerSourceGoid"]=1407},
        readyMessages:new[]{"Digger is immediately ahead. CSR110: right-click Use > Retrieval > accept, then ordinary elevator to Office/Kedemoth. Shared open-floor placement is integrated; fresh qualification and native walking/picking/restoration remain pending. No baseline/hide/show command exists. Use the final headless proof’s persisted Centre/Standing coordinates and record the free Computer plus grounded standing witness, ordinary Take, disk and Leave area. Inspect the source monitor/tower/keyboard before acceptance, during the mission, after completion, same-world reaccept and abort: the source must remain visible throughout without relog. Then verify cold restoration separately. No source-engine redraw or recovery of already-hidden clients is claimed. See docs/MISSION-COMPUTER-1407.md."});
}
