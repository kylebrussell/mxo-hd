using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterCraftedPillRoundTripMode;

namespace NativeQa.Modes;

public sealed class StarterCompileContestMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, StartingInfo = 17000;
    private const ushort CodeCrafter = 515;
    private const uint Boost = 39413, HealthPill = 2399;
    // Stock Compile60 frame from the PR479 fixture.
    private const string CompileBoost = "60 f5 99 00 00 90 01 00 01 01";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 37, 277, CodeCrafter).Order().ToArray();

    public override string Name => "ability-parity-starter-compile-contest-spend";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,1271,1,1,0),(110,531,2,1,0)," +
            string.Join(",", Scenarios.HealthPillMaterials.Select((id, i) => $"(110,{id},{4 + i},1,0)")),
        $"DELETE FROM char_abilities WHERE char_id=110 AND (slot=400 OR ability_id IN ({Boost << 7},{HealthPill << 7}))",
        $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES (110,400,{Boost << 7},1,0)",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110 AND ability_id >= 2147483648"
    }, new[]
    {
        $"UPDATE characters SET y={Y},cash={StartingInfo},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId=110"
    }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "515,969,970", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, CSR ({X},{Y},{Z}) on outdoor NAV floor 400. No targets or helpers. Make sure the bag clears the desktop overlay. Compiler bag 1, Analyser bag 2, Health Pill 1.0 ingredients bag 4–8. One Max Health Boost Pill 3.0 code charge is already in the archive. Start with {StartingInfo} Info.",
            "Use Activate Item in the bag menu for slot 1. In the Compiler choose Max Health Boost Pill 3.0. Wait for the timer to finish. The rank and complexity make this contest a guaranteed failure. Expect [COMPILE60] Contest failed item=39413 and 61 01, no physical Boost, the code still at one charge, and 14,796 Info spent. Close the Compiler.",
            "Activate bag slot 2 and write Consumables > Consumable Pills > Health Pill 1.0 with the prepared five materials. Expect [WRITECODE] success=True, one new ten-charge code and six Info spent. Close the Analyser. Activate bag slot 1 and compile that new Health Pill 1.0 code once. Expect [COMPILE60] Finished 2399 result 0 remaining 9, eight Info spent, one pill and the original Boost code intact. No combat or vital changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Expected repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1,
            "The crafting stand is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        var db = context.OwnedDatabase!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Crafting ranks match their full loaded prerequisites");
        Check.That(!OrdinaryItemCode.Succeeds(Level, CraftingRules.Find(Boost).Complexity!.Value, 100, 0),
            "Even the maximum draw fails for the Boost recipe");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        await ToolAsync(CraftingSession.Ability, CompilerOpen, true);
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(CompileBoost);
        await MatchAsync(cursor, "61 01", TimeSpan.FromSeconds(55));
        Check.Equal(2204L, await CashAsync(), "A completed contest failure debits 14,796 Info");
        Check.That(server.DebugLog().Contains("info=14796 cash=2204; code retained", StringComparison.Ordinal),
            "The failed contest publishes its Info debit");
        Check.Equal(1L, await ChargesAsync(Boost), "The failed result keeps its code charge");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid={Boost}"), "No Boost materializes");

        await ToolAsync(CraftingSession.Ability, CompilerClose, false);
        await ToolAsync(CraftingSession.WriterAbility, WriterOpen, true);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Scenarios.HealthPillWriterRequest);
        await MatchAsync(cursor, "81 2f 00 00 5f 09 00 00");
        Check.Equal(2198L, await CashAsync(), "The Writer spends six Info");
        Check.Equal(10L, await ChargesAsync(HealthPill), "The Writer produces a ten-charge code");
        await ToolAsync(CraftingSession.WriterAbility, WriterClose, false);
        await ToolAsync(CraftingSession.Ability, CompilerOpen, true);
        ushort slot = checked((ushort)await db.LongAsync($"SELECT slot FROM char_abilities WHERE char_id=110 AND ability_id={HealthPill << 7}"));
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"60 5f 09 00 00 {StockAbilityProfile.LittleEndianHex(slot)} 00 01 01");
        await MatchAsync(cursor, "61 00");
        Check.Equal(2190L, await CashAsync(), "Successful compile spends eight Info exactly once");
        Check.Equal(9L, await ChargesAsync(HealthPill), "Successful compile spends one charge");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid={HealthPill}"), "Successful compile produces one pill");
        Check.Equal(1L, await ChargesAsync(Boost), "Other code remains intact");
        await ToolAsync(CraftingSession.Ability, CompilerClose, false);
        Check.Equal(0, actor.World.Problems.Count, "Stock crafting frames parse cleanly");
        return Array.Empty<byte>();

        Task<long> CashAsync() => db.LongAsync("SELECT cash FROM characters WHERE charId=110");
        Task<long> ChargesAsync(uint goid) => db.LongAsync($"SELECT COALESCE(SUM(level),0) FROM char_abilities WHERE char_id=110 AND ability_id={goid << 7}");
        Task MatchAsync(int start, string bytes, TimeSpan? wait = null)
        {
            var pattern = BytePattern.Parse(bytes);
            return actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(start).Any(x => pattern.IsMatch(x.Bytes)),
                $"Expected stock crafting reply {bytes}", wait ?? HeadlessPlayer.DefaultTimeout);
        }
        async Task ToolAsync(ushort id, string frame, bool active)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(frame);
            await MatchAsync(start, $"80 bd 05 {StockAbilityProfile.LittleEndianHex(id)} 00 00 00 00 00 {(active ? "01" : "00")}");
        }
    }
}
