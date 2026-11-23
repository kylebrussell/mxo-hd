using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class TransmitCodeMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, EnemyZ = 6600;
    private const ushort Transmit = 593, Incoming = 330;
    private const int RawDamage = 640, ReducedDamage = 606;
    private static readonly ushort[] DefenderAbilities = TransmitCodeFixture.DefenderAbilities;
    private static readonly ushort[] AttackerAbilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle, Incoming)
        .Order().ToArray();
    private static readonly int DefenderMemory = DefenderAbilities.Sum(AbilityMemory.Cost);
    private static readonly int AttackerMemory = AttackerAbilities.Sum(AbilityMemory.Cost);
    private static readonly ushort[] BonusStats = { 1146, 1148 };

    public override string Name => "transmit-code-resistance";

    public override NativeQaModePlan Plan { get; } = new(DefenderAbilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Transmit)}" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation={MobAggro.YawTowards(X,EnemyZ,X,Z)} WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",",
                AttackerAbilities.Select((id, slot) => $"(6114,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        },
        DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "593", ["NativeCloseableRows"] = 0, ["Native593InitiallyLoaded"] = false,
            ["DefenderMemoryLoaded"] = DefenderMemory - AbilityMemory.Cost(Transmit), ["DefenderMemoryWith593"] = DefenderMemory,
            ["AttackerMemory"] = AttackerMemory, ["TransmitCodePolicy"] = "OURS level-normalized 1146/1148 bonus points"
        },
        readyMessages: new[]
        {
            $"Level {Level}. CSR has {DefenderMemory - AbilityMemory.Cost(Transmit)} memory loaded and {DefenderMemory}/100 with Transmit Code. QAEnemy uses a legal {AttackerMemory}/100 profile. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}) at 2 m. No ?tp or NPC selection is required. If an NPC appears behind CSR, strafe, turn the camera and single-click the name you intend to select.",
            """Stay at spawn and keep tactics fixed. Request {"actor":"QAEnemy","action":"cast","ability":330,"target":"CSR_Tester"}. This sends the complete retained stock 330 with its live target handle. Expect 2 s cast, 13 IS on the helper, a 4 s reuse and 640 damage with zero resisted. Wait 6 s after each helper request before repeating.""",
            "Type ?qaload 593. This disposable command submits the normal owned Archive load. Expect [MODIFIER] char=110 source=593 stat=1146 and 1148 add value=+10 duration=0s. Repeat the same helper cast. Expect [RESISTANCE-BONUS OURS] type2 level50 percent10 points22 base0 total22, then [TYPED-RESISTANCE OURS] 330 type2 total22 raw640 final606 and 34 resisted.",
            "Type ?qaunload 593 and check both +10 removals. The third identical helper request returns to 640 damage, zero resisted. No parent loadout change, max [VITALS] change or [PVP] round. The fixture verifies the current consumer policy. Retail magnitude and a stock thrown/DOT comparison remain open."
        });

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
        CheckArchive(defender, DefenderAbilities.Where(id => id != Transmit).ToArray(), DefenderAbilities);
        await PvpOpponentsFixture.EnterAsync(defender);
        NativeQaReadiness.RequireNoAmbientAggro(server, defender);

        var archive = ReadArchive(defender, 7);
        var database = context.OwnedDatabase!;
        uint descriptor = AbilityMessages.AbilityGameObject(Transmit);
        string parentQuery = $"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={defender.CharId} " +
            $"AND ability_id<>{descriptor} ORDER BY slot,id";
        string parents = await database.ExecuteAsync(parentQuery);
        Check.That(!defender.World.RpcSnapshot().Any(frame => BonusStats.Any(stat =>
            StockAbilityProfile.Bonus(frame, Transmit, stat, 10, 0, false))), "Unloaded Transmit Code publishes no bonuses");

        await HitAsync(RawDamage, 0);
        await SetPassiveAsync(true);
        await HitAsync(ReducedDamage, RawDamage - ReducedDamage);
        await SetPassiveAsync(false);
        await HitAsync(RawDamage, 0);

        Check.Equal(0, enemy.World.Problems.Count + defender.World.Problems.Count, "Both stock wire sessions decode cleanly");
        Console.WriteLine("TRANSMIT CODE PASS: exact retained 330 hits 640/0, 606/34, 640/0, with both owned Archive bonus records and unchanged prerequisites.");
        return Array.Empty<byte>();

        async Task SetPassiveAsync(bool load)
        {
            int cursor = await StockAbilityProfile.ChangeLoadoutAsync(defender, archive, new[] { Transmit }, load);
            foreach (ushort stat in BonusStats)
                await StockAbilityProfile.ObserveAsync(defender, cursor,
                    frame => StockAbilityProfile.Bonus(frame, Transmit, stat, 10, 0, !load),
                    $"Archive publishes 593/{stat}, loaded={load}");
            Check.Equal(load ? 1L : 0L,
                await database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={defender.CharId} AND ability_id={descriptor}"),
                "Only the owned passive changes Archive membership");
            Check.Equal(parents, await database.ExecuteAsync(parentQuery), "Prerequisite Archive rows are unchanged");
        }

        async Task HitAsync(int damage, int resisted)
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
                Check.That(log.Contains("[TYPED-RESISTANCE OURS] 330 type2 total22 raw640 final606"), "One typed conversion feeds the same incoming hit");
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
