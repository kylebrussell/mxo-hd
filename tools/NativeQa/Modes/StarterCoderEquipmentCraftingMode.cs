using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderEquipmentCraftingMode : NativeQaMode
{
    private sealed record Sample(uint Goid, uint[] Ingredients, int FirstSlot, int WriterInfo, int CompileInfo);

    private static readonly Sample[] Samples =
    {
        new(671, new uint[] { 2435, 2437, 2474, 2448 }, 11, 0, 1),
        new(700, new uint[] { 2435, 2444, 2449, 2450 }, 21, 81, 108),
        new(2485, new uint[] { 2443, 2435, 2445, 2473 }, 31, 81, 108)
    };

    private const int StartingInfo = 100000;
    private const byte CompilerSlot = 1, AnalyserSlot = 2;

    public override string Name => "ability-parity-coder-equipment-crafting";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 24, 37, 275, 277, 515 }, 23828, 13984, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,1271,1,1,0),(110,531,2,1,0)," +
                string.Join(",", Samples.SelectMany(sample => sample.Ingredients.Select((goid, index) => $"(110,{goid},{sample.FirstSlot + index},1,0)"))),
            "CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY (charId,goid)) ENGINE=InnoDB",
            "INSERT IGNORE INTO char_recipes(charId,goid) VALUES " + string.Join(",", Samples.Select(sample => $"(110,{sample.Goid})")),
            "DELETE FROM char_abilities WHERE char_id=110 AND (slot=400 OR ability_id IN (" +
                string.Join(",", Samples.Select(sample => sample.Goid << 7)) + "))",
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES (110,400,{39413u << 7},1,0,'Consumable comparison code')",
            $"UPDATE characters SET y=495,cash={StartingInfo} WHERE charId=110"
        }, Array.Empty<string>(), DevCommands: true, loadAbilityAncestors: true,
        hotbarAbilities: new ushort[] { 24, 37, 275, 277, 515 },
        extraQaMetadata: new() { ["ScopedStockRows"] = "24,37,275,277,515", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "The passives and skills 24/37/275/277/515 are loaded. Activate the Code Analyser in bag slot 2. The three learned recipes are eyewear 671, revolver 700 and Program Launcher 2485. Write each once. Check two charges per new code, four selected ingredient removals per recipe, and Info debits 0/81/81 with Programmer and Code Crafter.",
            "Close the Writer and activate the Code Compiler in bag slot 1. Compile each new item code once. Check one bag item each, one remaining code charge each, and Info debits 1/108/108. All three level-one complexity recipes reach the three-second compiler minimum.",
            "Compile the seeded 39413 health pill as the consumable control. It uses both reductions (about 9 s); tools, weapons and apparel use general reduction only. Capture the Writer and Compiler requests or any missing recipe/item."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Expected a disposable Coder database.");
        Check.Equal(14L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96"),
            "The two tools and all twelve ingredients are in the disposable bag");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable Coder server.");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        Scenarios.WriterTool(actor, AnalyserSlot, activate: true);
        await actor.WaitForRpcAsync("80 bd 05 ca 03 00 00 00 00 00 01");
        int balance = StartingInfo;
        var written = new List<(Sample Sample, ushort Slot)>();
        foreach (var sample in Samples)
        {
            actor.SkipReceived();
            actor.SendRpc(WriterRequest(sample));
            byte[] receipt = CraftingRequests.BuildResult(CraftingRequests.WriterSuccess, 0, sample.Goid);
            await actor.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(receipt), $"Writer completes {sample.Goid}");
            int source = checked((int)await db.LongAsync($"SELECT slot FROM char_abilities WHERE char_id=110 AND ability_id={sample.Goid << 7} AND level=2"));
            written.Add((sample, checked((ushort)source)));
            Check.Equal(0L, await db.LongAsync(
                $"SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot BETWEEN {sample.FirstSlot} AND {sample.FirstSlot + 3}"),
                "The four selected fragments are consumed");
            balance -= sample.WriterInfo;
            Check.Equal((long)balance, await BalanceAsync(), "Writer applies the loaded Code Crafter discount");
        }

        Scenarios.WriterTool(actor, AnalyserSlot, activate: false);
        await actor.WaitForRpcAsync("80 bd 05 ca 03 00 00 00 00 00 00");
        Scenarios.CompilerTool(actor, CompilerSlot, activate: true);
        await actor.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 01");
        foreach (var (sample, slot) in written)
        {
            actor.SkipReceived();
            // Native 60 holds the selected result GOID and ordinary archive slot.
            actor.SendRpc($"60 {WireFormat.ToHex(BitConverter.GetBytes(sample.Goid))} {StockAbilityProfile.LittleEndianHex(slot)} 00 01 01");
            await actor.WaitForRpcAsync("61 00", TimeSpan.FromSeconds(10));
            Check.Equal(1L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id=110 AND slot={slot} AND ability_id={sample.Goid << 7}"),
                "A successful compile spends one of the two direct code charges");
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid={sample.Goid} AND count=1"),
                "A successful compile puts one matching item in the bag");
            balance -= sample.CompileInfo;
            Check.Equal((long)balance, await BalanceAsync(), "Compiler debits the stock category's item price");
            Check.That(server.DebugLog().Contains($"[COMPILE60] Started {sample.Goid} seconds 3 skill 50 954 25 955 0", StringComparison.Ordinal),
                "General compile reduction applies to equipment and the consumable-only modifier is excluded");
        }
        actor.SkipReceived();
        actor.SendRpc("60 f5 99 00 00 90 01 00 01 01");
        await actor.WaitForRpcAsync("61 00", TimeSpan.FromSeconds(15));
        Check.That(server.DebugLog().Contains("[COMPILE60] Started 39413 seconds 9.045 skill 50 954 25 955 33", StringComparison.Ordinal),
            "The known consumable receives both reductions");
        balance -= 14796;
        Check.Equal((long)balance, await BalanceAsync(), "Existing consumable still charges its native quote");
        Check.Equal(0, actor.World.Problems.Count, "Equipment crafting replay framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "equipment-crafting.json"),
            JsonSerializer.Serialize(new { FinalInfo = balance, Results = written.Select(entry => new { entry.Sample.Goid, entry.Slot }) }));
        return Array.Empty<byte>();

        Task<long> BalanceAsync() => db.LongAsync("SELECT cash FROM characters WHERE charId=110");
    }

    private static byte[] WriterRequest(Sample sample)
    {
        // PR304's stock 81 2e body supplies the header and the 1b 87 ingredient record.
        // Fill the recipe result, selected fragment count, and this fixture's actual bag slots/GOIDs.
        byte[] captured = WireFormat.FromHex(Scenarios.HealthPillWriterRequest);
        byte[] request = new byte[11 + 8 * sample.Ingredients.Length];
        captured.AsSpan(0, 11).CopyTo(request);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(2), sample.Goid << 7);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(9), (ushort)sample.Ingredients.Length);
        for (int i = 0; i < sample.Ingredients.Length; i++)
        {
            var record = request.AsSpan(11 + 8 * i, 8);
            captured.AsSpan(11, 8).CopyTo(record);
            BinaryPrimitives.WriteUInt16LittleEndian(record[2..], (ushort)(sample.FirstSlot + i));
            BinaryPrimitives.WriteUInt32LittleEndian(record[4..], sample.Ingredients[i]);
        }
        return request;
    }
}
