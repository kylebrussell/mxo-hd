using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DeadlyShotMode : NativeQaMode
{
    private const ushort Shot = CombatSpecials.DeadlyShot, Rifles = CombatStyles.Rifles;
    private const int Level = 46, PickX = 21550, EnemyX = 21750, BlockedX = 22350, FarX = 19600, Y = 1095, Z = 16150;
    private const int Neutral = 73, Resisted = 69;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Rifles, Shot);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);

    public override string Name => "ability-parity-deadly-shot";
    public override NativeQaModePlan Plan { get; } = new(Loadout, PickX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "healthC=5000,healthM=5000,innerStrC=700,innerStrM=700 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={PickX},z={Z},rotation={MobAggro.YawTowards(PickX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},z={Z},rotation={MobAggro.YawTowards(EnemyX, Z, PickX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Shot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [StarterInterlockControlQa.DeadlyShotMode] = "1", ["MXO_RANGED_CONTEST_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "454", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} legal memory and rifle 692. CSR spawns at verified outdoor PICK ({PickX},{Y},{Z}) facing QAEnemy ({EnemyX},{Y},{Z}); no ?tp needed for the main casts. QAEnemy is a player. Use /target QAEnemy even if the camera faces away, and strafe once if the view needs refreshing. No NPC selection in this mode. Page 2 Rifles / Deadly Shot.",
            "Select Rifles and /target QAEnemy. For the admission control only, use the printed FAR and BLOCK ?tp stands and try slot 2. Expect local suppression or reasons 34/9, with no charge, cast or reuse. Return using the printed PICK ?tp before the positive cast.",
            $"Type ?rangedcontest hit first (the at-rest 454 now rolls the ranged contest 524 vs 1010). With QAEnemy selected at PICK and CSR out of combat, press slot 2. Save the first complete 80 b9 c6 01 request. Expect 45 IS (700 to 655), 2 s cast, 10 s own and 5 s family 689, {Neutral} Ballistic to QAEnemy, and no PvP round. Record the visible at-rest cast and release.",
            "This disposable mode forces only the shipped 10% Stun marker on 454 hitting QAEnemy. Look for the short 454 marker, 454/102 Stun for four seconds, linked 102/163 -20 and independent 734/681 for four seconds. Verify matching removals, no duplicate state, and QAEnemy recovers after expiry.",
            $$"""After reuse, send {"actor":"QAEnemy","action":"deadly-shot-armor"}. Verify source1061 stat181 +20. Stay at PICK and out of combat, select QAEnemy and repeat slot 2 for [TYPED-RESISTANCE OURS] 454 type1 total20 raw{{Neutral}} final{{Resisted}} and the same Stun teardown. Report the visible cast; archived ILDB 11359/11365/11366 do not identify a profile-specific interlock selector."""
            ,"Deflection: type ?rangedcontest deflect, wait out the 10 s reuse, cast slot 2 at QAEnemy. The rifle Deadly Shot motion still plays, QAEnemy takes no damage, stun or immunity, CSR sees 'QAEnemy deflected Deadly Shot.' and QAEnemy's log shows the deflection line. Then ?rangedcontest off."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var pick = Stand(PickX, Z);
        var blocked = Stand(BlockedX, Z);
        var far = Stand(FarX, Z);
        _ = Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, EnemyX, Y, Z) &&
            NavMesh.HasSightPath(1, FarX, Y, Z, EnemyX, Y, Z) && Math.Abs(FarX - EnemyX) > 2000 &&
            !NavMesh.HasSightPath(1, BlockedX, Y, Z, EnemyX, Y, Z), "Deadly Shot near, far and blocked outdoor paths");
        Console.WriteLine($"PICK {pick.Command}");
        Console.WriteLine($"BLOCK {blocked.Command} (6 m, blocked)");
        Console.WriteLine($"FAR {far.Command} (21.5 m, clear)");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckStand(enemy, EnemyX);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        DisablingShotMode.CheckStand(caster, PickX);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var selected = await caster.WaitForViewNamedAsync(enemy.Handle);
        caster.SendRpc(WithHandle(DisablingShotMode.StockRifles, DuelPackets.Handle(selected.ViewId, selected.SpawnId)));
        foreach (var (source, stat, value) in new (ushort, ushort, int)[] { (Rifles, 171, 12), (125, 1139, 4), (252, 1139, 5) })
            await ObserveAsync(caster, 0, frame => Bonus(frame, source, stat, value, 0, false), $"Authored Ballistic influence {source}: +{value}");
        await PistolSlideMode.ApplyArmorAsync(enemy);
        Check.Equal((ushort)Resisted, TypedAbilityResistance.Amount(Neutral, Level, Level, 20), "Same-target Ballistic typed control");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Stock supporting style and armor framing");
        Console.WriteLine("DEADLY SHOT BOOTSTRAP PASS: clear, far and blocked stands, held rifle, retained Rifles and Soldier Upgrade. First 454 request stays native.");
        return Array.Empty<byte>();
    }
}
