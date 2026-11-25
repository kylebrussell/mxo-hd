using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Cutthroat / Master Knife Thrower rear sneak parity:
/// Drop Kick (153) full rear-opener gate (Spy Style, Stealthed, live hostile, 6 m, sight, outside 50-deg cone,
/// outside combat), neutral stealth-started hit and +75 Thrown resistance on the heading-locked rear dummy,
/// plus +75 Thrown resistance rear-dummy proof for Leg Throw (1131) and Staggering Throw (348).
/// </summary>
public sealed class DropKickResistedThrowsMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, DummyZ = 6700, RearZ = 6448, ResetHeading = 0;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private const ushort DropKick = SpySneakOpeners.DropKick;
    private const ushort LegThrow = SpySneakOpeners.LegThrow;
    private const ushort StaggeringThrow = SpyMasterSneakOpeners.StaggeringThrow;

    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, DropKick, LegThrow, StaggeringThrow);

    public override string Name => "ability-parity-drop-kick-resisted-throws";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET x=21650,z=16250,rotation=192 WHERE charId=6111",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: new ushort[] { Spy, Sneak, DropKick, LegThrow, StaggeringThrow },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "153,1131,348",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor HOME ({X},{Y},{Z}) is 2 m behind QAEnemy ({EnemyX},{Y},{Z}, facing east); QAFriend observes. No initial tp. Page 2: 1=Spy Style (1112), 2=Sneak (1111), 3=Drop Kick (153), 4=Leg Throw (1131), 5=Staggering Throw (348).",
            "Step 1 (153 Drop Kick neutral rear cast first): slot 1 (Spy Style), slot 2 (Sneak), /target QAEnemy, slot 3 -> instant, 20 IS, 30 s own / 15 s grp 691, Sneak off (1111 off), neutral Thrown hit printed below, no interlock, zero QAFriend records (shipped ILDB name 19598 Drop Kick).",
            "Step 2 (153 refusals without IS/timer; wait out 30 s own reuse or test before landing): no Sneak -> local/23; Sneak + /target QAFriend -> 24; printed FRONT -> 24; BLOCK -> local/9; FAR -> 34; in Close Combat -> local/21 (leave with ?cd stop, then reselect slot 1 Spy Style).",
            "Step 3 (+75 Thrown resistance on rear dummy — 1131, then 348, then 153 so 153's 30 s own reuse expires naturally): type the RESISTED chunks — reset turns the fresh dummy north (heading 0, locked) and puts you at DUMMY (17000,495,6448) 2.52 m behind it; do not walk around it. After 15 s grp 691, click fresh dummy, Sneak, slot 4 (1131, 25 IS, 2 s cast) -> [TYPED-RESISTANCE OURS] 1131 type7 total75. Wait 15 s grp 691 (if Close Combat opened: '?cd stop' then slot 1 Spy Style; repeat RESISTED chunks), click fresh dummy, Sneak, slot 5 (348, 45 IS, 2 s cast) -> [TYPED-RESISTANCE OURS] 348 type7 total75. Wait 15 s grp 691, repeat RESISTED chunks / '?cd stop' + slot 1 if needed, click fresh dummy, Sneak, slot 3 (153, 20 IS, instant) -> [TYPED-RESISTANCE OURS] 153 type7 total75 raw/final matching neutral."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));

        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        var front = StarterConcealedShotTargetingMode.Stand(21850, 16200);
        var block = StarterConcealedShotTargetingMode.Stand(21950, Z);
        var far = StarterConcealedShotTargetingMode.FindFarStand();
        var rear = DummyStand(DummyX, RearZ, facing: 0);

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear 2 m rear approach");
        Check.That(NavMesh.HasSightPath(1, 21850, Y, 16200, EnemyX, Y, Z) &&
            Stealth.InFrustum(EnemyX, Z, 64, 21850, 16200, 50), "Clear frontal stand inside QAEnemy's 50-deg cone");
        Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall blocks sight");
        Check.That(NavMesh.HasSightPath(1, DummyX, DummyY, RearZ, DummyX, DummyY, DummyZ) &&
            !Stealth.InFrustum(DummyX, DummyZ, ResetHeading, DummyX, RearZ, 50), "Clear rear stand outside the dummy's cone");

        var dropSource = AbilityCatalog.Find(DropKick);
        Check.Equal(50d, dropSource.Number("Ability/CastOutsideTargetFOVAngle"), "Shipped 153 rear angle");
        Check.Equal(6d, dropSource.Number("Ability/SelectionRange"), "Shipped 153 range");
        Check.That(SneakFreeThrowAdmission.GatesInstantOpener(DropKick), "153 uses the rear-opener gate");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-50 loadout fits memory");

        PrintChunks("HOME", home.Command);
        PrintChunks("FRONT", front.Command);
        PrintChunks("BLOCK", block.Command);
        PrintChunks("FAR", far.Command);
        PrintChunks("DUMMY", rear.Command);
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");

        var server = context.ActualServer!;
        var enemy = await LoginAsync("QAEnemy");
        var friend = await LoginAsync("QAFriend");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint target = await HandleAsync(caster, enemy);

        // Refusals for 153 Drop Kick before debit
        await RefusedAsync(caster, DropKick, Request(DropKick, target), AbilityRefusals.WrongStyle);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await RefusedAsync(caster, DropKick, Request(DropKick, target), AbilityRefusals.CasterStateRequired);
        await SneakAsync();
        uint friendHandle = await HandleAsync(caster, friend);
        await RefusedAsync(caster, DropKick, Request(DropKick, friendHandle), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, DropKick, Request(DropKick, target ^ 0x10000), AbilityRefusals.InvalidTarget);
        await MoveAsync(front.Command, front.Confirmation);
        await RefusedAsync(caster, DropKick, Request(DropKick, await HandleAsync(caster, enemy)), AbilityRefusals.InvalidTarget);
        await MoveAsync(block.Command, block.Confirmation);
        await RefusedAsync(caster, DropKick, Request(DropKick, await HandleAsync(caster, enemy)), AbilityRefusals.TargetNotInSight);
        await MoveAsync(far.Command, far.Confirmation);
        await RefusedAsync(caster, DropKick, Request(DropKick, await HandleAsync(caster, enemy)), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(home.Command, home.Confirmation);
        target = await HandleAsync(caster, enemy);

        // Neutral 153 Drop Kick on QAEnemy
        var landed = new Regex(@"\[PVP\] CSR_Tester's [^\r\n]* lands on QAEnemy for (\d+)");
        int casterRpc = caster.World.RpcSnapshot().Count, enemyRpc = enemy.World.RpcSnapshot().Count, logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
        caster.SendRpc(Request(DropKick, target));
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[logFrom..]), "153 lands on QAEnemy", TimeSpan.FromSeconds(8));
        int neutral153 = int.Parse(landed.Match(server.DebugLog()[logFrom..]).Groups[1].Value);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, DropKick, caster.Handle, enemy.Handle, (ushort)neutral153, 0)),
            "153 thrown hit reaches QAEnemy");
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, DropKick, AbilityCooldowns.OwnTimer, 30);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal),
            "153 consumes Sneak", HeadlessPlayer.DefaultTimeout);
        Check.That(server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=153 paid=20", StringComparison.Ordinal),
            "153 pays 20 IS");
        Check.That(!caster.World.RpcSnapshot().Skip(casterRpc).Any(r => r.Bytes is [0x46, ..] && r.Bytes.Length == 6 && r.Bytes[5] == 1),
            "153 does not enter interlock");
        Console.WriteLine($"READY BOOTSTRAP 153 neutral stealth opener on QAEnemy: thrown {neutral153}");

        // Move to rear dummy and verify +75 Thrown resistance on 1131, 348, then 153 (so 153's 30 s own reuse expires cleanly).
        await MoveAsync(rear.Command, rear.Confirmation);
        await WaitForGroup691Async();
        await VerifyResistedAsync(LegThrow, 25, expectedRaw: null, castSeconds: 2);
        await WaitForGroup691Async();
        await VerifyResistedAsync(StaggeringThrow, 45, expectedRaw: null, castSeconds: 2);
        await WaitForGroup691Async();
        await VerifyResistedAsync(DropKick, 20, expectedRaw: neutral153, castSeconds: 0);

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("DROP KICK + RESISTED THROWS PASS: 153 refusals/opener and 153/1131/348 +75 Thrown resistance verified.");
        return Array.Empty<byte>();

        async Task VerifyResistedAsync(ushort abilityId, int cost, int? expectedRaw, int castSeconds)
        {
            int created = caster.World.CreationSnapshot().Count;
            caster.SendChat("?combatdepth fixture-reset-thrown-resisted");
            await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
            await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(created).Any(v => v.Name == CombatFixtureLoadout.DummyName),
                $"Fresh resisted Melee Dummy for {abilityId}", HeadlessPlayer.DefaultTimeout);
            var dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
            uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);

            // Re-select Spy Style in case a prior dummy interaction dropped stance, then Sneak.
            caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
            await SneakAsync();

            int from = server.DebugLog().Length;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
            caster.SendRpc(Request(abilityId, dummyHandle));
            var resisted = new Regex($@"\[TYPED-RESISTANCE OURS\] {abilityId} type7 total75 raw(\d+) final(\d+)");
            await caster.WaitUntilAsync(() => resisted.IsMatch(server.DebugLog()[from..]),
                $"+75 Thrown resistance reduces {abilityId}", TimeSpan.FromSeconds(castSeconds + 8));
            var m = resisted.Match(server.DebugLog()[from..]);
            int raw = int.Parse(m.Groups[1].Value);
            int final = int.Parse(m.Groups[2].Value);
            if (expectedRaw.HasValue)
                Check.Equal(expectedRaw.Value, raw, $"{abilityId} resisted raw matches neutral opener");
            Check.Equal(TypedAbilityResistance.Amount((ushort)raw, Level, Level, 75), final, $"{abilityId} resistance formula");
            Check.That(server.DebugLog()[from..].Contains($"[ABILITY-COST] char=110 ability={abilityId} paid={cost}", StringComparison.Ordinal),
                $"{abilityId} pays {cost} IS");
            Check.That(server.DebugLog()[from..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal),
                $"{abilityId} consumes Sneak on landing");
            Console.WriteLine($"READY BOOTSTRAP {abilityId} +75 Thrown resisted on dummy: raw{raw} final{final}");
        }

        async Task WaitForGroup691Async() => await Task.Delay(TimeSpan.FromSeconds(15.5));

        async Task SneakAsync()
        {
            int sneakLog = server.DebugLog().Length;
            caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
            await caster.WaitUntilAsync(() => server.DebugLog()[sneakLog..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
                "Stock Sneak activates", HeadlessPlayer.DefaultTimeout);
        }

        async Task MoveAsync(string command, string confirmation)
        {
            caster.SkipReceived();
            caster.SendChat(command);
            await caster.WaitForTextAsync(confirmation);
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

    private static (string Command, string Confirmation) DummyStand(int x, int z, byte facing)
    {
        Check.That(NavMesh.Locate(1, x, DummyY, z, out var stand) && !stand.indoors && stand.floorY + 95 == DummyY,
            $"Verified outdoor dummy stand ({x},{DummyY},{z})");
        return ($"?tp {x} {DummyY} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}",
            $"Teleported to {stand.sector:x}/{stand.region:x}");
    }

    private static void PrintChunks(string name, string command)
    {
        string[] parts = command.Split(' ');
        Console.WriteLine($"READY {name} type '{string.Join(' ', parts[..3])}', then ' {string.Join(' ', parts[3..7])}', then ' {parts[7]} {parts[8]}'. Press Enter once.");
    }

    private static async Task<uint> HandleAsync(HeadlessPlayer observer, HeadlessPlayer target)
    {
        var view = await observer.WaitForViewNamedAsync(target.Handle);
        return DuelPackets.Handle(view.ViewId, view.SpawnId);
    }

    private static byte[] Request(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
}
