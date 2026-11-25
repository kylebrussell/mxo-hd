using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class LegSweepCannyStrikeParityMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort LegSweep = LegSweepAdmission.LegSweep;       // 134
    private const ushort CannyStrike = CannyStrikeDamage.CannyStrike; // 295
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700, EnemyX = 16800;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, LegSweep, CannyStrike);

    public override string Name => "ability-parity-leg-sweep-canny-strike";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=0,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: new[] { Spy, LegSweep, CannyStrike }, hotbarWithdraw: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.MartialFixedDamageMode] = "spy-interlock"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "134,295",
            ["NativeCloseableRows"] = 2,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (+54% Thrown influence). Verified outdoor HOME ({X},{Y},{Z}) faces Melee Dummy 3 m north ({X},{Y},{DummyZ}) with QAEnemy 2 m west ({EnemyX},{Y},{Z}). Page 2: 1=Spy Style (1112), 2=Leg Sweep (134), 3=Canny Strike (295), 4=Withdraw. No initial tp needed.",
            "Step 1 (At-rest refusals — no IS debit or reuse): Select slot 1 (Spy Style), stay out of combat. Try slot 2 (134) and slot 3 (295) with Melee Dummy or empty target -> local gate or InvalidTarget (24) without debit.",
            "Step 2 (NPC interlock — choreography 283, neutral & +75 Thrown resistance): Select Melee Dummy, right-click Close Combat, reselect slot 1 (Spy Style). Run '?cd ma 134' and press slot 2 (134 Leg Sweep): expect 12 IS, 12 s own / 5 s grp 687 reuse, result 0x100, ILDB move 283 (LegSweep), 20 Thrown dmg (fixed 13 + 54%), and Stat 1009 -15 (10 s). Run '?cd ma 295' and press slot 3 (295 Canny Strike): expect 70 IS, 30 s own reuse, result 0x100, and VF4 Thrown dmg 12..15 (base 8..10 + 54%). Run '?cd ma75' (+75 Thrown resistance on dummy), wait reuse, repeat '?cd ma 134' + slot 2 -> [TYPED-RESISTANCE OURS] 134 type7 total75 raw20 final16, and '?cd ma 295' + slot 3 -> [TYPED-RESISTANCE OURS] 295 type7 total75 (9..12). Withdraw (slot 4).",
            "Step 3 (Live player PvP interlock on QAEnemy): /target QAEnemy, right-click Close Combat, reselect slot 1 (Spy Style). Press slot 2 (134 Leg Sweep): expect ILDB move 283 (LegSweep), 20 Thrown dmg, and QAEnemy receiving source134 Stat 1009 -15 (10 s) + paired removal at ~10 s. Wait reuse if needed and press slot 3 (295 Canny Strike) to verify PvP interlock acceptance and VF4 Thrown damage (12..15)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Leg Sweep / Canny Strike mode requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME has clear sight to Melee Dummy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "HOME has clear sight to QAEnemy");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Level-50 loadout fits memory");
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add("QAEnemy", enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);

        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await ObserveAsync(caster, 0, frame => Bonus(frame, Spy, 1034, 29, 0, false),
            "Spy Style activates +29 Thrown influence");

        // 1. At-rest refusals for BOTH 134 and 295.
        foreach (ushort id in new[] { LegSweep, CannyStrike })
        {
            await RefusedAsync(caster, id, Request(id, 0), AbilityRefusals.InvalidTarget);
            await RefusedAsync(caster, id, Request(id, dummyHandle), AbilityRefusals.InvalidTarget);
        }

        // 2. NPC Close Combat interlock: 134 Leg Sweep (move 283, 20 neutral -> 16 resisted) & 295 Canny Strike (12..15 -> 9..12).
        string dummyHex = WireFormat.ToHex(BitConverter.GetBytes(dummyHandle));
        caster.World.SendRpcs(WithHandle(MartialArtsStarterAdmissionMode.StockOpen, dummyHandle),
            WireFormat.FromHex($"40 {dummyHex} 01 6b"));
        await caster.WaitForRpcAsync($"40 {dummyHex} 00 6b");
        await caster.WaitForRpcAsync("47");
        caster.SendRpc($"81 51 {dummyHex}");
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);

        await RunNpcSpecialAsync(LegSweep, 20, 20, expectMove: 283, resisted: false);
        await RunNpcSpecialAsync(CannyStrike, 12, 15, expectMove: null, resisted: false);

        caster.SkipReceived();
        caster.SendChat("?cd ma75");
        await caster.WaitForTextAsync("Martial starter dummy resistance +75.");

        await Task.Delay(12500);
        await RunNpcSpecialAsync(LegSweep, 16, 16, expectMove: 283, resisted: true);

        await Task.Delay(18000);
        await RunNpcSpecialAsync(CannyStrike, 9, 12, expectMove: null, resisted: true);

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count,
            "Clean stock framing for 134 and 295");
        Console.WriteLine("LEG SWEEP + CANNY STRIKE PASS: refusals, move 283, fixed/VF4 damage, +75 Thrown resistance, and debuff verified.");
        return Array.Empty<byte>();

        async Task RunNpcSpecialAsync(ushort id, int minDmg, int maxDmg, int? expectMove, bool resisted)
        {
            caster.SkipReceived();
            caster.SendChat($"?cd ma {id}");
            await caster.WaitForTextAsync($"Martial starter fixture armed {id}.");

            int logFrom = server.DebugLog().Length;
            caster.SendRpc(Request(id, dummyHandle));
            var landed = new Regex($@"\[ABILITY\] {id} lands on view {dummy.ViewId} for (\d+)\b");
            await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[logFrom..]),
                $"Special {id} lands on Melee Dummy", TimeSpan.FromSeconds(12));
            string tail = server.DebugLog()[logFrom..];
            int dmg = int.Parse(landed.Match(tail).Groups[1].Value);
            Check.That(dmg >= minDmg && dmg <= maxDmg, $"{id} damage {dmg} within [{minDmg}..{maxDmg}]");
            if (expectMove.HasValue)
                Check.That(tail.Contains($"animation {expectMove.Value} (", StringComparison.Ordinal),
                    $"{id} plays named ILDB animation {expectMove.Value}");
            if (resisted)
                Check.That(tail.Contains($"[TYPED-RESISTANCE OURS] {id} type7 total75", StringComparison.Ordinal),
                    $"{id} applies +75 Thrown resistance");
            if (id == LegSweep)
                Check.That(tail.Contains("[EFFECTS] 134 on Melee Dummy: Stat 1009 -15 10 s", StringComparison.Ordinal),
                    "134 applies Melee Defense -15 (10 s) on Melee Dummy");
        }
    }

    private static byte[] Request(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
}
