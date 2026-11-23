using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class AdvancedDualSmgConeMode : NativeQaMode
{
    private const ushort Style = CombatStyles.SubmachineGuns, Pinning = 492, Direct = 493;
    private const int Level = 42, X = 17000, Y = 495, Z = 6400, RadiusEdgeZ = Z + 800;
    private const uint Weapon = CombatFixtureWeapons.DualSmgs;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Style, Pinning, Direct);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);
    private static readonly (string Handle, uint CharId, int X, int Z, byte Org)[] Stands =
    {
        ("QAEnemy", 6114, X, Z + 300, 2),
        ("QAOther", 6113, X, RadiusEdgeZ, 2),
        ("QAFar", 6112, X, RadiusEdgeZ + 1, 2),
        ("QAFriend", 6111, X - 100, Z + 300, 1)
    };

    public override string Name => "ability-parity-advanced-dual-smg-cones";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(Weapon) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=0," +
                "healthC=5000,healthM=5000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6111,6112,6113,6114)",
            $"UPDATE characters SET x={X},z={Z},rotation={MobAggro.YawTowards(X, Z, X, Z + 300)} WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET x={stand.X},z={stand.Z},alignment={stand.Org} WHERE charId={stand.CharId}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Pinning, Direct }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatDepthDev.ConePositionMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "492,493", ["NativeCloseableRows"] = 2 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory and full prerequisites including dual SMGs. Page 2 Submachine Guns / Pinning Fire / Direct Fire. Outdoor CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{Z + 300}). QAOther ({X},{Y},{RadiusEdgeZ}) is exactly 8 m. QAFar is 1 unit farther. Allied QAFriend ({X - 100},{Y},{Z + 300}) is inside the cone. Do not move CSR.",
            "Select Submachine Guns. Try slot 3 with QAFriend and QAFar independently, then target QAEnemy. Expect local gate or reason 24/34, with no cost or timer. Activate Pinning Fire at rest. Save the full stock action. Expect 2 s, 45 IS, 10 s own/5 s family 690, 26 damage from Ballistic 21 with +24 influence on both hostile recipients, and no hit on ally/outside. Rooted 99 lasts 8 s. The linked state, Root target line, and own MovementScale -1 go to each enemy. Use the 492 movement check during and after release.",
            """Movement: during Root, send {"actor":"QAOther","action":"cone-move","x":17000,"y":495,"z":7220}. Expect returned z=7200. Repeat after the matching state removals and movement restoration. Expect z=7220. Move it back to 7200, keeping QAFar at 7201.""",
            "Activate Direct Fire at rest facing selected QAEnemy. Save the full stock action. Expect 2 s, 50 IS, 10 s own/5 s group 690, 43 damage from Ballistic 35 on both hostile recipients with no ally/outside damage. A natural 30% Enrage lasts 10 s. If neither hit procs, allow the ten-second reuse and try again (ordinary proc rolls). Check the matched state removal. No interlock or queued move.",
            """Then activate QAEnemy's stock armor using {"actor":"QAEnemy","action":"advanced-cone-armor"}. Expect Soldier Upgrade 1061 adding 181 +20. Retest both on the original target: [TYPED-RESISTANCE OURS] 492 type1 total20 raw26 final24, 493 type1 total20 raw43 final40. QAOther stays neutral (26/43); excluded helpers still take no damage. Capture [PVP] hits and [MODIFIER] controls. NPC 492 duration 14.08 s and NPC 493 damage records are independent old-source observations."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)).Append((X, RadiusEdgeZ + 20)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified clear outdoor stand ({x},{Y},{z})");
        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { X, Y, Z }),
            $"CSR enters at the exact outdoor stand ({X},{Y},{Z}) before any movement");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, Weapon);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Handle);
            Check.Equal(stand.CharId, BitConverter.ToUInt32(view.Attributes["CharacterID"]), "The correct recipient avatar is visible");
            Check.Equal(stand.Org, view.Attributes["OrganizationID"].Single(), "The recipient has the correct team disposition");
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }), "The recipient is at its exact stand");
        }
        var selected = await actor.WaitForViewNamedAsync("QAEnemy");
        actor.SendRpc(WithHandle(DualSmgConeOpenersMode.StockStyle, DuelPackets.Handle(selected.ViewId, selected.SpawnId)));
        foreach (var (source, stat, value) in new (ushort, ushort, int)[] { (Style, 171, 11), (125, 1139, 4), (246, 1139, 5), (491, 1139, 4) })
            await ObserveAsync(actor, 0, frame => Bonus(frame, source, stat, value, 0, false), $"Authored Ballistic influence {source}: +{value}");
        Check.Equal((ushort)24, TypedAbilityResistance.Amount(26, Level, Level, 20), "Pinning Fire resisted expectation");
        Check.Equal((ushort)40, TypedAbilityResistance.Amount(43, Level, Level, 20), "Direct Fire resisted expectation");
        await PistolSlideMode.ApplyArmorAsync(context.Helpers["QAEnemy"]);
        var witness = context.Helpers["QAOther"];
        Check.Equal((17000d, 495d, 7220d), await DualSmgConeOpenersMode.MoveAndReadPositionAsync(witness, X, Y, RadiusEdgeZ + 20),
            "The movement helper reads the exact free position before any native Root");
        Check.Equal((17000d, 495d, 7200d), await DualSmgConeOpenersMode.MoveAndReadPositionAsync(witness, X, Y, RadiusEdgeZ),
            "The movement helper returns to the 8 m boundary");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Stock controls and helper framing");
        Console.WriteLine("ADVANCED SMG BOOTSTRAP PASS: actual stock style, loaded armor, five clear stands and root witness movement. Both direct actions are captured in native QA.");
        return Array.Empty<byte>();
    }
}
