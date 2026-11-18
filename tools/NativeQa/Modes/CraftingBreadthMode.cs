using System.Diagnostics;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CraftingBreadthMode : NativeQaMode
{
    private const byte AnalyzerSlot = 2, CompilerSlot = 3, MaterialSlot = 4;
    private const ushort CodeSlot = 400;
    private const uint Analyzer = 531, Compiler = 1271, CodeBit = 7336, Fragment = 7458, Pill = 39413;
    private const long InitialInfo = 100000, PillPrice = 16440;

    public override string Name => "crafting-breadth";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 37, 277 }, 23828, 13984,
        new[] { "DELETE FROM inventory WHERE charId=110 AND slot<97" },
        new[] { $"""
            INSERT INTO inventory (charId,goid,slot,count,purity) VALUES
                (110,{Analyzer},{AnalyzerSlot},1,0),(110,{Compiler},{CompilerSlot},1,0),(110,{CodeBit},{MaterialSlot},2,0);
            INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded,ability_name)
                VALUES (110,{CodeSlot},{Pill << 7},2,0,'Max Health Boost Pill 3 code');
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
            "Activate the Code Analyser in bag slot 2 to open Write Code. Select Function Subroutine: Consumable from the known recipes. "
                + "The two Code Bit 5 in slot 4 disappear and one fragment appears.",
            "Activate the Code Compiler in slot 3. The archive's Max Health Boost Pill 3 takes about 18 seconds. "
                + "Activate the Analyser in slot 2 before the timer expires to cancel, then open the Compiler and finish one." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Crafting QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Crafting QA requires an isolated server.");
        Check.Equal(3L, await db.LongAsync($"""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
                ((goid={Analyzer} AND slot={AnalyzerSlot} AND count=1) OR (goid={Compiler} AND slot={CompilerSlot} AND count=1)
                 OR (goid={CodeBit} AND slot={MaterialSlot} AND count=2))
            """), "CSR has both tools and exactly two recipe materials");
        Check.Equal(2L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id=110 AND slot={CodeSlot} AND ability_id={Pill << 7}"),
            "CSR has two charges of the timed compile code");

        using var probe = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        byte[] recipes = probe.CharacterData[0x0a];
        Check.That(Enumerable.Range(1, recipes.Length / 4 - 1).Any(i => BitConverter.ToUInt32(recipes, i * 4) == Fragment),
            "The actual Margin recipe list contains the selected fragment");
        await probe.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, probe);
        uint id = probe.CharId;
        string castOn = "80 bc ?? ?? ?? ?? 00 c9 03 00 00 c8 03 ?? ?? ?? ?? 00";
        string writerOn = "80 bc ?? ?? ?? ?? 00 ca 03 00 00 c8 03 ?? ?? ?? ?? 00";
        Scenarios.WriterTool(probe, AnalyzerSlot, true);
        await probe.WaitForRpcAsync(writerOn);
        probe.SendRpc($"81 2e {Le(Fragment << 7)} 09 00 {AnalyzerSlot:x2} 01 00 {Le(2)} {Le(CodeBit)}");
        await probe.WaitForRpcAsync($"5f {MaterialSlot:x2} 00 01");
        await probe.WaitForRpcAsync($"81 2f 01 ?? {Le(Fragment)}");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid={CodeBit}"),
            "The Writer consumed both Code Bits");
        Check.Equal(1L, await db.LongAsync($"SELECT COALESCE(SUM(GREATEST(count,1)),0) FROM inventory WHERE charId={id} AND goid={Fragment}"),
            "The Writer produced one code fragment");

        long info = await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}");
        string materialize = $"60 {Le(Pill)} {WireFormat.ToHex(BitConverter.GetBytes(CodeSlot))} 00 {CompilerSlot:x2} 01";
        Scenarios.CompilerTool(probe, CompilerSlot, true);
        await probe.WaitForRpcAsync(castOn);
        int debugStart = server.DebugLog().LastIndexOf("[COMPILE60] Started", StringComparison.Ordinal);
        probe.SendRpc(materialize);
        await probe.WaitUntilAsync(() => server.DebugLog().LastIndexOf("[COMPILE60] Started", StringComparison.Ordinal) > debugStart,
            "the timed compile to start", TimeSpan.FromSeconds(8));
        Scenarios.WriterTool(probe, AnalyzerSlot, true);
        await probe.WaitForRpcAsync("61 01");
        await probe.WaitForRpcAsync(writerOn);
        await Task.Delay(TimeSpan.FromSeconds(18.5));
        Check.Equal(info, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"), "Cancellation keeps the Info");
        Check.Equal(2L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id={id} AND slot={CodeSlot}"),
            "Cancellation keeps both charges");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid={Pill}"),
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
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid={Pill}"),
            "One pill appears in the bag");
        Check.Equal(0, probe.World.Problems.Count, "The crafting packets decode cleanly");
        Console.WriteLine($"Crafting bootstrap verified Writer material depletion, cancellation by switching tools, and one completion in {elapsed.Elapsed.TotalSeconds:F2}s.");
        return Array.Empty<byte>();
    }

    private static string Le(uint value) => WireFormat.ToHex(BitConverter.GetBytes(value));
}
