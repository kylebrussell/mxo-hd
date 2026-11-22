using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterEvadeReleaseMode : NativeQaMode
{
    private const int Level = 19, EnemyLevel = 11, X = 17000, Y = 495, Z = 2300, EnemyX = 17120;
    private const ushort Evade = EvasionShield.Parent, Sprint = 184, Blur = 49;

    // Complete stock Evade request from PR524, Sprint request from PR627.
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    // Complete Gaussian Blur request from the stock capture used by StarterEvadeLevelGapMode.
    private const string BlurUse = "80b93100000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Evade, Sprint, Blur).Order().ToArray();

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
        }, DevCommands: true, hotbarAbilities: new[] { Evade, Sprint, Blur }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "10", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "217,184,49", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level} CSR ({X},{Y},{Z}) faces level {EnemyLevel} QAEnemy ({EnemyX},{Y},{Z}) on verified outdoor NAV. Start with 160 IS and keep default Speed. QAEnemy starts in Power. Page 2 slots 1â€“3 are Evade Combat, Hyper-Sprint and Gaussian Blur.",
            "From rest use slot 1. Expect the 2 s cast, [ABILITY-COST] ability=217 paid=40 and [EVASION] CSR_Tester state on. State 624 and the own shield meter at 255 appear after the cast. Clear any unnecessary selection.",
            """Use helper input {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Expect [EVASION] CSR_Tester resistance IS 120->113 cost=7; shield 255->194 damage=24. Keep the client-visible refusal and the defender's 4d 0e 01 01 00 02 00 <self-spawn> while the helper sees its ordinary attacker result. There should be no [PVP] round or health change.""",
            "Press slot 1 to end Evade, then slot 2 Hyper-Sprint immediately. Expect 624 removal, the 30 s Evade reuse and no second Evade cast bar or charge. Sprint should activate on the first press, [ABILITY-COST] ability=184 paid=25 and [MODIFIER] source 184 adds 115/877/878 +15. After 1 s and before the first 5 s upkeep, press slot 2 again to stop Sprint and capture the three +15 removals.",
            "Within the Evade timer, try slot 1 again and try Gaussian Blur in slot 3. Expect the stock gates or reason 5 for Evade and 23 for Blur, with no IS debit or cast. After the 30 s ends, use Evade once. Expect one 2 s cast, [ABILITY-COST] 217 paid=40, a new source 217 state 624 add and a fresh 255 shield. Then slot 3 should cast for 20 IS and add [MODIFIER] source 49 stat 520 +25 for 600 s. No [PVP] round or max [VITALS] change."
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
        await Task.Delay(TimeSpan.FromSeconds(1));
        int stopSprint = actor.World.RpcSnapshot().Count;
        actor.SendRpc(SprintUse);
        await StockAbilityProfile.ObserveAsync(actor, stopSprint,
            frame => StockAbilityProfile.Bonus(frame, Sprint, 115, 15, 0, true), "Sprint stops independently");

        await StarterHackerControlTargetMode.RefusedAsync(actor, Evade, WireFormat.FromHex(EvadeUse), AbilityRefusals.ReuseWait);
        await StarterHackerControlTargetMode.RefusedAsync(actor, Blur, WireFormat.FromHex(BlurUse), AbilityRefusals.CasterStateRequired);

        byte[] endTimer = AbilityCooldowns.BuildTimer(Evade, AbilityCooldowns.OwnTimer, 30, false);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(release).Any(frame => frame.Bytes.AsSpan().SequenceEqual(endTimer)),
            "Evade's 30-second termination timer clears", TimeSpan.FromSeconds(35));
        int rearm = actor.World.RpcSnapshot().Count, rearmViews = actor.World.ViewSnapshot().Count;
        int enemyViews = enemy.World.ViewSnapshot().Count;
        logAt = server.DebugLog().Length;
        actor.SendRpc(EvadeUse);
        await EvidenceRetestWire.CastBarAsync(actor, Evade, 2);
        await StockAbilityProfile.ObserveAsync(actor, rearm,
            frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, false), "A new Evade activation restores 624");
        byte[] flags = PlayerViewAttributes.U32(PvpFlag.PvpBit | EvasionShield.StateFlag);
        byte[] fullMeter = PlayerViewAttributes.BuildSelf((30, PlayerViewAttributes.U8(255)),
            (PlayerViewAttributes.SelfConditionStateFlags, flags));
        await EvidenceRetestWire.ViewSinceAsync(actor, rearmViews, 2, WireFormat.ToHex(fullMeter[2..]));
        byte[] otherMeter = PlayerViewAttributes.Build((21, PlayerViewAttributes.U8(255)),
            (PlayerViewAttributes.OtherConditionStateFlags, flags));
        await EvidenceRetestWire.ViewSinceAsync(enemy, enemyViews, defender.ViewId, WireFormat.ToHex(otherMeter));

        int blur = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BlurUse);
        await EvidenceRetestWire.CastBarAsync(actor, Blur, 2);
        await StockAbilityProfile.ObserveAsync(actor, blur,
            frame => StockAbilityProfile.Bonus(frame, Blur, 520, 25, 600, false), "The fresh shield reopens Gaussian Blur admission");
        string restored = server.DebugLog()[logAt..];
        Check.That(restored.Contains("ability=217 paid=40 IS=88->48", StringComparison.Ordinal) &&
            restored.Contains("ability=49 paid=20 IS=48->28", StringComparison.Ordinal),
            "Only the accepted Evade and Blur casts spend Inner Strength");

        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Stock toggle and combat result framing");
        return Array.Empty<byte>();
    }
}
