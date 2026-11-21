using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerPassiveRoundtripsMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400;
    private const ushort Virologist = 98, VirologistUpgrade = 1077, Contagion = 73, Network = 64, NetworkUpgrade = 1078;
    private static readonly (ushort Ability, ushort Upgrade, (ushort Stat, int Value)[] Modifiers)[] Cases =
    {
        (Virologist, VirologistUpgrade, new (ushort, int)[] { (1030, 1), (1045, 1) }),
        (Contagion, 0, new (ushort, int)[] { (995, 1), (1147, 5) }),
        (Network, NetworkUpgrade, new (ushort, int)[] { (1147, 1) })
    };
    private static readonly ushort[] Loadout =
        StockAbilityProfile.Profile(new ushort[] { 2, 17 }.Concat(Cases.Select(row => row.Ability)).ToArray()).Order().ToArray();

    public override string Name => "ability-parity-starter-hacker-passive-roundtrips";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "innerStrC=150,innerStrM=200 WHERE charId=110" },
        DevCommands: true, loadAbilityAncestors: true, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "64,73,98", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) is outdoors on NAV floor 400. No helper, bag or targeting. For each row, use ?qaunload ID, then ?qaload ID. Capture the [MODIFIER] lines for char=110 and the listed source. All records have duration 0s.",
            "98 Virologist removes/restores 1030 +1 and 1045 +1. Confirm PacketLog 80 b3/80 b2 for 1077 (35 04) and whether Virologist Upgrade disappears/returns in Actions.",
            "73 Resist Contagion removes/restores 995 +1 and 1147 +5. Leave the other passives alone to separate their source records.",
            "64 Network Hacker is the control for the same 1147 stat. Expect +1 off/on and PacketLog 80 b3/80 b2 for 1078 (36 04). No max [VITALS] change or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Hacker passives need repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
            "The passive test stand is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "The starter Hacker loadout fits memory");
        foreach (ushort ability in Loadout)
        {
            Check.That(archive.ContainsKey(ability), $"Stock archive contains ability {ability}");
            foreach (var req in AbilityPrerequisites.Current.Closure(ability))
                Check.That(archive.TryGetValue(req.Key, out var row) && row.Level >= req.Value,
                    $"Ability {ability} needs loaded {req.Key} rank {req.Value}, observed {archive.GetValueOrDefault(req.Key)?.Level}");
        }
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var (ability, upgrade, modifiers) in Cases)
        {
            await RecordsAsync(0, false);
            foreach (bool load in new[] { false, true })
            {
                int start = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load);
                await RecordsAsync(start, !load);
            }
            async Task RecordsAsync(int start, bool removed)
            {
                foreach (var (stat, value) in modifiers)
                    await StockAbilityProfile.ObserveAsync(actor, start,
                        frame => StockAbilityProfile.Bonus(frame, ability, stat, value, 0, removed),
                        $"Passive {ability} stat {stat} value {value} remove={removed}");
                if (upgrade != 0)
                    await StockAbilityProfile.ObserveAsync(actor, start, StockAbilityProfile.GrantRecord(upgrade, removed),
                        $"Native grant {upgrade}, remove={removed}");
            }
        }

        Check.Equal(0, actor.World.Problems.Count, "Starter Hacker passive framing");
        Console.WriteLine("HACKER PASSIVES PASS: 98/73/64 modifier round-trips and both grant lifecycles.");
        return Array.Empty<byte>();
    }
}
