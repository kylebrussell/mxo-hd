using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Weakening Blow (1133, Assassin) and Warding Blow (1134, Master Assassin): the shared SneakFree rear-throw admission,
/// SneakFree*_Subject targeted knife-throw motion, level-scaled thrown damage, both 8 s debuff records, and +75 Thrown
/// resistance on the rear of the Melee Dummy. Player stands reuse the PR1145 Harrying/Dazzling geometry.
/// </summary>
public sealed class SneakFreeWeakeningWardingMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, DummyZ = 6700, RearZ = 6900;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private const ushort Weakening = SpySneakOpeners.WeakeningBlow, Warding = SpyMasterSneakOpeners.WardingBlow;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, Weakening, Warding);
    private static readonly (ushort Id, int Cost, int Value)[] Rows = { (Weakening, 35, -45), (Warding, 45, -30) };

    public override string Name => "ability-parity-weakening-warding-blows";

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
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak, Weakening, Warding },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1133,1134",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor HOME ({X},{Y},{Z}) is 2 m behind QAEnemy ({EnemyX},{Y},{Z}, facing east); QAFriend observes nearby. No initial tp. Page 2: 1=Spy Style (1112), 2=Sneak (1111), 3=Weakening Blow (1133, 6 m), 4=Warding Blow (1134, 4 m).",
            "Step 1 (cast first): select slot 1, press slot 2 (Sneak), /target QAEnemy, press slot 3. Save the first full 1133 request. Expect Sneak kept through the 2.0 s cast, 35 IS, Sneak off on landing, the targeted knife-throw motion, the thrown hit printed below, 45 s own / 15 s family 691 reuse, and two source1133 -45 records for 8 s (Melee and Ballistic Damage Resistance) with matching removals. Keep QAEnemy HP before/after.",
            "Step 2: after the 15 s family timer, Sneak again, press slot 4 (1134). Save the first full request. Expect 45 IS, the targeted motion, the hit printed below, 45 s own / 15 s 691, and two source1134 -30 records for 8 s (Melee and Ballistic Accuracy) with matching removals.",
            "Step 3 (refusals, no IS, cast or timer; wait out the 45 s reuse or expect a local gate): drop Sneak and press 3 -> 23 or local gate. Sneak, /target QAFriend -> 24. At the printed FRONT (inside QAEnemy's 50-degree cone) -> 24, BLOCK (solid wall) -> 9, FAR -> 34, each for slots 3 and 4.",
            "Step 4 (+75 Thrown resistance): type the printed DUMMY chunks (2 m behind the Melee Dummy, which faces south), then the RESISTED chunks. Strafe, click the fresh dummy, Sneak, press slot 3 after its reuse -> [TYPED-RESISTANCE OURS] 1133 type7 total75 with the values printed below. The hit dummy retaliates into Close Combat: after 15 s type the RESISTED chunks again, click the fresh dummy, Sneak and press slot 4 -> the printed 1134 resisted values."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));

        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        var front = StarterConcealedShotTargetingMode.Stand(21850, 16200);
        var block = StarterConcealedShotTargetingMode.Stand(21950, Z);
        var far = StarterConcealedShotTargetingMode.FindFarStand();
        var rear = DummyStand(DummyX, RearZ, facing: 128);

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear two-metre rear approach");
        Check.That(NavMesh.HasSightPath(1, 21850, Y, 16200, EnemyX, Y, Z) &&
            Stealth.InFrustum(EnemyX, Z, 64, 21850, 16200, 50), "Clear frontal stand inside QAEnemy's 50-deg cone");
        Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall blocks throw sight");
        Check.That(NavMesh.HasSightPath(1, DummyX, DummyY, RearZ, DummyX, DummyY, DummyZ) &&
            !Stealth.InFrustum(DummyX, DummyZ, 128, DummyX, RearZ, 50), "Clear rear stand outside the dummy's 50-deg cone");
        foreach (var row in Rows)
        {
            var source = AbilityCatalog.Find(row.Id);
            Check.Equal(50d, source.Number("Ability/CastOutsideTargetFOVAngle"), $"Shipped {row.Id} rear angle");
            Check.Equal(1d, source.Number("Ability/CastWhenUsed"), $"Shipped {row.Id} outside-combat opener");
            Check.Equal(2d, source.Number("Ability/CastingTime"), $"Shipped {row.Id} cast");
        }
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

        foreach (var row in Rows)
            await RefusedAsync(caster, row.Id, Request(row.Id, target), AbilityRefusals.WrongStyle);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        foreach (var row in Rows)
            await RefusedAsync(caster, row.Id, Request(row.Id, target), AbilityRefusals.CasterStateRequired);

        await SneakAsync();
        uint friendHandle = await HandleAsync(caster, friend);
        foreach (var row in Rows)
        {
            await RefusedAsync(caster, row.Id, Request(row.Id, friendHandle), AbilityRefusals.InvalidTarget);
            await RefusedAsync(caster, row.Id, Request(row.Id, target ^ 0x10000), AbilityRefusals.InvalidTarget);
        }
        await MoveAsync(front.Command, front.Confirmation);
        foreach (var row in Rows)
            await RefusedAsync(caster, row.Id, Request(row.Id, await HandleAsync(caster, enemy)), AbilityRefusals.InvalidTarget);
        await MoveAsync(block.Command, block.Confirmation);
        foreach (var row in Rows)
            await RefusedAsync(caster, row.Id, Request(row.Id, await HandleAsync(caster, enemy)), AbilityRefusals.TargetNotInSight);
        await MoveAsync(far.Command, far.Confirmation);
        foreach (var row in Rows)
            await RefusedAsync(caster, row.Id, Request(row.Id, await HandleAsync(caster, enemy)), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(home.Command, home.Confirmation);
        target = await HandleAsync(caster, enemy);

        var seenCaster = await friend.WaitForViewNamedAsync("CSR_Tester");
        uint seenEnemy = await HandleAsync(friend, enemy);
        var hits = new Dictionary<ushort, int>();
        var landed = new Regex(@"\[PVP\] CSR_Tester's [^\r\n]* lands on QAEnemy for (\d+)");
        foreach (var row in Rows)
        {
            // Sneak from the refusal phase is still active for the first cast; each landing then consumes it.
            if (row.Id == Warding)
            {
                await Task.Delay(TimeSpan.FromSeconds(15.25));
                await SneakAsync();
            }
            int casterRpc = caster.World.RpcSnapshot().Count, enemyRpc = enemy.World.RpcSnapshot().Count;
            int logFrom = server.DebugLog().Length;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
            caster.SendRpc(Request(row.Id, target));
            await EvidenceRetestWire.CastBarAsync(caster, row.Id, 2);
            await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[logFrom..]), $"{row.Id} lands on QAEnemy", TimeSpan.FromSeconds(8));
            int hit = int.Parse(landed.Match(server.DebugLog()[logFrom..]).Groups[1].Value);
            hits[row.Id] = hit;
            await ObserveAsync(enemy, enemyRpc,
                p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, row.Id, caster.Handle, enemy.Handle, (ushort)hit, 0)),
                $"{row.Id} thrown hit reaches QAEnemy");
            await caster.WaitForViewUpdateAsync(2, MotionPattern(row.Id, target));
            await friend.WaitForViewUpdateAsync(seenCaster.ViewId, MotionPattern(row.Id, seenEnemy));
            await EvidenceRetestWire.TimerAsync(caster, casterRpc, row.Id, AbilityCooldowns.OwnTimer, 45);
            await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
            Check.That(server.DebugLog()[logFrom..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal) &&
                server.DebugLog()[logFrom..].Contains($"[ABILITY-COST] char=110 ability={row.Id} paid={row.Cost}", StringComparison.Ordinal),
                $"{row.Id} breaks Sneak on landing and pays {row.Cost} IS");
            var stats = new List<ManageBonusRecord>();
            await enemy.WaitUntilAsync(() =>
            {
                stats = enemy.World.RpcSnapshot().Skip(enemyRpc).Select(f => ManageBonusRecord.TryDecode(f.Bytes, out var r) ? r : null)
                    .Where(r => r != null && r.SourceId == row.Id && r.SignedValue == row.Value && Math.Abs(r.Duration - 8) < 0.001 &&
                        r.AddRemoveType == 0).Select(r => r!).GroupBy(r => r.TargetId).Select(g => g.First()).ToList();
                return stats.Count == 2;
            }, $"{row.Id} adds both {row.Value} 8 s records", HeadlessPlayer.DefaultTimeout);
            foreach (var added in stats)
                await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(enemyRpc)
                    .Any(f => ManageBonusRecord.TryDecode(f.Bytes, out var rem) && rem == added with { AddRemoveType = 1 }),
                    $"{row.Id} removes stat {added.TargetId} at 8 s", TimeSpan.FromSeconds(11));
            Console.WriteLine($"READY BOOTSTRAP {row.Id} on QAEnemy: thrown {hit}; records {string.Join(",", stats.Select(s => $"{s.TargetId}{s.SignedValue:+0;-0}"))} 8 s");
        }

        // +75 Thrown resistance on the rear of the stationary dummy, once each throw's own reuse has ended.
        await MoveAsync(rear.Command, rear.Confirmation);
        int created = caster.World.CreationSnapshot().Count;
        caster.SendChat("?combatdepth fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(created).Any(v => v.Name == CombatFixtureLoadout.DummyName),
            "A fresh resisted Melee Dummy", HeadlessPlayer.DefaultTimeout);
        var dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        await Task.Delay(TimeSpan.FromSeconds(16));
        var resisted = new Regex(@"\[TYPED-RESISTANCE OURS\] (\d+) type7 total75 raw(\d+) final(\d+)");
        foreach (var row in Rows)
        {
            if (row.Id == Warding)
            {
                // The hit dummy retaliates into Close Combat; a fresh resisted dummy restores the outside-combat opener.
                await Task.Delay(TimeSpan.FromSeconds(15.25));
                int reset = caster.World.CreationSnapshot().Count;
                caster.SendChat("?combatdepth fixture-reset-thrown-resisted");
                await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
                await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(reset).Any(v => v.Name == CombatFixtureLoadout.DummyName),
                    "A fresh resisted Melee Dummy", HeadlessPlayer.DefaultTimeout);
                dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
                dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            await SneakAsync();
            int logFrom = server.DebugLog().Length;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
            caster.SendRpc(Request(row.Id, dummyHandle));
            await caster.WaitUntilAsync(() => resisted.Matches(server.DebugLog()[logFrom..]).Any(m => m.Groups[1].Value == row.Id.ToString()),
                $"+75 Thrown resistance reduces {row.Id} on the rear dummy", TimeSpan.FromSeconds(8));
            var m = resisted.Matches(server.DebugLog()[logFrom..]).First(x => x.Groups[1].Value == row.Id.ToString());
            Check.Equal(hits[row.Id].ToString(), m.Groups[2].Value, $"{row.Id} resisted raw matches the neutral player hit");
            Check.Equal(TypedAbilityResistance.Amount((ushort)hits[row.Id], Level, Level, 75).ToString(), m.Groups[3].Value, $"{row.Id} resistance formula");
            Console.WriteLine($"READY BOOTSTRAP {row.Id} +75 Thrown resisted on the dummy: raw{m.Groups[2].Value} final{m.Groups[3].Value}");
        }

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("WEAKENING + WARDING PASS: refusals, rear casts, targeted motions, damage, 8 s records and +75 resistance.");
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

    private static string MotionPattern(ushort ability, uint target) =>
        $"01 20 ?? 01 0c 40 {LittleEndianHex(ability)} {WireFormat.ToHex(BitConverter.GetBytes(target))}";
}
