using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterAbilityMakerCompileMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300;
    private const ushort AbilityMaker = 516, AbilityCrafting = 653;
    private const string AwakenedWrite = "81 2e 00 08 00 80 09 00 02 04 00 " +
        "00 91 03 00 84 09 00 00 00 91 04 00 87 09 00 00 00 91 05 00 ad 09 00 00 00 91 06 00 97 09 00 00";
    private static readonly uint Awakened = AbilityMessages.AbilityGameObject(2);
    private static readonly uint HyperDodge = AbilityMessages.AbilityGameObject(10);
    private static readonly uint[] Recipes = { Awakened, HyperDodge };
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, 37, 275, AbilityMaker }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-ability-maker-compile";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES " +
                "(110,531,2,1,0),(110,2436,3,4,0),(110,2439,4,4,0)," +
                "(110,2477,5,2,0),(110,2455,6,4,0),(110,2467,7,2,0)",
            "CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY (charId,goid)) ENGINE=InnoDB",
            $"INSERT IGNORE INTO char_recipes(charId,goid) VALUES (110,{Awakened}),(110,{HyperDodge})",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,cash=100000 WHERE charId=110" },
        DevCommands: true, hotbarAbilities: new[] { AbilityMaker }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "516", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, verified outdoor NAV at ({X},{Y},{Z}). The Code Analyser is bag slot 2. Ability Maker starts loaded with stat 653 +19.",
            "Use ?qaunload 516. Right-click the bag-slot-2 Analyser, Activate Item, then Write Code > Ability Codes > Awakened Abilities > Awakened. Click Write Code. Expect [WRITECODE] writing=15 stat=653 specialization=0 effective=15 complexity=1 info=7 success=True.",
            "The first and second Awakened writes each spend 7 Info and consume one ingredient from each of bag slots 3–6. Expect one rank-1 Awakened code in the bag and Writer status success. Close Write Code, type ?qaload 516, then reopen and repeat Awakened. Expect [MODIFIER] source=516 stat=653 +19 and [WRITECODE] writing=15 specialization=19 effective=19. Hyper-Dodge is an optional next recipe. No targeting, PvP or max vitals changes."
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
            Check.That(CraftingRules.CanWriteAbilityCode(item), $"Source ability recipe {item} uses specialization 653");
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
        Scenarios.WriterTool(actor, 2, activate: true);
        await actor.WaitForRpcAsync("80 bd 05 ca 03 00 00 00 00 00 01");
        await WriteAwakenedAsync(0, 15, 1);
        Scenarios.WriterTool(actor, 2, activate: false);
        await actor.WaitForRpcAsync("80 bd 05 ca 03 00 00 00 00 00 00");

        int added = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { AbilityMaker }, load: true);
        await VerifyBonusAsync(added, false);
        Scenarios.WriterTool(actor, 2, activate: true);
        await actor.WaitForRpcAsync("80 bd 05 ca 03 00 00 00 00 00 01");
        await WriteAwakenedAsync(19, 19, 2);

        // A third identical stock request names the now-empty ingredient slot 5.
        actor.SkipReceived();
        actor.SendRpc(AwakenedWrite);
        await actor.WaitForRpcAsync("81 2f 05 00 00 08 00 80");
        await VerifyLedgerAsync(2);
        Check.Equal(0, actor.World.Problems.Count, "Stock ability Write Code requests and result framing");
        return Array.Empty<byte>();

        async Task WriteAwakenedAsync(int specialization, int effective, int written)
        {
            int from = context.ActualServer!.DebugLog().Length;
            actor.SkipReceived();
            actor.SendRpc(AwakenedWrite);
            var add = await actor.WaitForRpcAsync("5e ?? ?? 2e 00 00 00 01 08 00 80 01");
            await actor.WaitForRpcAsync("81 2f 00 00 00 08 00 80");
            ushort slot = BitConverter.ToUInt16(add.Bytes, 1);
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot={slot} AND goid={Awakened}"),
                "Ability Writer persists the correct bag result");
            string log = context.ActualServer.DebugLog()[from..];
            Check.That(log.Contains($"item={Awakened} writing=15 stat=653 specialization={specialization} effective={effective} complexity=1"),
                "The actual Writer contest consumes the available Ability Crafting input");
            await VerifyLedgerAsync(written);
        }

        async Task VerifyLedgerAsync(int written)
        {
            Check.Equal(100000L - 7L * written, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"),
                "Each successful write spends seven Info and the stale request spends none");
            Check.Equal((long)written, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid={Awakened}"),
                "Each successful write creates exactly one bag code");
            Check.Equal(14L - 4L * written, await db.LongAsync(
                "SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId=110 AND " +
                "((slot=3 AND goid=2436) OR (slot=4 AND goid=2439) OR (slot=5 AND goid=2477) OR (slot=6 AND goid=2455))"),
                "Each write consumes one of each ingredient and the stale request consumes none");
        }

        Task<RpcRecord> VerifyBonusAsync(int first, bool remove) => StockAbilityProfile.ObserveAsync(actor, first,
            packet => StockAbilityProfile.Bonus(packet, AbilityMaker, AbilityCrafting, Level, 0, remove),
            $"Ability Maker specialization rank {Level}, remove={remove}");
    }
}
