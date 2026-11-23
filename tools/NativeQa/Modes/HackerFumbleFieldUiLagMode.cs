using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerFumbleFieldUiLagMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, Centre = 6700, OutOfRange = 8001;
    private const ushort Fumble = 361, UiLag = 465, CodeNuke = 60, Style = CombatStyles.HackerStyle;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Style, CodeNuke, Fumble, UiLag).Order().ToArray();
    private static readonly (string Name, uint Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Centre, 2), ("QAOther", 6113, X, OutOfRange, 2),
        ("QAFar", 6112, X, Centre + 801, 2), ("QAFriend", 6111, X + 100, Centre, 1)
    };

    public override string Name => "ability-parity-hacker-fumble-field-ui-lag";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,Centre)},innerStrC=700,innerStrM=1000 WHERE charId=110",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Fumble, UiLag }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "361,465", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Abilities.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, full Howitzer and Destroyer prerequisite profile at {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Clear outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{Centre}) at 3 m. QAFriend (17100,495,6700) is an ally. QAOther starts at (17000,495,8001), 16.01 m from CSR.",
            "Select Hacker Style on page 2 slot 1. Try slots 2 and 3 with no target, QAFriend and QAOther. Keep the visible local refusal or server reason 24/34 and verify no cast, debit, or reuse. Use /target by helper name. Save the first complete stock requests on successful casts.",
            $$"""For Fumble Field 361, set QAOther to the 8.00 m edge with {"actor":"QAOther","action":"move","x":{{X}},"y":{{Y}},"z":7500}. QAFar starts outside at z7501. Select QAEnemy and press slot 2. Expect 2 s cast, 30 IS (700→670) and 58 s own reuse. QAEnemy and QAOther get [MODIFIER] source=361 stat=524 and 1032, both -27 for 35 s, then matching removals. The ally and outside helper get none. Wait for the removal before the next field.""",
            "Move QAOther to z7100 and QAFar to z7101 using the same helper move form. Keep QAEnemy selected and press slot 3, UI Lag 2.0 (465). Expect 2 s cast, 17 IS (670→653) and 30 s own reuse. QAEnemy and QAOther get [MODIFIER] source=465 stat=524 and 1032, both -20, and stat=1010 and 1011, both -18, for 17 s. All four expire. The ally and 4.01 m outside helper get none. Neither field deals damage or changes maximum vitals.",
            "After both samples, use the target's right-click Close Combat on QAEnemy. Reselect Hacker Style if the stock client switches styles. Once both reuse timers are clear, try slots 2 and 3 during active combat. Expect a local outside-combat refusal or reason 21 with no cast or debit."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Fumble Field and UI Lag need repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var edges = new[] { (X, Z), (X, 7500), (X, 7100), (X, 7101) };
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Concat(edges))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z) && NavMesh.HasSightPath(1, X, Y, Centre, x, Y, z),
                $"Visible outdoor NAV stand ({x},{Y},{z})");

        var server = context.ActualServer ?? throw new InvalidOperationException("The hostile field fixture needs a disposable server.");
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
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities) && Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Complete ranked Howitzer and Destroyer archive");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Name);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Name} is on its documented NAV stand");
        }

        // The archive has no complete 361/465 use requests. The bootstrap exercises their ordinary
        // load path and replays only the captured Hacker Style and Code Nuke requests.
        ushort[] tested = { Fumble, UiLag };
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, tested, load: false);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, tested, load: true);
        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, styleAt, frame => StockAbilityProfile.Bonus(frame, Style, 172, 27, 0, false),
            "Captured Hacker Style adds +27 Viral Damage");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 346, 1053, 15, 0, false),
            "Dedicated Code contributes the expected +15% cost reduction");

        var enemy = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAFriend");
        var edge = await actor.WaitForViewNamedAsync("QAOther");
        string stockNuke = StarterHackerViralSightMode.Samples.Single(sample => sample.Ability == CodeNuke).Capture;
        await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, CodeNuke, stockNuke, enemy, ally);
        int rangeAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stockNuke, DuelPackets.Handle(edge.ViewId, edge.SpawnId)));
        await StockAbilityProfile.ObserveAsync(actor, rangeAt, frame => AbilityRefusals.TryDecode(frame.Bytes, out var refusal) &&
            refusal.AbilityID == CodeNuke && refusal.ErrorID == AbilityRefusals.TargetOutOfRange, "Captured control gives a visible range refusal");

        await MoveAsync("QAOther", 7100);
        await MoveAsync("QAFar", 7101);
        var witnesses = context.Helpers.Values.ToDictionary(helper => helper, helper => helper.World.RpcSnapshot().Count);
        int castAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stockNuke, DuelPackets.Handle(enemy.ViewId, enemy.SpawnId)));
        await EvidenceRetestWire.CastBarAsync(actor, CodeNuke, 4);
        await EvidenceRetestWire.TimerAsync(actor, castAt, CodeNuke, AbilityCooldowns.OwnTimer, 2);
        var damage = BytePattern.Parse("81 67 ?? ?? ?? ?? " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.DamageTarget)) +
            " 00 00 10 00 00 " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.AbilityGameObject(CodeNuke))));
        foreach (var (helper, cursor) in witnesses)
        {
            if (helper.Handle is "QAEnemy" or "QAOther")
                await StockAbilityProfile.ObserveAsync(helper, cursor, frame => damage.IsMatch(frame.Bytes), $"Stock Code Nuke hits {helper.Handle}");
            else
            {
                helper.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(helper, cursor, frame => frame.Opcode == 0x149, "Excluded helper stays in world");
                Check.That(!helper.World.RpcSnapshot().Skip(cursor).Any(frame => damage.IsMatch(frame.Bytes)), $"Control excludes {helper.Handle}");
            }
        }
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Selected-field bootstrap framing");
        Console.WriteLine("FUMBLE FIELD/UI LAG BOOTSTRAP PASS: ordinary 361/465 Archive reload plus the exact captured selected-area control.");
        return Array.Empty<byte>();

        async Task MoveAsync(string name, int z)
        {
            var helper = context.Helpers[name];
            var view = await actor.WaitForViewNamedAsync(name);
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = actor.World.ViewSnapshot().Count;
            helper.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(actor, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }
    }
}
