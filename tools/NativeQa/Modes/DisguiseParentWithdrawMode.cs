using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class DisguiseParentWithdrawMode : NativeQaMode
{
    private const ushort Parent = 120, Mask = 1114, Sneak = 1111, HyperSpeed = 12;
    private const int X = 17000, Y = 495, Z = 2300, LaneStart = 6400, LaneEnd = 8300, Level = 50;
    private const string Home = "?tp 17000 495 2300 393 11 3 - 40";
    private const string Lane = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, CombatStyles.SpyStyle, 151, Parent, HyperSpeed)
        .Order().ToArray();

    public override string Name => "ability-parity-disguise-parent-withdraw";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { "DELETE FROM inventory WHERE charId=110 AND slot=107" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { 17, CombatStyles.SpyStyle, Sneak, HyperSpeed }, hotbarWithdraw: true,
        testMobFixture: "awakened-power-shot-cap.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1", [MovementSampleQa.Mode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "120", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"CSR starts on verified NAV facing Melee Dummy 2 m east. Return with {Home} only if displaced, then strafe, turn and single-click the visible dummy. Close Combat from the radial. Page 2 Self Defense, Spy Style, Sneak and Hyper-Speed; use the printed Withdraw slot. No Escape or bag.",
            "Keep Self Defense active. Look for permanent source=120 stat1045 +25 and stat1050 +25 at login. Press stock Withdraw. Expect [WITHDRAW INPUT OURS] CSR_Tester direct=65 input=81 bonus1045=25. Win or loss does not affect the readback.",
            "Reset between fights with ?cd fixture-reset and wait for the ready line and clear HUD. At rest ?qaunload 120, expect both +25 records removed and grant 1114 removed. Repeat stock Withdraw for direct=65 input=65 bonus1045=0. Reset, ?qaload 120, expect both records and grant restored, then repeat Withdraw for input=81.",
            $"After reset, use {Lane} for the movement sample. Choose Spy Style and Sneak. At the same gait take a forward sample for about 2 s within the verified lane to z={LaneEnd}. Off Sneak, unload 120, return to the same stand and repeat Sneak with the same gait. Compare steady [MOVE-SAMPLE] dz/ms away from onset and release. The current runtime does not read 1050 directly.",
            "For a positive movement control keep 120 unloaded, turn Sneak off, use the native Hyper-Speed slot and wait for source 12 +12 on 877. Repeat the same Sneak gait from the same stand. Save the emitted movement scale and the steady sample separately from the 1050 comparison. Turn off Sneak and notify if the two sample gaits differ. The original 1050 gameplay link remains open."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerWithdrawBonusMode.VerifyStands(context.RepositoryRoot!);
        Verify(X, Z, 0x393, 0x11, 3);
        Verify(X, LaneStart, 0x4e2, 0x12, 4);
        for (int z = LaneStart; z <= LaneEnd; z += 100)
            Check.That(NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, LaneStart, X, Y, z), $"Disguise movement lane at ({X},{Y},{z})");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await actor.EnterWorldAsync();
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        await ObserveAsync(0, false);
        await StarterHackerWithdrawBonusMode.SampleAsync(actor, server, dummy, 25, 81);
        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        await ObserveAsync(removed, true);
        await StarterHackerWithdrawBonusMode.SampleAsync(actor, server, dummy, 0, 65);
        int added = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: true);
        await ObserveAsync(added, false);
        await StarterHackerWithdrawBonusMode.SampleAsync(actor, server, dummy, 25, 81);
        Check.Equal(0, actor.World.Problems.Count, "Disguise parent withdrawal framing");
        return Array.Empty<byte>();

        async Task ObserveAsync(int cursor, bool remove)
        {
            await StockAbilityProfile.ObserveAsync(actor, cursor, StockAbilityProfile.GrantRecord(Mask, remove, rank: Level),
                $"Disguise grant membership (remove={remove})");
            foreach (ushort stat in new ushort[] { 1045, 1050 })
                await StockAbilityProfile.ObserveAsync(actor, cursor,
                    frame => StockAbilityProfile.Bonus(frame, Parent, stat, 25, 0, remove), $"Disguise {stat} (remove={remove})");
        }
    }

    private static void Verify(int x, int z, ushort sector, ushort region, ushort polygon) =>
        Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && (stand.sector, stand.region, stand.polygon) == (sector, region, polygon),
            $"Printed Disguise stand matches verified NAV ({x},{Y},{z})");
}
