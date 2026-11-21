using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterCoderSplashBatchMode;

namespace NativeQa.Modes;

public sealed class StarterCoderViralResistanceMode : NativeQaMode
{
    private const int Level = 23, Rank = 18, X = 24800, Y = 495, Z = -1498, EnemyZ = -1798;
    private const ushort Style = 1008, EmptyMind = 8;
    internal const int Resistance = 34;
    private static readonly int[] Mitigated = { 20, 3, 5 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Samples.Select(row => row.Id)
        .Concat(new ushort[] { 2, 17, Style }).ToArray()).Order().ToArray();
    private static readonly ushort[] DefenderLoadout = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();

    public override string Name => "ability-parity-starter-coder-viral-resistance";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6111,6112,6113,6114)",
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},alignment=2 WHERE charId=6114",
            $"UPDATE characters SET x={X + 150},y={Y},z={EnemyZ},alignment=2 WHERE charId=6112",
            $"UPDATE characters SET x={X - 330},y={Y},z={EnemyZ},alignment=2 WHERE charId=6111",
            $"UPDATE characters SET x={X + 130},y={Y},z={EnemyZ - 75},alignment=1 WHERE charId=6113",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", DefenderLoadout.Select((id, i) => $"(6112,{i},{0x80000000u | ((uint)id << 10)},{Level},1)")),
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Samples.Select(row => row.Id).Prepend(Style).ToArray(),
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "33,36,514", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Rank-18 starter abilities with the full 53-memory profile at character level 23. CSR faces QAEnemy 3 m straight ahead. QAFar is 1.5 m east of QAEnemy, allied QAOther is inside the splash radius, and hostile QAFriend is west outside it. No ambient mobs. Select Coder Style (page 2 slot 1) and QAFar before the resistance helper request.",
            """Send {"actor":"QAFar","action":"aura-resist"} and expect [MODIFIER] char=6112 source=8 stat=157 +34 for 20 s. Immediately use slot 2 Static Blast (2 s cast, 6 s reuse). QAFar takes raw24 final20, no secondary hit.""",
            "Immediately select QAEnemy. Press Tinkering (slot 3), then Advanced Tinkering (slot 4) as soon as slot 3 lands. Each casts for 6 s and has its own 30 s reuse. QAEnemy gets 4/6, and QAFar gets 3/5 under +34. QAOther and QAFriend take no damage. If Empty Mind expires before slot 4 lands, record that and repeat slot 4 with fresh Empty Mind after its 60 s reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar", "QAOther", "QAFriend" })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Every ranked prerequisite fits stock memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (string name in context.Helpers.Keys) await actor.WaitForViewNamedAsync(name);
        var neutral = context.Helpers["QAEnemy"];
        var resistant = context.Helpers["QAFar"];
        actor.SendRpc(StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(resistant, Resistance);
        for (int i = 0; i < Samples.Length; i++)
        {
            var row = Samples[i];
            var primary = row.Splash ? neutral : resistant;
            var view = await actor.WaitForViewNamedAsync(primary.Handle);
            uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
            var starts = context.Helpers.ToDictionary(pair => pair.Key, pair => pair.Value.World.RpcSnapshot().Count);
            int actorAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(row.Request, handle));
            await EvidenceRetestWire.CastBarAsync(actor, row.Id, row.Cast);
            await EvidenceRetestWire.TimerAsync(actor, actorAt, row.Id, AbilityCooldowns.OwnTimer, row.Reuse);
            await HitAsync(resistant, Mitigated[i], row.Damage - Mitigated[i]);
            if (row.Splash) await HitAsync(neutral, row.Damage, 0);
            foreach (var player in context.Helpers.Values.Where(player => player != resistant && (!row.Splash || player != neutral)))
            {
                player.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(player, starts[player.Handle], frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes),
                    "The excluded player's packet stream passed the hit boundary");
                byte[] damagePrefix = AbilityMessages.Damage(false, row.Id, actor.Handle, player.Handle, 0, 0)[..^8];
                Check.That(!player.World.RpcSnapshot().Skip(starts[player.Handle]).Any(frame => frame.Bytes.AsSpan().StartsWith(damagePrefix)),
                    $"No {row.Id} hit reaches {player.Handle}");
            }

            async Task HitAsync(HeadlessPlayer player, int damage, int absorb)
            {
                byte[] expected = AbilityMessages.Damage(false, row.Id, actor.Handle, player.Handle, damage, absorb);
                await StockAbilityProfile.ObserveAsync(player, starts[player.Handle], frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                    $"{row.Id} hits {player.Handle} for {damage}, absorbing {absorb}");
            }
        }
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count), "Coder resistance framing");
        return Array.Empty<byte>();
    }
}
