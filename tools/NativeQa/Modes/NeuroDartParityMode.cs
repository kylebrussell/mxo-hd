using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class NeuroDartParityMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle, NeuroDart = MasterKnifeThrowerBatch.NeuroDart;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, FarZ = 8401;
    private const int EnemyX = 16800, FriendX = 17150;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, NeuroDart);

    public override string Name => "ability-parity-neuro-dart-parity";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=0,alignment=1,healthC=5000,healthM=5000,innerStrC=900,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},rotation=192,alignment=1,healthC=5000,healthM=5000 WHERE charId=6111",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={AcrossWallX},y={WallY},z={WallZ},rotation=192,alignment=2,healthC=5000,healthM=5000 WHERE charId=6113",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6113,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6113,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { Spy, NeuroDart }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "141",
            ["NativeCloseableRows"] = 1,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, Spy Style +39% Thrown influence (+29 Spy Style + +10 passive closure). CSR starts at verified outdoor HOME ({X},{Y},{Z}) facing Melee Dummy 3 m north ({X},{Y},{DummyZ}), with QAEnemy 2 m west ({EnemyX},{Y},{Z}) and QAFriend 1.5 m east ({FriendX},{Y},{Z}). Page 2: 1=Spy Style (1112), 2=Neuro-Dart (141), 3=Withdraw. No initial tp needed.",
            "Step 1 (Targeting refusals — no IS debit, cast or timer): Select slot 1 (Spy Style), stay out of combat. Try slot 2 with selection cleared and with /target QAFriend -> InvalidTarget (24) or stock suppression. Type printed WALL chunks and /target QAOther (5 m across solid wall) -> TargetNotInSight (9) or suppression. Type printed FAR chunks, turn south and select Melee Dummy (17.01 m > 16 m) -> TargetOutOfRange (34) or suppression.",
            "Step 2 (Live player recipient QAEnemy — 9 records, 5 ticks, paired removals): Return to HOME, /target QAEnemy, press slot 2 at rest. Save the first full stock 141 80 b9 request. Expect 2.0 s cast, 20 IS, 18 s own / 3 s group 1215 reuse, 0 direct hit, and all 9 source141 15 s records on QAEnemy: 115 -57, 877 -85, 878 -72, 519 -20, 522 -15, 524 -15, 1009 -13, 1010 -21, plus ThrownDOT total 366 (5 ticks of 61 at 2.5 s cadence). Confirm paired removal of all 9 records at 15 s, no late tick, and no effect on QAFriend.",
            "Step 3 (Cumulative NPC ticks & +75 Thrown resistance): At HOME select Melee Dummy (3 m north; no player regen) and press slot 2 at rest -> 5 ticks of 61 (305 cumulative damage, 0 direct hit). After 15 s expiry type printed RESISTED chunks (?cd fixture-reset-thrown-resisted), strafe, select the fresh Melee Dummy and press slot 2 -> [TYPED-RESISTANCE OURS] 141 type7 total75 raw61 final50 for 5 ticks and clean expiry.",
            "Step 4 (In-combat refusal): Enter Close Combat on Melee Dummy, reselect Spy Style if needed, press slot 2 -> MustBeOutsideCombat (21) or stock suppression without debit/timer."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Neuro-Dart requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        PrintStand("HOME", Stand(X, Y, Z, indoors: false, facing: 0));
        PrintStand("FAR", Stand(X, Y, FarZ, indoors: false, facing: 128));
        PrintStand("WALL", Stand(WallX, WallY, WallZ, indoors: true, facing: 64));
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");

        _ = Stand(X, Y, DummyZ, indoors: false, facing: 128);
        _ = Stand(EnemyX, Y, Z, indoors: false, facing: 64);
        _ = Stand(FriendX, Y, Z, indoors: false, facing: 192);
        _ = Stand(AcrossWallX, WallY, WallZ, indoors: true, facing: 192);

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME has clear sight to Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "HOME has clear sight to QAEnemy");
        Check.That(NavMesh.HasSightPath(1, X, Y, FarZ, X, Y, DummyZ), "FAR has clear sight beyond 16 m range");
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Solid office wall blocks Neuro-Dart sight");

        var source = AbilityCatalog.Find(NeuroDart);
        Check.Equal(16d, source.Number("Ability/SelectionRange"), "Shipped Neuro-Dart range");
        Check.Equal(1d, source.Number("Ability/CastWhenUsed"), "Shipped Neuro-Dart is an at-rest opener");
        Check.Equal(2d, source.Number("Ability/CastingTime"), "Shipped Neuro-Dart cast time");
        Check.Equal((ushort)50, TypedAbilityResistance.Amount(61, Level, Level, 75), "Expected +75 Thrown resistance tick");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Neuro-Dart loadout fits level-50 memory");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        foreach (string handle in new[] { "QAEnemy", "QAFriend", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        await actor.WaitForViewNamedAsync("QAEnemy");
        await actor.WaitForViewNamedAsync("QAFriend");

        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Spy, 1034, 29, 0, false),
            "Retained Spy Style activates +29 Thrown influence for Neuro-Dart");
        // No archived stock 141 activation frame yet; first complete 80 b9 request is captured during stock QA.
        return Array.Empty<byte>();
    }

    private static string Stand(int x, int y, int z, bool indoors, byte facing)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == indoors && stand.floorY + 95 == y,
            $"Verified Neuro-Dart stand ({x},{y},{z})");
        return $"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}";
    }

    private static void PrintStand(string label, string command)
    {
        string[] words = command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
    }
}
