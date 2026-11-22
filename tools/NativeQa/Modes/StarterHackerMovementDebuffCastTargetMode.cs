using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerMovementDebuffLifecycleMode;

namespace NativeQa.Modes;

public sealed class StarterHackerMovementDebuffCastTargetMode : NativeQaMode
{
    private const int X = 17000, Y = 495, CasterZ = 8200, InZ = 6800, OutZ = 6100, StartingIs = 700;
    private const ushort Freeze = 40, Slow = 86, FreezeGroup = 713;
    private static readonly NativeQaModePlan BasePlan = new StarterHackerMovementDebuffLifecycleMode().Plan;
    private sealed record Sample(ushort Id, ushort Cost, float Reuse, float Duration, string StockRequest,
        (ushort Stat, int Value)[] Bonuses);
    private static readonly Sample[] Samples =
    {
        new(Freeze, 18, 58, 25, FreezeStock, RootStats),
        new(Slow, 12, 8, 28, SlowStock, SlowStats)
    };

    public override string Name => "ability-parity-starter-hacker-movement-debuff-cast-target";

    public override NativeQaModePlan Plan { get; } = new(BasePlan.Abilities.ToArray(), X, CasterZ,
        BasePlan.BeforeLoadout.ToArray(), BasePlan.AfterLoadout.ToArray(), new[]
        {
            StarterHackerPassiveFixture.PutActor(X),
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,InZ)} WHERE charId=110",
            StarterHackerPassiveFixture.PutHelper(6114, X, 2, 3000, 3000),
            $"UPDATE characters SET z={InZ} WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, hotbarAbilities: new ushort[] { CombatStyles.HackerStyle, Freeze, Slow },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new(BasePlan.EnvironmentOverrides)
        {
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "40,86", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level 19, {BasePlan.Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(19)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{InZ}), 14 m ahead on verified outdoor NAV. Both helper stands are clear of the ice.",
            "Select Hacker Style in page 2 slot 1 and type /target QAEnemy. Prepare the OUT helper request before pressing slot 2, then submit it as soon as Code Freeze's 2 s cast bar appears. Expect [ABILITY] 40 fizzles: the selected movement-debuff target changed or left range. The cast spends 18 IS and starts 58 s own / 20 s group reuse with no [MODIFIER] on QAEnemy.",
            $$"""OUT helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{OutZ}}}. IN helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{InZ}}}. OUT is 21 m away. Keep CSR stationary and the same helper selected.""",
            "Return QAEnemy IN and repeat with Slow in slot 3. Move OUT during the 2 s bar. Expect the matching fizzle for 86, 12 IS, 8 s own reuse and no [MODIFIER]. Wait for reuse and return QAEnemy IN. If a late move lets Code Freeze land, wait for the root to clear before moving and retry the fizzle after reuse.",
            "From IN, recast slot 3, then slot 2 when its reuse clears. Expect [MODIFIER] char=6114 source=86 stats 115/877/878 -50, 519 -15 and 524 -19 for 28 s. Source=40 stat=99 value=0 and stat=519 value=-20 for 25 s. After two fizzles and two clean landings, IS is 640. No HP loss or interlock round."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { CasterZ, InZ, OutZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, X, Y, z), $"Verified outdoor NAV and sight at ({X},{Y},{z})");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(caster, BasePlan.Abilities.ToArray());
        uint handle = await StarterHackerPassiveFixture.StartAsync(caster, enemy, server, pvp: true);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        int expectedIs = StartingIs;
        int[] fizzleStarts = new int[Samples.Length];
        for (int i = 0; i < Samples.Length; i++)
        {
            var sample = Samples[i];
            int logStart = server.DebugLog().Length;
            int targetStart = enemy.World.RpcSnapshot().Count;
            int rpcStart = await StartCastAsync(sample);
            fizzleStarts[i] = rpcStart;
            await MoveAsync(OutZ);
            await caster.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
                $"[ABILITY] {sample.Id} fizzles: the selected movement-debuff target changed or left range"),
                $"Accepted {sample.Id} fizzles when its recipient leaves range", HeadlessPlayer.DefaultTimeout);
            await AssertCostAndReuseAsync(sample, rpcStart);
            enemy.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(enemy, targetStart, frame => frame.Opcode == 0x149,
                "The recipient stream advances beyond the cast");
            Check.That(!enemy.World.RpcSnapshot().Skip(targetStart).Any(frame => sample.Bonuses.Any(bonus =>
                StockAbilityProfile.Bonus(frame, sample.Id, bonus.Stat, bonus.Value, sample.Duration, false))),
                $"The fizzled {sample.Id} applies no modifier");
            await MoveAsync(InZ);
        }

        for (int i = Samples.Length - 1; i >= 0; i--)
        {
            var sample = Samples[i];
            byte[] reuseEnd = AbilityCooldowns.BuildTimer(sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, start: false);
            await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(fizzleStarts[i]).Any(frame =>
                frame.Bytes.AsSpan().SequenceEqual(reuseEnd)), $"{sample.Id} reuse ends", TimeSpan.FromSeconds(62));
            int targetStart = enemy.World.RpcSnapshot().Count;
            int rpcStart = await StartCastAsync(sample);
            await AssertCostAndReuseAsync(sample, rpcStart);
            foreach (var (stat, value) in sample.Bonuses)
                await StockAbilityProfile.ObserveAsync(enemy, targetStart, frame =>
                    StockAbilityProfile.Bonus(frame, sample.Id, stat, value, sample.Duration, false),
                    $"{sample.Id}/{stat} retains its normal duration when the target stays in range");
        }
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Movement debuff cast target wire framing");
        Console.WriteLine("MOVEMENT DEBUFF CAST TARGET PASS 40/86");
        return Array.Empty<byte>();

        async Task<int> StartCastAsync(Sample sample)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SkipReceived();
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.StockRequest, handle));
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, 2);
            return cursor;
        }

        async Task MoveAsync(int z)
        {
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = caster.World.ViewSnapshot().Count;
            enemy.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(caster, cursor, target.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }

        async Task AssertCostAndReuseAsync(Sample sample, int castCursor)
        {
            expectedIs -= sample.Cost;
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Contains((ushort)expectedIs), $"Stock {sample.Id} spends {sample.Cost} IS", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(caster, castCursor, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            if (sample.Id == Freeze)
                await EvidenceRetestWire.TimerAsync(caster, castCursor, FreezeGroup, AbilityCooldowns.GroupTimer, 20);
        }
    }
}
