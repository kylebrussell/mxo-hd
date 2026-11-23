using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class WoundingShotMode : NativeQaMode
{
    private const ushort Shot = CombatSpecials.WoundingShot, Upgrade = 1061;
    private const int Level = 20, EnemyLevel = 25, CasterX = 21750, EnemyX = 21550, Y = 1095, Z = 16150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, CombatStyles.Rifles, Shot);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, CombatStyles.Rifles, 254, 255, 154);

    public override string Name => "ability-parity-wounding-shot";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={CasterX},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)},healthC=5000,healthM=5000,innerStrC=700,innerStrM=700 WHERE charId=110",
            $"UPDATE characters SET level={EnemyLevel},exp={PlayerProgression.ExperienceForLevel(EnemyLevel)},x={EnemyX},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)},healthC=5000,healthM=5000,innerStrC=700,innerStrM=700 WHERE charId=6114",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{EnemyLevel},1)")),
            $"DELETE FROM inventory WHERE charId=6114 AND slot IN ({CombatFixtureWeapons.HandSlot},107)",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (6114,{CombatFixtureWeapons.Rifle},{CombatFixtureWeapons.HandSlot},1,0)"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.Rifles, Shot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "253", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"CSR L{Level} {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory at ({CasterX},{Y},{Z}), facing QAEnemy L{EnemyLevel} at ({EnemyX},{Y},{Z}). Both verified outdoor NAV. Rifle equipped. Page 2 Rifles and Wounding Shot.",
            "Select QAEnemy and Rifles while at rest. Press Wounding Shot and retain the complete first stock request and visible cast/impact animation. Expect 20 IS, 2 s cast, 10 s own and 5 s group 690 reuse, no early hit and no interlock. The authored fixed base for this policy is 6 Ballistic; the expected neutral hit is 6. PR357's older server produced 70 with its former damage path.",
            "[MODIFIER] on QAEnemy source=253 stat=1157 value=+14 for 10 s. Expect three separate [EFFECTS] four-point NeutralDOT ticks at about 2.5, 5 and 7.5 s, with no fourth at expiry. GOB stores 7 before scaling; 14 is the retained capture-based runtime value.",
            "Repeat after the 10 s reuse if the natural 30% Stun does not proc. A proc adds 253/102 Stun and 734/681 immunity for 4 s each. The helper checks the matched removals and Soldier Upgrade refusal during Stun, then equips +20 Ballistic Resistance on the same QAEnemy after release. Wait for WOUNDING STUN PASS and the armor log.",
            "With the same QAEnemy at the same stand, use Wounding Shot after reuse. At L20 vs L25, expect [TYPED-RESISTANCE OURS] type1 total20 raw6 final3, with the same +14 DOT and three unmodified ticks. Then use QAEnemy's radial Close Combat, reselect Rifles and try Wounding Shot. Expect stock suppression or an outside-combat refusal, no charge or cast. Use ?cd fixture-disengage afterward.",
            "The bootstrap replays only retained Rifles/254/255 controls. Keep the native PacketLog, helper PacketLog, DebugLog, and wounding-shot-stun.json when seen. The archived Zero One Soldier NPC raw value of 70 uses a different matchup from this player profile. Projected DONE +1 (253) after these specific native checks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { CasterX, EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1,
                $"Exact grounded outdoor stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Grounded outdoor rifle sight at 2 m");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, EnemyLevel);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckStand(enemy, EnemyX);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(enemy, CombatFixtureWeapons.Rifle);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster)
        {
            _ = WatchStunAsync(context, enemy, server);
            return Array.Empty<byte>();
        }

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        DisablingShotMode.CheckStand(caster, CasterX);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        await caster.WaitForViewNamedAsync(enemy.Handle);
        int from = enemy.World.RpcSnapshot().Count;
        await ImmobilizingShotMode.FireAsync(enemy, caster.Handle, followWithDamage: true);
        await EvidenceRetestWire.TimerAsync(enemy, from, 254, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(enemy, from, 255, AbilityCooldowns.OwnTimer, 10);
        Check.Equal(0, enemy.World.Problems.Count + caster.World.Problems.Count, "Retained rifle opener controls decode cleanly");
        Console.WriteLine("WOUNDING SHOT BOOTSTRAP PASS: L20 caster and L25 control, retained 147/254/255. First 253 request remains native.");
        return Array.Empty<byte>();
    }

    private static async Task WatchStunAsync(NativeQaBootstrapContext context, HeadlessPlayer enemy, ServerInstance server)
    {
        string stop = Path.Combine(context.OutputDirectory, "stop");
        bool Running() => !File.Exists(stop) && !server.HasExited;
        bool Stun(RpcRecord frame, bool removed) => Bonus(frame, Shot, TempMods.StunAbility, 0, 4, removed);
        bool Immunity(RpcRecord frame, bool removed) => Bonus(frame, TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, 0, 4, removed);
        int cursor = enemy.World.RpcSnapshot().Count;
        try
        {
            RpcRecord? stun = null;
            while (Running() && (stun = enemy.World.RpcSnapshot().Skip(cursor).FirstOrDefault(frame => Stun(frame, false))) == null)
                await Task.Delay(25);
            if (stun == null) return;
            var immunity = await ObserveAsync(enemy, cursor, frame => Immunity(frame, false), "Wounding Shot's independent four-second immunity");
            int refusalAt = enemy.World.RpcSnapshot().Count;
            enemy.SendRpc(DisablingShotMode.StockUpgrade);
            var refused = await ObserveAsync(enemy, refusalAt, frame => AbilityRefusals.TryDecode(frame.Bytes, out var refusal) &&
                refusal.AbilityID == Upgrade, "Soldier Upgrade refused during Wounding Shot Stun");
            await enemy.WaitForTextAsync("You cannot act while stunned or pacified.");
            var stunOff = await ObserveAsync(enemy, cursor, frame => Stun(frame, true) && SameRecord(stun, frame), "Matched Stun removal");
            var immunityOff = await ObserveAsync(enemy, cursor, frame => Immunity(frame, true) && SameRecord(immunity, frame), "Matched immunity removal");
            int recoveredAt = enemy.World.RpcSnapshot().Count;
            enemy.SendRpc(DisablingShotMode.StockUpgrade);
            var armor = await ObserveAsync(enemy, recoveredAt, frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record) &&
                record is { SourceId: Upgrade, TargetId: 181, SignedValue: 20, AddRemoveType: 0 },
                "Recovered Soldier Upgrade grants the same QAEnemy +20 Ballistic Resistance");
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "wounding-shot-stun.json"), JsonSerializer.Serialize(new
            {
                enemy.CharId, Stun = stun.Hex, StunOff = stunOff.Hex, Immunity = immunity.Hex, ImmunityOff = immunityOff.Hex,
                RefusedUpgrade = refused.Hex, RecoveredArmor = armor.Hex
            }));
            Console.WriteLine("WOUNDING STUN PASS: matched four-second Stun and immunity, refusal and recovered +20 Ballistic Resistance.");
        }
        catch (Exception error)
        {
            if (Running()) Console.WriteLine($"WOUNDING STUN FAILED: {error.Message}");
        }
    }

    private static bool SameRecord(RpcRecord add, RpcRecord remove) =>
        ManageBonusRecord.TryDecode(add.Bytes, out var first) && ManageBonusRecord.TryDecode(remove.Bytes, out var second) &&
        first!.Locator == second!.Locator;
}
