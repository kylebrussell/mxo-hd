using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCraftedPillRoundTripMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, InitialInfo = 10000;
    private const ushort Decompile = 276, Skill = 707;
    private const byte CompilerSlot = 1, AnalyserSlot = 2, DecompilerSlot = 3, FirstMaterialSlot = 4;
    private const uint Pill = 2399, Decompiler = 613;
    // Complete stock inventory open and close requests from PR640e lines 1362, 1475, 1618 and 1688.
    internal const string WriterOpen = "80b9ca03000000000000000000000000000000000000000000000000000000000000000008020100000000";
    internal const string WriterClose = "80b9ca03000000000000000000000000000000000000000000000000e025b502b00c000005020000000000";
    internal const string CompilerOpen = "80b9c903000000000000000000000000000000000000000000000000000000000000000005010100000000";
    internal const string CompilerClose = "80b9c903000000000000000000000000000000000000000000000000000000000000006205010000000000";

    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 37, Decompile, 277).Order().ToArray();

    public override string Name => "ability-parity-starter-crafted-pill-round-trip";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{CraftingSession.Tool},{CompilerSlot},1,0)," +
            $"(110,{CraftingSession.WriterTool},{AnalyserSlot},1,0),(110,{Decompiler},{DecompilerSlot},1,0)," +
            string.Join(",", Scenarios.HealthPillMaterials.Select((item, i) => $"(110,{item},{FirstMaterialSlot + i},1,0)")),
        $"DELETE FROM char_abilities WHERE char_id=110 AND ability_id={Pill << 7}",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110 AND ability_id >= 2147483648"
    }, new[]
    {
        $"UPDATE characters SET x={X},y={Y},z={Z},cash={InitialInfo},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId=110"
    }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: "martial-player-controls.csv",
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(DecompilerSlot, Decompiler, "Item Decompiler") },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "276,969,970", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Level 18, CSR at (17000,495,6400) on outdoor NAV floor 400. Move the game window left until Inventory clears any desktop overlay. No helpers or targets. There are no Health Pills or Health Pill codes at entry. Enter ?qaunload 276 and confirm source 276 stat 707 +18 removal.",
            "Use Activate Item on bag slot 2 Code Analyser. Write Consumables > Consumable Pills > Health Pill 1.0, using the five ingredients in slots 4–8. Expect five material removals, a ten-charge code, a successful Writer result and 6 Info spent. Close Writer. Activate Code Compiler from bag slot 1 and compile that exact new code once. Expect a 3 s job, one charge used, 8 Info spent and one Health Pill in bag slot 4. Close Compiler.",
            "With 276 still unloaded, try the Decompiler shortcut on page 2 slot 1 and the only Health Pill. It is refused and the pill stays. Enter ?qaload 276, expect +18 to stat 707, then repeat on that same pill. Expect stock 81 31, [DECOMPILE] item=2399 skill=18 complexity=1 success=True, one pill removed, one recipe packet and 10 s reuse. No maximum [VITALS] changes or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Crafting needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
            "The bag fixture starts on outdoor NAV");
        var db = context.OwnedDatabase!;
        Check.Equal(8L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 8"),
            "All tools and five ingredients are present");
        Check.Equal(0L, await CountPillsAsync(), "The bag has no preexisting pill");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND ability_id={Pill << 7}"),
            "No preexisting Health Pill code");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(req => archive.TryGetValue(req.Key, out var row) && row.Level >= req.Value)),
            "The complete crafting profile fits level 18 and arrives with the required ranks");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Decompile }, false);
        await ModifierAsync(unloaded, true);

        await ToolAsync(CraftingSession.WriterAbility, WriterOpen, true);
        int writtenAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Scenarios.HealthPillWriterRequest);
        await MatchAsync(writtenAt, "81 2f 00 00 5f 09 00 00");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot BETWEEN 4 AND 8"),
            "Writer consumes exactly the five selected inputs");
        ushort slot = checked((ushort)await db.LongAsync($"SELECT slot FROM char_abilities WHERE char_id=110 AND ability_id={Pill << 7}"));
        long charges = await ChargesAsync(slot), writtenInfo = await InfoAsync();
        Check.That(slot > 0, "Writer creates a usable ordinary code slot");
        Check.Equal(10L, charges, "Writer creates a code with ten charges");
        Check.Equal(9994L, writtenInfo, "Writer spends 6 Info");
        await ToolAsync(CraftingSession.WriterAbility, WriterClose, false);

        await ToolAsync(CraftingSession.Ability, CompilerOpen, true);
        int compiledAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"60 5f 09 00 00 {StockAbilityProfile.LittleEndianHex(slot)} 00 01 01");
        await MatchAsync(compiledAt, "61 00");
        Check.Equal(charges - 1, await ChargesAsync(slot), "Compiler consumes exactly one charge");
        Check.Equal(1L, await CountPillsAsync(), "Compiler produces the only eligible pill");
        long compiledInfo = await InfoAsync();
        Check.Equal(9986L, compiledInfo, "Compiler spends 8 additional Info");
        Check.That(server.DebugLog().Contains("[COMPILE60] Started 2399 seconds 3"),
            "The pill uses the compiler price and three-second time floor");
        await ToolAsync(CraftingSession.Ability, CompilerClose, false);

        byte resultSlot = checked((byte)await db.LongAsync($"SELECT slot FROM inventory WHERE charId=110 AND goid={Pill}"));
        Check.Equal(FirstMaterialSlot, resultSlot, "Compiler fills the first freed bag slot");
        int refused = actor.World.RpcSnapshot().Count;
        actor.SendRpc(InventoryUse(resultSlot));
        await MatchAsync(refused, "81 2f 00 00 00 00 00 00");
        Check.Equal(1L, await CountPillsAsync(), "The unloaded refusal preserves the newly crafted pill");

        int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Decompile }, true);
        await ModifierAsync(reloaded, false);
        int accepted = actor.World.RpcSnapshot().Count;
        actor.SendRpc(InventoryUse(resultSlot));
        await MatchAsync(accepted, $"81 2f 01 {resultSlot:x2} 5f 09 00 00");
        await MatchAsync(accepted, "81 30 5f 09 00 00 01");
        await EvidenceRetestWire.TimerAsync(actor, accepted, Decompile, AbilityCooldowns.OwnTimer, 10);
        Check.Equal(0L, await CountPillsAsync(), "Reloaded Decompile consumes the compiler's output");
        Check.Equal(charges - 1, await ChargesAsync(slot), "Decompile does not touch the item code");
        Check.Equal(compiledInfo, await InfoAsync(), "Decompile spends no Info");
        Check.Equal(0, actor.World.Problems.Count, "Crafted pill framing");
        Console.WriteLine($"PILL ROUND TRIP PASS: code {charges} → {charges - 1}, Info {InitialInfo} → {writtenInfo} → {compiledInfo}, output slot {resultSlot} consumed after reload.");
        return Array.Empty<byte>();

        Task<long> CountPillsAsync() => db.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId=110 AND goid={Pill}");
        Task<long> InfoAsync() => db.LongAsync("SELECT cash FROM characters WHERE charId=110");
        Task<long> ChargesAsync(ushort codeSlot) => db.LongAsync($"SELECT level FROM char_abilities WHERE char_id=110 AND slot={codeSlot} AND ability_id={Pill << 7}");
        Task<RpcRecord> MatchAsync(int start, string hex) => StockAbilityProfile.ObserveAsync(actor, start, frame => BytePattern.Parse(hex).IsMatch(frame.Bytes), hex);
        Task<RpcRecord> ModifierAsync(int start, bool removed) => StockAbilityProfile.ObserveAsync(actor, start,
            frame => StockAbilityProfile.Bonus(frame, Decompile, Skill, 18, 0, removed), $"Decompile Skill remove={removed}");

        async Task ToolAsync(ushort id, string packet, bool active)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(packet);
            byte[] expected = AbilityBuffs.BuildExclusive(id, active);
            await StockAbilityProfile.ObserveAsync(actor, start, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"Inventory tool {id} active={active}");
        }
    }

    private static string InventoryUse(byte itemSlot) => $"81 31 14 01 00 00 {itemSlot:x2} 03 01";
}
