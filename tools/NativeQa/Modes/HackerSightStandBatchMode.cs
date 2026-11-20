using System.Buffers.Binary;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class HackerSightStandBatchMode : NativeQaMode
{
    private const ushort HackerStyle = 1006;
    private static readonly ushort[] Loadout = Profile(2, 17, HackerStyle, 354, 476, 66).Order().ToArray();
    private static readonly (ushort Ability, float Reuse, string Capture)[] Samples =
    {
        // PR449 (09f69e027ea5) stock 354 at 1318 and selected 476 at 1702. PR443 (1ea85443ffbd) 66 at 1430.
        (354, 120, "80b96201000000000000000000000000000000000000000000000000585cc40001000000086b0000000000"),
        (476, 120, "80b9dc010e000c0000000000000000000000000000000000000000000000004084d77741086b0000000000"),
        (66, 43, "80b94200040002000000000000000000000000000000000000000000585cc20001000000086b0000000000")
    };

    public override string Name => "ability-parity-hacker-sight-stands";
    public override NativeQaModePlan Plan { get; } = SightPlan(Loadout, Samples, closeableRows: 3, new[]
        {
            "Choose Hacker Style (page 2 slot 1). Run ?combatdepth foundations-sight-pick, which faces a nearby QA Sight Target on clear ground 2 m ahead. Select it there.",
            "Keep the selection and run ?combatdepth foundations-sight-blocked. The same NPC is now 6 m away across the gap. If teleport cleared the highlight, try Tab from the closer cast stand. Capture whether each press sends a request for that target.",
            "Press slots 2–4 once each. If sent, expect 'Sight blocked ability <id>' with no landed effects. A client-side refusal without a request also supplies stock sight evidence if the target stays selected. Prior stock direct effects are in PR449 and PR443."
        });

    internal static NativeQaModePlan SightPlan(ushort[] loadout, (ushort Ability, float Reuse, string Capture)[] samples,
        int closeableRows, string[] messages, ushort style = HackerStyle, bool forceDeflection = false) =>
        new(loadout, 21550, 16150, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=1095,healthC=5000,healthM=5000,innerStrC=500,innerStrM=500 WHERE charId=110"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: samples.Select(sample => sample.Ability).Prepend(style).ToArray(),
            environmentOverrides: new()
            {
                ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
                ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = forceDeflection ? "15000" : "32766",
                ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = forceDeflection ? "32766" : "0"
            },
            extraQaMetadata: new()
            {
                ["ScopedStockRows"] = string.Join(",", samples.Select(sample => sample.Ability)),
                ["NativeCloseableRows"] = closeableRows
            }, readyMessages: messages);

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) => CheckSightAsync(context, Loadout, Samples);

    internal static async Task<byte[]> CheckSightAsync(NativeQaBootstrapContext context, ushort[] loadout,
        (ushort Ability, float Reuse, string Capture)[] samples)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The sight samples fit one legal loadout");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        await StarterFixedDamageMode.SelectStyle(actor, self, HackerStyle, 172);
        actor.SendChat("?combatdepth foundations-sight-pick");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        var target = await actor.WaitForViewNamedAsync("QA Sight Target");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        foreach (var (ability, _, capture) in samples)
        {
            await StarterHackerControlTargetMode.RefusedAsync(actor, ability,
                StarterHackerControlTargetMode.WithHandle(capture, 0));
            await StarterHackerControlTargetMode.RefusedAsync(actor, ability,
                StarterHackerControlTargetMode.WithHandle(capture, handle ^ 0x10000));
        }
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        actor.SendChat("?combatdepth foundations-sight-blocked");
        await actor.WaitForTextAsync("QA Sight Target is 6 m west");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        foreach (var (ability, reuse, capture) in samples)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            byte[] request = Convert.FromHexString(capture);
            Check.Equal(43, request.Length, $"Full stock request for {ability}");
            BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), handle);
            actor.SendRpc(request);
            await EvidenceRetestWire.TimerAsync(actor, cursor, ability, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains($"[ABILITY] Sight blocked ability {ability} on view {target.ViewId}"),
                $"The selected handle stays actionable across the teleport and {ability} is blocked at landing", HeadlessPlayer.DefaultTimeout);
            string result = server.DebugLog()[logStart..];
            Check.That(!result.Contains($"[FREECAST-VIRAL] ability={ability}") && !result.Contains($"[EFFECTS] {ability} on QA Sight Target"),
                $"The obstructed {ability} reaches neither the contest nor its effects");
        }
        Check.Equal(0, actor.World.Problems.Count, "Sight stand protocol framing");
        return Array.Empty<byte>();
    }
}
