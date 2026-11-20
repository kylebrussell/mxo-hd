using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class MartialBlindEnrageAreaMode : NativeQaMode
{
    private const ushort KungFu = CombatStyles.KungFu;                                      // 133
    private const ushort MisdirectPunch = KungFuMasterSpecials.MisdirectPunch;              // 260
    private const ushort Karate = CombatStyles.Karate;                                      // 132
    private const ushort SidekickCombo = KarateAikidoMasterSpecials.SidekickCombo;          // 266
    private const ushort SwirlingKiSummon = MartialArtsRemainingSpecials.SwirlingKiSummon;  // 268
    private const ushort Probe = 198;                                                       // Cheap Shot
    private const uint SwirlingKiFx = 0x28000a43;                                           // 671091267 (Ability/ExecutionFX)

    private const int CasterX = 24751;                                                      // 2.52 m east of QAEnemy (<= 3 m)
    private const int StandY = CombatFixtureLoadout.GroundY;                                // 495
    private const int StandZ = CombatFixtureLoadout.CasterZ;                                // -1498
    private const int EnemyX = 24499, EnemyZ = StandZ;                                      // 2.52 m west (<= 3 m)
    private const int FriendX = 24625, FriendZ = StandZ + 200;                              // 2.36 m NW (<= 3 m, friendly)
    private const int FarX = CasterX + 400, FarZ = StandZ;                                  // 4.00 m east (> 3 m, hostile)

    private static readonly byte CasterYaw = MobAggro.YawTowards(CasterX, StandZ, EnemyX, EnemyZ);
    private static readonly byte EnemyYaw = MobAggro.YawTowards(EnemyX, EnemyZ, CasterX, StandZ);
    private static readonly byte FriendYaw = MobAggro.YawTowards(FriendX, FriendZ, CasterX, StandZ);
    private static readonly byte FarYaw = MobAggro.YawTowards(FarX, FarZ, CasterX, StandZ);

    // Exact stock requests from PR555/557/576/579.
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockKungFu = "80b985000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockKarate = "80b984000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockMisdirectPunch = "80b9040104000200000000000000000000000000000000000000000004010000006b0000086b0000000000";
    private const string StockSidekickCombo = "80b90a010400020000000000000000000000000000000000000000000a010000006b0000086b0000000000";
    private const string StockSwirlingKiSummon = "80b90c010000000000000000000000000000000000000000000000000c010000006b0000086b0000000000";
    private const string StockCheapShot = "80b9c600050003000000000000000000000000000000000000000000c6000000006b0000086b0000000000";
    private const string StockWithdraw = "44 01";

    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(15);
    private static readonly ushort[] CasterProfile = StockAbilityProfile
        .Profile(2, 17, KungFu, MisdirectPunch, Karate, SidekickCombo, SwirlingKiSummon)
        .Order().ToArray();
    private static readonly ushort[] RecipientProfile = StockAbilityProfile
        .Profile(2, 17, Probe)
        .Order().ToArray();

    private static readonly (string Name, uint CharId, int X, int Z, byte Yaw, int Org)[] Stands =
    {
        ("QAEnemy", 6114, EnemyX, EnemyZ, EnemyYaw, 2),
        ("QAFriend", 6111, FriendX, FriendZ, FriendYaw, 1),
        ("QAFar", 6112, FarX, FarZ, FarYaw, 2)
    };

    public override string Name => "ability-parity-ma-blind-enrage-area";
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
            $"UPDATE characters SET x={FriendX},y={StandY},z={FriendZ},rotation={FriendYaw},alignment=1,healthC=30000,healthM=30000,innerStrC=2000,innerStrM=2000 WHERE charId=6111",
            $"UPDATE characters SET x={FarX},y={StandY},z={FarZ},rotation={FarYaw},alignment=2,healthC=30000,healthM=30000,innerStrC=2000,innerStrM=2000 WHERE charId=6112",
            $"DELETE FROM inventory WHERE charId IN (110,6111,6112,6114) AND slot={CombatFixtureWeapons.HandSlot}",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)"
        }.Concat(RecipientProfile.Select((id, i) => Insert(6114, i, id, true))).ToArray();

        return new(
            CasterProfile, CasterX, StandZ, Array.Empty<string>(), Array.Empty<string>(), helpers,
            DevCommands: true,
            loadAbilityAncestors: false,
            testMobFixture: "martial-blind-enrage-area.csv",
            hotbarAbilities: new[] { KungFu, MisdirectPunch, Karate, SidekickCombo, SwirlingKiSummon },
            environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0",
                ["MXO_MOB_WANDER"] = "0",
                ["MXO_PVP_SERVER"] = "1",
                ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                [AreaTargetDiagnostics.Variable] = "1",
                [MartialBlindEnrageAreaQa.Mode] = "1"
            },
            extraQaMetadata: new()
            {
                ["ScopedStockRows"] = "260,266,268",
                ["PlayerRecipient"] = "QAEnemy (6114)",
                ["AreaWitnesses"] = "QAEnemy (2.52m hostile), QAFriend (2.36m friendly), QAFar (4.00m hostile)",
                ["LoadedMemory"] = mem,
                ["MemoryCapacity"] = AbilityMemory.ForLevel(50),
                ["FixedModifierChance"] = "266"
            },
            readyMessages: new[]
            {
                $"Level 50 rank 50 unarmed ({mem}/100 memory; Kung-Fu +39% melee, Karate +41% melee). CSR spawns at ({CasterX},{StandY},{StandZ}) outside the hardline booth facing QAEnemy (6114, hostile) 2.52 m west. QAFriend (6111, friendly) is 2.36 m NW (inside 3 m across the whole fight corridor); QAFar (6112, hostile) is 4.00 m east (outside 3 m). Fixed disposable modifier chance: 266 (20% -> 100%).",
                "Helper watches QAEnemy continuously in any order without session-fatal row timeouts. If any special misses, wait for its own reuse and retry the same slot. Wait for MARTIAL-BEA PASS before moving on.",
                "Row 1 — Misdirect Punch (260, Blind 4.0 s): From spawn, select QAEnemy (2.52 m west) and enter ordinary Close Combat. Press slot 1 (133 Kung-Fu), then slot 2 (260 Misdirect Punch). Expect 30 IS, 10.0 s own / 5.0 s group 688 reuse, 33 dmg (base 24 + 39%), QAEnemy Blind state 90 (4.0 s) + linked CombatTactics 163 -30, QAEnemy Cheap Shot (198) ADMITTED while blinded (not refused), then both Blind records removed at ~4.0 s + 'You are no longer blind.' Wait for MARTIAL-BEA PASS 260.",
                "Row 2 — Sidekick Combo (266, Enraged 8.0 s, fixed 100% proc): Stay in Close Combat with QAEnemy. Press slot 3 (132 Karate), then slot 4 (266 Sidekick Combo). Expect 25 IS, 10.0 s own / 5.0 s group 688 reuse, 39 dmg (base 28 + 41%), QAEnemy Enraged state 93 (8.0 s) + linked record, QAEnemy Withdraw (44 01) SUPPRESSED while enraged, both Enraged records removed at ~8.0 s + 'You are no longer enraged.', then Withdraw (44 01) ACCEPTED after expiry (QAEnemy exits Close Combat). Wait for MARTIAL-BEA PASS 266.",
                "Row 3 — Swirling Ki Summon (268, area FX + radius/friendly split): Once out of Close Combat (Karate 132 is already active, or press slot 3), clear selection (stay beside QAEnemy so QAEnemy <= 3 m, QAFriend <= 3 m, QAFar > 3 m) and press slot 5 (268 Swirling Ki Summon). Expect 60 IS, 2.0 s cast bar (80 ac 0c 01), caster ExecutionFX 0x28000a43 (43 0a 00 28) on CSR & watchers, 12.0 s own / 5.0 s group 687 reuse, AREA-QA QAEnemy included / QAFar reason=radius / QAFriend reason=pvp-NotOpponent, QAEnemy alone hit for 2 + Stat 522/524 -60 for 12.0 s (and matching 12 s removals). Wait for MARTIAL-BEA PASS 268.",
                "Keep CSR PacketLog, DebugLog, and martial-blind-enrage-area.jsonl for the three rows."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Martial Blind/Enrage/Area fixture requires the owned server.");
        var witnesses = new Dictionary<string, HeadlessPlayer>(StringComparer.Ordinal);
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            witnesses[stand.Name] = helper;
            Check.Equal(stand.CharId, helper.CharId, $"{stand.Name} helper identity");
            if (stand.CharId == 6114)
            {
                var owned = AbilityMemoryUiFixture.Decode(helper.CharacterData[6])
                    .Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
                Check.That(RecipientProfile.All(owned.Contains), "QAEnemy owns Cheap Shot (198) and prerequisites");
            }
            await PvpOpponentsFixture.EnterAsync(helper);
            VerifyStand(helper.World.CreationSnapshot().Last(v => v.ViewId == 2), stand.X, stand.Z, stand.Yaw);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        var enemy = witnesses["QAEnemy"];
        var friend = witnesses["QAFriend"];
        var far = witnesses["QAFar"];

        if (context.StockProbe is not { } caster)
        {
            _ = WatchNativeAsync(context, server, enemy, friend, far);
            return Array.Empty<byte>();
        }

        await PvpOpponentsFixture.EnterAsync(caster);
        VerifyStand(caster.World.CreationSnapshot().Last(v => v.ViewId == 2), CasterX, StandZ, CasterYaw);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var enemyView = await caster.WaitForViewNamedAsync("QAEnemy");
        var friendView = await caster.WaitForViewNamedAsync("QAFriend");
        var farView = await caster.WaitForViewNamedAsync("QAFar");
        PvpOpponentsFixture.Observe(enemyView, 2, 6114);
        PvpOpponentsFixture.Observe(friendView, 1, 6111);
        PvpOpponentsFixture.Observe(farView, 2, 6112);
        var csrAtEnemy = await enemy.WaitForViewNamedAsync(caster.Handle);
        VerifyStand(csrAtEnemy, CasterX, StandZ, CasterYaw);

        uint enemyHandle = Handle(enemyView);
        uint csrHandle = Handle(csrAtEnemy);
        string enemyHex = WireFormat.ToHex(BitConverter.GetBytes(enemyHandle));

        // 1. Open Close Combat with QAEnemy for 260 (Blind) and 266 (Enraged).
        int openRpc = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockSelfDefense, enemyHandle),
            WireFormat.FromHex($"40 {enemyHex} 01 6b"));
        await StockAbilityProfile.ObserveAsync(caster, openRpc, rpc => rpc.Bytes is [0x46, ..] &&
            rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1, "Real PvP interlock opens with QAEnemy");

        // 1a. Misdirect Punch (260): Blind 4s + linked -30, Cheap Shot 198 admitted while blind, expiry at ~4s.
        await SelectStyleAsync(caster, KungFu, StockKungFu, enemyHandle);
        caster.SendRpc($"81 51 {enemyHex}");
        await LandUntilObservedAsync(caster, enemy, StockMisdirectPunch, enemyHandle,
            frame => StateRecord(frame, MisdirectPunch, TempMods.BlindAbility, 4f, remove: false),
            async (rpcCursor, added) => await Observe260Async(context, enemy, csrHandle, rpcCursor, added));

        // 1b. Sidekick Combo (266): Enraged 8s (fixed 100% proc), Withdraw 44 01 suppressed, expiry at ~8s, Withdraw 44 01 accepted after expiry.
        await SelectStyleAsync(caster, Karate, StockKarate, enemyHandle);
        caster.SendRpc($"81 51 {enemyHex}");
        await LandUntilObservedAsync(caster, enemy, StockSidekickCombo, enemyHandle,
            frame => StateRecord(frame, SidekickCombo, TempMods.EnrageAbility, 8f, remove: false),
            async (rpcCursor, added) => await Observe266Async(context, server, enemy, rpcCursor, added));

        // 2. Swirling Ki Summon (268): direct cast after 266 (Karate already active), caster FX 0x28000a43, area split (QAEnemy in, QAFriend/QAFar out).
        int areaLogFrom = server.DebugLog().Length;
        int enemyRpc268 = enemy.World.RpcSnapshot().Count;
        int enemyView268 = enemy.World.ViewSnapshot().Count;
        int casterView268 = caster.World.ViewSnapshot().Count;
        int friendRpc268 = friend.World.RpcSnapshot().Count;
        int farRpc268 = far.World.RpcSnapshot().Count;
        caster.SendRpc(StockSwirlingKiSummon);
        await EvidenceRetestWire.CastBarAsync(caster, SwirlingKiSummon, 2);
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(casterView268).Any(HasSwirlingKiFx),
            "CSR receives 268 ExecutionFX 0x28000a43 on own view", Hold);
        await Observe268Async(context, server, enemy, friend, far, areaLogFrom, enemyRpc268, enemyView268, friendRpc268, farRpc268,
            awaitExpiry: true);

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count + far.World.Problems.Count,
            "Martial Blind/Enrage/Area replay packet framing");
        return Array.Empty<byte>();
    }

    private static async Task WatchNativeAsync(NativeQaBootstrapContext context, ServerInstance server,
        HeadlessPlayer enemy, HeadlessPlayer friend, HeadlessPlayer far)
    {
        string stop = Path.Combine(context.OutputDirectory, "stop");
        var passed = new HashSet<ushort>();
        var csr = await WaitForNativeAsync(() => enemy.World.CreationSnapshot().LastOrDefault(v => v.Name == "CSR_Tester"));
        if (csr == null) return;

        Console.WriteLine("MARTIAL-BEA READY: waiting for 268 (Swirling Ki Summon), 260 (Misdirect Punch), and 266 (Sidekick Combo) in any order. Session will stay open for retries.");

        int rpcCursor = enemy.World.RpcSnapshot().Count;
        int viewCursor = enemy.World.ViewSnapshot().Count;
        int friendCursor = friend.World.RpcSnapshot().Count;
        int farCursor = far.World.RpcSnapshot().Count;
        int logCursor = server.DebugLog().Length;

        while (Running())
        {
            var rpcs = enemy.World.RpcSnapshot();
            if (rpcCursor < rpcs.Count)
            {
                int foundIndex = -1;
                ushort matched = 0;
                RpcRecord? trigger = null;
                for (int i = rpcCursor; i < rpcs.Count; i++)
                {
                    var f = rpcs[i];
                    if (StatRecord(f, SwirlingKiSummon, 522, -60, 12f, remove: false)) { foundIndex = i; matched = SwirlingKiSummon; trigger = f; break; }
                    if (StateRecord(f, MisdirectPunch, TempMods.BlindAbility, 4f, remove: false)) { foundIndex = i; matched = MisdirectPunch; trigger = f; break; }
                    if (StateRecord(f, SidekickCombo, TempMods.EnrageAbility, 8f, remove: false)) { foundIndex = i; matched = SidekickCombo; trigger = f; break; }
                }

                if (matched != 0 && trigger != null)
                {
                    // Start slightly before the matched record so paired records (e.g. 268's 524/522) are always visible.
                    int eventStart = Math.Max(0, foundIndex - 4);
                    // Advance past trigger immediately so the same event is never re-dispatched.
                    rpcCursor = foundIndex + 1;
                    try
                    {
                        if (matched == SwirlingKiSummon)
                        {
                            await Observe268Async(context, server, enemy, friend, far, logCursor, eventStart, viewCursor, friendCursor, farCursor, awaitExpiry: true);
                        }
                        else if (matched == MisdirectPunch)
                        {
                            var latestCsr = enemy.World.CreationSnapshot().LastOrDefault(v => v.Name == "CSR_Tester") ?? csr;
                            await Observe260Async(context, enemy, Handle(latestCsr), eventStart, trigger);
                        }
                        else if (matched == SidekickCombo)
                        {
                            await Observe266Async(context, server, enemy, eventStart, trigger);
                        }
                        passed.Add(matched);
                        if (passed.Contains(SwirlingKiSummon) && passed.Contains(MisdirectPunch) && passed.Contains(SidekickCombo))
                            Console.WriteLine("MARTIAL-BEA ALL PASS (268, 260, 266) — you may stop the session or retry any row.");
                    }
                    catch (Exception ex)
                    {
                        if (!Running()) return;
                        Console.WriteLine($"MARTIAL-BEA RETRY {matched} ({ex.Message}) — session remains alive; retry after reuse.");
                    }
                    // Snapshot fresh cursors for the next row or retry.
                    rpcCursor = enemy.World.RpcSnapshot().Count;
                    viewCursor = enemy.World.ViewSnapshot().Count;
                    friendCursor = friend.World.RpcSnapshot().Count;
                    farCursor = far.World.RpcSnapshot().Count;
                    logCursor = server.DebugLog().Length;
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

    private static async Task Observe268Async(NativeQaBootstrapContext context, ServerInstance server,
        HeadlessPlayer enemy, HeadlessPlayer friend, HeadlessPlayer far,
        int logFrom, int enemyRpcFrom, int enemyViewFrom, int friendRpcFrom, int farRpcFrom, bool awaitExpiry)
    {
        var s522Add = await StockAbilityProfile.ObserveAsync(enemy, enemyRpcFrom,
            f => StatRecord(f, SwirlingKiSummon, 522, -60, 12f, remove: false), "QAEnemy 268 Stat 522 -60 12s add");
        long hitAt = Stopwatch.GetTimestamp();
        var s524Add = await StockAbilityProfile.ObserveAsync(enemy, enemyRpcFrom,
            f => StatRecord(f, SwirlingKiSummon, 524, -60, 12f, remove: false), "QAEnemy 268 Stat 524 -60 12s add");
        var fxUpdate = enemy.World.ViewSnapshot().Skip(enemyViewFrom).FirstOrDefault(HasSwirlingKiFx);
        Check.That(fxUpdate != null, "QAEnemy observed CSR 268 ExecutionFX 0x28000a43");

        // Sync bystanders so any stray packet would already be delivered before asserting exclusion.
        friend.SendRpc("81 48");
        far.SendRpc("81 48");
        await friend.WaitForRpcAsync("81 49", Hold);
        await far.WaitForRpcAsync("81 49", Hold);

        bool friendHit = friend.World.RpcSnapshot().Skip(friendRpcFrom).Any(Is268Packet);
        bool farHit = far.World.RpcSnapshot().Skip(farRpcFrom).Any(Is268Packet);
        Check.That(!friendHit, "QAFriend (friendly <= 3 m) excluded from 268");
        Check.That(!farHit, "QAFar (hostile > 3 m) excluded from 268");

        string logSlice = server.DebugLog()[logFrom..];
        Check.That(logSlice.Contains("[AREA-QA]", StringComparison.Ordinal) &&
            logSlice.Contains("QAEnemy", StringComparison.Ordinal) &&
            logSlice.Contains("QAFar", StringComparison.Ordinal) &&
            logSlice.Contains("reason=radius", StringComparison.Ordinal) &&
            logSlice.Contains("QAFriend", StringComparison.Ordinal) &&
            logSlice.Contains("reason=pvp-NotOpponent", StringComparison.Ordinal),
            "AREA-QA logged QAEnemy included, QAFar radius excluded, QAFriend pvp-NotOpponent excluded");

        Console.WriteLine("MARTIAL-BEA 268 LANDED: FX 0x28000a43 observed; QAEnemy hit (522/524 -60 12s); QAFriend & QAFar excluded. Waiting for 12s expiry...");

        string s522RemHex = "", s524RemHex = "";
        double elapsed = 0;
        if (awaitExpiry)
        {
            int remFrom = enemy.World.RpcSnapshot().Count;
            var s522Rem = await ObserveRpcAsync(enemy, remFrom,
                f => StatRecord(f, SwirlingKiSummon, 522, -60, 12f, remove: true) && SameLocator(s522Add, f),
                "QAEnemy 268 Stat 522 removal", TimeSpan.FromSeconds(18));
            var s524Rem = await ObserveRpcAsync(enemy, remFrom,
                f => StatRecord(f, SwirlingKiSummon, 524, -60, 12f, remove: true) && SameLocator(s524Add, f),
                "QAEnemy 268 Stat 524 removal", TimeSpan.FromSeconds(18));
            elapsed = Stopwatch.GetElapsedTime(hitAt).TotalSeconds;
            Check.That(Math.Abs(elapsed - 12.0) < 2.0, $"268 Stat 522/524 expiry at {elapsed:F2}s (expected ~12.0s)");
            s522RemHex = s522Rem.Hex;
            s524RemHex = s524Rem.Hex;
        }

        AppendRecord(context, new
        {
            Ability = SwirlingKiSummon,
            Kind = "AreaFx",
            FxHex = Convert.ToHexString(fxUpdate!),
            Stat522Add = s522Add.Hex,
            Stat524Add = s524Add.Hex,
            Stat522Remove = s522RemHex,
            Stat524Remove = s524RemHex,
            ExpirySeconds = Math.Round(elapsed, 2),
            FriendExcluded = !friendHit,
            FarExcluded = !farHit
        });
        Console.WriteLine($"MARTIAL-BEA PASS 268: FX 0x28000a43 + area split verified + Stat 522/524 removed at {elapsed:F2}s — READY FOR NEXT");
    }

    private static async Task Observe260Async(NativeQaBootstrapContext context, HeadlessPlayer enemy,
        uint csrHandle, int rpcFrom, RpcRecord stateAdd)
    {
        long stateAt = Stopwatch.GetTimestamp();
        var linkedAdd = await StockAbilityProfile.ObserveAsync(enemy, rpcFrom,
            f => LinkedRecord(f, TempMods.BlindAbility, TempMods.CombatTacticsAbility, -30, remove: false),
            "QAEnemy 260 Blind linked -30 CombatTactics add");

        // While Blinded, attempt an ordinary combat special (Cheap Shot 198): Blind must NOT refuse it.
        int probeFrom = enemy.World.RpcSnapshot().Count;
        int viewFrom = enemy.World.ViewSnapshot().Count;
        enemy.SendRpc(StarterHackerControlTargetMode.WithHandle(StockCheapShot, csrHandle));
        var probeTimer = await StockAbilityProfile.ObserveAsync(enemy, probeFrom,
            f => ProbeTimer(f, end: false), "Cheap Shot (198) admitted during Blind (not refused)");
        bool anyRefusal = enemy.World.RpcSnapshot().Skip(probeFrom)
            .Any(f => AbilityRefusals.TryDecode(f.Bytes, out var r) && r.AbilityID == Probe);
        Check.That(!anyRefusal, "Blind did not refuse Cheap Shot (198)");

        // Capture both Blind removals + "You are no longer blind." at ~4.0 s.
        var stateRem = await StockAbilityProfile.ObserveAsync(enemy, probeFrom,
            f => StateRecord(f, MisdirectPunch, TempMods.BlindAbility, 4f, remove: true) && SameLocator(stateAdd, f),
            "QAEnemy 260 Blind state removal");
        double elapsed = Stopwatch.GetElapsedTime(stateAt).TotalSeconds;
        var linkedRem = await StockAbilityProfile.ObserveAsync(enemy, probeFrom,
            f => LinkedRecord(f, TempMods.BlindAbility, TempMods.CombatTacticsAbility, -30, remove: true) && SameLocator(linkedAdd, f),
            "QAEnemy 260 Blind linked -30 removal");
        byte[] stateEndBytes = AbilityMessages.StateEnd(TempMods.BlindAbility, "QAEnemy");
        var stateEnd = await StockAbilityProfile.ObserveAsync(enemy, probeFrom,
            f => f.Bytes.AsSpan().SequenceEqual(stateEndBytes),
            "QAEnemy Blind StateEnd message");
        Check.That(Math.Abs(elapsed - 4.0) < 1.5, $"260 Blind expiry at {elapsed:F2}s (expected ~4.0s)");

        AppendRecord(context, new
        {
            Ability = MisdirectPunch,
            Kind = "Blind",
            StateAdd = stateAdd.Hex,
            LinkedAdd = linkedAdd.Hex,
            AdmittedProbeTimer = probeTimer.Hex,
            StateRemove = stateRem.Hex,
            LinkedRemove = linkedRem.Hex,
            StateEnd = stateEnd.Hex,
            ExpirySeconds = Math.Round(elapsed, 2)
        });
        Console.WriteLine($"MARTIAL-BEA PASS 260: Blind 4s + linked -30 landed; Cheap Shot 198 admitted while blinded; removed at {elapsed:F2}s — READY FOR NEXT");
    }

    private static async Task Observe266Async(NativeQaBootstrapContext context, ServerInstance server,
        HeadlessPlayer enemy, int rpcFrom, RpcRecord stateAdd)
    {
        long stateAt = Stopwatch.GetTimestamp();
        var linkedAdd = await StockAbilityProfile.ObserveAsync(enemy, rpcFrom,
            f => LinkedRecord(f, TempMods.EnrageAbility, 0, 0, remove: false),
            "QAEnemy 266 Enraged linked add");

        // While Enraged, send ordinary Withdraw (44 01) and verify it is suppressed (!CanWithdraw).
        int logBeforeSuppressed = server.DebugLog().Length;
        enemy.SendRpc(StockWithdraw);
        enemy.SendRpc("81 48");
        await enemy.WaitForRpcAsync("81 49", Hold);
        bool suppressedOk = !server.DebugLog()[logBeforeSuppressed..].Contains("[COMBAT] Leave combat requested", StringComparison.Ordinal);
        Check.That(suppressedOk, "Withdraw (44 01) suppressed while Enraged");

        // Capture both Enraged removals + "You are no longer enraged." at ~8.0 s.
        int remFrom = enemy.World.RpcSnapshot().Count;
        var stateRem = await ObserveRpcAsync(enemy, remFrom,
            f => StateRecord(f, SidekickCombo, TempMods.EnrageAbility, 8f, remove: true) && SameLocator(stateAdd, f),
            "QAEnemy 266 Enraged state removal", TimeSpan.FromSeconds(14));
        double elapsed = Stopwatch.GetElapsedTime(stateAt).TotalSeconds;
        var linkedRem = await ObserveRpcAsync(enemy, remFrom,
            f => LinkedRecord(f, TempMods.EnrageAbility, 0, 0, remove: true) && SameLocator(linkedAdd, f),
            "QAEnemy 266 Enraged linked removal", TimeSpan.FromSeconds(14));
        byte[] stateEndBytes = AbilityMessages.StateEnd(TempMods.EnrageAbility, "QAEnemy");
        var stateEnd = await ObserveRpcAsync(enemy, remFrom,
            f => f.Bytes.AsSpan().SequenceEqual(stateEndBytes),
            "QAEnemy Enraged StateEnd message", TimeSpan.FromSeconds(14));
        Check.That(Math.Abs(elapsed - 8.0) < 1.5, $"266 Enraged expiry at {elapsed:F2}s (expected ~8.0s)");

        // After Enraged expires, send ordinary Withdraw (44 01) again and verify it is ACCEPTED.
        int logBeforeRecovery = server.DebugLog().Length;
        enemy.SendRpc(StockWithdraw);
        await enemy.WaitUntilAsync(() =>
            server.DebugLog()[logBeforeRecovery..].Contains("[COMBAT] Leave combat requested", StringComparison.Ordinal),
            "Withdraw (44 01) accepted after Enraged expires", Hold);

        AppendRecord(context, new
        {
            Ability = SidekickCombo,
            Kind = "Enraged",
            StateAdd = stateAdd.Hex,
            LinkedAdd = linkedAdd.Hex,
            WithdrawSuppressedWhileEnraged = suppressedOk,
            StateRemove = stateRem.Hex,
            LinkedRemove = linkedRem.Hex,
            StateEnd = stateEnd.Hex,
            WithdrawAcceptedAfterRelease = true,
            ExpirySeconds = Math.Round(elapsed, 2)
        });
        Console.WriteLine($"MARTIAL-BEA PASS 266: Enraged 8s landed; Withdraw 44 01 suppressed while enraged; removed at {elapsed:F2}s; Withdraw accepted after release — READY FOR NEXT");
    }

    private static async Task LandUntilObservedAsync(HeadlessPlayer caster, HeadlessPlayer enemy,
        string stockRequest, uint enemyHandle, Func<RpcRecord, bool> matchesAdd,
        Func<int, RpcRecord, Task> observe)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
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
            await Task.Delay(TimeSpan.FromSeconds(10.5));
        }
        throw new InvalidOperationException($"Did not observe expected state add after 8 attempts for {stockRequest[..12]}");
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
        File.AppendAllText(Path.Combine(context.OutputDirectory, "martial-blind-enrage-area.jsonl"),
            JsonSerializer.Serialize(record) + Environment.NewLine);

    private static bool HasSwirlingKiFx(byte[] viewBytes)
    {
        byte[] fx = BitConverter.GetBytes(SwirlingKiFx);
        return BytePattern.IndexOf(viewBytes, fx, 0) >= 0;
    }

    private static bool Is268Packet(RpcRecord frame) =>
        (ManageBonusRecord.TryDecode(frame.Bytes, out var rec) && rec!.SourceId == SwirlingKiSummon) ||
        BytePattern.IndexOf(frame.Bytes, BitConverter.GetBytes(AbilityMessages.AbilityGameObject(SwirlingKiSummon)), 0) >= 0;

    private static bool StateRecord(RpcRecord frame, ushort source, ushort state, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindState &&
        r.SourceId == source && r.TargetId == state && r.SignedValue == 0 &&
        Math.Abs(r.Duration - seconds) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);

    private static bool LinkedRecord(RpcRecord frame, ushort state, ushort target, int value, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindLinked &&
        r.SourceId == state && r.TargetId == target && r.SignedValue == value &&
        Math.Abs(r.Duration) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);

    private static bool StatRecord(RpcRecord frame, ushort source, ushort stat, int value, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindStat &&
        r.SourceId == source && r.TargetId == stat && r.SignedValue == value &&
        Math.Abs(r.Duration - seconds) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);

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
