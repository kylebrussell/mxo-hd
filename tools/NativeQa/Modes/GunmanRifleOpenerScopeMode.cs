using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;

namespace NativeQa.Modes;

public sealed class GunmanRifleOpenerScopeMode : NativeQaMode
{
    private const ushort Rifles = 147, Handguns = 129;
    private const int PickX = 21550, TargetX = 21750, AllyX = 21350, BlockedX = 22350, Y = 1095, Z = 16150;
    private const string StockRifles = "80b993000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockHandguns = "80b981000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    // PR328 stock 4242512d1e6c and PR389 d3b7112643d9.
    private const string Immobilizing = "80b9fe00060004000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string Disabling = "80b9ff00070005000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string Hindering = "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-gunman-rifle-opener-scope";
    public override NativeQaModePlan Plan { get; } = new(Profile(2, 17, 130, 254, 255, Rifles, Handguns).Order().ToArray(),
        PickX, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y={Y},rotation=64,healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=0 WHERE charId IN (110,6113,6114)",
            $"UPDATE characters SET x={TargetX},z={Z} WHERE charId=6114",
            $"UPDATE characters SET x={AllyX},z={Z} WHERE charId=6113"
        }, DevCommands: true, hotbarAbilities: new ushort[] { Rifles, 254, 255, Handguns, 130 }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            [CombatFixtureWeapons.WeaponMode] = "1", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "254,255,130", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Perform all openers at rest. Use ?combatdepth fixture-weapon rifle, then page 2 slot 1 Rifles. Try slots 2 and 3 with empty selection, then with friendly QAOther. If a request is sent, each should refuse before IS, cast bar or reuse. Enter submits chat. Keep the selected target without Escape.",
            $"CSR ({PickX},{Y},{Z}) faces QAEnemy ({TargetX},{Y},{Z}). Select QAEnemy, then use the printed BLOCK and FAR commands, trying slots 2/3 at each. " +
                "Expect a local geometry refusal or reasons 9/34, with no cost, cast, or reuse.",
            "Use the printed EDGE command, keep or reselect QAEnemy and use slots 2/3 at exactly 20 m. Both cast in 2 s and have 10 s own reuse. " +
                "Neutral damage 22/19 with Rifle influence, from bases 18/16. Observe any naturally landed conditional state without treating a missed roll as failure.",
            "Return via the printed PICK command. After shared reuse clears, use ?combatdepth fixture-weapon handgun then slot 4 Handguns. Keep or reselect QAEnemy. Slot 5 Hindering Shot is the outside-combat control, with 2 s cast and 8 s own reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var pick = Stand(PickX, Z);
        var blocked = Stand(BlockedX, Z);
        var (edge, far) = FindTwentyStands();
        _ = Stand(TargetX, Z);
        _ = Stand(AllyX, Z);
        Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, TargetX, Y, Z) &&
            !NavMesh.HasSightPath(1, BlockedX, Y, Z, TargetX, Y, Z), "Clear pick and blocked stand within 20 m");
        Console.WriteLine($"PICK {pick.Command}");
        Console.WriteLine($"BLOCK {blocked.Command}");
        Console.WriteLine($"EDGE {edge.Command} (20.00 m, clear sight)");
        Console.WriteLine($"FAR {far.Command} (20.01 m, clear sight)");

        foreach (string name in new[] { "QAEnemy", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var db = context.OwnedDatabase!;
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, db, "rifle", CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StockRifles);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 93 00 00 00");
        var hostile = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAOther");
        uint target = Handle(hostile);

        foreach (var (id, request) in new[] { ((ushort)254, Immobilizing), ((ushort)255, Disabling) })
        {
            await RefusedAsync(actor, id, WithHandle(request, 0));
            await RefusedAsync(actor, id, WithHandle(request, Handle(ally)));
            await RefusedAsync(actor, id, WithHandle(request, target ^ 0x10000));
        }
        await MoveAsync(blocked);
        await RefusedAsync(actor, 254, WithHandle(Immobilizing, target), AbilityRefusals.TargetNotInSight);
        await RefusedAsync(actor, 255, WithHandle(Disabling, target), AbilityRefusals.TargetNotInSight);
        await MoveAsync(far);
        await RefusedAsync(actor, 254, WithHandle(Immobilizing, target), AbilityRefusals.TargetOutOfRange);
        await RefusedAsync(actor, 255, WithHandle(Disabling, target), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(edge);
        await LandAsync(actor, context.Helpers["QAEnemy"], hostile, 254, Immobilizing, 22, 10);
        await LandAsync(actor, context.Helpers["QAEnemy"], hostile, 255, Disabling, 19, 10);
        await MoveAsync(pick);
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, db, "handgun", CombatFixtureWeapons.Handgun);
        actor.SendRpc(StockHandguns);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 81 00 00 00");
        await LandAsync(actor, context.Helpers["QAEnemy"], hostile, 130, Hindering, 11, 8);
        Check.Equal(0, actor.World.Problems.Count, "Rifle opener framing");
        return Array.Empty<byte>();

        async Task MoveAsync(Teleport stand)
        {
            actor.SkipReceived();
            actor.SendChat(stand.Command);
            await actor.WaitForTextAsync(stand.Confirmation);
        }
    }

    private static (Teleport Edge, Teleport Outside) FindTwentyStands()
    {
        foreach (var (dx, dz) in new[] { (-1, 0), (0, -1), (0, 1), (1, 0) })
        {
            int edgeX = TargetX + dx * 2000, edgeZ = Z + dz * 2000;
            int farX = edgeX + dx, farZ = edgeZ + dz;
            if (ClearOutdoor(edgeX, edgeZ) && ClearOutdoor(farX, farZ))
                return (Stand(edgeX, edgeZ), Stand(farX, farZ));
        }
        throw new InvalidOperationException("Rifle openers need clear NAV stands at and just beyond 20 m.");

        bool ClearOutdoor(int x, int z) => NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors &&
            Math.Abs(floor.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, x, Y, z, TargetX, Y, Z);
    }

    private static async Task LandAsync(HeadlessPlayer actor, HeadlessPlayer enemy, ViewCreation target,
        ushort id, string request, int damage, int reuse)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        await StarterFixedDamageMode.Land(actor, enemy, target, id, damage, 2, reuse, request);
        if (AbilityCooldowns.TryGet(id, out var cooldown) && cooldown.Group != 0)
            await EvidenceRetestWire.TimerAsync(actor, cursor, cooldown.Group, AbilityCooldowns.GroupTimer, cooldown.GroupSeconds, ended: true);
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
