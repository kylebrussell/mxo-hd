using System.Diagnostics;
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
    private const ushort Shot = 254, BreakShot = 255, Soldier = 154, Upgrade = 1061;
    private const int BreakAttempts = 8;
    private const int Level = 25, VictimX = 21550, ShooterX = 21750, Y = 1095, Z = 16150, WestX = 19750;
    private const int Neutral = 20, Resisted = 18;
    private static readonly Position Origin = new(VictimX, Y, Z), Step = new(VictimX - 20, Y, Z);
    private static readonly ushort[] VictimLoadout = DisablingShotMode.Closure(2, 17, Soldier);
    private static readonly ushort[] ShooterLoadout = DisablingShotMode.Closure(2, 17, CombatStyles.Rifles, Shot, BreakShot);

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
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "254", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR recipient uses {VictimLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. QAEnemy rifle caster uses {ShooterLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. CSR ({VictimX},{Y},{Z}) faces west toward ({WestX},{Y},{Z}), away from QAEnemy ({ShooterX},{Y},{Z}).",
            """For phase 1, stay on CSR's stand and leave page 2 slot 1 alone. Submit {"actor":"QAEnemy","action":"immobilizing-fire"}. QAEnemy uses the exact retained stock 254 and pays 30 IS, casts for 2 s, starts 10 s own and 5 s family 690 reuse. The legal profile gives +7 Rifles and +2/+3 linked mastery influence, so CSR takes 20 Ballistic damage.""",
            "On Rooted, hold forward for one second, then repeat the same input after its removal. Expect no westward progress while Rooted and normal westward displacement afterward. Capture CSR's own MovementScale -1 then 0 and position updates in the PacketLog.",
            "Natural expiry at about 8 s: expect source254 stat519 -15, stat0 +50 and state99 off with their original record IDs, the linked state99 off, one Rooted release (81 67, 0x7a00003c), and MovementScale back to 0. The tracked retail releases use this presentation.",
            """For phase 2 return to the printed CSR stand and confirm Soldier Upgrade 1061 gives [MODIFIER] char=110 stat181 +20. Wait for 254 reuse and submit {"actor":"QAEnemy","action":"immobilizing-break"}. Expect raw20/final18 from 254. The helper then lands the retained Disabling Shot 255 without opening interlock.""",
            "Hold forward briefly after Rooted. A natural 50% damage break releases Rooted before 8 s. Capture the same paired removals and one Rooted release, plus forward movement resuming at release. If the follow-up lands and Rooted lasts all 8 s, that draw retained the root. Return to the printed stand and wait 10 s after the follow-up hit before retrying. Refresh Soldier Upgrade if it was switched off.",
            "Keep the client PacketLog for both phases. Capture any extra release text verbatim. The retail Force Combat entry formula is outside this check."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stand = Stand(VictimX, Z);
        Console.WriteLine($"CSR {stand.Command}");
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
        int shooterAt = shooter.World.RpcSnapshot().Count;
        bool naturalBreak = await VerifyRootAsync(Neutral, followWithDamage: false);
        Check.That(!naturalBreak, "The no-follow-up control expires on its natural timer.");

        int armorAt = victim.World.RpcSnapshot().Count;
        victim.SendRpc(DisablingShotMode.StockUpgrade);
        await ObserveAsync(victim, armorAt, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: Upgrade, TargetId: 181, ValueBits: 20, AddRemoveType: 0 }, "Stock Soldier Upgrade gives +20 Ballistic Resistance.");
        await EvidenceRetestWire.TimerAsync(shooter, shooterAt, Shot, AbilityCooldowns.OwnTimer, 10, ended: true);

        bool damageBreak = false;
        for (int attempt = 0; attempt < BreakAttempts; attempt++)
        {
            int from = shooter.World.RpcSnapshot().Count;
            damageBreak = await VerifyRootAsync(Resisted, followWithDamage: true);
            if (damageBreak) break;
            await EvidenceRetestWire.TimerAsync(shooter, from, BreakShot, AbilityCooldowns.OwnTimer, 10, ended: true);
        }
        Check.That(damageBreak, "A landed follow-up drew the ordinary chance-driven damage release.");
        Check.Equal(0, shooter.World.Problems.Count + victim.World.Problems.Count, "Immobilizing Shot retained frames.");
        return Array.Empty<byte>();

        async Task<bool> VerifyRootAsync(int expectedDamage, bool followWithDamage)
        {
            int ownViews = victim.World.ViewSnapshot().Count;
            int observerViews = shooter.World.ViewSnapshot().Count;
            int recipientAt = victim.World.RpcSnapshot().Count;
            var fire = FireAsync(shooter, victim.Handle, followWithDamage);
            await ObserveHitAsync(victim, shooter, recipientAt, expectedDamage);
            var elapsed = Stopwatch.StartNew();
            var adds = await RootAdditionsAsync(victim, recipientAt);
            int rootView = await ObserveScaleAsync(victim, 2, PlayerViewAttributes.SelfMovementScale, ownViews, -1);
            int rootObserver = await ObserveScaleAsync(shooter, victimView.ViewId, PlayerViewAttributes.OtherMovementScale, observerViews, -1);
            Check.Equal(Origin, await CorrectedPositionAsync(victim, Step), "Root holds the live recipient stand.");

            await fire;
            if (followWithDamage)
            {
                await ObserveAsync(victim, recipientAt, rpc => HitBy(rpc, BreakShot, toCaster: false),
                    "The retained follow-up rifle shot deals real damage.");
            }
            await VerifyReleaseAsync(victim, recipientAt, adds);
            double seconds = elapsed.Elapsed.TotalSeconds;
            bool early = seconds < 7;
            Check.That(early ? followWithDamage && seconds >= 2 : seconds <= 10,
                $"Expected a landed early break or ordinary eight-second expiry, got {seconds:F2} s.");

            await ObserveScaleAsync(victim, 2, PlayerViewAttributes.SelfMovementScale, rootView, 0);
            await ObserveScaleAsync(shooter, victimView.ViewId, PlayerViewAttributes.OtherMovementScale, rootObserver, 0);
            int releasedAt = shooter.World.ViewSnapshot().Count;
            Move(victim, Step);
            await ObserveMoveAsync(shooter, victimView.ViewId, releasedAt, Step, "The same step reaches the observer after release.");
            int returnedAt = shooter.World.ViewSnapshot().Count;
            Move(victim, Origin);
            await ObserveMoveAsync(shooter, victimView.ViewId, returnedAt, Origin, "The recipient returns to the verified stand.");
            Console.WriteLine($"IMMOBILIZING RELEASE {(early ? "damage" : "natural")} at {seconds:F2} s with matching records, state-end and movement.");
            return early;
        }
    }

    internal static async Task FireAsync(HeadlessPlayer shooter, string victim, bool followWithDamage = false)
    {
        Check.Equal("QAEnemy", shooter.Handle, "Only the disposable rifle helper can fire.");
        var view = await shooter.WaitForViewNamedAsync(victim);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        shooter.SkipReceived();
        shooter.SendRpc(WithHandle(DisablingShotMode.StockRifles, handle));
        await shooter.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 93 00 00 00");
        shooter.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        int from = shooter.World.RpcSnapshot().Count;
        shooter.SendRpc(WithHandle(GunmanRifleOpenerScopeMode.Immobilizing, handle));
        await EvidenceRetestWire.CastBarAsync(shooter, Shot, 2);

        if (!followWithDamage) return;
        await ObserveAsync(shooter, from, rpc => HitBy(rpc, Shot, toCaster: true), "The root lands before the damage probe.");
        await Task.Delay(TimeSpan.FromSeconds(1));
        var stillSelected = await shooter.WaitForViewNamedAsync(victim);
        Check.Equal(handle, DuelPackets.Handle(stillSelected.ViewId, stillSelected.SpawnId), "The original victim is still in view.");
        shooter.SendRpc(WithHandle(DisablingShotMode.StockShot, handle));
        await EvidenceRetestWire.CastBarAsync(shooter, BreakShot, 2);
    }

    private static bool HitBy(RpcRecord packet, ushort ability, bool toCaster) =>
        packet.Bytes is [0x81, 0x67, ..] && packet.Bytes.Length >= 29 &&
        BitConverter.ToUInt32(packet.Bytes, 6) == (toCaster ? AbilityMessages.DamageCaster : AbilityMessages.DamageTarget) &&
        BitConverter.ToUInt32(packet.Bytes, 15) == AbilityMessages.AbilityGameObject(ability);

    private static async Task<ManageBonusRecord[]> RootAdditionsAsync(HeadlessPlayer victim, int from)
    {
        var records = new List<ManageBonusRecord>();
        foreach (var (source, target, value, duration) in new (ushort, ushort, int, float)[]
        {
            (Shot, 519, -15, 8), (Shot, 0, 50, 8), (Shot, TempMods.RootedAbility, 0, 8),
            (TempMods.RootedAbility, 0, 0, 0)
        })
        {
            var added = await ObserveAsync(victim, from, rpc => Bonus(rpc, source, target, value, duration, false),
                $"Root add {source}/{target} {value}.");
            ManageBonusRecord.TryDecode(added.Bytes, out var record);
            records.Add(record!);
        }
        return records.ToArray();
    }

    private static async Task VerifyReleaseAsync(HeadlessPlayer victim, int from, ManageBonusRecord[] additions)
    {
        foreach (var added in additions)
        {
            var removed = added with { AddRemoveType = 1 };
            await ObserveAsync(victim, from, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record == removed,
                $"Matching root removal {removed.SourceId}/{removed.TargetId} record {removed.RecordId}.");
        }
        byte[] stateEnd = AbilityMessages.StateEnd(TempMods.RootedAbility, victim.Handle);
        await ObserveAsync(victim, from, rpc => rpc.Bytes.AsSpan().SequenceEqual(stateEnd), "The captured Rooted state-end line.");
        var released = victim.World.RpcSnapshot().Skip(from).ToArray();
        Check.Equal(1, released.Count(rpc => rpc.Bytes.AsSpan().SequenceEqual(stateEnd)), "One Rooted release line.");
        Check.That(!released.Any(rpc => rpc.Bytes is [0x81, 0x67, ..] && rpc.Bytes.Length >= 15 &&
            BitConverter.ToUInt32(rpc.Bytes, 6) == 0x7c000236 && BitConverter.ToUInt32(rpc.Bytes, 11) == Shot),
            "No separate 254 generic term in the client stream after release, matching the recipient captures.");
    }

    private static async Task ObserveHitAsync(HeadlessPlayer victim, HeadlessPlayer shooter, int from, int damage)
    {
        byte[] expected = AbilityMessages.Damage(false, Shot, shooter.Handle, victim.Handle, damage, Neutral - damage);
        await ObserveAsync(victim, from, rpc => rpc.Bytes.AsSpan().SequenceEqual(expected), $"Immobilizing Shot landed {damage}.");
        await ObserveAsync(victim, from, rpc => Bonus(rpc, Shot, TempMods.RootedAbility, 0, 8, false), "Immobilizing Shot gives eight seconds of Rooted.");
    }
}
