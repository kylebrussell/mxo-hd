using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AikidoMasterResistanceMode : NativeQaMode
{
    private const ushort Aikido = CombatStyles.Aikido;                                    // 101
    private const ushort CounterThrow = KarateAikidoMasterSpecials.CounterThrow;          // 273
    private const ushort MakiOtoshi = KarateAikidoMasterSpecials.MakiOtoshi;              // 274
    private const ushort SpinningClayPigeon = MartialArtsRemainingSpecials.AikidoSpinningClayPigeon; // 296
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private const int StandX = 24751, StandZ = -1498;

    private static readonly ushort[] Roots = { 2, 17, Aikido, CounterThrow, MakiOtoshi, SpinningClayPigeon };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-aikido-master-resistance";

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
        hotbarAbilities: new[] { Aikido, CounterThrow, MakiOtoshi, SpinningClayPigeon },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (48/100 memory), spawned 2.52 m east of level-50 Melee Dummy. Page 2 slots: 1=101 Aikido, 2=273 Counter Throw (needs Off-Balance via '?combatdepth offbalance 60'), 3=274 Maki-Otoshi, 4=296 Aikido Spinning Clay Pigeon (needs Off-Balance via '?combatdepth offbalance 60').",
            "Step A (Neutral Melee Dummy): Select Melee Dummy, open Close Combat, reselect Aikido (slot 1), and run: 274 Maki-Otoshi (slot 3: 30 IS, 10s/5s fam 688, hit 40, 30% Stat 522 -60 16s), '?combatdepth offbalance 60' (Enter, no Escape) + 273 Counter Throw (slot 2: 15 IS, 10s/5s fam 687, hit 75, Confused State 91 4s), and 296 Aikido Spinning Clay Pigeon (slot 4: 40 IS, 40s reuse, hit 502, Confused State 91 8s; re-seed '?combatdepth offbalance 60' if expired).",
            "Step B (Resisted Melee Dummy +75 Melee Damage Resistance via '?combatdepth fixture-reset-resisted'): Reset with '?combatdepth fixture-reset-resisted', strafe/select fresh dummy, Close Combat + Aikido (slot 1), and repeat all three specials (seeding '?combatdepth offbalance 60' before 273 and 296). Expect '[TYPED-RESISTANCE OURS] <id> type0 total75' reducing all three: 274 (40 -> 32), 273 (75 -> 61), and 296 (502 -> 413)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "273,274,296",
            ["NativeCloseableRows"] = 3,
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

        int n274 = await RunMoveAsync(MakiOtoshi, resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(10.5));
        int r274 = await RunMoveAsync(MakiOtoshi, resisted: true);
        Check.That(r274 < n274, $"Maki-Otoshi resisted ({r274}) < neutral ({n274})");

        Console.WriteLine($"  Aikido Master resistance verified: 274 ({n274}->{r274})");
        return Array.Empty<byte>();

        async Task<int> RunMoveAsync(ushort ability, bool resisted)
        {
            await StarterInterlockDamageMode.Open(caster, dummy);
            await StarterInterlockDamageMode.Select(caster, dummy, Aikido);
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
