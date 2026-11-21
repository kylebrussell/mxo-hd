using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedJumpLoadoutMode : NativeQaMode
{
    private const int Level = 25, X = 24800, Y = 495, Z = -1498;
    private const ushort Jump = 11, Speed = 12, Sprint = 184, SuperJump = 567;
    // The second PR526 stock jump in pr526-PacketLog, with that session's target and full clock sample.
    private const string JumpRequest = "80 c2 00 00 00 80 8d 1c d8 40 00 00 00 00 00 f0 7e 40 00 00 00 20 69 56 9f " +
        "c0 14 04 10 00 03 00 00 00 7a 44 01 10 e9 7e 10";
    // Stock 11701b9dd84e, retained in the Awakened movement lifecycle fixture.
    private const string SpeedRequest = "80b90c00000000000000000000000000000000000000000000000000000015000000071c086b0000000000";
    private const string SprintRequest = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Jump, Speed, Sprint).Order().ToArray();

    public override string Name => "ability-parity-awakened-jump-loadout";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,24885,-3247)},innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x=25100,y={Y},z=-1798 WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Jump, Speed, Sprint }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "11,12,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Rank {Level} Hyper-Jump and full prerequisites. CSR ({X},{Y},{Z}) faces down the street toward (24885,495,-3247); QAFriend (25100,495,-1798) observes clear of the ice FX. Page 2 slots 1–3 Hyper-Jump, Hyper-Speed, Hyper-Sprint. Keep selection clear.",
            "Type ?qaunload 12 first, then ?qaunload 11. Expect the owner source 11 stat 567 +75 record to remove. Try page 2 slot 1 and Ctrl+Space once, holding neither key afterward. If the stock client still sends 80 c2, retain its flag and destination. A normal jump can use the same opcode; flag 1 is the Hyper-Jump witness.",
            "Type ?qaload 11 then ?qaload 12 and confirm source 11 stat 567 +75 restores. Press slot 1, aim at visible street ground toward the printed stand and left-click once. Ctrl+Space is the fallback. Keep the packet for the destination and peak, and check one 80 c3 echo. After landing, strafe briefly. Return via ?tp 24800 495 -1498 0 0 0.",
            "Press 2 then 3, then repeat the same jump while both are active. Expect source 12 stat 115 +45 (600 s), source 184 stats 115/877/878 +15, and one clock echo. Observe landing and continued ordinary movement, with no source 11 removal or speed-toggle removal on the jump. The two activation debits are 40 and 25 IS, plus 25 per completed Sprint drain. Stop Sprint and dismiss Speed after landing. No maximum [VITALS] changes or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(archive.TryGetValue(Jump, out var rank) && rank.Level == Level &&
            Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
                AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Stock Margin supplies rank-25 Jump and all prerequisites");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await WatchBonusAsync(0, Jump, SuperJump, 75, 0, false);

        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Speed }, load: false);
        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Jump }, load: false);
        await WatchBonusAsync(removed, Jump, SuperJump, 75, 0, true);
        int added = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Jump }, load: true);
        await WatchBonusAsync(added, Jump, SuperJump, 75, 0, false);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Speed }, load: true);

        int active = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(SpeedRequest));
        await WatchBonusAsync(active, Speed, 115, 45, 600, false);
        actor.SendRpc(Convert.FromHexString(SprintRequest));
        foreach (ushort stat in new ushort[] { 115, 877, 878 })
            await WatchBonusAsync(active, Sprint, stat, 15, 0, false);

        await observer.WaitForViewNamedAsync(actor.Handle);
        int jumpAt = actor.World.RpcSnapshot().Count, viewerAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(JumpRequest);
        await actor.WaitForRpcAsync("80 c3 10 e9 7e 10");
        var movement = BytePattern.Parse("02 00 03 01 08 00 80 80 bc 00 00 7a 44 04 00 ?? ?? ?? ?? " +
            "00 00 00 80 8d 1c d8 40 00 00 00 00 00 f0 7e 40 00 00 00 20 69 56 9f c0 10 b3 01");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewerAt).Any(movement.Occurs),
            "Owner gets the stock destination and peak, with the 435 IS left after both activations", HeadlessPlayer.DefaultTimeout);
        int barrier = actor.World.RpcSnapshot().Count;
        actor.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(actor, barrier, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes), "Post-jump barrier");
        Check.Equal(1, actor.World.RpcSnapshot().Skip(jumpAt).Count(frame => BytePattern.Parse("80 c3").IsMatch(frame.Bytes)), "Exactly one jump clock echo");
        Check.That(!actor.World.RpcSnapshot().Skip(jumpAt).Any(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var mod) &&
            mod!.AddRemoveType == 1 && mod.SourceId is Jump or Speed or Sprint), "Jump movement does not remove either active speed buff or the jump passive");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Awakened jump framing");
        Console.WriteLine("AWAKENED JUMP PASS: 567 +75 -> removed -> +75, stock jump with both speed buffs, one clock echo.");
        return Array.Empty<byte>();

        Task<RpcRecord> WatchBonusAsync(int cursor, ushort source, ushort stat, int value, float duration, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, source, stat, value, duration, remove),
                $"Source {source} stat {stat} value {value}, remove={remove}");
    }
}
