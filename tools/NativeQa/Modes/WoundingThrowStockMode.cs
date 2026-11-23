using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class WoundingThrowStockMode : NativeQaMode
{
    private const ushort Throw = CombatSpecials.WoundingThrow, Style = CombatStyles.SpyStyle;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, FarZ = 8401;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, 151, 134, 146, 152, Throw };
    private static readonly ushort[] FullLoadout = Loadout.Concat(Loadout.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-wounding-throw-stock";

    public override NativeQaModePlan Plan { get; } = new(FullLoadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        "UPDATE characters SET level=50,x=17150,y=495,z=6400,alignment=1 WHERE charId=6111",
        "UPDATE characters SET level=50,x=29150,y=895,z=12700,alignment=2 WHERE charId=6113",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=0,healthC=3000,healthM=3000,innerStrC=700,innerStrM=1000 WHERE charId=110",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { Style, Throw }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        readyMessages: new[]
        {
            "Page 2: 1=Spy Style, 2=Wounding Throw, plus Withdraw. CSR starts at READY HOME facing the level-50 30,000 HP Melee Dummy 3 m north. No teleport needed for the first cast. If it is behind the camera, use READY HOME, strafe once and click the visible dummy.",
            "Select Spy Style and try slot 2 with selection cleared and with /target QAFriend. Then use READY FAR, turn south if needed, click the same dummy at 17.01 m and press slot 2. Expect client suppression or invalid target/range refusal; no cost, effect or timer.",
            "Use READY WALL and /target QAOther at 5 m across the solid office wall. Expect client suppression or sight refusal and no debit or timer.",
            "Return to READY HOME, strafe, select the dummy and press slot 2 while at rest. Keep the first complete stock 80 b9 request. Expect 2 s, 20 IS, 10 s own / 3 s family 1215, zero initial damage, then 8 ThrownDOT about every 5 s for the 50 s lease. Record throw presentation at launch and during the effect.",
            "After 50 s use ?cd fixture-reset-thrown-resisted. Strafe, reselect the fresh dummy and Spy Style, and cast at rest. Expect type7 total75 raw8 final6 on ticks. The two stages should apply only to the selected dummy.",
            "After another reset, try slot 2 in Close Combat after reselecting Spy Style. Record whether the stock cast and its effect are shown and whether kind-6 scheduling or a named exchange is visible. Record that observation separately from thrown damage. The shipped flag declares a successful interlock move but no move ID has been established."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "281", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = FullLoadout.Sum(AbilityMemory.Cost) });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Wounding Throw requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {Stand(X)}");
        Console.WriteLine($"READY FAR {Stand(X, Y, FarZ, facing: 128)}");
        Console.WriteLine($"READY WALL {Stand(WallX, WallY, WallZ, indoors: true, facing: 64)}");
        Stand(AcrossWallX, WallY, WallZ, indoors: true);
        Stand(17150);
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Solid office wall blocks Wounding Throw");
        Stand(X, Y, DummyZ);
        Check.That(NavMesh.HasSightPath(1, X, Y, FarZ, X, Y, DummyZ), "Far stand tests range with clear sight");
        Check.That(FullLoadout.Sum(AbilityMemory.Cost) == 44 && FullLoadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            "Wounding Throw loadout is legal and retains the tested +29 Thrown influence");
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        foreach (string handle in new[] { "QAFriend", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);

        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Style, 1034, 29, 0, false), "Spy Style ready");
        // No archived 281 activation frame was identified. The target activation is reserved for stock QA.
        return Array.Empty<byte>();
    }

    private static string Stand(int x, int y = Y, int z = Z, bool indoors = false, byte facing = 0)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == indoors && stand.floorY + 95 == y, $"Verified Wounding Throw stand ({x},{y},{z})");
        return $"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}";
    }
}
