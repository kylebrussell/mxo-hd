using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class PistolBarrageMode : NativeQaMode
{
    private const ushort Barrage = 244, Handguns = CombatStyles.Handguns, Soldier = 154;
    private const int Level = 27, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private const uint DualHandguns = 2496;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Barrage);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Soldier);

    public override string Name => "ability-parity-pistol-barrage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(), new[]
        {
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}; " +
                $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{DualHandguns},{CombatFixtureWeapons.HandSlot},1,0)"
        }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z},healthC=15000,healthM=15000,innerStrC=850,innerStrM=850 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CasterX},rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Barrage },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "244", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory and QAEnemy {EnemyLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. Dual handguns {DualHandguns} are in hand. Outdoor NAV CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). Strafe once if needed. Page 2 slot 1 Handguns and slot 2 Pistol Barrage.",
            "At rest, select QAEnemy, select Handguns and press slot 2. Retain the full first stock 80 b9 f4 00. Expect a 2 s cast, 30 IS and 10 s own / 5 s group 689. The normal direct hit is 19 Ballistic with no queued round. Keep one natural 40% Powerless proc if it appears. Retry the same target after reuse until Powerless is observed.",
            """For a natural at-rest Powerless, immediately send {"actor":"QAEnemy","action":"barrage-probe"}. It sends the retained stock Soldier Upgrade from the affected helper. Expect 244/96 for eight seconds with linked 96/0 and separate 734/681 for eight seconds. Expect no Soldier Upgrade during Powerless and +20 Ballistic Resistance on the recovery request after expiry. Save {"actor":"QAEnemy","action":"barrage-records"} to confirm the paired IDs and one Powerless state-end.""",
            "With +20 armor active on the same QAEnemy, use the target's radial Close Combat and reselect 129 after the opening round. Press slot 2. Expect the next-move queue only after the 2 s cast, +15 tactic, 9534 at the ordinary 140-unit pose and [TYPED-RESISTANCE OURS] 244 type1 total20 raw19 final17. A queued miss follows the ordinary dual-gun exchange and leaves 244 damage and Powerless absent."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var casterStand = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Verified clear outdoor dual-handgun sight");
        Console.WriteLine($"CSR {casterStand.Command}");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckStand(enemy, EnemyX);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        DisablingShotMode.CheckStand(caster, CasterX);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, DualHandguns);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint target = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(target));
        int cursor = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(PistolSlideMode.StockSelfDefense, target), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, cursor, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
            "Stock Close Combat opens PvP");
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(WithHandle(PistolSlideMode.StockHandguns, target));
        await ObserveAsync(caster, cursor, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: Handguns, TargetId: 171, AddRemoveType: 0 } && record.SignedValue > 0,
            "The retained 129 request activates Handguns and publishes its Ballistic Damage record");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Pistol Barrage exact support framing");
        Console.WriteLine("PISTOL BARRAGE BOOTSTRAP PASS: both verified stands, legal dual-handgun profile, stock Close Combat and 129.");
        return Array.Empty<byte>();
    }

    internal static void ProbePowerless(HeadlessPlayer actor)
    {
        Check.Equal(6114u, actor.CharId, "Only QAEnemy uses the Powerless probe");
        actor.SendRpc(DisablingShotMode.StockUpgrade);
    }

    internal static object[] Records(HeadlessPlayer actor) => actor.World.RpcSnapshot().Where(frame =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record is
            { SourceId: Barrage or TempMods.PowerlessAbility or TempMods.ControlImmunityTimerAbility } ||
        frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.StateEnd(TempMods.PowerlessAbility, actor.Handle)))
        .Select(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record)
            ? new { record!.SourceId, record.TargetId, record.RecordId, record.Duration, record.AddRemoveType, Wire = frame.Hex }
            : (object)new { StateEnd = frame.Hex }).ToArray();
}
