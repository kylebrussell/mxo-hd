using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class KarateAikidoMasterSpecialsMode : NativeQaMode
{
    private const ushort Karate = CombatStyles.Karate, Aikido = CombatStyles.Aikido;
    private const ushort KiChargedPunch = 265, SidekickCombo = 266, SkyHighSidekick = 267,
        CounterThrow = 273, MakiOtoshi = 274;
    private const byte FixtureLevel = 40, FixtureRank = 40;
    private static readonly ushort[] Roots = { 2, 17, Karate, Aikido, KiChargedPunch, SidekickCombo, SkyHighSidekick, CounterThrow, MakiOtoshi };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-karate-aikido-master-specials";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=2500,healthM=2500,innerStrC=2500,innerStrM=2500 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { Karate, SkyHighSidekick, SidekickCombo, KiChargedPunch, Aikido, MakiOtoshi, CounterThrow },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "265,266,267,273,274",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "265,266,267,273,274",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 40 rank 40 unarmed with Melee Dummy 3 m west (57/80 memory; Karate 132 = +34% melee dmg, Aikido 101 = +32% melee dmg). Enter ordinary unarmed Close Combat on Melee Dummy.",
            "Step A (Karate 132). Press slot 1 (Karate 132). Press slot 2 (Sky-High Sidekick 267): expect 30 IS, 10s/grp 687 5s reuse, choreo 5937, 28 dmg (base 21 + 34%), Melee/Ballistic Dmg Res (180/181) -64/-62 for 12 s. Press slot 3 (Sidekick Combo 266): expect 25 IS, 10s/grp 688 5s reuse, choreo 5936, 37 dmg (base 28 + 34%), 20% Enraged 8 s (State 93). Select Melee Dummy, '?combatdepth stagger 30', press slot 4 (Ki-Charged Punch 265, CastRequiredStates=8 Stagger): expect 30 IS, 10s/grp 688 5s reuse, choreo 5940, 91 dmg (base 68 + 34%), 30% Stun 4 s (State 102 + immunity).",
            "Step B (Aikido 101). Press slot 5 (Aikido 101). Press slot 6 (Maki-Otoshi 274): expect 30 IS, 10s/grp 688 5s reuse, choreo 803, 39 dmg (base 30 + 32%), 30% Melee Accuracy (522) -60 for 16 s. Select Melee Dummy, '?combatdepth offbalance 30', press slot 7 (Counter Throw 273, CastRequiredStates=4 Off-Balance): expect 15 IS, 10s/grp 687 5s reuse, choreo 779, 73 dmg (base 56 + 32%), Confuse 4 s (State 91)."
        });
}
