using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class KarateAikidoGrandmasterSpecialsMode : NativeQaMode
{
    private const ushort Karate = CombatStyles.Karate, Aikido = CombatStyles.Aikido;
    private const ushort PunchReversal = 407, AerialTakedown = 411, TomoNage = 412,
        MachinegunKick = 532, WristThrow = 534, KiChargedFootSweep = 536;
    private const byte FixtureLevel = 50, FixtureRank = 50;
    private static readonly ushort[] Roots =
    {
        2, 17, Karate, Aikido, PunchReversal, AerialTakedown, TomoNage,
        MachinegunKick, WristThrow, KiChargedFootSweep
    };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-karate-aikido-gm-specials";

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
        hotbarAbilities: new ushort[] { Karate, WristThrow, MachinegunKick, KiChargedFootSweep, Aikido, PunchReversal, AerialTakedown, TomoNage },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "407,411,412,532,534,536",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "407,411,412,532,534,536",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 50 rank 50 unarmed with Melee Dummy 3 m west (85/100 memory; Karate 132 = +47% melee dmg, Aikido 101 = +44% melee dmg). Enter ordinary unarmed Close Combat on Melee Dummy.",
            "Step A (Karate 132). Press slot 1 (Karate 132). Slot 2 (Wrist Throw 534): 65 IS, 10s/grp688 5s, choreo 755, 45 dmg (base 31 + 47%), 40% Stun 4 s (State 102 + immunity). Select Melee Dummy, '?combatdepth daze 30', slot 3 (Machinegun Kick 532, CastRequiredStates=2): 32 IS, 10s/grp688 5s, choreo 5934, 107 dmg (base 73 + 47%), Stun 4 s (State 102 + immunity). Wait for stun immunity or proceed, '?combatdepth stagger 30', slot 4 (Ki-Charged Foot Sweep 536, CastRequiredStates=8): 45 IS, 10s/grp687 5s, choreo 5935, 196 dmg (base 134 + 47%), 50% Powerless 6 s (State 96).",
            "Step B (Aikido 101). Press slot 5 (Aikido 101). Slot 6 (Punch Reversal 407): 40 IS, 10s/grp688 5s, choreo 827, 30 dmg (base 21 + 44%), Powerless 4 s (State 96). Select Melee Dummy, '?combatdepth offbalance 30', slot 7 (Aerial Takedown 411, CastRequiredStates=4): 25 IS, 10s/grp687 5s, choreo 735, 138 dmg (base 96 + 44%), caster +40 Ballistic Dmg Res (stat 181) 15 s + 40% Enraged 15 s (State 93). Then slot 8 (Tomo Nage 412, CastRequiredStates=4): 45 IS, 10s/grp687 5s, choreo 751, 162 dmg (base 113 + 44%), Enraged 6 s (State 93) + 40% Melee Res (180) -140 & 40% Ballistic Res (181) -100 for 12 s."
        });
}
