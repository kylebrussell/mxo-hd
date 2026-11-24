using System.Text.RegularExpressions;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class KungFuMasterTacticDamageMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, DummyZ = 6700, Level = 50;
    private const ushort KungFu = 133;
    private static readonly (ushort Id, int Base, int Neutral, int Armored, float Reuse)[] Samples =
    {
        (259, 71, 95, 78, 10),
        (260, 24, 32, 26, 10),
        (262, 108, 145, 119, 10)
    };
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, KungFu }.Concat(Samples.Select(row => row.Id))
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-kung-fu-master-tactic-damage";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
            "rotation=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "wounding-throw.csv", hotbarAbilities: new ushort[] { KungFu, 259, 260, 262 },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.KungFuTacticMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "259,260,262", ["NativeCloseableRows"] = Samples.Length,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            "CSR starts at verified HOME facing the isolated dummy 3 m north. No teleport required. If the dummy is behind the camera, strafe, turn, single-click it and use right-click Close Combat. Reselect Kung-Fu from page 2 slot 1.",
            "Double-click Speed in Actions > Awakened. Enter ?cd ma 259 then press slot 2, ?cd ma 260 then slot 3, and ?cd ma 262 then slot 4. Wait at least 5 s between specials for the family timers. Expect [COMBAT] Speed ... result 0x100 and damage 95, 32, 145. Then double-click Power and repeat with the same results, letting 10 s own reuse clear.",
            "Still in Power, enter ?cd ma75 once. Repeat each special with the same arm step, waiting out reuse. Expect [TYPED-RESISTANCE OURS] type0 total75 and damage 95->78, 32->26, 145->119. Keep the named motions and damage lines.",
            "QA commands arm one win for the specified move, supply Daze for 259 and Staggered for 262, and may set +75 physical resistance. Normal damage and effect rolls apply. Powerless or Stun need not proc for this damage comparison. No helper action or Escape."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY Melee Dummy {InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "Clear outdoor NAV lane");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        foreach (var sample in Samples)
        {
            var health = AbilityCatalog.Find(sample.Id)!.Modifiers.Single(m =>
                m.Target == 108 && m.Number("WhenEnabled", -1) == 2);
            Check.Equal(sample.Base, (int)health.Number("MinValue"), $"GOB lower endpoint {sample.Id}");
            Check.Equal(sample.Base, (int)health.Number("MaxValue"), $"GOB upper endpoint {sample.Id}");
        }

        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        actor.World.SendRpcs(WithHandle(MartialArtsStarterAdmissionMode.StockOpen), WireFormat.FromHex($"40 {hex} 01 6b"));
        await actor.WaitForRpcAsync($"40 {hex} 00 6b");
        await actor.WaitForRpcAsync("47");
        actor.SendRpc($"81 51 {hex}");
        actor.SendRpc(WithHandle(MartialArtsStarterAdmissionMode.StockKungFu));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 85 00 00 00");

        actor.SendRpc("42 00 00 00 00 00 00");
        await StageAsync("Speed", false);
        actor.SendRpc("42 01 00 00 00 00 00");
        await StageAsync("Power", false);
        actor.SendChat("?cd ma75");
        await actor.WaitForTextAsync("Martial starter dummy resistance +75.");
        await StageAsync("Power", true);
        Check.Equal(0, actor.World.Problems.Count, "All nine samples have valid framing");
        Console.WriteLine("KUNG FU TACTIC BOOTSTRAP PASS: Speed/Power neutral damage and typed resistance.");
        return Array.Empty<byte>();

        byte[] WithHandle(string frame) => StarterHackerControlTargetMode.WithHandle(frame, handle);

        async Task StageAsync(string tactic, bool armored)
        {
            foreach (var row in Samples)
            {
                actor.SkipReceived();
                actor.SendChat($"?cd ma {row.Id}");
                await actor.WaitForTextAsync($"Martial starter fixture armed {row.Id}.");
                int from = actor.World.RpcSnapshot().Count;
                int log = server.DebugLog().Length;
                actor.SendRpc(WithHandle(MartialInterlockAdmissionMode.StockRequest(row.Id)));
                await EvidenceRetestWire.TimerAsync(actor, from, row.Id, AbilityCooldowns.OwnTimer, row.Reuse);
                int amount = armored ? row.Armored : row.Neutral;
                await actor.WaitUntilAsync(() =>
                {
                    string tail = server.DebugLog()[log..];
                    return Regex.IsMatch(tail, $@"\[COMBAT\] Round \d+: {tactic} \(.*result 0x100, player {AbilityCatalog.Find(row.Id)!.Name} lands") &&
                        Regex.IsMatch(tail, $@"\[ABILITY\] {row.Id} lands on view {dummy.ViewId} for {amount}\b") &&
                        (!armored || tail.Contains($"[TYPED-RESISTANCE OURS] {row.Id} type0 total75 raw{row.Neutral} final{amount}", StringComparison.Ordinal));
                }, $"Landed {row.Id} for {amount} in {tactic}", HeadlessPlayer.DefaultTimeout);
                await EvidenceRetestWire.TimerAsync(actor, from, row.Id, AbilityCooldowns.OwnTimer, row.Reuse, ended: true);
            }
        }
    }
}
