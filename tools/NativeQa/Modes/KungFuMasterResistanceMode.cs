using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class KungFuMasterResistanceMode : NativeQaMode
{
    private const ushort KungFu = CombatStyles.KungFu;                              // 133
    private const ushort DimMakStrike = KungFuMasterSpecials.DimMakStrike;          // 259
    private const ushort MisdirectPunch = KungFuMasterSpecials.MisdirectPunch;      // 260
    private const ushort MachinegunFistCombo = KungFuMasterSpecials.MachinegunFistCombo; // 261
    private const ushort PistonKicks = KungFuMasterSpecials.PistonKicks;            // 262
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    // Spawn 2.52 m east of Melee Dummy (24499, 495, -1498), clear open street stand.
    private const int StandX = 24751, StandZ = -1498;

    private static readonly ushort[] Roots = { 2, 17, KungFu, DimMakStrike, MisdirectPunch, MachinegunFistCombo, PistonKicks };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-kung-fu-master-resistance";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, StandX, StandZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        testMobFixture: "awakened-interlock-resistance.csv",
        hotbarAbilities: new[] { KungFu, DimMakStrike, MisdirectPunch, MachinegunFistCombo, PistonKicks },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (47/100 memory), spawned 2.52 m east of level-50 Melee Dummy. Page 2 slots: 1=133 Kung-Fu, 2=259 Dim Mak Strike (needs Dazed via '?combatdepth daze 60'), 3=260 Misdirect Punch, 4=261 Machinegun Fist Combo, 5=262 Piston Kicks (needs Staggered via '?combatdepth stagger 60').",
            "Step A (Neutral Melee Dummy): Select Melee Dummy, open Close Combat, reselect Kung-Fu (slot 1), and land all four specials (use '?combatdepth fixture-reset' + strafe/select + Close Combat + Kung-Fu if needed to clear immunity): 260 Misdirect Punch (slot 3: 30 IS, 10s/5s fam 688, hit 32, Blind 4s), 261 Machinegun Fist Combo (slot 4: 30 IS, 10s/5s fam 687, hit 31), '?combatdepth daze 60' (Enter, no Escape) + 259 Dim Mak Strike (slot 2: 20 IS, 10s/5s fam 688, hit 95, Powerless 6s + 6s immunity), '?combatdepth stagger 60' + 262 Piston Kicks (slot 5: 25 IS, 10s/5s fam 687, hit 145, 40% Stun 4s).",
            "Step B (Resisted Melee Dummy +75 Melee Damage Resistance via '?combatdepth fixture-reset-resisted'): Reset with '?combatdepth fixture-reset-resisted', strafe/select fresh dummy, Close Combat + Kung-Fu (slot 1), and repeat all four specials (re-seeding '?combatdepth daze 60' before 259 and '?combatdepth stagger 60' before 262). Expect '[TYPED-RESISTANCE OURS] <id> type0 total75' reducing all four: 260 (32 -> 26), 261 (31 -> 25), 259 (95 -> 78), and 262 (145 -> 119)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "259,260,261,262",
            ["NativeCloseableRows"] = 4,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        int n260 = await RunMoveAsync(MisdirectPunch, null, resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(10.5));
        int r260 = await RunMoveAsync(MisdirectPunch, null, resisted: true);
        Check.That(r260 < n260, $"Misdirect Punch resisted ({r260}) < neutral ({n260})");

        Console.WriteLine($"  Kung Fu Master resistance verified: 260 ({n260}->{r260})");
        return Array.Empty<byte>();

        async Task<int> RunMoveAsync(ushort ability, string? prepChat, bool resisted)
        {
            await StarterInterlockDamageMode.Open(caster, dummy);
            await StarterInterlockDamageMode.Select(caster, dummy, KungFu);
            if (prepChat != null)
            {
                caster.SendChat(prepChat);
                await caster.WaitForTextAsync("fixture applied");
            }
            int from = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(ability, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId),
                0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            var rx = new Regex($@"\[ABILITY\] {ability} lands on view {dummy.ViewId} for (\d+)\b");
            await caster.WaitUntilAsync(() => rx.IsMatch(server.DebugLog()[from..]), $"Ability {ability} lands", TimeSpan.FromSeconds(15));
            string slice = server.DebugLog()[from..];
            if (resisted)
                Check.That(slice.Contains($"[TYPED-RESISTANCE OURS] {ability} type0 total{Resistance}", StringComparison.Ordinal),
                    $"Ability {ability} logs type0 resistance");
            return int.Parse(rx.Match(slice).Groups[1].Value);
        }

        async Task<ViewCreation> ResetAsync(bool resisted)
        {
            int count = caster.World.CreationSnapshot().Count;
            caster.SendChat(resisted ? "?combatdepth fixture-reset-resisted" : "?combatdepth fixture-reset");
            await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
            await caster.WaitUntilAsync(() =>
                caster.World.CreationSnapshot().Skip(count).Any(v => v.Name == CombatFixtureLoadout.DummyName),
                "Fresh Melee Dummy spawns", TimeSpan.FromSeconds(8));
            return caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        }
    }
}
