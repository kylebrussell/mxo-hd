using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerSweepBonusMode : NativeQaMode
{
    private const int Level = 50, RepairRank = 19, DedicationRank = 30, X = 17000, Y = 495, Z = 2300, EnemyX = 17400;
    private const ushort Hacker = 1006, Slow = 86, Sweep = 320, RepairAndUpgrade = 71, Dedication = 370;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, Slow, Sweep, RepairAndUpgrade, Dedication)
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (ushort Ability, int Bonus)[] Passives = { (RepairAndUpgrade, 1), (Dedication, 3) };

    public override string Name => "ability-parity-starter-hacker-sweep-bonus";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level={RepairRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(RepairAndUpgrade)}",
        $"UPDATE char_abilities SET level={DedicationRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Dedication)}"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=64,innerStrC=700,innerStrM=1000,alignment=1,pvpflag=1 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z}," +
            "rotation=192,innerStrC=700,innerStrM=1000,alignment=2,pvpflag=1 WHERE charId=6114"
    }, DevCommands: true, hotbarAbilities: new[] { Hacker, Sweep }, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
        ["MXO_BUFF_DISPEL_QA_ROLL100"] = "33", ["MXO_FREECAST_VIRAL_QA"] = "1",
        ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "71,370", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
    readyMessages: new[]
    {
        $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) 4 m east, clear of ice. Page 2 slot 1 Hacker Style, slot 2 Negative Condition Sweep 1.0. Select slot 1 and keep no target. Native helper starts ready in Hacker Style.",
        "At entry [MODIFIER] char=110 stat=1052 adds are 71 +1 and 370 +3. Prerequisites 70 +10 and 74 +5 stay fixed. In each sample request helper {\"actor\":\"QAEnemy\",\"action\":\"cast\",\"ability\":86,\"target\":\"CSR_Tester\"}. Wait for [MODIFIER] char=110 source=86 stat=115 add value=-50 duration=28s. Press slot 2 and retain the first full stock 320 request. Expect 2 s cast, 15 IS, 5 s reuse and its unchanged +18/40 s effect record.",
        "With both loaded, after 5 s expect [SWEEP ROLL] ability=320 target=CSR_Tester debuff=86 base=18 chance=37 roll=33, then [SWEEP] 320 removed 86 from CSR_Tester. Type ?dismissbuff 320 before the next helper cast. Wait for the 8 s helper reuse between samples. Stay out of interlock.",
        "Type ?qaunload 71. Repeat with 370 only: chance=36 roll=33 removes Slow. Dismiss 320; type ?qaload 71 and ?qaunload 370. Repeat with 71 only: chance=34 roll=33 removes Slow. Dismiss again. Expect matching 1052 remove/add records and no max [VITALS] change or [PVP] round.",
        "Type ?qaunload 71. Repeat with both unloaded: chance=33 roll=33 leaves Slow active. Dismiss 320 after that first pulse. Slow removes normally at 28 s. Reload both after the control. The isolated fixture fixes the Slow hit and the negative sweep callback draw to make each passive distinguishable."
    });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor NAV at ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "The helper has mapped sight to CSR");
        Check.That(AbilityCancellation.CanDismiss(Sweep), "Stock sweep can be dismissed between samples");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy");
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        enemy.SendRpc(StarterHackerViralSightMode.StockStyle);
        await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The stock archive owns all sweep prerequisites, at the required ranks and within available memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var (passive, bonus) in Passives.Concat(new (ushort, int)[] { (70, 10), (74, 5) }))
            await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, passive, 1052, bonus, 0, false),
                $"Passive {passive} publishes +{bonus} Sweep Bonus");

        foreach (var (passive, bonus) in Passives)
            foreach (bool load in new[] { false, true })
            {
                int cursor = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { passive }, load);
                await StockAbilityProfile.ObserveAsync(actor, cursor, packet => StockAbilityProfile.Bonus(packet, passive, 1052, bonus, 0, !load),
                    $"Passive {passive} round-trips only its own source record, load={load}");
                Check.That(!actor.World.RpcSnapshot().Skip(cursor).Any(packet =>
                    StockAbilityProfile.Bonus(packet, 70, 1052, 10, 0, true) || StockAbilityProfile.Bonus(packet, 74, 1052, 5, 0, true)),
                    "The fixed prerequisite bonuses remain active through the Archive change");
            }

        var target = await enemy.WaitForViewNamedAsync(actor.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        enemy.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterHackerMovementDebuffLifecycleMode.SlowStock, handle));
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Slow, 115, -50, 28, false),
            "Exact stock Slow request seeds a cleansable debuff on CSR");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Sweep fixture actor and helper have valid wire framing");
        // Claude captures the first full stock 320 request in the native checklist.
        return Array.Empty<byte>();
    }
}
