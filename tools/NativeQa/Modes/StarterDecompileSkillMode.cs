using System.Buffers.Binary;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterDecompileSkillMode : NativeQaMode
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

    public override string Name => "ability-parity-starter-decompile-skill";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
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
    }, DevCommands: true, hotbarAbilities: new[] { Decompile }, hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[]
    {
        new(DecompilerSlot, Decompiler, "Item Decompiler"),
        new(CompilerSlot, CraftingSession.Tool, "Code Compiler"),
        new(AnalyserSlot, CraftingSession.WriterTool, "Code Analyser")
    }, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "276,969,970", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
    {
        $"CSR is at ({X},{Y},{Z}); no helpers. Start with no selection. [MODIFIER] char=110 source=276 stat=707 add value=+18 duration=0s. Page 2 slot 1 is the ability-only control. The three actual item shortcuts are already installed in slots 2–4.",
        "Press page 2 slot 2 to arm the Item Decompiler, then click Health Pill in bag slot 5 (stack of two). Expect [DECOMPILE] char=110 item=2399 skill=18 complexity=1, requiredAbove=54, success=True; one pill is consumed, 81 30 returns that recipe, 81 2f succeeds. If the target cursor appears but the bag still does not accept the pill click, record those separately. There is no [VITALS] or [PVP] change.",
        "After the 10 s reuse, use the same tool on Max Health Boost Pill 3.0 in slot 6. Expect [DECOMPILE] item=39413 skill=18 complexity=33 requiredAbove=182 success=False. The pill stays in the bag and no recipe packet arrives. The seeded rank makes both results independent of the regular 0–100 roll.",
        "Enter ?qaunload 276. Check [MODIFIER] char=110 source=276 stat=707 remove value=+18 duration=0s and no decompile result on the remaining slot-5 pill. Enter ?qaload 276 and see its +18 add. After the reuse, use the item shortcut and slot-5 pill again. It succeeds at the same skill and uses the last pill.",
        "Press page 2 slot 3 for Code Compiler and slot 4 for Code Analyser. Close each window before opening the other. This step never needs a bag click. Check the 969/970 linked source on/off records and typing/standing posture. Info stays constant."
    });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        foreach (var item in Plan.HotbarInventory)
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot={item.BagSlot} " +
                $"AND goid={item.Goid} AND count=1"), $"One {item.Label} occupies its seeded shortcut slot");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        byte[] inventory = actor.CharacterData[5];
        Check.Equal(0, inventory.Length % 9, "Margin ordinary inventory rows are nine bytes");
        foreach (var item in Plan.HotbarInventory)
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

        await ToolAsync(CraftingSession.Ability, active => Scenarios.CompilerTool(actor, CompilerSlot, active));
        await ToolAsync(CraftingSession.WriterAbility, active => Scenarios.WriterTool(actor, AnalyserSlot, active));
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
        async Task ToolAsync(ushort ability, Action<bool> send)
        {
            foreach (bool active in new[] { true, false })
            {
                int start = actor.World.RpcSnapshot().Count;
                send(active);
                byte[] expected = AbilityBuffs.BuildExclusive(ability, active);
                await StockAbilityProfile.ObserveAsync(actor, start, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                    $"Crafting tool {ability} active={active}");
            }
        }
    }
}
