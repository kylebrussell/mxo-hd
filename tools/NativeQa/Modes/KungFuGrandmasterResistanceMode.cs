using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class KungFuGrandmasterResistanceMode : NativeQaMode
{
    private const ushort KungFu = CombatStyles.KungFu;                                          // 133
    private const ushort TripleFrontKick = KungFuGrandmasterSpecials.TripleFrontKick;           // 427
    private const ushort SuicidalButterfly = KungFuGrandmasterSpecials.SuicidalButterfly;       // 428
    private const ushort ExtremeFallingKick = KungFuGrandmasterSpecials.ExtremeFallingKick;     // 429
    private const ushort WoodenDummyDrill = KungFuGrandmasterSpecials.WoodenDummyDrill;         // 430
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private const int StandX = 24751, StandZ = -1498;

    private static readonly ushort[] Roots = { 2, 17, KungFu, TripleFrontKick, SuicidalButterfly, ExtremeFallingKick, WoodenDummyDrill };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-kung-fu-grandmaster-resistance";

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
        hotbarAbilities: new[] { KungFu, TripleFrontKick, SuicidalButterfly, ExtremeFallingKick, WoodenDummyDrill },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (61/100 memory), spawned 2.52 m east of level-50 Melee Dummy. Page 2 slots: 1=133 Kung-Fu, 2=427 Triple Front Kick (needs Off-Balance via '?combatdepth offbalance 60'), 3=428 Suicidal Butterfly, 4=429 Extreme Falling Kick, 5=430 Wooden Dummy Drill (needs Dazed via '?combatdepth daze 60').",
            "Step A (Neutral Melee Dummy): Select Melee Dummy, open Close Combat, reselect Kung-Fu (slot 1), and run all four specials: 428 Suicidal Butterfly (slot 3: 35 IS, 10s/5s fam 688, hit 34, 30% Confused 4s), 429 Extreme Falling Kick (slot 4: 40 IS, 10s/5s fam 688, hit 63, 10% Stun 4s), '?combatdepth offbalance 60' (Enter, no Escape) + 427 Triple Front Kick (slot 2: 25 IS, 10s/5s fam 687, hit 124, Powerless 6s + 6s immunity), wait 6s for immunity or reset + '?combatdepth daze 60' + 430 Wooden Dummy Drill (slot 5: 30 IS, 10s/5s fam 687, hit 146, Powerless 6s + 6s immunity).",
            "Step B (Resisted Melee Dummy +75 Melee Damage Resistance via '?combatdepth fixture-reset-resisted'): Reset with '?combatdepth fixture-reset-resisted', strafe/select fresh dummy, Close Combat + Kung-Fu (slot 1), and repeat all four specials (seeding offbalance before 427 and daze before 430). Expect '[TYPED-RESISTANCE OURS] <id> type0 total75' reducing all four: 428 (34 -> 27), 429 (63 -> 51), 427 (124 -> 102), and 430 (146 -> 120)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "427,428,429,430",
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

        int n428 = await RunMoveAsync(SuicidalButterfly, resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(10.5));
        int r428 = await RunMoveAsync(SuicidalButterfly, resisted: true);
        Check.That(r428 < n428, $"Suicidal Butterfly resisted ({r428}) < neutral ({n428})");

        Console.WriteLine($"  Kung Fu Grandmaster resistance verified: 428 ({n428}->{r428})");
        return Array.Empty<byte>();

        async Task<int> RunMoveAsync(ushort ability, bool resisted)
        {
            await StarterInterlockDamageMode.Open(caster, dummy);
            await StarterInterlockDamageMode.Select(caster, dummy, KungFu);
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
