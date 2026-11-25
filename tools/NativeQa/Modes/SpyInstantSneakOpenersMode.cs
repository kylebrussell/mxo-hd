using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class SpyInstantSneakOpenersMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, DummyZ = 6700, DummyBackZ = 6448;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private const ushort SpinningBacksweep = SpinningBacksweepAdmission.SpinningBacksweep; // 152
    private const ushort Suplex = SuplexDamage.Suplex;                                     // 287
    private const ushort BullDog = BullDogDamage.Id;                                       // 387
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, SpinningBacksweep, Suplex, BullDog);

    public override string Name => "ability-parity-spy-instant-sneak-openers";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET x=21650,z=16250,rotation=192 WHERE charId=6111",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id=6114",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new[] { Spy, Sneak, SpinningBacksweep, Suplex, BullDog },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "152,287,387",
            ["NativeCloseableRows"] = 3,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (+54% Thrown influence). Verified outdoor HOME ({X},{Y},{Z}) faces east-facing QAEnemy's back 2 m east ({EnemyX},{Y},{Z}), QAFriend nearby. Page 2: 1=Spy Style (1112), 2=Sneak (1111), 3=Spinning Backsweep (152, 6 m), 4=Suplex (287, 6 m), 5=Bull Dog (387, 6 m). No initial tp needed.",
            "Step 1 (Cast first at HOME — one opener per 15 s family 691 window, Sneak before each): Select slot 1 (Spy Style), /target QAEnemy, press slot 2 (Sneak), then slot 3 (152) -> instant, 20 IS, 1111 off, 30 s own/15 s grp 691, 158 Thrown dmg, no interlock. After 15 s grp 691: Sneak, slot 4 (287) -> instant, 50 IS, 1111 off, 38 s own/15 s grp 691, 646 Thrown dmg + Dazed (199)/Off-Balance (200)/Staggered (201) for 15 s with matched removals, no interlock. After 15 s grp 691: Sneak, slot 5 (387) -> instant, 37 IS, 1111 off, 60 s own/15 s grp 691, 1578 Thrown dmg, no interlock.",
            "Step 2 (Rear-opener refusals — no IS debit or timer): Before Sneak -> local gate or reason 23 (StealthedFlag). Sneak on + /target QAFriend -> reason 24. At printed FRONT (inside QAEnemy's 50-deg cone), BLOCK (solid wall), and FAR (>6 m), /target QAEnemy and press slots 3/4/5 -> reason 24/9/34 or local gate without debit.",
            "Step 3 (+75 Thrown resistance on rear-facing Melee Dummy): Type printed DUMMY chunks (17000,495,6448 facing north), type printed RESISTED chunks (?cd fixture-reset-thrown-resisted) and stay where the reset places you (2.52 m behind the north-facing dummy), select fresh dummy, if Close Combat opens type '?cd stop' then reselect slot 1 (Spy Style), press slot 2 (Sneak), press slot 3 (152) -> [TYPED-RESISTANCE OURS] 152 type7 total75 raw158 final130."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));

        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        var front = StarterConcealedShotTargetingMode.Stand(21850, 16200);
        var block = StarterConcealedShotTargetingMode.Stand(21950, Z);
        var far = StarterConcealedShotTargetingMode.FindFarStand();
        Check.That(NavMesh.Locate(1, DummyX, DummyY, DummyBackZ, out var dummyStand) && !dummyStand.indoors && dummyStand.floorY + 95 == DummyY,
            "Verified rear stand behind Melee Dummy");
        var dummyTp = new Teleport(
            $"?tp {DummyX} {DummyY} {DummyBackZ} {dummyStand.sector:x} {dummyStand.region:x} {dummyStand.polygon:x} - 00",
            $"Teleported to {dummyStand.sector:x}/{dummyStand.region:x}");

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear two-metre rear approach to QAEnemy");
        Check.That(NavMesh.HasSightPath(1, 21850, Y, 16200, EnemyX, Y, Z) &&
            Stealth.InFrustum(EnemyX, Z, 64, 21850, 16200, 50), "Clear frontal stand inside QAEnemy's 50-deg cone");
        Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall blocks opener sight");
        Check.That(NavMesh.HasSightPath(1, DummyX, DummyY, DummyBackZ, DummyX, DummyY, DummyZ) &&
            !Stealth.InFrustum(DummyX, DummyZ, 0, DummyX, DummyBackZ, 50), "Rear stand behind north-facing reset Melee Dummy");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-50 loadout fits memory");

        PrintChunks("HOME", home);
        PrintChunks("FRONT", front);
        PrintChunks("BLOCK", block);
        PrintChunks("FAR", far);
        PrintTeleport("DUMMY", dummyTp);
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");

        var server = context.ActualServer!;
        var enemy = await LoginAsync("QAEnemy");
        var friend = await LoginAsync("QAFriend");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint target = await HandleAsync(caster, enemy);

        // WrongStyle before Spy Style.
        foreach (ushort id in new[] { SpinningBacksweep, Suplex, BullDog })
            await RefusedAsync(caster, id, Request(id, target), AbilityRefusals.WrongStyle);

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        // CasterStateRequired (StealthedFlag) before Sneak.
        foreach (ushort id in new[] { SpinningBacksweep, Suplex, BullDog })
        {
            int preStealth = caster.World.RpcSnapshot().Count;
            await RefusedAsync(caster, id, Request(id, target), AbilityRefusals.CasterStateRequired);
            await ObserveAsync(caster, preStealth, rpc => AbilityRefusals.TryDecode(rpc.Bytes, out var r) &&
                r.AbilityID == id && r.StatesNotMet == Stealth.StealthedFlag, $"{id} refusal carries StealthedFlag");
        }

        // Enter Sneak and verify ally, stale, frontal, wall, and range refusals for all three.
        await SneakAsync();
        uint friendHandle = await HandleAsync(caster, friend);
        foreach (ushort id in new[] { SpinningBacksweep, Suplex, BullDog })
        {
            await RefusedAsync(caster, id, Request(id, 0), AbilityRefusals.InvalidTarget);
            await RefusedAsync(caster, id, Request(id, friendHandle), AbilityRefusals.InvalidTarget);
            await RefusedAsync(caster, id, Request(id, target ^ 0x10000), AbilityRefusals.InvalidTarget);
        }

        await MoveAsync(front);
        uint frontTarget = await HandleAsync(caster, enemy);
        foreach (ushort id in new[] { SpinningBacksweep, Suplex, BullDog })
            await RefusedAsync(caster, id, Request(id, frontTarget), AbilityRefusals.InvalidTarget);

        await MoveAsync(block);
        uint blockTarget = await HandleAsync(caster, enemy);
        foreach (ushort id in new[] { SpinningBacksweep, Suplex, BullDog })
            await RefusedAsync(caster, id, Request(id, blockTarget), AbilityRefusals.TargetNotInSight);

        await MoveAsync(far);
        uint farTarget = await HandleAsync(caster, enemy);
        foreach (ushort id in new[] { SpinningBacksweep, Suplex, BullDog })
            await RefusedAsync(caster, id, Request(id, farTarget), AbilityRefusals.TargetOutOfRange);

        await MoveAsync(home);
        target = await HandleAsync(caster, enemy);

        // Cast 1: 152 Spinning Backsweep -> 20 IS, 158 dmg (28+75 + 54%), 30 s own / 15 s grp 691, Sneak off.
        int casterRpc = caster.World.RpcSnapshot().Count;
        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
        caster.SendRpc(Request(SpinningBacksweep, target));
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, SpinningBacksweep, caster.Handle, enemy.Handle, 158, 0)),
            "152 hits QAEnemy for 158 Thrown damage");
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, SpinningBacksweep, AbilityCooldowns.OwnTimer, 30);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=152 paid=20", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("1111 off (used ability 152)", StringComparison.Ordinal),
            "152 pays 20 IS and ends Sneak", HeadlessPlayer.DefaultTimeout);
        await WaitFamily691EndAsync(casterRpc);

        // Cast 2: 287 Suplex -> 50 IS, 646 dmg (120+300 + 54%), 38 s own / 15 s grp 691, Dazed/Off-Balance/Staggered 15 s.
        await SneakAsync();
        casterRpc = caster.World.RpcSnapshot().Count;
        enemyRpc = enemy.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(Suplex, target));
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, Suplex, caster.Handle, enemy.Handle, 646, 0)),
            "287 hits QAEnemy for 646 Thrown damage");
        foreach (ushort state in new[] { TempMods.DazeAbility, TempMods.OffBalanceAbility, TempMods.StaggeredAbility })
            await ObserveAsync(enemy, enemyRpc, p => StateRecord(p, Suplex, state, 15, false), $"287 applies state {state} for 15 s");
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, Suplex, AbilityCooldowns.OwnTimer, 38);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=287 paid=50", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("1111 off (used ability 287)", StringComparison.Ordinal),
            "287 pays 50 IS and ends Sneak", HeadlessPlayer.DefaultTimeout);
        foreach (ushort state in new[] { TempMods.DazeAbility, TempMods.OffBalanceAbility, TempMods.StaggeredAbility })
            await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(enemyRpc).Any(p => StateRecord(p, Suplex, state, 15, true)),
                $"287 state {state} expires cleanly at 15 s", TimeSpan.FromSeconds(20));
        await WaitFamily691EndAsync(casterRpc);

        // Cast 3: 387 Bull Dog -> 37 IS, 1578 dmg (75+950 + 54%), 60 s own / 15 s grp 691.
        await SneakAsync();
        casterRpc = caster.World.RpcSnapshot().Count;
        enemyRpc = enemy.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(BullDog, target));
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, BullDog, caster.Handle, enemy.Handle, 1578, 0)),
            "387 hits QAEnemy for 1578 Thrown damage");
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, BullDog, AbilityCooldowns.OwnTimer, 60);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=387 paid=37", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("1111 off (used ability 387)", StringComparison.Ordinal),
            "387 pays 37 IS and ends Sneak", HeadlessPlayer.DefaultTimeout);
        await WaitFamily691EndAsync(casterRpc);

        // Step 3: +75 Thrown Resistance on rear-facing Melee Dummy (152: 158 -> 130).
        caster.SkipReceived();
        caster.SendChat(dummyTp.Command);
        await caster.WaitForTextAsync(dummyTp.Confirmation);
        int creationsBefore = caster.World.CreationSnapshot().Count;
        caster.SkipReceived();
        caster.SendChat("?cd fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset:");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(creationsBefore)
            .Any(v => v.Name == CombatFixtureLoadout.DummyName), "Resisted dummy spawns", HeadlessPlayer.DefaultTimeout);
        var resDummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint resHandle = DuelPackets.Handle(resDummy.ViewId, resDummy.SpawnId);

        await SneakAsync();
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(SpinningBacksweep, resHandle));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[logFrom..].Contains($"[ABILITY] 152 lands on view {resDummy.ViewId} for 130", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("[TYPED-RESISTANCE OURS] 152 type7 total75 raw158 final130", StringComparison.Ordinal),
            "152 hits +75 Thrown-resisted rear dummy for 130", TimeSpan.FromSeconds(10));

        Check.That(!server.DebugLog().Contains("char=6111 source=152", StringComparison.Ordinal) &&
            !server.DebugLog().Contains("char=6111 source=287", StringComparison.Ordinal) &&
            !server.DebugLog().Contains("char=6111 source=387", StringComparison.Ordinal),
            "QAFriend receives zero hostile records");
        Check.That(!server.DebugLog().Contains("[PVP] Round", StringComparison.Ordinal),
            "Instant sneak openers do not open PvP interlock");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Clean stock framing for 152/287/387 instant sneak openers");
        Console.WriteLine("SPY INSTANT SNEAK OPENERS PASS: refusals, 152/287/387 rear casts, Suplex 15 s states, and +75 Thrown resistance verified.");
        return Array.Empty<byte>();

        async Task SneakAsync()
        {
            int from = server.DebugLog().Length;
            caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
            await caster.WaitUntilAsync(() => server.DebugLog()[from..].Contains("1111 on (", StringComparison.Ordinal),
                "Sneak activates", HeadlessPlayer.DefaultTimeout);
        }

        async Task WaitFamily691EndAsync(int from)
        {
            await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(from)
                .Any(f => BytePattern.Parse($"80 bd 01 b3 02 {AbilityCooldowns.GroupTimer:x2} 00 00 70 41 00").IsMatch(f.Bytes)),
                "family 691 15 s reuse completes", TimeSpan.FromSeconds(20));
        }

        async Task MoveAsync(StarterConcealedShotTargetingMode.Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }

        async Task<HeadlessPlayer> LoginAsync(string name)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
            return player;
        }
    }

    private sealed record Teleport(string Command, string Confirmation);

    private static void PrintChunks(string label, StarterConcealedShotTargetingMode.Teleport stand)
    {
        string[] w = stand.Command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', w[..3])}', then ' {string.Join(' ', w[3..7])}', then ' {w[7]} {w[8]}'. Press Enter once.");
    }

    private static void PrintTeleport(string label, Teleport stand)
    {
        string[] w = stand.Command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', w[..3])}', then ' {string.Join(' ', w[3..7])}', then ' {w[7]} {w[8]}'. Press Enter once.");
    }

    private static async Task<uint> HandleAsync(HeadlessPlayer observer, HeadlessPlayer target)
    {
        var view = await observer.WaitForViewNamedAsync(target.Handle);
        return DuelPackets.Handle(view.ViewId, view.SpawnId);
    }

    private static byte[] Request(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();

    private static bool StateRecord(RpcRecord frame, ushort source, ushort state, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindState &&
        r.SourceId == source && r.TargetId == state && r.SignedValue == 0 &&
        Math.Abs(r.Duration - seconds) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);
}
