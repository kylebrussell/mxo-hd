using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SpyMasterSneakOpenersMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle, SneakPassive = 151, SneakGrant = 1111;
    private const ushort StaggeringThrow = 348, DivingFrankensteiner = 385, FreedomToJoints = 386,
        InfuriatingAssault = 1130, WardingBlow = 1134;
    private const byte FixtureLevel = 50, FixtureRank = 50;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;

    private static readonly ushort[] Roots =
    {
        2, 17, SpyStyle, SneakPassive, StaggeringThrow, DivingFrankensteiner, FreedomToJoints,
        InfuriatingAssault, WardingBlow
    };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-spy-master-sneak-openers";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, BehindDummyX, BehindDummyZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=2500,innerStrM=2500 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { SpyStyle, SneakGrant, StaggeringThrow, InfuriatingAssault, WardingBlow, DivingFrankensteiner, FreedomToJoints },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_COMBAT_FIXTURE_DISENGAGE"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "348,385,386,1130,1134",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "348,385,386,1130,1134",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 50 rank 50 unarmed, spawned 2.5 m directly behind Melee Dummy (99/100 memory; Spy Style 1112 full closure = +54% thrown dmg). Press slot 1 (Spy Style 1112).",
            "Step A (2 s cast sneak free throws — stay outside combat; wait 15 s family 691 reuse and re-Sneak slot 2 between each). Slot 3 (Staggering Throw 348): 45 IS, 2s cast retaining Sneak, 45s/grp691 15s, 292 dmg (VF3 190 + 54%), Staggered 12 s (State 201). Slot 4 (Infuriating Assault 1130): 40 IS, 2s cast retaining Sneak, 45s/grp691 15s, 201 dmg (VF3 131 + 54%), Enraged 4 s (State 93). Slot 5 (Warding Blow 1134): 45 IS, 2s cast retaining Sneak, 45s/grp691 15s, 217 dmg (VF3 141 + 54%), Melee/Ballistic Acc (522/524) -30 for 8 s.",
            "Step B (Instant interlock sneak openers — before each: '?combatdepth fixture-disengage', wait 15 s grp691, stay behind Melee Dummy '?tp 24499 495 -1750 0 0 0' if needed, re-Sneak slot 2). Slot 6 (Diving Frankensteiner 385): 80 IS, 60s/grp691 15s, choreo 19592, 1369 dmg ((164+725)+54%). Slot 7 (Freedom to Joints 386): 60 IS, 60s/grp691 15s, choreo 19600, 1527 dmg ((92+900)+54%), Powerless 8 s (State 96)."
        });
}
