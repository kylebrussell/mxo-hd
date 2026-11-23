using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DualPistolDashMode : NativeQaMode
{
    private const ushort Dash = CombatSpecials.DualPistolDash, Handguns = CombatStyles.Handguns;
    private const int Level = 36, CasterX = 21550, EnemyX = 21750, FarX = 19600, Y = 1095, Z = 16150;
    private const int Neutral = 14, Resisted = 13;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Dash);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);
    private static readonly (string Name, uint Id, int X, int Z)[] Targets =
    {
        ("QAEnemy", 6114, EnemyX, Z),
        ("QAFriend", 6111, CasterX, Z + 200),
        ("QAFar", 6112, FarX, Z)
    };

    public override string Name => "ability-parity-dual-pistol-dash";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.DualHandguns),
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES " +
                $"(110,{CombatFixtureWeapons.DualHandguns},{CombatFixtureWeapons.HandSlot},1,0)"
        },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "healthC=5000,healthM=5000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6111,6112,6114)",
            $"UPDATE characters SET x={CasterX},z={Z},rotation={MobAggro.YawTowards(CasterX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},z={Z},rotation={MobAggro.YawTowards(EnemyX, Z, CasterX, Z)} WHERE charId=6114",
            $"UPDATE characters SET x={CasterX},z={Z + 200} WHERE charId=6111",
            $"UPDATE characters SET x={FarX},z={Z},alignment=2 WHERE charId=6112",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Dash }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "500", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "500" },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} legal memory, dual handguns 2496 in hand. Verified outdoor NAV CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}), with QAFriend ({CasterX},{Y},{Z + 200}) and QAFar ({FarX},{Y},{Z}) at 19.5 m with clear sight. Page 2 Handguns / Dual Pistol Dash.",
            "Stay out of combat. Select Handguns. Try slot 2 on the ally and on QAFar. Expect stock suppression or reason 24/34 with no charge. Select QAEnemy and use slot 2. Save the first complete stock 500. Expect an instant hit, 35 IS (900 to 865), 20 s own/5 s group690, 14 Ballistic, no interlock rounds and no other recipient. Record what the stock client renders on open ground.",
            """QAEnemy receives 500/93 for eight seconds and its linked 93/0, with restriction masks 1/21/2021 hex on application and 2020/2000/0 on expiry. Use {"actor":"QAEnemy","action":"pistol-dash-records"} after eight seconds to get its mask and source-record sequence. The matching removals release the Enrage restrictions and produce one state-end line.""",
            """After own reuse, apply {"actor":"QAEnemy","action":"pistol-dash-armor"}. Remain out of combat and aim at the same QAEnemy. Cast again for [TYPED-RESISTANCE OURS] 500 type1 total20 raw14 final13. Verify the second eight-second Enrage and the same restriction sequence. The named ILDB 9524/9531 execution selector is outside this direct-route check."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var server = context.ActualServer!;
        foreach (var (x, z) in Targets.Select(target => (target.X, target.Z)).Append((CasterX, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, CasterX, Y, Z, x, Y, z), $"Verified Dash stand ({x},{Y},{z})");
        foreach (var target in Targets)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, target.Name, target.Name, stockCharacterLoad: true);
            context.Helpers.Add(target.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            CheckStand(helper, target.X, target.Z);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var enemy = context.Helpers["QAEnemy"];
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        CheckStand(caster, CasterX, Z);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.DualHandguns);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var target in Targets)
        {
            var view = await caster.WaitForViewNamedAsync(target.Name);
            Check.Equal(target.Id, BitConverter.ToUInt32(view.Attributes["CharacterID"]), "Authentic helper view");
        }
        var enemyView = await caster.WaitForViewNamedAsync(enemy.Handle);
        caster.SendRpc(WithHandle(PistolSlideMode.StockHandguns, DuelPackets.Handle(enemyView.ViewId, enemyView.SpawnId)));
        foreach (var (source, stat, value) in new (ushort, ushort, int)[] { (Handguns, 171, 10), (125, 1139, 3), (239, 1139, 1), (240, 1139, 1) })
            await ObserveAsync(caster, 0, packet => Bonus(packet, source, stat, value, 0, false), $"Authored source {source} stat {stat} +{value}");
        await PistolSlideMode.ApplyArmorAsync(enemy);
        Check.Equal((ushort)Resisted, TypedAbilityResistance.Amount(Neutral, Level, Level, 20), "Same-target mitigation");
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Retained support frame decoding");
        Console.WriteLine("DUAL PISTOL DASH BOOTSTRAP PASS: exact outdoor stands, actual dual handguns and retained Handguns/armor; first 500 comes from native.");
        return Array.Empty<byte>();
    }

    internal static object Records(HeadlessPlayer helper)
    {
        Check.Equal(6114u, helper.CharId, "Use the selected QAEnemy recipient for this witness");
        var frames = helper.World.RpcSnapshot();
        var masks = frames.Where(rpc => rpc.Bytes is [0x81, 0x6e, ..] && rpc.Bytes.Length == 11)
            .Select(rpc => $"0x{BitConverter.ToUInt32(rpc.Bytes, 2):x}").ToArray();
        var modifiers = frames.Where(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record is { SourceId: Dash or 93 })
            .Select(rpc => rpc.Hex).ToArray();
        return new { Masks = masks, Records = modifiers };
    }

    private static void CheckStand(HeadlessPlayer player, int x, int z)
    {
        var self = player.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { x, Y, z }), $"Actual {player.Handle} stand");
    }
}
