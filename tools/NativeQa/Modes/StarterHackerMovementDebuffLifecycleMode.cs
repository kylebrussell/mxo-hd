using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerMovementDebuffLifecycleMode : NativeQaMode
{
    private const int Level = 19, X = 24800, Y = 495, Z = -1498, EnemyX = 25200;
    private const ushort Hacker = 1006, Freeze = 40, FreezeTwo = 41, Slow = 86, Blast = 58;
    private const ushort Root = 99, Force = 519;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Hacker, Freeze, FreezeTwo, Slow, Blast).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    internal static readonly (ushort Stat, int Value)[] RootStats = { (Root, 0), (Force, -20) };
    internal static readonly (ushort Stat, int Value)[] SlowStats = { (115, -50), (519, -15), (524, -19), (877, -50), (878, -50) };
    // Full stock requests used in PR606 and PR597. Patch only the live helper view/spawn.
    internal const string FreezeStock = "80b92800040002000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string FreezeTwoStock = "80b92900000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    internal const string SlowStock = "80b95600040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000";
    private const string BlastStock = "80b93a000400020000000000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-starter-hacker-movement-debuff-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "innerStrC=700,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z}," +
                "healthC=3000,healthM=3000,alignment=2,pvpflag=0 WHERE charId=6114",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z - 400}," +
                "healthC=3000,healthM=3000,alignment=1,pvpflag=0 WHERE charId=6113",
            "DELETE FROM char_abilities WHERE char_id IN (6113,6114)"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, Freeze, Slow, FreezeTwo, Blast }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "40,41,86", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full {Memory}/{AbilityMemory.ForLevel(Level)} profile. CSR faces QAEnemy 4 m east outside. Use Hacker Style (page 2 slot 1) and stay out of interlock. QAOther is 4 m south and allied.",
            "Select QAOther and try slots 2–4. Expect one invalid-target refusal if stock sends a request. No cast, IS debit, reuse or [MODIFIER] add on char=6113. Switch to QAEnemy. All expected hostile [MODIFIER] lines use char=6114.",
            "Slot 2 Code Freeze 1.0: source=40 stat=99 value=0, stat=519 value=-20, both duration=25s. Cast slot 5 Logic Blast after the root lands. Both source=40 records should remove on the first damaging hit. No early removal before damage.",
            "Slot 3 Slow: source=86 stat=115/-50, 519/-15, 524/-19, 877/-50, 878/-50, all duration=28s. All five matching records should remove at natural expiry. The stock cast lasts 2 s and has 8 s own reuse.",
            "Once the shared 20 s Code Freeze timer clears, slot 4 Code Freeze 2.0: source=41 stat=99 value=0, stat=519 value=-20, both duration=19s. After landing, use slot 5 once. The 50% damage-break draw can remove both records on that hit. If it keeps the root, wait for both removals at 19 s. Capture the observed branch."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        var ally = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther", stockCharacterLoad: true);
        context.Helpers.Add(ally.Handle, ally);
        await ally.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, ally);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The full ranked profile fits at level 19");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var friendly = await actor.WaitForViewNamedAsync(ally.Handle);
        uint friendlyHandle = DuelPackets.Handle(friendly.ViewId, friendly.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(friendlyHandle))}");
        int allyAt = ally.World.RpcSnapshot().Count;
        foreach (var (id, capture) in new[] { (Freeze, FreezeStock), (Slow, SlowStock), (FreezeTwo, FreezeTwoStock) })
            await StarterHackerControlTargetMode.RefusedAsync(actor, id,
                StarterHackerControlTargetMode.WithHandle(capture, friendlyHandle));
        Check.That(!ally.World.RpcSnapshot().Skip(allyAt).Any(frame =>
            RootStats.Any(stat => StockAbilityProfile.Bonus(frame, Freeze, stat.Stat, stat.Value, 25, false) ||
                StockAbilityProfile.Bonus(frame, FreezeTwo, stat.Stat, stat.Value, 19, false)) ||
            SlowStats.Any(stat => StockAbilityProfile.Bonus(frame, Slow, stat.Stat, stat.Value, 28, false))),
            "The allied observer receives none of the refused modifiers");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        int groupAt = actor.World.RpcSnapshot().Count;
        int freezeAt = await ApplyAsync(Freeze, FreezeStock, 58, 25, RootStats);
        byte[] coordinates = new[] { (float)EnemyX + 20, Y, Z }.SelectMany(BitConverter.GetBytes).ToArray();
        byte[] move = Convert.FromHexString("02000108").Concat(coordinates).Concat(new byte[] { 0, 0 }).ToArray();
        string held = Convert.ToHexString(new[] { (float)EnemyX, Y, Z }.SelectMany(BitConverter.GetBytes).ToArray());
        enemy.World.SendViewList(move);
        await enemy.WaitForViewUpdateAsync(2, $"01 0c ?? {held}");
        int damageAt = enemy.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BlastStock, handle));
        await StockAbilityProfile.ObserveAsync(enemy, damageAt, frame => frame.Bytes.AsSpan().SequenceEqual(
            AbilityMessages.Damage(false, Blast, actor.Handle, enemy.Handle, 12, 0)), "Logic Blast damages the rooted player");
        await RecordsAsync(freezeAt, Freeze, RootStats, 25, off: true, timeout: 5);
        int movedAt = actor.World.ViewSnapshot().Count;
        enemy.World.SendViewList(move);
        var moved = BytePattern.Parse($"{StockAbilityProfile.LittleEndianHex(target.ViewId)} 01 08 {Convert.ToHexString(coordinates)}");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(movedAt).Any(moved.Occurs),
            "The same helper move reaches CSR after damage breaks Code Freeze", HeadlessPlayer.DefaultTimeout);

        int slowAt = await ApplyAsync(Slow, SlowStock, 8, 28, SlowStats);
        byte[] cooldown = AbilityCooldowns.BuildTimer(713, AbilityCooldowns.GroupTimer, 20, false);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(groupAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(cooldown)),
            "Code Freeze shared reuse clears", TimeSpan.FromSeconds(23));
        int freezeTwoAt = await ApplyAsync(FreezeTwo, FreezeTwoStock, 43, 19, RootStats);
        await RecordsAsync(slowAt, Slow, SlowStats, 28, off: true, timeout: 30);
        await RecordsAsync(freezeTwoAt, FreezeTwo, RootStats, 19, off: true, timeout: 21);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + ally.World.Problems.Count, "Movement-debuff player records frame cleanly");
        return Array.Empty<byte>();

        async Task<int> ApplyAsync(ushort id, string capture, int reuse, int seconds, (ushort Stat, int Value)[] stats)
        {
            int targetAt = enemy.World.RpcSnapshot().Count;
            int castAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(capture, handle));
            await EvidenceRetestWire.CastBarAsync(actor, id, 2);
            await EvidenceRetestWire.TimerAsync(actor, castAt, id, AbilityCooldowns.OwnTimer, reuse);
            await RecordsAsync(targetAt, id, stats, seconds, off: false, timeout: 10);
            return targetAt;
        }

        async Task RecordsAsync(int cursor, ushort source, (ushort Stat, int Value)[] stats, int duration, bool off, int timeout)
        {
            foreach (var (stat, value) in stats)
                await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(cursor)
                    .Any(frame => StockAbilityProfile.Bonus(frame, source, stat, value, duration, off)),
                    $"{source}/{stat} value {value} duration {duration}, remove={off}", TimeSpan.FromSeconds(timeout));
        }
    }
}
