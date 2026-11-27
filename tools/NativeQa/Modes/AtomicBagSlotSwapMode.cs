using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AtomicBagSlotSwapMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;

    public override string Name => "bag-slot-swap-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=0 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,512,3,1,0),(110,2399,4,2,0),(110,531,5,1,0);
            """, Scenarios.BagSwapFaultSql(110) }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara street verified NAV (17000,495,6400). No teleport, target, helper, or dev command. Bag 3: Lock Pick. Bag 4: two Antibiotics. Bag 5: Code Analyser. Bag 6 empty.",
            "Plain left-drag bag 3 onto occupied bag 4. A disclosed CHECK in the disposable DB rejects the second update when the pills would move to bag 3. Expect [INVENTORY] Slot move rolled back, visible 'Your items were kept.', and same-slot 65 03 00 03 00 00. The drag cursor clears. Bags 3 and 4 stay exactly as seeded.",
            "Drag bag 3 onto occupied bag 5. Expect 65 03 00 05 00 00, bag 3 is Code Analyser and bag 5 is Lock Pick. The pills stay in bag 4.",
            "Drag bag 5 to empty bag 6. Expect 65 05 00 06 00 00, bag 5 empty, Lock Pick in 6."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Atomic bag swap QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open Mara NAV ground");
        Check.Equal(3L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
              ((slot=3 AND goid=512) OR (slot=4 AND goid=2399 AND count=2) OR (slot=5 AND goid=531))
            """), "The fixture contains the three intended rows");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=6"), "Bag 6 is empty");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='inventory' AND CONSTRAINT_NAME='qa_swap_second_slot'"),
            "Fixture DB has the disclosed second-slot constraint");
        if (context.StockProbe is { } player)
        {
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
            await Scenarios.ProveAtomicBagSlotSwapAsync(player, db);
        }
        return Array.Empty<byte>();
    }
}
