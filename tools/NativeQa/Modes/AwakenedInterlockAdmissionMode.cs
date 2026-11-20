using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedInterlockAdmissionMode : NativeQaMode
{
    // Native PR386 Power Shot, ev-sweep/p323-hb.txt Head Butt and ev-sweep/aw-s8.txt Cheap Shot and opening.
    private const string StockOpen = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
    private static readonly (ushort Ability, string Capture, int Reuse, string AbilityName)[] Samples =
    {
        (14, "80b90e000400020000000000000000000000000000000000000000000e000000006b0000086b0000000000", 60, "PowerShotAbility"),
        (198, "80b9c600050003000000000000000000000000000000000000000000c6000000006b0000086b0000000000", 20, "CheapShotAbility"),
        (197, "80b9c500040002000000000000000000000000000000000000000000c5000000006b0000086b0000000000", 30, "HeadButtAbility")
    };

    public override string Name => "ability-parity-awakened-interlock-admission";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 14, 197, 198 }, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=350,innerStrM=1000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { 14, 198, 197 },
        testMobFixture: CombatFixtureLoadout.MeleeCsv, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.ResetMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "14,197,198", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Use Self Defense with Melee Dummy selected. At rest try page 2 slots 1–3 (Power Shot, Cheap Shot, Head Butt). Grey is expected. Any request rejected by the server should leave IS and reuse unchanged.",
            "Open Close Combat and wait for the interlock HUD. Press slot 1 on its current opponent. Expect a queued move and 60-second reuse. On a hit, record the actual Inner Strength gain.",
            "Use ?combatdepth fixture-reset between moves. Strafe slightly to uncover the fresh dummy, reselect, and open ordinary Close Combat again. Press slot 2 for Cheap Shot (20-second reuse). For Head Butt, open again after reset, select the active dummy and verify slot 3 is unavailable before Daze. Run ?combatdepth daze 60 with the active target selected and press slot 3 (30-second reuse). Enter submits the command. Keep the target selected without Escape. On a landed Head Butt, expect Self Defense move 284."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, target);
        foreach (var sample in Samples)
            await StarterHackerControlTargetMode.RefusedAsync(actor, sample.Ability,
                StarterHackerControlTargetMode.WithHandle(sample.Capture, Handle(target)),
                sample.Ability == 14 ? AbilityRefusals.LegacyAmbiguous : AbilityRefusals.InvalidTarget);
        foreach (var (ability, capture, reuse, abilityName) in Samples)
        {
            uint handle = Handle(target);
            string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            actor.SkipReceived();
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockOpen, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
            await actor.WaitForRpcAsync($"40 {hex} 00 6b");
            await actor.WaitForRpcAsync("47");
            actor.SendRpc($"81 51 {hex}");
            if (ability == 197)
            {
                var noDaze = await StarterHackerControlTargetMode.RefusedAsync(actor, ability,
                    StarterHackerControlTargetMode.WithHandle(capture, handle), AbilityRefusals.TargetStateRequired);
                Check.That(AbilityRefusals.TryDecode(noDaze.Bytes, out var state) && state.StatesNotMet == 2,
                    "Head Butt asks for the shipped target state 2 without charging");
                actor.SendChat("?combatdepth daze 60");
                await actor.WaitForTextAsync("Daze fixture applied 884/199 to Melee Dummy for 60 seconds");
            }
            await StarterHackerControlTargetMode.RefusedAsync(actor, ability,
                StarterHackerControlTargetMode.WithHandle(capture, handle ^ 0x10000),
                ability == 14 ? AbilityRefusals.LegacyAmbiguous : AbilityRefusals.InvalidTarget);
            int cursor = actor.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(capture, handle));
            await EvidenceRetestWire.TimerAsync(actor, cursor, ability, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains($"[COMBAT] {abilityName} is the next move"),
                $"{ability} queues only on the live interlock opponent", HeadlessPlayer.DefaultTimeout);
            string queueLine = $"[COMBAT] {abilityName} is the next move";
            await actor.WaitUntilAsync(() =>
            {
                string log = server.DebugLog()[logStart..];
                int queued = log.IndexOf(queueLine, StringComparison.Ordinal);
                return queued >= 0 && log[queued..].Contains("[COMBAT] Round ", StringComparison.Ordinal);
            }, $"An ordinary round resolves the queued {ability} before reset", HeadlessPlayer.DefaultTimeout);
            int previousViewCount = actor.World.CreationSnapshot().Count;
            actor.SendChat("?combatdepth fixture-reset");
            await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(previousViewCount)
                .Any(view => view.Name == CombatFixtureLoadout.DummyName), "A fresh dummy is visible after reset", HeadlessPlayer.DefaultTimeout);
            var fresh = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
            Check.That(fresh.ViewId != target.ViewId && fresh.SpawnId != target.SpawnId, "Reset retires the prior opponent");
            target = fresh;
        }
        Check.Equal(0, actor.World.Problems.Count, "Awakened interlock framing");
        return Array.Empty<byte>();
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
