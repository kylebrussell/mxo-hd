using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerCrashMovementMode : NativeQaMode
{
    internal const ushort Crash = 396;
    private const ushort Style = CombatStyles.HackerStyle;
    private const int Level = 28, X = 17000, Y = 495, Z = 6400, EndZ = 8000, EnemyX = 17300;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    // proxy_026 lines 74107–74108. Rebind the live target handle only.
    private const string Cast = "80b98c010d00720000000000000000000000000000000000000000000000002040e83d41086b0000000000";
    private static readonly (ushort Stat, int Value, float Seconds)[] Stats =
    {
        (115, -50, 28), (877, -50, 28), (878, -50, 28), (519, -15, 28),
        (520, -50, 28), (342, -27, 42), (524, -27, 42)
    };
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, Crash }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-hacker-crash-movement";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},alignment=2 WHERE charId=6114",
            $"UPDATE characters SET x=17200,y={Y},z=6800,alignment=1 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", Loadout.Select((id, slot) => $"(6114,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Style, Crash }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "396", ["NativeCloseableRows"] = 1 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces the +Z outdoor NAV lane through z={EndZ}. QAEnemy ({EnemyX},{Y},{Z}) is 3 m east, ally QAFriend is at (17200,{Y},6800).",
            "Select Hacker Style (page 2 slot 1). Try Crash (slot 2) with no target and on QAFriend. If sent, reason 24 before cost, cast or reuse. Use /target QAEnemy, cast Crash and expect 25 IS, 4 s cast, 15 s own reuse, no HP damage or interlock round. Only QAEnemy gains the seven modifiers.",
            "[MODIFIER] source=396 stats 115/877/878 and 520 each -50, stat 519 -15 for 28 s. Stats 342/524 -27 for 42 s. The 28 s expiry restores MovementScale; the two accuracy records outlast it.",
            $"For stock movement, run forward for about 2 s from HOME ({Home}) on the same direction and hold time each sample. Return HOME before the active and expired samples. Save actual first and last position/time from the PacketLog.",
            """From HOME, submit {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":396}. After the records appear, repeat the run. Confirm scale -0.50, then repeat at scale 0 after the 28 s removal. Base run distance should halve during Crash. No Escape, Sprint or change to run/walk settings."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Crash movement needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, (EndZ - Z) / 100 + 1).Select(index => (X, Z + 100 * index));
        foreach (var (x, z) in lane.Append((EnemyX, Z)).Append((17200, 6800)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Verified Crash outdoor stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && home.sector == 0x4e2 && home.region == 0x12 && home.polygon == 4,
            "The printed Crash movement reset matches actual NAV");

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var enemy = context.Helpers["QAEnemy"];
        StarterHackerPassiveFixture.VerifyProfile(enemy, Loadout);
        enemy.SendRpc(StarterHackerViralSightMode.StockStyle);
        await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        if (context.StockProbe is not { } runner) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(runner, Loadout);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "The ranked Crash loadout fits level 28 memory");
        await PvpOpponentsFixture.EnterAsync(runner);
        NativeQaReadiness.RequireNoAmbientAggro(server, runner);
        var observer = context.Helpers["QAFriend"];
        var observerView = await observer.WaitForViewNamedAsync(runner.Handle);
        var allyView = await runner.WaitForViewNamedAsync(observer.Handle);
        uint ally = DuelPackets.Handle(allyView.ViewId, allyView.SpawnId);
        runner.SendRpc(StarterHackerViralSightMode.StockStyle);
        await runner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await StarterHackerControlTargetMode.RefusedAsync(runner, Crash, StarterHackerControlTargetMode.WithHandle(Cast, ally));

        int ownAt = runner.World.ViewSnapshot().Count, watchAt = observer.World.ViewSnapshot().Count;
        int enemyAt = enemy.World.RpcSnapshot().Count, costAt = server.DebugLog().Length;
        await SendSampleAsync(enemy, runner.Handle, Crash);
        await EvidenceRetestWire.CastBarAsync(enemy, Crash, 4);
        await EvidenceRetestWire.TimerAsync(enemy, enemyAt, Crash, AbilityCooldowns.OwnTimer, 15);
        await runner.WaitUntilAsync(() => server.DebugLog()[costAt..].Contains("[ABILITY-COST] char=6114 ability=396 paid=25 "),
            "The exact Crash cast pays 25 IS", HeadlessPlayer.DefaultTimeout);
        foreach (var (stat, value, duration) in Stats)
            await StockAbilityProfile.ObserveAsync(runner, 0, frame => StockAbilityProfile.Bonus(frame, Crash, stat, value, duration, false),
                $"Crash sends stat {stat} value {value} for {duration} seconds");
        await CheckMovementAsync(-.50f);
        ownAt = runner.World.ViewSnapshot().Count;
        watchAt = observer.World.ViewSnapshot().Count;
        await runner.WaitUntilAsync(() => runner.World.RpcSnapshot().Any(frame => StockAbilityProfile.Bonus(frame, Crash, 115, -50, 28, true)),
            "The movement record expires first", TimeSpan.FromSeconds(32));
        await CheckMovementAsync(0f);
        Check.That(!runner.World.RpcSnapshot().Any(frame => StockAbilityProfile.Bonus(frame, Crash, 342, -27, 42, true)),
            "Viral accuracy outlasts movement");
        await runner.WaitUntilAsync(() => runner.World.RpcSnapshot().Any(frame => StockAbilityProfile.Bonus(frame, Crash, 342, -27, 42, true)),
            "The independent accuracy records finish later", TimeSpan.FromSeconds(18));
        Check.Equal(0, runner.World.Problems.Count + enemy.World.Problems.Count + observer.World.Problems.Count, "Crash stock framing");
        return Array.Empty<byte>();

        async Task CheckMovementAsync(float scale)
        {
            await EvidenceRetestWire.ViewSinceAsync(runner, ownAt, 2,
                WireFormat.ToHex(PlayerViewAttributes.Build((PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(scale)))));
            await EvidenceRetestWire.ViewSinceAsync(observer, watchAt, observerView.ViewId,
                WireFormat.ToHex(PlayerViewAttributes.Build((PlayerViewAttributes.OtherMovementScale, PlayerViewAttributes.F32(scale)))));
        }
    }

    internal static async Task<object> SendSampleAsync(HeadlessPlayer helper, string target, ushort ability)
    {
        Check.That(helper.Handle == "QAEnemy" && target == "CSR_Tester" && ability == Crash,
            "The Crash movement sample is QAEnemy → CSR_Tester only");
        var view = await helper.WaitForViewNamedAsync(target);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        byte[] request = StarterHackerControlTargetMode.WithHandle(Cast, handle);
        helper.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        helper.SendRpc(request);
        return new { Ability = ability, Target = target, Request = Convert.ToHexString(request) };
    }
}
