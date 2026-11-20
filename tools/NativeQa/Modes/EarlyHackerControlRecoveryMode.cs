using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class EarlyHackerControlRecoveryMode : NativeQaMode
{
    private const ushort HackerStyle = 1006, Blind = 43, Pacify = 48, Stun = 315, Barrage = 57;
    // 6971d24474bd:5109/4933, 95744828036e:8858, 40515a0af6ef:4354.
    // The complete stock casts preserve all fields except the live target handle.
    internal const string BlindRequest = "80b92b00060004000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    internal const string PacifyRequest = "80b93000070005000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    internal const string StunRequest = "80b93b0110000f0000000000000000000000000000000000000000003200000000000000086b0000000000";
    private const string BarrageRequest = "80b93900040002000000000000000000000000000000000000000000585cba0001000000086b0000000000";

    public override string Name => "ability-parity-early-hacker-control-recovery";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, HackerStyle, Blind, Pacify, Stun, Barrage },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=3000,healthM=3000,innerStrC=1500,innerStrM=1500 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { HackerStyle, Blind, Pacify, Stun, Barrage },
        testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new()
        {
            [CombatFixtureWeapons.ResetMode] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "43,48,315", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Step A. Select the 30,000 HP Melee Dummy 3 m west, use Hacker Style (slot 1) and Logic Barrage (slot 5). When it interlocks, run ?combatdepth fixture-reset. Wait for the fresh dummy, then click it. Your position is 2.5 m behind its spawn.",
            "Step B. Use Disrupt Inputs (slot 2) for 16 seconds of Blind. Immediately use Freeze System (slot 3) for 6 seconds of Pacify plus a 12-second immunity timer. Use Logic Barrage before Pacify expires. Confirm the dummy retaliates on damage. The fixture fixes the viral hit roll.",
            "Step C. Use ?combatdepth fixture-reset again, reselect the new dummy, and use Stun (slot 4). Check 7.35 seconds of Stun (7-second base with Resist Contagion) and 14 seconds of immunity. Capture the ordinary target selection and successful casts after both resets."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor)
        {
            using var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
            await observer.EnterWorldAsync();
            CombatFixtureLoadout.AssertDummy(observer, await observer.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName));
            return Array.Empty<byte>();
        }

        var server = context.ActualServer ?? throw new InvalidOperationException("The Hacker control fixture requires a disposable server.");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var original = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, original);
        await StarterFixedDamageMode.SelectStyle(actor, self, HackerStyle, 172);
        Select(original);
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BarrageRequest, Handle(original)));
        await actor.WaitForRpcAsync("47", TimeSpan.FromSeconds(12));

        var first = await ResetAsync(original);
        await StarterHackerControlTargetMode.RefusedAsync(actor, Blind,
            StarterHackerControlTargetMode.WithHandle(BlindRequest, Handle(original)));
        Select(first);
        await LandAsync(BlindRequest, Blind, first, state: 90, seconds: 16);
        await LandAsync(PacifyRequest, Pacify, first, state: 85, seconds: 6, immunity: 12);

        var pacifyClock = Stopwatch.StartNew();
        int strikeAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BarrageRequest, Handle(first)));
        var fighting = BytePattern.Parse("47");
        await StockAbilityProfile.ObserveAsync(actor, strikeAt, frame => fighting.IsMatch(frame.Bytes), "Damage breaks Pacify and starts retaliation");
        Check.That(pacifyClock.Elapsed < TimeSpan.FromSeconds(6), "Retaliation starts before Pacify's natural expiry");

        var second = await ResetAsync(first);
        Select(second);
        await LandAsync(StunRequest, Stun, second, state: 102, seconds: 7.35f, immunity: 14);
        Check.Equal(0, actor.World.Problems.Count, "The reset and three control requests decode cleanly");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "early-hacker-control-recovery.json"),
            JsonSerializer.Serialize(new { Original = Handle(original), AfterFirstReset = Handle(first), AfterSecondReset = Handle(second) }));
        return Array.Empty<byte>();

        async Task<ViewCreation> ResetAsync(ViewCreation old)
        {
            int creationAt = actor.World.CreationSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            byte[] delete = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(old.ViewId)}");
            actor.SendChat("?combatdepth fixture-reset");
            await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
            await actor.WaitUntilAsync(() =>
                actor.World.CreationSnapshot().Skip(creationAt).Any(view => view.Name == CombatFixtureLoadout.DummyName),
                "A fresh stock dummy creation follows reset", HeadlessPlayer.DefaultTimeout);
            var current = actor.World.CreationSnapshot().Skip(creationAt).Single(view => view.Name == CombatFixtureLoadout.DummyName);
            Check.That(actor.World.ViewSnapshot().Skip(viewAt).Any(section => section.AsSpan().IndexOf(delete) >= 0),
                "Reset deletes the retired view before assigning the new one");
            Check.That(current.ViewId != old.ViewId && current.SpawnId != old.SpawnId, "Reset retires the old view and spawn ID");
            Check.That(current.Attributes["IsEnemy"][0] == 1 && current.Attributes["IsDead"][0] == 0 &&
                BitConverter.ToUInt16(current.Attributes["Health"]) == 30000,
                "Fresh creation presents a live hostile target at full health");
            return current;
        }

        async Task LandAsync(string request, ushort ability, ViewCreation target, ushort state, float seconds, int immunity = 0)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(request, Handle(target)));
            string prefix = Regex.Escape($"[EFFECTS] {ability} on {target.Name}:");
            var effect = new Regex(prefix + $@"[^\r\n]*\bState {state} 0 ([\d.]+) s");
            await actor.WaitUntilAsync(() =>
            {
                Match match = effect.Match(server.DebugLog()[log..]);
                return match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float elapsed) &&
                    Math.Abs(elapsed - seconds) < 0.005;
            }, $"The new dummy accepts {ability} and receives state {state} for {seconds} seconds", HeadlessPlayer.DefaultTimeout);
            if (immunity > 0)
            {
                byte[] record = AbilityMessages.AboutAbility(AbilityMessages.ImmunityCaster, true,
                    ability, actor.Handle, target.Name, immunity);
                await StockAbilityProfile.ObserveAsync(actor, cursor,
                    frame => frame.Bytes.AsSpan().SequenceEqual(record), $"{ability} gives {immunity} seconds of immunity");
            }
        }

        void Select(ViewCreation target) => actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(Handle(target)))}");
        static uint Handle(ViewCreation target) => DuelPackets.Handle(target.ViewId, target.SpawnId);
    }
}
