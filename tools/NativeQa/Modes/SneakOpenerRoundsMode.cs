using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// The instant sneak openers' at-rest presentation (SneakOpenerRound): each of 286/288/289/385/387/388 cast once from
/// Sneak behind QAEnemy shows one Close Combat exchange naming its ILDB "Sneak Attacks" move, with damage, state and cost
/// unchanged and no fight entered; then one QA-forced "Failed Sneak v &lt;style&gt;" presentation.
/// </summary>
public sealed class SneakOpenerRoundsMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, DummyZ = 6700, RearZ = 6448, ResetHeading = 0;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility;
    private static readonly (ushort Id, int Cost, float Own, ushort State)[] Rows =
    {
        (SpySneakOpeners.SpinThrow, 50, 38, 0),
        (SpySneakOpeners.TopsyTurvy, 60, 38, TempMods.ConfuseAbility),
        (SpySneakOpeners.Punt, 40, 38, 0),
        (SpyMasterSneakOpeners.DivingFrankensteiner, 80, 60, 0),
        (BullDogDamage.Id, 37, 60, 0),
        (SpyShadowAssassinBatch.ReverseBullDog, 60, 60, TempMods.StunAbility)
    };
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, 286, 288, 289, 385, 387, 388);

    public override string Name => "ability-parity-sneak-opener-rounds";

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
    }, DevCommands: true, hotbarAbilities: new ushort[] { Spy, Sneak, 286, 288, 289, 385, 387, 388 },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [SneakOpenerRound.FailMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "286,288,289,385,387,388",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}. HOME ({X},{Y},{Z}) is 2 m behind QAEnemy ({EnemyX},{Y},{Z}, facing east); no initial tp. Page 2: 1=Spy Style, 2=Sneak, 3=Spin Throw 286, 4=Topsy Turvy 288, 5=Punt 289, 6=Diving Frankensteiner 385, 7=Bull Dog 387, 8=Reverse Bull Dog 388.",
            "Step 1 (one opener per 15 s family 691 window; Sneak before each; stay at HOME behind QAEnemy): /target QAEnemy, press 3..8 in turn. Each landing should show one Close Combat exchange with the named full-body move: 286 Twist Throw 19593, 288 Topsy Turvy 19594, 289 Punt 19590, 385 Diving Frankensteiner 19592, 387 Running Bulldog 19591, 388 Reverse Pulldown 19595 (server log '[SNEAK-OPENER-ROUND] <id> landed move <move>'). Damage, IS and effects are as before (bootstrap prints them); no Close Combat should start. Record, per id, whether the named move played and anything odd (pose snap, combat HUD, the client re-requesting Close Combat).",
            "Step 2 (forced-fail presentation): type ?openerfail on, wait out 286's 38 s reuse, Sneak, press 3. Expect the 'Failed Sneak v Self-Defense' exchange (19603) instead of Twist Throw; this is presentation only, the hit still lands. Type ?openerfail off."
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
            Check.That(SneakFreeThrowAdmission.GatesInstantOpener(row.Id) || SpyInstantSneakOpenerAdmission.Handles(row.Id), $"{row.Id} uses the rear-opener gate");
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
            // Sneak from the refusal phase is still active for the first opener; each landing then consumes it.
            if (!first)
            {
                await Task.Delay(TimeSpan.FromSeconds(15.25));
                await SneakAsync();
            }
            first = false;
            int casterRpc = caster.World.RpcSnapshot().Count, enemyRpc = enemy.World.RpcSnapshot().Count, logFrom = server.DebugLog().Length;
            int viewsBefore = caster.World.ViewSnapshot().Count;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
            caster.SendRpc(Request(row.Id, target));
            await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[logFrom..]), $"{row.Id} lands on QAEnemy", TimeSpan.FromSeconds(8));
            var round = CombatAnimations.Special(row.Id, true, CombatTactic.Speed)!;
            await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains($"[SNEAK-OPENER-ROUND] {row.Id} landed move {round.Id} ({round.Name})", StringComparison.Ordinal) &&
                server.DebugLog()[logFrom..].Contains($"animation {round.Id} ({round.Name})", StringComparison.Ordinal),
                $"{row.Id} presents its {round.Name} exchange", TimeSpan.FromSeconds(8));
            // After the exchange the caster's view of QAEnemy gets QAEnemy's real server yaw (64, east) and position again.
            byte[] facing = PvpFight.BuildFacing(64, EnemyX, Y, Z);
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(viewsBefore).Any(v => v.AsSpan().IndexOf(facing) >= 0),
                $"{row.Id} restores QAEnemy's facing after the exchange", TimeSpan.FromSeconds(round.DurationMs / 1000.0 + 4));
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

        // Forced-fail presentation: the QA toggle swaps the exchange to Failed Sneak; the hit still lands.
        caster.SendChat("?openerfail on");
        await caster.WaitForTextAsync("Opener fail presentation on.");
        await Task.Delay(TimeSpan.FromSeconds(38 + 1.5));
        await SneakAsync();
        int failFrom = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
        caster.SendRpc(Request(SpySneakOpeners.SpinThrow, target));
        await caster.WaitUntilAsync(() => server.DebugLog()[failFrom..].Contains("[SNEAK-OPENER-ROUND] 286 failed (QA) move 19603 (Failed Sneak v Self-Defense)", StringComparison.Ordinal),
            "Forced Failed Sneak exchange", TimeSpan.FromSeconds(8));
        caster.SendChat("?openerfail off");
        await caster.WaitForTextAsync("Opener fail presentation off.");
        Console.WriteLine("READY BOOTSTRAP forced fail: 286 presents Failed Sneak v Self-Defense 19603");

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("SNEAK OPENER ROUNDS PASS: each opener lands with its named exchange; the forced fail presents Failed Sneak.");
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
