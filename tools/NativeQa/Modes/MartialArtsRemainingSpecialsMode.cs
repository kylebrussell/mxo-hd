using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MartialArtsRemainingSpecialsMode : NativeQaMode
{
    private const ushort Karate = CombatStyles.Karate, Aikido = CombatStyles.Aikido;
    private const ushort SwirlingKiSummon = 268, AikidoSpinningClayPigeon = 296, KiBurst = 406;
    private const byte FixtureLevel = 50, FixtureRank = 50;
    // Melee Dummy is at (24499, 495, -1498). Spawning 2.01 m east ensures 268 (3 m PBAE) and 406 (4 m PBAE) both reach.
    private const int NearDummyX = 24700, NearDummyZ = -1498;
    private static readonly ushort[] Roots = { 2, 17, Karate, Aikido, SwirlingKiSummon, AikidoSpinningClayPigeon, KiBurst };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-ma-remaining-specials";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, NearDummyX, NearDummyZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=2500,innerStrM=2500 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { Karate, SwirlingKiSummon, Aikido, KiBurst, AikidoSpinningClayPigeon },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "268,296,406",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "268,296,406",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 50 rank 50 unarmed, 2.0 m east of Melee Dummy (64/100 memory; Karate 132 = +41% melee dmg, Aikido 101 = +38% melee dmg; inside 3 m PBAE radius for 268 and 4 m for 406).",
            "Step A (PBAEs outside combat — clear target selection). Press slot 1 (Karate 132), then slot 2 (Swirling Ki Summon 268): expect 60 IS, 2.0 s cast, 12s/grp687 5s reuse, 2 physical dmg (base 2 + 41%) on Melee Dummy, Melee/Ballistic Acc (522/524) -60 for 12 s. Press slot 3 (Aikido 101), then slot 4 (Ki Burst 406): expect 70 IS, 1.0 s cast, 15s/grp688 5s reuse, 31 physical dmg (base 23 + 38%) on Melee Dummy, 20% Enraged 15 s (State 93).",
            "Step B (Aikido interlock). Enter ordinary unarmed Close Combat on Melee Dummy, press slot 3 (Aikido 101), select Melee Dummy, '?combatdepth offbalance 30', press slot 5 (Aikido Spinning Clay Pigeon 296, CastRequiredStates=4 Off-Balance): expect 40 IS, 40s own reuse, choreo 741 (vs-tactic variant), 517 physical dmg (base 375 + 38%), Confuse 8 s (State 91)."
        });
}
