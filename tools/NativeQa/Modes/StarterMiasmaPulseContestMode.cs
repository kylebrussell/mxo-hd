using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterMiasmaPulseContestMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400;
    private const ushort Miasma = 61, Pulse = 701;
    // Full unselected stock Miasma request from PR320, PacketLog lines 1837–1838.
    private const string MiasmaUse = "80b93d00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle, Miasma)
        .Order().ToArray();
    private static readonly (string Name, uint Id, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, X + 400, Z, 2),
        ("QAFar", 6112, X, Z + 700, 2),
        ("QAFriend", 6111, X - 200, Z, 1)
    };

    public override string Name => "ability-parity-starter-miasma-pulse-contest";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
            "innerStrC=200,innerStrM=200,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)" }
        .Concat(Stands.Select(s => $"UPDATE characters SET level={Level},x={s.X},y={Y},z={s.Z},alignment={s.Org}," +
            $"healthC=3000,healthM=3000,pvpflag=0 WHERE charId={s.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new ushort[] { CombatStyles.HackerStyle, Miasma },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10", [AreaTargetDiagnostics.Variable] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "0",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "32766"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "61,701", ["NativeCloseableRows"] = 1,
            ["NativeCloseableIds"] = "701", ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, full prerequisite profile. CSR ({X},{Y},{Z}) faces QAEnemy ({X + 400},{Y},{Z}), 4 m east. QAFar is 7 m north, QAFriend is 2 m west. All stands are on outdoor NAV.",
            "Page 2 slot 1 Hacker Style, slot 2 Miasma. Clear world selection. Select Hacker Style then start Miasma once. Wait through two pulses, then turn it off. Save the first stock ability request and visible deflection line or FX.",
            "Expect [ABILITY-COST] 61 paid=5 IS=200->195, then 3 IS per scheduled 5 s tick. Only QAEnemy receives [FREECAST-VIRAL] ability=701 hit=False qa=True. QAFar and QAFriend are excluded before any roll. [AURA] 61 pulse 701 reaches 0 targets. No 701 [MODIFIER] add, HP damage, max [VITALS] or [PVP] round. Prior stock passes already cover hits, sight and the 6 m edge."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified outdoor stand and clear route ({x},{Y},{z})");

        var server = context.ActualServer!;
        foreach (var (name, _, _, _, _) in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            rows.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                rows.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The complete Miasma profile fits memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var helper in context.Helpers.Values)
            await actor.WaitForViewNamedAsync(helper.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var cursors = context.Helpers.ToDictionary(pair => pair.Key, pair => pair.Value.World.RpcSnapshot().Count);
        int start = server.DebugLog().Length;
        int views = actor.World.ViewSnapshot().Count;
        actor.SendRpc(MiasmaUse);
        await EvidenceRetestWire.CastBarAsync(actor, Miasma, 2);
        var miss = BytePattern.Parse("81 67 ?? ?? ?? ?? f2 00 00 24 01 bd 02 00 00");
        await actor.WaitUntilAsync(() => server.DebugLog()[start..].Split('\n')
            .Count(line => line.Contains("[AURA] 61 pulse 701 reaches 0 targets", StringComparison.Ordinal)) >= 2,
            "Two Miasma pulses finish without a landed hit", TimeSpan.FromSeconds(12));
        actor.SendRpc(MiasmaUse);
        await actor.WaitForRpcAsync("80 bd 05 3d 00 00 00 00 00 00 00");
        var casts = server.DebugLog()[start..].Split('\n').Where(line =>
            line.Contains("[FREECAST-VIRAL] ability=701 ", StringComparison.Ordinal)).ToArray();
        Check.That(casts.Length == 2 && casts.All(line => line.Contains("target=QAEnemy ", StringComparison.Ordinal) &&
            line.Contains("hit=False qa=True", StringComparison.Ordinal)), "Only the eligible hostile rolls, and both pulses deflect");
        Check.That(actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
            .Contains((ushort)192), "Miasma pays activation and one scheduled drain despite the deflections");
        foreach (var (name, helper) in context.Helpers)
        {
            var reports = helper.World.RpcSnapshot().Skip(cursors[name]).ToArray();
            Check.That(!reports.Any(frame => new ushort[] { 1009, 1010 }.Any(stat =>
                StockAbilityProfile.Bonus(frame, Pulse, stat, -14, 5, false))), "Neither defense modifier lands on " + name);
            if (name == "QAEnemy")
                Check.Equal(2, reports.Count(frame => miss.IsMatch(frame.Bytes)), "The recipient receives both 701 deflection notices");
        }
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Miasma contest framing");
        return Array.Empty<byte>();
    }
}
