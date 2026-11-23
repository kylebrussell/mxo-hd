using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerAttackProgrammingEfficientCodeMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, EnemyZ = 6700;
    private const ushort Evade = 217, Disruption = 38, Style = CombatStyles.HackerStyle, Programming = 471, Efficient = 477;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Evade, Disruption, Programming, Efficient).Order().ToArray();
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Sources =
    {
        (Programming, 0, new (ushort, int)[] { (1053, 5), (1059, 15) }),
        (Efficient, 0, new (ushort, int)[] { (995, 5), (996, 10), (997, 10), (1139, 10), (1142, 10) })
    };

    public override string Name => "ability-parity-hacker-attack-programming-efficient-code";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,EnemyZ)},innerStrC=350,innerStrM=1600 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ},alignment=2 WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114"
    }, DevCommands: true, hotbarAbilities: new[] { Style, Evade, Disruption }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "471,477", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, full ranked Vector/Network Attacker profile at {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}) at 3 m. Page 2 Hacker Style, Evade Combat, Area Disruption. Start below your IS maximum and stay at rest between casts.",
            "At login expect [MODIFIER] source=471 stat=1053 +5 and 1059 +15. The fixed stat1059 contributors are Execute Program 45 +10 and Ravager 470 +5. Resting IS grows by 10 or 11 each 2 s, totalling 52 over five ticks. Select Hacker Style, cast Evade from slot 2, check [ABILITY-COST] 217 paid=38 and switch it off. After the 30 s own timer starts, cast slot 3 Area Disruption. On QAEnemy expect source=38 stat=102 for about 3.30 s, plus independent source=734 stat=681 for 8 s. Wait for their removals.",
            "While idle use ?qaunload 471. Both source=471 modifiers come off. Observe resting IS increases of 9 or 10 each 2 s, totalling 46 over five ticks. Once Evade reuse has cleared, repeat slot 2 and expect 40 IS before switching it off again. Type ?qaload 471: +5 and +15 return, along with the 52 IS per five resting ticks.",
            "At login 477 adds stat 995 +5 and stats 996/997/1139/1142 +10 each. Type ?qaunload 477 and expect all five records removed. The unchanged Resist Contagion source=73 keeps +5 stat995. After the original 43 s Area Disruption reuse, recast slot 3. Expect about 3.15 s Stun with the same 8 s immunity on QAEnemy. 38 takes 2 s and 14 IS in both samples. No target HP loss or max [VITALS] change. Type ?qaload 477 to restore all five records."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Attack Programming and Efficient Code need repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { Z, EnemyZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, X, Y, z), $"Visible outdoor NAV stand ({X},{Y},{z})");

        var server = context.ActualServer ?? throw new InvalidOperationException("The ranked passive fixture needs a disposable server.");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Complete legal rank-50 passive profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        Check.That(EngageFoesFixture.Position(target).SequenceEqual(new double[] { X, Y, EnemyZ }), "QAEnemy is on the printed stand");
        var observed = new List<object>();
        await StockAbilityProfile.CheckPassivesAsync(actor, observed, Sources, 0, remove: false);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 45, 1059, 10, 0, false),
            "Execute Program contributes +10% IS regeneration");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 470, 1059, 5, 0, false),
            "Ravager contributes +5% IS regeneration");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 73, 995, 5, 0, false),
            "Resist Contagion contributes +5% Stun duration");

        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, styleAt, frame => StockAbilityProfile.Bonus(frame, Style, 172, 27, 0, false),
            "Captured Hacker Style is active");
        await RestingRegenAsync(programming: true);
        int evadeOff = await EvadeAsync(38);
        int disruptionAt = await DisruptionAsync(3.30f);
        await ChangePassiveAsync(Programming, load: false);
        await RestingRegenAsync(programming: false);
        await WaitReuseAsync(evadeOff, Evade, 30);
        await EvadeAsync(40);
        await ChangePassiveAsync(Programming, load: true);
        await RestingRegenAsync(programming: true);
        await ChangePassiveAsync(Efficient, load: false);
        await WaitReuseAsync(disruptionAt, Disruption, 43);
        await DisruptionAsync(3.15f);
        await ChangePassiveAsync(Efficient, load: true);

        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Ranked passive replay framing");
        Console.WriteLine("ATTACK PROGRAMMING/EFFICIENT CODE BOOTSTRAP PASS: 38/40 IS, 52/46 over five resting ticks, and 3.30/3.15 s stun with 8 s immunity.");
        return Array.Empty<byte>();

        async Task ChangePassiveAsync(ushort ability, bool load)
        {
            int start = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load);
            await StockAbilityProfile.CheckPassivesAsync(actor, observed, Sources.Where(source => source.Parent == ability).ToArray(),
                start, remove: !load);
        }

        async Task RestingRegenAsync(bool programming)
        {
            int cursor = actor.World.ViewSnapshot().Count;
            const int samples = 6;
            ushort[] values = Array.Empty<ushort>();
            await actor.WaitUntilAsync(() => (values = actor.World.ViewSnapshot().Skip(cursor)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Take(samples).ToArray()).Length == samples,
                "Natural resting IS regen samples", TimeSpan.FromSeconds(16));
            var deltas = values.Zip(values.Skip(1), (prior, next) => next - prior).ToArray();
            int minimum = programming ? 10 : 9;
            int total = programming ? 52 : 46;
            Check.That(deltas.Min() == minimum && deltas.Max() == minimum + 1 && deltas.Sum() == total,
                $"Resting IS deltas with 471 loaded={programming}: {string.Join(", ", deltas)}");
        }

        async Task<int> EvadeAsync(int cost)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int debugAt = server.DebugLog().Length;
            actor.SendRpc(StarterHackerCostBonusesMode.EvadeUse);
            await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => StockAbilityProfile.Bonus(frame, Evade,
                EvasionShield.StateAbility, 0, 0, false), "Stock Evade enters its ordinary shield state");
            Check.That(server.DebugLog()[debugAt..].Contains($"[ABILITY-COST] char=110 ability={Evade} paid={cost} IS="),
                $"Stock Evade charges exactly {cost} IS");
            int off = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerCostBonusesMode.EvadeUse);
            await EvidenceRetestWire.TimerAsync(actor, off, Evade, AbilityCooldowns.OwnTimer, 30);
            return off;
        }

        async Task<int> DisruptionAsync(float seconds)
        {
            int actorAt = actor.World.RpcSnapshot().Count, enemyAt = enemy.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterAreaDisruptionCompletionMode.StockCast,
                DuelPackets.Handle(target.ViewId, target.SpawnId)));
            await EvidenceRetestWire.CastBarAsync(actor, Disruption, 2);
            await EvidenceRetestWire.TimerAsync(actor, actorAt, Disruption, AbilityCooldowns.OwnTimer, 43);
            foreach (bool remove in new[] { false, true })
            {
                await StockAbilityProfile.ObserveAsync(enemy, enemyAt, frame => IsStun(frame, seconds, remove),
                    $"Area Disruption stun {seconds:F2} s remove={remove}");
                await StockAbilityProfile.ObserveAsync(enemy, enemyAt, frame => StockAbilityProfile.Bonus(frame,
                    TempMods.ControlImmunityTimerAbility, 681, 0, 8, remove), $"Independent 8 s immunity remove={remove}");
            }
            return actorAt;
        }

        Task WaitReuseAsync(int cursor, ushort ability, int seconds)
        {
            byte[] expired = AbilityCooldowns.BuildTimer(ability, AbilityCooldowns.OwnTimer, seconds, false);
            return actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Bytes.AsSpan().SequenceEqual(expired)),
                $"Ability {ability} reuse clears", TimeSpan.FromSeconds(60));
        }
    }

    private static bool IsStun(RpcRecord frame, float duration, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Disruption && record.TargetId == 102 &&
        record.SignedValue == 0 && Math.Abs(record.Duration - duration) < 0.0001f && record.AddRemoveType == (remove ? 1 : 0);
}
