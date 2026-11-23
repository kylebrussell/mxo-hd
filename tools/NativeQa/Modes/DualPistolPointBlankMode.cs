using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DualPistolPointBlankMode : NativeQaMode
{
    private const ushort Shot = CombatSpecials.DualPistolPointBlank, Handguns = CombatStyles.Handguns;
    private const int Level = 42, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private const int Neutral = 115, Resisted = 108;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Shot);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);

    public override string Name => "ability-parity-dual-pistol-point-blank";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.DualHandguns),
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES " +
                $"(110,{CombatFixtureWeapons.DualHandguns},{CombatFixtureWeapons.HandSlot},1,0)"
        }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z}," +
                "healthC=5000,healthM=5000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CasterX},rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Shot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_CONDITIONAL_THROW_QA"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "499", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "499" },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} legal memory, dual handguns 2496 in hand. Verified clear outdoor NAV CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). Page 2 is Handguns / Dual Pistol Point Blank. Keep this flat stand pair.",
            "Select Handguns and /target QAEnemy. Try slot 2 at rest; expect local suppression or reason 24 with no debit. Right-click QAEnemy and use Close Combat. Wait for round 0, then reselect Handguns on slot 1 because the radial can select Self Defense. Before Off Balance, try slot 2. Expect local state refusal or reason 22 with mask 4, no cost or reuse.",
            "Select QAEnemy, type ?cd offbalance 30 and confirm 885/200. Press slot 2 and save the first complete stock 499 request. Expect an instant queue, [SPECIAL-TACTIC OURS] ability=499 bonus=29, 30 IS and 10 s own / 5 s group690 reuse. On an ordinary lost roll, collect the regular exchange with no 499 damage or effect and retry after reuse. A win at the 140-unit sent pose must play 9533 and hit for 115. Expect the victim's Powerless source499/stat96 for 6.48 s, linked 96/0 and separate 734/681 immunity for six seconds, with matching removals. The caster and target each receive their own Powerless line.",
            """After Powerless clears, use ?cd stop and wait for the 10 s own reuse. Send {"actor":"QAEnemy","action":"point-blank-armor"}; expect source1061 stat181 +20. Use the same stand and right-click Close Combat, reselect Handguns and ?cd offbalance 30 again. Slot 2 must produce [TYPED-RESISTANCE OURS] 499 type1 total20 raw115 final108, the same 9533 and a new set of Powerless adds/removals."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stand = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Dual Pistol Point Blank has clear outdoor sight");
        Console.WriteLine($"CSR {stand.Command}");

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
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.DualHandguns);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var opponent = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint target = DuelPackets.Handle(opponent.ViewId, opponent.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(target));
        int cursor = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(PistolSlideMode.StockSelfDefense, target), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, cursor, rpc => EnrageNpcCloseCombatFixture.CombatFlag(rpc, opponent, 1),
            "Retained Close Combat opens on the original player");
        caster.SendRpc(WithHandle(PistolSlideMode.StockHandguns, target));
        foreach (var (source, stat, value) in new (ushort, ushort, int)[] { (Handguns, 171, 11), (125, 1139, 4), (239, 1139, 2), (240, 1139, 1), (498, 1042, 8) })
            await ObserveAsync(caster, 0, packet => Bonus(packet, source, stat, value, 0, false), $"Authored source {source} stat {stat} +{value}");
        caster.SendRpc($"81 51 {handle}");
        caster.SendChat("?cd offbalance 30");
        await ObserveAsync(enemy, 0, packet => Bonus(packet, 885, 200, 0, 30, false), "Selected player receives authored Off Balance");
        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended");
        await PistolSlideMode.ApplyArmorAsync(enemy);

        Check.Equal((ushort)Resisted, TypedAbilityResistance.Amount(Neutral, Level, Level, 20), "Same target typed mitigation");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Retained stock supporting frames decode");
        Console.WriteLine("DUAL PISTOL POINT BLANK BOOTSTRAP PASS: legal dual handguns, stock Close Combat/Handguns, selected Off Balance and stock armor. First 499 remains native.");
        return Array.Empty<byte>();
    }
}
