using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class WithdrawSiderollMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Home = 6400, Enemy = 6600;
    private static readonly ushort[] Loadout = { 2, 17 };

    public override string Name => "ability-parity-withdraw-sideroll";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Home, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Home},rotation=0 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={Enemy},rotation=128 WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES (6114,400,2147501056,50,1)"
    }, DevCommands: false, hotbarAbilities: new ushort[] { 17 }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, readyMessages: new[]
        {
            "Stay at verified HOME facing QAEnemy. No tp or NPC selection. /target QAEnemy, radial Close Combat, then double-click Power in Actions > Awakened.",
            """Immediately after a round arrives, press Withdraw on page 2 and send {"actor":"QAEnemy","action":"withdraw-pair"} before the next round. Helper submits ordinary 44 01. Two ordinary withdraw requests resolve without a roll; neither side takes round damage. If only one is registered, open a fresh fight and repeat.""",
            "Expect result 0x200, an authored Withdraw Sideroll at 23966 for a successful Power/Self Defense withdrawer, and normal disengage. Speed 23965 or Grab 23967 can apply if that actor owns the deciding round. Confirm the visible sidestep/roll versus the old dodge. No RNG aid."
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "ordinary-withdraw", ["NativeCloseableRows"] = 0 });

    internal static void RequestHelperWithdrawal(HeadlessPlayer actor)
    {
        if (actor.Handle != "QAEnemy") throw new InvalidOperationException("Use QAEnemy for paired withdrawal.");
        actor.SendRpc("44 01");
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Home, 0, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, Enemy, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Home, X, Y, Enemy), "Clear outdoor combat path");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        actor.SendRpc($"40 {hex} 01 6b");
        await actor.WaitForRpcAsync($"46 {hex} 01");
        await actor.WaitForRpcAsync("45");
        int log = server.DebugLog().Length;
        int at = actor.World.RpcSnapshot().Count;
        actor.SendRpc("44 01");
        RequestHelperWithdrawal(enemy);
        await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains("result 0x200", StringComparison.Ordinal) &&
            server.DebugLog()[log..].Contains("animation 23965 (Withdraw Sideroll SS)", StringComparison.Ordinal),
            "Successful ordinary withdrawal publishes an authored Sideroll", HeadlessPlayer.DefaultTimeout);
        await actor.WaitForRpcAsync($"46 {hex} 00");
        Check.That(actor.World.RpcSnapshot().Skip(at).Any(x => WithdrawalFixture.WithdrawalRound(x.Bytes)),
            "Normal 0x200 withdrawal round framing");
        foreach (var (tactic, id) in new[] { (CombatTactic.Speed, 23965u), (CombatTactic.Power, 23966u),
            (CombatTactic.Grab, 23967u), (CombatTactic.Block, 23965u) })
            Check.Equal(id, CombatAnimations.Choose(tactic, CombatTactic.Speed, CombatRules.Withdraw, 0).Id,
                "Authored tactic or safe authored fallback");
        return Array.Empty<byte>();
    }
}
