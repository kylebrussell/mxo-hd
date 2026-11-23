using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterControlRangeMode : NativeQaMode
{
    private const int Level = 35, Rank = 19, X = 21550, Y = 1095, Z = 16150, TargetX = 21750, AllyX = 22350;
    private const ushort Hacker = 1006, Handguns = 129, Hindering = 130;
    // PR389 Hindering Shot plus the complete PR489 Blind and Pacify captures.
    private static readonly (ushort Id, string Capture, int Reuse, string Effect)[] Samples =
    {
        (43, EarlyHackerControlRecoveryMode.BlindRequest, 43, "State 90 0 16 s"),
        (48, EarlyHackerControlRecoveryMode.PacifyRequest, 43, "State 85 0 6 s"),
        (Hindering, "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000", 8, "Stat 519 -10 8 s")
    };
    private static readonly ushort[] Loadout = Samples.Select(s => s.Id).Concat(new ushort[] { 2, 17, Hacker, Handguns })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-control-range";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql, CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={AllyX},y={Y},z={Z},rotation=192 WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, Handguns, (ushort)43, (ushort)48, Hindering },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "43,48,130", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Rank cap {Rank}, level {Level} for the combined {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory profile. Verified outdoor NAV: CSR ({X},{Y},{Z}) faces the QA Sight Target at ({TargetX},{Y},{Z}); same-org QAFriend ({AllyX},{Y},{Z}). Handgun in hand. Page 2 slots 1–5 Hacker Style, Handguns, Disrupt Inputs, Freeze System, Hindering Shot.",
            "Run ?cd foundations-sight-pick and select QA Sight Target 2 m east. Run ?cd foundations-sight-far while selected. Try 43 and 48 with Hacker Style and 130 with Handguns. At the printed NAV stand the target exceeds 16/20/20 m. Expect no IS, cast, reuse or effects. If sent, a stock request gets reason 34. Keep selection without pressing Escape.",
            "Return to the 2 m stand with ?cd foundations-sight-pick. With Handguns, /target QAFriend and try 130. If the request reaches the server expect the live enemy notice and no cost, cast or reuse. Reselect QA Sight Target and test 130 at rest first. Expect damage 9, [MODIFIER] 115/877/878 -50 and 519 -10 for 8 s, and 20 IS with 8 s reuse.",
            "Keep QA Sight Target selected, switch back to Hacker Style and run ?cd sb. Try Disrupt Inputs and Freeze System across the mapped gap. Both should refuse locally or with reason 9 if sent, with no IS, cast, reuse or effects. Return with ?cd sp and keep the same target selected.",
            "On clear ground, 43 costs 10 IS and applies Blind for 16 s. Then 48 costs 26 IS and applies Pacify for 6 s plus viral resistance -20 for 12 s. Each has a 2 s cast and 43 s own reuse. The fixture fixes eligible viral draws. No PvP rounds or max [VITALS] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, TargetX, AllyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Outdoor range check stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, Z) && !NavMesh.HasSightPath(1, AllyX, Y, Z, TargetX, Y, Z),
            "The same hostile view has distinct clear and blocked stands within both cast ranges");

        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await PvpOpponentsFixture.EnterAsync(friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The complete control and handgun profile fits in memory");

        var server = context.ActualServer!;
        var target = await SightStandCastProof.PickAsync(actor, Hacker, StarterHackerViralSightMode.StockStyle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendChat("?cd foundations-sight-far");
        await actor.WaitForTextAsync("QA Sight Target is beyond 20 m");
        foreach (var sample in Samples)
        {
            if (sample.Id == Hindering) await SelectHandgunsAsync();
            await RefusedAsync(actor, sample.Id, WithHandle(sample.Capture, handle), AbilityRefusals.TargetOutOfRange);
        }

        actor.SendChat("?cd foundations-sight-pick");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        ViewCreation ally = await actor.WaitForViewNamedAsync(friend.Handle);
        await HinderingShotTargetMode.Refused(actor,
            WithHandle(Samples[2].Capture, DuelPackets.Handle(ally.ViewId, ally.SpawnId)), "a friendly player");

        await LandAsync(Samples[2]);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendChat("?cd sb");
        await actor.WaitForTextAsync("QA Sight Target is 6 m west across the mapped gap");
        foreach (var sample in Samples.Where(sample => sample.Id != Hindering))
            await RefusedAsync(actor, sample.Id, WithHandle(sample.Capture, handle), AbilityRefusals.TargetNotInSight);
        actor.SendChat("?cd sp");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        await LandAsync(Samples[0]);
        await LandAsync(Samples[1]);
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Range and live recipient framing");
        return Array.Empty<byte>();

        async Task SelectHandgunsAsync()
        {
            string style = StarterSoldierResistanceMode.Samples.Single(sample => sample.Id == Hindering).StockStyle;
            actor.SendRpc(WithHandle(style, handle));
            await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 81 00 00 00");
        }

        async Task LandAsync((ushort Id, string Capture, int Reuse, string Effect) sample)
        {
            int from = server.DebugLog().Length;
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(WithHandle(sample.Capture, handle));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, 2);
            await EvidenceRetestWire.TimerAsync(actor, cursor, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            await actor.WaitUntilAsync(() => server.DebugLog()[from..].Split('\n').Any(line =>
                line.Contains($"[EFFECTS] {sample.Id} on QA Sight Target:", StringComparison.Ordinal) &&
                line.Contains(sample.Effect, StringComparison.Ordinal)), $"In-range {sample.Id} applies {sample.Effect}", HeadlessPlayer.DefaultTimeout);
        }
    }
}
