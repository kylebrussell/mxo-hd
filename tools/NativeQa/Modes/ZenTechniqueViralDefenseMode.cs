using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class ZenTechniqueViralDefenseMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, EnemyZ = 6600, Level = 99, Rank = 50;
    private const ushort Technique = 860, StaticBlast = 33, Coder = CombatStyles.CoderStyle;
    private const string Power = "42 01 00 00 00 00 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Technique);
    private static readonly ushort[] VirusLoadout = DisablingShotMode.Closure(2, 17, StaticBlast, Coder);
    private static readonly Regex Ledger = new(@"\[FREECAST-VIRAL\] ability=33 source=QAEnemy target=CSR_Tester inputs=(\d+)/(\d+) rolls=\d+/\d+ hit=(True|False) qa=True", RegexOptions.CultureInvariant);

    public override string Name => "ability-parity-zen-technique-viral-defense";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,alignment=1,healthC=5000,healthM=5000 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=128,alignment=2,innerStrC=500,innerStrM=500 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId IN (110,6114)",
        $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", VirusLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Rank},1)"))
    }, DevCommands: true, hotbarAbilities: new ushort[] { 17 }, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
        ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", [QaLoadout.ZenTechniqueMode] = "1",
        ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "16384",
        ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "16384"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "860", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at verified HOME facing QAEnemy. No tp or selection required. Diagnostic L99, rank 50. Double-click Power in Actions > Awakened and remain out of close combat.",
            """Run ?qaunload 860. Expect [MODIFIER] source860 stat183 remove +198 and stat572 remove +99. Send {"actor":"QAEnemy","action":"zen-viral"}. Wait for its result (about 8 s). Expect [FREECAST-VIRAL] inputs=65/47 rolls=82/58 hit=True.""",
            """Run ?qaload 860 and repeat the same helper call. Expect inputs=65/245 rolls=82/157 hit=False. Run ?qaunload 860 and repeat once more for the original input. The helper sends the retained Static Blast cast and waits for reuse to end. Both draws are fixed to the same value on the disposable server."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, EnemyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, EnemyZ), "Clear two-metre viral cast path");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckProfile(enemy, VirusLoadout, Rank);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        enemy.SendRpc(StarterCoderSplashBatchMode.StockStyle);
        await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        enemy.SendRpc(Power);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Rank);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendRpc(Power);
        var measurements = new List<Probe>();
        foreach (bool unload in new[] { true, false, true })
        {
            int from = caster.World.RpcSnapshot().Count;
            caster.SendChat(unload ? "?qaunload 860" : "?qaload 860");
            await ObserveAsync(caster, from, r => Bonus(r, Technique, 183, 198, 0, unload), "Technique changes the same +198 record");
            await ObserveAsync(caster, from, r => Bonus(r, Technique, 572, 99, 0, unload), "Technique changes the same +99 record");
            measurements.Add(await CastProbeAsync(enemy, server));
        }
        var before = measurements[0];
        var loaded = measurements[1];
        var restored = measurements[2];
        Check.Equal(before.Attack, loaded.Attack, "Same caster input with Technique loaded");
        Check.Equal(before.Attack, restored.Attack, "Same caster input after second unload");
        Check.Equal(198, loaded.Defense - before.Defense, "Exact Technique defense input delta");
        Check.Equal(before, restored, "The unchanged cast goes back to its initial inputs and verdict");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Retained stock framing");
        Console.WriteLine($"ZEN TECHNIQUE VIRAL PASS {before.Defense}/{loaded.Defense}/{restored.Defense}, hit {before.Hit}/{loaded.Hit}/{restored.Hit}");
        return Array.Empty<byte>();
    }

    internal sealed record Probe(int Attack, int Defense, bool Hit);

    internal static async Task<Probe> CastProbeAsync(HeadlessPlayer enemy, ServerInstance server)
    {
        Check.Equal(6114u, enemy.CharId, "Only the fixture enemy casts the virus");
        var target = await enemy.WaitForViewNamedAsync("CSR_Tester");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        int at = enemy.World.RpcSnapshot().Count, log = server.DebugLog().Length;
        enemy.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        enemy.SendRpc(WithHandle(StarterCoderSplashBatchMode.Samples[0].Request, handle));
        await EvidenceRetestWire.CastBarAsync(enemy, StaticBlast, 2);
        await enemy.WaitUntilAsync(() => Ledger.IsMatch(server.DebugLog()[log..]), "QAEnemy's retained virus resolves on CSR", HeadlessPlayer.DefaultTimeout);
        var result = Ledger.Match(server.DebugLog()[log..]);
        await EvidenceRetestWire.TimerAsync(enemy, at, StaticBlast, AbilityCooldowns.OwnTimer, 6, ended: true);
        return new Probe(int.Parse(result.Groups[1].Value), int.Parse(result.Groups[2].Value), bool.Parse(result.Groups[3].Value));
    }
}
