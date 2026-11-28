using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicLuggableTransfersMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;

    public override string Name => "luggable-transfer-rollback";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=0 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=105;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,531,3,1,0),(110,596,5,1,0),(110,47276,6,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara street NAV (17000,495,6400), no teleport, helper or dev command. Open Inventory (I). Bags 1/2 empty, Code Analyser in bag 3, light luggable in bag 5, Datamine Tap 1 in bag 6.",
            "Drag bag 5 onto the street. A disclosed disposable SQL CHECK rejects the ground insert after the bag deletion. Expect drop_rolled_back, visible 'Your item was kept.', 65 05 00 05 00 00 and cleared cursor. Bag 5 stays and no object appears.",
            "Drag bag 6 onto the street. Expect 5f 06 00 01 and one ground tap. Step aside and turn if it lands under CSR, staying within one metre of it. Select its Take radial. A disclosed trigger rejects deleting the tap after insertion in bag 1. Expect pickup_rolled_back, visible 'The luggable is still on the ground.', no bag add, and the same tap remains visible.",
            "Drag Code Analyser from bag 3 to empty bag 1. Use Take on the same tap. Expect 5e 02 00 ac b8 00 00, the tap in bag 2, and the ground view disappears. Bag 5 still has its original item."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Luggable rollback QA requires a disposable database.");
        await db.InstallLuggableTransferFailuresAsync(110);
        string data = Path.Combine(context.RepositoryRoot!, "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open NAV ground for ground transfers");
        Check.Equal(3L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND count=1 AND
                ((slot=3 AND goid=531) OR (slot=5 AND goid=596) OR (slot=6 AND goid=47276))
            """), "The starter bag contents are present");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot IN (1,2)"),
            "Two retry slots are open");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='world_luggables' AND CONSTRAINT_NAME='qa_luggable_drop'"),
            "The disclosed ground insertion fault is installed");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TRIGGERS " +
            "WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='qa_luggable_take'"),
            "The disclosed ground deletion fault is installed");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveAtomicLuggableTransfersAsync(actor, db);
        return Array.Empty<byte>();
    }
}
