using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SpyShadowAssassinBatchMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle, SneakPassive = 151, SneakGrant = 1111;
    private const ushort UncannySenses = 161, SeverArtery = 341, GhostStrike = 365,
        DisruptConnection = 384, ReverseBullDog = 388;
    private const byte FixtureLevel = 50, FixtureRank = 50;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;

    private static readonly ushort[] Roots =
    {
        2, 17, SpyStyle, SneakPassive, UncannySenses, SeverArtery, GhostStrike, DisruptConnection, ReverseBullDog
    };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-spy-shadow-assassin";

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
        hotbarAbilities: new ushort[] { SpyStyle, SneakGrant, GhostStrike, SeverArtery, ReverseBullDog },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "161,341,365,384,388",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "161,341,365,384,388",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            SpyFixtureReset.Reselect,
            "Run ?combatdepth fixture-reset between damaging casts. Let Sever Artery tick before resetting for Reverse Bull Dog, then activate Sneak once and select the fresh dummy.",
            "Level 50 rank 50 unarmed, spawned 2.5 m directly behind Melee Dummy (97/100 memory; Spy Style 1112 full closure = +54% thrown dmg).",
            "Step A (Passives 161 Uncanny Senses, 384 Disrupt Connection). Inspect Status on login, run '?qaunload 388, 384, 161' (97 -> 89 memory: Sneak Attack Detection 577 -40, Contested Withdraw Bonus 1045 77% -> 67%; Blind Duration remains 65%, Enrage Dur 1068 -5, Stun Dur 995 -15), then '?qaload 161, 384, 388' restoring exact login Status.",
            "Step B (Outside-combat Spy actives — press slot 1 Spy Style 1112 and select Melee Dummy). Slot 3 (Ghost Strike 365): 60 IS, 1.5s cast, 10s reuse, 44 thrown dmg (base 29 + 54%). Slot 4 (Sever Artery 341): 35 IS, 4.0s cast, 15s/grp1216 3s, 0 direct dmg. The stored ThrownDOT total 68 scales to 104 with Spy Style. The 30 s / 2.5 s interval produces 9 damage per tick. State procs are chance-based; a run without a state does not fail this DOT check. Then press slot 2 (Sneak 1111) behind Melee Dummy and press slot 5 (Reverse Bull Dog 388): 60 IS, 60s/grp691 15s, choreo 19595, 1934 thrown dmg ((56+1200)+54%), Stun 9.2 s (State 102, 8 s base with +15% Stun Duration) + Control Immunity 16 s."
        });

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) => SpyFixtureReset.VerifyAsync(context);
}
