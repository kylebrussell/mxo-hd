using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Spy Bull Dog (1110, SneakFreePrototypeAbility): rear-stealthed 4 m admission refusals, targeted knife-throw
/// motion on caster and observer, Sneak break, 10 IS, 10 s own / 15 s family 691 reuse, and 0.5 s NeutralDOT (10 dmg).
/// </summary>
public sealed class SpyBullDog1110Mode : NativeQaMode
{
    private const int Level = 20, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const ushort Sneak = Stealth.SneakGrantAbility, BullDog = StealthShieldAbilities.BullDog; // 1110
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, 151, BullDog);

    public override string Name => "ability-parity-spy-bull-dog-1110";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET x=21650,z=16250,rotation=192 WHERE charId=6111",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id=6114",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new[] { Sneak, BullDog },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1110",
            ["NativeCloseableRows"] = 1,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor HOME ({X},{Y},{Z}) is 2 m behind east-facing QAEnemy ({EnemyX},{Y},{Z}) with QAFriend observing nearby. Page 2: 1=Sneak (1111), 2=Bull Dog (1110, 4 m). No initial tp needed.",
            "Step 1 (Refusals — no IS debit or timer): Try slot 2 with empty selection & /target QAFriend -> reason 24. /target QAEnemy before Sneak -> local gate or reason 23 (StealthedFlag). Press slot 1 (Sneak), teleport to printed FRONT (inside QAEnemy's 50-deg cone), BLOCK (solid wall), and FAR (>4 m), /target QAEnemy, try slot 2 -> reason 24 / 9 / 34 or local gate without debit.",
            "Step 2 (Rear cast at HOME): Return to HOME, press slot 1 (Sneak), /target QAEnemy, press slot 2 (1110). Expect instant cast, 10 IS, 10 s own / 15 s family 691 reuse, Sneak off ('1111 off (used ability 1110)'), targeted knife-throw motion on CSR and QAFriend, '[STEALTH-FREE]1110 terminal10 OURS on QAEnemy' (10 damage at 0.5 s), zero QAFriend records, and no interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));

        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        var front = StarterConcealedShotTargetingMode.Stand(21850, 16200);
        var block = StarterConcealedShotTargetingMode.Stand(21950, Z);
        var far = StarterConcealedShotTargetingMode.FindFarStand();

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear 2 m rear approach");
        Check.That(NavMesh.HasSightPath(1, 21850, Y, 16200, EnemyX, Y, Z) &&
            Stealth.InFrustum(EnemyX, Z, 64, 21850, 16200, 50), "Frontal stand inside QAEnemy's 50-deg cone");
        Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall blocks sight");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-20 loadout fits memory");

        PrintChunks("HOME", home);
        PrintChunks("FRONT", front);
        PrintChunks("BLOCK", block);
        PrintChunks("FAR", far);

        var server = context.ActualServer!;
        var enemy = await LoginAsync("QAEnemy");
        var friend = await LoginAsync("QAFriend");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint target = await HandleAsync(caster, enemy);
        uint friendHandle = await HandleAsync(caster, friend);

        // Refusals before Sneak: empty, ally, unstealthed on live enemy (CasterStateRequired + StealthedFlag).
        await RefusedAsync(caster, BullDog, Request(BullDog, 0), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, BullDog, Request(BullDog, friendHandle), AbilityRefusals.InvalidTarget);
        int preStealth = caster.World.RpcSnapshot().Count;
        await RefusedAsync(caster, BullDog, Request(BullDog, target), AbilityRefusals.CasterStateRequired);
        await ObserveAsync(caster, preStealth, rpc => AbilityRefusals.TryDecode(rpc.Bytes, out var r) &&
            r.AbilityID == BullDog && r.StatesNotMet == Stealth.StealthedFlag, "1110 refusal carries StealthedFlag");

        // Enter Sneak and verify stale, front, wall, and >4 m range refusals.
        await SneakAsync();
        await RefusedAsync(caster, BullDog, Request(BullDog, target ^ 0x10000), AbilityRefusals.InvalidTarget);

        await MoveAsync(front);
        await RefusedAsync(caster, BullDog, Request(BullDog, await HandleAsync(caster, enemy)), AbilityRefusals.InvalidTarget);

        await MoveAsync(block);
        await RefusedAsync(caster, BullDog, Request(BullDog, await HandleAsync(caster, enemy)), AbilityRefusals.TargetNotInSight);

        await MoveAsync(far);
        await RefusedAsync(caster, BullDog, Request(BullDog, await HandleAsync(caster, enemy)), AbilityRefusals.TargetOutOfRange);

        await MoveAsync(home);
        target = await HandleAsync(caster, enemy);

        // Rear cast at HOME: 10 IS, 10 s own / 15 s grp 691, Sneak off, throw motion on CSR + QAFriend, terminal 10 at 0.5 s.
        var seenCaster = await friend.WaitForViewNamedAsync("CSR_Tester");
        uint seenEnemy = await HandleAsync(friend, enemy);
        int casterRpc = caster.World.RpcSnapshot().Count;
        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
        caster.SendRpc(Request(BullDog, target));

        await caster.WaitForViewUpdateAsync(2, MotionPattern(BullDog, target));
        await friend.WaitForViewUpdateAsync(seenCaster.ViewId, MotionPattern(BullDog, seenEnemy));
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, BullDog, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=1110 paid=10", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("1111 off (used ability 1110)", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("[STEALTH-FREE]1110 terminal10 OURS on QAEnemy", StringComparison.Ordinal),
            "1110 pays 10 IS, breaks Sneak, and fires 0.5 s terminal 10 damage", TimeSpan.FromSeconds(8));
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, BullDog, caster.Handle, enemy.Handle, 10, 0)),
            "QAEnemy receives 10 terminal damage from 1110");

        Check.That(!server.DebugLog().Contains("char=6111 source=1110", StringComparison.Ordinal),
            "QAFriend receives zero hostile records");
        Check.That(!server.DebugLog().Contains("[PVP] Round", StringComparison.Ordinal),
            "1110 does not open PvP interlock");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Clean stock framing for 1110 Spy Bull Dog");
        Console.WriteLine("SPY BULL DOG 1110 PASS: refusals, rear throw motion, Sneak break, timers, and 0.5 s terminal 10 verified.");
        return Array.Empty<byte>();

        async Task SneakAsync()
        {
            int from = server.DebugLog().Length;
            caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
            await caster.WaitUntilAsync(() => server.DebugLog()[from..].Contains("1111 on (", StringComparison.Ordinal),
                "Sneak activates", HeadlessPlayer.DefaultTimeout);
        }

        async Task MoveAsync(StarterConcealedShotTargetingMode.Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }

        async Task<HeadlessPlayer> LoginAsync(string name)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
            return player;
        }
    }

    private static void PrintChunks(string label, StarterConcealedShotTargetingMode.Teleport stand)
    {
        string[] w = stand.Command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', w[..3])}', then ' {string.Join(' ', w[3..7])}', then ' {w[7]} {w[8]}'. Press Enter once.");
    }

    private static async Task<uint> HandleAsync(HeadlessPlayer observer, HeadlessPlayer target)
    {
        var view = await observer.WaitForViewNamedAsync(target.Handle);
        return DuelPackets.Handle(view.ViewId, view.SpawnId);
    }

    private static byte[] Request(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();

    private static string MotionPattern(ushort ability, uint target) =>
        $"01 20 ?? 01 0c 40 {LittleEndianHex(ability)} {WireFormat.ToHex(BitConverter.GetBytes(target))}";
}
