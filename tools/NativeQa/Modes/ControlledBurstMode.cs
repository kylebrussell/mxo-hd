using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class ControlledBurstMode : NativeQaMode
{
    private const ushort Burst = 250, Style = CombatStyles.SubmachineGuns, Soldier = 154;
    private const int Level = 27, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Style, Burst);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Soldier);
    // Full retained stock PR330 stance frames. The 250 frame belongs to the native run.
    private const string StockStyle = "80b99e000400020000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";

    public override string Name => "ability-parity-controlled-burst";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.DualSmgs) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z},healthC=15000,healthM=15000,innerStrC=850,innerStrM=850 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CasterX},rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Style, Burst },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "250", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. QAEnemy {EnemyLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. Outdoor NAV CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). The dual SMGs (2503) start in hand. Strafe once after login if needed. Use the target's radial Close Combat to avoid starting Free Fire, then select Submachine Guns again.",
            "Neutral interlock: select Submachine Guns on page 2 slot 1, select QAEnemy and use Close Combat. Reselect 158, then press slot 2 once. Keep the first complete stock 80 b9 fa 00 request. Expect [ABILITY-COST] 250 paid=30 and a 4 s cast bar. Before the end there is no queued 250 or 250 damage. After completion expect 10 s own/5 s group 689 and the next resolved eligible round. On a hit expect 21082 and [PVP] ControlledBurstAbility lands on QAEnemy for 29. For a queued 250 that does not land, retain the [PVP] next-move and result lines, the [COMBAT-VIEW] round, and the missing 250 hit/Enraged records. Leave both fighters on Speed for the ordinary dual-SMG versus Self Defense palette. With both misses expect a dodged exchange 3793–3795 or 20491–20493, based on the winning side. If QAEnemy lands an ordinary counter, its hit exchange is 20494/20496/20498 and any damage belongs to that counter. This is the shipped style fallback for the unresolved named deflection move.",
            """With a clean HUD after ?cd stop, send {"actor":"QAEnemy","action":"burst-armor"}. Verify source1061 stat181 +20. Reopen Close Combat against the same QAEnemy, reselect Submachine Guns and repeat after reuse. On a hit expect [TYPED-RESISTANCE OURS] 250 type1 total20 raw29 final26 with 21082. The authored direct base is 26 before this profile's +13 Ballistic influence.""",
            """All casts keep the ordinary 40% Enraged roll. Repeat a landed opportunity after each reuse until one procs. Expect 250 -> 93 and linked 93 -> 0, eight seconds, with no new control-immunity timer. Send {"actor":"QAEnemy","action":"burst-withdraw"} during the eight seconds and again after expiry. [PVP] QAEnemy is withdrawing only after expiry. Send {"actor":"QAEnemy","action":"burst-records"} for the exact paired record IDs and one Enraged state-end. No RNG result is forced.""",
            "Route control after the measured interlock hits: finish with ?cd stop, keep QAEnemy selected and try slot 2 from rest after reuse. Record the stock gate or complete request. If admitted at rest, the existing selected-hit route casts for 4 s, debits 30 IS, and lands directly for 26 under the same armor. It must not queue 21082 in a fight that did not exist at cast start."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var casterStand = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Verified clear outdoor dual-SMG sight");
        Console.WriteLine($"CSR {casterStand.Command}");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckStand(enemy, EnemyX);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        enemy.SendRpc("42 00 00 00 00 00 00"); // Retained stock Speed from the recipient-control sweep.
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        DisablingShotMode.CheckStand(caster, CasterX);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.DualSmgs);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint target = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(target));
        int cursor = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(StockSelfDefense, target), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, cursor, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
            "Ordinary PvP Close Combat admits the same selected opponent");
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(WithHandle(StockStyle, target));
        await ObserveAsync(caster, cursor, rpc => Bonus(rpc, Style, 171, 8, 0, false), "The stock SMG style adds +8 Ballistic Damage");
        await ObserveAsync(caster, 0, rpc => Bonus(rpc, 125, 1139, 2, 0, false), "Loaded Firearms Expert adds +2 Ballistic Damage Bonus");
        await ObserveAsync(caster, 0, rpc => Bonus(rpc, 246, 1139, 3, 0, false), "Loaded Dual SMGs adds +3 Ballistic Damage Bonus");

        await PistolSlideMode.ApplyArmorAsync(enemy);
        int logAt = server.DebugLog().Length;
        SendWithdraw(enemy);
        await enemy.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[PVP] QAEnemy is withdrawing", StringComparison.Ordinal),
            "The unchanged stock Withdraw works before Enraged", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Controlled Burst exact support framing");
        Console.WriteLine("CONTROLLED BURST BOOTSTRAP PASS: live dual-SMG interlock, +13 Ballistic influence, same-target +20 resistance, and baseline stock Withdraw.");
        return Array.Empty<byte>();
    }

    internal static void SendWithdraw(HeadlessPlayer actor)
    {
        Check.Equal(6114u, actor.CharId, "Only QAEnemy performs the recipient Withdraw check");
        actor.SendRpc("44 01");
    }

    internal static object[] Records(HeadlessPlayer actor) =>
        actor.World.RpcSnapshot().Where(frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record is
                { SourceId: Burst or TempMods.EnrageAbility or TempMods.ControlImmunityTimerAbility } ||
            frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.StateEnd(TempMods.EnrageAbility, actor.Handle)))
            .Select(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record)
                ? new { record!.SourceId, record.TargetId, record.RecordId, record.Duration, record.AddRemoveType, Wire = frame.Hex }
                : (object)new { StateEnd = frame.Hex }).ToArray();
}
