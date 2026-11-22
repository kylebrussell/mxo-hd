using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterAbilityMakerCompileMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300;
    private const ushort AbilityMaker = 516, AbilityCrafting = 653, CompileItems = 277;
    private static readonly uint Awakened = AbilityMessages.AbilityGameObject(2);
    private static readonly uint HyperDodge = AbilityMessages.AbilityGameObject(10);
    private static readonly uint[] Recipes = { Awakened, HyperDodge };
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, 37, 275, CompileItems, AbilityMaker }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    // Compiler bag slot 1, stock capture from PR479.
    private const string OpenCompiler = "80b9c903000000000000000000000000000000000000000000000000000000000000000008010100000000";

    public override string Name => "ability-parity-starter-ability-maker-compile";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES " +
                "(110,1271,1,1,0),(110,531,2,1,0),(110,2436,3,4,0),(110,2439,4,4,0)," +
                "(110,2477,5,2,0),(110,2455,6,4,0),(110,2467,7,2,0)",
            "CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY (charId,goid)) ENGINE=InnoDB",
            $"INSERT IGNORE INTO char_recipes(charId,goid) VALUES (110,{Awakened}),(110,{HyperDodge})",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,cash=100000 WHERE charId=110" },
        DevCommands: true, hotbarAbilities: new[] { AbilityMaker }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "516", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, verified outdoor NAV at ({X},{Y},{Z}). The Compiler is bag slot 1 and the Analyser is slot 2. Awakened and Hyper-Dodge recipes are taught and have two sets of ingredients each. Ability Maker starts loaded with stat 653 +19.",
            "Unload 516 using ?qaunload 516. Right-click the Compiler and choose Activate Item. Look for the taught ability recipes in the stock interface. If available, compile Awakened, then Hyper-Dodge, from ingredients. Save the first complete request and every reply. [COMPILE RECIPE] should show stat=653, specialization=0. Each successful result should occupy a single usable ability-code bag slot (wire item 46 with its ability descriptor).",
            "Close the Compiler, use ?qaload 516, and reopen it for the second attempts. Expect [MODIFIER] 653 +19 and [COMPILE RECIPE] specialization=19, effective skill one point higher. Compare the Info debit and code item. Natural rolls are used. If the Compiler offers only already-written archive code, look for the taught ability recipes via bag-slot 2 Analyser and capture what stock sends through the Writer. Stop on a missing action or refusal and record the UI route. No target, PvP or max vitals checks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
            $"Outdoor crafting stand ({X},{Y},{Z})");
        var db = context.OwnedDatabase!;
        foreach (uint item in Recipes)
        {
            var rule = CraftingRules.Find(item);
            Check.That(rule is { CantCraft: false } && (rule.Category & 0xff) == (byte)'A' &&
                Crafting.Recipes.ContainsKey(item), $"Source ability recipe {item} uses specialization 653");
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM char_recipes WHERE charId=110 AND goid={item}"),
                $"Ability recipe {item} is taught");
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "All ranked crafting prerequisites fit memory");
        byte[] known = actor.CharacterData[0x0a];
        var knownGoids = Enumerable.Range(1, known.Length / 4 - 1).Select(i => BitConverter.ToUInt32(known, i * 4)).ToHashSet();
        Check.That(Recipes.All(knownGoids.Contains), "The stock login sends both ability recipe IDs");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await VerifyBonusAsync(0, false);
        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { AbilityMaker }, load: false);
        await VerifyBonusAsync(removed, true);
        int added = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { AbilityMaker }, load: true);
        await VerifyBonusAsync(added, false);
        actor.SendRpc(OpenCompiler);
        await actor.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 01");

        foreach (uint item in Recipes)
        {
            // The dev chat uses the actual recipe output path. The stock recipe submit remains to be captured.
            string descriptor = WireFormat.ToHex(BitConverter.GetBytes(AbilityProgression.AbilityCodeValue(item)));
            actor.SendChat($"?compile {item}");
            var add = await actor.WaitForRpcAsync($"5e ?? ?? 2e 00 00 00 {descriptor} 01");
            ushort slot = BitConverter.ToUInt16(add.Bytes, 1);
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot={slot} AND goid={item}"),
                "Materialization persists the ability GOID and sends the stock bag class and packed rank");
        }
        Check.Equal(0, actor.World.Problems.Count, "Ability Maker seed, loadout and stock compiler tool framing");
        return Array.Empty<byte>();

        Task<RpcRecord> VerifyBonusAsync(int first, bool remove) => StockAbilityProfile.ObserveAsync(actor, first,
            packet => StockAbilityProfile.Bonus(packet, AbilityMaker, AbilityCrafting, Level, 0, remove),
            $"Ability Maker specialization rank {Level}, remove={remove}");
    }
}
