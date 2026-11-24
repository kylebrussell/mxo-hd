using System.Text.RegularExpressions;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MasterKnifeThrowerFixedDamageMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6448, TargetZ = 6700, Level = 50;
    private const ushort Spy = 1112, Sneak = 1111, SneakParent = 151;
    private static readonly (ushort Id, int Base, int Native, int Armored, float Cast, float Reuse, string Prep, string Frame)[] Moves =
    {
        (340, 80, 123, 101, 1.5f, 8, "?cd stagger 30", "80b9540104000200000000000000000000000000000000000000000054010000006b0000086b0000000000"),
        (347, 57, 87, 71, 1.5f, 8, "?cd daze 30", "80b95b01090008000000000000000000000000000000000000000000585cb60001000000086b0000000000"),
        (386, 92, 1527, 1256, 0, 60, "", "80b982010c00100000000000000000000000000000000000000000000000000090767241086b0000000000")
    };
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(new ushort[] { 2, 17, Spy, SneakParent }
        .Concat(Moves.Select(move => move.Id)).ToArray()).Order().ToArray();

    public override string Name => "ability-parity-master-knife-thrower-fixed-damage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=0," +
            "healthC=5000,healthM=5000,innerStrC=2500,innerStrM=2500 WHERE charId=110",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "knife-fixed-damage.csv",
        hotbarAbilities: new[] { Spy, Sneak, (ushort)340, (ushort)347, (ushort)386 },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_KNIFE_FIXED_DAMAGE_QA"] = "1", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "340,347,386", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            "Page 2 Spy / Sneak / Throat Cutting Throw / Subduing Throw / Freedom to Joints. Start outside combat at HOME behind the dummy. Select Spy. If the dummy is covered by CSR, strafe briefly and click the fresh dummy. Verify HOME after every reset and return with the printed tp if displaced.",
            "Run ?cd k0 before each neutral sample. Select the fresh dummy. ?cd stagger 30 then slot 3, ?cd daze 30 then slot 4, and Sneak slot 2 then slot 5. Expect 123 / 87 / 1527. 340 and 347 have 1.5 s bars. 386 uses the Sneak opener (move 19600) and its 60 s timer.",
            "After own reuse repeat with ?cd k75 before each sample for +75 thrown resistance. Expect 101 / 71 / 1256. For 386 wait on the 60 s own timer. Keep the rear approach and select the fresh target each time.",
            "QA resets the dummy, preps the selected state, and adds armor for the comparison. No helper or roll override. Evidence is for fixed damage and its normal modifiers. Other targeting and repertoire checks are tracked independently."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY Melee Dummy {InvisibilityBeltWatcherMode.Warp(X, Y, TargetZ, 0, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, TargetZ), "Clear rear approach");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        var prior = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        var earlier = new Dictionary<ushort, int>();
        foreach (bool armored in new[] { false, true })
            foreach (var move in Moves)
            {
                var health = AbilityCatalog.Find(move.Id)!.Modifiers.Single(m => m.Target == 108 && m.Number("DamageType", -1) == 7);
                Check.Equal(move.Base, (int)health.Number("MinValue"), $"Fixed GOB min {move.Id}");
                Check.Equal(move.Base, (int)health.Number("MaxValue"), $"Fixed GOB max {move.Id}");
                if (earlier.TryGetValue(move.Id, out int last))
                {
                    string duration = WireFormat.ToHex(BitConverter.GetBytes(move.Reuse));
                    var end = BytePattern.Parse($"80 bd 01 {EvidenceRetestWire.Le(move.Id)} 04 {duration} 00");
                    await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(last).Any(f => end.IsMatch(f.Bytes)),
                        "Normal own timer expires", TimeSpan.FromSeconds(move.Reuse + 4));
                }
                var dummy = await ResetAsync(armored);
                uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
                actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
                int log = server.DebugLog().Length;
                if (move.Prep != "")
                {
                    actor.SendChat(move.Prep);
                    await actor.WaitForTextAsync("fixture applied");
                }
                else
                {
                    actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
                    await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
                        "Stock Sneak active at the rear stand", HeadlessPlayer.DefaultTimeout);
                }
                int rpc = actor.World.RpcSnapshot().Count;
                earlier[move.Id] = rpc;
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(move.Frame, handle));
                if (move.Cast > 0) await EvidenceRetestWire.CastBarAsync(actor, move.Id, move.Cast);
                await EvidenceRetestWire.TimerAsync(actor, rpc, move.Id, AbilityCooldowns.OwnTimer, move.Reuse);
                int damage = armored ? move.Armored : move.Native;
                await actor.WaitUntilAsync(() =>
                {
                    string tail = server.DebugLog()[log..];
                    return Regex.IsMatch(tail, $@"\[ABILITY\] {move.Id} lands on view {dummy.ViewId} for {damage}\b") &&
                        (!armored || tail.Contains($"[TYPED-RESISTANCE OURS] {move.Id} type7 total75 raw{move.Native} final{damage}", StringComparison.Ordinal));
                }, $"Fixed thrown hit {move.Id} lands for {damage}", HeadlessPlayer.DefaultTimeout);
            }
        Check.Equal(0, actor.World.Problems.Count, "Retained thrown frames decoded normally");
        Console.WriteLine("MASTER KNIFE FIXED DAMAGE BOOTSTRAP PASS: three exact stock throws, neutral and resisted.");
        return Array.Empty<byte>();

        async Task<ViewCreation> ResetAsync(bool armored)
        {
            int from = actor.World.CreationSnapshot().Count;
            actor.SkipReceived();
            actor.SendChat(armored ? "?cd k75" : "?cd k0");
            await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored");
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(from).Any(v =>
                v.Name == CombatFixtureLoadout.DummyName && v.ViewId != prior.ViewId && v.SpawnId != prior.SpawnId),
                "Fresh selected dummy for this throw", HeadlessPlayer.DefaultTimeout);
            prior = actor.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
            return prior;
        }
    }
}
