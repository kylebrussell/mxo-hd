using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AtomicBagStacksMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;

    public override string Name => "bag-stacks-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=0 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,512,1,1,0),(110,531,2,1,0),(110,7332,3,10,0);
            """, Scenarios.SplitStackFaultSql }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara NAV (17000,495,6400). No travel, target, helper, or dev commands. Slots 1/2 are occupied, slot 3 is ten Code Bit 1s, and slot 4 is the first empty bag slot.",
            "Right-click bag 3 > Split Quantity... > Choose Amount: 4 > OK, then click empty bag 4. Expect 68 03 04 04 00 00 00 00. A disclosed SQL CHECK in the disposable database fails the second slot write only at bag 4 x4. Expect 'Your items were kept.', bag 3 stays x10, and bag 4 stays empty. DebugLog: [INVENTORY] Stack move rolled back. Source correction 69 01 count 24 hex, no 5e to bag 4.",
            "Repeat Split Quantity with amount 3 into bag 4. Expect 69 01 for bag 3 x7 and 5e for bag 4 x3. Normal left-drag bag 4 onto bag 3 to merge. Expect 67 04 00 03 00 00, bag 3 x10 and bag 4 removed with 5f. DebugLog has one Split committed and one Merge committed."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Atomic stack QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open Mara NAV ground");
        Check.Equal(3L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
              ((slot=1 AND goid=512) OR (slot=2 AND goid=531) OR (slot=3 AND goid=7332 AND count=10))
            """), "CSR has the expected bag order and split stack");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=4"),
            "The fault slot starts empty");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='inventory' AND CONSTRAINT_NAME='qa_split_destination'"),
            "The disposable inventory has the split fault");
        if (context.StockProbe is { } player)
        {
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
            await Scenarios.ProveAtomicBagStacksAsync(player, db);
        }
        return Array.Empty<byte>();
    }
}
