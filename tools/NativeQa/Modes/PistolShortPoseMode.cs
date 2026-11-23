using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class PistolShortPoseMode : NativeQaMode
{
    private const ushort Evasive = CombatSpecials.PistolEvasive, Barrage = 244;
    private const ushort Handguns = CombatStyles.Handguns, Soldier = 154;
    private const int Level = 27, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150, DualPistolSlot = 14;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Evasive, Barrage);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Soldier);

    public override string Name => "ability-parity-pistol-short-pose";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(), new[]
    {
        CombatFixtureLoadout.SeedWeapons,
        $"DELETE FROM inventory WHERE charId=110 AND slot={DualPistolSlot}; " +
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{CombatFixtureWeapons.DualHandguns},{DualPistolSlot},1,0)"
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
    }, DevCommands: true, hotbarAbilities: new[] { Handguns, Evasive, Barrage },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.WeaponMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "242,244", ["NativeCloseableRows"] = 2, ["NativeCloseableIds"] = "242,244" },
        readyMessages: new[]
        {
            $"Level {Level}, CSR {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory and QAEnemy {EnemyLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. Clear outdoor NAV CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). The single handgun 2565 starts in hand. The stock Close Combat radial may reselect Self Defense. Recover the target with /target QAEnemy, strafe once if needed, and use page 2 slot 1 Handguns after opening each fight.",
            "Single pistol. Right-click Close Combat on QAEnemy, reselect Handguns, then use slot 2 Pistol Evasive after the opening round. Expect a 2 s cast, 30 IS, 10 s own and 5 s group 689. A landed special has +10 tactic, raw5, and animation 9530 with the 140-unit sent pose. Expect [MODIFIER] source=242 only on char110, stats 1009/1010/1011 +20 for 12 s, each removed. Retry after reuse if the first attempt misses.",
            """After the three defenses retire, use ?cd stop and ?cd w dual-handgun. Wait for held dual handguns 2496. Apply QAEnemy armor with {"actor":"QAEnemy","action":"pistol-pose-armor"}. Open Close Combat again from the same stands, reselect Handguns and use slot 3 Pistol Barrage. Expect the same cast/debit/timers, +15 tactic, animation 9534 with the same 140-unit pose, and [TYPED-RESISTANCE OURS] 244 type1 total20 raw19 final17. If Powerless procs naturally, record 244/96, linked 96/0 and independent 734/681, with matched removal at 8 s.""",
            "For either move, a normal queued miss should play the ordinary style exchange with no special damage and no new ability-specific modifiers. Save one miss if it occurs; ordinary contests remain random. PR910 and PR865 already verified ordinary effects, direct casts and medium-pose animations."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var start = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Same-floor and clear sight for both pistol stages");
        Console.WriteLine($"Pistol return stand {start.Command}");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckStand(enemy, EnemyX);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Handgun);
        DisablingShotMode.CheckStand(caster, CasterX);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint targetHandle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(targetHandle));
        await OpenAsync();
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, context.OwnedDatabase!, "dual-handgun", CombatFixtureWeapons.DualHandguns);
        await PistolSlideMode.ApplyArmorAsync(enemy);
        await OpenAsync();
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Both pistol fixture stages use clean stock framing");
        Console.WriteLine("PISTOL SHORT POSE BOOTSTRAP PASS: stock Close Combat and Handguns in the single and dual-pistol stages, verified swap and armor.");
        return Array.Empty<byte>();

        async Task OpenAsync()
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.World.SendRpcs(WithHandle(PistolSlideMode.StockSelfDefense, targetHandle), WireFormat.FromHex($"40 {handle} 01 6b"));
            await ObserveAsync(caster, cursor, rpc => EnrageNpcCloseCombatFixture.CombatFlag(rpc, target, 1),
                "Stock Close Combat opens on the original player");
            cursor = caster.World.RpcSnapshot().Count;
            caster.SendRpc(WithHandle(PistolSlideMode.StockHandguns, targetHandle));
            await ObserveAsync(caster, cursor, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
                record is { SourceId: Handguns, TargetId: 171, AddRemoveType: 0 } && record.SignedValue > 0,
                "Stock 129 activates Handguns on the current weapon");
        }
    }
}
