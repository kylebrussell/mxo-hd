using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class RifleButtSmashMode : NativeQaMode
{
    private const ushort Shot = CombatSpecials.RifleButtSmash, Rifles = CombatStyles.Rifles;
    private const int Level = 39, X = 17000, Y = 495, Z = 6400, EnemyZ = 6600;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Rifles, Shot);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";

    public override string Name => "ability-parity-rifle-butt-smash";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0," +
                "healthC=5000,healthM=5000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6114)",
            $"UPDATE characters SET z={EnemyZ},rotation=128 WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Shot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [CombatFixtureWeapons.RifleEscapeWinMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "453", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} legal memory, rifle 692 in hand. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}). Stay on the stands. Open page 2, Rifles / Rifle Butt Smash.",
            "Select Rifles and /target QAEnemy, then try slot 2 at rest. Expect local suppression or reason24 with no cost. Use QAEnemy's right-click Close Combat (double-click starts Free Fire), wait for round 0 and reselect Rifles. Use slot 2 and retain the first complete stock 453 request. Expect no cast bar, 40 IS, 60 s own and 5 s group689. Do not use ordinary Withdraw during the 453 attempt.",
            "For the win retest, after selecting Rifles and with QAEnemy still in interlock, type ?cd rb before slot 2. Expect one [RIFLE ESCAPE QA] 453 win consumed line. Then expect 0x100, Rifle RifleButt 11356, 7 Ballistic, and [PVP] CSR_Tester withdraws. Strafe after teardown to check normal movement. The prior native run already covered unassisted losses and a tie.",
            """After a success and own reuse, use {"actor":"QAEnemy","action":"rifle-butt-armor"} for source1061 stat181 +20. If needed after a failed roll, end that interlock with ?cd stop before the next stage. Stay on the same pair. Repeat with right-click Close Combat, slot 1, ?cd rb and then slot 2. Expect [TYPED-RESISTANCE OURS] 453 type1 total20 raw7 final6, the same 11356, and withdrawal when the roll wins. No Confuse modifier."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { Z, EnemyZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, X, Y, z), $"Verified outdoor stand ({X},{Y},{z})");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        CheckStand(enemy, EnemyZ);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        CheckStand(caster, Z);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendChat("?cd rb");
        await caster.WaitForTextAsync("Rifle Butt Smash win aid requires the active interlock with QAEnemy.");
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint target = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(target));
        int roundAt = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(StockSelfDefense, target), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, roundAt, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
            "Retained stock Close Combat opens the original opponent");
        caster.SendRpc(WithHandle(DisablingShotMode.StockRifles, target));
        foreach (var (source, stat, value) in new (ushort, ushort, int)[] { (Rifles, 171, 11), (125, 1139, 3), (252, 1139, 4) })
            await ObserveAsync(caster, 0, frame => Bonus(frame, source, stat, value, 0, false), $"Authored Ballistic influence {source}: +{value}");
        caster.SendChat("?cd rb");
        await caster.WaitForTextAsync("Next Rifle Butt Smash against QAEnemy is armed for one winning withdrawal roll.");
        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended");
        await PistolSlideMode.ApplyArmorAsync(enemy);
        Check.Equal((ushort)6, TypedAbilityResistance.Amount(7, Level, Level, 20), "Same-target rifle armor expectation");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Stock supporting RPC framing");
        Console.WriteLine("RIFLE BUTT SMASH BOOTSTRAP PASS: loaded rifle and clear native stands, retained Close Combat/Rifles/armor. The first 453 request stays native.");
        return Array.Empty<byte>();
    }

    private static void CheckStand(HeadlessPlayer player, int z)
    {
        var self = player.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { X, Y, z }), $"Actual {player.Handle} stand");
    }
}
