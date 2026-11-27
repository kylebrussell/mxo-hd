using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class DatamineTapCarryFxMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;

    public override string Name => "datamine-tap-carry-fx";
    public override NativeQaModePlan Plan { get; } = new(Array.Empty<ushort>(), X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=0 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,107);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47276,5,1,0),(110,596,6,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara street NAV (17000,495,6400). No teleport, target, or dev command. Bag 5 is Datamine Tap 1, bag 6 is a different light luggable.",
            "Enter world and wait for two pulses. Expect a repeating tap effect on CSR about four seconds apart, [DATA-MINING] tap FX char=110 effect=0x04000078, and self-view FX bytes 78 00 00 04.",
            "Drag bag 5 out onto the street. Expect 5c 05 00 01, 5f 05 00 01, and the ground tap. Leave bag 6 alone. Wait at least six seconds and check there are no more tap pulses.",
            "Select the ground tap and use its Pick Up radial. Step aside and turn to see it if it landed under CSR, staying within reach. Expect 80 c7 with action 04 and a 5e add for ac b8 00 00. Wait for two more pulses four seconds apart."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Datamine QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open NAV ground for the inventory drop and pickup");
        Check.Equal(2L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND ((slot=5 AND goid=47276) OR (slot=6 AND goid=596))
            """), "The tap and ordinary luggable are seeded");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveDatamineTapCarryFxAsync(actor, db);
        return Array.Empty<byte>();
    }
}
