using System.Diagnostics;
using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerMovementDebuffLifecycleMode;

namespace NativeQa.Modes;

public sealed class StarterHackerDebuffLoadoutMode : NativeQaMode
{
    private const int X = 17000, Y = 495, CasterZ = 8200, EnemyZ = 6800, StartingIs = 700;
    private const ushort Freeze = 40, Slow = 86, Overload = 66, FreezeFamily = 713;
    private static readonly ushort[] LoadOrder = { Freeze, Slow, Overload };
    private static readonly ushort[] UnloadOrder = { Overload, Slow, Freeze };
    private static readonly ushort[] Abilities = StarterHackerPassiveFixture.Loadout(LoadOrder);
    private sealed record Sample(ushort Id, ushort Cost, float Reuse, float Duration, string Request, (ushort Stat, int Value)[] Bonuses);
    private static readonly Sample Root = new(Freeze, 18, 58, 25, FreezeStock, RootStats);
    private static readonly Sample Slowdown = new(Slow, 12, 8, 28, SlowStock, SlowStats);
    private static readonly Sample AccuracyDebuff = new(Overload, 14, 43, 33, HackerSightStandBatchMode.OverloadStock,
        new[] { ((ushort)522, -10), ((ushort)524, -10), ((ushort)1009, -10), ((ushort)1010, -10) });
    private static readonly Sample[] Samples = { Root, Slowdown, AccuracyDebuff };

    public override string Name => "ability-parity-starter-hacker-debuff-loadout";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, CasterZ, Array.Empty<string>(),
        new[] { StarterHackerPassiveFixture.CapRanks }, new[]
        {
            StarterHackerPassiveFixture.PutActor(X),
            $"UPDATE characters SET y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,EnemyZ)} WHERE charId=110",
            StarterHackerPassiveFixture.PutHelper(6114, X, 2, 3000, 3000),
            $"UPDATE characters SET z={EnemyZ} WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, hotbarAbilities: LoadOrder.Prepend(CombatStyles.HackerStyle).ToArray(), testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "40,66,86", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level 19, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(19)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{EnemyZ}), 14 m away on verified outdoor NAV clear of the ice. Select Hacker Style (page 2 slot 1) and /target QAEnemy.",
            "Enter separate ?qaunload commands for 66, 86, 40 in that order. Try slots 2–4. An unloaded shortcut may clear or gate locally. A sent attempt gets 80 ba reason 7 with no cast, [ABILITY-COST], reuse or recipient [MODIFIER]. Reload in order 40, 86, 66.",
            "Optional timed Slow: press slot 3 and submit ?qaunload 86 during its 2 s bar. Expect [ABILITY] 86 fizzles: the starter debuff was unloaded before landing, 12 IS and 8 s reuse. Keep 86 unloaded until the next step. The headless replay performs this check.",
            "Cast Code Freeze (slot 2) and submit ?qaunload 66 during the 2 s bar. Freeze still lands: 18 IS, 58 s own / 20 s group reuse, char=6114 source=40 stat=99 value=0 and stat=519 value=-20 for 25 s. Then ?qaunload 40. The root lasts to expiry, and reloading 40 before 58 s still leaves its reuse active.",
            "Reload 40, 86 and 66 as needed. Use loaded Slow and Overload, then unload each after its own modifiers arrive. 86 is 12 IS/8 s with stats 115/877/878 -50, 519 -15, 524 -19 for 28 s. 66 is 14 IS/43 s with stats 522/524/1009/1010 -10 for 33 s. Both sets expire on their own timers after unload, including through the other ability's Archive change. Final IS 644 with the timed Slow fizzle, or 656 without it."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { CasterZ, EnemyZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1,
                $"Verified outdoor NAV stand ({X},{Y},{z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, CasterZ, X, Y, EnemyZ), "The selected enemy stays in clear mapped sight");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var archive = StarterHackerPassiveFixture.VerifyProfile(caster, Abilities);
        uint handle = await StarterHackerPassiveFixture.StartAsync(caster, enemy, server, pvp: true);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, UnloadOrder, load: false);
        foreach (Sample sample in Samples)
            await RefuseAsync(sample, AbilityRefusals.NotLoaded);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, LoadOrder, load: true);

