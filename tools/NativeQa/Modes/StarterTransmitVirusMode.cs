using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterTransmitVirusMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498, EnemyZ = -1798, Level = 50;
    private const ushort Style = 1006, Transmit = 95, Barrage = 57, Blast = 58, Incoming = 330;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Style, Transmit, Barrage, Blast).Order().ToArray();
    private static readonly ushort[] EnemyAbilities = { 2, 17, Style, Incoming };
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    // PR396 and PR545 stock direct viral requests. Only the receiver-local selection handle changes.
    private const string BarrageStock = "80b93900040002000000000000000000000000000000000000000000585cba0001000000086b0000000000";
    private const string BlastStock = "80b93a000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string IncomingStock = "80b94a010e000c000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-starter-transmit-virus";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)} WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ} WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", EnemyAbilities.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},50,1)"))
    }, DevCommands: true, hotbarAbilities: new[] { Style, Barrage, Blast },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "57,58,95", ["MemoryUsed"] = Memory, ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level 50, full profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}), 3 m ahead and clear of the ice FX. No bag actions. Select Hacker Style (page 2 slot 1) and QAEnemy, staying outside interlock.",
            "Transmit Virus starts loaded. Expect [MODIFIER] char=110 source=95 stat=1031 and 1148, both add value=+15 duration=0s. Cast Logic Barrage and Logic Blast (slots 2 and 3), waiting 4 s for the shared reuse. Expect outgoing raw damage 11 and 15.",
            """Request {"actor":"QAEnemy","action":"cast","ability":330,"target":"CSR_Tester"}. Expect [RESISTANCE-BONUS OURS] CSR_Tester type2 level50 percent15 points33 base0 total33, then 496 damage and 42 resisted (raw538). Keep the helper and both tactics fixed.""",
            "Enter ?qaunload 95. Check both +15 removals, then repeat the two outgoing hits (raw 10 and 13) and the incoming 330 (538 damage, 0 resisted). Enter ?qaload 95, check both adds and repeat to restore 11/15 outgoing and 496/42 incoming. No maximum [VITALS] changes or interlock [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Transmit Virus QA needs a disposable server.");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        await SetStyleAsync(enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) &&
            Abilities.All(id => archive.TryGetValue(id, out ushort rank) && rank == Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The full stock loadout fits and is loaded");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await SetStyleAsync(actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint selected = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(selected))}");

        await ModifiersAsync(0, false);
        var loaded = await MeasureAsync();
        int off = actor.World.RpcSnapshot().Count;
        actor.SendChat("?qaunload 95");
        await ModifiersAsync(off, true);
        var unloaded = await MeasureAsync();
        int on = actor.World.RpcSnapshot().Count;
        actor.SendChat("?qaload 95");
        await ModifiersAsync(on, false);
        var restored = await MeasureAsync();

        Check.Equal((11, 15, 496, 42), loaded, "Both typed consumers use the loaded passive");
        Check.Equal((10, 13, 538, 0), unloaded, "Unloading removes both bonuses and keeps the incoming raw damage fixed");
        Check.Equal(loaded, restored, "Restoring the passive reproduces both typed consumers");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Transmit Virus batch framing");
        Console.WriteLine($"TRANSMIT VIRUS PASS: loaded {loaded}, unloaded {unloaded}, restored {restored}.");
        return Array.Empty<byte>();

        async Task ModifiersAsync(int at, bool removed)
        {
            foreach (ushort stat in new ushort[] { 1031, 1148 })
                await StockAbilityProfile.ObserveAsync(actor, at,
                    frame => StockAbilityProfile.Bonus(frame, Transmit, stat, 15, 0, removed),
                    $"Transmit Virus stat {stat}, remove={removed}");
        }

        async Task<(int Barrage, int Blast, int Incoming, int Resisted)> MeasureAsync()
        {
            var a = await HitAsync(actor, enemy, Barrage, () =>
            {
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BarrageStock, selected));
                return Task.CompletedTask;
            });
            var b = await HitAsync(actor, enemy, Blast, () =>
            {
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BlastStock, selected));
                return Task.CompletedTask;
            });
            var c = await HitAsync(enemy, actor, Incoming, () => SendProbeAsync(enemy, actor.Handle, Incoming));
            return (a.Damage + a.Resisted, b.Damage + b.Resisted, c.Damage, c.Resisted);
        }
    }

    internal static async Task<object> SendProbeAsync(HeadlessPlayer actor, string target, ushort ability)
    {
        if (actor.Handle != "QAEnemy" || target != "CSR_Tester" || ability != Incoming)
            throw new InvalidOperationException("Use QAEnemy to cast Logic Barrage 330 on CSR_Tester.");
        var view = await actor.WaitForViewNamedAsync(target);
        byte[] request = StarterHackerControlTargetMode.WithHandle(IncomingStock, DuelPackets.Handle(view.ViewId, view.SpawnId));
        actor.SendRpc(request);
        return new { Ability = ability, Target = target, Request = Convert.ToHexString(request) };
    }

    private static async Task SetStyleAsync(HeadlessPlayer player)
    {
        player.SendRpc(StarterHackerViralSightMode.StockStyle);
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
    }

    private static async Task<(int Damage, int Resisted)> HitAsync(
        HeadlessPlayer attacker, HeadlessPlayer recipient, ushort ability, Func<Task> send)
    {
        int outgoing = attacker.World.RpcSnapshot().Count;
        int incoming = recipient.World.RpcSnapshot().Count;
        byte[] message = AbilityMessages.Damage(false, ability, attacker.Handle, recipient.Handle, 0, 0);
        await send();
        var hit = await StockAbilityProfile.ObserveAsync(recipient, incoming, frame =>
            frame.Bytes.Length == message.Length && frame.Bytes.AsSpan().StartsWith(message.AsSpan(0, message.Length - 8)),
            $"The stock {ability} hit reaches {recipient.Handle}");
        await EvidenceRetestWire.TimerAsync(attacker, outgoing, ability, AbilityCooldowns.OwnTimer, 4, ended: true);
        return (BitConverter.ToInt32(hit.Bytes, hit.Bytes.Length - 8), BitConverter.ToInt32(hit.Bytes, hit.Bytes.Length - 4));
    }
}
