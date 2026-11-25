using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Sixth Sense (149, Infiltrator) stock parity mode:
/// verifies 1500 cm 3D radius (2 m enemy + 7 m neutral included; 16 m enemy excluded),
/// deterministic resist (?sixthroll 5: CastFx 0x7a000003 only, ResistLine 0x60000080, 0 pings),
/// deterministic discovery (?sixthroll 20: CastFx + TargetFx 0x7a000004, DiscoveryLine 0x7a00023c, 2 pings for 10 s),
/// 10 IS cost, and outside-combat refusal.
/// </summary>
public sealed class SixthSenseParityMode : NativeQaMode
{
    private const int Level = 16, SixthRank = 10;
    private const uint SixthDescriptor = 0x80000000u | ((uint)SixthSense.Ability << 10);
    private const int X = SixthSenseFixture.X, Y = SixthSenseFixture.Y, Z = SixthSenseFixture.Z;
    private const int EnemyX = X + 200, NeutralZ = 1600, FarX = SixthSenseFixture.FarX;
    private const string Resist = "81 67 17 00 ?? ?? 80 00 00 60 00 95 00 00 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, SixthSense.Ability);

    public override string Name => "ability-parity-sixth-sense";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6112,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,healthC=1500,healthM=1500,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},z={Z},rotation=192,alignment=2,healthC=1500,healthM=1500 WHERE charId=6114",
        $"UPDATE characters SET x={X},z={NeutralZ},rotation=0,alignment=1,healthC=1500,healthM=1500 WHERE charId=6111",
        $"UPDATE characters SET x={FarX},z={Z},rotation=192,alignment=2,healthC=1500,healthM=1500 WHERE charId=6112",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"UPDATE char_abilities SET level={SixthRank} WHERE char_id=110 AND ability_id={SixthDescriptor}",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6112,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: new ushort[] { SixthSense.Ability },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [SixthSenseQa.Mode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "149",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Sixth Sense (149) at rank {SixthRank} (+{SixthRank * 4} stat 646 Detection vs shipped 50 unstealthed threshold). Verified outdoor HOME ({X},{Y},{Z}) facing QAEnemy 2 m E ({EnemyX},{Y},{Z}, enemy), QAFriend 7 m S ({X},{Y},{NeutralZ}, neutral non-group), QAFar 16 m E ({FarX},{Y},{Z}, outside 15 m). Page 2: 1=Sixth Sense (149).",
            "Step 1 (Resist — roll 5 + 40 = 45 <= 50): type '?sixthroll 5', press slot 1 -> 10 IS, '[SIXTH-SENSE] ... roll=5 detection=40 detected=0 resisted=2 pings=0 castFx=0x7a000003 targetFx=none', CastFx only, 0 minimap dots, QAEnemy+QAFriend receive Scan+Resist, QAFar receives nothing.",
            "Step 2 (Detect — roll 20 + 40 = 60 > 50): type '?sixthroll 20', press slot 1 -> 10 IS, '[SIXTH-SENSE] ... roll=20 detection=40 detected=2 resisted=0 pings=2 castFx=0x7a000003 targetFx=0x7a000004', CastFx+TargetFx, 2 minimap dots for 10 s (red 2 m E + neutral 7 m S), QAFar still excluded.",
            "Step 3 (In-combat refusal): /target QAEnemy (2 m E), open Close Combat -> press slot 1 -> refused locally / 21 (MustBeOutsideCombat) with 0 IS and no scan/FX. Type '?cd stop' to exit combat."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));

        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && !home.indoors, "Verified outdoor HOME");
        Check.That(SixthSense.WithinRadius((X, Y, Z), (EnemyX, Y, Z)), "QAEnemy inside 1500 cm");
        Check.That(SixthSense.WithinRadius((X, Y, Z), (X, Y, NeutralZ)), "QAFriend inside 1500 cm");
        Check.That(!SixthSense.WithinRadius((X, Y, Z), (FarX, Y, Z)), "QAFar outside 1500 cm");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Loadout fits level-16 memory");

        var server = context.ActualServer!;
        var enemy = await LoginAsync("QAEnemy");
        var neutral = await LoginAsync("QAFriend");
        var far = await LoginAsync("QAFar");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        // Verify Step 1: Resist (roll 5 + 40 = 45 <= 50)
        caster.SkipReceived();
        caster.SendChat("?sixthroll 5");
        await caster.WaitForTextAsync("Sixth Sense QA roll set to 5.");

        int casterRpc = caster.World.RpcSnapshot().Count;
        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int neutralRpc = neutral.World.RpcSnapshot().Count;
        int logFrom = server.DebugLog().Length;
        caster.SendRpc(Request());

        var resistLog = new Regex(@"\[SIXTH-SENSE\] caster=CSR_Tester roll=5 detection=40 detected=0 resisted=2 pings=0 castFx=0x7a000003 targetFx=none");
        await caster.WaitUntilAsync(() => resistLog.IsMatch(server.DebugLog()[logFrom..]), "Resist scan logged", HeadlessPlayer.DefaultTimeout);
        Check.That(server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=149 paid=10", StringComparison.Ordinal), "149 pays 10 IS on resist");
        var resistMap = await ObserveAsync(caster, casterRpc, p => p.Bytes is [0x80, 0xc5, ..], "Empty 80 c5 map on resist");
        Check.Equal(0, ReadPingCount(resistMap.Bytes), "Zero map pings on resist");
        await ObserveAsync(enemy, enemyRpc, p => MatchesPattern(p.Bytes, SixthSenseFixture.Scan), "QAEnemy receives ScanLine");
        await ObserveAsync(enemy, enemyRpc, p => MatchesPattern(p.Bytes, Resist), "QAEnemy receives ResistLine");
        await ObserveAsync(neutral, neutralRpc, p => MatchesPattern(p.Bytes, SixthSenseFixture.Scan), "QAFriend receives ScanLine");
        await ObserveAsync(neutral, neutralRpc, p => MatchesPattern(p.Bytes, Resist), "QAFriend receives ResistLine");

        // Verify Step 2: Detect (roll 20 + 40 = 60 > 50); wait past the 0.5 s duplicate-request window.
        await Task.Delay(TimeSpan.FromMilliseconds(750));
        caster.SkipReceived();
        caster.SendChat("?sixthroll 20");
        await caster.WaitForTextAsync("Sixth Sense QA roll set to 20.");

        casterRpc = caster.World.RpcSnapshot().Count;
        enemyRpc = enemy.World.RpcSnapshot().Count;
        neutralRpc = neutral.World.RpcSnapshot().Count;
        logFrom = server.DebugLog().Length;
        caster.SendRpc(Request());

        var detectLog = new Regex(@"\[SIXTH-SENSE\] caster=CSR_Tester roll=20 detection=40 detected=2 resisted=0 pings=2 castFx=0x7a000003 targetFx=0x7a000004");
        await caster.WaitUntilAsync(() => detectLog.IsMatch(server.DebugLog()[logFrom..]), "Detect scan logged", HeadlessPlayer.DefaultTimeout);
        Check.That(server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=149 paid=10", StringComparison.Ordinal), "149 pays 10 IS on detect");
        var detectMap = await ObserveAsync(caster, casterRpc, p => p.Bytes is [0x80, 0xc5, ..], "Two-ping 80 c5 map on detect");
        Check.Equal(2, ReadPingCount(detectMap.Bytes), "Two map pings on detect");
        await ObserveAsync(enemy, enemyRpc, p => MatchesPattern(p.Bytes, SixthSenseFixture.Scan), "QAEnemy receives ScanLine");
        await ObserveAsync(enemy, enemyRpc, p => MatchesPattern(p.Bytes, SixthSenseFixture.Discovery), "QAEnemy receives DiscoveryLine");
        await ObserveAsync(neutral, neutralRpc, p => MatchesPattern(p.Bytes, SixthSenseFixture.Scan), "QAFriend receives ScanLine");
        await ObserveAsync(neutral, neutralRpc, p => MatchesPattern(p.Bytes, SixthSenseFixture.Discovery), "QAFriend receives DiscoveryLine");

        // Confirm QAFar (16 m) received zero scans across both casts
        far.SendRpc("81 48");
        await far.WaitForRpcAsync("81 49");
        Check.Equal(0, far.Received(SixthSenseFixture.Scan).Count, "QAFar at 16 m receives no scan");

        // Verify Step 3: In-combat refusal without IS debit
        var enemyView = await caster.WaitForViewNamedAsync("QAEnemy");
        uint enemyHandle = DuelPackets.Handle(enemyView.ViewId, enemyView.SpawnId);
        int combatRpc = caster.World.RpcSnapshot().Count;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(enemyHandle))}");
        caster.SendRpc($"40 {WireFormat.ToHex(BitConverter.GetBytes(enemyHandle))} 00 00 00 00 00 00 00");
        await ObserveAsync(caster, combatRpc, p => p.Bytes is [0x46, ..], "PvP close combat opens");
        logFrom = server.DebugLog().Length;
        await RefusedAsync(caster, SixthSense.Ability, Request(), AbilityRefusals.MustBeOutsideCombat);
        Check.That(!server.DebugLog()[logFrom..].Contains("[ABILITY-COST] char=110 ability=149", StringComparison.Ordinal), "In-combat refusal spends 0 IS");
        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended.");

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + neutral.World.Problems.Count + far.World.Problems.Count,
            "Clean stock framing");
        Console.WriteLine("SIXTH SENSE PARITY PASS: resist (CastFx only, 0 pings), detect (CastFx+TargetFx, 2 pings), 15 m exclusion, and in-combat refusal verified.");
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

    private static int ReadPingCount(byte[] map)
    {
        Check.That(map.Length >= 5 && map[0] == 0x80 && map[1] == 0xc5, "Valid 80 c5 header");
        Check.Equal(10, map[4], "10 s display duration");
        ushort offset = BitConverter.ToUInt16(map, 2);
        if (offset == 0) return 0;
        Check.That(map.Length >= offset + 2, "80 c5 ping count present");
        return BitConverter.ToUInt16(map, offset);
    }

    private static bool MatchesPattern(byte[] bytes, string pattern)
    {
        string[] tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (bytes.Length < tokens.Length) return false;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] == "??") continue;
            if (bytes[i] != Convert.ToByte(tokens[i], 16)) return false;
        }
        return true;
    }

    private static byte[] Request() =>
        new AbilityUseRecord(SixthSense.Ability, 0, 0, 0, 0, SixthSense.Ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
}
