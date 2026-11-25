using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Wounding Throw (281, Shadow): selected-opponent refusals, at-rest ThrownDOT landing on a live player,
/// the interlock route that queues the completed 2 s cast into ILDB move 290 (Knife Throw SR), and +75 Thrown resistance.
/// </summary>
public sealed class WoundingThrowInterlockMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle, WoundingThrow = CombatSpecials.WoundingThrow;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, FarZ = 8401;
    private const int EnemyX = 16800, FriendX = 17150;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, WoundingThrow);

    public override string Name => "ability-parity-wounding-throw-interlock";

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
        hotbarAbilities: new[] { Spy, WoundingThrow }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [CombatFixtureWeapons.MartialFixedDamageMode] = "spy-throws",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "281",
            ["NativeCloseableRows"] = 1,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (+29% Thrown influence from Spy Style). CSR starts at verified outdoor HOME ({X},{Y},{Z}) facing Melee Dummy 3 m north ({X},{Y},{DummyZ}); QAEnemy 2 m west ({EnemyX},{Y},{Z}), QAFriend 1.5 m east ({FriendX},{Y},{Z}). No initial tp. Page 2: 1=Spy Style (1112), 2=Wounding Throw (281), 3=Withdraw.",
            "Step 1 (Cast first, at rest, Spy Style on): Select slot 1 (Spy Style), /target QAEnemy, press slot 2 (281). Expect 2.0 s cast, 20 IS, 10 s own / 3 s group 1215 reuse, no direct hit, no interlock, and source=281 ThrownDOT (50 s, 5 s interval, ticks of 8).",
            "Step 2 (Refusals — no IS, cast or timer): After 10 s reuse, clear selection and press 2 -> 24 or local gate; /target QAFriend -> 24; printed WALL chunks + /target QAOther -> 9; printed FAR chunks + click dummy (17.01 m) -> 34; return with printed HOME chunks.",
            "Step 3 (NEW interlock choreography on Melee Dummy): Click the dummy, open Close Combat, reselect slot 1 (Spy Style), type '?cd ma 281' (reply 'Martial starter fixture armed 281.'), press slot 2. Ordinary rounds continue during the 2.0 s cast, then '[COMBAT] ... is the next move', '[MARTIAL STARTER QA] one exchange win 281', '[COMBAT-VIEW] ... animation 290 (Knife Throw SR)', and 281 ThrownDOT ticks of 8 on the dummy. Type '?cd stop' to leave combat.",
            "Step 4 (+75 Thrown resistance): Type printed RESISTED chunks (?cd fixture-reset-thrown-resisted), strafe/select fresh dummy, press slot 2 -> '[TYPED-RESISTANCE OURS] 281 type7 total75 raw8 final6' on DOT ticks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Wounding Throw mode requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var home = Stand(X, Y, Z, indoors: false, facing: 0);
        var far = Stand(X, Y, FarZ, indoors: false, facing: 128);
        var wall = Stand(WallX, WallY, WallZ, indoors: true, facing: 64);
        PrintStand("HOME", home.Command);
        PrintStand("FAR", far.Command);
        PrintStand("WALL", wall.Command);
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME clear sight to Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "HOME clear sight to QAEnemy");
        Check.That(NavMesh.HasSightPath(1, X, Y, FarZ, X, Y, DummyZ), "FAR clear sight beyond 16 m");
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Solid wall blocks throw sight");
        Check.Equal(290u, CombatAnimations.Special(WoundingThrow, true, CombatTactic.Speed)?.Id, "281 maps to ILDB 290 Knife Throw SR");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Loadout fits level-50 memory");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        var helpers = new Dictionary<string, HeadlessPlayer>();
        foreach (string handle in new[] { "QAEnemy", "QAFriend", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            helpers[handle] = helper;
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        await caster.WaitForViewNamedAsync("QAFriend");

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await StockAbilityProfile.ObserveAsync(caster, 0, frame => StockAbilityProfile.Bonus(frame, Spy, 1034, 29, 0, false),
            "Spy Style activates +29 Thrown influence");

        // Step 1: at-rest cast on QAEnemy.
        int log = server.DebugLog().Length, rpc = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(WoundingThrow, await HandleAsync("QAEnemy")));
        await EvidenceRetestWire.CastBarAsync(caster, WoundingThrow, 2);
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log..].Contains("[ABILITY-COST] char=110 ability=281 paid=20", StringComparison.Ordinal) &&
            server.DebugLog()[log..].Contains("[EFFECTS] 281 on QAEnemy", StringComparison.Ordinal),
            "281 lands ThrownDOT on QAEnemy at rest", TimeSpan.FromSeconds(8));
        await EvidenceRetestWire.TimerAsync(caster, rpc, WoundingThrow, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(caster, rpc, 1215, AbilityCooldowns.GroupTimer, 3);
        Check.That(!caster.World.RpcSnapshot().Skip(rpc).Any(r => r.Bytes is [0x46, ..] && r.Bytes.Length == 6 && r.Bytes[5] == 1),
            "At-rest 281 does not open interlock");

        // Step 2: refusals after 10 s own reuse.
        await Task.Delay(TimeSpan.FromSeconds(10.25));
        await RefusedAsync(caster, WoundingThrow, Use(WoundingThrow, 0));
        await RefusedAsync(caster, WoundingThrow, Use(WoundingThrow, await HandleAsync("QAFriend")));
        await MoveAsync(wall);
        await RefusedAsync(caster, WoundingThrow, Use(WoundingThrow, await HandleAsync("QAOther")), AbilityRefusals.TargetNotInSight);
        int created = caster.World.CreationSnapshot().Count;
        await MoveAsync(far);
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(created).Any(v => v.Name == CombatFixtureLoadout.DummyName),
            "Fresh dummy view at FAR", HeadlessPlayer.DefaultTimeout);
        dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        await RefusedAsync(caster, WoundingThrow, Use(WoundingThrow, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(home);

        // Step 3: interlock choreography on Melee Dummy (move 290 Knife Throw SR).
        dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        await StarterInterlockDamageMode.Open(caster, dummy);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
        caster.SendChat("?cd ma 281");
        await caster.WaitForTextAsync("Martial starter fixture armed 281.");
        log = server.DebugLog().Length;
        caster.SendRpc(Use(WoundingThrow, dummyHandle));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log..].Contains("[MARTIAL STARTER QA] one exchange win 281", StringComparison.Ordinal) &&
            server.DebugLog()[log..].Contains("animation 290 (Knife Throw SR)", StringComparison.Ordinal) &&
            server.DebugLog()[log..].Contains($"[ABILITY] 281 lands on view {dummy.ViewId}", StringComparison.Ordinal),
            "Interlocked 281 queues after 2 s cast and plays animation 290 (Knife Throw SR)", TimeSpan.FromSeconds(15));
        Check.That(server.DebugLog()[log..].Contains("is the next move", StringComparison.Ordinal), "281 becomes the next move");
        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended.");

        // Step 4: +75 Thrown resistance on DOT tick (8 -> 6).
        await Task.Delay(TimeSpan.FromSeconds(10.25));
        int at = caster.World.CreationSnapshot().Count;
        caster.SendChat("?combatdepth fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(at).Any(v => v.Name == CombatFixtureLoadout.DummyName),
            "Resisted dummy spawns", HeadlessPlayer.DefaultTimeout);
        dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        log = server.DebugLog().Length;
        caster.SendRpc(Use(WoundingThrow, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log..].Contains("[TYPED-RESISTANCE OURS] 281 type7 total75 raw8 final6", StringComparison.Ordinal),
            "+75 Thrown resistance reduces 281 DOT tick from 8 to 6", TimeSpan.FromSeconds(12));

        Check.Equal(0, caster.World.Problems.Count + helpers["QAEnemy"].World.Problems.Count, "Clean parser state");
        Console.WriteLine("WOUNDING THROW INTERLOCK PASS: at-rest DOT, refusals, move 290 (Knife Throw SR), and +75 Thrown resistance verified.");
        return Array.Empty<byte>();

        async Task<uint> HandleAsync(string name)
        {
            var view = await caster.WaitForViewNamedAsync(name);
            return DuelPackets.Handle(view.ViewId, view.SpawnId);
        }

        async Task MoveAsync(Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }
    }

    private static byte[] Use(ushort ability, uint handle) =>
        new AbilityUseRecord(ability, handle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();

    private sealed record Teleport(string Command, string Confirmation);

    private static Teleport Stand(int x, int y, int z, bool indoors, byte facing)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == indoors && stand.floorY + 95 == y,
            $"Verified Wounding Throw stand ({x},{y},{z})");
        return new($"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}",
            $"Teleported to {stand.sector:x}/{stand.region:x}");
    }

    private static void PrintStand(string label, string command)
    {
        string[] words = command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
    }
}
