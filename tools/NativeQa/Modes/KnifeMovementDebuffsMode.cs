using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class KnifeMovementDebuffsMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, EndZ = 8000, EnemyX = 17300;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort NeuroDart = MasterKnifeThrowerBatch.NeuroDart;           // 141
    private const ushort DeadlyThrow = DeadlyThrowDamage.Id;                      // 279
    private const ushort CripplingThrow = SpyAbilityDamage.CripplingThrow;        // 280
    private const ushort SeverArtery = CombatSpecials.SeverArtery;                // 341
    private static readonly ushort[] RunnerLoadout = { 2, 17 };
    private static readonly ushort[] CasterLoadout = DisablingShotMode.Closure(2, 17, Spy, NeuroDart, DeadlyThrow, CripplingThrow, SeverArtery)
        .Order().ToArray();

    public override string Name => "ability-parity-knife-movement-debuffs";

    public override NativeQaModePlan Plan { get; } = new(RunnerLoadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=192,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x=17200,y={Y},z=6800,rotation=192,alignment=1 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
        "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", CasterLoadout.Select((id, slot) => $"(6114,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)")),
        $"DELETE FROM inventory WHERE charId IN (110,6111,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: new ushort[] { 17 }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [MovementSampleQa.Mode] = "1", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "141,279,280,341", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "141",
            ["CasterMemory"] = CasterLoadout.Sum(AbilityMemory.Cost)
        }, readyMessages: new[]
        {
            $"CSR (Level {Level}, Self Defense only, baseline MovementScale 0.00) starts at verified outdoor HOME ({X},{Y},{Z}) facing +Z down clear lane z={Z}–{EndZ}. Use {Home} before each sample. QAEnemy ({EnemyX},{Y},{Z}, L50 Spy with 141/279/280/341) is 3 m east; friendly observer QAFriend is at (17200,{Y},6800). Take a ~2 s baseline forward run first and return HOME.",
            """Step 1 (141 Neuro-Dart): From HOME submit {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":141}. Expect 2 s cast, [MODIFIER] source=141 stats 115 -57, 877 -85, 878 -72, 519 -20, 522 -15, 524 -15, 1009 -13, 1010 -21 (15 s) + ThrownDOT. Confirm own & QAFriend MovementScale = -0.57, run ~2 s (~0.43x pace), wait 15 s expiry -> MovementScale restores 0.00. Return HOME.""",
            """Step 2 (279 Deadly Throw): From HOME submit {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":279}. Expect 2 s cast, [MODIFIER] source=279 stats 115 -57, 877 -85, 878 -72, 519 -15 (15 s). Confirm own & QAFriend MovementScale = -0.57, run ~2 s (~0.43x pace), wait 15 s expiry -> MovementScale restores 0.00. Return HOME.""",
            """Step 3 (280 Crippling Throw): From HOME submit {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":280}. Expect 2 s cast, 1 dmg hit (protected by -100 sibling), source=280 Rooted (99) for 16.5 s (rank-50 Wicked Aim +10%) + Force Combat 519 -40 (15 s). Confirm own & QAFriend MovementScale = -1.00; forward movement is held rooted until 16.5 s expiry restores MovementScale 0.00. Return HOME.""",
            """Step 4 (341 Sever Artery): From HOME submit {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":341}. Expect 4 s cast, [MODIFIER] source=341 stats 115/877/878 -50, 519 -10 (30 s) + ThrownDOT. Confirm own & QAFriend MovementScale = -0.50, run ~2 s (~0.50x pace), wait 30 s expiry -> MovementScale restores 0.00."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Knife movement debuffs mode requires NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, (EndZ - Z) / 100 + 1).Select(i => (X, Z + 100 * i));
        foreach (var (x, z) in lane.Append((EnemyX, Z)).Append((17200, 6800)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Verified outdoor stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && home.sector == 0x4e2 && home.region == 0x12 && home.polygon == 4,
            "Printed HOME matches actual NAV");

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var enemy = context.Helpers["QAEnemy"];
        DisablingShotMode.CheckProfile(enemy, CasterLoadout, Level);
        enemy.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        if (context.StockProbe is not { } runner) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(runner, RunnerLoadout, Level);
        await PvpOpponentsFixture.EnterAsync(runner);
        NativeQaReadiness.RequireNoAmbientAggro(server, runner);
        var observer = context.Helpers["QAFriend"];
        var observerView = await observer.WaitForViewNamedAsync(runner.Handle);

        // Step 1: 141 Neuro-Dart (-0.57 for 15 s -> 0.00).
        await VerifySlowAsync(NeuroDart, 2, 15, -.57f,
            (115, -57), (877, -85), (878, -72), (519, -20), (522, -15), (524, -15), (1009, -13), (1010, -21));

        // Step 2: 279 Deadly Throw (-0.57 for 15 s -> 0.00).
        await VerifySlowAsync(DeadlyThrow, 2, 15, -.57f,
            (115, -57), (877, -85), (878, -72), (519, -15));

        // Step 3: 280 Crippling Throw (Rooted 16.5 s -> -1.00 -> 0.00).
        int rpcAt = runner.World.RpcSnapshot().Count;
        int ownAt = runner.World.ViewSnapshot().Count, watchAt = observer.World.ViewSnapshot().Count;
        await SendSampleAsync(enemy, runner.Handle, CripplingThrow);
        await EvidenceRetestWire.CastBarAsync(enemy, CripplingThrow, 2);
        await ObserveAsync(runner, rpcAt, f => StateRecord(f, CripplingThrow, TempMods.RootedAbility, 16.5f, false),
            "280 applies 16.5 s Rooted");
        await ObserveAsync(runner, rpcAt, f => Bonus(f, CripplingThrow, 519, -40, 15, false),
            "280 applies 15 s Force Combat -40");
        await CheckScaleAsync(ownAt, watchAt, -1f);

        ownAt = runner.World.ViewSnapshot().Count;
        watchAt = observer.World.ViewSnapshot().Count;
        await runner.WaitUntilAsync(() => runner.World.RpcSnapshot().Skip(rpcAt).Any(f =>
            StateRecord(f, CripplingThrow, TempMods.RootedAbility, 16.5f, true)),
            "280 Rooted expires after 16.5 s", TimeSpan.FromSeconds(22));
        await CheckScaleAsync(ownAt, watchAt, 0f);

        // Step 4: 341 Sever Artery (-0.50 for 30 s -> 0.00).
        await VerifySlowAsync(SeverArtery, 4, 30, -.50f,
            (115, -50), (877, -50), (878, -50), (519, -10));

        Check.Equal(0, runner.World.Problems.Count + enemy.World.Problems.Count + observer.World.Problems.Count,
            "Knife movement debuffs framing");
        Console.WriteLine("KNIFE MOVEMENT DEBUFFS PASS: 141 (-0.57), 279 (-0.57), 280 Root (-1.00), and 341 (-0.50) projected and restored on self & observer.");
        return Array.Empty<byte>();

        async Task VerifySlowAsync(ushort ability, float castSeconds, float duration, float expectedScale, params (ushort Stat, int Value)[] stats)
        {
            int fromRpc = runner.World.RpcSnapshot().Count;
            int fromOwn = runner.World.ViewSnapshot().Count, fromWatch = observer.World.ViewSnapshot().Count;
            await SendSampleAsync(enemy, runner.Handle, ability);
            await EvidenceRetestWire.CastBarAsync(enemy, ability, castSeconds);
            foreach (var (stat, value) in stats)
                await ObserveAsync(runner, fromRpc, f => Bonus(f, ability, stat, value, duration, false),
                    $"{ability} adds stat {stat} {value} for {duration} s");
            await CheckScaleAsync(fromOwn, fromWatch, expectedScale);

            fromOwn = runner.World.ViewSnapshot().Count;
            fromWatch = observer.World.ViewSnapshot().Count;
            await runner.WaitUntilAsync(() => runner.World.RpcSnapshot().Skip(fromRpc).Any(f =>
                Bonus(f, ability, 115, stats[0].Value, duration, true)),
                $"{ability} slow expires after {duration} s", TimeSpan.FromSeconds(duration + 6));
            await CheckScaleAsync(fromOwn, fromWatch, 0f);
        }

        async Task CheckScaleAsync(int fromOwn, int fromWatch, float scale)
        {
            await EvidenceRetestWire.ViewSinceAsync(runner, fromOwn, 2,
                WireFormat.ToHex(PlayerViewAttributes.Build((PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(scale)))));
            await EvidenceRetestWire.ViewSinceAsync(observer, fromWatch, observerView.ViewId,
                WireFormat.ToHex(PlayerViewAttributes.Build((PlayerViewAttributes.OtherMovementScale, PlayerViewAttributes.F32(scale)))));
        }
    }

    internal static async Task<object> SendSampleAsync(HeadlessPlayer helper, string target, ushort ability)
    {
        Check.That(helper.Handle == "QAEnemy" && target == "CSR_Tester" &&
            ability is NeuroDart or DeadlyThrow or CripplingThrow or SeverArtery,
            "Knife movement sample supports QAEnemy -> CSR_Tester for 141, 279, 280, or 341 only");
        var view = await helper.WaitForViewNamedAsync(target);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        byte[] request = new AbilityUseRecord(ability, handle, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
        helper.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        helper.SendRpc(request);
        return new { Ability = ability, Target = target, Request = Convert.ToHexString(request) };
    }

    private static bool StateRecord(RpcRecord frame, ushort source, ushort state, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindState &&
        r.SourceId == source && r.TargetId == state && r.SignedValue == 0 &&
        Math.Abs(r.Duration - seconds) < 0.01 && r.AddRemoveType == (remove ? 1 : 0);
}
