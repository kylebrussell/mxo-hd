using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerDotResistanceMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498, Level = 18;
    private const ushort Hacker = 1006, Lag = 69, Blast = 58, Barrage = 59;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Hacker, Lag, Blast, Barrage).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    // Complete stock activation for 69 from PR439; 58/59 from PR396. Patch only the live target handle.
    private const string LagUse = "80b94500040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000";
    private const string BlastUse = "80b93a000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string BarrageUse = "80b93b00040002000000000000000000000000000000000000000000585cba0001000000086b0000000000";
    private const string HackerUse = StarterHackerViralSightMode.StockStyle;

    public override string Name => "ability-parity-starter-hacker-dot-resistance";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+400,Z)},innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, Lag, Blast, Barrage },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "69,58,59", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR starts on the outdoor stand facing east. Select Hacker Style (page 2 slot 1), then enter ?combatdepth foundations-viral. Viral Neutral is 4 m ahead and Viral Resisted is southeast, with +75 Viral Resistance. The viral hit draw is fixed.",
            "Start Processor Lag (slot 2) on Viral Neutral. Expect a 2 s cast, 10 IS, 43 s reuse, stored damage 35 for 30 s, and five 6-point ticks at 5 s intervals. Wait for slot 2 reuse before casting it on Resisted. Expect five 3-point ticks and no sixth on either target.",
            "Fill the reuse wait with the two direct comparisons. Logic Blast (slot 3) hits Neutral/Resisted for 12/6, costs 6 IS and starts 4 s reuse. Logic Barrage 2.0 (slot 4) hits for 19/10, costs 7 IS and starts 4 s reuse. Both cast in 2 s. Wait for the slot to recover before changing targets.",
            "Finish with sight and range confirmation: when slot 2 is ready, run ?combatdepth foundations-sight-pick, select QA Sight Target 2 m ahead, then enter ?combatdepth foundations-sight-far without Escape. Try slot 2 once. Expect a range refusal with no cast, IS debit, or timer if sent. Enter ?combatdepth foundations-sight-blocked and try the same held selection. The cast may start and use its timer, but must apply no DOT and run no hit contest."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(requirement =>
                archive.TryGetValue(requirement.Key, out ushort rank) && rank >= requirement.Value)),
            "The stock profile contains all three viruses and their ranked prerequisites");
        Check.That(Memory <= AbilityMemory.ForLevel(Level), "The virus profile fits at level 18");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(HackerUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        actor.SendChat("?combatdepth foundations-viral");
        await actor.WaitForTextAsync("Viral fixture ready.");
        var neutral = await actor.WaitForViewNamedAsync("Viral Neutral");
        var resisted = await actor.WaitForViewNamedAsync("Viral Resisted");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StarterHackerControlTargetMode.RefusedAsync(actor, Lag, StarterHackerControlTargetMode.WithHandle(LagUse, 0));
        await Task.Delay(800); // Stay outside the repeat guard after the deliberate invalid request.
        var first = await StartLagAsync(neutral);
        await CompareDirectAsync(Blast, BlastUse, 12, 6);
        await CompareDirectAsync(Barrage, BarrageUse, 19, 10);
        await CheckTicksAsync(neutral, first, 6);

        var second = await StartLagAsync(resisted);
        await CheckTicksAsync(resisted, second, 3);
        Check.That(server.DebugLog()[second.Log..].Contains("69 type2 total75 raw6 final3", StringComparison.Ordinal),
            "Each Processor Lag tick consumes the selected victim's typed resistance");

        actor.SendChat("?combatdepth foundations-sight-pick");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        var sight = await actor.WaitForViewNamedAsync("QA Sight Target");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(Handle(sight)))}");
        actor.SendChat("?combatdepth foundations-sight-far");
        await actor.WaitForTextAsync("QA Sight Target is beyond 20 m");
        await StarterHackerControlTargetMode.RefusedAsync(actor, Lag,
            StarterHackerControlTargetMode.WithHandle(LagUse, Handle(sight)), AbilityRefusals.TargetOutOfRange);
        await SightStandCastProof.CheckBlockedAsync(context, sight, new[] { (Lag, 43f, LagUse) });

        Check.Equal(0, actor.World.Problems.Count, "Direct and periodic viral resistance framing");
        return Array.Empty<byte>();

        async Task<(int Rpc, int Log)> StartLagAsync(ViewCreation target)
        {
            int timerAt = actor.World.RpcSnapshot().Count;
            int logAt = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(LagUse, Handle(target)));
            await EvidenceRetestWire.CastBarAsync(actor, Lag, 2);
            await EvidenceRetestWire.TimerAsync(actor, timerAt, Lag, AbilityCooldowns.OwnTimer, 43);
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(
                $"[EFFECTS] 69 on {target.Name}: Dot 1157 35 30 s", StringComparison.Ordinal),
                $"Processor Lag snapshots the 35-point DOT on {target.Name}", HeadlessPlayer.DefaultTimeout);
            return (timerAt, logAt);
        }

        async Task CheckTicksAsync(ViewCreation target, (int Rpc, int Log) cursor, int expected)
        {
            byte[] ready = AbilityCooldowns.BuildTimer(Lag, AbilityCooldowns.OwnTimer, 43, false);
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor.Rpc).Any(packet => packet.Bytes.AsSpan().SequenceEqual(ready)),
                $"Processor Lag reuse clears after the DOT on {target.Name} has expired", TimeSpan.FromSeconds(44));
            var tick = new Regex($@"\[EFFECTS\] 69 ticks on {Regex.Escape(target.Name ?? "")} for (\d+)");
            int[] ticks = tick.Matches(server.DebugLog()[cursor.Log..]).Select(match => int.Parse(match.Groups[1].Value)).ToArray();
            Check.That(ticks.SequenceEqual(Enumerable.Repeat(expected, 5)), $"{target.Name}: five {expected}-point Processor Lag ticks");
        }

        async Task CompareDirectAsync(ushort id, string stock, int unresisted, int reduced)
        {
            foreach (var (target, amount) in new[] { (neutral, unresisted), (resisted, reduced) })
            {
                int timerAt = actor.World.RpcSnapshot().Count;
                int logAt = server.DebugLog().Length;
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, Handle(target)));
                await EvidenceRetestWire.CastBarAsync(actor, id, 2);
                var hits = new Regex($@"\[ABILITY\] {id} lands on view {target.ViewId} for (\d+)");
                Match hit = Match.Empty;
                await actor.WaitUntilAsync(() => (hit = hits.Match(server.DebugLog()[logAt..])).Success,
                    $"Ability {id} lands on {target.Name}", HeadlessPlayer.DefaultTimeout);
                Check.Equal(amount, int.Parse(hit.Groups[1].Value), $"Ability {id} hits {target.Name} for the expected damage");
                await EvidenceRetestWire.TimerAsync(actor, timerAt, id, AbilityCooldowns.OwnTimer, 4, ended: true);
            }
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
