using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterStaticFieldPulseContestMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400;
    private const ushort Hacker = CombatStyles.HackerStyle, Static = 89, Miasma = 61;
    private const string MiasmaUse = "80b93d00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, Static, Miasma).Order().ToArray();
    private static readonly (string Name, uint Id, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, X + 400, Z, 2),
        ("QAFar", 6112, X, Z + 700, 2),
        ("QAFriend", 6111, X - 200, Z, 1)
    };

    public override string Name => "ability-parity-starter-static-field-pulse-contest";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X, Z, X + 400, Z)}," +
            "innerStrC=200,innerStrM=200,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)" }
        .Concat(Stands.Select(s => $"UPDATE characters SET level={Level},x={s.X},y={Y},z={s.Z},alignment={s.Org}," +
            $"healthC=3000,healthM=3000,pvpflag=0 WHERE charId={s.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Hacker, Static, Miasma },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10", [AreaTargetDiagnostics.Variable] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "0",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "32766"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "89,694,61,701", ["NativeCloseableRows"] = 1,
            ["NativeCloseableIds"] = "694", ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}. Outdoor NAV. CSR ({X},{Y},{Z}) faces QAEnemy ({X + 400},{Y},{Z}), 4 m east. QAFar is 7 m north and QAFriend is 2 m west. Clear selection. Page 2 slots 1 Hacker, 2 Static Field, 3 Miasma.",
            "Select Hacker, then turn on Static Field. Save first stock 89 request. Expect [ABILITY-COST] 89 paid=11 IS=200->189. Let the second pulse fire; IS should reach 177. Expect two [FREECAST-VIRAL] ability=694 target=QAEnemy hit=False qa=True, both followed by [AURA] 89 pulse 694 reaches 0 targets. QAFar and QAFriend never roll. QAEnemy remains at 3000 HP. Toggle 89 off.",
            "Turn on Miasma as the control. Expect 5 IS then 3 IS upkeep (177->172->169). Capture two 701 deflections on QAEnemy and zero 701 [MODIFIER] adds. Then toggle off. No max [VITALS] changes or [PVP] interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(s => (s.X, s.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified open outdoor NAV ({x},{Y},{z})");

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
                rows.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Both auras' full level-19 profile fits memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var helper in context.Helpers.Values)
            await actor.WaitForViewNamedAsync(helper.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await CheckPulseAsync(Static, 694, StarterPathogenistAuraSightMode.StaticUse, 177);
        await CheckPulseAsync(Miasma, 701, MiasmaUse, 169);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Aura contest request framing");
        return Array.Empty<byte>();

        async Task CheckPulseAsync(ushort parent, ushort child, string use, ushort afterUpkeep)
        {
            int logAt = server.DebugLog().Length;
            int viewsAt = actor.World.ViewSnapshot().Count;
            var recipient = context.Helpers["QAEnemy"];
            int receivedAt = recipient.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(use);
            await actor.WaitForRpcAsync($"80 bd 05 {StockAbilityProfile.LittleEndianHex(parent)} 00 00 00 00 00 01");
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Split('\n').Count(line =>
                line.Contains($"[AURA] {parent} pulse {child} reaches 0 targets", StringComparison.Ordinal)) >= 2,
                $"Two {child} pulses finish without a landed effect", TimeSpan.FromSeconds(12));
            actor.SendRpc(use);
            await actor.WaitForRpcAsync($"80 bd 05 {StockAbilityProfile.LittleEndianHex(parent)} 00 00 00 00 00 00");
            var casts = server.DebugLog()[logAt..].Split('\n').Where(line =>
                line.Contains($"[FREECAST-VIRAL] ability={child} ", StringComparison.Ordinal)).ToArray();
            Check.That(casts.Length == 2 && casts.All(line => line.Contains("target=QAEnemy ", StringComparison.Ordinal) &&
                line.Contains("hit=False qa=True", StringComparison.Ordinal)), $"Both {child} pulses deflect only on QAEnemy");
            var miss = BytePattern.Parse($"81 67 ?? ?? ?? ?? f2 00 00 24 01 {StockAbilityProfile.LittleEndianHex(child)} 00 00");
            var replies = recipient.World.RpcSnapshot().Skip(receivedAt).ToArray();
            Check.Equal(2, replies.Count(frame => miss.IsMatch(frame.Bytes)), "The recipient receives both native deflection notices");
            Check.That(!replies.Any(frame => frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, child,
                actor.Handle, recipient.Handle, 1, 0)) || StockAbilityProfile.Bonus(frame, child, 1009, -14, 5, false)),
                "The deflections land neither damage nor debuffs");
            Check.That(AwakenedPowerShotMode.InnerStrengthUpdates(actor.World.ViewSnapshot().Skip(viewsAt)).Contains(afterUpkeep),
                "The parent still charges activation and one upkeep");
        }
    }
}
