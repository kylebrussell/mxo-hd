using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class EventHealthRegenItemsMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;
    public override string Name => "event-health-regen-items";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128,healthC=100,healthM=1000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,46995,5,2,0),(110,47013,6,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), 100/1000 HP at login. Stay standing and out of combat. Baseline +20 HP per 2 s. No teleport or target. Open Inventory.",
            "Right-click bag 5 (Oracle's Anniversary Cookie) > Activate Item. Expect 1.5 s cast, stack 2 to 1, FX 27 00 00 04 and source 1258 stat 112 +75 for 60 s. Regen rises to +35 HP/tick. Wait for two ticks before the next dose.",
            "Activate bag 6 (Patcher's Pill). Expect 1.5 s cast, empty slot, FX 2a 00 00 04 and source 1263 stat 112 +30 for 300 s. While both are active expect +41 HP/tick. Neither item starts a separate reuse timer.",
            "At 60 s the Cookie removes its single 1258 record. Patcher's Pill remains active. If HP is still below cap after expiry, expect +26 HP/tick. No virus event is seeded. Verify a chat reply after Cookie expiry."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Regen QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal(2L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
                ((slot=5 AND goid=46995 AND count=2) OR (slot=6 AND goid=47013 AND count=1))
            """), "Both regeneration consumables are seeded");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveEventHealthRegenAsync(actor, db);
        return Array.Empty<byte>();
    }
}
