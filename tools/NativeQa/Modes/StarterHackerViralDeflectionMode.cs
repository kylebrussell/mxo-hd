using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerViralSightMode;

namespace NativeQa.Modes;

public sealed class StarterHackerViralDeflectionMode : NativeQaMode
{
    private const string Target = "QA Sight Target";

    public override string Name => "ability-parity-starter-hacker-viral-deflection";
    public override NativeQaModePlan Plan { get; } = HackerSightStandBatchMode.SightPlan(
        Loadout, Samples, closeableRows: 0, new[]
        {
            "Disposable QA forces the viral contest draw to a fixed deflecting pair for the owned test character. Ordinary PvP and NPC combat do not use these draws.",
            "Select Hacker Style on page 2 slot 1, use ?combatdepth foundations-sight-pick, and select QA Sight Target on clear ground two meters away. Stay there.",
            "Use slots 2–5 in order, waiting for each cast to finish and the shared timer to clear. Each successful cast attempt shows the target deflecting it. Record the deflection message/FX, ordinary IS and own reuse, and no landed damage or splash."
        }, forceDeflection: true);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var target = await SightStandCastProof.PickAsync(actor, HackerStyle, StockStyle);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        var proof = new List<string>();
        foreach (var (ability, reuse, capture) in Samples)
        {
            int rpcCursor = actor.World.RpcSnapshot().Count;
            int viewCursor = actor.World.ViewSnapshot().Count;
            int logCursor = server.DebugLog().Length;
            string id = StockAbilityProfile.LittleEndianHex(ability);
            var line = BytePattern.Parse($"81 67 ?? ?? ?? ?? f2 00 00 24 01 {id} 00 00");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(capture, handle));
            var report = await StockAbilityProfile.ObserveAsync(actor, rpcCursor, rpc => line.IsMatch(rpc.Bytes), $"{ability} deflect notice");
            await EvidenceRetestWire.TimerAsync(actor, rpcCursor, ability, AbilityCooldowns.OwnTimer, reuse);
            var fx = BytePattern.Parse("9c 05 00 28");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewCursor).Any(fx.Occurs),
                $"Deflection FX reaches the client for {ability}", HeadlessPlayer.DefaultTimeout);
            string contest = $"[FREECAST-VIRAL] ability={ability} source={actor.Handle} target={Target} ";
            await actor.WaitUntilAsync(() => server.DebugLog()[logCursor..].Split('\n').Any(
                line => line.StartsWith(contest, StringComparison.Ordinal) && line.Contains("hit=False qa=True")),
                $"The real contest rejected {ability}", HeadlessPlayer.DefaultTimeout);
            string log = server.DebugLog()[logCursor..];
            Check.That(!log.Contains($"[ABILITY] {ability} lands") && !log.Contains($"[EFFECTS] {ability} on"),
                $"Deflected {ability} cannot land damage, apply effects or emit secondary splash");
            proof.Add(report.Hex);
            byte[] groupReady = AbilityCooldowns.BuildTimer(682, AbilityCooldowns.GroupTimer, reuse, false);
            await StockAbilityProfile.ObserveAsync(actor, rpcCursor, rpc => rpc.Bytes.AsSpan().SequenceEqual(groupReady),
                $"Shared viral reuse clears before the next sample ({ability})");
        }
        Check.Equal(0, actor.World.Problems.Count, "Starter viral deflection framing");
        await File.WriteAllLinesAsync(Path.Combine(context.OutputDirectory, "starter-viral-deflect.hex"), proof);
        return Array.Empty<byte>();
    }
}
