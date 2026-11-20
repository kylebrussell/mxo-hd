using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class MartialArtsInitiateResistanceMode : NativeQaMode
{
    private const ushort Aikido = CombatStyles.Aikido;                // 101
    private const ushort KungFu = CombatStyles.KungFu;                // 133
    private const ushort GuardBreaker = MartialArtsStarterAdmission.GuardBreaker; // 138
    private const ushort IronGuard = MartialArtsStarterAdmission.IronGuard;       // 139
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;

    private static readonly ushort[] Roots = { 2, 17, Aikido, KungFu, GuardBreaker, IronGuard };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-ma-initiate-resistance";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        testMobFixture: "awakened-interlock-resistance.csv",
        hotbarAbilities: new[] { Aikido, KungFu, GuardBreaker, IronGuard },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (38/100 memory), level-50 Melee Dummy 3 m west. Page 2 slots: 1=101 Aikido, 2=133 Kung-Fu, 3=138 Guard Breaker, 4=139 Iron Guard. Both 138 and 139 are closure candidates for Martial Arts Initiate.",
            "Step A (Neutral Melee Dummy): Open ordinary Close Combat, reselect Aikido (slot 1), and land Iron Guard (slot 4): expect 25 IS, 10.0 s own / 5.0 s fam 687 reuse, choreography 738, neutral physical hit 15, and caster +10 MeleeDef (1009) & +10 BallisticDef (1010) for 12 s. Reset with '?combatdepth fixture-reset', wait, strafe/turn for fresh dummy, click it, open Close Combat, reselect Kung-Fu (slot 2), run '?combatdepth offbalance 60' (Enter submits chat; do not use Escape), and land Guard Breaker (slot 3): expect 20 IS, 10.0 s own / 5.0 s fam 688 reuse, choreography 5951, neutral physical hit 47, and '[EFFECTS] 138 on Melee Dummy: Stat 1009 -10 15 s'.",
            "Step B (Resisted Melee Dummy +75 Melee Damage Resistance): Use '?combatdepth fixture-reset-resisted', wait, strafe/turn for fresh dummy, click it, open Close Combat, reselect Aikido (slot 1), and land Iron Guard (slot 4): expect '[TYPED-RESISTANCE] 139 type0 total75 raw15 final12', landed hit 12, and caster +10/+10 for 12 s. Use '?combatdepth fixture-reset-resisted' again, strafe/select, open Close Combat, reselect Kung-Fu (slot 2), run '?combatdepth offbalance 60', and land Guard Breaker (slot 3): expect '[TYPED-RESISTANCE] 138 type0 total75 raw47 final38', landed hit 38, and Stat 1009 -10 for 15 s."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "138,139",
            ["NativeCloseableRows"] = 2,
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

        // Step A: Neutral 139 Iron Guard (15) and 138 Guard Breaker (47).
        int hit139Neutral = await RunMoveAsync(Aikido, IronGuard, needsOffBalance: false, resisted: false);
        Check.Equal(15, hit139Neutral, "Neutral Iron Guard hit at Lv50 rank 50");

        dummy = await ResetAsync(resisted: false);
        int hit138Neutral = await RunMoveAsync(KungFu, GuardBreaker, needsOffBalance: true, resisted: false);
        Check.Equal(47, hit138Neutral, "Neutral Guard Breaker hit at Lv50 rank 50");

        // Step B: Resisted (+75 Melee Damage Resistance) 139 Iron Guard (12) and 138 Guard Breaker (38).
        await Task.Delay(TimeSpan.FromSeconds(11));
        dummy = await ResetAsync(resisted: true);
        int hit139Resisted = await RunMoveAsync(Aikido, IronGuard, needsOffBalance: false, resisted: true);
        Check.Equal(12, hit139Resisted, "Resisted Iron Guard hit at +75 physical resistance");

        await Task.Delay(TimeSpan.FromSeconds(11));
        dummy = await ResetAsync(resisted: true);
        int hit138Resisted = await RunMoveAsync(KungFu, GuardBreaker, needsOffBalance: true, resisted: true);
        Check.Equal(38, hit138Resisted, "Resisted Guard Breaker hit at +75 physical resistance");

        Console.WriteLine($"  MA Initiate resistance closure verified: 139 ({hit139Neutral}->{hit139Resisted}), 138 ({hit138Neutral}->{hit138Resisted})");
        return Array.Empty<byte>();

        async Task<int> RunMoveAsync(ushort style, ushort ability, bool needsOffBalance, bool resisted)
        {
            await StarterInterlockDamageMode.Open(caster, dummy);
            await StarterInterlockDamageMode.Select(caster, dummy, style);
            if (needsOffBalance)
            {
                caster.SendChat("?combatdepth offbalance 60");
                await caster.WaitForTextAsync("OffBalance fixture applied");
            }
            int from = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(ability, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId),
                0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            var rx = new Regex($@"\[ABILITY\] {ability} lands on view {dummy.ViewId} for (\d+)\b");
            await caster.WaitUntilAsync(() => rx.IsMatch(server.DebugLog()[from..]),
                $"Ability {ability} lands (resisted={resisted})", TimeSpan.FromSeconds(15));
            string slice = server.DebugLog()[from..];
            if (resisted)
                Check.That(slice.Contains($"[TYPED-RESISTANCE] {ability} type0 total{Resistance}", StringComparison.Ordinal),
                    $"Typed physical resistance logged for {ability}");
            return int.Parse(rx.Match(slice).Groups[1].Value);
        }

        async Task<ViewCreation> ResetAsync(bool resisted)
        {
            int at = caster.World.CreationSnapshot().Count;
            caster.SendChat(resisted ? "?combatdepth fixture-reset-resisted" : "?combatdepth fixture-reset");
            await caster.WaitForTextAsync(resisted ? $"with +{Resistance} Melee Damage Resistance" : "Fixture reset: Melee Dummy restored.");
            await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(at)
                .Any(view => view.Name == CombatFixtureLoadout.DummyName),
                "Fresh Melee Dummy visible after reset", HeadlessPlayer.DefaultTimeout);
            return await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        }
    }
}
