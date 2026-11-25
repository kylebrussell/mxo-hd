using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class DeadlyCripplingThrowsParityMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort DeadlyThrow = DeadlyThrowDamage.Id;               // 279
    private const ushort CripplingThrow = SpyAbilityDamage.CripplingThrow; // 280
    private const int Level = 25, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, FarZ = 8401;
    private const int EnemyX = 16800, FriendX = 17150;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, DeadlyThrow, CripplingThrow);

    public override string Name => "ability-parity-deadly-crippling-throws";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=0,alignment=1,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},rotation=192,alignment=1,healthC=5000,healthM=5000 WHERE charId=6111",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={AcrossWallX},y={WallY},z={WallZ},rotation=192,alignment=2,healthC=5000,healthM=5000 WHERE charId=6113",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6113,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6113,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { Spy, DeadlyThrow, CripplingThrow }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_CONDITIONAL_THROW_QA"] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [DeadlyThrowProcsQa.Mode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "279,280",
            ["NativeCloseableRows"] = 2,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost),
            ["ProcDrawAid"] = "?deadlyproc <both|daze|stagger|none|off> (defaults both)"
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (+28% Thrown influence, +5% Root duration from rank-25 Wicked Aim 278). Verified outdoor HOME ({X},{Y},{Z}) faces Melee Dummy 3 m north ({X},{Y},{DummyZ}), QAEnemy 2 m west ({EnemyX},{Y},{Z}), QAFriend 1.5 m east ({FriendX},{Y},{Z}). Page 2: 1=Spy Style (1112), 2=Deadly Throw (279), 3=Crippling Throw (280), 4=Withdraw. No initial tp needed.",
            "Step 1 (Targeting refusals for 279 & 280 — no IS debit, cast or timer): Select slot 1 (Spy Style), stay out of combat. Try slot 2 & slot 3 with selection cleared and with /target QAFriend -> InvalidTarget (24) or stock suppression. Type printed WALL chunks and /target QAOther (5 m across solid wall) -> TargetNotInSight (9). Type printed FAR chunks, turn south and select Melee Dummy (17.01 m > 16 m) -> TargetOutOfRange (34).",
            "Step 2 (280 Crippling Throw full lifecycle on QAEnemy): Return to HOME, /target QAEnemy, press slot 3 at rest. Save the first full stock 280 request. Expect 2.0 s cast, 20 IS, 10 s own / 3 s group 1215 reuse, 1 Thrown damage (first health change protected so Root stays on), QAEnemy source280 Rooted (99) for 15.75 s + Force Combat (519 -40) for 15 s, zero records on QAFriend, and paired removals at expiry.",
            "Step 3 (280 Root break on subsequent damage + 279 Deadly Throw full records): After 280 expires, cast slot 3 (280) on QAEnemy -> Rooted stays active through the 1 dmg hit; immediately cast slot 2 (279, default ?deadlyproc both) -> 2.0 s cast, 18 IS, 10 s own / 3 s group 1216 reuse, 40 Thrown dmg (no Off-Balance bonus), IMMEDIATELY breaks 280's Rooted (99) + clears parent 280 (519 removed early), and applies all 6 source279 records on QAEnemy: Stat 877 -85, Stat 519 -15, Stat 878 -72, Stat 115 -57 (15 s) + State 199 Dazed (8 s) + State 201 Staggered (8 s) with paired removals and zero QAFriend effect.",
            "Step 4 (279 independent 20% procs + DealDamageIfOffBalance + 280 in-combat refusal): Wait 10 s reuse; run '?deadlyproc daze' and '?cd daze 30', cast slot 2 -> 40 dmg (NO bonus hit on Daze alone) + ONLY 199 Dazed (8 s) + 4 stats. Wait 10 s reuse; run '?deadlyproc stagger' and '?cd offbalance 30', cast slot 2 -> 40 base + separate 42 Off-Balance Thrown bonus hit + ONLY 201 Staggered (8 s) + 4 stats. Optional: '?deadlyproc none' -> 40 dmg + 4 stats only. Finally enter Close Combat on Melee Dummy and try slot 3 (280) -> local gate or MustBeOutsideCombat (22) without debit."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Deadly/Crippling Throw mode requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var home = Stand(X, Y, Z, indoors: false, facing: 0);
        var far = Stand(X, Y, FarZ, indoors: false, facing: 128);
        var wall = Stand(WallX, WallY, WallZ, indoors: true, facing: 64);
        PrintStand("HOME", home);
        PrintStand("FAR", far);
        PrintStand("WALL", wall);

        _ = Stand(X, Y, DummyZ, indoors: false, facing: 128);
        _ = Stand(EnemyX, Y, Z, indoors: false, facing: 64);
        _ = Stand(FriendX, Y, Z, indoors: false, facing: 192);
        _ = Stand(AcrossWallX, WallY, WallZ, indoors: true, facing: 192);

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME has clear sight to Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "HOME has clear sight to QAEnemy");
        Check.That(NavMesh.HasSightPath(1, X, Y, FarZ, X, Y, DummyZ), "FAR has clear sight beyond 16 m range");
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Solid office wall blocks throw sight");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-25 loadout fits memory");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        var enemy = await LoginAsync("QAEnemy");
        var friend = await LoginAsync("QAFriend");
        var other = await LoginAsync("QAOther");

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        uint enemyHandle = await HandleAsync(caster, enemy);
        uint friendHandle = await HandleAsync(caster, friend);

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await ObserveAsync(caster, 0, frame => Bonus(frame, Spy, 1034, 16, 0, false),
            "Spy Style activates +16 Thrown influence at level 25");

        // 1. Targeting refusals for BOTH 279 and 280 (empty, ally, stale spawn, wall LOS, >16 m range).
        foreach (ushort id in new[] { DeadlyThrow, CripplingThrow })
        {
            await RefusedAsync(caster, id, Request(id, 0), AbilityRefusals.InvalidTarget);
            await RefusedAsync(caster, id, Request(id, friendHandle), AbilityRefusals.InvalidTarget);
            await RefusedAsync(caster, id, Request(id, enemyHandle ^ 0x10000), AbilityRefusals.InvalidTarget);
        }

        await MoveAsync(far);
        uint farDummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        foreach (ushort id in new[] { DeadlyThrow, CripplingThrow })
            await RefusedAsync(caster, id, Request(id, farDummyHandle), AbilityRefusals.TargetOutOfRange);

        await MoveAsync(home);
        enemyHandle = await HandleAsync(caster, enemy);

        // 2. 280 Crippling Throw full natural lifecycle on QAEnemy (first health change protected, 15.75 s Root + 15 s Force Combat -40).
        int casterRpc = caster.World.RpcSnapshot().Count;
        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int friendRpc = friend.World.RpcSnapshot().Count;
        int logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(enemyHandle))}");
        caster.SendRpc(Request(CripplingThrow, enemyHandle));
        await EvidenceRetestWire.CastBarAsync(caster, CripplingThrow, 2);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, CripplingThrow, caster.Handle, enemy.Handle, 1, 0)),
            "280 hits QAEnemy for 1 Thrown damage");
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, CripplingThrow, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 1215, AbilityCooldowns.GroupTimer, 3);
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=280 paid=20", StringComparison.Ordinal),
            "280 pays 20 IS", HeadlessPlayer.DefaultTimeout);

        var rootAdd = await ObserveAsync(enemy, enemyRpc, f => StateRecord(f, CripplingThrow, TempMods.RootedAbility, 15.75f, false),
            "280 applies Rooted (99) for 15.75 s on QAEnemy");
        var fcAdd = await ObserveAsync(enemy, enemyRpc, f => StatRecord(f, CripplingThrow, 519, -40, 15f, false),
            "280 applies Force Combat (519 -40) for 15 s on QAEnemy");
        ManageBonusRecord.TryDecode(rootAdd.Bytes, out var rootRec);
        ManageBonusRecord.TryDecode(fcAdd.Bytes, out var fcRec);
        await enemy.WaitUntilAsync(() =>
            enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == fcRec! with { AddRemoveType = 1 }) &&
            enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == rootRec! with { AddRemoveType = 1 }),
            "280 removes 519 at ~15 s and Rooted 99 at ~15.75 s", TimeSpan.FromSeconds(20));
        Check.That(!friend.World.RpcSnapshot().Skip(friendRpc).Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rec) && rec!.SourceId == CripplingThrow),
            "280 does not affect QAFriend");

        // 3. 280 Root break on subsequent damage + 279 Deadly Throw (default ?deadlyproc both: 40 dmg + 4 stats 15 s + Dazed/Staggered 8 s).
        int breakRpc = enemy.World.RpcSnapshot().Count;
        caster.SendRpc(Request(CripplingThrow, enemyHandle));
        var root2Add = await ObserveAsync(enemy, breakRpc, f => StateRecord(f, CripplingThrow, TempMods.RootedAbility, 15.75f, false),
            "Second 280 re-applies Rooted (99) and survives its own 1-dmg hit");
        var fc2Add = await ObserveAsync(enemy, breakRpc, f => StatRecord(f, CripplingThrow, 519, -40, 15f, false),
            "Second 280 re-applies Force Combat (519 -40)");
        ManageBonusRecord.TryDecode(root2Add.Bytes, out var root2Rec);
        ManageBonusRecord.TryDecode(fc2Add.Bytes, out var fc2Rec);

        casterRpc = caster.World.RpcSnapshot().Count;
        enemyRpc = enemy.World.RpcSnapshot().Count;
        friendRpc = friend.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(DeadlyThrow, enemyHandle));
        await EvidenceRetestWire.CastBarAsync(caster, DeadlyThrow, 2);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, DeadlyThrow, caster.Handle, enemy.Handle, 40, 0)),
            "279 hits QAEnemy for 40 Thrown damage (base 32 + 28% influence)");
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, DeadlyThrow, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 1216, AbilityCooldowns.GroupTimer, 3);
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=279 paid=18", StringComparison.Ordinal),
            "279 pays 18 IS", HeadlessPlayer.DefaultTimeout);

        // 279's hit must break 280's Rooted + Force Combat IMMEDIATELY (well before 15 s).
        await enemy.WaitUntilAsync(() =>
            enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == root2Rec! with { AddRemoveType = 1 }) &&
            enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == fc2Rec! with { AddRemoveType = 1 }),
            "279 subsequent damage immediately breaks 280 Rooted and clears parent 280 (519 removed early)", TimeSpan.FromSeconds(4));

        // Verify all 6 source279 records on QAEnemy (4 numeric 15 s + Dazed 199 8 s + Staggered 201 8 s).
        var d279Records = new List<RpcRecord>
        {
            await ObserveAsync(enemy, enemyRpc, f => StatRecord(f, DeadlyThrow, 877, -85, 15f, false), "279 applies 877 -85 (15 s)"),
            await ObserveAsync(enemy, enemyRpc, f => StatRecord(f, DeadlyThrow, 519, -15, 15f, false), "279 applies 519 -15 (15 s)"),
            await ObserveAsync(enemy, enemyRpc, f => StatRecord(f, DeadlyThrow, 878, -72, 15f, false), "279 applies 878 -72 (15 s)"),
            await ObserveAsync(enemy, enemyRpc, f => StatRecord(f, DeadlyThrow, 115, -57, 15f, false), "279 applies 115 -57 (15 s)"),
            await ObserveAsync(enemy, enemyRpc, f => StateRecord(f, DeadlyThrow, TempMods.DazeAbility, 8f, false), "279 applies Dazed (199) 8 s"),
            await ObserveAsync(enemy, enemyRpc, f => StateRecord(f, DeadlyThrow, TempMods.StaggeredAbility, 8f, false), "279 applies Staggered (201) 8 s")
        };
        foreach (var added in d279Records)
        {
            ManageBonusRecord.TryDecode(added.Bytes, out var rec);
            await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(enemyRpc)
                .Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == rec! with { AddRemoveType = 1 }),
                $"279 removes target {rec!.TargetId} at expiry", TimeSpan.FromSeconds(18));
        }
        Check.That(!friend.World.RpcSnapshot().Skip(friendRpc).Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rec) && rec!.SourceId == DeadlyThrow),
            "279 does not affect QAFriend");

        // 4. 279 proc isolation + DealDamageIfOffBalance (Daze negative control -> 40 only; Off-Balance positive -> 40 + 42).
        await ChatAsync("?deadlyproc daze", "Deadly Throw QA proc draws: daze.");
        await ChatAsync("?cd daze 30", "Daze fixture applied 884/199 to QAEnemy");
        casterRpc = caster.World.RpcSnapshot().Count;
        enemyRpc = enemy.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(DeadlyThrow, enemyHandle));
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, DeadlyThrow, caster.Handle, enemy.Handle, 40, 0)),
            "279 (daze-only branch) lands 40 base damage");
        await ObserveAsync(enemy, enemyRpc, f => StateRecord(f, DeadlyThrow, TempMods.DazeAbility, 8f, false),
            "279 (daze-only branch) applies Dazed (199)");
        await Task.Delay(300);
        Check.That(!enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => StateRecord(f, DeadlyThrow, TempMods.StaggeredAbility, 8f, false)),
            "279 (daze-only branch) omits Staggered (201)");
        Check.That(!server.DebugLog()[logFrom..].Contains("[CONDITIONAL-THROW]", StringComparison.Ordinal),
            "Daze alone does not trigger 279 DealDamageIfOffBalance");

        await ObserveAsync(caster, casterRpc,
            f => BytePattern.Parse($"80 bd 01 17 01 {AbilityCooldowns.OwnTimer:x2} 00 00 20 41 00").IsMatch(f.Bytes),
            "279 10 s own reuse finishes before Off-Balance cast");

        await ChatAsync("?deadlyproc stagger", "Deadly Throw QA proc draws: stagger.");
        await ChatAsync("?cd offbalance 30", "OffBalance fixture applied 885/200 to QAEnemy");
        enemyRpc = enemy.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(DeadlyThrow, enemyHandle));
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, DeadlyThrow, caster.Handle, enemy.Handle, 40, 0)),
            "279 (offbalance + stagger branch) lands 40 base damage");
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, DeadlyThrow, caster.Handle, enemy.Handle, 42, 0)),
            "279 (offbalance branch) lands separate 42 DealDamageIfOffBalance bonus hit");
        await ObserveAsync(enemy, enemyRpc, f => StateRecord(f, DeadlyThrow, TempMods.StaggeredAbility, 8f, false),
            "279 (stagger-only branch) applies Staggered (201)");
        await Task.Delay(300);
        Check.That(!enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => StateRecord(f, DeadlyThrow, TempMods.DazeAbility, 8f, false)),
            "279 (stagger-only branch) omits Dazed (199)");

        await ChatAsync("?deadlyproc both", "Deadly Throw QA proc draws: both.");

        // 5. Blocked sight refusal across solid office wall for BOTH 279 and 280.
        await MoveAsync(wall);
        uint otherHandle = await HandleAsync(caster, other);
        foreach (ushort id in new[] { DeadlyThrow, CripplingThrow })
            await RefusedAsync(caster, id, Request(id, otherHandle), AbilityRefusals.TargetNotInSight);

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count + other.World.Problems.Count,
            "Clean stock framing across all four sessions");
        Console.WriteLine("DEADLY + CRIPPLING THROW PASS: refusals, 280 root/protect/break, 279 stats/procs/offbalance all verified.");
        return Array.Empty<byte>();

        async Task MoveAsync(Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }

        async Task ChatAsync(string command, string expect)
        {
            caster.SkipReceived();
            caster.SendChat(command);
            await caster.WaitForTextAsync(expect);
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

    private static Teleport Stand(int x, int y, int z, bool indoors, byte facing)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == indoors && stand.floorY + 95 == y,
            $"Verified throw stand ({x},{y},{z})");
        return new($"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}", $"Teleported to {stand.sector:x}/{stand.region:x}");
    }

    private static void PrintStand(string name, Teleport stand)
    {
        string[] words = stand.Command.Split(' ');
        Console.WriteLine($"READY {name} type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
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
        Math.Abs(r.Duration - seconds) < 0.01 && r.AddRemoveType == (remove ? 1 : 0);

    private static bool StatRecord(RpcRecord frame, ushort source, ushort stat, int value, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindStat &&
        r.SourceId == source && r.TargetId == stat && r.SignedValue == value &&
        Math.Abs(r.Duration - seconds) < 0.01 && r.AddRemoveType == (remove ? 1 : 0);
}
