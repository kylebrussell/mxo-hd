using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SpySneakOpenersMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle, SneakPassive = 151, SneakGrant = 1111;
    private const ushort DropKick = 153, SpinThrow = 286, TopsyTurvy = 288, Punt = 289,
        LegThrow = 1131, WeakeningBlow = 1133;
    private const byte FixtureLevel = 40, FixtureRank = 40;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;

    private static readonly ushort[] Roots =
    {
        2, 17, SpyStyle, SneakPassive, DropKick, SpinThrow, TopsyTurvy, Punt, LegThrow, WeakeningBlow
    };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-spy-sneak-openers";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, BehindDummyX, BehindDummyZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { SpyStyle, SneakGrant, LegThrow, WeakeningBlow, DropKick, SpinThrow, TopsyTurvy, Punt },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "153,286,288,289,1131,1133",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "153,286,288,289,1131,1133",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            SpyFixtureReset.Reselect,
            "Level 40 rank 40 unarmed, spawned 2.5 m directly behind Melee Dummy (55/80 memory; Spy Style 1112 full closure = +32% thrown dmg). Press slot 1 (Spy Style 1112).",
            "Between EVERY opener (1131/1133/153/286/288/289): run '?combatdepth fixture-reset' (disengages combat, restores Melee Dummy to spawn facing +Z, teleports CSR 2.5 m behind facing it), wait 15 s family 691 reuse, press slot 2 (Sneak 1111 — do not double-press while already sneaking), select Melee Dummy, and fire the next slot.",
            "Step A (2 s cast sneak free throws, cast outside combat; enemy retaliation after landing is normal): slot 3 (Leg Throw 1131) -> 25 IS, 2s cast retaining Sneak, 45s/grp691 15s, 201 dmg (VF3 153 + 32%), 50% Off-Balance 12s (State 200); slot 4 (Weakening Blow 1133) -> 35 IS, 2s cast retaining Sneak, 45s/grp691 15s, 149 dmg (VF3 113 + 32%), stat 180/181 -45 for 8s.",
            "Step B (Instant interlock sneak openers): slot 5 (Drop Kick 153) -> 20 IS, 30s/grp691 15s, choreo 19598, 266 dmg ((52+150)+32%); slot 6 (Spin Throw 286) -> 50 IS, 38s/grp691 15s, choreo 19593, 501 dmg ((80+300)+32%); slot 7 (Topsy Turvy 288) -> 60 IS, 38s/grp691 15s, choreo 19594, 1071 dmg ((37+775)+32%), Confuse 10s (State 91); slot 8 (Punt 289) -> 40 IS, 38s/grp691 15s, choreo 19590, 976 dmg ((140+600)+32%)."
        });

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) => SpyFixtureReset.VerifyAsync(context);
}
