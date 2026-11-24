using System.Text.RegularExpressions;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public abstract class MartialFixedDamageMode : NativeQaMode
{
    protected sealed record Move(ushort Id, int Base, int Neutral, int Resisted, float Reuse, int Animation);
    private const int X = 17000, Y = 495, Z = 6400, DummyZ = 6700, Level = 50;
    private readonly ushort style;
    private readonly string stockStyle;
    private readonly Move[] moves;
    private readonly ushort[] loadout;

    public override string Name { get; }
    public override NativeQaModePlan Plan { get; }

    protected MartialFixedDamageMode(string fixture, ushort style, string stockStyle, Move[] moves, string[] controls)
    {
        Name = $"ability-parity-{fixture}-fixed-damage";
        this.style = style;
        this.stockStyle = stockStyle;
        this.moves = moves;
        loadout = new ushort[] { 2, 17, style }.Concat(moves.Select(move => move.Id))
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
        Plan = new(loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "rotation=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: "wounding-throw.csv",
            hotbarAbilities: new[] { style }.Concat(moves.Select(move => move.Id)).ToArray(),
            environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.MartialFixedDamageMode] = fixture
            },
            extraQaMetadata: new()
            {
                ["ScopedStockRows"] = string.Join(",", moves.Select(move => move.Id)), ["NativeCloseableRows"] = moves.Length,
                ["LoadedMemory"] = loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
            }, readyMessages: new[]
            {
                "Stay at READY HOME facing the isolated 30,000 HP dummy 3 m north. If behind your camera, strafe and turn to single-click it. Right-click Close Combat and reselect the style (page 2 slot 1).",
                $"Double-click Speed in Actions > Awakened. Arm each cast with its command and use these page 2 slots: {string.Join("; ", moves.Select((m, i) => $"{i + 2} with ?cd ma {m.Id} ({m.Id})"))}. Wait 5 s family reuse between casts. Expect result 0x100 and damage {string.Join("/", moves.Select(m => m.Neutral))}.",
                "Double-click Power and repeat the same order after each move's own reuse. The damage stays the same.",
                $"Still in Power, enter ?cd ma75 once, then repeat that sequence. Expect [TYPED-RESISTANCE OURS] type0 total75 and damage {string.Join("/", moves.Select(m => m.Resisted))}. Named animations in slot order: {string.Join("/", moves.Select(m => m.Animation))}.",
                "QA arms one win for the stated move, may supply its required state and gives the final +75 resistance. Damage and secondary proc rolls follow the ordinary path. No helpers or Escape."
            }.Concat(controls).ToArray());
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY Melee Dummy {InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "Clear outdoor NAV lane");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, loadout, Level);
        foreach (var move in moves)
        {
            var health = AbilityCatalog.Find(move.Id)!.Modifiers.Single(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2);
            Check.Equal(move.Base, (int)health.Number("MinValue"), $"GOB lower endpoint {move.Id}");
            Check.Equal(move.Base, (int)health.Number("MaxValue"), $"GOB upper endpoint {move.Id}");
        }

        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        byte[] WithHandle(string frame) => StarterHackerControlTargetMode.WithHandle(frame, handle);
        actor.World.SendRpcs(WithHandle(MartialArtsStarterAdmissionMode.StockOpen), WireFormat.FromHex($"40 {hex} 01 6b"));
        await actor.WaitForRpcAsync($"40 {hex} 00 6b");
        await actor.WaitForRpcAsync("47");
        actor.SendRpc($"81 51 {hex}");
        actor.SendRpc(WithHandle(stockStyle));
        await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {EvidenceRetestWire.Le(style)} 00 00");

        var previous = new Dictionary<ushort, int>();
        actor.SendRpc("42 00 00 00 00 00 00");
        await StageAsync("Speed", false);
        actor.SendRpc("42 01 00 00 00 00 00");
        await StageAsync("Power", false);
        actor.SendChat("?cd ma75");
        await actor.WaitForTextAsync("Martial starter dummy resistance +75.");
        await StageAsync("Power", true);
        Check.Equal(0, actor.World.Problems.Count, "All fixed-GOB samples have valid framing");
        Console.WriteLine($"MARTIAL FIXED DAMAGE BOOTSTRAP PASS: {Name}, {moves.Length * 3} stock casts.");
        return Array.Empty<byte>();

        async Task StageAsync(string tactic, bool armored)
        {
            foreach (var move in moves)
            {
                if (previous.TryGetValue(move.Id, out var last))
                {
                    string timer = WireFormat.ToHex(BitConverter.GetBytes(move.Reuse));
                    var timeout = BytePattern.Parse($"80 bd 01 {EvidenceRetestWire.Le(move.Id)} 04 {timer} 00");
                    await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(last).Any(rpc => timeout.IsMatch(rpc.Bytes)),
                        $"Own reuse ended for {move.Id}", TimeSpan.FromSeconds(move.Reuse + 5));
                }

                actor.SendChat($"?cd ma {move.Id}");
                await actor.WaitForTextAsync($"Martial starter fixture armed {move.Id}.");
                int from = actor.World.RpcSnapshot().Count;
                previous[move.Id] = from;
                int log = server.DebugLog().Length;
                actor.SendRpc(WithHandle(MartialInterlockAdmissionMode.StockRequest(move.Id)));
                await EvidenceRetestWire.TimerAsync(actor, from, move.Id, AbilityCooldowns.OwnTimer, move.Reuse);
                int damage = armored ? move.Resisted : move.Neutral;
                await actor.WaitUntilAsync(() =>
                {
                    string tail = server.DebugLog()[log..];
                    var round = Regex.Match(tail, $@"\[COMBAT\] Round (\d+): {tactic} \(.*result 0x100, player {AbilityCatalog.Find(move.Id)!.Name} lands");
                    return round.Success &&
                        Regex.IsMatch(tail, $@"\[ABILITY\] {move.Id} lands on view {dummy.ViewId} for {damage}\b") &&
                        tail.Contains($"[COMBAT-VIEW] Round {round.Groups[1].Value}: animation {move.Animation} ", StringComparison.Ordinal) &&
                        (!armored || tail.Contains($"[TYPED-RESISTANCE OURS] {move.Id} type0 total75 raw{move.Neutral} final{damage}", StringComparison.Ordinal));
                }, $"Landed {move.Id} for {damage} in {tactic}", HeadlessPlayer.DefaultTimeout);
                // Sky-High Sidekick lowers physical resistance on landing for 12 seconds. It is last in each pass.
                await Task.Delay(TimeSpan.FromSeconds(move.Id == 267 ? 12.5 : 5.2));
            }
        }
    }
}
