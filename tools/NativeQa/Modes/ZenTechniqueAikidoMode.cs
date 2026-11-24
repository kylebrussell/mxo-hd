using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class ZenTechniqueAikidoMode : NativeQaMode
{
    private const int Level = 99, Rank = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700;
    private const ushort Aikido = 101, Technique = 860, Reversal = 407;
    // Exact PR473 Punch Reversal cast.
    private const string StockReversal = "80b9970104000200000000000000000000000000000000000000000097010000006b0000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Aikido, Technique, Reversal);

    public override string Name => "ability-parity-zen-technique-aikido";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,level={Level},exp={PlayerProgression.ExperienceForLevel(50)},healthC=10000,healthM=10000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "wounding-throw.csv", hotbarAbilities: new ushort[] { Aikido, Reversal },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [QaLoadout.ZenTechniqueMode] = "1", [CombatFixtureWeapons.MartialFixedDamageMode] = "aikido-grandmaster"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "860", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Diagnostic level 99 qualifies for ordinary Technique 860 load, and Aikido abilities are learned at rank 50. No tp. Stay at HOME facing the Melee Dummy 3 m north. Strafe once if the dummy is hidden by the camera and click it.",
            "Right-click Close Combat, reselect page 2 slot 1 Aikido, and double-click Speed in Actions > Awakened. ?qaunload 860. Expect 572 +99 and 183 +198 removed. ?cd ma 407 then slot 2 for 28 damage, animation 827, 40 IS and 10 s reuse.",
            "After reuse, ?qaload 860 restores 572 +99 and 183 +198. Keep the same opponent and style. ?cd ma 407 then slot 2, expecting 49 damage with the same 827 and timers. QA arms the two identical wins only; skill load toggles use ordinary Archive records.",
            "This checks the typed Aikido consumer of 572. The original server's full ordinary-attack and viral-defense contours are tracked separately."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY Dummy {InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "Clear three-metre combat approach");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer!;
        DisablingShotMode.CheckProfile(caster, Loadout, Rank);
        await caster.EnterWorldAsync();
        Check.Equal((byte)Level, caster.World.CreationSnapshot().Last(v => v.ViewId == 2).Attributes["Level"][0], "Stored diagnostic level");
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        await StarterInterlockDamageMode.Open(caster, dummy);
        caster.SendRpc(WithHandle(MartialArtsStarterAdmissionMode.StockAikido, handle));
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 65 00 00 00");
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        foreach (bool unload in new[] { true, false })
        {
            int records = caster.World.RpcSnapshot().Count;
            caster.SendChat(unload ? "?qaunload 860" : "?qaload 860");
            await caster.WaitForRpcAsync(unload ? "80 b3 5c 03" : "80 b2 5c 03");
            foreach (var (stat, value) in new (ushort, int)[] { (572, 99), (183, 198) })
                await ObserveAsync(caster, records, frame => Bonus(frame, Technique, stat, value, 0, unload), $"Technique toggles {stat} {value:+#}");
            caster.SendChat("?cd ma 407");
            await caster.WaitForTextAsync("Martial starter fixture armed 407.");
            int from = caster.World.RpcSnapshot().Count, log = server.DebugLog().Length;
            caster.SendRpc(WithHandle(StockReversal, handle));
            await EvidenceRetestWire.TimerAsync(caster, from, Reversal, AbilityCooldowns.OwnTimer, 10);
            int damage = unload ? 28 : 49;
            await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains($"[ABILITY] 407 lands on view {dummy.ViewId} for {damage}", StringComparison.Ordinal),
                $"Technique toggle produces {damage} on the same ranked Aikido hit", HeadlessPlayer.DefaultTimeout);
            if (unload)
                await EvidenceRetestWire.TimerAsync(caster, from, Reversal, AbilityCooldowns.OwnTimer, 10, ended: true);
        }
        Check.Equal(0, caster.World.Problems.Count, "Clean retained stock framing");
        Console.WriteLine("ZEN TECHNIQUE AIKIDO PASS: same fixed strike 28 unloaded / 49 loaded.");
        return Array.Empty<byte>();
    }
}
