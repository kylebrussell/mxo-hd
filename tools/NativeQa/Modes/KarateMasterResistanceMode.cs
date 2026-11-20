using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class KarateMasterResistanceMode : NativeQaMode
{
    private const ushort Karate = CombatStyles.Karate;                                  // 132
    private const ushort KiChargedPunch = KarateAikidoMasterSpecials.KiChargedPunch;    // 265
    private const ushort SidekickCombo = KarateAikidoMasterSpecials.SidekickCombo;      // 266
    private const ushort SkyHighSidekick = KarateAikidoMasterSpecials.SkyHighSidekick;  // 267
    private const ushort SwirlingKiSummon = MartialArtsRemainingSpecials.SwirlingKiSummon; // 268
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    // Spawn 2.0 m east of Melee Dummy (24499, 495, -1498) so 268's 3 m PBAE reaches cleanly.
    private const int StandX = 24699, StandZ = -1498;

    private static readonly ushort[] Roots = { 2, 17, Karate, KiChargedPunch, SidekickCombo, SkyHighSidekick, SwirlingKiSummon };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-karate-master-resistance";

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
        hotbarAbilities: new[] { Karate, KiChargedPunch, SidekickCombo, SkyHighSidekick, SwirlingKiSummon },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (47/100 memory), spawned 2.0 m east of level-50 Melee Dummy (inside 268's 3 m PBAE radius). Page 2 slots: 1=132 Karate, 2=265 Ki-Charged Punch (needs Staggered via '?combatdepth stagger 60'), 3=266 Sidekick Combo, 4=267 Sky-High Sidekick (run LAST per pass: applies Stat 180 -64 for 12 s), 5=268 Swirling Ki Summon (3 m PBAE).",
            "Step A (Neutral Melee Dummy): Select Melee Dummy, open Close Combat, reselect Karate (slot 1), and run in order: 268 Swirling Ki Summon (slot 5, stay <= 3 m: 60 IS, 2.0s cast, 12s/5s fam 687, hit 2, Stat 522/524 -60 12s), 266 Sidekick Combo (slot 3: 25 IS, 10s/5s fam 688, hit 38, 20% Enraged 8s), '?combatdepth stagger 60' (Enter, no Escape) + 265 Ki-Charged Punch (slot 2: 30 IS, 10s/5s fam 688, hit 93, 30% Stun 4s), and 267 Sky-High Sidekick LAST (slot 4: 30 IS, 10s/5s fam 687, hit 28, Stat 180 -64 & Stat 181 -62 12s).",
            "Step B (Resisted Melee Dummy +75 Melee Damage Resistance via '?combatdepth fixture-reset-resisted'): Reset with '?combatdepth fixture-reset-resisted', strafe/select fresh dummy (stay within 2.5 m for 268), Close Combat + Karate (slot 1), and repeat the same order (268 -> 266 -> '?combatdepth stagger 60' + 265 -> 267 LAST). Expect '[TYPED-RESISTANCE OURS] <id> type0 total75' reducing all four: 268 (2 -> 1), 266 (38 -> 31), 265 (93 -> 76), and 267 (28 -> 23)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "265,266,267,268",
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

        int n266 = await RunMoveAsync(SidekickCombo, resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(10.5));
        int r266 = await RunMoveAsync(SidekickCombo, resisted: true);
        Check.That(r266 < n266, $"Sidekick Combo resisted ({r266}) < neutral ({n266})");

        Console.WriteLine($"  Karate Master resistance verified: 266 ({n266}->{r266})");
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
