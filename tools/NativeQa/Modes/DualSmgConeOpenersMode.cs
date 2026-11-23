using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DualSmgConeOpenersMode : NativeQaMode
{
    private const ushort Style = 158, BulletSpray = 247, SuppressionFire = 248;
    private const int Level = 33, X = 17000, Y = 495, Z = 6400;
    private const uint Weapon = CombatFixtureWeapons.DualSmgs;
    // Full Submachine Guns style activation from the PR330 stock dual-SMG PacketLog.
    internal const string StockStyle =
        "80b99e00040002000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private static readonly ushort[] Loadout = Profile(2, 17, Style, BulletSpray, SuppressionFire).Order().ToArray();
    private static readonly (string Name, uint Id, int X, int Z, byte Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Z + 300, 2),
        ("QAOther", 6113, X, Z + 801, 2),
        ("QAFar", 6112, X + 291, Z + 700, 2),
        ("QAFriend", 6111, X - 100, Z + 300, 1)
    };

    public override string Name => "ability-parity-dual-smg-cone-openers";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(Weapon) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},healthC=3000,healthM=3000,innerStrC=700,innerStrM=700 WHERE charId IN (110,6111,6112,6113,6114)",
            $"UPDATE characters SET x={X},z={Z},rotation={MobAggro.YawTowards(X,Z,X,Z+300)} WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET x={stand.X},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, BulletSpray, SuppressionFire }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [CombatDepthDev.ConePositionMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "247,248", ["NativeCloseableRows"] = 2 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory and stocked dual SMGs in hand. Outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{Z+300}). Page 2 is Submachine Guns, Bullet Spray, Suppression Fire.",
            "Select Submachine Guns. Try slots 2 and 3 with an empty target, QAFriend and QAOther at 8.01 m. Expect local refusal or server 24/34, with no IS, cast or reuse. Use /target by helper name.",
            "Move QAOther to (17289,495,7100). QAFar is (17291,495,7100). Aim at QAEnemy. Stock Bullet Spray hits QAEnemy and QAOther inside the 22.5-degree half-angle and excludes QAFar and allied QAFriend. Expect [PVP] 28 damage from 25 Ballistic base and +16 influence (158 +9, 125 +3, 246 +4), 2 s cast, 30 IS, 10 s own and 5 s group 690. A natural 15% Enrage lasts 8 s. Save the full 247 request and both hit lines. A separate roll can occur on each hit.",
            "Wait for group reuse. Move QAOther to (17000,495,7200) and QAFar to (17000,495,7201). Aim at QAEnemy and cast stock Suppression Fire. Expect [PVP] 17 damage from 15 Ballistic base with the same +16 influence, 4 s cast, 35 IS, 10 s own and 5 s group 690, and paired Rooted 99 for 12 s with MovementScale -1 through release on QAEnemy and QAOther only. Save the first full 248 request. For QAOther, use action=cone-move with x=17000,y=495,z=7220 during the root. The response should report authoritative Z=7200. Repeat after removal and verify Z=7220. The archived longer duration comes from an NPC cast.",
            "Neither opener enters close combat or sends an interlock move. After reuse, start ordinary Close Combat on QAEnemy, reselect Submachine Guns, and try both slots to exercise the stock outside-combat gate. Helper move JSON uses actor, action=move, x/y/z. Try another Bullet Spray only after reuse if no Enrage procs; the headless proc test separately checks both sides of the 15% threshold."
        });

    internal static async Task<(double X, double Y, double Z)> MoveAndReadPositionAsync(
        HeadlessPlayer actor, float x, float y, float z)
    {
        if (actor.Handle != "QAOther") throw new InvalidOperationException("Use QAOther for the cone move witness.");
        var bytes = new List<byte>(WireFormat.FromHex("02 00 01 08"));
        foreach (float coordinate in new[] { x, y, z }) bytes.AddRange(BitConverter.GetBytes(coordinate));
        bytes.AddRange(new byte[] { 0, 0 });
        actor.World.SendViewList(bytes.ToArray());

        actor.SkipReceived();
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
        actor.SendChat("?combatdepth cone-position");
        var record = await actor.WaitForTextAsync("Cone position (", TimeSpan.FromSeconds(3));
        var result = Regex.Match(Encoding.Latin1.GetString(record.Bytes), @"Cone position \(([^,]+),([^,]+),([^)]+)\)");
        Check.That(result.Success, "Read the server's authoritative cone witness position");
        return (Coordinate(1), Coordinate(2), Coordinate(3));

        double Coordinate(int index) => double.Parse(result.Groups[index].Value, CultureInfo.InvariantCulture);
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var allStands = Stands.Select(stand => (stand.X, stand.Z)).Concat(new[] { (X, Z), (X + 289, Z + 700), (X, Z + 800), (X, Z + 820) });
        foreach (var (x, z) in allStands)
            Check.That(NavMesh.Locate(1, x, Y, z, out var location) && !location.indoors && Math.Abs(location.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified visible outdoor NAV ({x},{Y},{z})");

        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(req => archive.TryGetValue(req.Key, out var row) && row.Level >= req.Value)),
            "Complete ranked dual-SMG cone profile fits");
        await PvpOpponentsFixture.EnterAsync(actor);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { X, Y, Z }),
            $"CSR enters at the exact outdoor stand ({X},{Y},{Z}) before any movement");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, Weapon);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Name);
            Check.Equal(stand.Id, BitConverter.ToUInt32(view.Attributes["CharacterID"]), "Observed fixture identity");
            Check.Equal(stand.Organization, view.Attributes["OrganizationID"].Single(), "Observed fixture organization");
            Check.Equal((byte)Level, view.Attributes["Level"].Single(), "Observed fixture level");
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Name} appeared at its documented stand");
        }

        ushort[] openers = { BulletSpray, SuppressionFire };
        await ChangeLoadoutAsync(actor, archive, openers, load: false);
        await ChangeLoadoutAsync(actor, archive, openers, load: true);
        var selected = await actor.WaitForViewNamedAsync("QAEnemy");
        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(WithHandle(StockStyle, DuelPackets.Handle(selected.ViewId, selected.SpawnId)));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 9e 00 00 00");
        await ObserveAsync(actor, styleAt, frame => Bonus(frame, Style, 171, 9, 0, false), "Level 33 Submachine Guns publishes +9 Ballistic Damage");
        await ObserveAsync(actor, 0, frame => Bonus(frame, 125, 1139, 3, 0, false), "Covering Fire contributes +3 linked Ballistic Damage");
        await ObserveAsync(actor, 0, frame => Bonus(frame, 246, 1139, 4, 0, false), "Dual SMGs contributes +4 linked Ballistic Damage");

        var witness = context.Helpers["QAOther"];
        Check.Equal(((double)X, (double)Y, (double)(Z + 820)), await MoveAndReadPositionAsync(witness, X, Y, Z + 820),
            "Cone move reports the witness position from the server");
        Check.Equal(((double)X, (double)Y, (double)(Z + 801)), await MoveAndReadPositionAsync(witness, X, Y, Z + 801),
            "Cone witness returns to its initial stand");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Dual-SMG bootstrap framing");
        Console.WriteLine("DUAL SMG CONE BOOTSTRAP PASS: stock Archive reload, visible stands, equipped dual SMGs, full Ballistic influence and authoritative witness moves.");
        return Array.Empty<byte>();
    }
}
