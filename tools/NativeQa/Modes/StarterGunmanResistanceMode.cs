using hds;
using static NativeQa.Modes.StarterSoldierResistanceMode;

namespace NativeQa.Modes;

public sealed class StarterGunmanResistanceMode : NativeQaMode
{
    private const int Level = 50, Rank = 19;
    // Full stock requests from PR394 and the native PR404 weapon samples, also used by StarterGunSpecialAdmissionMode.
    internal static readonly Sample[] Samples =
    {
        new(126, 129, "handgun", CombatFixtureWeapons.Handgun, 1, 8, 9522,
            "80b981000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
            "80b97e000400020000000000000000000000000000000000000000007e000000006b0000086b0000000000",
            @"Stat 524 -(?:1[89]|2[0-9]|3[0-4]) 10 s"),
        new(176, 158, "smg", CombatFixtureWeapons.Smg, 1, 8, 21083,
            "80b99e000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
            "80b9b000050003000000000000000000000000000000000000000000b0000000006b0000086b0000000000"),
        new(105, 147, "rifle", CombatFixtureWeapons.Rifle, 1, 8, 11364,
            "80b993000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
            "80b9690004000200000000000000000000000000000000000000000069000000006b0000086b0000000000",
            "Stat 180 -5 10 s", "Stat 181 -5 10 s")
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Samples.SelectMany(sample => new[] { sample.Style, sample.Id })
        .Concat(new ushort[] { 2, 17 }).ToArray()).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-gunman-resistance";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,rotation=192,healthC=4000,healthM=4000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Samples.SelectMany(sample => new[] { sample.Style, sample.Id }).ToArray(),
        testMobFixture: "awakened-interlock-resistance.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1",
            [CombatFixtureWeapons.WeaponMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "105,126,176", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Full {Memory}/{AbilityMemory.ForLevel(Level)} memory at character level {Level} with rank-{Rank} starter abilities. The 30,000 HP passive Melee Dummy is ahead. Page 2 has three style/shot pairs: Handguns/126, SMGs/176, Rifles/105.",
            "Use ?combatdepth fixture-reset for neutral and ?combatdepth fixture-reset-ballistic-resisted for +75 Ballistic Resistance. Enter submits chat. Wait for the HUD to clear, strafe once and select the same dummy. Avoid Escape. Open Close Combat, reselect the matching style and use the next slot. Wait for 8 s own reuse before retrying a shot. After Covering Fire, wait for the shared 5 s timer before trying Body Shot.",
            "Handgun is equipped at login. For Disarming Shot (slot 2), expect animation 9522 and [MODIFIER] mob='Melee Dummy' source=126 stat=524 between -34 and -18 for 10 s. Expect 11 neutral and 9 resisted.",
            "Use ?combatdepth fixture-weapon smg. For Covering Fire (slot 4), expect animation 21083 on landed attempts. If the authored 15% roll procs, [MODIFIER] shows source=176 stat=99 value=0 for 8 s. Expect 18 neutral and 14 resisted whether or not the root procs.",
            "Use ?combatdepth fixture-weapon rifle for Body Shot (slot 6). Expect animation 11364 and [MODIFIER] source=105 stats 180 and 181 each -5 for 10 s. Expect 8 neutral and 6 resisted. Each resisted hit logs [TYPED-RESISTANCE OURS] type1 total75. NPC maximum HP stays 30,000."
        });

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) => VerifyAsync(context, Loadout, Samples, CombatFixtureWeapons.Handgun);
}
