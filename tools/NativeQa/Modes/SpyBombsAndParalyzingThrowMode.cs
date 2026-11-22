using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SpyBombsAndParalyzingThrowMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                  // 1112
    private const ushort FlashBomb = SpyGroundBombs.FlashBomb;              // 107
    private const ushort SmokeBomb = SpyGroundBombs.SmokeBomb;              // 364
    private const ushort DazzlingThrow = 1132;
    private const ushort ParalyzingThrow = ParalyzingThrowDamage.Id;        // 1135
    private const uint InvisibilityBeltGoid = SpyGroundBombs.InvisibilityBeltGoid; // 460
    private const byte BeltSlot = 40;

    private const byte Level = 50, Rank = 50;
    private const int GroundY = 495;
    private const int CasterX = 17000, CasterZ = 2300, DummyX = 17200;
    private const int FarZ = CasterZ - 2500;

    private static readonly ushort[] Roots =
    {
        2, 17, SpyStyle, FlashBomb, SmokeBomb, DazzlingThrow, ParalyzingThrow
    };

    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-spy-bombs-and-paralyzing-throw";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CasterX, CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={GroundY},rotation=64," +
                "healthC=3000,healthM=3000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot IN ({CombatFixtureWeapons.HandSlot},{BeltSlot})",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{InvisibilityBeltGoid},{BeltSlot},1,0)"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        testMobFixture: "spy-bombs-and-throw.csv",
        hotbarAbilities: new[] { SpyStyle, FlashBomb, SmokeBomb, DazzlingThrow, ParalyzingThrow },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (80/100 memory, max IS 2000), Invisibility Belt (GOID 460) in bag slot 40, CSR on verified outdoor NAV (17000,495,2300), facing Melee Dummy (17200,495,2300) from 2 m behind. Page 2 hotbar slots: 1=1112 Spy Style, 2=107 Flash Bomb, 3=364 Smoke Bomb, 4=1132 Dazzling Throw, 5=1135 Paralyzing Throw.",
            "Step A (107 Flash Bomb - ground-target AOE 5 m): Select Spy Style (slot 1). Press slot 2 (107 Flash Bomb) and click the ground under/near Melee Dummy: expect 25 IS spent, 2.0 s cast bar, 58.0 s own / 3.0 s family 1216 reuse, '[BOMB] 107 ... mobs=1', 0 damage, and '[EFFECTS] 107 on Melee Dummy: State 90 0 12 s' (Blind 12 s, plus 15% each Dazed/Off-Balance/Staggered 12 s).",
            "Step B (364 Smoke Bomb - ground-target AOE 3 m + SmokeBombInit vanish): Use ?combatdepth fixture-reset, wait, strafe/turn to reveal fresh Melee Dummy view, reselect Spy Style (slot 1). Enter submits chat; Escape can holster the active style. Press slot 3 (364 Smoke Bomb) and click the ground at Melee Dummy: expect 120 IS spent, 1.5 s cast bar, 60.0 s own reuse, '[BOMB] 364 ... mobs=1', 0 damage, '[EFFECTS] 364 on Melee Dummy: State 90 0 20 s' (Blind 20 s), and 0.5 s later '[STEALTH] CSR_Tester 1116 on (bits 4, SL 0, 130 s)' + '[SPECIAL] Smoke Bomb: char 110 vanishes into invisibility'. Capture the visible stealth pose and '[EMOTE] CSR_Tester stance 1a'.",
            "Step C (1135 Paralyzing Throw - standalone shipped 30 base + conditional stun via 1132 Dazzling Throw): Use ?combatdepth fixture-reset, wait, strafe/turn to reveal fresh Melee Dummy view, click Melee Dummy, reselect Spy Style (slot 1). Press slot 5 (1135 Paralyzing Throw): expect 30 IS spent, 2.0 s cast bar, 6.0 s own / 3.0 s family 1216 reuse, thrown damage 46 (shipped base 30 + 54% Lv50 closure), and '[EFFECTS] 1135 on Melee Dummy:' with State 99 (Rooted 8.8 s (base 8 s plus 10% Root Duration Bonus)) and Stat 519 -20 (Force Combat 8 s). Wait until 1135 reuse and the first root have both cleared. With the dummy selected, type ?cd daze 30 and press Enter. Confirm source 884 applied Daze to Melee Dummy, then press slot 5 (1135 Paralyzing Throw) again: expect thrown damage 46 plus conditional Stun 8 s (State 102) and 8 s stun immunity alongside Rooted and Force Combat."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "107,364,1135",
            ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Expected repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { CasterX, DummyX })
            Check.That(NavMesh.Locate(1, x, GroundY, CasterZ, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - GroundY) < 1,
                $"Spy bomb stand ({x},{GroundY},{CasterZ}) is on outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, CasterX, GroundY, CasterZ, DummyX, GroundY, CasterZ), "The dummy has a clear sight line");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);

        // Rest refusals: 1135 requires live enemy; 107/364 refuse out-of-range ground targets (> 16 m).
        await AssertDeniedAsync(new AbilityUseRecord(ParalyzingThrow, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode(),
            "[ABILITY] Denied 1135: native refusal 24", "Paralyzing Throw refuses empty target");
        await AssertDeniedAsync(UseGround(FlashBomb, CasterX, GroundY, FarZ),
            $"[ABILITY] Denied 107: native refusal {AbilityRefusals.TargetOutOfRange}", "Flash Bomb refuses ground point beyond 16 m");

        // Step A: 107 Flash Bomb centered on Melee Dummy.
        int log107 = server.DebugLog().Length;
        int rpc107 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(UseGround(FlashBomb, DummyX, GroundY, CasterZ));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log107..].Contains("[BOMB] 107", StringComparison.Ordinal) &&
            server.DebugLog()[log107..].Contains("[EFFECTS] 107 on Melee Dummy:", StringComparison.Ordinal) &&
            server.DebugLog()[log107..].Contains("State 90 0 12 s", StringComparison.Ordinal),
            "Flash Bomb lands on Melee Dummy with 12 s Blind", TimeSpan.FromSeconds(8));
        await caster.WaitUntilAsync(() => server.DebugLog()[log107..].Contains(
            $"[ABILITY] 107 lands on view {dummy.ViewId} for 0", StringComparison.Ordinal),
            "Flash Bomb deals 0 direct damage", HeadlessPlayer.DefaultTimeout);
        CheckTimer(rpc107, FlashBomb, AbilityCooldowns.OwnTimer, 58, "Flash Bomb 58 s own reuse");
        CheckTimer(rpc107, 1216, AbilityCooldowns.GroupTimer, 3, "Flash Bomb 3 s family 1216 reuse");

        // Step B: Reset -> 364 Smoke Bomb centered on Melee Dummy -> Blind 20 s + SmokeBombInit vanish (1116, 130 s).
        dummy = await ResetAsync();
        int log364 = server.DebugLog().Length;
        int rpc364 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(UseGround(SmokeBomb, DummyX, GroundY, CasterZ));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log364..].Contains("[BOMB] 364", StringComparison.Ordinal) &&
            server.DebugLog()[log364..].Contains("State 90 0 20 s", StringComparison.Ordinal) &&
            server.DebugLog()[log364..].Contains("[STEALTH] CSR_Tester 1116 on (bits 4, SL 0, 130 s)", StringComparison.Ordinal) &&
            server.DebugLog()[log364..].Contains("[SPECIAL] Smoke Bomb: char 110 vanishes into invisibility", StringComparison.Ordinal),
            "Smoke Bomb blinds Melee Dummy for 20 s and vanishes CSR into 1116 Invisibility (130 s)", TimeSpan.FromSeconds(8));
        CheckTimer(rpc364, SmokeBomb, AbilityCooldowns.OwnTimer, 60, "Smoke Bomb 60 s own reuse");
        Check.That(server.DebugLog()[log364..].Contains("[EMOTE] CSR_Tester stance 1a", StringComparison.Ordinal),
            "Smoke Bomb enters the shared Invisibility movement stance");
        await StockAbilityProfile.ObserveAsync(caster, rpc364, frame => StockAbilityProfile.Bonus(frame, 1116, 115, -300, 130, false),
            "Smoke Bomb publishes the shared Invisibility movement modifier");

        // Let the Flash Bomb family timer expire before replaying the selected-target throw.
        dummy = await ResetAsync();
        await Task.Delay(TimeSpan.FromSeconds(3.25));
        int log1135 = server.DebugLog().Length;
        int rpc1135 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(UseTarget(ParalyzingThrow, dummy));
        var land1135 = new Regex($@"\[ABILITY\] 1135 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => land1135.IsMatch(server.DebugLog()[log1135..]),
            "Paralyzing Throw lands on Melee Dummy", TimeSpan.FromSeconds(8));
        Check.Equal(46, int.Parse(land1135.Match(server.DebugLog()[log1135..]).Groups[1].Value),
            "Paralyzing Throw lands for 46 thrown damage (base 30 + 54% Lv50 closure)");
        Check.That(server.DebugLog()[log1135..].Contains("State 99 0 8.8 s, Terminal 1135 8 0.01 s, Stat 519 -20 8 s", StringComparison.Ordinal),
            "Paralyzing Throw applies 8.8 s Rooted and 8 s Force Combat" + Environment.NewLine + server.DebugLog()[log1135..]);
        CheckTimer(rpc1135, ParalyzingThrow, AbilityCooldowns.OwnTimer, 6, "Paralyzing Throw 6 s own reuse");
        CheckTimer(rpc1135, 1216, AbilityCooldowns.GroupTimer, 3, "Paralyzing Throw 3 s family 1216 reuse");

        await Task.Delay(TimeSpan.FromSeconds(9));
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)))}");
        caster.SendChat("?cd daze 30");
        await caster.WaitForTextAsync("Daze fixture applied 884/199 to Melee Dummy for 30 seconds");
        int log1135Stun = server.DebugLog().Length;
        caster.SendRpc(UseTarget(ParalyzingThrow, dummy));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log1135Stun..].Contains("[ABILITY] 1135 lands on view", StringComparison.Ordinal) &&
            server.DebugLog()[log1135Stun..].Contains("State 102 0 8 s", StringComparison.Ordinal),
            "Paralyzing Throw on Dazed target triggers 8 s Stun", TimeSpan.FromSeconds(8));

        Console.WriteLine("  Flash Bomb (107), Smoke Bomb (364 + SmokeBombInit vanish), and Paralyzing Throw (1135 shipped 30 + conditional stun) verified");
        return Array.Empty<byte>();

        static byte[] UseTarget(ushort ability, ViewCreation target) =>
            new AbilityUseRecord(ability, DuelPackets.Handle(target.ViewId, target.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();

        static byte[] UseGround(ushort ability, double x, double y, double z) =>
            new AbilityUseRecord(ability, 0, 0,
                (ulong)BitConverter.DoubleToInt64Bits(x),
                (ulong)BitConverter.DoubleToInt64Bits(y),
                (ulong)BitConverter.DoubleToInt64Bits(z),
                0x6b, 0x6b, 0, 0).Encode();

        void CheckTimer(int fromRpc, ushort id, byte kind, float seconds, string message)
        {
            byte[] expected = AbilityCooldowns.BuildTimer(id, kind, seconds, true);
            Check.That(caster.World.RpcSnapshot().Skip(fromRpc).Any(r => r.Bytes.AsSpan().SequenceEqual(expected)), message);
        }

        async Task AssertDeniedAsync(byte[] rpc, string expectedLog, string message)
        {
            int from = server.DebugLog().Length;
            caster.SendRpc(rpc);
            await caster.WaitUntilAsync(() => server.DebugLog()[from..].Contains(expectedLog, StringComparison.Ordinal),
                message, TimeSpan.FromSeconds(5));
        }

        async Task<ViewCreation> ResetAsync()
        {
            int at = caster.World.CreationSnapshot().Count;
            caster.SendChat("?combatdepth fixture-reset");
            await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
            await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(at)
                .Any(view => view.Name == CombatFixtureLoadout.DummyName),
                "A fresh Melee Dummy is visible after fixture-reset", HeadlessPlayer.DefaultTimeout);
            return await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        }
    }
}
