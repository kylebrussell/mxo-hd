using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class VendorAbilityBuyRefusalMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "vendor-ability-buy-refusal";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,cash=40000 WHERE charId=110;
            DELETE FROM char_abilities WHERE char_id=110 AND slot<520
                AND (ability_id & 0xFC000000)=0x80000000 AND (((ability_id >> 10) & 4095)=5);
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity)
            WITH RECURSIVE slots(slot) AS (SELECT 1 UNION ALL SELECT slot+1 FROM slots WHERE slot<96)
            SELECT 110,11666,slot,1,0 FROM slots;
            """ }, Array.Empty<string>(), DevCommands: true,
        environmentOverrides: new() { ["MXO_ABILITY_ACQUISITION_QA"] = "1" },
        readyMessages: new[] {
            "CSR starts on verified Mara Central NAV (17000,495,2300), with 40,000 Info and all 96 bag slots filled with shirts. Type ?abvendor to open the existing ability vendor. The disclosed disposable-only command opens its ordinary window.",
            "Select Calm Mind Calm Body (40,000) and click Buy. Expect 'Your inventory is full.' in chat and 81 10 00 00 00 00 00. Verify the wallet stays at 40,000 and the vendor remains responsive.",
            "Close the vendor. Recycle the shirt in raw bag slot 11 for 340 Info. Type ?abvendor and buy Calm Mind Calm Body again. Expect one ability code in slot 11, 81 0f, and 340 Info. Stay in the same login."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "Mara Central is on outdoor NAV");
        Check.Equal(96L, await db.LongAsync("SELECT COUNT(DISTINCT slot) FROM inventory WHERE charId=110 AND goid=11666 AND slot BETWEEN 1 AND 96"),
            "All 96 bag slots hold the fixture shirts");
        Check.Equal(40000L, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "The wallet covers one ability code");
        if (context.StockProbe is not { } buyer) return Array.Empty<byte>();
        await buyer.EnterWorldAsync();
        await Scenarios.ProveVendorAbilityBuyRefusalAsync(buyer, db, useQaOpener: true);
        return Array.Empty<byte>();
    }
}
