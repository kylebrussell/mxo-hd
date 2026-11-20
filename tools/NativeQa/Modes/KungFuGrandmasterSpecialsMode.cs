using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class KungFuGrandmasterSpecialsMode : NativeQaMode
{
    private const ushort KungFu = CombatStyles.KungFu;
    private const ushort TripleFrontKick = 427, SuicidalButterfly = 428,
        ExtremeFallingKick = 429, WoodenDummyDrill = 430;
    private const byte FixtureLevel = 50, FixtureRank = 50;
    private static readonly ushort[] Roots = { 2, 17, KungFu, TripleFrontKick, SuicidalButterfly, ExtremeFallingKick, WoodenDummyDrill };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-kungfu-gm-specials";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=2500,innerStrM=2500 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { KungFu, SuicidalButterfly, ExtremeFallingKick, TripleFrontKick, WoodenDummyDrill },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "427,428,429,430",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "427,428,429,430",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 50 rank 50 unarmed with Melee Dummy 3 m west (61/100 memory; Kung-Fu 133 full closure = +37% melee dmg). Enter ordinary unarmed Close Combat on Melee Dummy and press page 2 slot 1 (Kung-Fu 133).",
            "Step A (No target state needed). Press slot 2 (Suicidal Butterfly 428): expect 35 IS, 10s/grp 688 5s reuse, choreo 8084, 34 dmg (base 25 + 37%), 30% Confuse 4 s (State 91). Press slot 3 (Extreme Falling Kick 429): expect 40 IS, 10s/grp 688 5s reuse, choreo 8083, 63 dmg (base 46 + 37%), 10% Stun 4 s (State 102 + immunity).",
            "Step B (Conditional states). Select Melee Dummy, '?combatdepth offbalance 30', press slot 4 (Triple Front Kick 427, CastRequiredStates=4 Off-Balance): expect 25 IS, 10s/grp 687 5s reuse, choreo 8086, 124 dmg (base 91 + 37%), Powerless 6 s (State 96). Then '?combatdepth daze 30', press slot 5 (Wooden Dummy Drill 430, CastRequiredStates=2 Daze): expect 30 IS, 10s/grp 687 5s reuse, choreo 8080, 146 dmg (base 107 + 37%), Powerless 6 s (State 96)."
        });
}
