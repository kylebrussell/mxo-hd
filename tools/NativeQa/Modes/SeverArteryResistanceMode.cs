using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class SeverArteryResistanceMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Home = 6400, DummyZ = 6700;
    private const ushort Spy = CombatStyles.SpyStyle, Sever = CombatSpecials.SeverArtery;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Sever);

    public override string Name => "ability-parity-sever-artery-thrown-resistance";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Home, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Home},rotation=0 WHERE charId=110",
        "UPDATE char_abilities SET level=50 WHERE char_id=110"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sever }, testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", ["MXO_SEVER_STATE_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "341", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "At verified HOME with Melee Dummy 3 m ahead. No initial tp. If it is behind the camera, use the printed HOME, strafe once and click the visible dummy. /target does not select NPCs. Stay at rest in Spy Style (page 2 slot 1).",
            "Type ?severstate 99 0 to suppress only the random-state callback. Select Melee Dummy and use slot 2. Expect source341 stat1157 +104 for 30 s and normal no-direct-hit ThrownDOT ticks for 9. Unlike the player helper, the stationary dummy has no ordinary player health regeneration. Check HP steps accumulate until the last tick and none follow removal.",
            "After expiry type the printed short chunks for the resisted dummy, Enter once. Strafe and reselect the new view. Repeat slot 2. Expect the same parent, [TYPED-RESISTANCE OURS] type7 total75 raw9 final7, and smaller HP steps. Save the first full 341 request with current NPC view."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Home, 0, inside: false)}");
        Console.WriteLine($"READY DUMMY {InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 0, inside: false)}");
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");
        Check.That(NavMesh.HasSightPath(1, X, Y, Home, X, Y, DummyZ), "Clear outdoor ThrownDOT test path");
        Check.Equal((ushort)7, TypedAbilityResistance.Amount(9, 50, 50, 75), "Expected +75 Thrown resistance result");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, 50);
        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, target);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await StockAbilityProfile.ObserveAsync(actor, 0, rpc => StockAbilityProfile.Bonus(rpc, Spy, 1034, 29, 0, false),
            "Retained Spy Style for the neutral and resisted tests");
        return Array.Empty<byte>();
    }
}
