using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class DatamineTapShadowMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;
    private static readonly ushort[] Loadout = Scenarios.DatamineShadowLoadout();
    public override string Name => "datamine-tap-shadow";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=0,innerStrC=1000,innerStrM=1000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,107);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47276,5,1,0),(110,596,6,1,0);
            """ }, Array.Empty<string>(),
        hotbarAbilities: new[] { CombatStyles.SpyStyle, Stealth.MasterShadowAbility },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara street NAV (17000,495,6400). No target, teleport, or dev command. Bag 5 contains Datamine Tap 1 (47276). Bag 6 holds a different light luggable as a control. Page 2 slots 1/2 are Spy Style and Master Shadow.",
            "Select Spy Style and press Master Shadow at rest. It should cast in 1.5 s, add 1118/681 and turn on its silhouette. After about four seconds it turns off, removes 1118/681 and starts its normal 30 s reuse. The tap stays in the bag. DebugLog: [STEALTH] CSR_Tester 1118 off (carrying a Datamine tap).",
            "During reuse drag bag 5 out onto clear ground. Expect 5c 05 00 01, 5f 05 00 01, and the dropped tap. Leave bag 6 alone and do not pick the tap up. After reuse clears, activate Master Shadow again. Wait six seconds. It stays on and logs the ordinary 25 IS drain. Press Master Shadow again to end the control."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Datamine QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open NAV ground for the inventory drop");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The full Spy prerequisite profile fits memory");
        Check.Equal(2L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND ((slot=5 AND goid=47276) OR (slot=6 AND goid=596))
            """), "The tap and ordinary luggable are seeded");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveDatamineTapShadowAsync(actor, db, context.ActualServer!);
        return Array.Empty<byte>();
    }
}
