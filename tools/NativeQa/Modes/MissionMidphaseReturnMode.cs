using HeadlessClient;
namespace NativeQa.Modes;
public sealed class MissionMidphaseReturnMode:NativeQaMode
{
    public override string Name=>MissionMidphaseReturnFixture.Name;
    public override NativeQaModePlan Plan {get;}=new(MissionMidphaseReturnFixture.Abilities,3700,13100,
        Array.Empty<string>(),new[]{MissionMidphaseReturnFixture.NativeSql()},Array.Empty<string>(),
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(MissionMidphaseReturnFixture.Environment),
        extraQaMetadata:new(){["CapturedCheckpointReplay"]=true,["CheckpointMission"]=101208,["CheckpointPhase"]=0,["CheckpointRawBatchSha256"]=MissionMidphaseReturnFixture.RawSha,["CheckpointNormalizedSha256"]=MissionMidphaseReturnFixture.NormalizedSha,["NewEarnedChain"]=false,["NativeStartsOnCapturedFloor"]=true},
        readyMessages:new[]{"CAPTURED native101208 phase0 replay, not a newly earned chain. Done1,3,2; Upload4OPEN; ownedCD6831 bag6 rawcount0. NativeCSR110 starts3700/1295/13100. Walk normally to exact floor door270532686 and Open: must ride DOWN to3700/95/13100 without completing Upload. Normal Quit; reconnect SAME fixture/character with CD, no reprepare. Walk/open lobby270532869: must ride UP; Open floor again: DOWN. No ?tp/?missionfloor or forced flags. Optional ordinary elevator panelFloor2/495door must merelyOpen, not hijackUp. Retain actualdialogs/011c/ACK, CD, unchangedphase/no payout. See docs/MISSION-MIDPHASE-RETURN.md."});
}
