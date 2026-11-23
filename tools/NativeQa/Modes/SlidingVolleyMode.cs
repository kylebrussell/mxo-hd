using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class SlidingVolleyMode : NativeQaMode
{
    private const ushort Volley = 249, Style = CombatStyles.SubmachineGuns, Soldier = 154;
    private const int Level = 22, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Style, Volley);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Soldier);
    // Retained PR330 stance requests. The first full Sliding Volley request comes from this stock run.
    private const string StockStyle = "80b99e000400020000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";

    public override string Name => "ability-parity-sliding-volley";
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
        }, DevCommands: true, hotbarAbilities: new[] { Style, Volley },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "249", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}; CSR {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory and QAEnemy {EnemyLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. Dual SMGs (2503) are already in hand. Outdoor NAV: CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). Strafe once after login if needed. Page 2 slot 1 is Submachine Guns, slot 2 Sliding Volley. Use the target's radial Close Combat to avoid Free Fire, wait for the opening round, then reselect slot 1.",
            "With QAEnemy selected, try slot 2 at rest, then during interlock before Daze. Expect a stock local refusal or a server refusal with zero debit/reuse. The live interlock's Daze refusal carries reason 22, required-state mask 2. If Speed procs Daze on its own before the second control, wait for the Dazed state to clear.",
            "Keep QAEnemy selected and type ?cd daze 30. Daze fixture applied 884/199 to QAEnemy and state199 enables slot 2. Press slot 2 once and save the first complete stock 80 b9 f9 00. Expect [ABILITY-COST] 249 paid=15, 10 s own/5 s group 689 reuse and [SPECIAL-TACTIC OURS] ability=249 bonus=24. A landed special uses 21081 and does 85 Ballistic damage. This is the shipped base 77 with +11 live Ballistic influence. On a queued miss, retain the ordinary [PVP] round and [COMBAT-VIEW] exchange with no 21081, special damage, or 249 state. Ordinary counter damage can still occur. Retry after reuse.",
            """Type ?cd stop to leave without Escape. Send {"actor":"QAEnemy","action":"volley-armor"} and check source1061 stat181 +20. Reopen the same target's radial Close Combat, reselect 158, and use ?cd daze 30 when Daze is absent. After reuse, land slot 2 again. Expect the same 21081 with [TYPED-RESISTANCE OURS] 249 type1 total20 raw85 final76.""",
            """Keep the ordinary 20% proc. Repeat eligible landed hits after reuse if needed. Enraged adds 249 -> 93 and linked 93 -> 0 for 10 s, with no new control immunity. Send {"actor":"QAEnemy","action":"volley-withdraw"} before and after expiry. Only the latter can start [PVP] QAEnemy is withdrawing. The {"actor":"QAEnemy","action":"volley-records"} response saves the paired record IDs and one Enraged state-end. Capture a queued ordinary miss in the same run if it has not already occurred."""
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
            "Stock Close Combat opens PvP against the selected stand");
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(WithHandle(StockStyle, target));
        await ObserveAsync(caster, cursor, rpc => Bonus(rpc, Style, 171, 7, 0, false), "Stock 158 adds +7 Ballistic Damage");
        await ObserveAsync(caster, 0, rpc => Bonus(rpc, 125, 1139, 2, 0, false), "Loaded Firearms Expert adds +2 Ballistic Damage Bonus");
        await ObserveAsync(caster, 0, rpc => Bonus(rpc, 246, 1139, 2, 0, false), "Loaded Dual SMGs adds +2 Ballistic Damage Bonus");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Sliding Volley exact support framing");
        Console.WriteLine("SLIDING VOLLEY BOOTSTRAP PASS: both verified stands, legal dual-SMG loadout, stock Close Combat and 158, +11 Ballistic influence.");
        return Array.Empty<byte>();
    }

    internal static void SendWithdraw(HeadlessPlayer actor)
    {
        Check.Equal(6114u, actor.CharId, "Only QAEnemy performs the recipient Withdraw check");
        actor.SendRpc("44 01");
    }

    internal static object[] Records(HeadlessPlayer actor) => actor.World.RpcSnapshot().Where(frame =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record is
            { SourceId: Volley or TempMods.EnrageAbility or TempMods.ControlImmunityTimerAbility } ||
        frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.StateEnd(TempMods.EnrageAbility, actor.Handle)))
        .Select(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record)
            ? new { record!.SourceId, record.TargetId, record.RecordId, record.Duration, record.AddRemoveType, Wire = frame.Hex }
            : (object)new { StateEnd = frame.Hex }).ToArray();
}
