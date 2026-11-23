using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class ThreeRoundBurstMode : NativeQaMode
{
    private const ushort Burst = 256, Rifles = CombatStyles.Rifles, Soldier = 154;
    private const int Level = 27, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Rifles, Burst);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Soldier);
    // Retained PR330 Self Defense stance. The first full 256 request comes from this stock run.
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";

    public override string Name => "ability-parity-three-round-burst";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z},healthC=15000,healthM=15000,innerStrC=850,innerStrM=850 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CasterX},rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Burst },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "256", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory and QAEnemy {EnemyLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. The rifle (692) is already in hand. Outdoor NAV: CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). Strafe once if needed. Use page 2 slot 1 Rifles and slot 2 Three Round Burst. Use radial Close Combat for interlock. The target double-click starts Free Fire.",
            "Select QAEnemy on the clear stand. Try slot 2 before Stagger. A stock local gate or reason 22 with state mask 8 must leave IS/reuse unchanged. Enter radial Close Combat, wait for the opening round and reselect 147. Keep QAEnemy selected and use ?cd stagger 30 for player state 883/201. Press slot 2 and retain the first complete stock 80 b9 00 01. Expect 20 IS, 4 s cast (no queue before expiry), 10 s own and 5 s group 689. The next eligible round uses +21 tactic. On a hit expect 11367 for 90 Ballistic. On a queued miss preserve the ordinary exchange with zero 256 damage or Stun. Any ordinary counter damage belongs to that exchange.",
            """The 0.01 s terminal marker has a normal 40% landed roll. Repeat the neutral hit after reuse until a Stun procs. Expect 256/102 Stun for four seconds, linked 102/0 and 102/163 Combat Tactics -20 for the Stun's lifetime, and independent 734/681 immunity for four seconds. Save {"actor":"QAEnemy","action":"three-round-records"} after expiry for each original add/remove ID, including 102/163, and one state-end. To probe Stun, send {"actor":"QAEnemy","action":"three-round-withdraw"} immediately after a neutral Stun appears and once more after both original removals. During Stun the retained Withdraw cannot start. After release expect [PVP] QAEnemy is withdrawing.""",
            """Wait for Stun removal and use ?cd stop if the interlock is still active. Send {"actor":"QAEnemy","action":"three-round-armor"} for source1061 stat181 +20. Reopen the same target's radial Close Combat and reselect 147. Use ?cd stagger 30 when the state is absent. Expect 11367 with [TYPED-RESISTANCE OURS] 256 type1 total20 raw90 final82.""",
            "After reuse use ?cd stop to leave without Escape. Keep QAEnemy selected and apply Stagger again when absent. Test slot 2 from rest. If stock sends it, expect the same 4 s cast and direct 82 under armor, with 20 IS and 10/5 reuse. The at-rest cast does not queue a special. Retain a local refusal instead if stock withholds the request."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var casterStand = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Verified clear outdoor rifle sight");
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
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint target = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(target));
        int cursor = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(StockSelfDefense, target), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, cursor, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
            "Stock Close Combat opens PvP");
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(WithHandle(DisablingShotMode.StockRifles, target));
        await ObserveAsync(caster, cursor, rpc => Bonus(rpc, Rifles, 171, 8, 0, false), "Stock 147 adds +8 Ballistic Damage");
        await ObserveAsync(caster, 0, rpc => Bonus(rpc, 125, 1139, 2, 0, false), "Loaded Firearms Expert adds +2 Ballistic Damage Bonus");
        await ObserveAsync(caster, 0, rpc => Bonus(rpc, 252, 1139, 3, 0, false), "Loaded Advanced Riflery adds +3 Ballistic Damage Bonus");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Three Round Burst exact support framing");
        Console.WriteLine("THREE ROUND BURST BOOTSTRAP PASS: both verified stands, legal rifle loadout, stock Close Combat and 147, +13 Ballistic influence.");
        return Array.Empty<byte>();
    }

    internal static void ProbeWithdraw(HeadlessPlayer actor)
    {
        Check.Equal(6114u, actor.CharId, "Only QAEnemy uses the Stun Withdraw probe");
        actor.SendRpc("44 01");
    }

    internal static object[] Records(HeadlessPlayer actor) => actor.World.RpcSnapshot().Where(frame =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record is
            { SourceId: Burst or TempMods.StunAbility or TempMods.ControlImmunityTimerAbility } ||
        frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.StateEnd(TempMods.StunAbility, actor.Handle)))
        .Select(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record)
            ? new { record!.SourceId, record.TargetId, record.RecordId, record.Duration, record.AddRemoveType, Wire = frame.Hex }
            : (object)new { StateEnd = frame.Hex }).ToArray();
}
