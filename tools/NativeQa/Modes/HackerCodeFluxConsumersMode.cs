using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerCodeFluxConsumersMode : NativeQaMode
{
    private const int Level = 50, SneakRank = 45, X = 17000, Y = 495, Z = 6400, TargetZ = 6600, RunnerStart = 8401, RunnerNear = 7000;
    private const ushort Style = CombatStyles.HackerStyle, Flux = 478, Countermeasures = 293, Evade = 217;
    // Retained from the stock Evade entry fixture.
    private const string StockEvade = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loadout = Closure(2, 17, Style, Flux);
    private static readonly ushort[] ObserverLoadout = Closure(2, 17, Evade, Countermeasures, 298);
    private static readonly ushort[] RunnerLoadout = Closure(2, 17, 151);
    private static readonly (string Handle, uint Id, int X, int Z, int Faction, int Yaw)[] Stands =
    {
        ("QAEnemy", 6114, X, TargetZ, 2, 0), ("QAFar", 6112, X, RunnerStart, 1, 128),
        ("QAFriend", 6111, X + 150, Z, 1, 0)
    };

    public override string Name => "ability-parity-hacker-code-flux-consumers";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=3000,healthM=3000,innerStrC=900,innerStrM=1500 WHERE charId IN (110,6111,6112,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,innerStrC=500 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
        "DELETE FROM inventory WHERE charId=110 AND slot=107",
        Seed(6114, ObserverLoadout, Level),
        Seed(6112, RunnerLoadout, SneakRank)
    }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Faction},rotation={stand.Yaw} WHERE charId={stand.Id}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { Style, Flux }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "478", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = 52 },
        readyMessages: new[]
        {
            "Level 50, 52/100. Outdoor NAV CSR (17000,495,6400) faces QAEnemy (17000,495,6600) at 2 m. QAEnemy faces north toward the sneaking QAFar on the (17000,495,8401) to (17000,495,7000) route. QAFriend stands at (17150,495,6400).",
            "Select page 2 Hacker Style and keep Speed. Wait for the baseline observer line [STEALTH] QAEnemy sees QAFar (SA 108 SL 45). QAEnemy's Evade shield is already active. Do one right-click Close Combat on QAEnemy. Expect [COMBAT-INSIGHT OURS] force80 evade110 allowedFalse and the shield refusal. Stay at rest.",
            "Reselect Hacker Style in case Close Combat changed it. Press slot 2 Code Flux on /target QAFriend first. Expect reason 24 if sent, with no debit or reuse. Select /target QAEnemy, press slot 2 and save the first full stock 478 request. Expect 2 s cast, 42 IS (500->458), 30 s own reuse and no damage. QAEnemy gets [MODIFIER] 204 -100 and 520 -100 for 60 s. An independent 10% roll may add Blind state 90 for 8 s.",
            "Wait for the automatic second runner pass. The same QAEnemy now logs a missed detection at SA 8 and SL 45. Repeat the same Close Combat once while 478 and the Evade shield are active. Expect force80 evade10 allowedTrue and an actual interlock. Stay in the fight until the two -100 records are removed at 60 s, then end the fixture."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Code Flux needs repository NAV data");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)).Append((X, RunnerNear)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                (x == X && z == TargetZ || NavMesh.HasSightPath(1, X, Y, TargetZ, x, Y, z)), $"Code Flux outdoor NAV and sight ({x},{Y},{z})");

        var server = context.ActualServer ?? throw new InvalidOperationException("Missing Code Flux fixture server");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var observer = context.Helpers["QAEnemy"];
        var runner = context.Helpers["QAFar"];
        CheckArchive(observer, ObserverLoadout, Level);
        CheckArchive(runner, RunnerLoadout, SneakRank);
        int shieldCursor = observer.World.RpcSnapshot().Count;
        observer.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(observer, shieldCursor,
            packet => StockAbilityProfile.Bonus(packet, Evade, EvasionShield.StateAbility, 0, 0, false), "Ordinary helper Evade shield");

        if (context.StockProbe is not { } actor)
        {
            _ = ObserveNativeAsync(observer, runner, server);
            return Array.Empty<byte>();
        }

        CheckArchive(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(observer.Handle);
        await RunObserverAsync(observer, runner, server, cursed: false);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Style, 172, 27, 0, false),
            "The exact Hacker Style control is active");
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
        int start = server.DebugLog().Length;
        actor.SendRpc($"40 {handle} 01 6b");
        await actor.WaitForRpcAsync($"4d 0e 00 01 00 {handle}");
        Check.That(server.DebugLog()[start..].Contains("force80 evade110 allowedFalse"), "Real pre-Flux melee shield refusal");
        Check.That(context.Helpers.Values.Append(actor).All(player => player.World.Problems.Count == 0), "Code Flux setup transport");
        return Array.Empty<byte>();
    }

    private static async Task ObserveNativeAsync(HeadlessPlayer observer, HeadlessPlayer runner, ServerInstance server)
    {
        try
        {
            while (!server.HasExited && !observer.World.CreationSnapshot().Any(view => view.Name == "CSR_Tester"))
                await Task.Delay(100);
            if (server.HasExited) return;
            await RunObserverAsync(observer, runner, server, cursed: false);
            Console.WriteLine("CODE FLUX OBSERVER PASS: SA 108 reveals QAFar");
            while (!server.HasExited && !observer.World.RpcSnapshot().Any(packet => StockAbilityProfile.Bonus(packet, Flux, 204, -100, 60, false)))
                await Task.Delay(100);
            if (server.HasExited) return;
            await RunObserverAsync(observer, runner, server, cursed: true);
            Console.WriteLine("CODE FLUX OBSERVER PASS: SA 8 misses QAFar");
        }
        catch (Exception error) { Console.Error.WriteLine($"CODE FLUX OBSERVER FAILED: {error.Message}"); }
    }

    private static async Task RunObserverAsync(HeadlessPlayer observer, HeadlessPlayer runner, ServerInstance server, bool cursed)
    {
        var lastCountermeasure = observer.World.RpcSnapshot().LastOrDefault(packet =>
            StockAbilityProfile.Bonus(packet, Countermeasures, 204, 80, 30, false) ||
            StockAbilityProfile.Bonus(packet, Countermeasures, 204, 80, 30, true));
        if (lastCountermeasure == null || StockAbilityProfile.Bonus(lastCountermeasure, Countermeasures, 204, 80, 30, true))
        {
            int cursor = observer.World.RpcSnapshot().Count;
            observer.SendRpc(StarterStealthObserverMode.StockCountermeasures);
            await StockAbilityProfile.ObserveAsync(observer, cursor,
                packet => StockAbilityProfile.Bonus(packet, Countermeasures, 204, 80, 30, false), "Observer's ordinary +80 Countermeasures");
        }

        runner.SendChat("?stealth off");
        await MoveAsync(runner, RunnerStart);
        int start = server.DebugLog().Length;
        int stealth = runner.World.RpcSnapshot().Count;
        runner.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await StockAbilityProfile.ObserveAsync(runner, stealth, packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(1111, true)),
            "The runner enters ordinary Sneak");
        await MoveAsync(runner, (RunnerStart + RunnerNear) / 2);
        await MoveAsync(runner, RunnerNear);
        string outcome = cursed ? "missed (SA 8 SL 45)" : "QAEnemy sees QAFar (SA 108 SL 45";
        await runner.WaitUntilAsync(() => server.DebugLog()[start..].Contains(outcome, StringComparison.Ordinal),
            "The same live observer reads the expected awareness", TimeSpan.FromSeconds(12));
        runner.SendChat("?stealth off");
        await MoveAsync(runner, (RunnerStart + RunnerNear) / 2);
        await MoveAsync(runner, RunnerStart);
    }

    private static async Task MoveAsync(HeadlessPlayer runner, int z)
    {
        byte[] xyz = new[] { (float)X, Y, z }.SelectMany(BitConverter.GetBytes).ToArray();
        runner.World.SendViewList(Convert.FromHexString("02000108").Concat(xyz).Concat(new byte[] { 0, 0 }).ToArray());
        runner.SkipReceived();
        runner.SendRpc("81 48");
        await runner.WaitForRpcAsync("81 49");
    }

    private static void CheckArchive(HeadlessPlayer player, ushort[] expected, int rank)
    {
        var archive = AbilityMemoryUiFixture.Decode(player.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.Keys.ToHashSet().SetEquals(expected) && expected.All(id => archive[id] == rank &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var level) && level >= need.Value)),
            $"{player.Handle} has every legal ranked prerequisite");
    }

    private static string Seed(uint id, ushort[] profile, int rank) =>
        "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
        string.Join(",", profile.Select((ability, i) => $"({id},{400 + i},{AbilityMessages.AbilityGameObject(ability)},{rank},1)"));

    private static ushort[] Closure(params ushort[] roots) => roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
        .Distinct().Order().ToArray();
}
