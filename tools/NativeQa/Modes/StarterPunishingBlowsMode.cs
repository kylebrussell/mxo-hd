using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterPunishingBlowsMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498, EnemyZ = -1798, Level = 50;
    private const ushort Punishing = 16, Freeze = 40, Blast = 58, Barrage = 59;
    // PR627 captured stock toggle, 6971d24474bd:4113 Code Freeze, PR396 Logic Blast/Barrage.
    private const string ToggleStock = "80b91000000000000000000000000000000000000000000000000000e05bc40001000000086b0000000000";
    private const string FreezeStock = "80b92800040002000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, StarterHackerViralSightMode.HackerStyle,
        Punishing, Freeze, Blast, Barrage).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-punishing-blows";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)},innerStrC=800,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ} WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114"
    }, DevCommands: true, hotbarAbilities: new[] { StarterHackerViralSightMode.HackerStyle, Punishing, Freeze, Blast, Barrage },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_CONDITIONAL_THROW_QA"] = "1", ["MXO_PUNISHING_BLOWS_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "16,40,58,59", ["MemoryUsed"] = Memory, ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, full profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}), clear of the ice FX. Page 2: Hacker Style, Punishing Blows, Code Freeze, Logic Blast, Logic Barrage. Select Hacker Style and QAEnemy. Stay out of interlock and keep QAEnemy selected; there are no bag actions.",
            "Use Logic Blast (slot 4) with Punishing Blows off. [PUNISHING-BLOWS OURS] shows raw15 stat0 vulnerableFalse final15. Then toggle on with slot 2. Expect [MODIFIER] char=110 source=16 stat=212 add value=+15 duration=0s and [BUFF-DRAIN] char110 ability16 amount1 every second.",
            "Use Code Freeze slot 3, wait for [MODIFIER] char=6114 source=40 stat=99 (25 s root), then use Logic Blast slot 4. Root alone gives stat15 vulnerableFalse, with the same damage as baseline. The hit removes the root. Wait at least 4 s between Logic Blast and Barrage.",
            "With QAEnemy still selected, enter ?combatdepth offbalance 20. Expect [MODIFIER] char=6114 source=885 stat=200 add for 20 s. Cast Logic Blast and then Logic Barrage (slots 4 and 5). Expect stat15 vulnerableTrue and raw15 final17 / raw24 final27. The Damage lines must match 17 and 27.",
            "Wait for the 885 state removal, then use Logic Blast again. Expect baseline damage with stat15 vulnerableFalse. Toggle 16 off and observe the 212 removal and end of the periodic 1 IS/s drain. No maximum [VITALS] changes or interlock [PVP] rounds. This verifies the documented server interpretation of vulnerability."
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

        int baseline = await DamageAsync(1, 0, false);
        await ToggleAsync(false);
        int rootAt = enemy.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(FreezeStock, selected));
        await StockAbilityProfile.ObserveAsync(enemy, rootAt, frame => StockAbilityProfile.Bonus(frame, Freeze, 99, 0, 25, false),
            "Real Code Freeze on the opponent");
        Check.Equal(baseline, await DamageAsync(1, 15, false), "Root alone adds no damage");
        await StockAbilityProfile.ObserveAsync(enemy, rootAt, frame => StockAbilityProfile.Bonus(frame, Freeze, 99, 0, 25, true),
            "The normal damage breaks the existing root");

        int stateAt = enemy.World.RpcSnapshot().Count;
        actor.SendChat("?combatdepth offbalance 20");
        await StockAbilityProfile.ObserveAsync(enemy, stateAt, frame => StockAbilityProfile.Bonus(frame, 885, 200, 0, 20, false),
            "The selected player's 20-second Off Balance control");
        int boostedBlast = await DamageAsync(1, 15, true);
        int boostedBarrage = await DamageAsync(2, 15, true);
        Check.That(boostedBlast > baseline, "Vulnerable Blast damage exceeds its own baseline");
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(stateAt)
            .Any(frame => StockAbilityProfile.Bonus(frame, 885, 200, 0, 20, true)), "Natural Off Balance expiry", TimeSpan.FromSeconds(22));
        Check.Equal(baseline, await DamageAsync(1, 15, false), "The bonus ends at state expiry while the toggle is still on");
        Check.Equal((15, 17, 27), (baseline, boostedBlast, boostedBarrage), "The prepared loadout's exact damage lines");
        await ToggleAsync(true);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Punishing Blows batch framing");
        Console.WriteLine($"PUNISHING BLOWS PASS: Blast baseline/root/expiry {baseline}, Off Balance {boostedBlast}; Barrage {boostedBarrage}.");
        return Array.Empty<byte>();

        async Task ToggleAsync(bool removed)
        {
            int from = actor.World.RpcSnapshot().Count;
            actor.SendRpc(ToggleStock);
            await StockAbilityProfile.ObserveAsync(actor, from,
                frame => StockAbilityProfile.Bonus(frame, Punishing, 212, 15, 0, removed), $"Damage bonus removed={removed}");
        }

        async Task<int> DamageAsync(int index, int percent, bool vulnerable)
        {
            var sample = StarterHackerViralSightMode.Samples[index];
            int from = actor.World.RpcSnapshot().Count;
            int recipient = enemy.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            byte[] damage = AbilityMessages.Damage(false, sample.Ability, actor.Handle, enemy.Handle, 0, 0);
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, selected));
            var hit = await StockAbilityProfile.ObserveAsync(enemy, recipient, frame => frame.Bytes.Length == damage.Length &&
                frame.Bytes.AsSpan().StartsWith(damage.AsSpan(0, damage.Length - 8)), $"Damage from {sample.Ability} reaches QAEnemy");
            var line = new Regex($@"\[PUNISHING-BLOWS OURS\] CSR_Tester -> QAEnemy raw(\d+) stat{percent} vulnerable{vulnerable} final(\d+)");
            await actor.WaitUntilAsync(() => line.IsMatch(server.DebugLog()[log..]), "The victim's vulnerability is sampled for this hit",
                HeadlessPlayer.DefaultTimeout);
            var match = line.Match(server.DebugLog()[log..]);
            int raw = int.Parse(match.Groups[1].Value);
            int final = int.Parse(match.Groups[2].Value);
            Check.Equal(raw * (100 + (vulnerable ? percent : 0)) / 100, final, "Damage uses integer truncation and only qualifying states");
            Check.Equal(final, BitConverter.ToInt32(hit.Bytes, hit.Bytes.Length - 8), "Combat message matches the damage consumer");
            await EvidenceRetestWire.TimerAsync(actor, from, sample.Ability, AbilityCooldowns.OwnTimer, sample.Reuse, ended: true);
            return final;
        }
    }
}
