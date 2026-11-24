using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CodeInfectionStockLedgerMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private const ushort Style = CombatStyles.HackerStyle;
    private static readonly ushort[] Hotbar = { Style, 472,474 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Hotbar.Concat(new ushort[] { 2, 17 }).ToArray()).Order().ToArray();
    private static readonly ushort[] Defender = StockAbilityProfile.Profile(2, 17, 8).Order().ToArray();
    private static readonly (string Handle, uint Id, int X, int Z, int Alignment)[] Stands =
    {
        ("QAEnemy", 6114, X, 6600, 2), ("QAFar", 6112, X, 6750, 2),
        ("QAFriend", 6111, 17100, 6600, 1), ("QAOther", 6113, X, 8001, 2)
    };

    public override string Name => "ability-parity-code-infection-stock-ledger";

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
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "472,474", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Page 2 slots 1/2/3: Hacker Style / Code Infection 3.0 / Code Infection 4.0. Start at READY HOME facing QAEnemy 2 m away; no teleport needed. Select 1. Try both rows without a target, /target QAFriend and /target QAOther (16.01 m). Expect stock suppression or 24/34 with no cast, reuse or IS debit.",
            "Use /target QAEnemy. Press slots 2 and 3 at least 5 s apart. Save both full first requests. Expect 2 s cast for each, 11/13 IS after truncated +5% cost reduction, 12 s own / 4 s family 702, and separate [MODIFIER] source=472 and 474 stat=1157 for 30 s. No initial HP damage. Count the entire 2.5 s tick ledger and matching removals. The existing capture-calibrated runtime starts from 716 and 866â€“880 total, then consumes +42 Viral influence: +1016 and +1229–1249 in the applied DOT records, with neutral ticks 85 and 102–104.",
            "After both infections expire, /target QAFar and submit {\"actor\":\"QAFar\",\"action\":\"aura-resist\"}. Repeat both slots 5 s apart. Observe [MODIFIER] source=8 stat=157 +75 for 20 s. Each infection has type2 total75 with final 69 and 83–85 respectively, then returns to its own unresisted tick after the resistance removes. Only the selected player receives the DOT.",
            "Once reuse clears, submit the printed WALL TARGET request, move CSR by READY WALL and /target QAOther at 5 m through the solid wall. Try 2 and 3. Expect stock suppression or reason 9, with no cast, reuse, debit or DOT. Use READY HOME to restore."
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
