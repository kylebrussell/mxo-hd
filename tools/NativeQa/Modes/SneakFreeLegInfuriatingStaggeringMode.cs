using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class SneakFreeLegInfuriatingStaggeringMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private const ushort LegThrow = SpySneakOpeners.LegThrow;                     // 1131
    private const ushort InfuriatingAssault = SpyMasterSneakOpeners.InfuriatingAssault; // 1130
    private const ushort StaggeringThrow = SpyMasterSneakOpeners.StaggeringThrow; // 348
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, LegThrow, InfuriatingAssault, StaggeringThrow);

    public override string Name => "ability-parity-leg-infuriating-staggering-throws";

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
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak, LegThrow, InfuriatingAssault, StaggeringThrow },
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
            ["ScopedStockRows"] = "1131,1130,348",
            ["NativeCloseableRows"] = 3,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost),
            ["ProcDrawAid"] = "?sneakproc <all|none|off> (defaults all; controls only 50% procs on 1131 & 348)"
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (+54% Thrown influence). Verified outdoor HOME ({X},{Y},{Z}) faces QAEnemy's back 2 m east ({EnemyX},{Y},{Z}) with QAFriend observing nearby. Page 2: 1=Spy Style (1112), 2=Sneak (1111), 3=Leg Throw (1131, 6 m), 4=Infuriating Assault (1130, 4 m), 5=Staggering Throw (348, 6 m). No initial tp needed.",
            "Step 1 (Targeting refusals — no IS debit, cast or timer): Select slot 1 (Spy Style), /target QAEnemy, try slot 3 before Sneak -> local gate or reason 23 (StealthedFlag). Press slot 2 (Sneak), /target QAFriend, try slots 3/4/5 -> reason 24. At printed FRONT (inside QAEnemy's 50-deg cone), BLOCK (solid wall), and FAR (>6 m), /target QAEnemy and try slots 3/4/5 -> reason 24/9/34 or local gate without debit.",
            "Step 2 (1131 Leg Throw rear cast): Return to HOME, /target QAEnemy, Sneak (slot 2), press slot 3. Save the first full stock 1131 request. Expect retained Sneak through the 2.0 s cast, 25 IS, Sneak off on landing, targeted knife-throw motion, 237 Thrown dmg (VF3 154 + 54%), 45 s own / 15 s family 691 reuse, and QAEnemy receiving source1131 Off-Balance (200) for 12 s + paired removal at ~12 s.",
            "Step 3 (1130 Infuriating Assault rear cast): After 15 s family 691 reuse, press slot 2 (Sneak), /target QAEnemy, press slot 4. Save the first full stock 1130 request. Expect retained Sneak through the 2.0 s cast, 40 IS, Sneak off on landing, targeted knife-throw motion, 201 Thrown dmg (VF3 131 + 54%), 45 s own / 15 s family 691 reuse, and QAEnemy receiving source1130 Enraged (93, 100%) for 4 s + paired removal at ~4 s.",
            "Step 4 (348 Staggering Throw rear cast): After 15 s family 691 reuse, press slot 2 (Sneak), /target QAEnemy, press slot 5. Save the first full stock 348 request. Expect retained Sneak through the 2.0 s cast, 45 IS, Sneak off on landing, targeted knife-throw motion, 292 Thrown dmg (VF3 190 + 54%), 45 s own / 15 s family 691 reuse, and QAEnemy receiving source348 Staggered (201) for 12 s + paired removal at ~12 s. ?sneakproc all|none|off fixes only the 50% proc draws on 1131 & 348."
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
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-50 loadout fits memory");

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

        ushort[] all = { LegThrow, InfuriatingAssault, StaggeringThrow };

        // WrongStyle refusal before Spy Style.
        foreach (ushort id in all)
            await RefusedAsync(caster, id, Request(id, target), AbilityRefusals.WrongStyle);

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        // CasterStateRequired (StealthedFlag) before Sneak.
        int preStealth = caster.World.RpcSnapshot().Count;
        foreach (ushort id in all)
            await RefusedAsync(caster, id, Request(id, target), AbilityRefusals.CasterStateRequired);
        foreach (ushort id in all)
            await ObserveAsync(caster, preStealth, rpc => AbilityRefusals.TryDecode(rpc.Bytes, out var r) &&
                r.AbilityID == id && r.StatesNotMet == Stealth.StealthedFlag, $"{id} refusal carries StealthedFlag");

        // Enter Sneak and verify ally, stale, frontal, wall, and range refusals for all three throws.
        await SneakAsync();
        uint friendHandle = await HandleAsync(caster, friend);
        foreach (ushort id in all)
        {
            await RefusedAsync(caster, id, Request(id, friendHandle), AbilityRefusals.InvalidTarget);
            await RefusedAsync(caster, id, Request(id, target ^ 0x10000), AbilityRefusals.InvalidTarget);
        }

        await MoveAsync(front);
        uint frontTarget = await HandleAsync(caster, enemy);
        foreach (ushort id in all)
            await RefusedAsync(caster, id, Request(id, frontTarget), AbilityRefusals.InvalidTarget);

        await MoveAsync(block);
        uint blockTarget = await HandleAsync(caster, enemy);
        foreach (ushort id in all)
            await RefusedAsync(caster, id, Request(id, blockTarget), AbilityRefusals.TargetNotInSight);

        await MoveAsync(far);
        uint farTarget = await HandleAsync(caster, enemy);
        foreach (ushort id in all)
            await RefusedAsync(caster, id, Request(id, farTarget), AbilityRefusals.TargetOutOfRange);

        await MoveAsync(home);
        target = await HandleAsync(caster, enemy);

        var seenCaster = await friend.WaitForViewNamedAsync("CSR_Tester");
        uint seenEnemy = await HandleAsync(friend, enemy);

        // Cast 1: 1131 Leg Throw — 25 IS, 237 dmg, Off-Balance (200) 12 s.
        int casterRpc = await CastAndVerifyAsync(LegThrow, expectedDamage: 237, expectedCost: 25,
            expectedState: TempMods.OffBalanceAbility, stateSeconds: 12f);

        await WaitFamily691EndAsync(casterRpc);
        await SneakAsync();

        // Cast 2: 1130 Infuriating Assault — 40 IS, 201 dmg, Enraged (93) 4 s.
        casterRpc = await CastAndVerifyAsync(InfuriatingAssault, expectedDamage: 201, expectedCost: 40,
            expectedState: TempMods.EnrageAbility, stateSeconds: 4f);

        await WaitFamily691EndAsync(casterRpc);
        await SneakAsync();

        // Cast 3: 348 Staggering Throw — 45 IS, 292 dmg, Staggered (201) 12 s.
        await CastAndVerifyAsync(StaggeringThrow, expectedDamage: 292, expectedCost: 45,
            expectedState: TempMods.StaggeredAbility, stateSeconds: 12f);

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Clean stock framing for 1131, 1130, and 348");
        Console.WriteLine("LEG + INFURIATING + STAGGERING PASS: refusals, rear casts, targeted motions, damage, and state lifecycles verified.");
        return Array.Empty<byte>();

        async Task<int> CastAndVerifyAsync(ushort id, ushort expectedDamage, int expectedCost, ushort expectedState, float stateSeconds)
        {
            int cRpc = caster.World.RpcSnapshot().Count;
            int eRpc = enemy.World.RpcSnapshot().Count;
            int fRpc = friend.World.RpcSnapshot().Count;
            int logFrom = server.DebugLog().Length;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
            caster.SendRpc(Request(id, target));
            await EvidenceRetestWire.CastBarAsync(caster, id, 2);
            await ObserveAsync(enemy, eRpc,
                p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, id, caster.Handle, enemy.Handle, expectedDamage, 0)),
                $"{id} hits QAEnemy for {expectedDamage} Thrown damage");
            await caster.WaitForViewUpdateAsync(2, MotionPattern(id, target));
            await friend.WaitForViewUpdateAsync(seenCaster.ViewId, MotionPattern(id, seenEnemy));
            await EvidenceRetestWire.TimerAsync(caster, cRpc, id, AbilityCooldowns.OwnTimer, 45);
            await EvidenceRetestWire.TimerAsync(caster, cRpc, 691, AbilityCooldowns.GroupTimer, 15);
            await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal) &&
                server.DebugLog()[logFrom..].Contains($"[ABILITY-COST] char=110 ability={id} paid={expectedCost}", StringComparison.Ordinal),
                $"{id} breaks Sneak on landing and pays {expectedCost} IS", HeadlessPlayer.DefaultTimeout);

            var added = await ObserveAsync(enemy, eRpc, f => StateRecord(f, id, expectedState, stateSeconds, false),
                $"{id} applies state {expectedState} for {stateSeconds} s on QAEnemy");
            ManageBonusRecord.TryDecode(added.Bytes, out var rec);
            await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(eRpc)
                .Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == rec! with { AddRemoveType = 1 }),
                $"{id} removes state {expectedState} at ~{stateSeconds} s", TimeSpan.FromSeconds(stateSeconds + 4));
            Check.That(!friend.World.RpcSnapshot().Skip(fRpc).Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var r) && r!.SourceId == id),
                $"{id} does not affect QAFriend");
            return cRpc;
        }

        async Task WaitFamily691EndAsync(int fromRpc) =>
            await ObserveAsync(caster, fromRpc,
                f => BytePattern.Parse($"80 bd 01 b3 02 {AbilityCooldowns.GroupTimer:x2} 00 00 70 41 00").IsMatch(f.Bytes),
                "Family 691 15 s reuse finishes before next rear throw");

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
