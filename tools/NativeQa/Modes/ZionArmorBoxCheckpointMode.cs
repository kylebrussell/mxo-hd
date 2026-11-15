using HeadlessClient;
namespace NativeQa.Modes;

public sealed class ZionArmorBoxCheckpointMode:NativeQaMode
{
    public override string Name=>ZionArmorBoxCheckpoint.Mode;
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},28500,14400,Array.Empty<string>(),new[]{ZionArmorBoxCheckpoint.SeedSql()},Array.Empty<string>(),
        Bootstrap:"earned-zion-armor-restore-and-ordinary-ride",environmentOverrides:new(){["MXO_PACKET_LOG"]="1",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"},
        extraQaMetadata:new(){["EarnedCheckpointFixture"]=true,["CheckpointMission"]=101216,["CheckpointPhase"]=3,["CheckpointPayloadSha256"]=ZionArmorBoxCheckpoint.PayloadSha256,["NativePlacementProven"]=false,["NativeDoorEntryProven"]=false,["NativeStartsOnCapturedFloor"]=true},
        readyMessages:new[]{"EARNED checkpoint, not a new chain proof. The hook normally rides from the seeded lobby into101216phase3 Armor/Box, then logs out on that floor and waits33s. NativeCSR110 therefore jacks into the captured floor, NOT the initial lobby. This is NOT native door-entry proof. Walk and inspect the Box BEFORE any Open; retain original Armor/NPC/body poses. Authored actions and ordinary exit are available only when mission goals allow; no synthetic waitingForExit. No forced source, payload edit, healing or unaided survival claim. If source/geometry/actors differ, STOP/HOLD. Supply reviewed MXO_ZION_ARMOR_SOURCE_SHA256 before normal NativeQa startup as documented. See docs/MISSION-ZION-ARMOR-BOX-CHECKPOINT.md."});
    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)=>ZionArmorBoxCheckpointHook.RunAsync(context);
}
