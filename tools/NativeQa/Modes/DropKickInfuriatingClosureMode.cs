using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Closes the two remaining native-only checks in the Cutthroat / Master Shadow lane:
/// (1) Drop Kick (153) own rear-opener refusals (pre-Sneak, friend, FRONT, BLOCK, FAR, zero IS debit)
///     before 153 enters its 30 s reuse, followed by its neutral rear landing on QAEnemy;
/// (2) Infuriating Assault (1130) +75 Thrown resistance on the heading-locked north-facing Melee Dummy.
/// </summary>
public sealed class DropKickInfuriatingClosureMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, DummyZ = 6700, RearZ = 6448, ResetHeading = 0;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private const ushort DropKick = SpySneakOpeners.DropKick;                 // 153
    private const ushort InfuriatingAssault = 1130;

    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, DropKick, InfuriatingAssault);

    public override string Name => "ability-parity-drop-kick-infuriating-closure";

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
    }, DevCommands: true, hotbarAbilities: new ushort[] { Spy, Sneak, DropKick, InfuriatingAssault },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "153,1130",
            ["NativeCloseableRows"] = 1,
            ["NativeCloseableIds"] = "1130",
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor HOME ({X},{Y},{Z}) is 2 m behind east-facing QAEnemy ({EnemyX},{Y},{Z}); QAFriend observes. No initial tp. Page 2: 1=Spy Style (1112), 2=Sneak (1111), 3=Drop Kick (153), 4=Infuriating Assault (1130).",
            "Step 1 (153 Drop Kick OWN refusals FIRST, before 153 enters 30 s reuse — all 0 IS, no cast/timer): Select slot 1 (Spy Style), /target QAEnemy (no Sneak) -> press slot 3 (153) -> local/23. Press slot 2 (Sneak): /target QAFriend + slot 3 -> 24; printed FRONT + /target QAEnemy + slot 3 -> 24; BLOCK + /target QAEnemy + slot 3 -> local/9; FAR + /target QAEnemy + slot 3 -> 34; return HOME.",
            "Step 2 (153 Drop Kick neutral rear landing at HOME): Still sneaking behind QAEnemy, /target QAEnemy, press slot 3 (153) -> instant, 20 IS, 30 s own / 15 s grp 691, Sneak off (1111 off), neutral Thrown hit printed below, zero QAFriend records.",
            "Step 3 (1130 Infuriating Assault +75 Thrown resistance on rear dummy): Wait 15 s grp 691, type RESISTED chunks ('?cd fixture-reset-thrown-resisted') -> places CSR 2.52 m behind the fresh north-facing +75 Thrown dummy (heading 0, locked). Click fresh dummy, press slot 2 (Sneak), press slot 4 (1130) -> 2 s cast holding Sneak, 40 IS, 45 s own / 15 s grp 691, Sneak off on landing, '[TYPED-RESISTANCE OURS] 1130 type7 total75 raw/final' printed below + Enraged 93 for 4 s with clean removal."
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
            !Stealth.InFrustum(DummyX, DummyZ, ResetHeading, DummyX, RearZ, 50), "Clear rear stand outside dummy cone");
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

        // Step 1: 153 Drop Kick OWN refusals before any IS debit or cooldown.
        await RefusedAsync(caster, DropKick, Request(DropKick, target), AbilityRefusals.WrongStyle);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await RefusedAsync(caster, DropKick, Request(DropKick, target), AbilityRefusals.CasterStateRequired);
        await SneakAsync();
        uint friendHandle = await HandleAsync(caster, friend);
        await RefusedAsync(caster, DropKick, Request(DropKick, 0), AbilityRefusals.InvalidTarget);
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

        // Step 2: Neutral 153 Drop Kick rear opener on QAEnemy.
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
        Console.WriteLine($"READY BOOTSTRAP 153 neutral stealth opener on QAEnemy: thrown {neutral153}");

        // Step 3: +75 Thrown resistance on north-facing rear dummy for 1130 Infuriating Assault.
        await Task.Delay(TimeSpan.FromSeconds(15.5));
        int created = caster.World.CreationSnapshot().Count;
        caster.SendChat("?combatdepth fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(created).Any(v => v.Name == CombatFixtureLoadout.DummyName),
            "Fresh resisted Melee Dummy for 1130", HeadlessPlayer.DefaultTimeout);
        var dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await SneakAsync();

        int from = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
        caster.SendRpc(Request(InfuriatingAssault, dummyHandle));
        await EvidenceRetestWire.CastBarAsync(caster, InfuriatingAssault, 2);
        var resisted = new Regex(@"\[TYPED-RESISTANCE OURS\] 1130 type7 total75 raw(\d+) final(\d+)");
        await caster.WaitUntilAsync(() => resisted.IsMatch(server.DebugLog()[from..]),
            "+75 Thrown resistance reduces 1130", TimeSpan.FromSeconds(10));
        var m = resisted.Match(server.DebugLog()[from..]);
        int raw1130 = int.Parse(m.Groups[1].Value);
        int final1130 = int.Parse(m.Groups[2].Value);
        Check.Equal(TypedAbilityResistance.Amount((ushort)raw1130, Level, Level, 75), final1130, "1130 resistance formula");
        Check.That(server.DebugLog()[from..].Contains("[ABILITY-COST] char=110 ability=1130 paid=40", StringComparison.Ordinal),
            "1130 pays 40 IS");
        Check.That(server.DebugLog()[from..].Contains("[STEALTH] CSR_Tester 1111 off (landed ability 1130)", StringComparison.Ordinal) ||
            server.DebugLog()[from..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal),
            "1130 consumes Sneak on landing");
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[from..].Contains("source=1130 stat=93 add", StringComparison.Ordinal) &&
            server.DebugLog()[from..].Contains("source=1130 stat=93 remove", StringComparison.Ordinal),
            "1130 Enraged 93 applies and expires cleanly at 4 s", TimeSpan.FromSeconds(10));
        Console.WriteLine($"READY BOOTSTRAP 1130 +75 Thrown resisted on dummy: raw{raw1130} final{final1130}");

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine($"DROP KICK & INFURIATING CLOSURE PASS: 153 refusals+{neutral153} opener and 1130 resisted {raw1130}->{final1130} + Enraged 4 s verified.");
        return Array.Empty<byte>();

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