        int logStart = server.DebugLog().Length;
        int enemyStart = enemy.World.RpcSnapshot().Count;
        int castStart = await BeginAsync(Slowdown);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Slow }, load: false);
        await caster.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
            "[ABILITY] 86 fizzles: the starter debuff was unloaded before landing"),
            "Unloading Slow during its cast prevents the pending effect", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(caster, castStart, Slow, AbilityCooldowns.OwnTimer, Slowdown.Reuse);
        int remainingIs = StartingIs - Slowdown.Cost;
        await AssertIsAsync(remainingIs);
        await TargetBarrierAsync();
        Check.That(!Slowdown.Bonuses.Any(bonus => SawBonus(enemyStart, Slowdown, bonus, remove: false)), "No Slow modifier applied");

        var rootCast = await LandAndUnloadAsync(Root, new[] { Overload });
        await ExpireAsync(Root, rootCast);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, LoadOrder, load: true);
        await RefuseAsync(Root, AbilityRefusals.ReuseWait);

        var slowCast = await LandAndUnloadAsync(Slowdown);
        var overloadCast = await LandAndUnloadAsync(AccuracyDebuff);
        await ExpireAsync(Slowdown, slowCast);
        await ExpireAsync(AccuracyDebuff, overloadCast);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Slow, Overload }, load: true);
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Starter debuff Archive wire framing");
        Console.WriteLine("STARTER DEBUFF LOADOUT PASS 40/66/86");
        return Array.Empty<byte>();

        byte[] Request(Sample sample) => StarterHackerControlTargetMode.WithHandle(sample.Request, handle);

        Task<RpcRecord> RefuseAsync(Sample sample, uint reason) =>
            StarterHackerControlTargetMode.RefusedAsync(caster, sample.Id, Request(sample), reason);

        async Task<int> BeginAsync(Sample sample)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SkipReceived();
            caster.SendRpc(Request(sample));
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, 2);
            return cursor;
        }

        async Task<(int Cursor, Stopwatch Lifetime)> LandAndUnloadAsync(Sample sample, ushort[]? unrelatedUnload = null)
        {
            int cursor = enemy.World.RpcSnapshot().Count;
            int cast = await BeginAsync(sample);
            if (unrelatedUnload != null)
                await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, unrelatedUnload, load: false);
            await EvidenceRetestWire.TimerAsync(caster, cast, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            if (sample.Id == Freeze)
                await EvidenceRetestWire.TimerAsync(caster, cast, FreezeFamily, AbilityCooldowns.GroupTimer, 20);
            await AssertIsAsync(remainingIs -= sample.Cost);
            foreach (var (stat, value) in sample.Bonuses)
                await StockAbilityProfile.ObserveAsync(enemy, cursor,
                    frame => StockAbilityProfile.Bonus(frame, sample.Id, stat, value, sample.Duration, false),
                    $"The selected enemy receives {sample.Id}/{stat}");
            var age = Stopwatch.StartNew();
            await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { sample.Id }, load: false);
            await TargetBarrierAsync();
            Check.That(!sample.Bonuses.Any(bonus => SawBonus(cursor, sample, bonus, remove: true)), $"Unloading {sample.Id} leaves its completed effect active");
            return (cursor, age);
        }

        async Task ExpireAsync(Sample sample, (int Cursor, Stopwatch Lifetime) active)
        {
            foreach (var bonus in sample.Bonuses)
                await enemy.WaitUntilAsync(() => SawBonus(active.Cursor, sample, bonus, remove: true),
                    $"Natural removal of {sample.Id}/{bonus.Stat}", TimeSpan.FromSeconds(sample.Duration + 3));
            Check.That(Math.Abs(active.Lifetime.Elapsed.TotalSeconds - sample.Duration) < 1.5,
                $"{sample.Id} expires at its normal {sample.Duration} s boundary");
        }

        async Task TargetBarrierAsync()
        {
            int cursor = enemy.World.RpcSnapshot().Count;
            enemy.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(enemy, cursor, frame => frame.Opcode == 0x149, "The selected enemy stream advances");
        }

        Task AssertIsAsync(int expected) => caster.WaitUntilAsync(() => caster.World.ViewSnapshot()
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)expected),
            $"Ordinary cast costs leave {expected} IS", HeadlessPlayer.DefaultTimeout);

        bool SawBonus(int cursor, Sample sample, (ushort Stat, int Value) bonus, bool remove) =>
            enemy.World.RpcSnapshot().Skip(cursor).Any(frame =>
                StockAbilityProfile.Bonus(frame, sample.Id, bonus.Stat, bonus.Value, sample.Duration, remove));
    }
}
