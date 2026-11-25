using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DisarmTrapsParityMode : NativeQaMode
{
    private const ushort DisarmTraps = UtilityAbilities.DisarmTraps; // 119
    private const uint TrapDetectToolGoid = 2500;
    private const ushort WallSafeGoid = 1416;
    private const byte Level = 10, Rank = 10;
    private const int X = 17000, Y = 495, Z = 6400;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, DisarmTraps);

    public override string Name => "ability-parity-disarm-traps";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=0,healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot IN (1,{CombatFixtureWeapons.HandSlot})",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{TrapDetectToolGoid},1,1,0)"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { DisarmTraps }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [DisarmTrapsQa.Mode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "119",
            ["NativeCloseableRows"] = 1,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost),
            ["RollControlAid"] = "?traproll <0..99|off>"
        },
        readyMessages: new[]
        {
            $"Level {Level}, rank {Rank} (AbilityLevelCap 10), {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Bag slot 1 = TrapDetectToolBasic (GOID 2500). Page 2: 1=Disarm Traps (119), 2=Withdraw. Verified outdoor HOME ({X},{Y},{Z}) faces Melee Dummy 3 m north. No initial tp needed.",
            "Step 1 (Undetected trap — always Critical regardless of roll): Run '?trapobj 1416 hidden', '?traproll 99', '?trapobj disarm'. Expect 2.0 s cast bar, 10 IS paid, 'Trap went off while being disarmed!' (0x76000250), '[UTILITY] Disarm Traps on 1416 in 0x839: Critical (detected=False, roll=99)', and state 0x839 -> 0x83b (TriggeredState).",
            "Step 2 (Detected trap — Critical roll): Run '?trapobj 1416', '?traproll 10' (delta 10+10-50=-30 <= -25), '?trapobj disarm'. Expect 2.0 s cast, 10 IS, 0x76000250, Critical, state 0x839 -> 0x83b.",
            "Step 3 (Detected trap — Retryable Failure then Disarmed on SAME safe): Run '?trapobj 1416', '?traproll 30' (delta -10), '?trapobj disarm' -> 'Trap was not disarmed successfully!' (0x7600024f), Failed, stays 0x839 (TrappedState). Now run '?traproll 50' (delta +10 > 0), '?trapobj disarm' -> 'Trap was successfully disarmed!' (0x7600024e), Disarmed, state 0x839 -> 0x83a (DisarmedState).",
            "Step 4 (Already disarmed silent NotTrapped + in-combat refusal): Run '?trapobj disarm' again on the 0x83a safe -> NotTrapped (no disarm chat line, state stays 0x83a). Finally enter Close Combat on Melee Dummy and try slot 1 (119) -> local gate or MustBeOutsideCombat (22) without debit."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-10 loadout fits memory");

        // 1. Undetected trap (DetectTrapFX unset) -> Critical even at roll 99 -> 0x76000250 & TriggeredState (0x83b).
        await SpawnTrapAsync("?trapobj 1416 hidden");
        await ChatAsync("?traproll 99", "Disarm Traps QA roll set to 99.");
        await DisarmAndExpectAsync("Critical (detected=False, roll=99)", UtilityAbilities.TrapDisarmCritical);

        // 2. Detected trap + critical roll (10 -> delta -30 <= -25) -> Critical -> 0x76000250 & TriggeredState (0x83b).
        await SpawnTrapAsync("?trapobj 1416");
        await ChatAsync("?traproll 10", "Disarm Traps QA roll set to 10.");
        await DisarmAndExpectAsync("Critical (detected=True, roll=10)", UtilityAbilities.TrapDisarmCritical);

        // 3. Detected trap + failure roll (30 -> delta -10) -> Failed -> 0x7600024f, stays 0x839, then roll 50 -> Disarmed -> 0x7600024e & 0x83a.
        await SpawnTrapAsync("?trapobj 1416");
        await ChatAsync("?traproll 30", "Disarm Traps QA roll set to 30.");
        await DisarmAndExpectAsync("Failed (detected=True, roll=30)", UtilityAbilities.TrapDisarmFailure);

        await ChatAsync("?traproll 50", "Disarm Traps QA roll set to 50.");
        await DisarmAndExpectAsync("Disarmed (detected=True, roll=50)", UtilityAbilities.TrapDisarmed);

        // 4. Already disarmed (0x83a) -> NotTrapped silent no-op.
        int logNotTrapped = server.DebugLog().Length;
        int rpcNotTrapped = actor.World.RpcSnapshot().Count;
        actor.SendChat("?trapobj disarm");
        await actor.WaitUntilAsync(() => server.DebugLog()[logNotTrapped..].Contains("in 0x83a: NotTrapped (detected=True, roll=50)", StringComparison.Ordinal),
            "Already-disarmed safe returns NotTrapped", TimeSpan.FromSeconds(8));
        byte[] disarmedMsg = AbilityMessages.AboutAbility(UtilityAbilities.TrapDisarmed, true, DisarmTraps, actor.Handle, "");
        Check.That(!actor.World.RpcSnapshot().Skip(rpcNotTrapped).Any(r => r.Bytes.AsSpan().SequenceEqual(disarmedMsg)),
            "NotTrapped emits no disarm message");

        await ChatAsync("?traproll off", "Disarm Traps QA roll override cleared.");
        Check.Equal(0, actor.World.Problems.Count, "Clean stock framing for 119 Disarm Traps");
        Console.WriteLine("DISARM TRAPS PASS: hidden-trap Critical, detected Critical/Failed/Disarmed, and NotTrapped all verified.");
        return Array.Empty<byte>();

        async Task SpawnTrapAsync(string command)
        {
            int count = actor.World.CreationSnapshot().Count;
            actor.SendChat(command);
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Count > count,
                $"Trapped Wall Safe spawns ({command})", TimeSpan.FromSeconds(5));
        }

        async Task ChatAsync(string command, string expect)
        {
            actor.SkipReceived();
            actor.SendChat(command);
            await actor.WaitForTextAsync(expect);
        }

        async Task DisarmAndExpectAsync(string logFragment, uint stringId)
        {
            int logFrom = server.DebugLog().Length;
            int rpcFrom = actor.World.RpcSnapshot().Count;
            actor.SendChat("?trapobj disarm");
            await EvidenceRetestWire.CastBarAsync(actor, DisarmTraps, 2);
            await actor.WaitUntilAsync(() =>
                server.DebugLog()[logFrom..].Contains($"[UTILITY] Disarm Traps on {WallSafeGoid} in 0x839: {logFragment}", StringComparison.Ordinal) &&
                server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=119 paid=10", StringComparison.Ordinal),
                $"Disarm Traps resolves with {logFragment}", TimeSpan.FromSeconds(8));
            byte[] expectedLine = AbilityMessages.AboutAbility(stringId, true, DisarmTraps, actor.Handle, "");
            await StockAbilityProfile.ObserveAsync(actor, rpcFrom, r => r.Bytes.AsSpan().SequenceEqual(expectedLine),
                $"Disarm Traps sends 0x{stringId:x}");
        }
    }
}
