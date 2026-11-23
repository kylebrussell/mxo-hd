using HeadlessClient;
using MxoHd.PacketResearch;
using hds;

namespace NativeQa.Modes;

public sealed class HackerReactiveCodeDeflectionMode : NativeQaMode
{
    private const ushort Parent = 941, Pulse = 942, Health = 1000;
    private const int Level = 50, X = 21925, Y = 1095, Z = 16100;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Parent, 943, 945).Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Org, string Decision)[] Stands =
    {
        ("QAEnemy", 6114, X + 300, 2, "included"),
        ("QAOther", 6113, X + 301, 2, "radius"),
        ("QAFar", 6112, X - 250, 2, "sight"),
        ("QAFriend", 6111, X + 100, 1, "pvp-NotOpponent")
    };
    // Complete stock parent frame from PR545, with a zero target.
    private const string StockReactiveCode = "80b9ad03000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000";

    public override string Name => "ability-parity-hacker-reactive-code-deflection";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},pvpflag=0,healthC={Health},healthM={Health},innerStrC=700,innerStrM=700 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X, Z, X + 300, Z)},alignment=1 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={Z},alignment={stand.Org} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Parent }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "0", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "32766",
            [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "942", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR starts at ({X},{Y},{Z}), facing QAEnemy exactly 3.00 m east. QAOther is 3.01 m east, QAFar 2.50 m west behind a floor-mesh gap and allied QAFriend 1 m east. Stay put with selection clear.",
            "Toggle Reactive Code on with page 2 slot 1. Enter ?health 900 once from 1000. Expect one [HEALTH-CALLBACK] 941, one [FREECAST-VIRAL] ability=942 target=QAEnemy hit=False qa=True, the deflection chat/effect and no damage or PvP round. [AREA-QA] should exclude QAOther for radius, QAFar for sight and QAFriend as pvp-NotOpponent. Only CSR changes HP.",
            "Toggle it off and enter ?health 850. No later 942 contest should run. PR545 covers the positive stock hits. The fixed deflection draw and manual self-HP change are fixture controls."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in Stands.Select(stand => stand.X).Append(X))
            Check.That(NavMesh.Locate(1, x, Y, Z, out var at) && !at.indoors && at.floorY + 95 == Y,
                $"Reactive Code stand ({x},{Y},{Z}) has outdoor NAV");
        foreach (var stand in Stands)
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, stand.X, Y, Z) == (stand.Decision != "sight"),
                $"Expected sight for {stand.Handle}");

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var helper in context.Helpers.Values) await actor.WaitForViewNamedAsync(helper.Handle);
        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockReactiveCode);
        await ObserveToggleAsync(start, true);
        int logStart = server.DebugLog().Length;
        var witnesses = context.Helpers.Values.Select(helper => (Helper: helper, View: helper.World.ViewSnapshot().Count)).ToArray();
        actor.SendChat("?health 900");
        var deflection = BytePattern.Parse($"81 67 ?? ?? ?? ?? f2 00 00 24 01 {EvidenceRetestWire.Le(Pulse)} 00 00");
        await StockAbilityProfile.ObserveAsync(actor, start, frame => deflection.IsMatch(frame.Bytes), "Reactive Code deflects to the caster");
        await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains("[FREECAST-VIRAL] ability=942 ", StringComparison.Ordinal),
            "The child contest is logged", HeadlessPlayer.DefaultTimeout);
        string[] trace = server.DebugLog()[logStart..].Split('\n');
        string contest = trace.Single(line => line.Contains("[FREECAST-VIRAL] ability=942 ", StringComparison.Ordinal));
        Check.That(contest.Contains("target=QAEnemy ") && contest.Contains("hit=False qa=True"), "Only the boundary hostile enters the contest");
        foreach (var stand in Stands)
            Check.That(trace.Any(line => line.Contains($"candidate={stand.Handle} ") && line.TrimEnd().EndsWith("reason=" + stand.Decision)),
                $"Expected area decision for {stand.Handle}");
        Check.That(witnesses.All(witness => PvpOpponentsFixture.HealthHistory(witness.Helper.World.ViewSnapshot(), witness.View, 2, self: true).Length == 0),
            "No recipient takes damage");
        var opponent = witnesses.Single(witness => witness.Helper.Handle == "QAEnemy");
        var deflectFx = BytePattern.Parse("9c 05 00 28");
        await opponent.Helper.WaitUntilAsync(() => opponent.Helper.World.ViewSnapshot().Skip(opponent.View).Any(deflectFx.Occurs),
            "The boundary hostile receives the deflection FX", HeadlessPlayer.DefaultTimeout);
        start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockReactiveCode);
        await ObserveToggleAsync(start, false);
        int afterStop = server.DebugLog().Length;
        int ownerViews = actor.World.ViewSnapshot().Count;
        actor.SendChat("?health 850");
        var ownerHealth = BytePattern.Parse($"02 00 02 80 80 80 40 {EvidenceRetestWire.Le(850)}");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(ownerViews).Any(ownerHealth.Occurs),
            "The owner health change is delivered after toggle-off", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[afterStop..].Contains("[FREECAST-VIRAL] ability=942 ", StringComparison.Ordinal),
            "Toggle-off removes the health callback");
        Console.WriteLine("REACTIVE CODE DEFLECTION PASS 942");
        return Array.Empty<byte>();

        Task ObserveToggleAsync(int cursor, bool on) => StockAbilityProfile.ObserveAsync(actor, cursor, frame =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Parent, on)), $"Reactive Code on={on}");
    }
}
