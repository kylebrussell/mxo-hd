using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Stock-client observation batch for the Knife Thrower rows that still need presentation or a resisted sample:
/// 280 and 279 cast from Self Defense (server style auto-switch) and their at-rest presentation, 279 pressed
/// inside Close Combat, and 287 Suplex on the +75 Thrown-resisted rear dummy.
/// </summary>
public sealed class KnifeThrowerClosureMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private const ushort DeadlyThrow = DeadlyThrowDamage.Id;               // 279
    private const ushort CripplingThrow = SpyAbilityDamage.CripplingThrow; // 280
    private const ushort Suplex = SuplexDamage.Suplex;                     // 287
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, DummyBackZ = 6448, EnemyX = 16800;
    public const ushort DeadlyNeutral = 67, SuplexNeutral = 646, SuplexResisted = 531;
    public static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, DeadlyThrow, CripplingThrow, Suplex);

    public override string Name => "ability-parity-knife-thrower-closure";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=0,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x=17150,y={Y},z={Z},rotation=192,alignment=1,healthC=5000,healthM=5000 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { Spy, DeadlyThrow, CripplingThrow, Sneak, Suplex },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "279,280,287",
            ["NativeCloseableRows"] = 2,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. HOME ({X},{Y},{Z}) faces the Melee Dummy 3 m north; QAEnemy stands 2 m west. Page 2: 1=Spy Style, 2=Deadly Throw 279, 3=Crippling Throw 280, 4=Sneak, 5=Suplex 287. Do NOT press slot 1: you start in Self Defense.",
            "Step 1 (280 auto-switch + presentation): /target QAEnemy, press slot 3. Expect the style to switch to Spy Style without pressing slot 1, then a 2.0 s cast, 20 IS, a 1-point Thrown hit and Rooted on QAEnemy. Watch the CSR from cast start to 2 s after landing and write down exactly what it does (throw motion, cast pose only, or nothing).",
            $"Step 2 (279 presentation at rest): wait 4 s, /target QAEnemy, press slot 2. Expect 2.0 s cast, 18 IS, {DeadlyNeutral} Thrown (plus 279's records). Write down the CSR's motion the same way.",
            "Step 3 (279 inside Close Combat): /target Melee Dummy, right-click it and choose Close Combat. Once the fight starts (Spy Style still active; press slot 1 if the style changed), press slot 2. Record the client's answer word for word: a local refusal, or a cast bar. If it casts, note whether the fight keeps going and save the server lines from '[ABILITY] 279' and the '[COMBAT-VIEW] Round' lines around it.",
            $"Step 4 (287 resisted + presentation): type '?cd stop'. Type the printed DUMMY chunks, then the RESISTED chunks. Stay where the reset puts you (behind the dummy). Select the new dummy, press slot 1 if Close Combat changed your style, slot 4 (Sneak), then slot 5. Expect '[TYPED-RESISTANCE OURS] 287 type7 total75 raw{SuplexNeutral} final{SuplexResisted}' and a {SuplexResisted} hit. Write down whether the CSR plays a Suplex move or only a hit spark."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Knife Thrower closure mode requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME has clear sight to Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "HOME has clear sight to QAEnemy");
        Check.That(NavMesh.Locate(1, X, Y, DummyBackZ, out var dummyStand) && !dummyStand.indoors && dummyStand.floorY + 95 == Y,
            "Verified rear stand behind Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, DummyBackZ, X, Y, DummyZ) && !Stealth.InFrustum(X, DummyZ, 0, X, DummyBackZ, 50),
            "Rear stand is behind the north-facing reset Melee Dummy");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-50 loadout fits memory");
        var dummyTp = $"?tp {X} {Y} {DummyBackZ} {dummyStand.sector:x} {dummyStand.region:x} {dummyStand.polygon:x} - 00";
        var dummyConfirm = $"Teleported to {dummyStand.sector:x}/{dummyStand.region:x}";
        string[] w = dummyTp.Split(' ');
        Console.WriteLine($"READY DUMMY type '{string.Join(' ', w[..3])}', then ' {string.Join(' ', w[3..7])}', then ' {w[7]} {w[8]}'. Press Enter once.");
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        var enemy = await LoginAsync("QAEnemy");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint enemyHandle = DuelPackets.Handle((await caster.WaitForViewNamedAsync(enemy.Handle)).ViewId,
            (await caster.WaitForViewNamedAsync(enemy.Handle)).SpawnId);

        // 1. From Self Defense, the loaded special move switches to Spy Style before its cast.
        int casterRpc = caster.World.RpcSnapshot().Count;
        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int logFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(enemyHandle))}");
        caster.SendRpc(Request(CripplingThrow, enemyHandle));
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await EvidenceRetestWire.CastBarAsync(caster, CripplingThrow, 2);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, CripplingThrow, caster.Handle, enemy.Handle, 1, 0)),
            "280 cast from Self Defense switches to Spy Style and hits QAEnemy for 1");
        await ObserveAsync(enemy, enemyRpc, f => ManageBonusRecord.TryDecode(f.Bytes, out var r) && r!.Type == TempMods.KindState &&
            r.SourceId == CripplingThrow && r.TargetId == TempMods.RootedAbility && r.AddRemoveType == 0, "280 roots QAEnemy");
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=280 paid=20", StringComparison.Ordinal),
            "280 pays 20 IS", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(caster, casterRpc, 1215, AbilityCooldowns.GroupTimer, 3);

        // 2. 279 at rest: 18 IS and the level-50 Thrown hit (it also breaks 280's root).
        enemyRpc = enemy.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(DeadlyThrow, enemyHandle));
        await EvidenceRetestWire.CastBarAsync(caster, DeadlyThrow, 2);
        await ObserveAsync(enemy, enemyRpc,
            p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, DeadlyThrow, caster.Handle, enemy.Handle, DeadlyNeutral, 0)),
            $"279 hits QAEnemy for {DeadlyNeutral}");
        await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=279 paid=18", StringComparison.Ordinal),
            "279 pays 18 IS", HeadlessPlayer.DefaultTimeout);

        // 4. 287 on the +75 Thrown-resisted rear dummy.
        caster.SkipReceived();
        caster.SendChat(dummyTp);
        await caster.WaitForTextAsync(dummyConfirm);
        int creationsBefore = caster.World.CreationSnapshot().Count;
        caster.SkipReceived();
        caster.SendChat("?cd fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset:");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(creationsBefore)
            .Any(v => v.Name == CombatFixtureLoadout.DummyName), "Resisted dummy spawns", HeadlessPlayer.DefaultTimeout);
        var resDummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint resHandle = DuelPackets.Handle(resDummy.ViewId, resDummy.SpawnId);

        int sneakFrom = server.DebugLog().Length;
        caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await caster.WaitUntilAsync(() => server.DebugLog()[sneakFrom..].Contains("1111 on (", StringComparison.Ordinal),
            "Sneak activates", HeadlessPlayer.DefaultTimeout);
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request(Suplex, resHandle));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[logFrom..].Contains($"[ABILITY] 287 lands on view {resDummy.ViewId} for {SuplexResisted}", StringComparison.Ordinal) &&
            server.DebugLog()[logFrom..].Contains($"[TYPED-RESISTANCE OURS] 287 type7 total75 raw{SuplexNeutral} final{SuplexResisted}", StringComparison.Ordinal),
            $"287 hits the +75 Thrown-resisted rear dummy for {SuplexResisted}", TimeSpan.FromSeconds(10));

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean stock framing for the closure batch");
        Console.WriteLine("KNIFE THROWER CLOSURE PASS: 280 auto-switch and root, 279 at rest, and 287 resisted verified.");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> LoginAsync(string name)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
            return player;
        }
    }

    private static byte[] Request(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
}
