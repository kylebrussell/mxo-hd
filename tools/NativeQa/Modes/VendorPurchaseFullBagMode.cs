using HeadlessClient;

namespace NativeQa.Modes;

public sealed class VendorPurchaseFullBagMode : NativeQaMode
{
    public override string Name => "vendor-purchase-full-bag";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,cash=5000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity)
            WITH RECURSIVE slots(slot) AS (SELECT 1 UNION ALL SELECT slot+1 FROM slots WHERE slot<96)
            SELECT 110,11666,slot,1,0 FROM slots;
            """ }, Array.Empty<string>(), DevCommands: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "At the Mara Central stand (17000,495,2300), type ?clientactions vendor to open the tools vendor. Find Lock Pick (1,000 Info), attempt Buy with all bag slots filled, then confirm Info stays 5,000. If the stock client refuses before 81 0e, record the local message. If it sends 81 0e, expect the full-bag modal and 81 10.",
            "Close the window, Recycle one shirt from the bag for 340 Info, then reopen the vendor and Buy Lock Pick. Expect one Lock Pick in the freed slot, Info 4,340 and [VENDOR] Bought char=110 item=4."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Vendor QA requires a disposable database.");
        Check.Equal(96L, await db.LongAsync("SELECT COUNT(DISTINCT slot) FROM inventory WHERE charId=110 AND goid=11666 AND slot BETWEEN 1 AND 96"),
            "CSR has a full bag");
        Check.Equal(5000L, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "CSR starting Info");
        using var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await helper.EnterWorldAsync();
        helper.SendChat("?clientactions vendor");
        var opened = await helper.WaitForRpcAsync("81 0d");
        Check.That(Enumerable.Range(0, BitConverter.ToUInt16(opened.Bytes, 32))
            .Any(i => BitConverter.ToUInt32(opened.Bytes, 34 + i * 4) == 4), "Tools vendor stocks the Lock Pick");
        return Array.Empty<byte>();
    }
}
