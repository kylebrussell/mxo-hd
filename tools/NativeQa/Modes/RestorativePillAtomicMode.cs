using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class RestorativePillAtomicMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;
    public override string Name => "restorative-pill-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128,healthC=100,healthM=2000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,7,8);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
                (110,2399,5,2,0),(110,2399,6,1,0),(110,39410,7,2,0),(110,39410,8,1,0);
            """, Scenarios.RestorativePillConstraint(110) }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300). No target or teleport. Open Inventory. This disposable database deliberately rejects spending from bag 5 (Health Pill 1.0 x2) and bag 7 (Antibiotic 5.0 x2). The single doses in bags 6/8 are normal.",
            "Right-click bag 5 > Activate Item, then bag 7 after the first refusal. Each should cast 1 s and show 'This item could not be used. Your item was kept.' Both stacks stay at two. No heal, sweep, or reuse records should follow either failed cast.",
            "Activate bag 6, then bag 8 as soon as each cast finishes. Bag 6 disappears and the Health Pill heals 30 with 10 s item and family reuse. Bag 8 disappears, source 678 adds a sweep, and its item and family reuse are 8 s. Bags 5/7 remain at two."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Restorative QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "The Mara Central stand is on NAV");
        Check.Equal(4L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
                ((slot=5 AND goid=2399 AND count=2) OR (slot=6 AND goid=2399 AND count=1) OR
                 (slot=7 AND goid=39410 AND count=2) OR (slot=8 AND goid=39410 AND count=1))
            """), "The two fault-injected stacks and two normal doses are in the expected bag slots");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveRestorativePillAtomicAsync(actor, db, context.ActualServer!);
        return Array.Empty<byte>();
    }
}
