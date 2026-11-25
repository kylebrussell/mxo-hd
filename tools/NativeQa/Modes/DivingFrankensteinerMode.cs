using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Diving Frankensteiner (385, Master Shadow) on the shared instant rear-opener gate (outside combat, Spy Style,
/// Stealthed, live hostile, 6 m, sight, outside the target's 50-degree cone), its stealth-started thrown hit, and
/// +75 Thrown resistance on the PR1165 post-reset rear of the Melee Dummy. Player stands reuse the PR1155 geometry.
/// </summary>
public sealed class DivingFrankensteinerMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, DummyZ = 6700, RearZ = 6448, ResetHeading = 0;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private static readonly (ushort Id, int Cost, float Own, ushort State)[] Rows =
    {
        (SpyMasterSneakOpeners.DivingFrankensteiner, 80, 60, 0)
    };
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, 385);

    public override string Name => "ability-parity-diving-frankensteiner";

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
    }, DevCommands: true, hotbarAbilities: new ushort[] { Spy, Sneak, 385 },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "385",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor HOME ({X},{Y},{Z}) is 2 m behind QAEnemy ({EnemyX},{Y},{Z}, facing east); QAFriend observes. No initial tp. Page 2: 1=Spy Style, 2=Sneak, 3=Diving Frankensteiner (385).",
            "Step 1 (cast first): slot 1, Sneak (slot 2), /target QAEnemy, press 3. Save the first full request. Expect Sneak off, 80 IS, the stealth-started thrown hit printed below, 60 s own / 15 s family 691 reuse, no interlock. Watch which move the client plays (ILDB name 19592 Diving Frankensteiner).",
            "Step 2 (refusals, no IS or timer; local gates are fine): no Spy Style -> 13; no Sneak -> 23; Sneak, /target QAFriend -> 24; printed FRONT (inside QAEnemy's 50-degree cone) -> 24; BLOCK -> 9; FAR -> 34.",
            "Step 3 (+75 Thrown resistance, after the 60 s reuse): type the RESISTED chunks. The reset turns the fresh dummy north and teleports you 2.52 m behind it, facing it (the printed DUMMY stand); the dummy holds that heading, so do not move. Click the fresh dummy, Sneak, press 3 -> [TYPED-RESISTANCE OURS] 385 type7 total75 with the values printed below. If Close Combat opens, ?cd stop drops Spy Style: press slot 1 again."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));

        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        var front = StarterConcealedShotTargetingMode.Stand(21850, 16200);
        var block = StarterConcealedShotTargetingMode.Stand(21950, Z);
        var far = StarterConcealedShotTargetingMode.FindFarStand();
        // A resisted reset turns the dummy to heading 0 (north) and teleports the caster here, 2.52 m behind it.
        var rear = DummyStand(DummyX, RearZ, facing: 0);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear two-metre rear approach");
        Check.That(NavMesh.HasSightPath(1, 21850, Y, 16200, EnemyX, Y, Z) &&
            Stealth.InFrustum(EnemyX, Z, 64, 21850, 16200, 50), "Clear frontal stand inside QAEnemy's 50-deg cone");
        Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall blocks sight");
        Check.That(NavMesh.HasSightPath(1, DummyX, DummyY, RearZ, DummyX, DummyY, DummyZ) &&
            !Stealth.InFrustum(DummyX, DummyZ, ResetHeading, DummyX, RearZ, 50), "Clear rear stand outside the dummy's cone");
        foreach (var row in Rows)
        {
            var source = AbilityCatalog.Find(row.Id);
            Check.Equal(50d, source.Number("Ability/CastOutsideTargetFOVAngle"), $"Shipped {row.Id} rear angle");
            Check.Equal(6d, source.Number("Ability/SelectionRange"), $"Shipped {row.Id} range");
            Check.That(SpyInstantSneakOpenerAdmission.Handles(row.Id), $"{row.Id} uses the instant rear-opener gate");
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
        var usedAt = new Dictionary<ushort, DateTime>();
        var landed = new Regex(@"\[PVP\] CSR_Tester's [^\r\n]* lands on QAEnemy for (\d+)");
        bool first = true;
        foreach (var row in Rows)
        {
            // Sneak from the refusal phase is still active for the first opener; each landing then consumes it.
            if (!first)
            {
                await Task.Delay(TimeSpan.FromSeconds(15.25));
                await SneakAsync();
            }
            first = false;
            int casterRpc = caster.World.RpcSnapshot().Count, enemyRpc = enemy.World.RpcSnapshot().Count, logFrom = server.DebugLog().Length;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
            caster.SendRpc(Request(row.Id, target));
            usedAt[row.Id] = DateTime.UtcNow;
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
        await Task.Delay(TimeSpan.FromSeconds(3));
        int created = caster.World.CreationSnapshot().Count;
        caster.SendChat("?combatdepth fixture-reset-thrown-resisted");
        await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(created).Any(v => v.Name == CombatFixtureLoadout.DummyName),
            "A fresh resisted Melee Dummy", HeadlessPlayer.DefaultTimeout);
        var dummy = caster.World.CreationSnapshot().Skip(created).Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        var spin = Rows[0];
        // Wait out the opener's own 60 s reuse before the resisted cast.
        var ready = usedAt[spin.Id] + TimeSpan.FromSeconds(spin.Own + 1.5);
        if (ready > DateTime.UtcNow) await Task.Delay(ready - DateTime.UtcNow);
        await SneakAsync();
        int from = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
        caster.SendRpc(Request(spin.Id, dummyHandle));
        var resisted = new Regex($@"\[TYPED-RESISTANCE OURS\] {spin.Id} type7 total75 raw(\d+) final(\d+)");
        await caster.WaitUntilAsync(() => resisted.IsMatch(server.DebugLog()[from..]), "+75 Thrown resistance reduces the rear opener", TimeSpan.FromSeconds(8));
        var m = resisted.Match(server.DebugLog()[from..]);
        Check.Equal(hits[spin.Id].ToString(), m.Groups[1].Value, "Resisted raw matches the neutral stealth opener");
        Check.Equal(TypedAbilityResistance.Amount((ushort)hits[spin.Id], Level, Level, 75).ToString(), m.Groups[2].Value, "Resistance formula");
        Console.WriteLine($"READY BOOTSTRAP {spin.Id} +75 Thrown resisted on the dummy: raw{m.Groups[1].Value} final{m.Groups[2].Value}");


        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("DIVING FRANKENSTEINER PASS: rear-opener refusals, stealth hit, timers and +75 resistance.");
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
