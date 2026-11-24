using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class InfectAreaTwoStockMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private const ushort Style = CombatStyles.HackerStyle;
    private static readonly ushort[] Hotbar = { Style, 475 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Hotbar.Concat(new ushort[] { 2, 17 }).ToArray()).Order().ToArray();
    private static readonly ushort[] Defender = StockAbilityProfile.Profile(2, 17, 8).Order().ToArray();
    private static readonly (string Handle, uint Id, int X, int Z, int Alignment)[] Stands =
    {
        ("QAEnemy", 6114, X, 6600, 2), ("QAFar", 6112, X, 7100, 2),
        ("QAFriend", 6111, 17100, 6600, 1), ("QAOther", 6113, X, 7101, 2)
    };

    public override string Name => "ability-parity-infect-area-two-stock";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=8000,healthM=8000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,alignment=1,innerStrC=700,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Defender.Select((id, i) =>
            $"(6112,{i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Alignment} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "475", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Page 2 slots 1/2: Hacker Style / Infect Area 2.0. CSR starts HOME facing QAEnemy 2 m ahead. Press slot 1. Try 2 with target cleared and with /target QAFriend. Expect stock suppression or 24, no debit/cast/reuse. QAFriend is 1 m from QAEnemy.",
            "Select /target QAEnemy and press slot 2. Capture the first full request. Expect 2 s cast, 24 IS (base 25 less the truncated +5% cost reduction), 12 s reuse and no initial HP damage. QAEnemy and QAFar at exactly 5.00 m receive source=475 stat=1157 for 30 s, then removal. QAOther at 5.01 m and the nearby same-org ally receive neither. Count every 2.5 s tick through expiry; the retained capture-calibrated base 624 scales to +886, and its neutral ticks are 74.",
            "After expiry, keep QAEnemy selected. Submit {\"actor\":\"QAFar\",\"action\":\"aura-resist\"} and confirm source 8 stat 157 +75 (20 s), then repeat slot 2. Only QAFar is reduced, [TYPED-RESISTANCE OURS] 475 type2 total75 raw74 final60, and that same DOT resumes neutral damage after the resistance removes. The primary remains unresisted.",
            "After expiry, move QAOther to (17000,495,8001) via its move request. /target QAOther from HOME and try 475 (16.01 m, reason 34 if sent). Next submit WALL TARGET and move CSR by READY WALL; reselect QAOther through the wall at 5 m. Expect reason 9 if sent. Neither attempt creates a primary or splash DOT. Restore with READY HOME."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Ravager abilities require NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(s => (s.X, s.Z)).Append((X, Z)).Append((X, 8001)))
        {
            Stand(x, Y, z);
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Outdoor sight ({x},{Y},{z})");
        }
        string wallTarget = Stand(AcrossWallX, WallY, WallZ, indoors: true, facing: 192);
        string wall = Stand(WallX, WallY, WallZ, indoors: true, facing: 64);
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Solid office wall blocks sight");
        Console.WriteLine($"READY HOME {Stand(X, Y, Z)}");
        Console.WriteLine($"READY WALL {wall}");
        Console.WriteLine($$"""WALL TARGET {"actor":"QAOther","action":"chat","text":"{{wallTarget}}"}""");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable fixture.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(parent => archive.TryGetValue(parent.Key, out ushort rank) && rank >= parent.Value)),
            "Complete Ravager profile fits stock memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Handle);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Handle} is on the verified stand");
        }
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Style, 172, 27, 0, false), "Stock Hacker Style enabled");
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(context.Helpers["QAFar"]);
        // First stock request for the Ravager hotbar slots must be captured in native QA.
        return Array.Empty<byte>();
    }

    private static string Stand(int x, int y, int z, bool indoors = false, byte facing = 0)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == indoors && stand.floorY + 95 == y,
            $"Verified NAV ({x},{y},{z})");
        return $"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {facing:x2}";
    }
}

