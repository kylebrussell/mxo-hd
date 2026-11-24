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
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "120", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"CSR starts on verified NAV facing Melee Dummy 2 m east. Return with {Home} only if displaced, then strafe, turn and single-click the visible dummy. Close Combat from the radial. Page 2 Self Defense, Spy Style, Sneak and Hyper-Speed; use the printed Withdraw slot. No Escape or bag.",
            "Keep Self Defense active. Look for permanent source=120 stat1045 +25 and stat1050 +25 at login. Press stock Withdraw. Expect [WITHDRAW INPUT OURS] CSR_Tester direct=65 input=81 bonus1045=25. Win or loss does not affect the readback.",
            "Reset between fights with ?cd fixture-reset and wait for the ready line and clear HUD. At rest ?qaunload 120, expect both +25 records removed and grant 1114 removed. Repeat stock Withdraw for direct=65 input=65 bonus1045=0. Reset, ?qaload 120, expect both records and grant restored, then repeat Withdraw for input=81.",
            $"After reset, use {Lane} for the movement sample. Choose Spy Style and Sneak. At the same gait take a forward sample for about 2 s within the verified lane to z={LaneEnd}. Expect self scale 0.25. Leave Sneak active and unload 120; it remains active and scale falls to zero. Return to the same stand and sample with the same gait. Reload 120 while still Sneaking and expect scale 0.25 again. Compare steady [MOVE-SAMPLE] planar distance/ms on all three legs.",
            "For a positive control unload 120 again and turn Sneak off. Use ordinary Hyper-Speed (+12 on 877) before re-entering Sneak. Expect scale 0.12 and about 1.12x the unloaded normal Sneak pace. With 120 loaded again, expect scale 0.37. Turn off Sneak to clear the owner contribution. The linked projection follows the shipped 877 to 1050 percentage relation."
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
        actor.SendChat(Lane);
        await actor.WaitForTextAsync("Teleported to 4e2/12");
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int views = actor.World.ViewSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await ScaleAsync(views, .25f);
        views = actor.World.ViewSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        await ScaleAsync(views, 0f);
        views = actor.World.ViewSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: true);
        await ScaleAsync(views, .25f);

        Check.Equal(0, actor.World.Problems.Count, "Disguise parent withdrawal framing");
        return Array.Empty<byte>();

        Task ScaleAsync(int cursor, float value)
        {
            var pattern = BytePattern.Parse(WireFormat.ToHex(
                PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(value)))));
            return actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor).Any(pattern.Occurs),
                $"Live Sneak MovementScale {value:0.00}", HeadlessPlayer.DefaultTimeout);
        }

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
