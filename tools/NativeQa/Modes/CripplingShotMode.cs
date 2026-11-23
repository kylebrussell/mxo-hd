using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class CripplingShotMode : NativeQaMode
{
    private const ushort Shot = 450, Rifles = CombatStyles.Rifles;
    private const int Level = 42, X = 17000, Y = 495, Z = 6400, EnemyZ = 6800, FarZ = Z + 2001;
    private const string StockSpeed = "80b90c00000000000000000000000000000000000000000000000000000015000000071c086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Rifles, Shot);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 12, 17, 154);
    private static readonly (string Player, uint Id, int X, int Z, byte Org)[] Stands =
    {
        ("QAEnemy", 6114, X, EnemyZ, 2),
        ("QAFriend", 6111, X - 200, EnemyZ, 1),
        ("QAFar", 6112, X, FarZ, 2)
    };

    public override string Name => "ability-parity-crippling-shot";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=0," +
                "healthC=5000,healthM=5000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6111,6112,6114)",
            $"UPDATE characters SET x={X},z={Z} WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},z={stand.Z},alignment={stand.Org} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Rifles, Shot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "450", ["NativeCloseableRows"] = 1 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, full prerequisites with rifle 692 in hand. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}). QAFriend ({X - 200},{Y},{EnemyZ}) is allied; QAFar ({X},{Y},{FarZ}) is outside 20 m. Stay on the stand. Page 2 Rifles / Crippling Shot.",
            "At rest, select Rifles. Try slot 2 on QAFriend and QAFar; expect local suppression or server reason 24/34 and no charge. Select /target QAEnemy. Cast slot 2 and save the first complete stock 450 request. Expect 2 s, 45 IS, 10 s own/5 s family 690, 58 Ballistic from stored 49 plus +20 influence. [MODIFIER] source450 115/877/878 each -75 for 8 s, target reduction notice, own MovementScale -0.75, then three matching removals and scale 0. No other helper loses health. The optional records action confirms the recipient-side signed movement and modifier IDs.",
            """After reuse, use {"actor":"QAEnemy","action":"crippling-speed-and-armor"}. It uses the retained stock Soldier Upgrade and Hyper-Speed. Expect source1061 181 +20 and baseline MovementScale +0.45. Aim at the same QAEnemy and cast slot 2. Expect [TYPED-RESISTANCE OURS] 450 type1 total20 raw58 final54, the same three -75 records, movement -0.30 during them, and exactly +0.45 after expiry. Take {"actor":"QAEnemy","action":"crippling-records"} after 8 s to verify both signed updates and matching removals."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var server = context.ActualServer!;
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified clear outdoor Crippling Shot stand ({x},{Y},{z})");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Player, stand.Player, stockCharacterLoad: true);
            context.Helpers.Add(stand.Player, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            var self = helper.World.CreationSnapshot().Last(view => view.ViewId == 2);
            Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { stand.X, Y, stand.Z }), $"Entered on verified {stand.Player} stand");
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var enemy = context.Helpers["QAEnemy"];
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        var caster = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(caster).SequenceEqual(new double[] { X, Y, Z }), "CSR enters on the intended verified stand");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Player);
            Check.Equal(stand.Id, BitConverter.ToUInt32(view.Attributes["CharacterID"]), "Canonical helper identity");
            Check.Equal(stand.Org, view.Attributes["OrganizationID"].Single(), "Helper disposition");
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }), "Exact range witness position");
        }
        var selected = await actor.WaitForViewNamedAsync(enemy.Handle);
        actor.SendRpc(WithHandle(DisablingShotMode.StockRifles, DuelPackets.Handle(selected.ViewId, selected.SpawnId)));
        foreach (var (source, stat, value) in new (ushort, ushort, int)[] { (Rifles, 171, 11), (125, 1139, 4), (252, 1139, 5) })
            await ObserveAsync(actor, 0, frame => Bonus(frame, source, stat, value, 0, false), $"Current Ballistic source {source}: +{value}");
        await SpeedAndArmorAsync(enemy);
        Check.Equal((ushort)54, TypedAbilityResistance.Amount(58, Level, Level, 20), "Same-target typed armor expectation");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Stock rifle and helper framing");
        Console.WriteLine("CRIPPLING SHOT BOOTSTRAP PASS: clear stands, stock Rifles/armor, and real recipient Hyper-Speed +0.45. The first 450 request comes from native QA.");
        return Array.Empty<byte>();
    }

    internal static async Task SpeedAndArmorAsync(HeadlessPlayer enemy)
    {
        await PistolSlideMode.ApplyArmorAsync(enemy);
        int afterArmor = enemy.World.ViewSnapshot().Count;
        enemy.SendRpc(StockSpeed);
        await EvidenceRetestWire.ViewSinceAsync(enemy, afterArmor, 2, "02 88 05 8c 1e 66 66 e6 3e");
    }

    internal static object Records(HeadlessPlayer actor)
    {
        Check.Equal(6114u, actor.CharId, "Use the selected QAEnemy recipient for this witness");
        var records = actor.World.RpcSnapshot().Where(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record) &&
            record is { SourceId: Shot }).Select(frame => frame.Hex).ToArray();
        var movements = new[] { -.75f, -.30f, 0f, .45f }.Select(scale => (Scale: scale,
            Pattern: BytePattern.Parse(WireFormat.ToHex(PlayerViewAttributes.BuildSelf(
                (PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(scale))))))).ToArray();
        var views = actor.World.ViewSnapshot().SelectMany(view => movements.Where(movement => movement.Pattern.Occurs(view))
            .Select(movement => movement.Scale)).ToArray();
        return new { Records = records, MovementScale = views };
    }
}
