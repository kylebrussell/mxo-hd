using System.Diagnostics;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CraftingBreadthMode : NativeQaMode
{
    private const byte AnalyzerSlot = 2, CompilerSlot = 3, FirstMaterialSlot = 4;
    private const ushort CodeSlot = 400;
    private const uint Analyzer = 531, Compiler = 1271, WriterPill = 2399, BoostPill = 39413;
    private const int WriterDefaultCharges = 10;
    private const long InitialInfo = 100000, PillPrice = 16440;

    private static string MaterialRows => string.Join(',', Scenarios.HealthPillMaterials
        .Select((ingredient, index) => $"(110,{ingredient},{FirstMaterialSlot + index},1,0)"));
    private static string MaterialSlots => string.Join(" OR ", Scenarios.HealthPillMaterials
        .Select((ingredient, index) => $"(goid={ingredient} AND slot={FirstMaterialSlot + index} AND count=1)"));

    public override string Name => "crafting-breadth";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 37, 277 }, 23828, 13984,
        new[] { "DELETE FROM inventory WHERE charId=110 AND slot<97" },
        new[] { $"""
            INSERT INTO inventory (charId,goid,slot,count,purity) VALUES
                (110,{Analyzer},{AnalyzerSlot},1,0),(110,{Compiler},{CompilerSlot},1,0),{MaterialRows};
            INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded,ability_name)
                VALUES (110,{CodeSlot},{BoostPill << 7},2,0,'Max Health Boost Pill 3 code');
            UPDATE char_abilities SET level=50 WHERE char_id=110 AND ability_id={0x80000000u | 277u << 10};
            UPDATE characters SET cash={InitialInfo},y=495 WHERE charId=110;
            """ },
        new[] { """
            INSERT INTO inventory (charId,goid,slot,count,purity)
                SELECT c.charId,i.goid,i.slot,i.count,i.purity FROM inventory i
                JOIN characters c ON c.handle='QAFriend' WHERE i.charId=110 AND i.slot<97;
            """ },
        environmentOverrides: new() { ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Activate the Analyser in bag slot 2 and write Consumables > Consumable Pills > Health Pill 1.0. "
                + "Five ingredients leave slots 4 through 8 and code with 10 charges joins the Compiler list.",
            "Activate the Compiler in slot 3 and compile the new Health Pill 1.0 code. Expect one pill after about "
                + "three seconds and 9 charges remaining. The client has no working mid-compile cancel control." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Crafting QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Crafting QA requires an isolated server.");
        Check.Equal(7L, await db.LongAsync($"""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
                ((goid={Analyzer} AND slot={AnalyzerSlot} AND count=1) OR
                 (goid={Compiler} AND slot={CompilerSlot} AND count=1) OR {MaterialSlots})
            """), "CSR has both tools and all five Health Pill 1.0 ingredients");
        Check.Equal(2L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id=110 AND slot={CodeSlot} AND ability_id={BoostPill << 7}"),
            "CSR has two charges of the timed compile code");

        using var probe = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        byte[] recipes = probe.CharacterData[0x0a];
        Check.That(Enumerable.Range(1, recipes.Length / 4 - 1).Any(i => BitConverter.ToUInt32(recipes, i * 4) == WriterPill),
            "The actual Margin recipe list contains Health Pill 1.0");
        await probe.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, probe);
        uint id = probe.CharId;
        string castOn = "80 bc ?? ?? ?? ?? 00 c9 03 00 00 c8 03 ?? ?? ?? ?? 00";
        string writerOn = "80 bc ?? ?? ?? ?? 00 ca 03 00 00 c8 03 ?? ?? ?? ?? 00";
        Scenarios.WriterTool(probe, AnalyzerSlot, true);
        await probe.WaitForRpcAsync(writerOn);
        var staleSelection = WireFormat.FromHex(Scenarios.HealthPillWriterRequest);
        staleSelection[^6] = 7; // The last ingredient claims slot 7, which holds a different GOID.
        probe.SendRpc(staleSelection);
        await probe.WaitForRpcAsync($"81 2f 05 00 {Le(WriterPill)}");
        Check.Equal(5L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND ({MaterialSlots})"),
            "A stale selected slot rolls back all material consumption");
        Check.Equal(InitialInfo, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),
            "A stale selected slot does not charge Info");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={id} AND ability_id={WriterPill << 7}"),
            "A stale selected slot grants no code");

        probe.SkipReceived();
        probe.SendRpc(Scenarios.HealthPillWriterRequest);
        for (int slot = FirstMaterialSlot; slot < FirstMaterialSlot + Scenarios.HealthPillMaterials.Count; slot++)
            await probe.WaitForRpcAsync($"5f {slot:x2} 00 01");
        var written = await probe.WaitForRpcAsync($"5e ?? ?? 00 00 00 00 {Le(WriterPill << 7 | WriterDefaultCharges)} 00");
        ushort writerCodeSlot = BitConverter.ToUInt16(written.Bytes, 1);
        await probe.WaitForRpcAsync($"81 2f 00 00 {Le(WriterPill)}");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND ({MaterialSlots})"),
            "Writing consumes all five ingredients");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid={WriterPill}"),
            "The Writer creates a code");
        Check.Equal((long)WriterDefaultCharges, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id={id} " +
            $"AND slot={writerCodeSlot} AND ability_id={WriterPill << 7}"), "Written code persists with the stored default charges");
        long writtenInfo = await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}");
        probe.SkipReceived();
        probe.SendRpc(Scenarios.HealthPillWriterRequest);
        await probe.WaitForRpcAsync($"81 2f 05 00 {Le(WriterPill)}");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={id} AND ability_id={WriterPill << 7}"),
            "Replaying without ingredients cannot produce another code");
        Check.Equal(writtenInfo, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),
            "Replaying without ingredients does not charge Info");

        Scenarios.CompilerTool(probe, CompilerSlot, true);
        await probe.WaitForRpcAsync(castOn);
        probe.SendRpc(Materialize(WriterPill, writerCodeSlot));
        await probe.WaitForRpcAsync("61 00", TimeSpan.FromSeconds(15));
        Check.Equal(1L, await db.LongAsync($"SELECT COALESCE(SUM(GREATEST(count,1)),0) FROM inventory WHERE charId={id} AND goid={WriterPill}"),
            "The Compiler materializes one pill from the written code");
        Check.Equal((long)WriterDefaultCharges - 1, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id={id} AND slot={writerCodeSlot}"),
            "The Compiler uses one of the written code's charges");
        Check.Equal(writtenInfo - 9, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),
            "Health Pill 1.0 materialization costs 9 Info");

        long info = await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}");
        string materialize = Materialize(BoostPill, CodeSlot);
        probe.SkipReceived();
        int debugStart = server.DebugLog().LastIndexOf("[COMPILE60] Started", StringComparison.Ordinal);
        probe.SendRpc(materialize);
        await probe.WaitUntilAsync(() => server.DebugLog().LastIndexOf("[COMPILE60] Started", StringComparison.Ordinal) > debugStart,
            "the timed compile to start", TimeSpan.FromSeconds(8));
        Scenarios.CompilerTool(probe, CompilerSlot, false);
        await probe.WaitForRpcAsync("61 01");
        await Task.Delay(TimeSpan.FromSeconds(18.5));
        Check.Equal(info, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"), "Cancellation keeps the Info");
        Check.Equal(2L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id={id} AND slot={CodeSlot}"),
            "Cancellation keeps both charges");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid={BoostPill}"),
            "Cancellation leaves no delayed output");

        probe.SkipReceived();
        Scenarios.CompilerTool(probe, CompilerSlot, true);
        await probe.WaitForRpcAsync(castOn);
        var elapsed = Stopwatch.StartNew();
        probe.SendRpc(materialize);
        await probe.WaitForRpcAsync("61 00", TimeSpan.FromSeconds(28));
        Check.That(elapsed.Elapsed.TotalSeconds >= 17.8, "The timed item waits about 18 seconds");
        Check.Equal(info - PillPrice, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"), "One materialization debits 16440 Info");
        Check.Equal(1L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id={id} AND slot={CodeSlot}"),
            "One materialization consumes one charge");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid={BoostPill}"),
            "One pill appears in the bag");
        Check.Equal(0, probe.World.Problems.Count, "The crafting packets decode cleanly");
        Console.WriteLine($"Crafting bootstrap verified the captured Writer request, exact-slot rollback, new-code compile, flag-zero rollback, and Health Boost completion in {elapsed.Elapsed.TotalSeconds:F2}s.");
        return Array.Empty<byte>();
    }

    private static string Materialize(uint item, ushort slot) =>
        $"60 {Le(item)} {WireFormat.ToHex(BitConverter.GetBytes(slot))} 00 {CompilerSlot:x2} 01";

    private static string Le(uint value) => WireFormat.ToHex(BitConverter.GetBytes(value));
}
