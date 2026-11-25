using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class SneakFreeHarryingDazzlingMode : NativeQaMode
{
    private const int Level = 23, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private const ushort Harrying = HarryingAttackDamage.Id, Dazzling = DazzlingThrowDamage.Id;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, Harrying, Dazzling);

    public override string Name => "ability-parity-harrying-dazzling-throws";

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
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak, Harrying, Dazzling },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [SneakFreeControlsQa.Mode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1127,1132",
            ["NativeCloseableRows"] = 2,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost),
            ["ProcDrawAid"] = "?sneakproc <all|none|off> (defaults all)"
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (+25% Thrown influence). Verified outdoor HOME ({X},{Y},{Z}) faces QAEnemy's back 2 m east ({EnemyX},{Y},{Z}) with QAFriend observing nearby. Page 2: 1=Spy Style (1112), 2=Sneak (1111), 3=Harrying Attack (1127, 4 m), 4=Dazzling Throw (1132, 6 m). No initial tp needed.",
            "Step 1 (Targeting refusals — no IS debit, cast or timer): Select slot 1 (Spy Style), /target QAEnemy, try slot 3 before Sneak -> local gate or reason 23 (StealthedFlag). Press slot 2 (Sneak), /target QAFriend, try slot 3 & slot 4 -> reason 24. At printed FRONT (inside QAEnemy's 50-deg cone), BLOCK (solid wall), and FAR (>6 m), /target QAEnemy and try slot 3 or 4 -> reason 24/9/34 or local gate without debit.",
            "Step 2 (1127 Harrying Attack rear cast): Return to HOME, /target QAEnemy, Sneak (slot 2), press slot 3. Save the first full stock 1127 request. Expect retained Sneak through the 2.0 s cast, 25 IS, Sneak off on landing, targeted knife-throw motion, 95 Thrown dmg (base 76 + 25%), 45 s own / 15 s family 691 reuse, and QAEnemy receiving source1127 Dazed (199), Off-Balance (200), and Staggered (201) for 12 s (with matching removals at ~12 s).",
            "Step 3 (1132 Dazzling Throw rear cast): After 15 s family 691 reuse, press slot 2 (Sneak), /target QAEnemy, press slot 4. Save the first full stock 1132 request. Expect retained Sneak through the 2.0 s cast, 35 IS, Sneak off on landing, targeted knife-throw motion, 53 Thrown dmg (base 43 + 25%), 45 s own / 15 s family 691 reuse, and QAEnemy receiving source1132 Dazed (199) for 12 s (with matching removal at ~12 s). QA aid fixes only probabilistic state draws (?sneakproc all|none|off)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));

        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        var front = StarterConcealedShotTargetingMode.Stand(21850, 16200);
        var block = StarterConcealedShotTargetingMode.Stand(21950, Z);
        var far = StarterConcealedShotTargetingMode.FindFarStand();

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear two-metre rear approach");
        Check.That(NavMesh.HasSightPath(1, 21850, Y, 16200, EnemyX, Y, Z) &&
            Stealth.InFrustum(EnemyX, Z, 64, 21850, 16200, 50), "Clear frontal stand inside QAEnemy's 50-deg cone");
        Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall blocks throw sight");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-23 loadout fits memory");

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

        // WrongStyle refusal before Spy Style.
        await RefusedAsync(caster, Harrying, Request(Harrying, target), AbilityRefusals.WrongStyle);
        await RefusedAsync(caster, Dazzling, Request(Dazzling, target), AbilityRefusals.WrongStyle);

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        // CasterStateRequired (StealthedFlag) before Sneak.
        int preStealth = caster.World.RpcSnapshot().Count;
        await RefusedAsync(caster, Harrying, Request(Harrying, target), AbilityRefusals.CasterStateRequired);
        await ObserveAsync(caster, preStealth, rpc => AbilityRefusals.TryDecode(rpc.Bytes, out var r) &&
            r.AbilityID == Harrying && r.StatesNotMet == Stealth.StealthedFlag, "1127 refusal carries StealthedFlag");
        await RefusedAsync(caster, Dazzling, Request(Dazzling, target), AbilityRefusals.CasterStateRequired);

        // Enter Sneak and verify ally, stale, frontal, wall, and range refusals for both throws.
        await SneakAsync();
        uint friendHandle = await HandleAsync(caster, friend);
        await RefusedAsync(caster, Harrying, Request(Harrying, friendHandle), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, Dazzling, Request(Dazzling, friendHandle), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, Harrying, Request(Harrying, target ^ 0x10000), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, Dazzling, Request(Dazzling, target ^ 0x10000), AbilityRefusals.InvalidTarget);

        await MoveAsync(front);
        await RefusedAsync(caster, Harrying, Request(Harrying, await HandleAsync(caster, enemy)), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, Dazzling, Request(Dazzling, await HandleAsync(caster, enemy)), AbilityRefusals.InvalidTarget);

        await MoveAsync(block);
        await RefusedAsync(caster, Harrying, Request(Harrying, await HandleAsync(caster, enemy)), AbilityRefusals.TargetNotInSight);
        await RefusedAsync(caster, Dazzling, Request(Dazzling, await HandleAsync(caster, enemy)), AbilityRefusals.TargetNotInSight);

        await MoveAsync(far);
        await RefusedAsync(caster, Harrying, Request(Harrying, await HandleAsync(caster, enemy)), AbilityRefusals.TargetOutOfRange);
        await RefusedAsync(caster, Dazzling, Request(Dazzling, await HandleAsync(caster, enemy)), AbilityRefusals.TargetOutOfRange);

        await MoveAsync(home);
        target = await HandleAsync(caster, enemy);

        // Cast 1: Harrying Attack (1127) — 2 s cast retaining Sneak, 25 IS, 95 dmg, targeted throw, Dazed/Off-Balance/Staggered 12 s.
        var seenCaster = await friend.WaitForViewNamedAsync("CSR_Tester");
        uint seenEnemy = await HandleAsync(friend, enemy);
        int casterRpc = caster.World.RpcSnapshot().Count;
        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
        caster.SendRpc(Request(Harrying, target));
        await EvidenceRetestWire.CastBarAsync(caster, Harrying, 2);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, Harrying, caster.Handle, enemy.Handle, 95, 0)),
            "1127 hits QAEnemy for 95 Thrown damage");
        await caster.WaitForViewUpdateAsync(2, MotionPattern(Harrying, target));
        await friend.WaitForViewUpdateAsync(seenCaster.ViewId, MotionPattern(Harrying, seenEnemy));
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, Harrying, AbilityCooldowns.OwnTimer, 45);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=1127 paid=25", StringComparison.Ordinal),
            "1127 breaks Sneak on landing and pays 25 IS", HeadlessPlayer.DefaultTimeout);

        var hStates = new List<RpcRecord>();
        foreach (ushort state in new[] { TempMods.DazeAbility, TempMods.OffBalanceAbility, TempMods.StaggeredAbility })
        {
            var added = await ObserveAsync(enemy, enemyRpc,
                f => StateRecord(f, Harrying, state, 12f, remove: false),
                $"1127 applies state {state} for 12 s on QAEnemy");
            hStates.Add(added);
        }
        foreach (var added in hStates)
        {
            ManageBonusRecord.TryDecode(added.Bytes, out var rec);
            await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(enemyRpc)
                .Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == rec! with { AddRemoveType = 1 }),
                $"1127 removes state {rec!.TargetId} at ~12 s", TimeSpan.FromSeconds(15));
        }

        // Wait for 15 s family 691 reuse end before casting 1132.
        await ObserveAsync(caster, casterRpc,
            f => BytePattern.Parse($"80 bd 01 b3 02 {AbilityCooldowns.GroupTimer:x2} 00 00 70 41 00").IsMatch(f.Bytes),
            "Family 691 15 s reuse finishes before 1132");

        // Cast 2: Dazzling Throw (1132) — re-Sneak, 2 s cast retaining Sneak, 35 IS, 53 dmg, targeted throw, Dazed 12 s.
        await SneakAsync();
        casterRpc = caster.World.RpcSnapshot().Count;
        enemyRpc = enemy.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
        caster.SendRpc(Request(Dazzling, target));
        await EvidenceRetestWire.CastBarAsync(caster, Dazzling, 2);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, Dazzling, caster.Handle, enemy.Handle, 53, 0)),
            "1132 hits QAEnemy for 53 Thrown damage");
        await caster.WaitForViewUpdateAsync(2, MotionPattern(Dazzling, target));
        await friend.WaitForViewUpdateAsync(seenCaster.ViewId, MotionPattern(Dazzling, seenEnemy));
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, Dazzling, AbilityCooldowns.OwnTimer, 45);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=1132 paid=35", StringComparison.Ordinal),
            "1132 breaks Sneak on landing and pays 35 IS", HeadlessPlayer.DefaultTimeout);

        var dazeAdd = await ObserveAsync(enemy, enemyRpc,
            f => StateRecord(f, Dazzling, TempMods.DazeAbility, 12f, remove: false),
            "1132 applies Dazed (199) for 12 s on QAEnemy");
        ManageBonusRecord.TryDecode(dazeAdd.Bytes, out var dazeRec);
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(enemyRpc)
            .Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == dazeRec! with { AddRemoveType = 1 }),
            "1132 removes Dazed (199) at ~12 s", TimeSpan.FromSeconds(15));

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Clean stock framing for 1127 and 1132");
        Console.WriteLine("HARRYING + DAZZLING PASS: full refusals, rear casts, targeted motions, damage, and 12 s state lifecycles verified.");
        return Array.Empty<byte>();

        async Task SneakAsync()
        {
            int sneakLog = server.DebugLog().Length;
            caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
            await caster.WaitUntilAsync(() => server.DebugLog()[sneakLog..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
                "Stock Sneak activates", HeadlessPlayer.DefaultTimeout);
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

    private static void PrintChunks(string name, StarterConcealedShotTargetingMode.Teleport stand)
    {
        string[] parts = stand.Command.Split(' ');
        Console.WriteLine($"READY {name} type '{string.Join(' ', parts[..3])}', then ' {string.Join(' ', parts[3..7])}', then ' {parts[7]} {parts[8]}'. Press Enter once.");
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

    private static bool StateRecord(RpcRecord frame, ushort source, ushort state, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindState &&
        r.SourceId == source && r.TargetId == state && r.SignedValue == 0 &&
        Math.Abs(r.Duration - seconds) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);
}
