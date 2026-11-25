using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public class TransmitCodeMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, EnemyZ = 6600;
    private const ushort Incoming = 330;
    private const int RawDamage = 640;
    private readonly ushort passive;
    private readonly (ushort Stat, int Value)[] bonuses;
    private readonly ushort[] defenderAbilities;
    private readonly int baselinePercent, baselinePoints, baselineDamage, bonusPercent, resistancePoints, reducedDamage;
    private static readonly ushort[] AttackerAbilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle, Incoming)
        .Order().ToArray();
    private static readonly int AttackerMemory = AttackerAbilities.Sum(AbilityMemory.Cost);

    public override string Name => "transmit-code-resistance";
    public override NativeQaModePlan Plan { get; }

    public TransmitCodeMode() : this(593, new (ushort, int)[] { (1146, 10), (1148, 10) }, reducedDamage: 606) { }

    protected TransmitCodeMode(ushort passive, (ushort Stat, int Value)[] bonuses, int reducedDamage,
        int baselinePercent = 0, int baselineDamage = RawDamage)
    {
        this.passive = passive;
        this.bonuses = bonuses;
        defenderAbilities = DisablingShotMode.Closure(17, passive);
        int defenderMemory = defenderAbilities.Sum(AbilityMemory.Cost);
        this.baselinePercent = baselinePercent;
        baselinePoints = (4 * Level + 20) * baselinePercent / 100;
        this.baselineDamage = baselineDamage;
        bonusPercent = baselinePercent + bonuses.Single(bonus => bonus.Stat == 1148).Value;
        resistancePoints = (4 * Level + 20) * bonusPercent / 100;
        this.reducedDamage = reducedDamage;
        string records = string.Join(", ", bonuses.Select(bonus => $"stat={bonus.Stat} +{bonus.Value}"));
        int baseResisted = RawDamage - baselineDamage;
        Plan = new(defenderAbilities, X, Z, Array.Empty<string>(),
            new[] { $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(passive)}" },
            new[]
            {
                PvpOpponentsFixture.NativeSql,
                $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)} WHERE charId=110",
                $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation={MobAggro.YawTowards(X,EnemyZ,X,Z)} WHERE charId=6114",
                $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
                "DELETE FROM char_abilities WHERE char_id=6114",
                "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",",
                    AttackerAbilities.Select((id, slot) => $"(6114,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
            },
            DevCommands: true, hotbarAbilities: passive == 593 ? null : new ushort[] { 17 }, environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
            },
            extraQaMetadata: new()
            {
                ["ScopedStockRows"] = passive.ToString(), ["NativeCloseableRows"] = 0, [$"Native{passive}InitiallyLoaded"] = false,
                ["DefenderMemoryLoaded"] = defenderMemory - AbilityMemory.Cost(passive), [$"DefenderMemoryWith{passive}"] = defenderMemory,
                ["AttackerMemory"] = AttackerMemory, ["TransmitCodePolicy"] = "OURS level-normalized typed resistance bonus points"
            },
            readyMessages: new[]
            {
                $"Level {Level}. CSR has {defenderMemory - AbilityMemory.Cost(passive)} memory loaded and {defenderMemory}/100 with passive {passive}. QAEnemy uses a legal {AttackerMemory}/100 profile. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}) at 2 m. No ?tp or NPC selection is required. If an NPC appears behind CSR, strafe, turn the camera and single-click the name you intend to select.",
                $"Stay at spawn and keep tactics fixed. Request {{\"actor\":\"QAEnemy\",\"action\":\"cast\",\"ability\":330,\"target\":\"CSR_Tester\"}}. This sends the complete retained stock 330 with its live target handle. Expect 2 s cast, 13 IS on the helper, a 4 s reuse and {baselineDamage} damage with {baseResisted} resisted. Wait 6 s after each helper request before repeating.",
                $"Type ?qaload {passive}. This disposable command submits the normal Archive load. Expect [MODIFIER] char=110 source={passive} add duration=0s for {records}. Repeat the helper cast. Expect [RESISTANCE-BONUS OURS] type2 level50 percent{bonusPercent} points{resistancePoints} base0 total{resistancePoints}, then [TYPED-RESISTANCE OURS] 330 type2 total{resistancePoints} raw640 final{reducedDamage}, {RawDamage - reducedDamage} resisted.",
                $"Type ?qaunload {passive} and check the matching removals. The third identical request returns to {baselineDamage} damage, {baseResisted} resisted. No [PVP] round. This verifies the current resistance consumer. The historical retail coefficient remains open."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { Z, EnemyZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, X, Y, z), $"Clear outdoor NAV ({X},{Y},{z})");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        CheckArchive(enemy, AttackerAbilities, AttackerAbilities);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        enemy.SendRpc(StarterHackerViralSightMode.StockStyle);
        await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        if (context.StockProbe is not { } defender) return Array.Empty<byte>();
        CheckArchive(defender, defenderAbilities.Where(id => id != passive).ToArray(), defenderAbilities);
        await PvpOpponentsFixture.EnterAsync(defender);
        NativeQaReadiness.RequireNoAmbientAggro(server, defender);

        var archive = ReadArchive(defender, 7);
        var database = context.OwnedDatabase!;
        uint descriptor = AbilityMessages.AbilityGameObject(passive);
        string parentQuery = $"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={defender.CharId} " +
            $"AND ability_id<>{descriptor} ORDER BY slot,id";
        string parents = await database.ExecuteAsync(parentQuery);
        Check.That(!defender.World.RpcSnapshot().Any(frame => bonuses.Any(bonus =>
            StockAbilityProfile.Bonus(frame, passive, bonus.Stat, bonus.Value, 0, false))), "Unloaded passive publishes no bonuses");

        await HitAsync(baselineDamage, RawDamage - baselineDamage, baselinePoints);
        await SetPassiveAsync(true);
        await HitAsync(reducedDamage, RawDamage - reducedDamage, resistancePoints);
        await SetPassiveAsync(false);
        await HitAsync(baselineDamage, RawDamage - baselineDamage, baselinePoints);

        Check.Equal(0, enemy.World.Problems.Count + defender.World.Problems.Count, "Both stock wire sessions decode cleanly");
        Console.WriteLine($"VIRAL RESISTANCE PASS {passive}: retained 330 hits {baselineDamage}/{RawDamage - baselineDamage}, {reducedDamage}/{RawDamage - reducedDamage}, {baselineDamage}/{RawDamage - baselineDamage}. Archive prerequisites are unchanged.");
        return Array.Empty<byte>();

        async Task SetPassiveAsync(bool load)
        {
            int cursor = await StockAbilityProfile.ChangeLoadoutAsync(defender, archive, new[] { passive }, load);
            foreach (var (stat, value) in bonuses)
                await StockAbilityProfile.ObserveAsync(defender, cursor,
                    frame => StockAbilityProfile.Bonus(frame, passive, stat, value, 0, !load),
                    $"Archive publishes {passive}/{stat}, loaded={load}");
            Check.Equal(load ? 1L : 0L,
                await database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={defender.CharId} AND ability_id={descriptor}"),
                "Only the owned passive changes Archive membership");
            Check.Equal(parents, await database.ExecuteAsync(parentQuery), "Prerequisite Archive rows are unchanged");
        }

        async Task HitAsync(int damage, int resisted, int expectedPoints)
        {
            int casts = enemy.World.RpcSnapshot().Count, damageAt = defender.World.RpcSnapshot().Count, logAt = server.DebugLog().Length;
            await StarterTransmitVirusMode.SendProbeAsync(enemy, defender.Handle, Incoming);
            var bar = await StockAbilityProfile.ObserveAsync(enemy, casts, frame => frame.Bytes is [0x80, 0xac, 0x4a, 0x01, ..],
                "Fresh cast bar for exact Logic Barrage");
            Check.Equal(2f, BitConverter.ToSingle(bar.Bytes.AsSpan(^4)), "Two-second 330 cast");
            var hit = await StockAbilityProfile.ObserveAsync(defender, damageAt, frame =>
                TransmitCodeFixture.Damage(frame.Bytes, Incoming, enemy.Handle, defender.Handle, out _, out _), "Ordinary damage reaches CSR");
            TransmitCodeFixture.Damage(hit.Bytes, Incoming, enemy.Handle, defender.Handle, out int done, out int absorbed);
            Check.Equal((damage, resisted), (done, absorbed), "The same stock attack observes only the current typed resistance");
            await EvidenceRetestWire.TimerAsync(enemy, casts, Incoming, AbilityCooldowns.OwnTimer, 4, ended: true);
            string log = server.DebugLog()[logAt..];
            Check.That(log.Contains("[ABILITY-COST] char=6114 ability=330 paid=13"), "The helper pays the ordinary 330 cost");
            if (resisted > 0)
                Check.That(log.Contains($"[TYPED-RESISTANCE OURS] 330 type2 total{expectedPoints} raw640 final{damage}"), "One typed conversion feeds the same incoming hit");
        }
    }

    private static Dictionary<ushort, AbilityMemoryUiFixture.Tuple> ReadArchive(HeadlessPlayer player, byte chunk) =>
        AbilityMemoryUiFixture.Decode(player.CharacterData[chunk]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));

    private static void CheckArchive(HeadlessPlayer player, ushort[] loadedIds, ushort[] ownedIds)
    {
        var loaded = ReadArchive(player, 6);
        var owned = ReadArchive(player, 7);
        Check.That(loaded.Keys.ToHashSet().SetEquals(loadedIds) && ownedIds.All(owned.ContainsKey) &&
            ownedIds.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && ownedIds.All(id =>
                AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            $"{player.Handle} owns the complete legal ranked profile. Loaded: [{string.Join(",", loaded.Keys.Order())}], " +
            $"owned: [{string.Join(",", owned.Keys.Order())}]");
    }
}
