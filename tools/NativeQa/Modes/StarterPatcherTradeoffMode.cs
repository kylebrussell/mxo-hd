using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterPatcherTradeoffMode : NativeQaMode
{
    private const int Level = 19, X = 24800, Y = 495, Z = -1498, EnemyZ = -1798;
    private const int FriendX = 25000, FriendZ = -1598;
    private const ushort Style = 1006, Patcher = 55, GroupRepairs = 56, LogicBarrage = 57;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Style, Patcher, GroupRepairs, LogicBarrage)
        .Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly (ushort Stat, int Value)[] Penalty = { (522, -25), (524, -18), (1032, -18), (342, -25) };

    // Retail proxy_024_chatting_hyperjump_fight lines 11689–11690. Replace only the live selection handle.
    private const string PatcherStock = "80b937000d005a0000000000000000000000000000000000000000002000000078f21200086b0000000000";
    // PR610 stock Group Repairs and native PR396 Logic Barrage.
    private const string GroupStock = "80b93800000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string LogicStock = "80b93900040002000000000000000000000000000000000000000000585cba0001000000086b0000000000";

    public override string Name => "ability-parity-starter-patcher-tradeoff";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=100,healthM=500 WHERE charId IN (110,6111,6114)",
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ} WHERE charId=6114",
            $"UPDATE characters SET x={FriendX},y={Y},z={FriendZ} WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)"
        }, DevCommands: true, hotbarAbilities: new[] { Style, Patcher, GroupRepairs, LogicBarrage },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "55,56,57", ["MemoryUsed"] = Memory, ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces hostile QAEnemy ({X},{Y},{EnemyZ}), 3 m ahead. QAFriend ({FriendX},{Y},{FriendZ}) is 2.24 m away. All three start at 100 HP with natural HP regeneration held. No bag actions.",
            """Send helper request {"actor":"QAFriend","action":"invite","target":"CSR_Tester"}, then accept. Select Hacker Style (page 2 slot 1), keep normal Speed and select QAEnemy. Press slot 4 Logic Barrage and wait for its 4 s reuse. Expect [FREECAST-VIRAL] attack input 34.""",
            "Keep QAEnemy selected and press slot 2 Patcher. Expect 22 IS, 43 s own reuse, [ABILITY] Heal 55 -> CSR_Tester for 23 (100 -> 123), and [MODIFIER] char=110 source=55 stat=522/-25, 524/-18, 1032/-18 and 342/-25, all add for 10 s. Neither helper receives a heal or a modifier from 55.",
            "Immediately use slot 4 again before 55 expires. Expect [FREECAST-VIRAL] attack input 5. After the four source-55 modifiers remove naturally, use slot 4 a third time for input 34. Keep the same target and Speed tactic.",
            "Clear selection and use slot 3 Group Repairs with QAFriend in the team. Expect 4 s cast and +9 HP on CSR (123 -> 132) and QAFriend (100 -> 109). QAEnemy gets no 56 heal. There are no maximum [VITALS] changes or [PVP] interlock rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Patcher QA needs a disposable server.");
        foreach (string handle in new[] { "QAFriend", "QAEnemy" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var friend = context.Helpers["QAFriend"];
        var enemy = context.Helpers["QAEnemy"];
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && Abilities.All(id => archive.TryGetValue(id, out ushort rank) && rank == Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Full rank-19 profile fits and arrives through Margin");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync(friend.Handle);
        await TeamBreadthMode.InviteAsync(friend, actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint selected = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(selected))}");

        int baseline = await BarrageAsync();
        Check.Equal(34, baseline, "Baseline Logic Barrage uses the normal Speed input");
        int patchAt = actor.World.RpcSnapshot().Count;
        int patchViews = actor.World.ViewSnapshot().Count;
        int patchLog = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(PatcherStock, selected));
        await EvidenceRetestWire.ViewSinceAsync(actor, patchViews, 2, $"02 80 80 80 40 {EvidenceRetestWire.Le(123)}");
        await EvidenceRetestWire.TimerAsync(actor, patchAt, Patcher, AbilityCooldowns.OwnTimer, 43);
        await ModifiersAsync(false);
        int penalized = await BarrageAsync();
        Check.Equal(5, penalized, "Patcher subtracts 25 before the Speed multiplier");
        await ModifiersAsync(true);
        Check.Equal(baseline, await BarrageAsync(), "Expiry restores the original viral accuracy input");

        actor.SendRpc("81 51 00 00 00 00");
        int allyAt = friend.World.ViewSnapshot().Count;
        int selfAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(GroupStock);
        await EvidenceRetestWire.CastBarAsync(actor, GroupRepairs, 4);
        await EvidenceRetestWire.ViewSinceAsync(friend, allyAt, 2, $"02 80 80 80 40 {EvidenceRetestWire.Le(109)}");
        await EvidenceRetestWire.ViewSinceAsync(actor, selfAt, 2, $"02 80 80 80 40 {EvidenceRetestWire.Le(132)}");
        string log = server.DebugLog()[patchLog..];
        var patchHeals = log.Split('\n').Where(line => line.Contains("[ABILITY] Heal 55 ->", StringComparison.Ordinal)).ToArray();
        Check.That(patchHeals.Length == 1 && patchHeals[0].Contains("CSR_Tester for 23", StringComparison.Ordinal),
            "The Patcher heal ignores the selected opponent and nearby teammate");
        Check.That(!log.Contains("[ABILITY] Heal 56 -> QAEnemy", StringComparison.Ordinal), "Group Repairs excludes the hostile");
        Check.That(context.Helpers.Values.All(helper => !helper.World.RpcSnapshot().Any(frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var bonus) && bonus!.SourceId == Patcher)), "Patcher penalties stay on CSR");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count + enemy.World.Problems.Count, "Patcher batch framing");
        Console.WriteLine($"PATCHER PASS: 55 HP 100 -> 123, 57 attack input {baseline} -> {penalized} -> {baseline}, 56 HP 132/109.");
        return Array.Empty<byte>();

        async Task ModifiersAsync(bool remove)
        {
            foreach (var (stat, value) in Penalty)
                await StockAbilityProfile.ObserveAsync(actor, patchAt,
                    frame => StockAbilityProfile.Bonus(frame, Patcher, stat, value, 10, remove),
                    $"Patcher stat {stat} value {value}, remove={remove}");
        }

        async Task<int> BarrageAsync()
        {
            int logAt = server.DebugLog().Length;
            int rpcAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(LogicStock, selected));
            await EvidenceRetestWire.CastBarAsync(actor, LogicBarrage, 2);
            var input = new Regex(@"\[FREECAST-VIRAL\] ability=57 source=CSR_Tester target=QAEnemy inputs=(\d+)/");
            await actor.WaitUntilAsync(() => input.IsMatch(server.DebugLog()[logAt..]), "The ordinary Logic Barrage contest ran",
                HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, LogicBarrage, AbilityCooldowns.OwnTimer, 4, ended: true);
            return int.Parse(input.Match(server.DebugLog()[logAt..]).Groups[1].Value);
        }
    }
}
