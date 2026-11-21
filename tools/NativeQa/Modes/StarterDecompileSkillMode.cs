using System.Buffers.Binary;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public class StarterDecompileSkillMode : NativeQaMode
{
    private const ushort Decompile = 276, Skill = 707;
    private const byte CompilerSlot = 1, AnalyserSlot = 2, DecompilerSlot = 3;
    private const uint Decompiler = 613;
    private const uint HealthPill = 2399, MaxHealthPill = 39413;
    private const int Rank = 18, X = 26000, Y = 495, Z = -1498;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 37, Decompile, 277).Order().ToArray();

    // PR584 sends these exact 81 31 frames for inventory slots 5 and 6 with the decompiler in slot 3.
    // The frame identifies the input by bag slot and does not contain its GOID. Both fixture pill recipes are stock-known.
    private const string UseSlot5 = "81 31 14 01 00 00 05 03 01";
    private const string UseSlot6 = "81 31 14 01 00 00 06 03 01";
    // PR479 native Compiler-window close.
    private const string CloseCompiler = "80b9c903000000000000000000000000000000000000000000000000000000000000006208010000000000";
    // PR640 native hotbar requests. Both carry flag zero even when pressed from idle.
    private const string HotbarCompiler = "80b9c90300000000000000000000000000000000000000000000000064f11a00b8d5a91f08010000000000";
    private const string HotbarWriter = "80b9ca0300000000000000000000000000000000000000000000000064f11a00b8d6a91f08020000000000";

    private static readonly AbilitySweepHotbar.InventoryShortcut[] Tools =
    {
        new(DecompilerSlot, Decompiler, "Item Decompiler"),
        new(CompilerSlot, CraftingSession.Tool, "Code Compiler"),
        new(AnalyserSlot, CraftingSession.WriterTool, "Code Analyser")
    };

    public StarterDecompileSkillMode() : this(cleanUi: false) { }

    protected StarterDecompileSkillMode(bool cleanUi) => Plan = BuildPlan(cleanUi);

    public override string Name => "ability-parity-starter-decompile-skill";
    public override NativeQaModePlan Plan { get; }

    private static NativeQaModePlan BuildPlan(bool cleanUi) => new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{CraftingSession.Tool},{CompilerSlot},1,0)," +
            $"(110,{CraftingSession.WriterTool},{AnalyserSlot},1,0),(110,{Decompiler},{DecompilerSlot},1,0)," +
            $"(110,{HealthPill},5,2,0),(110,{MaxHealthPill},6,1,0)",
        "CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY(charId,goid))",
        $"DELETE FROM char_recipes WHERE charId=110 AND goid IN ({HealthPill},{MaxHealthPill})"
    }, new[]
    {
        $"UPDATE characters SET x={X},y={Y},z={Z} WHERE charId=110",
        $"UPDATE char_abilities SET level={Rank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Decompile)}"
    }, DevCommands: true, hotbarAbilities: cleanUi ? null : new[] { Decompile }, hotbarInventory: cleanUi ? null : Tools,
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
    }, extraQaMetadata: new()
    {
        ["ScopedStockRows"] = "276,969,970", ["NativeCloseableRows"] = 0,
        ["UiProfile"] = cleanUi ? "stock-default" : "seeded-control"
    }, readyMessages: new[]
    {
        cleanUi
            ? "PR640 B: start the stock client with its disposable CSR cui.cfg held aside and leave every other UI setting as in A. This mode supplies no character UI seed. Open Inventory with I and try title-bar drag and hover before any shortcut assignment. Record whether the pill tooltip lights up and actual use sends 81 31."
            : "PR640 A: the standard seeded profile. Page 2 slot 1 is the ability control. The three item shortcuts are installed in slots 2–4.",
        $"CSR is at ({X},{Y},{Z}); no helpers. Start with no selection. [MODIFIER] char=110 source=276 stat=707 add value=+18 duration=0s.",
        "Use the Item Decompiler from its inventory context menu, then select Health Pill in bag slot 5 (stack of two). Expect [DECOMPILE] char=110 item=2399 skill=18 complexity=1, requiredAbove=54, success=True; one pill is consumed, 81 30 returns that recipe, 81 2f succeeds. There is no [VITALS] or [PVP] change.",
        "After the 10 s reuse, use the same tool on Max Health Boost Pill 3.0 in slot 6. Expect [DECOMPILE] item=39413 skill=18 complexity=33 requiredAbove=182 success=False. The pill stays in the bag and no recipe packet arrives. The seeded rank makes both results independent of the regular 0–100 roll.",
        "Enter ?qaunload 276. Check [MODIFIER] char=110 source=276 stat=707 remove value=+18 duration=0s and no decompile result on the remaining slot-5 pill. Enter ?qaload 276 and see its +18 add. After the reuse, use the inventory activation and slot-5 pill again. It succeeds at the same skill and uses the last pill.",
        "Use the inventory context menu's Activate Item action for Code Compiler (bag slot 1) and Code Analyser (bag slot 2). The client opens the selected window locally and sends the matching explicit activation. Close each window before opening the other. Check the 969/970 linked source on/off records and typing/standing posture. Info stays constant."
    });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        foreach (var item in Tools)
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot={item.BagSlot} " +
                $"AND goid={item.Goid} AND count=1"), $"One {item.Label} occupies its seeded shortcut slot");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.Equal(Plan.HasHotbar, actor.CharacterData.ContainsKey(QaCharacterUi.DataType),
            "Only the seeded control sends Margin character UI data");
        byte[] inventory = actor.CharacterData[5];
        Check.Equal(0, inventory.Length % 9, "Margin ordinary inventory rows are nine bytes");
        foreach (var item in Tools)
            Check.That(Enumerable.Range(0, inventory.Length / 9).Any(index =>
            {
                var row = inventory.AsSpan(index * 9, 9);
                return row[0] == item.BagSlot && BinaryPrimitives.ReadUInt32LittleEndian(row[1..]) == item.Goid &&
                    BinaryPrimitives.ReadUInt32LittleEndian(row[5..]) == AbilitySweepHotbar.SingleItemDescriptor;
            }), $"Margin inventory matches {item.Label}'s shortcut slot, GOID and instance descriptor");
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive[Decompile] == Rank && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The test rank and complete prerequisite profile reach Margin");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        long info = await db.LongAsync("SELECT cash FROM characters WHERE charId=110");
        await ModifierAsync(0, false);

        int first = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UseSlot5);
        await PacketAsync(first, "81 2f 01 05 5f 09 00 00");
        await PacketAsync(first, "81 30 5f 09 00 00 01");
        Check.Equal(1L, await CountAsync(HealthPill), "One pill remains");
        Check.Equal(1L, await LearnedAsync(HealthPill), "The recipe was written");
        await EvidenceRetestWire.TimerAsync(actor, first, Decompile, AbilityCooldowns.OwnTimer, 10, ended: true);

        int failed = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UseSlot6);
        await PacketAsync(failed, "81 2f 00 06 f5 99 00 00");
        Check.Equal(1L, await CountAsync(MaxHealthPill), "The rejected higher-complexity pill remains in inventory");
        Check.Equal(0L, await LearnedAsync(MaxHealthPill), "The rejected recipe was not written");
        string log = server.DebugLog();
        Check.That(log.Contains("item=2399 skill=18 complexity=1") && log.Contains("requiredAbove=54 success=True") &&
            log.Contains("item=39413 skill=18 complexity=33") && log.Contains("requiredAbove=182 success=False"),
            "Normal crafting uses the loaded +18 in both live contests");

        int off = actor.World.RpcSnapshot().Count;
        actor.SendChat("?qaunload 276");
        await ModifierAsync(off, true);
        int blocked = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UseSlot5);
        await PacketAsync(blocked, "81 2f 00 00 00 00 00 00");
        Check.Equal(1L, await CountAsync(HealthPill), "An unloaded ability cannot spend the input");
        int on = actor.World.RpcSnapshot().Count;
        actor.SendChat("?qaload 276");
        await ModifierAsync(on, false);
        await EvidenceRetestWire.TimerAsync(actor, failed, Decompile, AbilityCooldowns.OwnTimer, 10, ended: true);
        int again = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UseSlot5);
        await PacketAsync(again, "81 2f 01 05 5f 09 00 00");
        Check.Equal(0L, await CountAsync(HealthPill), "With the skill restored, the remaining pill is spent");
        Check.Equal(1L, await LearnedAsync(HealthPill), "Repeating a known recipe is idempotent");

        await CheckHotbarIdleAsync(CraftingSession.Ability, HotbarCompiler, "?compilesession", "Compile session inactive.");
        await CheckHotbarIdleAsync(CraftingSession.WriterAbility, HotbarWriter, "?writersession", "Writer session inactive.");

        await ToolAsync(CraftingSession.Ability, StarterCoderWriterInitializerMode.StockCompiler, () => actor.SendRpc(CloseCompiler));
        await ToolAsync(CraftingSession.WriterAbility, StarterCoderWriterInitializerMode.StockWriter,
            () => Scenarios.WriterTool(actor, AnalyserSlot, activate: false));
        Check.Equal(info, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "No crafting-tool step spends Info");
        Check.Equal(0, actor.World.Problems.Count, "Decompile proof framing");
        Console.WriteLine("DECOMPILE SKILL PASS: +18 applied to both pill contests; failed and unloaded input preserved; Compiler and Writer reopen normally.");
        return Array.Empty<byte>();

        Task<long> CountAsync(uint item) => db.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId=110 AND goid={item}");
        Task<long> LearnedAsync(uint item) => db.LongAsync($"SELECT COUNT(*) FROM char_recipes WHERE charId=110 AND goid={item}");
        Task<RpcRecord> PacketAsync(int start, string hex) => StockAbilityProfile.ObserveAsync(actor, start,
            frame => BytePattern.Parse(hex).IsMatch(frame.Bytes), hex);
        Task<RpcRecord> ModifierAsync(int start, bool removed) => StockAbilityProfile.ObserveAsync(actor, start,
            frame => StockAbilityProfile.Bonus(frame, Decompile, Skill, Rank, 0, removed), $"Decompile Skill {(removed ? "remove" : "add")}");
        async Task CheckHotbarIdleAsync(ushort ability, string request, string command, string expectedStatus)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            actor.SendChat(command);
            await actor.WaitForTextAsync(expectedStatus);
            byte[] active = AbilityBuffs.BuildExclusive(ability, true);
            Check.That(!actor.World.RpcSnapshot().Skip(start).Any(frame => frame.Bytes.AsSpan().SequenceEqual(active)),
                $"The captured flag-zero hotbar request for {ability} leaves its session inactive");
        }

        async Task ToolAsync(ushort ability, string open, Action close)
        {
            foreach (bool active in new[] { true, false })
            {
                int start = actor.World.RpcSnapshot().Count;
                if (active) actor.SendRpc(open);
                else close();
                byte[] expected = AbilityBuffs.BuildExclusive(ability, active);
                await StockAbilityProfile.ObserveAsync(actor, start, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                    $"Crafting tool {ability} active={active}");
                await StockAbilityProfile.ObserveAsync(actor, start,
                    frame => ManageBonusRecord.TryDecode(frame.Bytes, out var bonus) && bonus!.SourceId == ability &&
                        bonus.Type == AbilityBuffs.KindLinked && bonus.TargetId == CraftingSession.Attribute &&
                        bonus.AddRemoveType == (active ? 0 : 1), $"Crafting tool {ability} linked state active={active}");
            }
        }
    }
}

public sealed class StarterDecompileCleanUiMode : StarterDecompileSkillMode
{
    public StarterDecompileCleanUiMode() : base(cleanUi: true) { }

    public override string Name => "ability-parity-starter-decompile-clean-ui";
}
