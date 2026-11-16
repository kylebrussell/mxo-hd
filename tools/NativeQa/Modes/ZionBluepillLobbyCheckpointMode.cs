using HeadlessClient;
namespace NativeQa.Modes;
public sealed class ZionBluepillLobbyCheckpointMode:NativeQaMode
{
    public override string Name=>ZionBluepillLobbyCheckpoint.Mode;
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),DevCommands:true,
        Bootstrap:"captured-bluepill-public-lobby-guidance-diagnostic",environmentOverrides:new(){["MXO_PACKET_LOG"]="1",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"},
        extraQaMetadata:new(){["CapturedAcceptedState"]=true,["CheckpointMission"]=101210,["CheckpointPhase"]=1,["CheckpointPayloadSha256"]=ZionBluepillLobbyCheckpoint.PayloadSha256,["NativeStartsOutside"]=true,["CustomGameplayPreparation"]=true,["NativeCarRenderingProven"]=false,["NewChainProven"]=false},
        readyMessages:new[]{"CAPTURED native0940 accepted checkpoint, not a new earned chain. Supply MXO_ZION_BLUEPILL_LOBBY_SOURCE_SHA256 as documented. Custom hook uses disclosed NAV TP aids, ordinary public365 Open, exact current-lobby809e and ordinary public return/logout; no car invocation. Native CSR110 starts OUTSIDE: walk through ordinary public365, stand at -2600/95/-8300 and face WEST (-X) toward car921699406 native -2800/145/-8300. Inspect model/hover and lobby HUD first; STOP if absent. No native walking/render/car success claim. See docs/MISSION-ZION-BLUEPILL-LOBBY-CHECKPOINT.md."});
    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)=>ZionBluepillLobbyCheckpointHook.RunAsync(context);
}
