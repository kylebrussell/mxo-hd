using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterCoderSplashBatchMode;

namespace NativeQa.Modes;

public sealed class StarterRunProgramContestMode : NativeQaMode
{
    private const ushort RunProgram = 32, Style = 1008;
    private const int X = 24800, Y = 495, Z = -1498, EnemyZ = -1798;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(
        Samples.Select(row => row.Id).Concat(new ushort[] { 2, 17, Style, RunProgram }).ToArray()).Order().ToArray();

    public override string Name => "ability-parity-starter-run-program-contest";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)},innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ},alignment=2,healthC=3000,healthM=3000 WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "UPDATE char_abilities SET level=50 WHERE char_id=110"
    }, DevCommands: true, hotbarAbilities: Samples.Select(row => row.Id).Prepend(Style).ToArray(),
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "32,33,36,514", ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) }, readyMessages: new[]
        {
            $"CSR at ({X},{Y},{Z}) faces QAEnemy at ({X},{Y},{EnemyZ}), 3 m ahead on clear ground. Stay out of interlock. Select Coder Style on page 2 slot 1, select the normal Speed tactic, then select QAEnemy.",
            "Run Program 32 starts loaded. Expect [MODIFIER] char=110 source=32 stat=1147 add value=+10 duration=0s. Use slot 2 Static Blast 33 (2 s cast, 6 s reuse). Expect attack input 79 in [FREECAST-VIRAL] ability=33; the hit lands.",
            "Enter ?qaunload 32 and check the [MODIFIER] char=110 source=32 stat=1147 remove value=+10 duration=0s. Use slot 3 Tinkering 36, then Static Blast again after its reuse clears. Both should land. On that same target the input for both is 73 with Speed.",
            "Enter ?qaload 32 and check the same +10 add. Use slot 4 Advanced Tinkering 514 and Static Blast once more after its reuse. Both restore attack input 79. Keep QAEnemy selected throughout. There is no interlock [PVP] round and no maximum-vital change. Run Program adds five before the Speed multiplier and truncation; native retail scaling remains open."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Run Program QA requires a disposable server.");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50) &&
            Loadout.All(id => archive.TryGetValue(id, out ushort rank) && rank == 50), "The full rank-50 profile fits in stock memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, RunProgram, 1147, 10, 0, false),
            "Loaded Run Program publishes +10 at login");
        actor.SendRpc(StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        actor.SendRpc("42 00 00 00 00 00 00"); // Stock Speed selection from PR583.
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        int original = await CastAsync(Samples[0]);
        await SetRunProgramAsync(false);
        int without = await CastAsync(Samples[1]);
        Check.Equal(79, original, "Loaded Run Program contributes five before the Speed multiplier and truncation");
        Check.Equal(73, without, "Unload removes the Run Program contribution from the viral attack input");
        Check.Equal(without, await CastAsync(Samples[0]), "Static Blast loses the same input on the same target");
        await SetRunProgramAsync(true);
        Check.Equal(original, await CastAsync(Samples[2]), "Advanced Tinkering consumes the restored passive");
        Check.Equal(original, await CastAsync(Samples[0]), "Static Blast restores its original input");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Run Program control framing");
        Console.WriteLine($"RUN-PROGRAM PASS: viral input {original} -> {without} -> {original} with source 32 stat 1147.");
        return Array.Empty<byte>();

        async Task SetRunProgramAsync(bool load)
        {
            int first = actor.World.RpcSnapshot().Count;
            actor.SendChat(load ? "?qaload 32" : "?qaunload 32");
            await StockAbilityProfile.ObserveAsync(actor, first,
                frame => StockAbilityProfile.Bonus(frame, RunProgram, 1147, 10, 0, !load),
                $"Run Program modifier {(load ? "added" : "removed")}");
        }

        async Task<int> CastAsync(Sample row)
        {
            int logStart = server.DebugLog().Length;
            int castStart = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(row.Request, handle));
            await EvidenceRetestWire.CastBarAsync(actor, row.Id, row.Cast);
            var contest = new Regex($@"\[FREECAST-VIRAL\] ability={row.Id} source=CSR_Tester target=QAEnemy inputs=(\d+)/\d+ rolls=\d+/\d+ hit=True qa=True");
            await actor.WaitUntilAsync(() => contest.IsMatch(server.DebugLog()[logStart..]), $"Ability {row.Id} lands with a seeded viral contest",
                HeadlessPlayer.DefaultTimeout);
            if (row.Id == Samples[0].Id)
                await EvidenceRetestWire.TimerAsync(actor, castStart, row.Id, AbilityCooldowns.OwnTimer, row.Reuse, ended: true);
            return int.Parse(contest.Match(server.DebugLog()[logStart..]).Groups[1].Value);
        }
    }
}
