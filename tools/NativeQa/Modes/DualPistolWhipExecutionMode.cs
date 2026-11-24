using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DualPistolWhipExecutionMode : NativeQaMode
{
    private const ushort Handguns = CombatStyles.Handguns, Whip = CombatSpecials.PistolWhip,
        Execution = CombatSpecials.DualPistolExecution;
    // First state-eligible stock 501 from PR995.
    private const string StockWhip = "80b9f501040002000000000000000000000000000000000000000000f5010000006b0000086b0000000000";
    private const int Level = 46, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private const int OtherX = 21650, OtherZ = 16250;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Whip, Execution);

    public override string Name => "ability-parity-dual-pistol-whip-execution";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.DualHandguns),
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES " +
                $"(110,{CombatFixtureWeapons.DualHandguns},{CombatFixtureWeapons.HandSlot},1,0)"
        },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z}," +
                "healthC=12000,healthM=12000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CasterX},rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)} WHERE charId=6114",
            $"UPDATE characters SET x={OtherX},y={Y},z={OtherZ},level={Level}," +
                $"exp={PlayerProgression.ExperienceForLevel(Level)},alignment=2 WHERE charId=6113",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Whip, Execution }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [CombatFixtureWeapons.PistolWhipWinMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "501", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Dual handguns 2496 in hand. CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) on clear outdoor NAV. Page 2 Handguns / Pistol Whip / Dual Pistol Execution. If needed use /target QAEnemy and strafe once.",
            "No teleport. /target QAEnemy, open radial Close Combat, then reselect Handguns with page 2 slot 1.",
            "Type /target QAOther. If the client locks selection to QAEnemy, record the local refusal and skip the off-target button. If QAOther is selected, type ?cd daze 30 and press slot 2. Expect stock refusal 24 or suppression, no IS, timer or landed 501. Keep the same fight.",
            "Now /target QAEnemy, ?cd daze 30, ?cd pww and press slot 2. Expect the accepted same-opponent cast: 30 IS, 10/5 s reuse, animation 287, damage 129 and source 501 state 90 for 9.576 s. QA supplies Daze and one exchange win only. The earlier stock ordinary-loss outcome carries."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var server = context.ActualServer!;
        var start = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Console.WriteLine($"READY QAOther {InvisibilityBeltWatcherMode.Warp(OtherX, Y, OtherZ, 192, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, OtherX, Y, OtherZ), "The nearby non-opponent is in clear sight");
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Whip/Execution equal-floor clear sight");
        string[] stand = start.Command.Split(' ');
        Console.WriteLine($"READY HOME type '{string.Join(' ', stand[..3])}', then ' {string.Join(' ', stand[3..7])}', then ' {stand[7]} {stand[8]}'; press Enter once.");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        enemy.SendRpc("42 00 00 00 00 00 00"); // Retained stock Speed, also used by the recipient-control sweep.
        DisablingShotMode.CheckStand(enemy, EnemyX);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        var bystander = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther", stockCharacterLoad: true);
        context.Helpers.Add(bystander.Handle, bystander);
        await PvpOpponentsFixture.EnterAsync(bystander);
        NativeQaReadiness.RequireNoAmbientAggro(server, bystander);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.DualHandguns);
        DisablingShotMode.CheckStand(caster, CasterX);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendChat("?cd pww");
        await caster.WaitForTextAsync("Pistol Whip win aid requires the active interlock with QAEnemy.");
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint targetHandle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(targetHandle));
        int cursor = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(PistolSlideMode.StockSelfDefense, targetHandle), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, cursor, rpc => EnrageNpcCloseCombatFixture.CombatFlag(rpc, target, 1),
            "Retained Close Combat opens with the visible player");
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(WithHandle(PistolSlideMode.StockHandguns, targetHandle));
        await ObserveAsync(caster, cursor, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: Handguns, TargetId: 171, AddRemoveType: 0 } && record.SignedValue > 0,
            "Retained Handguns selects the active dual pistol style");
        var other = await caster.WaitForViewNamedAsync(bystander.Handle);
        uint otherHandle = DuelPackets.Handle(other.ViewId, other.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(otherHandle))}");
        caster.SendChat("?cd daze 30");
        await caster.WaitForTextAsync("Daze fixture applied 884/199 to QAOther");
        await RefusedAsync(caster, Whip, WithHandle(StockWhip, otherHandle));
        await RefusedAsync(caster, Whip, WithHandle(StockWhip, targetHandle ^ 0x10000));
        caster.SendRpc($"81 51 {handle}");
        caster.SendChat("?cd daze 30");
        await caster.WaitForTextAsync("Daze fixture applied 884/199 to QAEnemy");
        caster.SendChat("?cd pww");
        await caster.WaitForTextAsync("Next Pistol Whip against QAEnemy is armed for one ordinary win.");
        int targetAt = enemy.World.RpcSnapshot().Count;
        int castAt = caster.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        caster.SendRpc(WithHandle(StockWhip, targetHandle));
        await EvidenceRetestWire.TimerAsync(caster, castAt, Whip, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(caster, castAt, 689, AbilityCooldowns.GroupTimer, 5);
        var blind = await ObserveAsync(enemy, targetAt, r => ManageBonusRecord.TryDecode(r.Bytes, out var v) &&
            v is { SourceId: Whip, TargetId: TempMods.BlindAbility, AddRemoveType: 0 },
            "Eligible Pistol Whip adds recipient Blind");
        ManageBonusRecord.TryDecode(blind.Bytes, out var record);
        Check.That(record != null && Math.Abs(record.Duration - 9.576f) < .001f, "Rank-46 recipient Blind lasts 9.576 s");
        var linked = await ObserveAsync(enemy, targetAt, r => Bonus(r, TempMods.BlindAbility, TempMods.CombatTacticsAbility, -30, 0, false),
            "Blind links Combat Tactics -30");
        ManageBonusRecord.TryDecode(linked.Bytes, out var linkedRecord);
        await enemy.WaitUntilAsync(() =>
        {
            var frames = enemy.World.RpcSnapshot().Skip(targetAt).ToArray();
            return frames.Any(r => Removal(r, record!)) && frames.Any(r => Removal(r, linkedRecord!)) &&
                frames.Any(r => r.Bytes.AsSpan().SequenceEqual(AbilityMessages.StateEnd(TempMods.BlindAbility, enemy.Handle)));
        }, "Matched Blind and linked removals with localized release", TimeSpan.FromSeconds(15));
        string result = server.DebugLog()[logAt..];
        Check.That(result.Contains("[PISTOL WHIP QA] 501 win consumed", StringComparison.Ordinal) &&
            result.Contains("animation 287 (Pistol Whip)", StringComparison.Ordinal) &&
            result.Contains("PistolWhipAbility lands on QAEnemy for 129", StringComparison.Ordinal),
            "The one scoped win uses the named 287 and ordinary live damage");
        static bool Removal(RpcRecord frame, ManageBonusRecord source) =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var removed) && removed == source with { AddRemoveType = 1 };
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean retained stock framing");
        Console.WriteLine("PISTOL WHIP BLIND PASS: exact retained 501, paired recipient Blind and Tactics adds/removals and release.");
        return Array.Empty<byte>();
    }
}
