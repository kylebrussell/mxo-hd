using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class KungFuMasterSpecialsMode : NativeQaMode
{
    private const ushort KungFu = CombatStyles.KungFu;
    private const ushort DimMakStrike = 259, MisdirectPunch = 260, MachinegunFistCombo = 261, PistonKicks = 262;
    private const byte FixtureLevel = 30, FixtureRank = 30;
    private static readonly ushort[] Roots = { 2, 17, KungFu, DimMakStrike, MisdirectPunch, MachinegunFistCombo, PistonKicks };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-kungfu-master-specials";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=2000,healthM=2000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { KungFu, MisdirectPunch, MachinegunFistCombo, DimMakStrike, PistonKicks },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "259,260,261,262",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "259,260,261,262",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 30 rank 30 unarmed with Melee Dummy 3 m west (full closure with Kung-Fu 133 active = +23% melee damage). Enter ordinary unarmed Close Combat on Melee Dummy and press page 2 slot 1 (Kung-Fu 133).",
            "Step A (No target state needed). Press slot 2 (Misdirect Punch 260): expect 30 IS, 10s own / 5s group 688 reuse, choreography 8082, landed physical damage 29 (base 24 + 23%), and Blinded 4 s (State 90). Press slot 3 (Machinegun Fist Combo 261): expect 30 IS, 10s own / 5s group 687 reuse, choreography 8081, landed physical damage 28 (base 23 + 23%).",
            "Step B (Conditional states). Select Melee Dummy, run '?combatdepth daze 30', press slot 4 (Dim Mak Strike 259, CastRequiredStates=2 Daze): expect 20 IS, 10s own / 5s group 688 reuse, choreography 8089, landed physical damage 87 (base 71 + 23%), and Powerless 6 s (State 96). Then run '?combatdepth stagger 30', press slot 5 (Piston Kicks 262, CastRequiredStates=8 Stagger): expect 25 IS, 10s own / 5s group 687 reuse, choreography 8085, landed physical damage 132 (base 108 + 23%), and 40% Stun 4 s (State 102)."
        });
}
