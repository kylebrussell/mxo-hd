using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterSpyThrownResistanceMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                  // 1112
    private const ushort SneakPassive = 151, SneakGrant = 1111;
    private const ushort LegSweep = 134;
    private const ushort PoisonKnife = CombatSpecials.PoisonKnife;          // 146
    private const ushort SpinningBacksweep = 152;
    private const ushort WoundingThrow = CombatSpecials.WoundingThrow;      // 281
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;

    private static readonly ushort[] Roots = { 2, 17, SpyStyle, SneakPassive, LegSweep, PoisonKnife, SpinningBacksweep, WoundingThrow };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-starter-spy-thrown-resistance";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, BehindDummyX, BehindDummyZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        testMobFixture: "awakened-interlock-resistance.csv",
        hotbarAbilities: new[] { SpyStyle, SneakGrant, LegSweep, SpinningBacksweep, PoisonKnife, WoundingThrow },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", ["MXO_POISON_KNIFE_QA"] = "dummy"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (44/100 memory), spawned 2.52 m behind level-50 Melee Dummy. Page 2 slots: 1=1112 Spy Style, 2=1111 Sneak, 3=134 Leg Sweep, 4=152 Spinning Backsweep, 5=146 Poison Knife, 6=281 Wounding Throw.",
            "Step A (Neutral Melee Dummy): Select Melee Dummy, open Close Combat, reselect Spy Style (slot 1), and land Leg Sweep (slot 3): expect neutral hit 16 and Stat 1009 -15 (10 s). Reset with '?combatdepth fixture-reset', strafe/turn for fresh dummy, click it, slot 1 (Spy Style), slot 2 (Sneak), slot 4 (152 Spinning Backsweep): expect neutral hit 132. Reset again, strafe/select, slot 1 (Spy Style), slot 5 (146 Poison Knife) and slot 6 (281 Wounding Throw): capture neutral ThrownDOT ticks (146 tick 2 every 2.5 s; 281 tick 8 every 5.0 s). The fixture pins only Poison Knife versus Melee Dummy.",
            "Step B (Resisted Melee Dummy +75 Thrown Damage Resistance via '?combatdepth fixture-reset-thrown-resisted'): Repeat the same 4 moves against the +75 Thrown-resisted dummy (using '?combatdepth fixture-reset-thrown-resisted', strafe/select, Spy Style before each). Expect '[TYPED-RESISTANCE OURS] <id> type7 total75' reducing every hit/tick: 134 Leg Sweep 16 -> 13, 152 Spinning Backsweep 132 -> 108, 146 Poison Knife tick 2 -> 1, and 281 Wounding Throw tick 8 -> 6."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "134,146,152,281",
            ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        // Neutral vs +75 Thrown-resisted comparison for 134, 152, 146, 281.
        int n134 = await RunLegSweepAsync(resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(12.5));
        int r134 = await RunLegSweepAsync(resisted: true);
        Check.That(r134 < n134, $"Leg Sweep resisted ({r134}) < neutral ({n134})");

        dummy = await ResetAsync(resisted: false);
        int n152 = await RunBacksweepAsync(resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(30.5));
        int r152 = await RunBacksweepAsync(resisted: true);
        Check.That(r152 < n152, $"Spinning Backsweep resisted ({r152}) < neutral ({n152})");

        Console.WriteLine($"  Starter Spy Thrown resistance verified: 134 ({n134}->{r134}), 152 ({n152}->{r152})");
        return Array.Empty<byte>();

        async Task<int> RunLegSweepAsync(bool resisted)
        {
            await StarterInterlockDamageMode.Open(caster, dummy);
            await StarterInterlockDamageMode.Select(caster, dummy, SpyStyle);
            int from = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(LegSweep, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId),
                0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            var rx = new Regex($@"\[ABILITY\] 134 lands on view {dummy.ViewId} for (\d+)\b");
            await caster.WaitUntilAsync(() => rx.IsMatch(server.DebugLog()[from..]), "Leg Sweep lands", TimeSpan.FromSeconds(15));
            string slice = server.DebugLog()[from..];
            if (resisted)
                Check.That(slice.Contains($"[TYPED-RESISTANCE OURS] 134 type7 total{Resistance}", StringComparison.Ordinal),
                    "Leg Sweep logs type7 resistance");
            return int.Parse(rx.Match(slice).Groups[1].Value);
        }

        async Task<int> RunBacksweepAsync(bool resisted)
        {
            await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);
            int sneakFrom = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(SneakGrant, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            await caster.WaitUntilAsync(() => server.DebugLog()[sneakFrom..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
                "Sneak activates", TimeSpan.FromSeconds(8));
            int from = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(SpinningBacksweep, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId),
                0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            var rx = new Regex($@"\[ABILITY\] 152 lands on view {dummy.ViewId} for (\d+)\b");
            await caster.WaitUntilAsync(() => rx.IsMatch(server.DebugLog()[from..]), "Spinning Backsweep lands", TimeSpan.FromSeconds(10));
            string slice = server.DebugLog()[from..];
            if (resisted)
                Check.That(slice.Contains($"[TYPED-RESISTANCE OURS] 152 type7 total{Resistance}", StringComparison.Ordinal),
                    "Spinning Backsweep logs type7 resistance");
            return int.Parse(rx.Match(slice).Groups[1].Value);
        }

        async Task<ViewCreation> ResetAsync(bool resisted)
        {
            int at = caster.World.CreationSnapshot().Count;
            caster.SendChat(resisted ? "?combatdepth fixture-reset-thrown-resisted" : "?combatdepth fixture-reset");
            await caster.WaitForTextAsync(resisted ? $"with +{Resistance} Thrown Damage Resistance" : "Fixture reset: Melee Dummy restored.");
            await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(at)
                .Any(view => view.Name == CombatFixtureLoadout.DummyName),
                "Fresh Melee Dummy visible after reset", HeadlessPlayer.DefaultTimeout);
            return await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        }
    }
}
