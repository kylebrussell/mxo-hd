using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AtomicClothingEquipmentMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;

    public override string Name => "clothing-equipment-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=0 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=105;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
                (110,48122,3,1,0),(110,48123,4,1,0),(110,5910,99,1,0),(110,6389,102,1,0);
            """, Scenarios.ClothingEquipmentRsiSql(110), Scenarios.ClothingEquipmentFaultSql(110) }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV (17000,495,6400). No teleport, target, helper or dev command. Open Inventory (I). CSR wears the original shirt (model/color 1/15) and pants. Bags 1/2 are empty. Bags 3/4 hold the two Recapture shirts.",
            "Double-click bag 3. A disclosed CHECK on the disposable RSI row rejects color 40 after both shirt moves. Expect [INVENTORY] Clothing move rolled back, visible 'Your items were kept.', same-slot 65 03 00 03 00 00, and no appearance update. Bag 1 stays empty and both shirts stay put.",
            "Double-click bag 4. Expect 65 63 00 01 00 00 then 65 04 00 63 00 00, one own-view appearance update, and the new shirt (model/color 5/41) on CSR. The original shirt goes to bag 1. Pants and hair stay the same.",
            "Double-click the worn shirt in the paper doll. Expect 64 63 00, 65 63 00 02 00 00, and one appearance update. The shirt goes to bag 2 and the paper-doll slot clears. Bag 3 still has its Recapture shirt. Optional relog checks that the shirt stays off."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Atomic clothing equipment QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open Mara NAV ground");
        Check.Equal(4L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND count=1 AND
              ((slot=3 AND goid=48122) OR (slot=4 AND goid=48123) OR
                (slot=99 AND goid=5910) OR (slot=102 AND goid=6389))
            """), "The expected shirts and pants are in place");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot IN (1,2)"),
            "Bags 1 and 2 are empty");
        Check.Equal("1 15 2", await db.ScalarAsync(
            "SELECT CONCAT(shirt,' ',shirtcolor,' ',hair) FROM rsivalues WHERE charid=110"), "The initial outfit matches the worn shirt");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='rsivalues' AND CONSTRAINT_NAME='qa_outfit_rsi'"),
            "The fixture has the disclosed RSI constraint");
        if (context.StockProbe is { } player)
        {
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
            await Scenarios.ProveAtomicClothingEquipmentAsync(player, db);
        }
        return Array.Empty<byte>();
    }
}
