using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Spin Throw (286), Topsy Turvy (288), Punt (289) and Reverse Bull Dog (388): the shared rear-opener gate
/// (Spy Style, Stealthed, live hostile, 6 m, sight, outside the target's 50-degree cone, outside combat), the
/// stealth-started thrown hit, Confuse/Stun records and +75 Thrown resistance on the rear of the Melee Dummy.
/// Player stands reuse the PR1121/PR1145 geometry.
/// </summary>
public sealed class AssassinSneakAttacksMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, DummyZ = 6700, RearZ = 6900;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private static readonly (ushort Id, int Cost, float Own, ushort State)[] Rows =
    {
        (SpySneakOpeners.SpinThrow, 50, 38, 0),
        (SpySneakOpeners.TopsyTurvy, 60, 38, TempMods.ConfuseAbility),
        (SpySneakOpeners.Punt, 40, 38, 0),
        (SpyShadowAssassinBatch.ReverseBullDog, 60, 60, TempMods.StunAbility)
    };
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, 286, 288, 289, 388);

    public override string Name => "ability-parity-assassin-sneak-attacks";

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
    }, DevCommands: true, hotbarAbilities: new ushort[] { Spy, Sneak, 286, 288, 289, 388 },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "286,288,289,388",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor HOME ({X},{Y},{Z}) is 2 m behind QAEnemy ({EnemyX},{Y},{Z}, facing east); QAFriend observes. No initial tp. Page 2: 1=Spy Style, 2=Sneak, 3=Spin Throw (286), 4=Topsy Turvy (288), 5=Punt (289), 6=Reverse Bull Dog (388).",
            "Step 1 (cast first, one per 15 s family 691 window, Sneak before each): /target QAEnemy and press 3, 4, 5, 6 in turn. Save the first full request of each. Expect Sneak off, the IS cost, the stealth-started thrown hit printed below, own reuse 38/38/38/60 s, 288 Confuse and 388 Stun plus its 16 s 681 timer on QAEnemy, no interlock. Keep QAEnemy HP before/after and watch which move the client plays (shipped ILDB names 19593 Twist Throw, 19594 Topsy Turvy, 19590 Punt, 19595 Reverse Pulldown).",
            "Step 2 (refusals, no IS, cast or timer; wait out reuse or expect a local gate): without Sneak -> 23; Sneak, /target QAFriend -> 24; printed FRONT (inside QAEnemy's 50-degree cone) -> 24; BLOCK (solid wall) -> 9; FAR -> 34; in Close Combat -> 21.",
            "Step 3 (+75 Thrown resistance): type the printed DUMMY chunks (2 m behind the south-facing Melee Dummy) and the RESISTED chunks, strafe, click the fresh dummy, Sneak, press 3 after its reuse -> [TYPED-RESISTANCE OURS] 286 type7 total75 with the values printed below. Type ?cd stop after each dummy opener before the next."
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
        Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall blocks sight");
        Check.That(NavMesh.HasSightPath(1, DummyX, DummyY, RearZ, DummyX, DummyY, DummyZ) &&
            !Stealth.InFrustum(DummyX, DummyZ, 128, DummyX, RearZ, 50), "Clear rear stand outside the dummy's cone");
        foreach (var row in Rows)
        {
            var source = AbilityCatalog.Find(row.Id);
            Check.Equal(50d, source.Number("Ability/CastOutsideTargetFOVAngle"), $"Shipped {row.Id} rear angle");
            Check.Equal(6d, source.Number("Ability/SelectionRange"), $"Shipped {row.Id} range");
            Check.That(SneakFreeThrowAdmission.GatesInstantOpener(row.Id), $"{row.Id} uses the rear-opener gate");
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

        var hits = new Dictionary<ushort, int>();
        var landed = new Regex(@"\[PVP\] CSR_Tester's [^\r\n]* lands on QAEnemy for (\d+)");
        bool first = true;
        foreach (var row in Rows)
        {
            if (!first) await Task.Delay(TimeSpan.FromSeconds(15.25));
            first = false;
            await SneakAsync();
            int casterRpc = caster.World.RpcSnapshot().Count, enemyRpc = enemy.World.RpcSnapshot().Count, logFrom = server.DebugLog().Length;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
            caster.SendRpc(Request(row.Id, target));
            await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[logFrom..]), $"{row.Id} lands on QAEnemy", TimeSpan.FromSeconds(8));
            int hit = int.Parse(landed.Match(server.DebugLog()[logFrom..]).Groups[1].Value);
            hits[row.Id] = hit;
            await ObserveAsync(enemy, enemyRpc,
                p => p.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, row.Id, caster.Handle, enemy.Handle, (ushort)hit, 0)),
                $"{row.Id} thrown hit reaches QAEnemy");
            await EvidenceRetestWire.TimerAsync(caster, casterRpc, row.Id, AbilityCooldowns.OwnTimer, row.Own);
            await EvidenceRetestWire.TimerAsync(caster, casterRpc, 691, AbilityCooldowns.GroupTimer, 15);
            await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal),
                $"{row.Id} consumes Sneak", HeadlessPlayer.DefaultTimeout);
            Check.That(server.DebugLog()[logFrom..].Contains($"[ABILITY-COST] char=110 ability={row.Id} paid={row.Cost}", StringComparison.Ordinal),
                $"{row.Id} pays {row.Cost} IS");
            string control = "";
            if (row.State != 0)
            {
                var added = await ObserveAsync(enemy, enemyRpc, f => ManageBonusRecord.TryDecode(f.Bytes, out var r) && r!.Type == TempMods.KindState &&
                    r.SourceId == row.Id && r.TargetId == row.State && r.AddRemoveType == 0, $"{row.Id} applies state {row.State}");
                ManageBonusRecord.TryDecode(added.Bytes, out var record);
                control = FormattableString.Invariant($"; state {row.State} {record!.Duration:0.###} s");
            }
            Check.That(!caster.World.RpcSnapshot().Skip(casterRpc).Any(r => r.Bytes is [0x46, ..] && r.Bytes.Length == 6 && r.Bytes[5] == 1),
                $"{row.Id} does not enter interlock");
            Console.WriteLine($"READY BOOTSTRAP {row.Id} stealth opener on QAEnemy: thrown {hit}{control}");
        }

        await MoveAsync(rear.Command, rear.Confirmation);
        int created = caster.World.CreationSnapshot().Count;
        caster.SendChat("?combatdepth fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(created).Any(v => v.Name == CombatFixtureLoadout.DummyName),
            "A fresh resisted Melee Dummy", HeadlessPlayer.DefaultTimeout);
        var dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        await Task.Delay(TimeSpan.FromSeconds(16));
        var spin = Rows[0];
        await SneakAsync();
        int from = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
        caster.SendRpc(Request(spin.Id, dummyHandle));
        var resisted = new Regex($@"\[TYPED-RESISTANCE OURS\] {spin.Id} type7 total75 raw(\d+) final(\d+)");
        await caster.WaitUntilAsync(() => resisted.IsMatch(server.DebugLog()[from..]), "+75 Thrown resistance reduces the rear opener", TimeSpan.FromSeconds(8));
        var m = resisted.Match(server.DebugLog()[from..]);
        Check.Equal(hits[spin.Id].ToString(), m.Groups[1].Value, "Resisted raw matches the neutral stealth opener");
        Check.Equal(TypedAbilityResistance.Amount((ushort)hits[spin.Id], Level, Level, 75).ToString(), m.Groups[2].Value, "Resistance formula");
        Console.WriteLine($"READY BOOTSTRAP 286 +75 Thrown resisted on the dummy: raw{m.Groups[1].Value} final{m.Groups[2].Value}");

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("ASSASSIN SNEAK ATTACKS PASS: rear-opener refusals, stealth hits, controls, timers and +75 resistance.");
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
