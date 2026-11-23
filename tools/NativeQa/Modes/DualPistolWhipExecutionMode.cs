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
    private const int Level = 46, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
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
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Whip, Execution }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [CombatFixtureWeapons.PistolWhipLossMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "501,502", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Dual handguns 2496 in hand. CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) on clear outdoor NAV. Page 2 Handguns / Pistol Whip / Dual Pistol Execution. If needed use /target QAEnemy and strafe once.",
            "Select Handguns and QAEnemy, then try Pistol Whip at rest. Expect a local restriction or an invalid-target refusal with no debit. Open Close Combat on QAEnemy and reselect Handguns after the opening round. Try Pistol Whip while QAEnemy is clear of Daze. Expect a local restriction or 80 ba with required state 2.",
            "Apply ?cd daze 30 to the selected QAEnemy. Use Pistol Whip. Save its first complete request, instant queue with 30 IS, own 10 s and group689 5 s. A landed round at the normal 140-unit sent pose uses animation 287. For the loss retest, use ?cd pw before pressing slot 2. Expect one [PISTOL WHIP QA] 501 loss consumed line, an ordinary QAEnemy win with no 287 or 501 hit/Blind, and the fight still active. Verify the 30 IS and 10/5 s reuse still apply. The prior 287 and 502 checks carry.",
            "With the same live opponent and active Handguns, use Dual Pistol Execution after family reuse. Save its first complete request, 4 s cast bar, 35 IS, own 10 s and group689 5 s. The cast queues only after four seconds and does not hit between rounds. A landed exchange at the 140-unit pose uses 9532. A normal miss uses ordinary exchange animation. Note observed damage and control effects as evidence only.",
            "Use ?cd stop and wait until combat has cleared. After own reuse, keep QAEnemy selected, return to Handguns and use Dual Pistol Execution again. Its four-second at-rest cast may land directly without a queued move. The supplied choreography proof does not settle the historical damage magnitude."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var server = context.ActualServer!;
        var start = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Whip/Execution equal-floor clear sight");
        Console.WriteLine($"Dual-pistol return stand {start.Command}");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        enemy.SendRpc("42 00 00 00 00 00 00"); // Retained stock Speed, also used by the recipient-control sweep.
        DisablingShotMode.CheckStand(enemy, EnemyX);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.DualHandguns);
        DisablingShotMode.CheckStand(caster, CasterX);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendChat("?cd pw");
        await caster.WaitForTextAsync("Pistol Whip loss aid requires the active interlock with QAEnemy.");
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
        caster.SendChat("?cd pw");
        await caster.WaitForTextAsync("Next Pistol Whip against QAEnemy is armed for one ordinary loss.");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean retained stock framing");
        Console.WriteLine("DUAL PISTOL WHIP/EXECUTION BOOTSTRAP PASS: verified stands, legal loadout and held dual handguns; exact retained Close Combat and Handguns only.");
        return Array.Empty<byte>();
    }
}
