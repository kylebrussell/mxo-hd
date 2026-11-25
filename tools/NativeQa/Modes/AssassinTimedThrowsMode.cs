using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Blinding Throw (282) and Paralyzing Throw (1135): selected-opponent refusals, at-rest landing on a live player,
/// the interlock route that plays the named success moves 291/292, Off Balance's conditional thrown hit and +75 Thrown resistance.
/// Stands reuse the native-verified PR1143 Neuro-Dart geometry.
/// </summary>
public sealed class AssassinTimedThrowsMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle, Blinding = SpyTimedThrows.BlindingThrow, Paralyzing = SpyTimedThrows.ParalyzingThrow;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, FarZ = 8401;
    private const int EnemyX = 16800, FriendX = 17150;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Blinding, Paralyzing);

    public override string Name => "ability-parity-assassin-timed-throws";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=0,alignment=1,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},rotation=192,alignment=1,healthC=5000,healthM=5000 WHERE charId=6111",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={AcrossWallX},y={WallY},z={WallZ},rotation=192,alignment=2,healthC=5000,healthM=5000 WHERE charId=6113",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6113,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6113,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { Spy, Blinding, Paralyzing },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [CombatFixtureWeapons.MartialFixedDamageMode] = "spy-throws",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "282,1135",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR starts at verified outdoor HOME ({X},{Y},{Z}) facing the Melee Dummy 3 m north ({X},{Y},{DummyZ}); QAEnemy 2 m west ({EnemyX},{Y},{Z}), QAFriend 1.5 m east ({FriendX},{Y},{Z}). No initial tp. Page 2: 1=Spy Style (1112), 2=Blinding Throw (282), 3=Paralyzing Throw (1135). Use /target for players; click the dummy (strafe if it is behind the camera).",
            "Step 1 (cast first, at rest, Spy Style on): /target QAEnemy, press slot 3 (1135). Save the first full 1135 80 b9 request. Expect 2.0 s cast, 30 IS, 6 s own / 3 s group 1216 reuse, one thrown hit (value printed below), Rooted, and source1135 stat519 -20 for 8 s. Then press slot 2 (282) on QAEnemy after the group timer: 2.0 s cast, 25 IS, 8 s own / 3 s group 1215, no direct hit, Blind plus ThrownDOT ticks. The DOT tick ends the 1135 root and its 519 record together (OnHealthReduceDeactivateChance -100). Keep QAEnemy HP lines before/after each tick.",
            "Step 2 (refusals, no IS, cast or timer): clear selection and press 2 and 3 -> 24 or a stock local gate. /target QAFriend, press 2 and 3 -> 24. Type the printed WALL chunks, /target QAOther (5 m behind a solid wall), press 2 and 3 -> 9. Type the printed FAR chunks, face south, click the dummy (17.01 m), press 2 and 3 -> 34. Return with the printed HOME chunks.",
            "Step 3 (interlock choreography on the dummy): click the dummy, open Close Combat, reselect Spy Style. Type ?cd ma 1135 (expect 'Martial starter fixture armed 1135.'), press slot 3. Ordinary rounds continue through the 2 s cast, then '[COMBAT] ... is the next move', '[MARTIAL STARTER QA] one exchange win 1135', '[COMBAT-VIEW] ... animation 292 (Paralyzing Throw SR)', the thrown hit, Rooted and 519 on the dummy. After 6 s type ?cd ma 282, press slot 2 -> animation 291 (Blinding Throw SR), Blind and ThrownDOT. Optionally cast once unarmed: a won round plays 291/292, a lost round plays the ordinary exchange with no hit or effect. Leave combat (?cd stop) and wait for the HUD to clear.",
            "Step 4 (Off Balance initializer, 282 only): at rest select the dummy, after 282 reuse type ?cd offbalance 30, press slot 2 -> '[CONDITIONAL-THROW]' one extra thrown hit (value printed below) in addition to the ordinary DOT. Then ?cd daze 30 instead and repeat after reuse -> no extra hit.",
            "Step 5 (+75 Thrown resistance): after effects expire type the printed RESISTED chunks, strafe, click the fresh dummy, press slot 3 -> [TYPED-RESISTANCE OURS] 1135 type7 total75 with the resisted value printed below; then slot 2 after the group timer -> resisted ThrownDOT ticks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Assassin throws require NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var home = Stand(X, Y, Z, indoors: false, facing: 0);
        var far = Stand(X, Y, FarZ, indoors: false, facing: 128);
        var wall = Stand(WallX, WallY, WallZ, indoors: true, facing: 64);
        PrintStand("HOME", home.Command);
        PrintStand("FAR", far.Command);
        PrintStand("WALL", wall.Command);
        Console.WriteLine("READY RESISTED type '?cd fixture-rese', then 't-thrown-resiste', then 'd'; press Enter once.");
        _ = Stand(X, Y, DummyZ, indoors: false, facing: 128);
        _ = Stand(EnemyX, Y, Z, indoors: false, facing: 64);
        _ = Stand(FriendX, Y, Z, indoors: false, facing: 192);
        _ = Stand(AcrossWallX, WallY, WallZ, indoors: true, facing: 192);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME has clear sight to Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "HOME has clear sight to QAEnemy");
        Check.That(NavMesh.HasSightPath(1, X, Y, FarZ, X, Y, DummyZ), "FAR has clear sight beyond the 16 m selector");
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "A solid office wall blocks the throw");
        foreach (ushort id in new[] { Blinding, Paralyzing })
        {
            var source = AbilityCatalog.Find(id);
            Check.Equal(16d, source.Number("Ability/SelectionRange"), $"Shipped {id} range");
            Check.Equal(2d, source.Number("Ability/CastingTime"), $"Shipped {id} cast time");
            Check.Equal(1d, source.Number("Ability/HasSuccessInterlockMove"), $"Shipped {id} success interlock move");
        }
        Check.Equal(291u, CombatAnimations.Special(Blinding, true, CombatTactic.Speed)?.Id, "Blinding Throw SR");
        Check.Equal(292u, CombatAnimations.Special(Paralyzing, true, CombatTactic.Speed)?.Id, "Paralyzing Throw SR");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Loadout fits level-50 memory");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        var helpers = new Dictionary<string, HeadlessPlayer>();
        foreach (string handle in new[] { "QAEnemy", "QAFriend", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            helpers[handle] = helper;
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        var enemy = helpers["QAEnemy"];
        await caster.WaitForViewNamedAsync("QAFriend");

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await StockAbilityProfile.ObserveAsync(caster, 0, frame => StockAbilityProfile.Bonus(frame, Spy, 1034, 29, 0, false),
            "Retained Spy Style activates +29 Thrown influence");

        // Step 1: cast first on the live player at rest.
        int log = server.DebugLog().Length, rpc = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(Paralyzing, await HandleAsync("QAEnemy")));
        var hit = new Regex(@"\[PVP\] CSR_Tester's [^\r\n]* lands on QAEnemy for (\d+)");
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[EFFECTS] 1135 on QAEnemy", StringComparison.Ordinal) &&
            hit.IsMatch(server.DebugLog()[log..]), "Paralyzing Throw lands on QAEnemy at rest", TimeSpan.FromSeconds(8));
        int paralyzingHit = int.Parse(hit.Match(server.DebugLog()[log..]).Groups[1].Value);
        Check.That(server.DebugLog()[log..].Contains("[ABILITY-COST] char=110 ability=1135 paid=30", StringComparison.Ordinal), "1135 costs 30 IS");
        await EvidenceRetestWire.TimerAsync(caster, rpc, Paralyzing, AbilityCooldowns.OwnTimer, 6);
        await EvidenceRetestWire.TimerAsync(caster, rpc, 1216, AbilityCooldowns.GroupTimer, 3);
        Check.That(!caster.World.RpcSnapshot().Skip(rpc).Any(r => r.Bytes is [0x46, ..] && r.Bytes.Length == 6 && r.Bytes[5] == 1),
            "The at-rest cast does not enter interlock");
        Console.WriteLine($"READY BOOTSTRAP 1135 at rest on QAEnemy: {paralyzingHit} thrown");

        await Task.Delay(TimeSpan.FromSeconds(3.25));
        log = server.DebugLog().Length; rpc = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(Blinding, await HandleAsync("QAEnemy")));
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[EFFECTS] 282 on QAEnemy", StringComparison.Ordinal),
            "Blinding Throw lands Blind and its DOT on QAEnemy", TimeSpan.FromSeconds(8));
        Check.That(server.DebugLog()[log..].Contains("[ABILITY-COST] char=110 ability=282 paid=25", StringComparison.Ordinal), "282 costs 25 IS");
        await EvidenceRetestWire.TimerAsync(caster, rpc, Blinding, AbilityCooldowns.OwnTimer, 8);
        await EvidenceRetestWire.TimerAsync(caster, rpc, 1215, AbilityCooldowns.GroupTimer, 3);

        // Step 2: refusals before cost, cast or reuse.
        await Task.Delay(TimeSpan.FromSeconds(8.25));
        foreach (ushort id in new[] { Blinding, Paralyzing })
        {
            await RefusedAsync(caster, id, Use(id, 0));
            await RefusedAsync(caster, id, Use(id, await HandleAsync("QAFriend")));
        }
        await MoveAsync(wall);
        uint other = await HandleAsync("QAOther");
        foreach (ushort id in new[] { Blinding, Paralyzing })
            await RefusedAsync(caster, id, Use(id, other), AbilityRefusals.TargetNotInSight);
        int created = caster.World.CreationSnapshot().Count;
        await MoveAsync(far);
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(created).Any(v => v.Name == CombatFixtureLoadout.DummyName),
            "A fresh Melee Dummy view at FAR", HeadlessPlayer.DefaultTimeout);
        dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        foreach (ushort id in new[] { Blinding, Paralyzing })
            await RefusedAsync(caster, id, Use(id, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(home);

        // Step 3: the completed cast becomes the next interlock move and plays its named success move.
        dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        await StarterInterlockDamageMode.Open(caster, dummy);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
        foreach (var (id, move, name) in new[] { (Paralyzing, 292, "Paralyzing Throw SR"), (Blinding, 291, "Blinding Throw SR") })
        {
            caster.SendChat($"?cd ma {id}");
            await caster.WaitForTextAsync($"Martial starter fixture armed {id}.");
            log = server.DebugLog().Length;
            caster.SendRpc(Use(id, dummyHandle));
            await caster.WaitUntilAsync(() =>
                server.DebugLog()[log..].Contains($"[MARTIAL STARTER QA] one exchange win {id}", StringComparison.Ordinal) &&
                server.DebugLog()[log..].Contains($"animation {move} ({name})", StringComparison.Ordinal) &&
                server.DebugLog()[log..].Contains($"[ABILITY] {id} lands on view {dummy.ViewId}", StringComparison.Ordinal),
                $"Interlocked {id} queues after its cast and lands with {move}", TimeSpan.FromSeconds(15));
            Check.That(server.DebugLog()[log..].Contains("is the next move", StringComparison.Ordinal), $"{id} becomes the next move");
            await Task.Delay(TimeSpan.FromSeconds(3.25));
        }
        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended.");

        // Step 4: the stored Off Balance initializer adds one separate thrown hit to an at-rest 282.
        await Task.Delay(TimeSpan.FromSeconds(8.25));
        dummy = await ResetAsync("?combatdepth fixture-reset", "Fixture reset: Melee Dummy restored.");
        dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummyHandle))}");
        caster.SendChat("?cd offbalance 30");
        await caster.WaitForTextAsync("OffBalance fixture applied 885/200 to Melee Dummy for 30 seconds");
        log = server.DebugLog().Length;
        caster.SendRpc(Use(Blinding, dummyHandle));
        var conditional = new Regex(@"\[CONDITIONAL-THROW\] 282 on Melee Dummy value(\d+) raw(\d+) final(\d+) hp(\d+)->(\d+)");
        await caster.WaitUntilAsync(() => conditional.IsMatch(server.DebugLog()[log..]), "Off Balance adds the 282 initializer hit", TimeSpan.FromSeconds(8));
        var extra = conditional.Match(server.DebugLog()[log..]);
        Console.WriteLine($"READY BOOTSTRAP 282 Off Balance initializer on the dummy: value{extra.Groups[1].Value} final{extra.Groups[3].Value} (hp {extra.Groups[4].Value}->{extra.Groups[5].Value})");

        // Step 5: +75 Thrown resistance on the same at-rest 1135 hit.
        await Task.Delay(TimeSpan.FromSeconds(3.25));
        dummy = await ResetAsync("?combatdepth fixture-reset-thrown-resisted", "Fixture reset: Melee Dummy restored");
        dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        log = server.DebugLog().Length;
        caster.SendRpc(Use(Paralyzing, dummyHandle));
        var resisted = new Regex(@"\[TYPED-RESISTANCE OURS\] 1135 type7 total75 raw(\d+) final(\d+)");
        await caster.WaitUntilAsync(() => resisted.IsMatch(server.DebugLog()[log..]), "+75 Thrown resistance reduces 1135", TimeSpan.FromSeconds(8));
        var r = resisted.Match(server.DebugLog()[log..]);
        Check.Equal(paralyzingHit.ToString(), r.Groups[1].Value, "The resisted raw value matches the neutral player hit");
        Check.Equal(TypedAbilityResistance.Amount((ushort)paralyzingHit, Level, Level, 75).ToString(), r.Groups[2].Value, "Resistance formula");
        Console.WriteLine($"READY BOOTSTRAP 1135 +75 Thrown resisted on the dummy: raw{r.Groups[1].Value} final{r.Groups[2].Value}");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean parser state");
        Console.WriteLine("ASSASSIN TIMED THROWS BOOTSTRAP PASS: rest landing, refusals, interlock 291/292 and timers.");
        return Array.Empty<byte>();

        async Task<uint> HandleAsync(string name)
        {
            var view = await caster.WaitForViewNamedAsync(name);
            return DuelPackets.Handle(view.ViewId, view.SpawnId);
        }

        async Task<ViewCreation> ResetAsync(string command, string reply)
        {
            int at = caster.World.CreationSnapshot().Count;
            caster.SendChat(command);
            await caster.WaitForTextAsync(reply);
            await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(at).Any(v => v.Name == CombatFixtureLoadout.DummyName),
                "A fresh Melee Dummy is visible after reset", HeadlessPlayer.DefaultTimeout);
            return caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        }

        async Task MoveAsync(Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }
    }

    private static byte[] Use(ushort ability, uint handle) =>
        new AbilityUseRecord(ability, handle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();

    private sealed record Teleport(string Command, string Confirmation);

    private static Teleport Stand(int x, int y, int z, bool indoors, byte facing)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == indoors && stand.floorY + 95 == y,
            $"Verified Assassin throw stand ({x},{y},{z})");
        return new($"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}",
            $"Teleported to {stand.sector:x}/{stand.region:x}");
    }

    private static void PrintStand(string label, string command)
    {
        string[] words = command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
    }
}
