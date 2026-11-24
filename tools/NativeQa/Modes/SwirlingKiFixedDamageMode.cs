using System.Text.RegularExpressions;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SwirlingKiFixedDamageMode : NativeQaMode
{
    private const ushort Karate = 132, Swirling = 268;
    private const int X = 17000, Y = 495, Z = 6448, DummyZ = 6700, Level = 50;
    // PR580 first stock no-target cast, and the retained stock Karate selection.
    private const string StockSwirling = "80b90c010000000000000000000000000000000000000000000000000c010000006b0000086b0000000000";
    private const string StockKarate = "80b984000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Karate, Swirling).Order().ToArray();

    public override string Name => "ability-parity-swirling-ki-fixed-damage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
            "rotation=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "wounding-throw.csv", hotbarAbilities: new[] { Karate, Swirling },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_KI_AREA_QA"] = "1", [CombatFixtureWeapons.ResetMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "268", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            "Stay on READY HOME 2.52 m from Melee Dummy. No selected target, teleport or Close Combat. Page 2 slot 1 Karate, slot 2 Swirling Ki Summon.",
            "Double-click Speed in Actions > Awakened, then slot 2. Expect direct 2 s cast, 60 IS, own reuse 12 s and group 687 reuse 5 s. The dummy takes 2 and gets 522/524 -60 for 12 s. Switch to Power and repeat after reuse for the same 2.",
            "After reuse type ?cd a75. The disposable reset places CSR back at HOME, refreshes the dummy with +75 physical resistance, and leaves target selection clear. Reselect Karate and use slot 2. Expect type0 total75 raw2 final1 and the two accuracy debuffs.",
            "The only test aid is the isolated dummy reset and armor. No RNG override or helper actions."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY Melee Dummy {InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "Unobstructed outdoor NAV lane");
        Check.That(MartialArtsRemainingSpecials.TryResolve(AbilityCatalog.Find(Swirling), out ushort raw) && raw == 2,
            "The shipped type-0 area damage is fixed at two");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        await StyleAsync();
        actor.SendRpc("42 00 00 00 00 00 00");
        await CastAsync(dummy, 2, false);
        actor.SendRpc("42 01 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 f9 03 00 00 f4 03");
        await CastAsync(dummy, 2, false);

        int creation = actor.World.CreationSnapshot().Count;
        actor.SkipReceived();
        actor.SendChat("?cd a75");
        await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored with +75");
        await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(creation).Any(v => v.Name == dummy.Name),
            "Fresh armored dummy", HeadlessPlayer.DefaultTimeout);
        dummy = actor.World.CreationSnapshot().Last(v => v.Name == dummy.Name);
        await CastAsync(dummy, 1, true);

        Check.Equal(0, actor.World.Problems.Count, "Valid area cast and reset framing");
        Console.WriteLine("SWIRLING KI FIXED DAMAGE BOOTSTRAP PASS: 2/2/1 from the exact no-target stock request.");
        return Array.Empty<byte>();

        async Task StyleAsync()
        {
            actor.SendRpc(StockKarate);
            await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 84 00 00 00");
        }

        async Task CastAsync(ViewCreation target, int expected, bool resisted)
        {
            actor.SkipReceived();
            int rpc = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            actor.SendRpc(StockSwirling);
            await EvidenceRetestWire.CastBarAsync(actor, Swirling, 2);
            await EvidenceRetestWire.TimerAsync(actor, rpc, Swirling, AbilityCooldowns.OwnTimer, 12);
            await EvidenceRetestWire.TimerAsync(actor, rpc, 687, AbilityCooldowns.GroupTimer, 5);
            await actor.WaitUntilAsync(() =>
            {
                string recent = server.DebugLog()[log..];
                return Regex.IsMatch(recent, $@"\[ABILITY\] 268 lands on view {target.ViewId} for {expected}\b") &&
                    recent.Contains("Stat 522 -60 12 s", StringComparison.Ordinal) &&
                    recent.Contains("Stat 524 -60 12 s", StringComparison.Ordinal) &&
                    (!resisted || recent.Contains("[TYPED-RESISTANCE OURS] 268 type0 total75 raw2 final1", StringComparison.Ordinal));
            }, $"Area hit {expected} and both debuffs", HeadlessPlayer.DefaultTimeout);
            if (resisted) return;
            var end = BytePattern.Parse("80 bd 01 0c 01 04 00 00 40 41 00");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(rpc).Any(f => end.IsMatch(f.Bytes)),
                "12-second own reuse ends", TimeSpan.FromSeconds(16));
        }
    }
}
