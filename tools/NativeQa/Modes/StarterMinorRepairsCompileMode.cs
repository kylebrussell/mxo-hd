using System.Diagnostics;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterMinorRepairsCompileMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400;
    private const ushort MinorRepairs = 24, CompileItems = 277, ConsumableSlot = 400, EquipmentSlot = 401;
    private const byte CompilerSlot = 1;
    private const uint Consumable = 39407, Equipment = 700;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, MinorRepairs, CompileItems }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-minor-repairs-compile";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            $"INSERT INTO inventory (charId,goid,slot,count,purity) VALUES (110,{CraftingSession.Tool},{CompilerSlot},1,0)",
            $"DELETE FROM char_abilities WHERE char_id=110 AND slot IN ({ConsumableSlot},{EquipmentSlot})",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded,ability_name) VALUES " +
                $"(110,{ConsumableSlot},{Consumable << 7},2,0,'Antibiotic 2 code'),(110,{EquipmentSlot},{Equipment << 7},2,0,'Revolver code')"
        }, new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},cash=20000 WHERE charId=110" },
        DevCommands: true, hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(CompilerSlot, CraftingSession.Tool, "Code Compiler") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "24,277,969", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "24" },
        readyMessages: new[]
        {
            $"CSR level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, outdoor NAV ({X},{Y},{Z}) floor 400. No helpers or targeting. Page 2 slot 1 pre-seeds the Code Compiler. The archive has two charges each of Antibiotic 2 (39407) and Revolver (700).",
            "First type ?qaunload 24. Check [MODIFIER] char=110 source=24 stat=955 remove +33; source 277 stat 706 stays +19. Open the compiler and select Antibiotic 2, then Compile. Save the native countdown and stock 60 through 61. It should run about 9 s. Compile Revolver next, about 3 s. Wait for each result before starting the next. Success consumes one charge; a failed roll still measures the timer.",
            "Close the compiler. Type ?qaload 24 and check the +33 stat 955 re-add. Reopen the compiler. Repeat Antibiotic 2, now about 6.03 s, and Revolver, still about 3 s. The log should show [COMPILE60] 706/skill 19, 954 0, 955 33 for Antibiotic and 955 0 for Revolver. Save the displayed countdown as well as the logged time. No [VITALS] or [PVP] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot={CompilerSlot} AND goid={CraftingSession.Tool}"),
            "The expected Code Compiler is in the hotbar's bag slot");
        Check.Equal(2L, await db.LongAsync("SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND level=2 AND is_loaded=0 AND " +
            $"((slot={ConsumableSlot} AND ability_id={Consumable << 7}) OR (slot={EquipmentSlot} AND ability_id={Equipment << 7}))"),
            "Both compile controls start with two usable charges");
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
            "The compiler stand is on verified outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "All learned compiler and Minor Repairs prerequisites fit level 19");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, CompileItems, 706, Level, 0, false),
            "Compile Items publishes skill 19");

        foreach (bool load in new[] { false, true })
        {
            int from = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { MinorRepairs }, load);
            await StockAbilityProfile.ObserveAsync(actor, from,
                frame => StockAbilityProfile.Bonus(frame, MinorRepairs, 955, 33, 0, !load), "Minor Repairs changes only the +33 time reduction");
            actor.SkipReceived();
            Scenarios.CompilerTool(actor, CompilerSlot, true);
            await actor.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 01");
            await CompileAsync(Consumable, ConsumableSlot, load ? 6.03 : 9, load ? 33 : 0);
            await CompileAsync(Equipment, EquipmentSlot, 3, 0);
            actor.SkipReceived();
            Scenarios.CompilerTool(actor, CompilerSlot, false);
            await actor.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 00");
        }
        Check.Equal(0, actor.World.Problems.Count, "Native compiler records and archive loadout parsing");
        return Array.Empty<byte>();

        async Task CompileAsync(uint goid, ushort slot, double seconds, int consumableReduction)
        {
            // Native Compile60 selects the result GOID, live archive slot and bag tool.
            int logAt = server.DebugLog().Length;
            var timer = Stopwatch.StartNew();
            actor.SkipReceived();
            actor.SendRpc($"60 {WireFormat.ToHex(BitConverter.GetBytes(goid))} {StockAbilityProfile.LittleEndianHex(slot)} 00 {CompilerSlot:x2} 01");
            await actor.WaitForRpcAsync(packet => packet.Bytes is [0x61, 0 or 1], "Ordinary compile completion",
                TimeSpan.FromSeconds(seconds + 8));
            Check.That(timer.Elapsed.TotalSeconds >= seconds - .2, $"{goid} does not complete before its computed duration");
            Check.That(server.DebugLog()[logAt..].Contains(FormattableString.Invariant(
                $"[COMPILE60] Started {goid} seconds {seconds} skill {Level} 954 0 955 {consumableReduction}"), StringComparison.Ordinal),
                $"{goid} starts the correct category timer");
        }
    }
}
