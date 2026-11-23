using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CoderInterlockSpecialsMode : NativeQaMode
{
    private const int Level = 50, CoderRank = 19, X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    private const ushort Coder = CombatStyles.CoderStyle, LowBlow = 605, Bash = 618;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Coder, LowBlow, Bash).Order().ToArray();

    // PR405 stock PacketLog 8745737faaee, lines 1190–1191.
    private const string LowBlowStock = "80b95d020400020000000000000000000000000000000000000000005d020000006b0000086b0000000000";
    private static readonly StarterSoldierResistanceMode.Sample[] StockControls =
    {
        new(LowBlow, Coder, "unarmed", 0, 0, 6, 288, StarterCoderPetBuffsAndDetonateMode.CoderStock, LowBlowStock)
    };

    public override string Name => "ability-parity-coder-interlock-specials";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[]
        {
            $"UPDATE char_abilities SET level={CoderRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Coder)}"
        },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X, Z, DummyX, Z)},healthC=4000,healthM=4000,innerStrC=800,innerStrM=1000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, LowBlow, Bash }, hotbarWithdraw: true,
        testMobFixture: "starter-soldier-resistance.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "605,618", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Level {Level}, Coder Style {CoderRank}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor CSR ({X},{Y},{Z}) faces Melee Dummy ({DummyX},{Y},{Z}) 2 m east. No helper or bag steps.",
            "Page 2 has Coder Style, Low Blow, Bash, and Withdraw. With the dummy selected, try Low Blow and Bash at rest. The stock interlock gate should hold. If a request reaches the server, expect reason 24, unchanged IS and no cast or reuse.",
            "Open Close Combat from the dummy's radial, then reselect Coder Style. Low Blow costs 30 IS with 6 s own reuse and move 288. Keep the target selected. Try Bash before Stagger; expect a disabled slot or reason 22 with state mask 8 and no IS or reuse. Submit ?combatdepth stagger 30 and retry. Retain the first complete 618 stock request.",
            "A winning Bash costs 40 IS, starts 10 s own and 5 s group reuse, and uses move 289. [MODIFIER] on Melee Dummy source=618 stat=102 value=0 for 4 s and stat=93 value=0 for 8 s, with matching removals. No 618 effects on a missed round. Retry a miss only after reuse and refresh Stagger if required.",
            "After reuse submit ?combatdepth fixture-reset-resisted and wait for the ready line and clear combat HUD. Press Enter for chat commands. Strafe and reselect if needed. Reopen Close Combat, reselect Coder, and compare the resisted Low Blow and Staggered Bash. [TYPED-RESISTANCE OURS] must show type0 total75 and lower final HP damage for both. Expect Low Blow 19 to 15 and Bash 77 to 63, from bases 17 and 66 before Coder +12 and Code Hardening +5."
        });

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) <= 1,
                $"Coder interlock stand ({x},{Y},{Z}) is grounded on outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "The grounded Melee Dummy is visible from CSR");

        // First stock Bash remains a native capture. The exact retained Low Blow
        // request exercises the same active-opponent path and both resistance states.
        return StarterSoldierResistanceMode.VerifyAsync(context, Abilities, StockControls, weaponAtLogin: 0, verifyAdmission: true);
    }
}
