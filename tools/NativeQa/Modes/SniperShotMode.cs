using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;

namespace NativeQa.Modes;

public class SniperShotMode : NativeQaMode
{
    private const int X = 21550, TargetX = 21750, BlockedX = 22350, Y = 1095, Z = 16150;
    private const ushort Rifles = CombatStyles.Rifles, Conceal = Stealth.ConcealmentGrantAbility,
        Baseline = CombatSpecials.ConcealedShot, Shot = CombatSpecials.SniperShot;
    // PR330 stock Rifle style capture as reused in GunStyleOpeningBatchMode.
    private const string RiflesFrame = "80b99300040002000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    // Successful stock 505 on QAEnemy (PR888 PacketLog line 3247).
    private const string SniperFrame = "80b9f9010400020000000000000000000000000000000000000000001060bd0001000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Rifles, 503, Baseline, Shot }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    private readonly int level;
    private bool IsRankComparison => level == 50;

    public SniperShotMode() : this(26) { }

    protected SniperShotMode(int level)
    {
        this.level = level;
        Plan = BuildPlan();
    }

    public override string Name => "ability-parity-sniper-shot";
    public override NativeQaModePlan Plan { get; }

    private NativeQaModePlan BuildPlan() => new(Loadout, X, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) },
        new[]
        {
            $"UPDATE characters SET level={level},exp={PlayerProgression.ExperienceForLevel(level)},y={Y}," +
                "rotation=64,alignment=1,pvpflag=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE char_abilities SET level={level} WHERE char_id=110",
            $"UPDATE characters SET x={TargetX},y={Y},z={Z},rotation=64,alignment=2,level={level}," +
                $"exp={PlayerProgression.ExperienceForLevel(level)},pvpflag=0,healthC=1000,healthM=1000 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{level},1)"))
        }, DevCommands: true, hotbarAbilities: IsRankComparison ? new[] { Rifles, Conceal, Shot } : new[] { Rifles, Conceal, Shot, Baseline },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "505", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Rank {level}, {Memory}/{AbilityMemory.ForLevel(level)} memory, rifle equipped. Stay at PICK with QAEnemy in front. /target QAEnemy, then Rifles slot 1. No teleport or interlock.",
            "Use slot 2 Concealment before each shot. Slot 4 Concealed Shot lands 24 for 45 IS. Slot 3 Sniper Shot lands 52 for 60 IS. Both drop Concealment at cast start, take 6 s and have 45 s own reuse. Wait 15 s between Concealment activations.",
            """After both neutral hits, send {"actor":"QAEnemy","action":"sniper-armor"} once and wait for [MODIFIER] source=1061 stat=181 +20. This helper action completes immediately and does not wait for your cast.""",
            "After the corresponding reuse, repeat both concealed shots. Expect type1 total20 raw24 final21 and raw52 final47. Stay at PICK. No RNG aid. Earlier stock range, sight and Sniper Stun evidence carries."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var pick = Stand(X, Z);
        var blocked = Stand(BlockedX, Z);
        var far = FindFarStand();
        _ = Stand(TargetX, Z);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, Z) &&
            !NavMesh.HasSightPath(1, BlockedX, Y, Z, TargetX, Y, Z), "Verified near sight and blocked geometry");
        Console.WriteLine($"PICK {pick.Command}");
        Console.WriteLine($"BLOCK {blocked.Command}");
        Console.WriteLine($"FAR {far.Command} (over 30 m, clear sight)");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, level);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory == 46 && Memory <= AbilityMemory.ForLevel(level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                loaded.TryGetValue(required.Key, out ushort rank) && rank >= required.Value)), $"Exact, legal L{level} stock profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(RiflesFrame);
        await ObserveAsync(actor, styleAt, rpc => Bonus(rpc, Rifles, 171, IsRankComparison ? 13 : 8, 0, false), "Captured Rifles applies its rank-scaled Ballistic Damage");
        await ObserveAsync(actor, 0, rpc => Bonus(rpc, 125, 1139, IsRankComparison ? 5 : 2, 0, false), "Loaded Firearms Expert contributes rank-scaled linked Ballistic Damage");
        await ObserveAsync(actor, 0, rpc => Bonus(rpc, 503, 1139, IsRankComparison ? 5 : 2, 0, false), "Loaded Concealment parent contributes rank-scaled linked Ballistic Damage");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        foreach (var row in new[] { (Baseline, 22), (Shot, 47) })
        {
            var health = AbilityCatalog.Find(row.Item1)!.Modifiers.Single(m => m.Target == 108 && m.Number("DamageType", -1) == 1);
            Check.Equal(row.Item2, (int)health.Number("MinValue"), $"Fixed GOB min {row.Item1}");
            Check.Equal(row.Item2, (int)health.Number("MaxValue"), $"Fixed GOB max {row.Item1}");
        }
        if (IsRankComparison)
        {
            actor.SendRpc("42 00 00 00 00 00 00"); // Stock Actions > Awakened Speed.
            int speedAt = await CastAsync(Shot, SniperFrame, 57);
            await WaitOwnAsync(Shot, speedAt);
            actor.SendRpc("42 01 00 00 00 00 00"); // Stock Actions > Awakened Power.
            int powerAt = await CastAsync(Shot, SniperFrame, 57);
            await WaitOwnAsync(Shot, powerAt);
            await PistolSlideMode.ApplyArmorAsync(enemy);
            await CastAsync(Shot, SniperFrame, 54, resisted: 3);
        }
        else
        {
            int baselineAt = await CastAsync(Baseline, ShotFrame, 24);
            await Task.Delay(TimeSpan.FromSeconds(15));
            int sniperAt = await CastAsync(Shot, SniperFrame, 52);
            await PistolSlideMode.ApplyArmorAsync(enemy);
            await WaitOwnAsync(Baseline, baselineAt);
            await CastAsync(Baseline, ShotFrame, 21, resisted: 3);
            await Task.Delay(TimeSpan.FromSeconds(15));
            await WaitOwnAsync(Shot, sniperAt);
            await CastAsync(Shot, SniperFrame, 47, resisted: 5);
        }
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Sniper stock framing");
        Console.WriteLine(IsRankComparison
            ? "SNIPER RANK BOOTSTRAP PASS: rank-50 Speed 57, Power 57, Power with +20 resistance 54."
            : "SNIPER FIXED DAMAGE BOOTSTRAP PASS: both retained stock shots, exact neutral and armored damage.");
        return Array.Empty<byte>();

        async Task<int> CastAsync(ushort id, string frame, int expected, int resisted = 0)
        {
            int conceal = actor.World.RpcSnapshot().Count;
            actor.SendRpc(ConcealFrame);
            await EvidenceRetestWire.CastBarAsync(actor, Conceal, 2);
            byte[] on = AbilityBuffs.BuildExclusive(Conceal, true), off = AbilityBuffs.BuildExclusive(Conceal, false);
            await ObserveAsync(actor, conceal, rpc => rpc.Bytes.AsSpan().SequenceEqual(on), "Captured Concealment becomes active");
            int from = actor.World.RpcSnapshot().Count, targetAt = enemy.World.RpcSnapshot().Count;
            actor.SendRpc(WithHandle(frame, handle));
            await ObserveAsync(actor, from, rpc => rpc.Bytes.AsSpan().SequenceEqual(off), $"Captured {id} consumes Concealment");
            await EvidenceRetestWire.CastBarAsync(actor, id, 6);
            byte[] damage = AbilityMessages.Damage(false, id, actor.Handle, enemy.Handle, expected, resisted);
            await ObserveAsync(enemy, targetAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(damage), $"Captured {id} lands for {expected}");
            await EvidenceRetestWire.TimerAsync(actor, from, id, AbilityCooldowns.OwnTimer, 45);
            return from;
        }

        async Task WaitOwnAsync(ushort id, int from)
        {
            var end = BytePattern.Parse($"80 bd 01 {EvidenceRetestWire.Le(id)} 04 00 00 34 42 00");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(from).Any(f => end.IsMatch(f.Bytes)),
                $"Natural 45 s reuse of {id}", TimeSpan.FromSeconds(49));
        }
    }
}

public sealed class SniperShotLevel50Mode : SniperShotMode
{
    public SniperShotLevel50Mode() : base(50) { }

    public override string Name => "ability-parity-sniper-shot-rank-ledger";
}
