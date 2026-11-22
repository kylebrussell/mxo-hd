using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerTrojanHorseDeflectionMode : NativeQaMode
{
    private const ushort Parent = 945, Pulse = 946;
    private const int Level = 30, X = 21925, Y = 1095, Z = 16100;
    private const int StartIs = 700, HelperIs = 500;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Parent).Order().ToArray();
    private static readonly (string Name, int Character, int X, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X + 200, 2),
        ("QAFar", 6112, X - 250, 2),
        ("QAFriend", 6111, X + 100, 1)
    };
    // Complete unselected Trojan Horse activation from native PR545.
    private const string StockTrojanHorse = "80b9b103000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000";

    public override string Name => "ability-parity-hacker-trojan-horse-deflection";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+200,Z)},alignment=1,pvpflag=0,innerStrC={StartIs},innerStrM=900 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={stand.X},y={Y},z={Z}," +
            $"alignment={stand.Organization},pvpflag=0,healthC=1000,healthM=1000,innerStrC={HelperIs},innerStrM={HelperIs} WHERE charId={stand.Character}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { Parent },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "0", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "32766",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "946", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR starts at ({X},{Y},{Z}), facing QAEnemy ({X + 200},{Y},{Z}) 2 m east. QAFar ({X - 250},{Y},{Z}) is behind the mapped gap but in the 3 m radius. Same-org QAFriend ({X + 100},{Y},{Z}) is clear. All are on verified outdoor NAV away from the ice.",
            "Keep selection empty and press page 2 slot 1. Confirm [ABILITY-COST] ability=945 paid=20 IS=700->680, a 2 s cast and 15 s reuse. There is no pulse until four seconds after the aura turns on.",
            "Watch two pulses. Only QAEnemy enters [FREECAST-VIRAL] ability=946 ... target=QAEnemy ... hit=False qa=True, with the normal visible deflection notice and FX. Expect [AREA-QA] QAFar reason=sight and QAFriend reason=pvp-NotOpponent. There is no [HOSTILE] 946 IS line, no recipient [VITALS] change or PvP round. CSR still pays 20 each tick, reaching 660 and 640 IS.",
            "Press slot 1 again after the second pulse to see 945 off. No later pulse should run. Prior stock PR574 verified the 20 IS loss on the eligible recipient. The fixed deflection draw is scoped to this disposable fixture."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in Stands.Select(stand => stand.X).Append(X))
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified clear outdoor NAV ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X + 200, Y, Z) &&
            NavMesh.HasSightPath(1, X, Y, Z, X + 100, Y, Z) &&
            !NavMesh.HasSightPath(1, X, Y, Z, X - 250, Y, Z), "The intended helpers share a floor and the far side is blocked");

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(caster, Abilities);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var helper in context.Helpers.Values) await caster.WaitForViewNamedAsync(helper.Handle);
        int rpcStart = caster.World.RpcSnapshot().Count;
        int logStart = server.DebugLog().Length;
        var witnesses = context.Helpers.Values.Select(helper => (Helper: helper, View: helper.World.ViewSnapshot().Count,
            Rpc: helper.World.RpcSnapshot().Count)).ToArray();
        caster.SendRpc(StockTrojanHorse);
        await EvidenceRetestWire.CastBarAsync(caster, Parent, 2);
        await EvidenceRetestWire.TimerAsync(caster, rpcStart, Parent, AbilityCooldowns.OwnTimer, 15);
        await ExclusiveAsync(rpcStart, on: true);
        Check.That(!server.DebugLog()[logStart..].Contains("[FREECAST-VIRAL] ability=946", StringComparison.Ordinal),
            "Trojan Horse starts without an initial contest");
        await AssertCasterIsAsync(680);

        string deflectPrefix = $"81 67 ?? ?? ?? ?? f2 00 00 24 01 {EvidenceRetestWire.Le(Pulse)} 00 00";
        var deflection = BytePattern.Parse(deflectPrefix);
        var enemy = witnesses.Single(witness => witness.Helper.Handle == "QAEnemy");
        await enemy.Helper.WaitUntilAsync(() => enemy.Helper.World.RpcSnapshot().Skip(enemy.Rpc)
            .Count(frame => deflection.IsMatch(frame.Bytes)) >= 2, "Only the eligible hostile receives two viral deflections", TimeSpan.FromSeconds(12));
        await AssertCasterIsAsync(660);
        await AssertCasterIsAsync(640);
        var fx = BytePattern.Parse("9c 05 00 28");
        Check.That(enemy.Helper.World.ViewSnapshot().Skip(enemy.View).Any(fx.Occurs), "Deflection FX reaches the recipient");
        int offCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StockTrojanHorse);
        await ExclusiveAsync(offCursor, on: false);
        string lines = server.DebugLog()[logStart..];
        var contests = lines.Split('\n').Where(line => line.StartsWith("[FREECAST-VIRAL] ability=946 ", StringComparison.Ordinal)).ToArray();
        Check.Equal(2, contests.Length, "One admitted recipient per pulse");
        Check.That(contests.All(line => line.Contains("target=QAEnemy ", StringComparison.Ordinal) &&
            line.Contains("hit=False qa=True", StringComparison.Ordinal)), "No excluded helper is rolled and both accepted contests deflect");
        Check.That(lines.Split('\n').Any(line => line.Contains("candidate=QAFar ", StringComparison.Ordinal) && line.Contains("reason=sight")) &&
            lines.Split('\n').Any(line => line.Contains("candidate=QAFriend ", StringComparison.Ordinal) && line.Contains("reason=pvp-NotOpponent")),
            "The mapped blocker and friendly recipient are excluded before their contests");
        Check.That(!lines.Contains("[HOSTILE] 946 IS", StringComparison.Ordinal) &&
            witnesses.All(witness => !witness.Helper.World.ViewSnapshot().Skip(witness.View).Any(view =>
                BuffDrainLifetimeFixture.TryInnerStrength(view, out ushort value) && value < HelperIs)),
            "A deflection leaves every helper's Inner Strength unchanged");
        Check.Equal(0, caster.World.Problems.Count + witnesses.Sum(witness => witness.Helper.World.Problems.Count),
            "Trojan Horse deflection packet framing");
        Console.WriteLine("TROJAN HORSE DEFLECTION PASS 945/946");
        return Array.Empty<byte>();

        Task ExclusiveAsync(int from, bool on) => StockAbilityProfile.ObserveAsync(caster, from, frame =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Parent, on)), $"Trojan Horse on={on}");

        Task AssertCasterIsAsync(ushort amount) => caster.WaitUntilAsync(() =>
            caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(amount),
            $"Caster reaches {amount} Inner Strength", HeadlessPlayer.DefaultTimeout);
    }
}
