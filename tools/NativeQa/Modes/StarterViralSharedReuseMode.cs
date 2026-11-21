using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterViralSharedReuseMode : NativeQaMode
{
    private const int Level = 33, Rank = 19, X = 24800, Y = 495, Z = -1498, EnemyZ = -1798;
    private const ushort Style = CombatStyles.HackerStyle, Family = 682;
    private sealed record Sample(ushort Id, int Cast, int Reuse, int Cost, int Damage, string Stock);
    private static readonly Sample[] Samples =
    {
        new(53, 4, 8, 18, 14, HarmfulCodeMode.StockViralUse),
        new(57, 2, 4, 5, 9, StarterHackerViralSightMode.Samples[0].Capture),
        new(58, 2, 4, 6, 12, StarterHackerViralSightMode.Samples[1].Capture),
        new(59, 2, 4, 7, 19, StarterHackerViralSightMode.Samples[2].Capture)
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Samples.Select(row => row.Id)
        .Concat(new ushort[] { 2, 17, Style }).ToArray()).Order().ToArray();

    public override string Name => "ability-parity-starter-viral-shared-reuse";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)}," +
                "innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ} WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Samples.Select(row => row.Id).Prepend(Style).ToArray(),
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "53,57,58,59", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Rank-19 starter profile. QAEnemy is straight ahead at 3 m on the open stand. No ambient mobs. Select Hacker Style (page 2 slot 1) and QAEnemy; stay outside interlock. IS starts at 700 with regeneration held.",
            "Use Harmful Code (slot 2). As soon as 14 damage lands, try slot 3 Logic Barrage during the four-second group timer. Stock may suppress the click. If it sends a request, the server rejects it before cast, hit or IS debit. 53 itself costs 18 IS, casts for 4 s and starts 8 s own and 4 s family reuse.",
            "As soon as group 682 clears, try slot 2 again while its own clock is still running, then press slot 3. The still-cooling 53 is blocked while 57 casts for 2 s and hits for 9. Do the same early and then ready click with slot 4 after slot 3 lands, and slot 5 after slot 4 lands.",
            "57, 58 and 59 cost 5, 6 and 7 IS, hit for 9, 12 and 19, and each starts 4 s own and family reuse. Expected IS only changes on the four successful casts: 700 → 682 → 677 → 671 → 664. Record any early stock requests separately from suppressed clicks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete ranked viral profile fits memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        int strength = 700;
        for (int i = 0; i < Samples.Length; i++)
        {
            var sample = Samples[i];
            int started = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            int hits = enemy.World.RpcSnapshot().Count;
            actor.SendRpc(Use(sample));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, sample.Cast);
            await EvidenceRetestWire.TimerAsync(actor, started, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            await EvidenceRetestWire.TimerAsync(actor, started, Family, AbilityCooldowns.GroupTimer, 4);
            byte[] hit = AbilityMessages.Damage(false, sample.Id, actor.Handle, enemy.Handle, sample.Damage, 0);
            await StockAbilityProfile.ObserveAsync(enemy, hits, frame => frame.Bytes.AsSpan().SequenceEqual(hit), "The ready sibling hits once");
            strength -= sample.Cost;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(value => value == strength), $"Only the ready cast spends {sample.Cost} IS", HeadlessPlayer.DefaultTimeout);
            if (i + 1 < Samples.Length) await RefuseAsync(Samples[i + 1]);
            await EvidenceRetestWire.TimerAsync(actor, started, Family, AbilityCooldowns.GroupTimer, 4, ended: true);
            if (i == 0) await RefuseAsync(sample); // Harmful Code still has four seconds of its own reuse.
        }
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Viral reuse replay framing");
        return Array.Empty<byte>();

        byte[] Use(Sample row) => StarterHackerControlTargetMode.WithHandle(row.Stock, handle);

        async Task RefuseAsync(Sample row)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            int enemyAt = enemy.World.RpcSnapshot().Count;
            actor.SendRpc(Use(row));
            byte[] expected = AbilityCooldowns.BuildDenied(row.Id, AbilityCooldowns.DeniedTimerRunning);
            await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"{row.Id} is correctly blocked by its own or family reuse");
            actor.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes),
                "Post-refusal barrier");
            var bar = BytePattern.Parse($"80 ac {StockAbilityProfile.LittleEndianHex(row.Id)}");
            byte[] hit = AbilityMessages.Damage(false, row.Id, actor.Handle, enemy.Handle, row.Damage, 0);
            Check.That(!actor.World.RpcSnapshot().Skip(rpcAt).Any(frame => bar.IsMatch(frame.Bytes)) &&
                !actor.World.ViewSnapshot().Skip(viewAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(isNow => isNow < strength) &&
                !enemy.World.RpcSnapshot().Skip(enemyAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(hit)),
                "Early requests spend no IS and start neither a cast nor a hit");
        }
    }
}
