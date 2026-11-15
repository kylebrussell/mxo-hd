using HeadlessClient;
namespace NativeQa.Modes;

public sealed class ZionArmorBoxCheckpointMode:NativeQaMode
{
    public override string Name=>ZionArmorBoxCheckpoint.Mode;
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},28500,14400,Array.Empty<string>(),new[]{ZionArmorBoxCheckpoint.SeedSql()},Array.Empty<string>(),
        Bootstrap:"earned-zion-armor-restore-and-ordinary-ride",environmentOverrides:new(){["MXO_PACKET_LOG"]="1",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"},
        extraQaMetadata:new(){["EarnedCheckpointFixture"]=true,["CheckpointMission"]=101216,["CheckpointPhase"]=3,["CheckpointPayloadSha256"]=ZionArmorBoxCheckpoint.PayloadSha256,["NativePlacementProven"]=false},
        readyMessages:new[]{"EARNED checkpoint, not a new chain proof. CSR110 starts by the ordinary lobby for101216phase3 Armor/Box. Use the actual door/Ride, walk and inspect the Box BEFORE Open; retain the original Armor/NPC and body poses. No missionfloor teleport, forced source, edited payload, healing or unaided survival claim. If door/source/geometry or actors differ, STOP/HOLD. Source pin MXO_ZION_ARMOR_SOURCE_SHA256 must match the reviewed actual producer. The bootstrap uses the real CSR restore/Ride/arrival receipt then exits and releases its session; no client lock or hidden mission action. See docs/MISSION-ZION-ARMOR-BOX-CHECKPOINT.md."});
    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)=>ZionArmorBoxCheckpointHook.RunAsync(context);
}
