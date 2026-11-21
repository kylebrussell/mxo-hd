using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterResistContagionMode : NativeQaMode
{
    private const ushort Resist = 73, Disruption = 38, Barrage = 57;
    private const int X = 24800, Y = 495, Z = -1498, EnemyZ = -1798, Level = 50;
    // PR556 stock Area Disruption and PR396 stock Logic Barrage.
    private const string DisruptionStock = "80b92600060004000000000000000000000000000000000000000000585cb70001000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, StarterHackerViralSightMode.HackerStyle,
        Resist, Disruption, Barrage).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-resist-contagion";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)} WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ} WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114"
    }, DevCommands: true, hotbarAbilities: new[] { StarterHackerViralSightMode.HackerStyle, Disruption, Barrage },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "38,57,73", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full stock prerequisite profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}), clear of the ice FX. Select Hacker Style (page 2 slot 1) and QAEnemy. Keep the same target and tactic for the whole run, stay out of interlock, and use Enter for chat.",
            "Resist Contagion 73 starts loaded. [MODIFIER] char=110 source=73 adds stat 995 +5 and 1147 +14. Press slot 2 Area Disruption (2 s cast, 43 s own reuse). QAEnemy: [MODIFIER] char=6114 source=38 stat=102 add value=0 duration=3.15s, then remove. Independent stat=681 immunity adds and removes at 8 s. After immunity clears, press slot 3 Logic Barrage and record the first number in [FREECAST-VIRAL] ability=57 inputs.",
            "Enter ?qaunload 73. Expect both passive removals. Press slot 3 again after its 4 s reuse. In [FREECAST-VIRAL] ability=57, expect inputs 88/0 loaded and 80/0 unloaded. The +7 pre-tactic contribution changes the Speed-adjusted input by 8. After the 43 s Disruption reuse expires, press slot 2 again. Expect 3 s stun add/remove and the same 8 s immunity.",
            "Enter ?qaload 73 and verify both passive re-adds and the original slot 3 inputs 88/0. No bag, maximum [VITALS] changes or interlock [PVP] rounds. This is a consumer comparison for the implemented viral formula."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Every ranked prerequisite fits the stock loadout");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint selected = DuelPackets.Handle(view.ViewId, view.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(selected))}");

        await PassiveAsync(0, false);
        int firstStun = await StunAsync(3 * 1.05f);
        var loaded = await BarrageAsync();
        int off = actor.World.RpcSnapshot().Count;
        actor.SendChat("?qaunload 73");
        await PassiveAsync(off, true);
        var unloaded = await BarrageAsync();
        Check.Equal((88, 0), loaded, "The loaded contribution is +7 before the Speed multiplier");
        Check.Equal((80, 0), unloaded, "Unloading drops the Speed-adjusted attack input by 8 with defense unchanged");
        byte[] reuseOff = AbilityCooldowns.BuildTimer(Disruption, AbilityCooldowns.OwnTimer, 43, false);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(firstStun)
            .Any(frame => frame.Bytes.AsSpan().SequenceEqual(reuseOff)), "Area Disruption reuse expires", TimeSpan.FromSeconds(46));
        await StunAsync(3);
        int on = actor.World.RpcSnapshot().Count;
        actor.SendChat("?qaload 73");
        await PassiveAsync(on, false);
        Check.Equal(loaded, await BarrageAsync(), "Reload restores the original contest inputs");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Resist Contagion framing");
        Console.WriteLine($"RESIST CONTAGION PASS: inputs {loaded} -> {unloaded} -> {loaded}; stun 3.15 -> 3 s, immunity 8 s.");
        return Array.Empty<byte>();

        async Task PassiveAsync(int from, bool removed)
        {
            foreach (var (stat, value) in new (ushort, int)[] { (995, 5), (1147, 14) })
                await StockAbilityProfile.ObserveAsync(actor, from,
                    frame => StockAbilityProfile.Bonus(frame, Resist, stat, value, 0, removed),
                    $"Resist Contagion stat {stat}, removed={removed}");
        }

        async Task<int> StunAsync(float duration)
        {
            int from = actor.World.RpcSnapshot().Count;
            int recipient = enemy.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(DisruptionStock, selected));
            await EvidenceRetestWire.CastBarAsync(actor, Disruption, 2);
            await EvidenceRetestWire.TimerAsync(actor, from, Disruption, AbilityCooldowns.OwnTimer, 43);
            foreach (bool removed in new[] { false, true })
            {
                await StockAbilityProfile.ObserveAsync(enemy, recipient,
                    frame => StockAbilityProfile.Bonus(frame, Disruption, 102, 0, duration, removed),
                    $"The {duration}s stun, removed={removed}");
                await StockAbilityProfile.ObserveAsync(enemy, recipient,
                    frame => StockAbilityProfile.Bonus(frame, TempMods.ControlImmunityTimerAbility, 681, 0, 8, removed),
                    $"The 8s immunity, removed={removed}");
            }
            return from;
        }

        async Task<(int Attack, int Defense)> BarrageAsync()
        {
            int from = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            var line = new Regex(@"\[FREECAST-VIRAL\] ability=57 source=CSR_Tester target=QAEnemy inputs=(\d+)/(\d+) [^\r\n]+hit=True");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterHackerViralSightMode.Samples[0].Capture, selected));
            await actor.WaitUntilAsync(() => line.IsMatch(server.DebugLog()[log..]),
                "Same-target Logic Barrage viral contest", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, from, Barrage, AbilityCooldowns.OwnTimer, 4, ended: true);
            var match = line.Match(server.DebugLog()[log..]);
            return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        }
    }
}
