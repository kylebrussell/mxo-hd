using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class MartialConfuseMode : NativeQaMode
{
    private const ushort KungFu = CombatStyles.KungFu;                                             // 133
    private const ushort SuicidalButterfly = KungFuGrandmasterSpecials.SuicidalButterfly;          // 428
    private const ushort Aikido = CombatStyles.Aikido;                                             // 101
    private const ushort CounterThrow = KarateAikidoMasterSpecials.CounterThrow;                   // 273
    private const ushort AikidoSpinningClayPigeon = MartialArtsRemainingSpecials.AikidoSpinningClayPigeon; // 296
    private const ushort Probe = 198;                                                              // Cheap Shot
    private static readonly uint OffBalanceBit = CombatInterlock.ConditionBit(CombatTactic.Grab);  // 0x04

    private const int CasterX = 24751;                                                             // 2.52 m east of QAEnemy
    private const int StandY = CombatFixtureLoadout.GroundY;                                       // 495
    private const int StandZ = CombatFixtureLoadout.CasterZ;                                       // -1498
    private const int EnemyX = 24499, EnemyZ = StandZ;                                             // 2.52 m west

    private static readonly byte CasterYaw = MobAggro.YawTowards(CasterX, StandZ, EnemyX, EnemyZ);
    private static readonly byte EnemyYaw = MobAggro.YawTowards(EnemyX, EnemyZ, CasterX, StandZ);

    // Exact stock requests (80 b9) and tactic RPCs (42).
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockKungFu = "80b985000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockAikido = "80b965000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockSuicidalButterfly = "80b9ac01040002000000000000000000000000000000000000000000ac010000006b0000086b0000000000";
    private const string StockCounterThrow = "80b9110104000200000000000000000000000000000000000000000011010000006b0000086b0000000000";
    private const string StockSpinningClayPigeon = "80b9280104000200000000000000000000000000000000000000000028010000006b0000086b0000000000";
    private const string StockCheapShot = "80b9c600050003000000000000000000000000000000000000000000c6000000006b0000086b0000000000";
    private const string StockTacticPower = "42 01 00 00 00 00 00";
    private const string StockTacticSpeed = "42 00 00 00 00 00 00";

    private static readonly byte[] SelfFlagsHeader = { 0x02, 0x00, 0x02, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 };
    private static readonly byte[] OtherFlagsHeader = { 0x02, 0x80, 0x80, 0x80, 0x80, 0x04 };

    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(15);
    private static readonly ushort[] CasterProfile = StockAbilityProfile
        .Profile(2, 17, KungFu, SuicidalButterfly, Aikido, CounterThrow, AikidoSpinningClayPigeon)
        .Order().ToArray();
    private static readonly ushort[] RecipientProfile = StockAbilityProfile
        .Profile(2, 17, Probe)
        .Order().ToArray();

    public override string Name => "ability-parity-ma-confuse";
    public override NativeQaModePlan Plan { get; } = MakePlan();

    private static NativeQaModePlan MakePlan()
    {
        int mem = CasterProfile.Sum(AbilityMemory.Cost);
        Check.That(mem <= AbilityMemory.ForLevel(50), $"Caster profile fits level-50 memory ({mem}/100)");
        Check.That(RecipientProfile.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Recipient profile fits level-50 memory");

        string[] helpers = new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={CasterX},y={StandY},z={StandZ},rotation={CasterYaw},alignment=1,healthC=30000,healthM=30000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},y={StandY},z={EnemyZ},rotation={EnemyYaw},alignment=2,healthC=30000,healthM=30000,innerStrC=2000,innerStrM=2000 WHERE charId=6114",
            $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }.Concat(RecipientProfile.Select((id, i) => Insert(6114, i, id, true))).ToArray();

        return new(
            CasterProfile, CasterX, StandZ, Array.Empty<string>(), Array.Empty<string>(), helpers,
            DevCommands: true,
            loadAbilityAncestors: false,
            testMobFixture: "martial-confuse.csv",
            hotbarAbilities: new[] { KungFu, SuicidalButterfly, Aikido, CounterThrow, AikidoSpinningClayPigeon },
            environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0",
                ["MXO_MOB_WANDER"] = "0",
                ["MXO_PVP_SERVER"] = "1",
                ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                [MartialConfuseQa.Mode] = "1"
            },
            extraQaMetadata: new()
            {
                ["ScopedStockRows"] = "428,273,296",
                ["PlayerRecipient"] = "QAEnemy (6114)",
                ["LoadedMemory"] = mem,
                ["MemoryCapacity"] = AbilityMemory.ForLevel(50),
                ["FixedModifierChance"] = "428 (30% -> 100%), Grab Off-Balance proc (25% -> 100%)"
            },
            readyMessages: new[]
            {
                $"Level 50 rank 50 unarmed ({mem}/100 memory; Kung-Fu +41% melee, Aikido +40% melee). CSR spawns at ({CasterX},{StandY},{StandZ}) outside the hardline booth facing QAEnemy (6114, hostile) 2.52 m west. This fixture seeds CSR on Grab and QAEnemy on Power. Leave the tactic alone. Fixed chances: 428 Confuse and Off Balance on damaging CSR Grab rounds.",
                "Helper watches QAEnemy continuously in any order without session-fatal row timeouts. If any special misses, wait for its own reuse and retry the same slot. Wait for MARTIAL-CONFUSE NEXT (after QAEnemy's Cheap Shot 198 20 s recovery cooldown finishes) before casting the next row.",
                "Row 1 — Suicidal Butterfly (428, Confuse 4.0 s, fixed 100% proc, no target state): From spawn, select QAEnemy (2.52 m west) and enter ordinary Close Combat. Press slot 1 (133 Kung-Fu), then slot 2 (428 Suicidal Butterfly). Expect 35 IS, 10.0 s own / 5.0 s group 688 reuse, 35 dmg (base 25 + 41%), QAEnemy Confuse state 91 (4.0 s) + linked record, QAEnemy Cheap Shot (198) REFUSED while confused ('You cannot perform special moves while confused.'), both Confuse records removed at ~4.0 s + 'You are no longer confused.', then Cheap Shot (198) ACCEPTED after expiry. Wait for MARTIAL-CONFUSE PASS 428 and MARTIAL-CONFUSE NEXT.",
                "Row 2 — Counter Throw (273, Confuse 4.0 s, requires Off-Balance 0x04): Stay in Close Combat with QAEnemy. Press slot 3 (101 Aikido) and wait for slot 4 to light. Each damaging Grab round gives QAEnemy Off Balance for 10 s. Press slot 4 (273 Counter Throw). Expect 15 IS, 10.0 s own / 5.0 s group 687 reuse, 78 dmg (base 56 + 40%), Confuse and linked record, Cheap Shot (198) refused until expiry and accepted after it. Wait for MARTIAL-CONFUSE PASS 273 and MARTIAL-CONFUSE NEXT.",
                "Row 3 — Aikido Spinning Clay Pigeon (296, Confuse 8.0 s, requires Off-Balance 0x04): Stay in Aikido (101) with GRAB tactic active so Off-Balance (0x04) refreshes on QAEnemy (slot 5 lit). Press slot 5 (296 Aikido Spinning Clay Pigeon). Expect 40 IS, 40.0 s own reuse, 524 dmg (base 375 + 40%), QAEnemy Confuse state 91 (8.0 s) + linked record, Cheap Shot (198) REFUSED while confused, removed at ~8.0 s + 'You are no longer confused.', then Cheap Shot (198) ACCEPTED after expiry. Wait for MARTIAL-CONFUSE ALL PASS.",
                "Keep CSR PacketLog, DebugLog, and martial-confuse.jsonl for the three rows."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Martial Confuse fixture requires the owned server.");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add("QAEnemy", enemy);
        Check.Equal(6114u, enemy.CharId, "QAEnemy helper identity");
        var owned = AbilityMemoryUiFixture.Decode(enemy.CharacterData[6])
            .Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(RecipientProfile.All(owned.Contains), "QAEnemy owns Cheap Shot (198) and prerequisites");
        await PvpOpponentsFixture.EnterAsync(enemy);
        VerifyStand(enemy.World.CreationSnapshot().Last(v => v.ViewId == 2), EnemyX, EnemyZ, EnemyYaw);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);

        // Switch QAEnemy to Power tactic before combat (0 IS) so CSR's Grab beats it cleanly.
        int tacticRpc = enemy.World.RpcSnapshot().Count;
        enemy.SendRpc(StockTacticPower);
        await StockAbilityProfile.ObserveAsync(enemy, tacticRpc,
            f => ManageBonusRecord.TryDecode(f.Bytes, out var r) && r!.SourceId == 1017 && r.AddRemoveType == 0,
            "QAEnemy switches tactic to Power (1017)");

        if (context.StockProbe is not { } caster)
        {
            _ = WatchNativeAsync(context, server, enemy);
            return Array.Empty<byte>();
        }

        await PvpOpponentsFixture.EnterAsync(caster);
        VerifyStand(caster.World.CreationSnapshot().Last(v => v.ViewId == 2), CasterX, StandZ, CasterYaw);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var enemyView = await caster.WaitForViewNamedAsync("QAEnemy");
        PvpOpponentsFixture.Observe(enemyView, 2, 6114);
        var csrAtEnemy = await enemy.WaitForViewNamedAsync(caster.Handle);
        VerifyStand(csrAtEnemy, CasterX, StandZ, CasterYaw);

        uint enemyHandle = Handle(enemyView);
        uint csrHandle = Handle(csrAtEnemy);
        string enemyHex = WireFormat.ToHex(BitConverter.GetBytes(enemyHandle));

        await ObserveTacticAsync(0, 1016, "CSR has the fixture's Grab tactic on login");
        int speedRpc = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StockTacticSpeed);
        await ObserveTacticAsync(speedRpc, 1015, "CSR can select Speed for the negative gate check");

        // Open Close Combat with QAEnemy.
        int openRpc = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockSelfDefense, enemyHandle),
            WireFormat.FromHex($"40 {enemyHex} 01 6b"));
        await StockAbilityProfile.ObserveAsync(caster, openRpc, rpc => rpc.Bytes is [0x46, ..] &&
            rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1, "Real PvP interlock opens with QAEnemy");

        // Row 1: Suicidal Butterfly (428) — Kung-Fu, fixed 100% Confuse 4.0 s, no target state required.
        await SelectStyleAsync(caster, KungFu, StockKungFu, enemyHandle);
        caster.SendRpc($"81 51 {enemyHex}");
        await LandUntilObservedAsync(caster, enemy, StockSuicidalButterfly, enemyHandle, TimeSpan.FromSeconds(10.5),
            requireOffBalanceView: null,
            frame => StateRecord(frame, SuicidalButterfly, TempMods.ConfuseAbility, 4f, remove: false),
            async (rpcCursor, added) => await ObserveConfuseAsync(context, server, enemy, csrHandle, SuicidalButterfly, 4f, rpcCursor, added, waitForProbeReady: true));

        // Switch to Aikido (101) and verify Counter Throw (273) is refused before Off-Balance (CastRequiredStates = 4).
        await SelectStyleAsync(caster, Aikido, StockAikido, enemyHandle);
        caster.SendRpc($"81 51 {enemyHex}");
        int preGateRpc = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(StockCounterThrow, enemyHandle));
        await StockAbilityProfile.ObserveAsync(caster, preGateRpc,
            f => AbilityRefusals.TryDecode(f.Bytes, out var r) && r.AbilityID == CounterThrow &&
                 r.ErrorID == AbilityRefusals.TargetStateRequired && r.StatesNotMet == 4u,
            "273 refused with TargetStateRequired(4) before Off-Balance");

        // Check both inputs against settled PvP rounds, in addition to checking the tactic records.
        int beforePower = server.DebugLog().Length;
        int powerRpc = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StockTacticPower);
        await ObserveTacticAsync(powerRpc, 1017, "CSR switches to Power with stock RPC 42 in interlock");
        await ObserveRoundAsync(beforePower, CombatTactic.Power);
        int beforeGrab = server.DebugLog().Length;
        int grabRpc = caster.World.RpcSnapshot().Count;
        caster.SendChat("?tactic grab");
        await ObserveTacticAsync(grabRpc, 1016, "CSR switches to Grab with the dev chat command in interlock");
        await ObserveRoundAsync(beforeGrab, CombatTactic.Grab);

        // Row 2: Counter Throw (273) — Aikido + real Grab Off-Balance (0x04 visible on CSR's view of QAEnemy), Confuse 4.0 s.
        await LandUntilObservedAsync(caster, enemy, StockCounterThrow, enemyHandle, TimeSpan.FromSeconds(10.5),
            requireOffBalanceView: enemyView.ViewId,
            frame => StateRecord(frame, CounterThrow, TempMods.ConfuseAbility, 4f, remove: false),
            async (rpcCursor, added) => await ObserveConfuseAsync(context, server, enemy, csrHandle, CounterThrow, 4f, rpcCursor, added, waitForProbeReady: true));

        // Row 3: Aikido Spinning Clay Pigeon (296) — Aikido + real Grab Off-Balance (0x04), Confuse 8.0 s.
        await LandUntilObservedAsync(caster, enemy, StockSpinningClayPigeon, enemyHandle, TimeSpan.FromSeconds(40.5),
            requireOffBalanceView: enemyView.ViewId,
            frame => StateRecord(frame, AikidoSpinningClayPigeon, TempMods.ConfuseAbility, 8f, remove: false),
            async (rpcCursor, added) => await ObserveConfuseAsync(context, server, enemy, csrHandle, AikidoSpinningClayPigeon, 8f, rpcCursor, added, waitForProbeReady: false));

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count,
            "Martial Confuse replay packet framing");
        return Array.Empty<byte>();

        Task<RpcRecord> ObserveTacticAsync(int cursor, ushort tactic, string description) =>
            StockAbilityProfile.ObserveAsync(caster, cursor,
                f => ManageBonusRecord.TryDecode(f.Bytes, out var r) && r!.SourceId == tactic && r.AddRemoveType == 0,
                description);

        Task ObserveRoundAsync(int cursor, CombatTactic tactic) => caster.WaitUntilAsync(() =>
        {
            string log = server.DebugLog();
            return log.Length > cursor && log[cursor..].Split('\n').Any(line =>
                line.StartsWith("[PVP] Round ", StringComparison.Ordinal) &&
                line.Contains($"CSR_Tester {tactic} (", StringComparison.Ordinal));
        }, $"A resolved PvP round uses CSR's {tactic} tactic", Hold);
    }

    private static async Task WatchNativeAsync(NativeQaBootstrapContext context, ServerInstance server, HeadlessPlayer enemy)
    {
        string stop = Path.Combine(context.OutputDirectory, "stop");
        var passed = new HashSet<ushort>();
        var csr = await WaitForNativeAsync(() => enemy.World.CreationSnapshot().LastOrDefault(v => v.Name == "CSR_Tester"));
        if (csr == null) return;

        Console.WriteLine("MARTIAL-CONFUSE READY: waiting for 428 (Suicidal Butterfly), 273 (Counter Throw), and 296 (Aikido Spinning Clay Pigeon) in any order. Session will stay open for retries.");

        int rpcCursor = enemy.World.RpcSnapshot().Count;
        int viewCursor = enemy.World.ViewSnapshot().Count;
        bool lastOffBalance = false;

        while (Running())
        {
            var views = enemy.World.ViewSnapshot();
            while (viewCursor < views.Count)
            {
                if (TryReadSelfConditionFlags(views[viewCursor++], out uint flags))
                {
                    bool nowOffBalance = (flags & OffBalanceBit) != 0;
                    if (nowOffBalance != lastOffBalance)
                    {
                        lastOffBalance = nowOffBalance;
                        Console.WriteLine(nowOffBalance
                            ? "MARTIAL-CONFUSE OFF-BALANCE ON (0x04, 10 s) — slots 4 (273) & 5 (296) enabled!"
                            : "MARTIAL-CONFUSE OFF-BALANCE OFF");
                    }
                }
            }

            var rpcs = enemy.World.RpcSnapshot();
            if (rpcCursor < rpcs.Count)
            {
                int foundIndex = -1;
                ushort matched = 0;
                float expectedSeconds = 0f;
                RpcRecord? trigger = null;
                for (int i = rpcCursor; i < rpcs.Count; i++)
                {
                    var f = rpcs[i];
                    if (StateRecord(f, SuicidalButterfly, TempMods.ConfuseAbility, 4f, remove: false)) { foundIndex = i; matched = SuicidalButterfly; expectedSeconds = 4f; trigger = f; break; }
                    if (StateRecord(f, CounterThrow, TempMods.ConfuseAbility, 4f, remove: false)) { foundIndex = i; matched = CounterThrow; expectedSeconds = 4f; trigger = f; break; }
                    if (StateRecord(f, AikidoSpinningClayPigeon, TempMods.ConfuseAbility, 8f, remove: false)) { foundIndex = i; matched = AikidoSpinningClayPigeon; expectedSeconds = 8f; trigger = f; break; }
                }

                if (matched != 0 && trigger != null)
                {
                    int eventStart = Math.Max(0, foundIndex - 4);
                    rpcCursor = foundIndex + 1;
                    bool willCompleteAll = passed.Count + (passed.Contains(matched) ? 0 : 1) >= 3;
                    try
                    {
                        var latestCsr = enemy.World.CreationSnapshot().LastOrDefault(v => v.Name == "CSR_Tester") ?? csr;
                        await ObserveConfuseAsync(context, server, enemy, Handle(latestCsr), matched, expectedSeconds, eventStart, trigger,
                            waitForProbeReady: !willCompleteAll);
                        passed.Add(matched);
                        if (passed.Contains(SuicidalButterfly) && passed.Contains(CounterThrow) && passed.Contains(AikidoSpinningClayPigeon))
                            Console.WriteLine("MARTIAL-CONFUSE ALL PASS (428, 273, 296) — you may stop the session or retry any row.");
                    }
                    catch (Exception ex)
                    {
                        if (!Running()) return;
                        Console.WriteLine($"MARTIAL-CONFUSE RETRY {matched} ({ex.Message}) — session remains alive; retry after reuse.");
                    }
                    rpcCursor = enemy.World.RpcSnapshot().Count;
                    viewCursor = enemy.World.ViewSnapshot().Count;
                    continue;
                }
                rpcCursor = rpcs.Count;
            }
            await Task.Delay(25);
        }

        bool Running()
        {
            if (File.Exists(stop)) return false;
            try { return !server.HasExited; }
            catch (InvalidOperationException) { return false; }
        }

        async Task<T?> WaitForNativeAsync<T>(Func<T?> read) where T : class
        {
            long seen = -1;
            while (Running())
            {
                long v = enemy.World.Version;
                if (seen != v)
                {
                    seen = v;
                    if (read() is { } result) return result;
                }
                await Task.Delay(25);
            }
            return null;
        }
    }

    private static async Task ObserveConfuseAsync(NativeQaBootstrapContext context, ServerInstance server,
        HeadlessPlayer enemy, uint csrHandle, ushort abilityId, float expectedSeconds,
        int rpcFrom, RpcRecord stateAdd, bool waitForProbeReady)
    {
        long stateAt = Stopwatch.GetTimestamp();
        var linkedAdd = await StockAbilityProfile.ObserveAsync(enemy, rpcFrom,
            f => LinkedRecord(f, TempMods.ConfuseAbility, 0, 0, remove: false),
            $"QAEnemy {abilityId} Confuse linked add");

        // While Confused, attempt Cheap Shot (198): MUST be refused with "You cannot perform special moves while confused."
        int logBeforeRefuse = server.DebugLog().Length;
        int refuseFrom = enemy.World.RpcSnapshot().Count;
        enemy.SendRpc(StarterHackerControlTargetMode.WithHandle(StockCheapShot, csrHandle));
        var refusal = await StockAbilityProfile.ObserveAsync(enemy, refuseFrom,
            f => AbilityRefusals.TryDecode(f.Bytes, out var r) && r.AbilityID == Probe,
            $"Cheap Shot (198) refused while confused by {abilityId}");
        enemy.SendRpc("81 48");
        await enemy.WaitForRpcAsync("81 49", Hold);

        bool timerStartedWhileConfused = enemy.World.RpcSnapshot().Skip(refuseFrom)
            .Any(f => ProbeTimer(f, end: false));
        Check.That(!timerStartedWhileConfused, $"Cheap Shot (198) cooldown did not start while confused by {abilityId}");
        string refuseLog = server.DebugLog()[logBeforeRefuse..];
        Check.That(refuseLog.Contains("You cannot perform special moves while confused.", StringComparison.Ordinal),
            $"Server logged Confuse refusal for Cheap Shot (198) on {abilityId}");

        // Wait for matched Confuse state + linked removals and StateEnd ("You are no longer confused.") at ~expectedSeconds.
        int remFrom = refuseFrom;
        var waitTimeout = TimeSpan.FromSeconds(expectedSeconds + 6);
        var stateRem = await ObserveRpcAsync(enemy, remFrom,
            f => StateRecord(f, abilityId, TempMods.ConfuseAbility, expectedSeconds, remove: true) && SameLocator(stateAdd, f),
            $"QAEnemy {abilityId} Confuse state removal", waitTimeout);
        double elapsed = Stopwatch.GetElapsedTime(stateAt).TotalSeconds;
        var linkedRem = await ObserveRpcAsync(enemy, remFrom,
            f => LinkedRecord(f, TempMods.ConfuseAbility, 0, 0, remove: true) && SameLocator(linkedAdd, f),
            $"QAEnemy {abilityId} Confuse linked removal", waitTimeout);
        byte[] stateEndBytes = AbilityMessages.StateEnd(TempMods.ConfuseAbility, "QAEnemy");
        var stateEnd = await ObserveRpcAsync(enemy, remFrom,
            f => f.Bytes.AsSpan().SequenceEqual(stateEndBytes),
            $"QAEnemy {abilityId} Confuse StateEnd message", waitTimeout);
        Check.That(Math.Abs(elapsed - expectedSeconds) < 1.5,
            $"{abilityId} Confuse expiry at {elapsed:F2}s (expected ~{expectedSeconds:F1}s)");

        // Immediately after release, attempt Cheap Shot (198) again: MUST be accepted (20 s own cooldown timer starts).
        int recoverFrom = enemy.World.RpcSnapshot().Count;
        enemy.SendRpc(StarterHackerControlTargetMode.WithHandle(StockCheapShot, csrHandle));
        var probeStart = await StockAbilityProfile.ObserveAsync(enemy, recoverFrom,
            f => ProbeTimer(f, end: false),
            $"Cheap Shot (198) accepted after {abilityId} Confuse release");
        bool anyPostRefusal = enemy.World.RpcSnapshot().Skip(recoverFrom)
            .Any(f => AbilityRefusals.TryDecode(f.Bytes, out var r) && r.AbilityID == Probe);
        Check.That(!anyPostRefusal, $"Cheap Shot (198) not refused after {abilityId} Confuse release");

        AppendRecord(context, new
        {
            Ability = abilityId,
            Kind = "Confuse",
            ExpectedSeconds = expectedSeconds,
            StateAdd = stateAdd.Hex,
            LinkedAdd = linkedAdd.Hex,
            RefusedProbe = refusal.Hex,
            TimerStartedWhileConfused = timerStartedWhileConfused,
            StateRemove = stateRem.Hex,
            LinkedRemove = linkedRem.Hex,
            StateEnd = stateEnd.Hex,
            RecoveredProbeTimer = probeStart.Hex,
            ExpirySeconds = Math.Round(elapsed, 2)
        });
        Console.WriteLine($"MARTIAL-CONFUSE PASS {abilityId}: Confuse {expectedSeconds:F0}s landed; Cheap Shot 198 refused while confused; removed at {elapsed:F2}s; Cheap Shot 198 accepted after release.");

        if (waitForProbeReady)
        {
            Console.WriteLine($"MARTIAL-CONFUSE WAIT {abilityId}: waiting for QAEnemy Cheap Shot (198) 20 s reuse before next row...");
            await ObserveRpcAsync(enemy, recoverFrom, f => ProbeTimer(f, end: true),
                $"QAEnemy Cheap Shot (198) 20 s reuse end after {abilityId}", TimeSpan.FromSeconds(26));
            Console.WriteLine($"MARTIAL-CONFUSE NEXT after {abilityId}: Cheap Shot (198) ready for next row.");
        }
    }

    private static async Task LandUntilObservedAsync(HeadlessPlayer caster, HeadlessPlayer enemy,
        string stockRequest, uint enemyHandle, TimeSpan reuseWait, ushort? requireOffBalanceView,
        Func<RpcRecord, bool> matchesAdd, Func<int, RpcRecord, Task> observe)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (requireOffBalanceView is ushort targetView)
            {
                await WaitForOtherOffBalanceAsync(caster, targetView);
            }

            int enemyCursor = enemy.World.RpcSnapshot().Count;
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(stockRequest, enemyHandle));
            var sw = Stopwatch.StartNew();
            RpcRecord? hit = null;
            while (sw.Elapsed < TimeSpan.FromSeconds(6))
            {
                hit = enemy.World.RpcSnapshot().Skip(enemyCursor).FirstOrDefault(matchesAdd);
                if (hit != null) break;
                await Task.Delay(50);
            }
            if (hit != null)
            {
                await observe(enemyCursor, hit);
                return;
            }
            await Task.Delay(reuseWait);
        }
        throw new InvalidOperationException($"Did not observe expected Confuse state add after 8 attempts for {stockRequest[..12]}");
    }

    private static async Task WaitForOtherOffBalanceAsync(HeadlessPlayer caster, ushort enemyViewId)
    {
        bool HasActiveOffBalance()
        {
            var views = caster.World.ViewSnapshot();
            for (int i = views.Count - 1; i >= 0; i--)
            {
                if (TryReadOtherConditionFlags(views[i], enemyViewId, out uint flags))
                    return (flags & OffBalanceBit) != 0;
            }
            return false;
        }

        await caster.WaitUntilAsync(HasActiveOffBalance,
            "CSR observes QAEnemy OtherConditionStateFlags Off-Balance (0x04) via real Grab round",
            TimeSpan.FromSeconds(20));
    }

    private static async Task SelectStyleAsync(HeadlessPlayer caster, ushort style, string request, uint target)
    {
        int viewCursor = caster.World.ViewSnapshot().Count;
        string styleHex = StockAbilityProfile.LittleEndianHex(style);
        var activeStyle = BytePattern.Parse($"02 00 02 80 80 80 80 80 10 {styleHex} 00 00");
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(request, target));
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(viewCursor).Any(activeStyle.Occurs),
            $"active style update for {style}", Hold);
    }

    private static async Task<RpcRecord> ObserveRpcAsync(HeadlessPlayer actor, int cursor,
        Func<RpcRecord, bool> predicate, string description, TimeSpan timeout)
    {
        RpcRecord? found = null;
        await actor.WaitUntilAsync(() => (found = actor.World.RpcSnapshot().Skip(cursor).FirstOrDefault(predicate)) != null,
            description, timeout);
        return found!;
    }

    private static void AppendRecord(NativeQaBootstrapContext context, object record) =>
        File.AppendAllText(Path.Combine(context.OutputDirectory, "martial-confuse.jsonl"),
            JsonSerializer.Serialize(record) + Environment.NewLine);

    public static bool TryReadSelfConditionFlags(byte[] viewBytes, out uint flags)
    {
        flags = 0;
        int pos = -1, next = 0;
        while ((next = BytePattern.IndexOf(viewBytes, SelfFlagsHeader, next)) >= 0)
        {
            if (next + SelfFlagsHeader.Length + 4 <= viewBytes.Length) pos = next;
            next++;
        }
        if (pos < 0) return false;
        flags = BinaryPrimitives.ReadUInt32LittleEndian(viewBytes.AsSpan(pos + SelfFlagsHeader.Length, 4));
        return true;
    }

    public static bool TryReadOtherConditionFlags(byte[] viewBytes, ushort viewId, out uint flags)
    {
        flags = 0;
        byte[] needle = new byte[2 + OtherFlagsHeader.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(needle, viewId);
        OtherFlagsHeader.CopyTo(needle, 2);
        int pos = -1, next = 0;
        while ((next = BytePattern.IndexOf(viewBytes, needle, next)) >= 0)
        {
            if (next + needle.Length + 4 <= viewBytes.Length) pos = next;
            next++;
        }
        if (pos < 0) return false;
        flags = BinaryPrimitives.ReadUInt32LittleEndian(viewBytes.AsSpan(pos + needle.Length, 4));
        return true;
    }

    private static bool StateRecord(RpcRecord frame, ushort source, ushort state, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindState &&
        r.SourceId == source && r.TargetId == state && r.SignedValue == 0 &&
        Math.Abs(r.Duration - seconds) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);

    private static bool LinkedRecord(RpcRecord frame, ushort state, ushort target, int value, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindLinked &&
        r.SourceId == state && r.TargetId == target && r.SignedValue == value &&
        Math.Abs(r.Duration) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);

    private static bool SameLocator(RpcRecord add, RpcRecord remove) =>
        ManageBonusRecord.TryDecode(add.Bytes, out var first) && ManageBonusRecord.TryDecode(remove.Bytes, out var second) &&
        first!.Locator == second!.Locator;

    private static bool ProbeTimer(RpcRecord frame, bool end) =>
        BytePattern.Parse($"80 bd 01 c6 00 {AbilityCooldowns.OwnTimer:x2} 00 00 a0 41 {(end ? "00" : "01")}").IsMatch(frame.Bytes);

    private static void VerifyStand(ViewCreation view, int x, int z, byte yaw)
    {
        byte[] pos = view.Attributes["Position"];
        Check.Equal((double)x, BitConverter.ToDouble(pos), $"{view.Name} exact spawn x");
        Check.Equal((double)StandY, BitConverter.ToDouble(pos, 8), $"{view.Name} exact spawn y");
        Check.Equal((double)z, BitConverter.ToDouble(pos, 16), $"{view.Name} exact spawn z");
        Check.Equal(yaw, view.Attributes["Yaw Interval"][0], $"{view.Name} spawn yaw");
        Check.That(NavMesh.Locate(1, x, StandY, z, out var spot) && !spot.indoors && Math.Abs(spot.floorY + 95 - StandY) < 10,
            $"{view.Name} spawn is on outdoor walkable floor");
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
    private static long Descriptor(ushort id) => 0x80000000L | ((long)id << 10);

    private static string Insert(int actor, int slot, ushort id, bool loaded)
    {
        var ability = AbilityCatalog.Find(id);
        string name = ability.Name.Replace("'", "''");
        int rank = (int)Math.Min(50, ability.Number("Ability/AbilityLevelCap", 50));
        return $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES({actor},{slot},{Descriptor(id)},{rank},{(loaded ? 1 : 0)},'{name}')";
    }
}
