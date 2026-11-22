using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterConcealedShotLifecycleMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, EnemyX = 17400, FriendX = 16800;
    private const ushort Rifles = 147, Parent = 503, Conceal = Stealth.ConcealmentGrantAbility, Shot = CombatSpecials.ConcealedShot;

    // PR429 stock Concealment (7483f644ff39:1600–1601) and PR410 stock Concealed Shot (3ef3dc4c8f4c:2400–2401).
    private const string ConcealFrame = "80b97104000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string ShotFrame = "80b9f801040002000000000000000000000000000000000000000000585cc10001000000086b0000000000";

    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Rifles, Parent, Shot }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-concealed-shot-lifecycle";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
                "healthC=700,healthM=2000,innerStrC=400,innerStrM=400,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            $"UPDATE characters SET x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,level=10," +
                $"exp={PlayerProgression.ExperienceForLevel(10)},pvpflag=0,healthC=1000,healthM=1000 WHERE charId=6114",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=64,alignment=1,level={Level}," +
                $"exp={PlayerProgression.ExperienceForLevel(Level)},pvpflag=0 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114) AND ability_id NOT IN (2147485696,2147501056)"
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Conceal, Shot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "6",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "503,504,1137", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, 42/49 memory, 503 at rank {Level} grants 1137. Verified outdoor NAV: CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) from 4 m behind; allied observer QAFriend ({FriendX},{Y},{Z}). Rifle equipped. Page 2 slots 1/2/3 = Rifles/Concealment/Concealed Shot.",
            "Check login source 503 [MODIFIER] 575 +3, 1045 +1, 1059 +3 and 1139 +1. Choose Rifles and /target QAEnemy. From rest press slot 3 before concealing. Expect the local Concealment gate or a single reason 23 (mask 0x00040000) without cast bar, cost, hit or reuse.",
            "Use ?health 700 before slot 2. Expect 1137 2 s activation, 15 s own reuse, [STEALTH] bits 2 SL 19 and source 1137 Stealth +19 plus states 623/881. Use ?health 1800 shortly after activation. That health gain leaves concealment up. Then ?health 600 ends it once with [STEALTH] 1137 off (damaged). Try slot 3 again to verify the gate returns after concealment breaks. These commands drive the normal health-change callback in the disposable fixture.",
            "Wait for 1137 reuse to clear. Keep QAEnemy 4 m ahead and activate slot 2 again, then press slot 3. The accepted shot consumes 1137 at cast start, spends 45 IS, shows a 6 s bar and hits QAEnemy for 23 at completion with 45 s own reuse. This run does not assess armor piercing or the random stun."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, EnemyX, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Verified clear outdoor concealment stand ({x},{Y},{Z})");

        var enemy = await StartHelperAsync("QAEnemy");
        var friend = await StartHelperAsync("QAFriend");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var profile = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        Check.Equal((ushort)Level, profile[Parent].Level, "Full Concealment training rank in the stock archive");
        Check.Equal(42, Loadout.Sum(AbilityMemory.Cost), "Legal level-19 starter Concealed Shot profile");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        await actor.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Conceal, remove: false, rank: Level),
            "The loaded parent granted rank-19 Concealment at world entry");
        foreach (var (stat, value) in new (ushort, int)[] { (575, 3), (1045, 1), (1059, 3), (1139, 1) })
            await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Parent, stat, value, 0, false),
                $"Rank-19 Concealment parent adds {stat}");

        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        var friendlyView = await friend.WaitForViewNamedAsync(actor.Handle);
        var own = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        byte[] shot = WithHandle(ShotFrame, handle);
        await StarterFixedDamageMode.SelectStyle(actor, own, Rifles, 171);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        var refused = await RefusedAsync(actor, Shot, shot, AbilityRefusals.CasterStateRequired);
        Check.That(AbilityRefusals.TryDecode(refused.Bytes, out var record) && record.StatesNotMet == Stealth.ConcealmentFlag,
            "The source-backed concealment bit is the only missing state");

        int concealAt = actor.World.RpcSnapshot().Count;
        await ActivateAsync();
        await RefusedAsync(actor, Shot, WithHandle(ShotFrame, handle ^ 0x10000));
        int healthLogAt = server.DebugLog().Length;
        await HealthAsync(1800);
        Check.That(!server.DebugLog()[healthLogAt..].Contains("1137 off", StringComparison.Ordinal),
            "The health gain and preceding stale-target refusal do not remove Concealment");
        int breakAt = actor.World.RpcSnapshot().Count;
        int friendBreakAt = friend.World.ViewSnapshot().Count;
        await HealthAsync(600);
        await CheckOffAsync(breakAt, friendBreakAt);
        Check.That(server.DebugLog()[healthLogAt..].Contains("1137 off (damaged)", StringComparison.Ordinal),
            "Normal health loss invokes the Concealment damage break");
        await RefusedAsync(actor, Shot, shot, AbilityRefusals.CasterStateRequired);
        byte[] concealReady = AbilityCooldowns.BuildTimer(Conceal, AbilityCooldowns.OwnTimer, 15, false);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(concealAt)
            .Any(frame => frame.Bytes.AsSpan().SequenceEqual(concealReady)),
            "The first Concealment activation's own reuse clears", TimeSpan.FromSeconds(20));

        await ActivateAsync();
        int shotAt = actor.World.RpcSnapshot().Count;
        int friendShotAt = friend.World.ViewSnapshot().Count;
        int enemyShotAt = enemy.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        byte[] damage = AbilityMessages.Damage(false, Shot, actor.Handle, enemy.Handle, 23, 0);
        actor.SendRpc(shot);
        await CheckOffAsync(shotAt, friendShotAt);
        Check.That(!enemy.World.RpcSnapshot().Skip(enemyShotAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(damage)),
            "Concealment ends before the timed shot lands");
        await EvidenceRetestWire.CastBarAsync(actor, Shot, 6);
        await StockAbilityProfile.ObserveAsync(enemy, enemyShotAt,
            frame => frame.Bytes.AsSpan().SequenceEqual(damage), "The admitted concealed shot reaches QAEnemy for 23");
        await EvidenceRetestWire.TimerAsync(actor, shotAt, Shot, AbilityCooldowns.OwnTimer, 45);
        string landed = server.DebugLog()[logAt..];
        Check.That(landed.Contains("1137 off (used ability 504)", StringComparison.Ordinal) &&
            landed.Contains("ability=504 paid=45", StringComparison.Ordinal),
            "The shot consumed the originating Concealment and charged its shipped 45 IS");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Concealed Shot lifecycle protocol framing");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> StartHelperAsync(string name)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            await helper.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
            return helper;
        }

        async Task ActivateAsync()
        {
            int from = actor.World.RpcSnapshot().Count;
            int friendFrom = friend.World.ViewSnapshot().Count;
            actor.SendRpc(ConcealFrame);
            await EvidenceRetestWire.CastBarAsync(actor, Conceal, 2);
            await EvidenceRetestWire.TimerAsync(actor, from, Conceal, AbilityCooldowns.OwnTimer, 15);
            await StockAbilityProfile.ObserveAsync(actor, from,
                frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Conceal, true)), "Concealment toggles on");
            foreach (var (stat, value) in new (ushort, int)[] { (209, Level), (Stealth.ConcealedAbility, 0), (Stealth.StealthStateAbility, 0) })
                await StockAbilityProfile.ObserveAsync(actor, from,
                    frame => StockAbilityProfile.Bonus(frame, Conceal, stat, value, 0, false), $"1137 adds {stat}");
            await EvidenceRetestWire.ViewSinceAsync(friend, friendFrom, friendlyView.ViewId, WireFormat.ToHex(
                Stealth.BuildOther(Stealth.Sneak, PvpFlag.PvpBit | Stealth.StealthedFlag | Stealth.ConcealmentFlag)));
        }

        async Task CheckOffAsync(int from, int friendFrom)
        {
            await StockAbilityProfile.ObserveAsync(actor, from,
                frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Conceal, false)), "Concealment toggles off");
            foreach (var (stat, value) in new (ushort, int)[] { (209, Level), (Stealth.ConcealedAbility, 0), (Stealth.StealthStateAbility, 0) })
                await StockAbilityProfile.ObserveAsync(actor, from,
                    frame => StockAbilityProfile.Bonus(frame, Conceal, stat, value, 0, true), $"1137 removes {stat}");
            await EvidenceRetestWire.ViewSinceAsync(friend, friendFrom, friendlyView.ViewId,
                WireFormat.ToHex(Stealth.BuildOther(0, PvpFlag.PvpBit)));
        }

        async Task HealthAsync(ushort value)
        {
            int from = actor.World.ViewSnapshot().Count;
            actor.SendChat($"?health {value}");
            await EvidenceRetestWire.ViewSinceAsync(actor, from, 2, WireFormat.ToHex(ServerPackets.BuildSelfHealthUpdate(value)[2..]));
        }
    }
}
