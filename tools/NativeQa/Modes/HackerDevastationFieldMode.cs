
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerDevastationFieldMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, Centre = 6700, RangeControl = 8001;
    private const ushort Style = CombatStyles.HackerStyle, Field = 468, StockLogic = 57;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Field).Order().ToArray();
    private static readonly (string Handle, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Centre, 2),
        ("QAOther", 6113, X, Centre + 600, 2),
        ("QAFar", 6112, X, Centre + 601, 2),
        ("QAFriend", 6111, X + 100, Centre, 1)
    };

    public override string Name => "ability-parity-hacker-devastation-field";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=3000,healthM=3000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,Centre)},innerStrC=500,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Character}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { Style, Field }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "468", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = 61 },
        readyMessages: new[]
        {
            "Level 50, 61/100 ranked memory. CSR (17000,495,6400) faces QAEnemy (17000,495,6700). QAOther is exactly 6.00 m from QAEnemy at (17000,495,7300), QAFar is 6.01 m at (17000,495,7301), and ally QAFriend is at (17100,495,6700). Outdoor stands are clear. If needed, correct CSR with ?tp 17000 495 6400 4e2 12 4 - 00 and one strafe.",
            "Page 2 slot 1 selects Hacker Style, slot 2 Devastation Field. Stay out of combat. With QAFriend selected and then a distant QAEnemy, use slot 2. Expect local refusal or reason 24/34 if sent, no cost or reuse.",
            """Distant helper request {"actor":"QAEnemy","action":"move","x":17000,"y":495,"z":8001}. Return request {"actor":"QAEnemy","action":"move","x":17000,"y":495,"z":6700}.""",
            "Select the returned QAEnemy and cast slot 2. Keep the first full stock 468 request. Expect 56 IS (500 to 444), 8 s windup, 30 s own reuse and one viral damage on QAEnemy and QAOther only. The ally and the 6.01 m witness receive no effect. Each landed eligible hit has an independent 50% chance to apply six seconds of Stun 102 plus six seconds of control immunity 734/681 after its separate 0.1 s term marker.",
            "Watch [FREECAST-VIRAL], [PVP], and both Stun and immunity [MODIFIER] add/remove records. A natural non-proc still deals one damage. Wait for 30 s reuse and repeat the two-target cast if neither eligible recipient rolled Stun. Retry only if needed for one observed Stun and immunity pair; report any unobserved proc explicitly."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Devastation Field needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)).Append((X, RangeControl)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && nav.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z) && NavMesh.HasSightPath(1, X, Y, Centre, x, Y, z),
                $"Devastation Field outdoor stand and sight ({x},{Y},{z})");

        var server = context.ActualServer ?? throw new InvalidOperationException("Missing Devastation Field fixture server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) == 61 && 61 <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id =>
                AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Devastation Field has its complete legal ranked Archive.");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var stand in Stands)
        {
            var view = await caster.WaitForViewNamedAsync(stand.Handle);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Handle} spawns at the printed stand.");
        }

        var target = await caster.WaitForViewNamedAsync("QAEnemy");
        var ally = await caster.WaitForViewNamedAsync("QAFriend");
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(caster, 0, packet => StockAbilityProfile.Bonus(packet, Style, 172, 27, 0, false),
            "The stock Hacker Style is active.");
        // The archived intake has no complete 468 cast. The ordinary 57 request verifies the setup.
        string sample = StarterHackerViralSightMode.Samples[0].Capture;
        await StarterHackerViralDamageMode.VerifyInvalidTargets(caster, StockLogic, sample, target, ally);
        int cursor = caster.World.RpcSnapshot().Count, log = server.DebugLog().Length;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(sample, DuelPackets.Handle(target.ViewId, target.SpawnId)));
        await EvidenceRetestWire.CastBarAsync(caster, StockLogic, 2);
        await EvidenceRetestWire.TimerAsync(caster, cursor, StockLogic, AbilityCooldowns.OwnTimer, 4);
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("LogicBlast1Ability lands on QAEnemy for "),
            "The retained Logic Blast selects the opposing helper and lands.", HeadlessPlayer.DefaultTimeout);
        Check.That(context.Helpers.Values.Append(caster).All(player => player.World.Problems.Count == 0),
            "Devastation Field control replay has no transport errors.");
        return Array.Empty<byte>();
    }
}
