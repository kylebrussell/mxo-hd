using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class HealthBoostAntiStunAtomicMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;
    public override string Name => "health-boost-anti-stun-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128,healthC=350,healthM=500 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (4,5,6,7,8);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
                (110,39411,4,1,0),(110,39412,5,2,0),(110,39412,6,1,0),(110,42763,7,2,0),(110,42763,8,1,0);
            """, Scenarios.RestorativePillConstraint(110) }, Array.Empty<string>(),
        DevCommands: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300). No target or teleport. The disposable database rejects spending from bag 5 (Health Boost 2.0 x2) and bag 7 (Activity Facilitator 1.0 x2). Other doses are normal. Use Inventory > right-click > Activate Item for each step.",
            "Activate bag 4 (Health Boost 1.0). Expect +100 max HP, source 672. Activate bag 5. After the 1 s cast expect 'Your item was kept.', stack still 2, no 188 modifier or reuse, and 672 still active. Activate bag 6. It disappears, 672 removes, 188 gives +200 max HP for 600 s and starts 30 s own reuse.",
            "With Inventory open and bags 7/8 visible, type ?cd stun (fixture control only), then activate bag 7. Expect the same refusal and no cure or reuse. Activate bag 8 immediately. It disappears, Stun 315/102 clears, immunity 734 refreshes and reuse starts (971 900 s / family 973 600 s). If Stun expires before bag 8, type ?cd stun again first."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Pill QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal(5L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
                ((slot=4 AND goid=39411 AND count=1) OR (slot=5 AND goid=39412 AND count=2) OR
                 (slot=6 AND goid=39412 AND count=1) OR (slot=7 AND goid=42763 AND count=2) OR
                 (slot=8 AND goid=42763 AND count=1))
            """), "The three normal doses and two fault-injected stacks are ready");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveHealthBoostAntiStunAtomicAsync(actor, db);
        return Array.Empty<byte>();
    }
}
