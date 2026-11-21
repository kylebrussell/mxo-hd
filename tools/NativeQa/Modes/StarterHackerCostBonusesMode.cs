using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerCostBonusesMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400;
    private const ushort StandOff = 309, DedicatedCode = 346, Evade = 217;
    // Complete stock Evade Combat request from PR524.
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, StandOff, DedicatedCode, Evade }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly (ushort Ability, (ushort Stat, int Value)[] Mods)[] Passives =
    {
        (StandOff, new (ushort, int)[] { (1031, 1), (1053, 1), (1090, 3) }),
        (DedicatedCode, new (ushort, int)[] { (1031, 1), (1053, 5) })
    };

    public override string Name => "ability-parity-starter-hacker-cost-bonuses";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"x={X},y={Y},z={Z},innerStrC=39,innerStrM=200 WHERE charId=110" },
        DevCommands: true, hotbarAbilities: new ushort[] { Evade }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "217,309,346", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR level {Level}. {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, outdoor NAV ({X},{Y},{Z}) on floor 400. No helpers, targeting or bag. Current IS stays 39.",
            "With both passives loaded, capture [MODIFIER] char=110 source=309 stats 1031 +1, 1053 +1, 1090 +3 and source=346 stats 1031 +1, 1053 +5, all duration 0s. Press page 2 slot 1 (Evade Combat). Record whether a stock 80 b9 d9 request leaves the client and what the UI says. If sent, expect 80 ba reason 17 deficit 1 on the current server.",
            "Enter ?qaunload 309, then ?qaunload 346. Check removes for the same stats and values, then try Evade again as the unloaded control. Type ?qaload 346, then ?qaload 309. Check both sets re-add and retry Evade. Report any change in local admission or displayed cost across the three attempts.",
            "No shield-on, IS debit, maximum [VITALS] change or [PVP] round on an insufficient-IS attempt. Keep native evidence for both passive lifecycles. The separate 1053-to-117 cost calculation stays open unless stock evidence resolves it."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
            "The cost control stand is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "The two passive paths and Evade fit memory");
        foreach (ushort id in Loadout)
        {
            Check.That(archive.ContainsKey(id), $"Archive contains {id}");
            foreach (var need in AbilityPrerequisites.Current.Closure(id))
                Check.That(archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value,
                    $"Ability {id} needs {need.Key} rank {need.Value}, observed {archive.GetValueOrDefault(need.Key)?.Level}");
        }
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        foreach (var (ability, mods) in Passives)
            await RecordsAsync(0, ability, mods, false);
        await RefuseEvadeAsync();
        foreach (bool load in new[] { false, true })
        {
            foreach (var (ability, mods) in Passives)
            {
                int start = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load);
                await RecordsAsync(start, ability, mods, !load);
            }
            await Task.Delay(TimeSpan.FromSeconds(1)); // Cross the stock double-click suppression window.
            await RefuseEvadeAsync();
        }
        Check.Equal(0, actor.World.Problems.Count, "Hacker cost comparison framing");
        Console.WriteLine("HACKER COST BONUSES PASS: passive records round-trip; Evade at 39 IS refuses in all three loadouts.");
        return Array.Empty<byte>();

        async Task RecordsAsync(int start, ushort ability, (ushort Stat, int Value)[] mods, bool removed)
        {
            foreach (var (stat, value) in mods)
                await StockAbilityProfile.ObserveAsync(actor, start,
                    frame => StockAbilityProfile.Bonus(frame, ability, stat, value, 0, removed),
                    $"Passive {ability} stat {stat} value {value} remove={removed}");
        }

        async Task RefuseEvadeAsync()
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(EvadeUse);
            await StockAbilityProfile.ObserveAsync(actor, start, frame =>
                AbilityRefusals.TryDecode(frame.Bytes, out var refusal) && refusal.AbilityID == Evade &&
                refusal.ErrorID == AbilityRefusals.InsufficientInnerStrength && refusal.InnerstrengthDeficit == 1,
                "Evade requires one more IS");
            actor.SkipReceived();
            actor.SendRpc("81 48");
            await actor.WaitForRpcAsync("81 49");
            Check.That(!actor.World.RpcSnapshot().Skip(start).Any(frame =>
                frame.Opcode == 0xbd || StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, false)),
                "No reuse or shield from the refusal");
            ushort current = actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .LastOrDefault(BitConverter.ToUInt16(actor.World.CreationSnapshot().First(view => view.ViewId == 2)
                    .Attributes["InnerStrengthAvailable"]));
            Check.Equal((ushort)39, current, "Refused Evade leaves IS unchanged");
        }
    }
}
