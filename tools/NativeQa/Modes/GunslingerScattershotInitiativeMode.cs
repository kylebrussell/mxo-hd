using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;

namespace NativeQa.Modes;

public sealed class GunslingerScattershotInitiativeMode : NativeQaMode
{
    internal const ushort Gunslinger = 240, Scattershot = 245, Desperation = 1149;
    private const ushort GunslingerUpgrade = 1075, ScattershotUpgrade = 1071, Initiative = 1044,
        InitiativeBonus = 1046, BallisticBonus = 1139;
    private const int Level = 39, X = 21550, Y = 1095, Z = 16150, CurrentHealth = 101, MaxHealth = 500;
    private static readonly ushort[] Loadout = Profile(2, 17, Gunslinger, Scattershot, Desperation).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (Gunslinger, GunslingerUpgrade, new (ushort, int)[] { (InitiativeBonus, 2), (BallisticBonus, 1) }),
        (Scattershot, ScattershotUpgrade, new (ushort, int)[] { (InitiativeBonus, 1) })
    };

    public override string Name => "ability-parity-gunslinger-scattershot-initiative";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                $"healthC={CurrentHealth},healthM={MaxHealth},innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Desperation },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "240,245", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Level {Level}, legal {Memory}/{AbilityMemory.ForLevel(Level)} memory, CSR on outdoor NAV ({X},{Y},{Z}). " +
                $"No target needed. Page 2 slot 1 is Desperation. Starting HP {CurrentHealth}/{MaxHealth}, IS 500.",
            "Find Gunslinger Upgrade 1075 and Scattershot Upgrade 1071 in Actions. Use ?qaunload 240, then ?qaload 240. " +
                "Confirm 1075 alone goes off/on and source 240 removes/restores Initiative Bonus 1046 +2 and Ballistic Damage Bonus 1139 +1.",
            "Use ?qaunload 245, then ?qaload 245. Confirm 1071 alone goes off/on and source 245 removes/restores Initiative Bonus 1046 +1.",
            "With both parents loaded and HP 101/500, press Desperation. Expect source 1149 Initiative 1044 +86 for 20 s, 10 IS (500 to 490) and 90 s own reuse. " +
                "The loaded prerequisites supply Initiative Bonus +5 and these two parents add +3.",
            "During the effect unload both parents again. Their grant and passive records should go off, leaving the sampled +86 until the natural 20 s removal."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stand = Stand(X, Z);
        Console.WriteLine($"CSR {stand.Command}");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory == 77 && Memory <= AbilityMemory.ForLevel(Level) && rows.Keys.ToHashSet().SetEquals(Loadout) &&
            !rows.ContainsKey(GunslingerUpgrade) && !rows.ContainsKey(ScattershotUpgrade) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                rows.TryGetValue(required.Key, out var found) && found.Level >= required.Value)),
            "Complete legal profile with the two upgrades granted only during play");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var own = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal((ushort)CurrentHealth, BitConverter.ToUInt16(own.Attributes["Health"]), "Exact fractional health at entry");

        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);
        foreach (var passive in Passives)
        {
            Check.Equal(1, actor.World.RpcSnapshot().Count(GrantRecord(passive.Child, remove: false)),
                $"{passive.Child} has one live grant at readiness");
            ushort[] parent = new[] { passive.Parent };
            var other = Passives.Single(entry => entry.Parent != passive.Parent);
            int removed = await ChangeLoadoutAsync(actor, rows, parent, load: false);
            await CheckPassivesAsync(actor, proof, new[] { passive }, removed, remove: true);
            int restored = await ChangeLoadoutAsync(actor, rows, parent, load: true);
            await CheckPassivesAsync(actor, proof, new[] { passive }, restored, remove: false);
            Check.That(!actor.World.RpcSnapshot().Skip(removed).Any(GrantRecord(other.Child, remove: true)),
                $"Unloading {passive.Parent} keeps the other upgrade active");
        }

        int castAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterInitiativeBatchMode.StockCast);
        await ObserveAsync(actor, castAt, packet => Bonus(packet, Desperation, Initiative, 86, 20, remove: false),
            "Real Desperation cast samples both disciplines' Initiative Bonus");
        await EvidenceRetestWire.TimerAsync(actor, castAt, Desperation, AbilityCooldowns.OwnTimer, 90);
        int bothOff = await ChangeLoadoutAsync(actor, rows, new[] { Gunslinger, Scattershot }, load: false);
        await CheckPassivesAsync(actor, proof, Passives, bothOff, remove: true);
        actor.SendChat("?combatdepth initiative");
        await actor.WaitForTextAsync("bonus=86");
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(castAt).Any(packet =>
            Bonus(packet, Desperation, Initiative, 86, 20, remove: true)), "Original +86 retires after 20 s", TimeSpan.FromSeconds(24));
        actor.SendChat("?combatdepth initiative");
        await actor.WaitForTextAsync("bonus=0");
        Check.Equal(0, actor.World.Problems.Count, "Gunslinger and Scattershot loaded grant framing");
        return JsonSerializer.SerializeToUtf8Bytes(proof);
    }
}
