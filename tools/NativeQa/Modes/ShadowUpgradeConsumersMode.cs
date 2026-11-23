using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class ShadowUpgradeConsumersMode : NativeQaMode
{
    private const ushort Parent = 290, Upgrade = 1117, Belt = 1116, LegSweep = 134;
    private const int Level = 50, X = 17000, Y = 495, HomeZ = 6400, LaneEnd = 8300, ObserverX = 17400;
    private const int FightZ = 2300, DummyX = 17200;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private const string FightStand = "?tp 17000 495 2300 393 11 3 - 40";
    public static IReadOnlyList<ushort> Abilities { get; } = StockAbilityProfile.Profile(2, 17, Parent, LegSweep, CombatStyles.SpyStyle)
        .Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-shadow-upgrade-consumers";
    public override NativeQaModePlan Plan { get; } = new(
        Abilities.ToArray(), X, HomeZ, Array.Empty<string>(), new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=460)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
        }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={HomeZ},rotation=0," +
                "healthC=4000,healthM=4000,innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={ObserverX},y={Y},z={HomeZ},alignment=1,pvpflag=0 WHERE charId=6111"
        }, DevCommands: true,
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Upgrade, LegSweep, CombatStyles.SelfDefense }, hotbarWithdraw: true,
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        testMobFixture: "awakened-power-shot-cap.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [MovementSampleQa.Mode] = "1", [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1117", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Memory, ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"L{Level}, rank-50 starter Shadow and Leg Sweep {Memory}/{AbilityMemory.ForLevel(Level)}. Page 2 slots 1–6: Spy Style, Shadow Upgrade, Leg Sweep, Self Defense, Withdraw, Invisibility Belt. Verify actual Shadow Upgrade in Actions > Abilities > Shadow. If the Belt shortcut does not cast, use Actions > Tools > Invisibility Belt. No teleport or target needed at startup. HOME is ({X},{Y},{HomeZ}), heading +Z, and QAFriend is at ({ObserverX},{Y},{HomeZ}).",
            $"MOVEMENT BASELINE. Choose slot 1, use the Belt and wait for the 3 s cast and [STEALTH] ... 1116 on ... 130 s. Hold forward about one second and release. Save the first and last [MOVE-SAMPLE] accepted z positions and monotonic ms. Use {Home} before each measured run. If the Belt is still on after its 30 s reuse, use it to turn it off before starting a different stance.",
            $"COMBAT BASELINE. With Belt and Shadow Upgrade off, type {FightStand}. After it settles the passive Melee Dummy is two metres east. If behind CSR, strafe and turn the camera, then single-click the dummy and choose its right-click Close Combat. After opening, select Spy Style again, press Leg Sweep on the dummy and require [COMBAT-ATTACK OURS] special 134 stat 1032 total 66. Select Self Defense and press Withdraw once for [WITHDRAW INPUT OURS] ... direct=65 input=81 bonus1045=25. The roll can miss or fail. Use ?cd fixture-reset while the dummy is in view, then wait for the ready line and clear combat HUD. Do not press Escape.",
            $"SHADOW MOVEMENT. Return with {Home}. Choose Spy Style if Self Defense is still active. Activate Shadow Upgrade in Actions or slot 2, preserve the first complete 80 b9 5d 04 request and confirm 20 IS, 30 s reuse and [MODIFIER] source=1117 adds 644 +3, 878 +10, 1032 +5 and 1005 +6. With Belt off the movement scale stays 0. Once the Belt is ready, cast it again for 133 s. Owner and QAFriend now get .10 on the actual Belt stance. Repeat the same forward sample for roughly 1.10 times the 130 s baseline speed. Compare dz / elapsed ms, keeping heading, gait and street fixed.",
            $"UNLOAD IN BELT. Keep the same Belt instance running. Type {Home}, use ?qaunload 290 and look for all four 1117 removals, MovementScale returning to 0 on both views and forward speed near the original Belt gait. The Belt keeps its original 133 s lease. Repeat the matching forward sample from HOME. Then turn the Belt off after reuse, type ?qaload 290 and verify it stays off. Reload restores the action without starting the buff.",
            $"SHADOW COMBAT. When ready, activate 1117 at rest, then {FightStand}. Select the dummy again and repeat the same Leg Sweep and Withdraw controls. Expect 71 for typed 1032 and direct=71 input=88 bonus1045=25 for Withdraw. Save the deterministic input lines, regardless of hit or withdrawal success. If a movement sample was missed, repeat that state from HOME when ordinary Belt reuse is ready."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, (LaneEnd - HomeZ) / 100 + 1).Select(step => (X, HomeZ + 100 * step));
        foreach (var (x, z) in lane.Append((ObserverX, HomeZ)).Append((X, FightZ)).Append((DummyX, FightZ)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Verified outdoor Shadow stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, HomeZ, out var home) && (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4) &&
            NavMesh.Locate(1, X, Y, FightZ, out var fight) && (fight.sector, fight.region, fight.polygon) == (0x393, 0x11, 3),
            "Both printed return commands match their NAV handles");
        Check.That(lane.Append((ObserverX, HomeZ)).All(stand => NavMesh.HasSightPath(1, X, Y, HomeZ, stand.Item1, Y, stand.Item2)) &&
            NavMesh.HasSightPath(1, X, Y, FightZ, DummyX, Y, FightZ), "Clear lane, observer and NPC sight lines");

        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && ranks.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Legal ranked Shadow and Leg Sweep profile with no archived hidden grants");
        Check.Equal(1L, await context.OwnedDatabase!.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid=460 AND slot=40"),
            "The retained Belt action uses the seeded tool slot");

        await actor.EnterWorldAsync();
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => ShadowReadyWireFixture.Member(frame.Bytes),
            "The ordinary world-ready path adds the granted Shadow action");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await observer.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int firstBelt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.BeltUse);
        await StockAbilityProfile.ObserveAsync(actor, firstBelt, frame => StockAbilityProfile.Bonus(frame, Belt, 115, -300, 130, false),
            "Retained ordinary Belt action lands the baseline 130 s stance");
        actor.SendChat(FightStand);
        await actor.WaitForTextAsync("Teleported to 393/11");
        CombatFixtureLoadout.AssertDummy(actor, await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName));
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stock setup framing");
        Console.WriteLine("SHADOW UPGRADE BOOTSTRAP PASS: legal Archive, actual outdoor stands and retained Spy/Belt. 1117 and 134 reserved for native.");
        return Array.Empty<byte>();
    }
}
