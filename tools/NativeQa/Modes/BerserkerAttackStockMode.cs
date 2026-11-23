using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class BerserkerAttackStockMode : NativeQaMode
{
    private const ushort Attack = CombatSpecials.BerserkerAttack;
    private const int X = 17000, Y = 495, Z = 2300, Level = 50;
    private const string Home = "?tp 17000 495 2300 393 11 3 - 40";
    private const string StockKarate = "80b984000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, CombatStyles.Karate, Attack).Order().ToArray();

    public override string Name => "ability-parity-berserker-attack-stock";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { "DELETE FROM inventory WHERE charId=110 AND slot=107" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.Karate, Attack },
        testMobFixture: "awakened-power-shot-cap.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "535", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Verified outdoor HOME {Home}, only if displaced. The level-3 Melee Dummy is two metres east. If behind, strafe, turn and single-click it. With the dummy visible, use ?cd fixture-reset once to make it passive without replacing its view. Page 2 Karate / Berserker Attack. No bag or helpers.",
            "Choose Karate and press 535 at rest with the dummy selected. Expect stock suppression or invalid-target refusal, with no resource debit, timer or buff. Enter its right-click Close Combat, reselect Karate and Speed, then press 535. Preserve first complete 80 b9 17 02. An ordinary missed round may retry after its 60 s reuse. The level gap keeps the contest favorable without a roll aid.",
            "On a landed attack expect 40 IS, 60 s own and 5 s group 687 reuse, zero direct ability damage, round 0x100 and animation 5939. Expect [MODIFIER] char=110 source=535 stat=159 +11, 180 +50 and 181 +50 for 21 s. The dummy receives no 535 modifier. Keep the fight open until all three records remove. No maximum [VITALS] change."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerWithdrawBonusMode.VerifyStands(context.RepositoryRoot!);
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) &&
            (home.sector, home.region, home.polygon) == (0x393, 0x11, 3), "Printed HOME matches verified NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        actor.SendChat("?cd fixture-reset");
        await actor.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle),
            WireFormat.FromHex($"40 {hex} 01 6b"));
        await actor.WaitForRpcAsync($"40 {hex} 00 6b");
        await actor.WaitForRpcAsync("47");
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockKarate, handle));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 84 00 00 00");
        Check.Equal(0, actor.World.Problems.Count, "Stock interlock and Karate setup framing");
        return Array.Empty<byte>();
    }
}
