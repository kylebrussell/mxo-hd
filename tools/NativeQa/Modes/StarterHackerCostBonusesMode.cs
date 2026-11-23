using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerCostBonusesMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, InitialIs = 300;
    private const ushort StandOff = 309, DedicatedCode = 346, Hacker = 1006, Freeze = 40, Evade = 217;
    // Complete stock PR606 Freeze and PR524 Evade requests. Only Freeze gets a live handle.
    private const string FreezeUse = "80b92800040002000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    internal const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const int EnemyX = 17400;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, StandOff, DedicatedCode, Hacker, Freeze, Evade }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly Dictionary<ushort, (ushort Stat, int Value)[]> Passives = new()
    {
        [StandOff] = new (ushort, int)[] { (1031, 1), (1053, 1), (1090, 3) },
        [DedicatedCode] = new (ushort, int)[] { (1031, 1), (1053, 5) }
    };

    public override string Name => "ability-parity-starter-hacker-cost-bonuses";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"x={X},y={Y},z={Z},rotation=64,innerStrC={InitialIs},innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET level={Level},x={EnemyX},y={Y},z={Z},healthC=3000,healthM=3000,alignment=2,pvpflag=0 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114" },
        DevCommands: true, hotbarAbilities: new ushort[] { Hacker, Freeze, Evade },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "309,346", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"CSR level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. Starts on verified outdoor NAV floor 400 at ({X},{Y},{Z}), facing QAEnemy 4 m east at (17400,495,6400). IS holds between casts. No bag input.",
            "Page 2 has Hacker Style, Code Freeze and Evade. Select Hacker Style once and /target QAEnemy. Check [MODIFIER] 309/1031 +1, 1053 +1, 1090 +3 and 346/1031 +1, 1053 +5, all permanent while loaded.",
            "Both loaded: slot 2 Code Freeze. Expect [ABILITY-COST] ability=40 paid=17 IS=300->283 and 25 s root and -20 on char=6114. Enter ?qaunload 309. Slot 3 Evade with only 346: paid=38 IS=283->245. Switch Evade off once active and wait for its 30 s reuse.",
            "Leave 346 loaded. After the 58 s Code Freeze reuse, slot 2 again: paid=18 IS=245->227. Then enter ?qaunload 346. Slot 3 again after its reuse: paid=40 IS=227->187. Switch off, then ?qaload 309 and ?qaload 346. Check the same passive records remove/re-add. No max [VITALS] change or interlock [PVP] round."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(new[] { X, EnemyX }.All(x => NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors &&
            Math.Abs(stand.floorY + 95 - Y) < 1), "CSR and QAEnemy are both on outdoor NAV");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The full starter profile fit level-19 memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        foreach (ushort ability in Passives.Keys)
            await RecordsAsync(0, ability, remove: false);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        byte[] freeze = StarterHackerControlTargetMode.WithHandle(FreezeUse, handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        ushort remaining = InitialIs;
        int freezeAt = actor.World.RpcSnapshot().Count;
        await CastAsync(Freeze, freeze, 17);
        await ToggleAsync(StandOff, load: false);
        int evadeOff = await EvadeAsync(38);
        await TimerExpiredAsync(Freeze, freezeAt, 58);
        await CastAsync(Freeze, freeze, 18);
        await ToggleAsync(DedicatedCode, load: false);
        await TimerExpiredAsync(Evade, evadeOff, 30);
        await EvadeAsync(40);
        await ToggleAsync(StandOff, load: true);
        await ToggleAsync(DedicatedCode, load: true);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Linked cost comparison framing");
        Console.WriteLine("HACKER COST BONUSES PASS: Freeze 17/18 and Evade 38/40 IS.");
        return Array.Empty<byte>();

        async Task ToggleAsync(ushort ability, bool load)
        {
            int start = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load);
            await RecordsAsync(start, ability, remove: !load);
        }

        async Task RecordsAsync(int start, ushort ability, bool remove)
        {
            foreach (var (stat, value) in Passives[ability])
                await StockAbilityProfile.ObserveAsync(actor, start,
                    frame => StockAbilityProfile.Bonus(frame, ability, stat, value, 0, remove),
                    $"Passive {ability} stat {stat} value {value} remove={remove}");
        }

        async Task CastAsync(ushort ability, byte[] body, ushort paid)
        {
            actor.SkipReceived();
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            int recipientAt = enemy.World.RpcSnapshot().Count;
            ushort before = remaining;
            remaining -= paid;
            actor.SendRpc(body);
            await EvidenceRetestWire.CastBarAsync(actor, ability, 2);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(remaining),
                $"Ability {ability} costs {paid} IS", HeadlessPlayer.DefaultTimeout);
            if (ability == Freeze)
                await StockAbilityProfile.ObserveAsync(enemy, recipientAt,
                    frame => StockAbilityProfile.Bonus(frame, Freeze, 99, 0, 25, false), "Code Freeze roots QAEnemy");
            else
                await StockAbilityProfile.ObserveAsync(actor, rpcAt,
                    frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, false), "Evade activates");
            Check.That(server.DebugLog().Contains($"[ABILITY-COST] char=110 ability={ability} paid={paid} IS={before}->{remaining}",
                StringComparison.Ordinal), "Paid-cost diagnostic matches the owner's wire update");
        }

        async Task<int> EvadeAsync(ushort paid)
        {
            byte[] use = WireFormat.FromHex(EvadeUse);
            await CastAsync(Evade, use, paid);
            int off = actor.World.RpcSnapshot().Count;
            actor.SendRpc(use);
            await EvidenceRetestWire.TimerAsync(actor, off, Evade, AbilityCooldowns.OwnTimer, 30);
            return off;
        }

        Task TimerExpiredAsync(ushort ability, int start, int seconds)
        {
            byte[] ended = AbilityCooldowns.BuildTimer(ability, AbilityCooldowns.OwnTimer, seconds, false);
            return actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(start).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(ended)),
                $"Ability {ability} reuse clears", TimeSpan.FromSeconds(65));
        }
    }
}
