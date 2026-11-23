using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.Modes.StarterInterlockControlMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class ImmobilizingShotMode : NativeQaMode
{
    private const ushort Shot = 254, Soldier = 154, Upgrade = 1061;
    private const int Level = 25, VictimX = 21550, ShooterX = 21750, Y = 1095, Z = 16150, WestX = 19750;
    private const int Neutral = 20, Resisted = 18;
    private static readonly Position Origin = new(VictimX, Y, Z), Step = new(VictimX - 20, Y, Z);
    private static readonly ushort[] VictimLoadout = DisablingShotMode.Closure(2, 17, Soldier);
    private static readonly ushort[] ShooterLoadout = DisablingShotMode.Closure(2, 17, CombatStyles.Rifles, Shot);

    public override string Name => "ability-parity-immobilizing-shot";
    public override NativeQaModePlan Plan { get; } = new(VictimLoadout, VictimX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z},healthC=5000,healthM=5000,innerStrC=700,innerStrM=700 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={VictimX},rotation={MobAggro.YawTowards(VictimX, Z, WestX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={ShooterX},rotation={MobAggro.YawTowards(ShooterX, Z, VictimX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", ShooterLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)")),
            $"DELETE FROM inventory WHERE charId=6114 AND slot IN ({CombatFixtureWeapons.HandSlot},107)",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (6114,{CombatFixtureWeapons.Rifle},{CombatFixtureWeapons.HandSlot},1,0)"
        }, DevCommands: true, hotbarAbilities: new[] { Upgrade }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "254", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR recipient uses {VictimLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. QAEnemy rifle caster uses {ShooterLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. CSR ({VictimX},{Y},{Z}) faces west toward ({WestX},{Y},{Z}), away from QAEnemy ({ShooterX},{Y},{Z}).",
            """For phase 1, stay on CSR's stand and leave page 2 slot 1 alone. Submit {"actor":"QAEnemy","action":"immobilizing-fire"}. QAEnemy uses the exact retained stock 254 and pays 30 IS, casts for 2 s, starts 10 s own and 5 s family 690 reuse. The legal profile gives +7 Rifles and +2/+3 linked mastery influence, so CSR takes 20 Ballistic damage.""",
            "On Rooted, hold forward for one second, then repeat the same input after its removal. Expect no westward progress while Rooted and normal westward displacement afterward. Capture CSR's own MovementScale -1 then 0 and position updates in the PacketLog.",
            "Expect [MODIFIER] for CSR source=254 stat=519 -15, stat=0 +50 and stat=99 0, each for 8 s and with matching removals. CSR gets the Rooted activation and normal release lines. Record any separate generic term line.",
            """For phase 2, keep QAEnemy within 20 m. Use page 2 slot 1 Soldier Upgrade 1061 and confirm [MODIFIER] char=110 source=1061 stat=181 +20. Wait until at least 10 s after the first hit, then submit {"actor":"QAEnemy","action":"immobilizing-fire"} again. Expect [TYPED-RESISTANCE OURS] 254 type1 total20 raw20 final18, and the same root. The interlock HUD stays clear. Projected DONE +0."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        _ = Stand(VictimX, Z);
        _ = Stand(ShooterX, Z);
        _ = Stand(WestX, Z);
        _ = Stand(VictimX - 20, Z);
        Check.That(NavMesh.HasSightPath(1, ShooterX, Y, Z, WestX, Y, Z), "Clear rifle sight and west movement lane.");

        var shooter = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(shooter.Handle, shooter);
        await PvpOpponentsFixture.EnterAsync(shooter);
        DisablingShotMode.CheckStand(shooter, ShooterX);
        DisablingShotMode.CheckProfile(shooter, ShooterLoadout, Level);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(shooter, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, shooter);
        if (context.StockProbe is not { } victim) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(victim, VictimLoadout, Level);
        await PvpOpponentsFixture.EnterAsync(victim);
        DisablingShotMode.CheckStand(victim, VictimX);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, victim);
        var victimView = await shooter.WaitForViewNamedAsync(victim.Handle);
        int ownViews = victim.World.ViewSnapshot().Count;
        int observerViews = shooter.World.ViewSnapshot().Count;
        int recipientAt = victim.World.RpcSnapshot().Count;
        int shooterAt = shooter.World.RpcSnapshot().Count;
        await FireAsync(shooter, victim.Handle);
        await ObserveHitAsync(victim, shooter, recipientAt, Neutral);
        int rootView = await ObserveScaleAsync(victim, 2, PlayerViewAttributes.SelfMovementScale, ownViews, -1);
        int rootObserver = await ObserveScaleAsync(shooter, victimView.ViewId, PlayerViewAttributes.OtherMovementScale, observerViews, -1);
        Check.Equal(Origin, await CorrectedPositionAsync(victim, Step), "Root holds the live recipient stand.");
        await ObserveAsync(victim, recipientAt, rpc => Bonus(rpc, Shot, TempMods.RootedAbility, 0, 8, true), "Rooted expires.");
        await ObserveScaleAsync(victim, 2, PlayerViewAttributes.SelfMovementScale, rootView, 0);
        await ObserveScaleAsync(shooter, victimView.ViewId, PlayerViewAttributes.OtherMovementScale, rootObserver, 0);

        int releasedAt = shooter.World.ViewSnapshot().Count;
        Move(victim, Step);
        await ObserveMoveAsync(shooter, victimView.ViewId, releasedAt, Step, "The same recipient step reaches the observer after removal.");

        int armorAt = victim.World.RpcSnapshot().Count;
        victim.SendRpc(DisablingShotMode.StockUpgrade);
        await ObserveAsync(victim, armorAt, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: Upgrade, TargetId: 181, ValueBits: 20, AddRemoveType: 0 }, "Stock Soldier Upgrade gives +20 Ballistic Resistance.");
        await EvidenceRetestWire.TimerAsync(shooter, shooterAt, Shot, AbilityCooldowns.OwnTimer, 10, ended: true);
        await EvidenceRetestWire.TimerAsync(shooter, shooterAt, 690, AbilityCooldowns.GroupTimer, 5, ended: true);

        recipientAt = victim.World.RpcSnapshot().Count;
        await FireAsync(shooter, victim.Handle);
        await ObserveHitAsync(victim, shooter, recipientAt, Resisted);
        Check.Equal(0, shooter.World.Problems.Count + victim.World.Problems.Count, "Immobilizing Shot retained frames.");
        return Array.Empty<byte>();
    }

    internal static async Task FireAsync(HeadlessPlayer shooter, string victim)
    {
        Check.Equal("QAEnemy", shooter.Handle, "Only the disposable rifle helper can fire.");
        var view = await shooter.WaitForViewNamedAsync(victim);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        shooter.SkipReceived();
        shooter.SendRpc(WithHandle(DisablingShotMode.StockRifles, handle));
        await shooter.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 93 00 00 00");
        shooter.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        shooter.SendRpc(WithHandle(GunmanRifleOpenerScopeMode.Immobilizing, handle));
        await EvidenceRetestWire.CastBarAsync(shooter, Shot, 2);
    }

    private static async Task ObserveHitAsync(HeadlessPlayer victim, HeadlessPlayer shooter, int from, int damage)
    {
        byte[] expected = AbilityMessages.Damage(false, Shot, shooter.Handle, victim.Handle, damage, Neutral - damage);
        await ObserveAsync(victim, from, rpc => rpc.Bytes.AsSpan().SequenceEqual(expected), $"Immobilizing Shot landed {damage}.");
        await ObserveAsync(victim, from, rpc => Bonus(rpc, Shot, TempMods.RootedAbility, 0, 8, false), "Immobilizing Shot gives eight seconds of Rooted.");
    }
}
