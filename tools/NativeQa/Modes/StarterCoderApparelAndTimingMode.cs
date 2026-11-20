using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderApparelAndTimingMode : NativeQaMode
{
    private const ushort MinorRepairs = 24, CodeCrafter = 515, ApparelMaker = 528;
    private const uint Eyewear = 671, HealthPill = 39413;
    private const int EyewearSlot = 14, HealthPillSlot = 400;
    // PR479 native tool and Compile60 records. Archive slots match the capture exactly.
    private const string OpenCompiler = "80b9c903000000000000000000000000000000000000000000000000000000000000000008010100000000";
    private const string CloseCompiler = "80b9c903000000000000000000000000000000000000000000000000000000000000006208010000000000";
    private const string CompileEyewear = "60 9f 02 00 00 0e 00 00 01 01";
    private const string CompilePill = "60 f5 99 00 00 90 01 00 01 01";
    private static readonly ushort[] Passives = { MinorRepairs, CodeCrafter, ApparelMaker };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 24, 37, 277, 515, 528).Order().ToArray();

    public override string Name => "ability-parity-starter-coder-apparel-and-timing";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 23828, 13984, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,1271,1,1,0)",
        $"UPDATE char_abilities SET slot=399 WHERE char_id=110 AND slot={EyewearSlot}",
        $"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded,ability_name) VALUES " +
            $"(110,{EyewearSlot},{Eyewear << 7},2,0,'Eyewear comparison code')," +
            $"(110,{HealthPillSlot},{HealthPill << 7},4,0,'Health pill comparison code')",
        "UPDATE characters SET y=495,cash=200000 WHERE charId=110"
    }, Array.Empty<string>(), DevCommands: true, hotbarAbilities: Passives,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "24,515,528", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Use Enter for separate commands. Page 2 identifies the passives. The Code Compiler is the second visible bag square. Right-click and choose Activate Item. Each compile should spend exactly one code charge. Open the tool again after each loadout change.",
            "A. Unload 528, then 515 and 24. Compile eyewear GO 671 once and Max Health Boost Pill 3.0 GO 39413 once. Compiler log should show apparel specialization 0, eyewear 3 s, and pill 18 s. Close Compiler.",
            "B. Load 515 only. Reopen and compile the pill once. Expect 954 +25 and 13.5 s. Close Compiler.",
            "C. Load 528 and 24, leaving 515 loaded. Check Apparel Crafting 652 +50. Reopen and compile the remaining eyewear charge at specialization 50, then the pill at 9.045 s. Eyewear stays at 3 s and never uses consumable reduction 955. Close Compiler.",
            "D. Unload 528, then 515, leaving 24 loaded. Reopen and compile the last pill charge. Expect 954 0, 955 +33 and 12.06 s. Capture the native Progress timings and the compiler log. The fixed server delays use the recovered client formula."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Apparel and timing chains fit in memory");
        Check.Equal(2L, await CodeChargesAsync(Eyewear, EyewearSlot), "Two exact-slot eyewear charges");
        Check.Equal(4L, await CodeChargesAsync(HealthPill, HealthPillSlot), "Four exact-slot pill charges");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StatAsync(0, ApparelMaker, 652, 50, remove: false);

        int unload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new ushort[] { ApparelMaker, CodeCrafter, MinorRepairs }, load: false);
        await StatAsync(unload, ApparelMaker, 652, 50, remove: true);
        await ToggleAsync(open: true);
        await CompileAsync(CompileEyewear, Eyewear, EyewearSlot, 1, 3, 0, 0, 0);
        await CompileAsync(CompilePill, HealthPill, HealthPillSlot, 3, 18, 0, 0, 0);
        await ToggleAsync(open: false);

        int general = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { CodeCrafter }, load: true);
        await StatAsync(general, CodeCrafter, 954, 25, remove: false);
        await ToggleAsync(open: true);
        await CompileAsync(CompilePill, HealthPill, HealthPillSlot, 2, 13.5f, 25, 0, 0);
        await ToggleAsync(open: false);

        int specialized = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ApparelMaker, MinorRepairs }, load: true);
        await StatAsync(specialized, ApparelMaker, 652, 50, remove: false);
        await StatAsync(specialized, MinorRepairs, 955, 33, remove: false);
        await ToggleAsync(open: true);
        await CompileAsync(CompileEyewear, Eyewear, EyewearSlot, 0, 3, 25, 0, 50);
        await CompileAsync(CompilePill, HealthPill, HealthPillSlot, 1, 9.045f, 25, 33, 0);
        await ToggleAsync(open: false);

        int residual = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ApparelMaker, CodeCrafter }, load: false);
        await StatAsync(residual, ApparelMaker, 652, 50, remove: true);
        await StatAsync(residual, CodeCrafter, 954, 25, remove: true);
        await ToggleAsync(open: true);
        await CompileAsync(CompilePill, HealthPill, HealthPillSlot, 0, 12.06f, 0, 33, 0);
        await ToggleAsync(open: false);
        Check.Equal(0, actor.World.Problems.Count, "Compiler fixture framing");
        return Array.Empty<byte>();

        Task<RpcRecord> StatAsync(int cursor, ushort source, ushort stat, int value, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, cursor,
                frame => StockAbilityProfile.Bonus(frame, source, stat, value, 0, remove), $"Compiler passive {source}/{stat}, remove={remove}");

        async Task ToggleAsync(bool open)
        {
            actor.SkipReceived();
            actor.SendRpc(open ? OpenCompiler : CloseCompiler);
            await actor.WaitForRpcAsync($"80 bd 05 c9 03 00 00 00 00 00 {(open ? "01" : "00")}");
        }

        async Task CompileAsync(string request, uint goid, int slot, int remaining, float seconds, int general, int consumable, int specialization)
        {
            long before = await InventoryAsync(goid);
            int logAt = server.DebugLog().Length;
            actor.SkipReceived();
            actor.SendRpc(request);
            await actor.WaitForRpcAsync("61 00", TimeSpan.FromSeconds(seconds + 7));
            string prefix = FormattableString.Invariant($"[COMPILE60] Started {goid} seconds {seconds} skill 50 954 {general} 955 {consumable} ");
            Check.That(server.DebugLog()[logAt..].Split('\n').Any(line => line.StartsWith(prefix, StringComparison.Ordinal) &&
                line.TrimEnd().EndsWith($"specialization {specialization}", StringComparison.Ordinal)),
                $"The compiler consumes the expected specialization and timing modifiers for {goid}");
            Check.Equal((long)remaining, await CodeChargesAsync(goid, slot), "Compiling spends exactly one code charge");
            Check.Equal(before + 1, await InventoryAsync(goid), "Compiling creates exactly one matching item");
        }

        Task<long> InventoryAsync(uint goid) => db.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId=110 AND goid={goid}");
        Task<long> CodeChargesAsync(uint goid, int slot) => db.LongAsync(
            $"SELECT COALESCE(MAX(level),0) FROM char_abilities WHERE char_id=110 AND slot={slot} AND ability_id={goid << 7}");
    }
}
