using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MasterShadowWithdrawMode : NativeQaMode
{
    private const ushort Parent = 367, Grant = 1118, Disguise = 120;
    private const int X = 17000, Y = 495, Z = 2300, Level = 50;
    private const string Home = "?tp 17000 495 2300 393 11 3 - 40";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Parent).Order().ToArray();

    public override string Name => "ability-parity-master-shadow-withdraw";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { "DELETE FROM inventory WHERE charId=110 AND slot=107" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=500,innerStrM=500 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { 17, Grant }, hotbarWithdraw: true,
        testMobFixture: "awakened-power-shot-cap.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "367", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"CSR starts on verified NAV facing Melee Dummy 2 m east. HOME {Home} only if displaced. Page 2 Self Defense / granted Master Shadow / Withdraw. No bag or helper. If the dummy is behind the camera, strafe, turn and single-click it; use radial Close Combat. Keep Self Defense. Do not press Escape.",
            "At login verify Master Shadow in Actions, and 1045 from 120 +25 and 367 +5. Leave 120 and all other ancestors loaded throughout. Open Close Combat and press stock Withdraw in slot 3, saving 44 01 if available. Expect [WITHDRAW INPUT OURS] CSR_Tester direct=65 input=84 bonus1045=30. Natural roll outcome can vary; the input line is the proof.",
            "Between samples use ?cd fixture-reset and wait for the ready line and clear combat HUD. At rest ?qaunload 367. Expect its 1045 +5 removal and the 1118 action removed; pressing stale slot 2 should be suppressed or refused, with no cast. Open again and Withdraw for direct=65 input=81 bonus1045=25. Reset, ?qaload 367, confirm one 1118 grant and 1045 +5, then repeat Withdraw for input=84. The +25 source remains in all three samples."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerWithdrawBonusMode.VerifyStands(context.RepositoryRoot!);
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) &&
            (home.sector, home.region, home.polygon) == (0x393, 0x11, 3), "HOME matches verified NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await actor.EnterWorldAsync();
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Disguise, 1045, 25, 0, false),
            "Unchanged required Disguise baseline");
        await ObserveAsync(0, false);
        await StarterHackerWithdrawBonusMode.SampleAsync(actor, server, dummy, 30, 84);
        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        await ObserveAsync(removed, true);
        await StarterHackerWithdrawBonusMode.SampleAsync(actor, server, dummy, 25, 81);
        int added = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: true);
        await ObserveAsync(added, false);
        await StarterHackerWithdrawBonusMode.SampleAsync(actor, server, dummy, 30, 84);
        Check.Equal(0, actor.World.Problems.Count, "Stock withdrawal framing");
        return Array.Empty<byte>();

        async Task ObserveAsync(int cursor, bool remove)
        {
            await StockAbilityProfile.ObserveAsync(actor, cursor, StockAbilityProfile.GrantRecord(Grant, remove),
                $"Master Shadow grant membership (remove={remove})");
            await StockAbilityProfile.ObserveAsync(actor, cursor,
                frame => StockAbilityProfile.Bonus(frame, Parent, 1045, 5, 0, remove), $"Parent 1045 (remove={remove})");
        }
    }
}
