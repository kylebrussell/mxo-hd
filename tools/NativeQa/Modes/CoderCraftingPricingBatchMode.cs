using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class CoderCraftingPricingBatchMode : NativeQaMode
{
    private const ushort MinorRepairs = 24, WriteCode = 37, Programmer = 275, CodeCrafter = 515;
    private static readonly ushort[] Loadout = Profile(2, 17, MinorRepairs, WriteCode, 277, CodeCrafter).Order().ToArray();
    private static string Ingredients => string.Join(',', Scenarios.HealthPillMaterials.Select((goid, index) => $"(110,{goid},{index + 4},1,0)"));
    // Stock Compile60 request for Health Boost 3.0 from the PR185 fixture, paid from archive slot 400 with tool slot 3.
    private const string Compile = "60 f5 99 00 00 90 01 00 03 01";

    public override string Name => "ability-parity-coder-pricing-and-timing";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 23828, 13984, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot < 97",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,531,2,1,0),(110,1271,3,1,0),{Ingredients}",
        "DELETE FROM char_abilities WHERE char_id=110 AND slot=400",
        $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(110,400,{39413u << 7},3,0,'Health Boost 3.0 code')",
        "UPDATE characters SET cash=44394,y=495 WHERE charId=110"
    }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "24,37,515", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Unload 24 and 515. Activate Compiler from bag slot 3 and compile one Health Boost 3.0. Check 18s and 14,796 Info. Close the Compiler between attempts.",
            "Load 24 and compile the second charge: 12.06s. Load 515 too and compile the last charge: 9.045s. Each compilation spends 14,796 Info, leaving exactly 6 Info and three pills.",
            "Use ?qaunload 275 to remove the general discount. With 37 and 515 still loaded and 6 Info, activate Code Analyser in bag slot 2 and attempt Health Pill 1.0 using the five prepared ingredients. The server refuses at the 6.75 Info price, leaving all materials and the 6 Info intact. Capture what the stock UI shows. If it suppresses the request, record that gate."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var database = context.OwnedDatabase!;
        var server = context.ActualServer!;
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.Equal((ushort)15, rows[WriteCode].Level, "Write Code uses its native training cap");
        await actor.EnterWorldAsync();
        await ChangeLoadoutAsync(actor, rows, new[] { MinorRepairs, CodeCrafter }, load: false);
        await CompileAsync("18 skill 50 954 0 955 0", 18, 29598);
        await ChangeLoadoutAsync(actor, rows, new[] { MinorRepairs }, load: true);
        await CompileAsync("12.06 skill 50 954 0 955 33", 12.06, 14802);
        await ChangeLoadoutAsync(actor, rows, new[] { CodeCrafter }, load: true);
        await CompileAsync("9.045 skill 50 954 25 955 33", 9.045, 6);
        Check.Equal(3L, await database.LongAsync("SELECT SUM(count) FROM inventory WHERE charId=110 AND goid=39413"), "Three ordinary charges give three physical pills");
        Check.Equal(0L, await database.LongAsync("SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND slot=400"), "The last compiler charge is removed");

        int start = await ChangeLoadoutAsync(actor, rows, new[] { Programmer }, load: false);
        await ObserveAsync(actor, start, rpc => Bonus(rpc, Programmer, 530, 10, 0, remove: true),
            "Programmer's 10% discount is removed for the fractional Writer boundary");
        start = actor.World.RpcSnapshot().Count;
        Scenarios.WriterTool(actor, 2, true);
        await ObserveAsync(actor, start, CraftingToggle(970, active: true), "Writer opens on its ordinary grant");
        actor.SendRpc(Scenarios.HealthPillWriterRequest);
        var noInfo = BytePattern.Parse("81 2f 04 00 5f 09 00 00");
        await ObserveAsync(actor, start, rpc => noInfo.IsMatch(rpc.Bytes), "The full 6.75 Info quote refuses the 6 Info wallet");
        Check.Equal(6L, await database.LongAsync("SELECT cash FROM characters WHERE charId=110"), "Refused Writer does not debit Info");
        Check.Equal(5L, await database.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot BETWEEN 4 AND 8 AND count=1"), "Refused Writer preserves each selected ingredient");
        Check.Equal(0L, await database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND ability_id={2399u << 7}"), "Refused Writer creates no code");
        Check.Equal(0, actor.World.Problems.Count, "Compiler and Writer framing");
        return Array.Empty<byte>();

        async Task CompileAsync(string timing, double seconds, long balance)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            Scenarios.CompilerTool(actor, 3, true);
            await ObserveAsync(actor, cursor, CraftingToggle(969, active: true), "Compiler opens on the ordinary grant");
            string start = "[COMPILE60] Started 39413 seconds " + timing + " cost 14796";
            cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(Compile);
            var finished = BytePattern.Parse("61 00");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(rpc => finished.IsMatch(rpc.Bytes)),
                $"Ordinary compiler finishes after the {seconds}s sourced UI schedule", TimeSpan.FromSeconds(seconds + 5));
            Check.That(server.DebugLog().Contains(start), $"Compiler used {timing} and ordinary price");
            Check.Equal(balance, await database.LongAsync("SELECT cash FROM characters WHERE charId=110"), "Compiler debits the advertised price once");
            cursor = actor.World.RpcSnapshot().Count;
            Scenarios.CompilerTool(actor, 3, false);
            await ObserveAsync(actor, cursor, CraftingToggle(969, active: false), "Compiler closes before changing the loadout");
        }
    }

    private static Func<RpcRecord, bool> CraftingToggle(ushort ability, bool active)
    {
        var pattern = BytePattern.Parse($"80 bd 05 {LittleEndianHex(ability)} 00 00 00 00 00 {(active ? "01" : "00")}");
        return packet => pattern.IsMatch(packet.Bytes);
    }
}
