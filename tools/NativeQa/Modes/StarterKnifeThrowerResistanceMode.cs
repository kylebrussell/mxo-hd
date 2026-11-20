using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterKnifeThrowerResistanceMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                  // 1112
    private const ushort SneakPassive = 151, SneakGrant = Stealth.SneakGrantAbility; // 1111
    private const ushort DeadlyThrow = DeadlyThrowDamage.Id;                // 279
    private const ushort CripplingThrow = SpyAbilityDamage.CripplingThrow;  // 280
    private const ushort HarryingAttack = HarryingAttackDamage.Id;          // 1127
    private const ushort DistractingBlow = hds.DistractingBlow.Id;          // 1128
    private const ushort DazzlingThrow = DazzlingThrowDamage.Id;            // 1132
    private const byte Level = 50, Rank = 50;
    private const int Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;

    private static readonly ushort[] Roots = { 2, 17, SpyStyle, SneakPassive, DeadlyThrow, CripplingThrow, HarryingAttack, DistractingBlow, DazzlingThrow };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-starter-knife-thrower-resistance";

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
        hotbarAbilities: new[] { SpyStyle, SneakGrant, DeadlyThrow, CripplingThrow, HarryingAttack, DistractingBlow, DazzlingThrow },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (57/100 memory, +54% Thrown Inf), spawned 2.52 m behind level-50 Melee Dummy. Page 2 slots: 1=1112 Spy Style, 2=1111 Sneak, 3=279 Deadly Throw, 4=280 Crippling Throw, 5=1127 Harrying Attack, 6=1128 Distracting Blow, 7=1132 Dazzling Throw.",
            "Step A (Neutral Melee Dummy): Select Melee Dummy, slot 1 (Spy Style), test: 279 Deadly Throw (slot 3: 18 IS, 2s cast, 10s/3s fam 1216, hit 67); '?combatdepth fixture-reset', strafe/select, Spy Style + 280 Crippling Throw (slot 4: 20 IS, 2s cast, 10s/3s fam 1215, hit 1, Rooted 16.5s [15s*1.10 from 278] + Stat 519 -40 15s); '?combatdepth fixture-reset', strafe/select (step within 3.5 m behind dummy), Spy Style + Sneak (slot 2) + 1127 Harrying Attack (slot 5: 25 IS, 2s cast, 45s/15s fam 691, hit 117); wait 15s fam + reset + Sneak + 1128 Distracting Blow (slot 6: 35 IS, 2s cast, 45s/15s fam 691, hit 175, applies 1129 +50 stat 1005 12s); wait 15s fam + reset + Sneak + 1132 Dazzling Throw (slot 7: 35 IS, 2s cast, 45s/15s fam 691, hit 66, 50% Dazed 12s).",
            "Step B (Resisted Melee Dummy +75 Thrown Damage Resistance via '?combatdepth fixture-reset-thrown-resisted'): Repeat the same 5 moves against the +75 Thrown-resisted dummy (using '?combatdepth fixture-reset-thrown-resisted', strafe/select, Spy Style, and Sneak for 1127/1128/1132). Expect '[TYPED-RESISTANCE OURS] <id> type7 total75' reducing all five: 279 (67 -> 55), 280 (1 -> 0), 1127 (117 -> 96), 1128 (175 -> 143), and 1132 (66 -> 54)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "279,280,1127,1128,1132",
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
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        int n279 = await RunThrowAsync(DeadlyThrow, resisted: false);
        dummy = await ResetAsync(resisted: true);
        await Task.Delay(TimeSpan.FromSeconds(10.5));
        int r279 = await RunThrowAsync(DeadlyThrow, resisted: true);
        Check.That(r279 < n279, $"Deadly Throw resisted ({r279}) < neutral ({n279})");

        Console.WriteLine($"  Starter Knife Thrower Thrown resistance verified: 279 ({n279}->{r279})");
        return Array.Empty<byte>();

        async Task<int> RunThrowAsync(ushort ability, bool resisted)
        {
            await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);
            int from = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(ability, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId),
                0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            var rx = new Regex($@"\[ABILITY\] {ability} lands on view {dummy.ViewId} for (\d+)\b");
            await caster.WaitUntilAsync(() => rx.IsMatch(server.DebugLog()[from..]), $"Ability {ability} lands", TimeSpan.FromSeconds(10));
            string slice = server.DebugLog()[from..];
            if (resisted)
                Check.That(slice.Contains($"[TYPED-RESISTANCE OURS] {ability} type7 total{Resistance}", StringComparison.Ordinal),
                    $"Ability {ability} logs type7 resistance");
            return int.Parse(rx.Match(slice).Groups[1].Value);
        }

        async Task<ViewCreation> ResetAsync(bool resisted)
        {
            int count = caster.World.CreationSnapshot().Count;
            caster.SendChat(resisted ? "?combatdepth fixture-reset-thrown-resisted" : "?combatdepth fixture-reset");
            await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
            await caster.WaitUntilAsync(() =>
                caster.World.CreationSnapshot().Skip(count).Any(v => v.Name == CombatFixtureLoadout.DummyName),
                "Fresh Melee Dummy spawns", TimeSpan.FromSeconds(8));
            return caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        }
    }
}
