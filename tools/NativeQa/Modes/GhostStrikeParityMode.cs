using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class GhostStrikeParityMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort GhostStrike = GhostStrikeAdmission.Id; // 365
    private const ushort InvisibilityPassive = 175;
    private const uint InvisibilityBeltGoid = 460;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, FarZ = 7900;
    private const int EnemyX = 16800, FriendX = 17150;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, InvisibilityPassive, GhostStrike);

    public override string Name => "ability-parity-ghost-strike-parity";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=0,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},rotation=192,alignment=1,healthC=5000,healthM=5000 WHERE charId=6111",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={AcrossWallX},y={WallY},z={WallZ},rotation=192,alignment=2,healthC=5000,healthM=5000 WHERE charId=6113",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6113,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6113,6114) AND slot IN (40,{CombatFixtureWeapons.HandSlot})",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{InvisibilityBeltGoid},40,1,0)"
    }, DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { Spy, GhostStrike }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_GHOST_STRIKE_QA"] = "1",
            ["MXO_GHOST_STRIKE_QA_ROLL"] = ""
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "365",
            ["NativeCloseableRows"] = 1,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost),
            ["CallbackControlAid"] = "?ghoststrike <8|9|off>"
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (+29% Thrown influence from Spy Style). Owned Invisibility_Belt (GOID 460) in bag slot 40. Verified outdoor HOME ({X},{Y},{Z}) faces Melee Dummy 3 m north ({X},{Y},{DummyZ}), QAEnemy 2 m west ({EnemyX},{Y},{Z}), QAFriend 1.5 m east ({FriendX},{Y},{Z}). Page 2: 1=Spy Style (1112), 2=Ghost Strike (365, 10 m), 3=Withdraw. No initial tp needed.",
            "Step 1 (Targeting refusals — no IS debit, cast or timer): Before Spy Style, try slot 2 on QAEnemy -> WrongStyle (13) or local gate. Select slot 1 (Spy Style), try slot 2 with empty selection & /target QAFriend -> InvalidTarget (24). Type printed FAR chunks (12 m > 10 m), turn south and click Melee Dummy -> TargetOutOfRange (34). Type printed WALL chunks + /target QAOther (across solid wall) -> TargetNotInSight (9).",
            "Step 2 (Fixed thrown base 29 + targeted throw + +75 Thrown resistance): Return to HOME, select Melee Dummy, press slot 2 -> 1.5 s cast, 60 IS, 10 s own reuse, targeted knife-throw motion, exact 37 Thrown dmg (fixed 29 + 29%). After 10 s reuse run printed RESISTED chunks (?cd fixture-reset-thrown-resisted), strafe/select fresh dummy, press slot 2 -> [TYPED-RESISTANCE OURS] 365 type7 total75 raw37 final30.",
            "Step 3 (Invisibility Belt callback — roll 9 no-restore vs roll 8 original-expiry restore): /target QAEnemy. Run '?ghoststrike 9', open Actions > Tools > double-click Invisibility Belt (GO460, 1116 active, 50 IS, 130 s), press slot 2 -> ordinary stealth break during 1.5 s cast, 37 Thrown dmg, targeted throw motion on CSR + QAFriend, NO invisibility restoration. Wait 30 s belt reuse, run '?ghoststrike 8', activate belt again, press slot 2 -> ordinary break then restored 1116 for original remaining expiry (<130 s) visible on CSR and QAFriend."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Ghost Strike mode requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var home = Stand(X, Y, Z, indoors: false, facing: 0);
        var far = Stand(X, Y, FarZ, indoors: false, facing: 128);
        var wall = Stand(WallX, WallY, WallZ, indoors: true, facing: 64);
        PrintStand("HOME", home);
        PrintStand("FAR", far);
        PrintStand("WALL", wall);
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME clear sight to Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "HOME clear sight to QAEnemy");
        Check.That(NavMesh.HasSightPath(1, X, Y, FarZ, X, Y, DummyZ), "FAR clear sight beyond 10 m range");
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Solid wall blocks Ghost Strike sight");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-50 loadout fits memory");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        var enemy = await LoginAsync("QAEnemy");
        var friend = await LoginAsync("QAFriend");
        var other = await LoginAsync("QAOther");

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        uint enemyHandle = await HandleAsync(caster, enemy);
        uint friendHandle = await HandleAsync(caster, friend);

        // WrongStyle before Spy Style.
        await RefusedAsync(caster, GhostStrike, Request(GhostStrike, enemyHandle), AbilityRefusals.WrongStyle);

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await ObserveAsync(caster, 0, frame => Bonus(frame, Spy, 1034, 29, 0, false),
            "Spy Style activates +29 Thrown influence");

        // Refusals: empty, ally, stale, >10 m range.
        await RefusedAsync(caster, GhostStrike, Request(GhostStrike, 0), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, GhostStrike, Request(GhostStrike, friendHandle), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, GhostStrike, Request(GhostStrike, enemyHandle ^ 0x10000), AbilityRefusals.InvalidTarget);

        await MoveAsync(far);
        await RefusedAsync(caster, GhostStrike, Request(GhostStrike, dummyHandle), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(home);

        // Cast 1: Fixed thrown 29 + 29% = 37 on QAEnemy + targeted throw motion on CSR and QAFriend.
        var seenCaster = await friend.WaitForViewNamedAsync("CSR_Tester");
        uint seenEnemy = await HandleAsync(friend, enemy);
        int casterRpc = caster.World.RpcSnapshot().Count;
        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(enemyHandle))}");
        caster.SendRpc(Request(GhostStrike, enemyHandle));
        await EvidenceRetestWire.CastBarAsync(caster, GhostStrike, 1.5f);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, GhostStrike, caster.Handle, enemy.Handle, 37, 0)),
            "365 hits QAEnemy for 37 Thrown damage (fixed 29 + 29%)");
        await caster.WaitForViewUpdateAsync(2, MotionPattern(GhostStrike, enemyHandle));
        await friend.WaitForViewUpdateAsync(seenCaster.ViewId, MotionPattern(GhostStrike, seenEnemy));
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, GhostStrike, AbilityCooldowns.OwnTimer, 10);
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=365 paid=60", StringComparison.Ordinal),
            "365 pays 60 IS", HeadlessPlayer.DefaultTimeout);

        // Wait 10 s reuse, then test +75 Thrown resistance on Melee Dummy (37 -> 30).
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(casterRpc)
            .Any(f => BytePattern.Parse($"80 bd 01 6d 01 {AbilityCooldowns.OwnTimer:x2} 00 00 20 41 00").IsMatch(f.Bytes)),
            "365 10 s reuse completes", TimeSpan.FromSeconds(14));

        int creationsBefore = caster.World.CreationSnapshot().Count;
        caster.SkipReceived();
        caster.SendChat("?cd fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset:");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(creationsBefore)
            .Any(v => v.Name == CombatFixtureLoadout.DummyName), "Resisted dummy spawns", HeadlessPlayer.DefaultTimeout);
        var resDummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint resHandle = DuelPackets.Handle(resDummy.ViewId, resDummy.SpawnId);

        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(GhostStrike, resHandle));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[logFrom..].Contains($"[ABILITY] 365 lands on view {resDummy.ViewId} for 30", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains("[TYPED-RESISTANCE OURS] 365 type7 total75 raw37 final30", StringComparison.Ordinal),
            "365 hits +75 Thrown-resisted dummy for 30", TimeSpan.FromSeconds(10));

        // Blocked wall sight refusal.
        await MoveAsync(wall);
        uint otherHandle = await HandleAsync(caster, other);
        await RefusedAsync(caster, GhostStrike, Request(GhostStrike, otherHandle), AbilityRefusals.TargetNotInSight);

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count + other.World.Problems.Count,
            "Clean stock framing for 365 Ghost Strike");
        Console.WriteLine("GHOST STRIKE PASS: refusals, fixed 29->37 neutral, 30 resisted, and targeted throw motion verified.");
        return Array.Empty<byte>();

        async Task MoveAsync(Teleport stand)
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

    private sealed record Teleport(string Command, string Confirmation);

    private static Teleport Stand(int x, int y, int z, bool indoors, byte facing)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == indoors && stand.floorY + 95 == y,
            $"Verified Ghost Strike stand ({x},{y},{z})");
        return new($"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}",
            $"Teleported to {stand.sector:x}/{stand.region:x}");
    }

    private static void PrintStand(string name, Teleport stand)
    {
        string[] words = stand.Command.Split(' ');
        Console.WriteLine($"READY {name} type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
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
