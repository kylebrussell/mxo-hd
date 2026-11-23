using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class PistolEvasiveMode : NativeQaMode
{
    private const ushort Evasive = 242, Handguns = CombatStyles.Handguns, Soldier = 154;
    private const int Level = 22, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Evasive);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Soldier);

    public override string Name => "ability-parity-pistol-evasive";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Handgun) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z},healthC=15000,healthM=15000,innerStrC=800,innerStrM=800 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CasterX},rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Evasive },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "242", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory and QAEnemy {EnemyLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. The handgun 2565 starts in hand. Outdoor NAV CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). Strafe once after login if needed. Page 2 slot 1 selects Handguns and slot 2 uses Pistol Evasive.",
            "Select Handguns at rest, select QAEnemy and press slot 2. Keep the complete first stock 80 b9 f2 00. Expect 30 IS, a 2 s cast, 10 s own and 5 s family 689 reuse, and a direct Ballistic hit for 5. Expect [MODIFIER] char=110 source=242 stats1009/1010/1011 each +20 for 12 s, followed by each removal. Compare before/active/after defense totals. The target gains none of the three modifiers.",
            "After all three defense records retire, use QAEnemy's radial Close Combat and reselect 129 after the opening round. Press slot 2. Expect a 2 s cast before the next-move queue, +10 tactic, 9530 at the ordinary 140-unit pose, and the same five-point hit and owner defense stack. A queued miss uses the ordinary style exchange and leaves the defense records unchanged. Retry a missed hit after reuse.",
            """After the defense expiry, leave with ?cd stop. Send {"actor":"QAEnemy","action":"evasive-armor"} for [MODIFIER] char=6114 source=1061 stat181 +20. Repeat the at-rest cast on the same QAEnemy. Expect [TYPED-RESISTANCE OURS] 242 type1 total20 raw5 final4, with the same three +20 caster defenses and their normal 12 s removals."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var casterStand = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Verified clear outdoor handgun sight");
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
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Handgun);
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
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Pistol Evasive exact support framing");
        Console.WriteLine("PISTOL EVASIVE BOOTSTRAP PASS: both verified stands, legal handgun loadout, stock Close Combat and 129.");
        return Array.Empty<byte>();
    }
}
