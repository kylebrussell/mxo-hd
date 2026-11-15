using HeadlessClient;
namespace NativeQa.Modes;
public sealed class IngressLivenessMode:NativeQaMode
{
    public override string Name=>"world-ingress-liveness";
    public override NativeQaModePlan Plan {get;}=new(IngressLivenessFixture.Abilities,23828,13984,Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),
        DevCommands:true,environmentOverrides:new(){["MXO_INGRESS_LIVENESS_QA"]="1",["MXO_PACKET_LOG"]="1"},
        extraQaMetadata:new(){["IngressWorkerPauseSeconds"]=35,["IngressPauseOneUse"]=true,["IngressLatencyQualification"]="Explicit diagnostic delay, not a normal mission latency result"},
        readyMessages:new[]{"CSR110 at normal Mara. This mode deliberately pauses ONLY your packet worker35s, one use. First perform a normal UI action; enter ?ingresspause and wait for START35. Make ordinary small movements during the delay so the native client sends real activity. Do not relog/retry/teleport. Require DONE35, same connection and responsive normal UI afterward, no disconnect/reset. Capture actual ingressDelta/current-owner proof and unchanged mission/wallet/online state. This is an explicit35s diagnostic, not acceptable normal coldmission latency. See docs/WORLD-INGRESS-LIVENESS.md. Standard shutdown/reset only; Claude owns native client/lock."});
}
