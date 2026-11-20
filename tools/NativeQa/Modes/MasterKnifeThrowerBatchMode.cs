using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MasterKnifeThrowerBatchMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort NeuroDart = 141, BlindingThrow = 282, DeadlyAim = 339,
        ThroatCuttingThrow = 340, SubduingThrow = 347;
    private const byte FixtureLevel = 50, FixtureRank = 50;
    private static readonly ushort[] Roots =
    {
        2, 17, SpyStyle, NeuroDart, BlindingThrow, DeadlyAim, ThroatCuttingThrow, SubduingThrow
    };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-master-knife-thrower-throws";

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
        hotbarAbilities: new ushort[] { SpyStyle, NeuroDart, SubduingThrow, ThroatCuttingThrow, BlindingThrow },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1",
            [CombatFixtureWeapons.ResetMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "141,282,339,340,347",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "141,282,339,340,347",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 50 rank 50 unarmed with Melee Dummy 3 m west (64/100 memory; Spy Style 1112 full closure = +54% thrown dmg).",
            "Step A (Deadly Aim 339 passive). Inspect Status Bonuses on login, run '?qaunload 347, 340, 339' (64 -> 56 memory: the 339 source records are Blind Duration 1041 -25 and Enrage Duration 1068 -5; the stock rows may hide. Thrown Damage Bonus stays +71), then '?qaload 339, 340, 347' restoring exact login Status.",
            "Step B (Throws outside combat — press slot 1 Spy Style 1112; run '?combatdepth fixture-reset' between casts; strafe slightly and reselect the fresh Melee Dummy after each reset). Slot 2 (Neuro-Dart 141): 20 IS, 2.0s cast, 18s/grp1215 3s, 0 direct dmg, captured base DOT total 264 scales to 406 over 15 s (68/tick), plus 8 debuffs. Run '?combatdepth fixture-reset', select Melee Dummy, '?combatdepth daze 30', slot 3 (Subduing Throw 347): 15 IS, 1.5s cast, 8s/grp1216 3s, 87 thrown dmg (base 57 + 54%), conditional Stun 4 s (State 102 + immunity). Run '?combatdepth fixture-reset', select Melee Dummy, '?combatdepth stagger 30', slot 4 (Throat Cutting Throw 340): 15 IS, 1.5s cast, 8s/grp1215 3s, 123 thrown dmg (base 80 + 54%), conditional Powerless 7.5 s (6 s base, +25% in this profile) (State 96). Run '?combatdepth fixture-reset', select Melee Dummy, '?combatdepth offbalance 30', slot 5 (Blinding Throw 282): 25 IS, 2.0s cast, 8s/grp1215 3s, Blind 12.5 s (State 90, 10 s base with +25%), ThrownDOT total 36 over 15 s (stored total 24, six per tick), plus conditional extra thrown dmg 36 (base 24 + 54%)."
        });

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) => SpyFixtureReset.VerifyAsync(context);
}
