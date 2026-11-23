using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerWhiteNoiseCodeShockMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 8100, AimZ = 7900, RangeZ = 6400;
    private const ushort Style = CombatStyles.HackerStyle, WhiteNoise = 362, CodeShock = 482, StockLogic = 57;
    private static readonly ushort[] Hotbar = { Style, WhiteNoise, CodeShock };
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17, StockLogic })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, AimZ, 2), ("QAFar", 6112, X, 7240, 2),
        ("QAOther", 6113, X, 7160, 2), ("QAFriend", 6111, X + 100, Z, 1)
    };

    public override string Name => "ability-parity-hacker-white-noise-code-shock";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=3000,healthM=3000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,AimZ)},innerStrC=500,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
    }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Id}"))
        .ToArray(), DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "362,482", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = 80 },
        readyMessages: new[]
        {
            "Level 50, 80/100 ranked memory. Verified outdoor NAV/sight CSR (17000,495,8100) faces QAEnemy (17000,495,7900). QAFar (17000,495,7240) is 6.6 m from the target, QAOther (17000,495,7160) is 7.4 m and QAFriend (17100,495,8100) is an ally. Stay on the starting stand.",
            "Select page 2 Hacker Style, then White Noise and Code Shock from slots 2–3. Try /target QAFriend and the distant QAEnemy. Expect reason 24/34 if sent, with no cost or reuse. Return QAEnemy before the positive casts.",
            """Far helper request {"actor":"QAEnemy","action":"move","x":17000,"y":495,"z":6400}. Near request {"actor":"QAEnemy","action":"move","x":17000,"y":495,"z":7900}.""",
            "Use /target QAEnemy and slot 2 White Noise. Keep its first complete stock 362 request. Expect 2 s cast, [ABILITY-COST] paid=25 IS=500->475 and 58 s own reuse. QAEnemy (6114) and QAFar (6112) get Blind state 90 for 12 s. QAOther and QAFriend get nothing. No damage or 734 immunity record. Wait for both Blind removals.",
            "Keep QAEnemy selected and use slot 3 Code Shock. Keep its first complete stock 482 request. Expect 4 s cast, [ABILITY-COST] paid=48 IS=475->427, 60 s own reuse and no damage. Only QAEnemy gets state 102 Stun for 6.6 s with Resist Contagion and Efficient Code, state 90 Blind for 6 s and 199/200/201 Daze/Off-Balance/Stagger for 5 s. The separate 734/681 immunity adds after those states and lasts 12 s. Observe all five state removals and the later immunity removal."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Control fixture requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)).Append((X, RangeZ)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Control fixture outdoor NAV and sight ({x},{Y},{z})");

        var server = context.ActualServer ?? throw new InvalidOperationException("Missing control fixture server");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) == 80 && 80 <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Both control casts have their complete ranked 80-memory loadout");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Name);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Name} appears at the printed stand");
        }

        var target = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAFriend");
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Style, 172, 27, 0, false),
            "The stock Hacker Style is ready");
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, 346, 1053, 15, 0, false),
            "Dedicated Code adds +15% cost reduction");
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, 477, 995, 5, 0, false),
            "Efficient Code adds +5% Stun Duration");
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, 73, 995, 5, 0, false),
            "Resist Contagion adds a separate +5% Stun Duration");

        // Neither 362 nor 482 has a retained complete input. Replay the exact stock sibling control.
        string stockLogic = StarterHackerViralSightMode.Samples[0].Capture;
        await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, StockLogic, stockLogic, target, ally);
        int cursor = actor.World.RpcSnapshot().Count;
        int log = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stockLogic, DuelPackets.Handle(target.ViewId, target.SpawnId)));
        await EvidenceRetestWire.CastBarAsync(actor, StockLogic, 2);
        await EvidenceRetestWire.TimerAsync(actor, cursor, StockLogic, AbilityCooldowns.OwnTimer, 4);
        await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains("LogicBlast1Ability lands on QAEnemy for "),
            "The retained selected viral cast lands only on the visible hostile", HeadlessPlayer.DefaultTimeout);
        Check.That(context.Helpers.Values.All(helper => helper.World.Problems.Count == 0) && actor.World.Problems.Count == 0,
            "Control setup keeps transport framing");
        return Array.Empty<byte>();
    }
}
