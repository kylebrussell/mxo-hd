using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class KarateGrandmasterResistanceMode : NativeQaMode
{
    private const ushort Karate = CombatStyles.Karate;                                      // 132
    private const ushort MachinegunKick = KarateAikidoGrandmasterSpecials.MachinegunKick;   // 532
    private const ushort WristThrow = KarateAikidoGrandmasterSpecials.WristThrow;           // 534
    private const ushort KiChargedFootSweep = KarateAikidoGrandmasterSpecials.KiChargedFootSweep; // 536
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private const int StandX = 24751, StandZ = -1498;

    private static readonly ushort[] Roots = { 2, 17, Karate, MachinegunKick, WristThrow, KiChargedFootSweep };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-karate-grandmaster-resistance";

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
        hotbarAbilities: new[] { Karate, WristThrow, MachinegunKick, KiChargedFootSweep },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (60/100 memory, +43% Melee Inf), spawned 2.52 m east of level-50 Melee Dummy. Page 2 slots: 1=132 Karate, 2=534 Wrist Throw, 3=532 Machinegun Kick (needs Dazed via '?combatdepth daze 60'), 4=536 Ki-Charged Foot Sweep (needs Staggered via '?combatdepth stagger 60').",
            "Step A (Neutral Melee Dummy): Select Melee Dummy, open Close Combat, reselect Karate (slot 1), and run: 534 Wrist Throw (slot 2: 65 IS, 10s/5s fam 688, hit 44, 40% Stun 4s + immunity); '?combatdepth daze 60' (Enter, no Escape) + 532 Machinegun Kick (slot 3: 32 IS, 10s/5s fam 688, hit 104, Stun 4s + immunity); '?combatdepth stagger 60' + 536 Ki-Charged Foot Sweep (slot 4: 45 IS, 10s/5s fam 687, hit 191, 50% Powerless 6s + immunity).",
            "Step B (Resisted Melee Dummy +75 Melee Damage Resistance via '?combatdepth fixture-reset-resisted'): Reset with '?combatdepth fixture-reset-resisted', strafe/select fresh dummy, Close Combat + Karate (slot 1), and repeat all three moves (534 -> '?combatdepth daze 60' + 532 -> '?combatdepth stagger 60' + 536). Expect '[TYPED-RESISTANCE OURS] <id> type0 total75' reducing all three: 534 (44 -> 36), 532 (104 -> 85), and 536 (191 -> 157)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "532,534,536",
            ["NativeCloseableRows"] = 0,
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

        int n534 = await RunMoveAsync(WristThrow, resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(10.5));
        int r534 = await RunMoveAsync(WristThrow, resisted: true);
        Check.That(r534 < n534, $"Wrist Throw resisted ({r534}) < neutral ({n534})");

        Console.WriteLine($"  Karate Grandmaster resistance verified: 534 ({n534}->{r534})");
        return Array.Empty<byte>();

        async Task<int> RunMoveAsync(ushort ability, bool resisted)
        {
            await StarterInterlockDamageMode.Open(caster, dummy);
            await StarterInterlockDamageMode.Select(caster, dummy, Karate);
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
