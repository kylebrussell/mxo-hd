using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterEvadeReleaseMode : NativeQaMode
{
    private const int Level = 19, EnemyLevel = 11, X = 17000, Y = 495, Z = 2300, EnemyX = 17120;
    private const ushort Evade = EvasionShield.Parent, Sprint = 184;

    // Complete stock Evade request from PR524, Sprint request from PR627.
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Evade, Sprint).Order().ToArray();

    public override string Name => "ability-parity-starter-evade-release";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,EnemyX,Z)},innerStrC=160,innerStrM=200 WHERE charId=110",
            $"UPDATE characters SET level={EnemyLevel},exp={PlayerProgression.ExperienceForLevel(EnemyLevel)},x={EnemyX},y={Y},z={Z} WHERE charId=6114",
            "DELETE FROM inventory WHERE charId IN (110,6114) AND slot IN (105,107)",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                $"(6114,400,{AbilityMessages.AbilityGameObject(2)},{EnemyLevel},1)," +
                $"(6114,401,{AbilityMessages.AbilityGameObject(17)},{EnemyLevel},1)"
        }, DevCommands: true, hotbarAbilities: new[] { Evade, Sprint }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "10", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "217,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level} CSR ({X},{Y},{Z}) faces level {EnemyLevel} QAEnemy ({EnemyX},{Y},{Z}) on verified outdoor NAV. Start with 160 IS and keep default Speed. QAEnemy starts in Power. Page 2 slots 1 and 2 are Evade Combat and Hyper-Sprint.",
            "From rest use slot 1. Expect the 2 s cast, [ABILITY-COST] ability=217 paid=40 and [EVASION] CSR_Tester state on. State 624 and the own shield meter at 255 appear after the cast. Clear any unnecessary selection.",
            """Use helper input {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Expect [EVASION] CSR_Tester resistance IS 120->113 cost=7; shield 255->194 damage=24. Keep the client-visible refusal and the defender's 4d 0e 01 01 00 02 00 <self-spawn> while the helper sees its ordinary attacker result. There should be no [PVP] round or health change.""",
            "Press slot 1 to end Evade, then slot 2 Hyper-Sprint immediately. Expect 624 removal, the 30 s Evade reuse and no second Evade cast bar or charge. Sprint should activate on the first press, [ABILITY-COST] ability=184 paid=25 and [MODIFIER] source 184 adds 115/877/878 +15. Keep the request times if the stock UI suppresses the immediate Sprint press."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Expected repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1,
                $"Verified outdoor stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "The close-combat helper has a clear path to CSR");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        await enemy.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        enemy.SendRpc("42 01 00 00 00 00 00");
        await StockAbilityProfile.ObserveAsync(enemy, 0, frame => StockAbilityProfile.Bonus(frame, 1017, 1012, 1, 0, false),
            "The helper selected the stock Power tactic");
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => ranks.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => ranks.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The stock login includes every prerequisite within level-19 memory");
        await actor.EnterWorldAsync();
        await actor.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var defender = await enemy.WaitForViewNamedAsync(actor.Handle);
        var owner = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);

        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(EvadeUse);
        await EvidenceRetestWire.CastBarAsync(actor, Evade, 2);
        await StockAbilityProfile.ObserveAsync(actor, start,
            frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, false), "The live shield's Evading record");

        string selected = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(defender.ViewId, defender.SpawnId)));
        string self = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(2, owner.SpawnId)));
        byte[] received = WireFormat.FromHex($"4d 0e 01 01 00 {self}");
        byte[] refused = WireFormat.FromHex($"4d 0e 00 01 00 {selected}");
        int ownerAt = actor.World.RpcSnapshot().Count, attackerAt = enemy.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        enemy.SendRpc($"40 {selected} 01 6b");
        await StockAbilityProfile.ObserveAsync(enemy, attackerAt,
            frame => frame.Bytes.AsSpan().SequenceEqual(refused), "Attacker's existing refusal");
        await StockAbilityProfile.ObserveAsync(actor, ownerAt,
            frame => frame.Bytes.AsSpan().SequenceEqual(received), "Defender's stock-shaped refusal uses its own view and spawn");
        Check.That(server.DebugLog()[logAt..].Contains("resistance IS 120->113 cost=7; shield 255->194 damage=24", StringComparison.Ordinal),
            "The defender result follows the expected IS and shield debit");

        int release = actor.World.RpcSnapshot().Count;
        logAt = server.DebugLog().Length;
        actor.World.SendRpcs(WireFormat.FromHex(EvadeUse), WireFormat.FromHex(SprintUse));
        await StockAbilityProfile.ObserveAsync(actor, release,
            frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, true), "The toggle-off removes the current shield");
        await EvidenceRetestWire.TimerAsync(actor, release, Evade, AbilityCooldowns.OwnTimer, 30);
        await StockAbilityProfile.ObserveAsync(actor, release,
            frame => StockAbilityProfile.Bonus(frame, Sprint, 115, 15, 0, false), "A following Sprint starts without a phantom cast reservation");
        string ended = server.DebugLog()[logAt..];
        Check.That(!ended.Contains("A cast is running; denied", StringComparison.Ordinal) &&
            ended.Contains("ability=184 paid=25 IS=113->88", StringComparison.Ordinal) &&
            !ended.Contains("ability=217 paid=", StringComparison.Ordinal), "Only Sprint spends IS after the two-request switch");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Stock toggle and combat result framing");
        return Array.Empty<byte>();
    }
}
