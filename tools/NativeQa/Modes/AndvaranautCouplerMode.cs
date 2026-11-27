using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AndvaranautCouplerMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "andvaranaut-coupler";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot=5;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47927,5,2,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300). Stay at rest. No teleport or target. Activate Andvaranaut Coupler from Inventory bag 5.",
            "Expect immediate use, stack 2 to 1 and a 120 s item timer keyed to 47927 (37 bb). Save the first request. Either 80 c6 05 00 or item-bearing 80 b9 61 05 / 63 05 is accepted.",
            "Expect [MODIFIER] char=110 source=1379 for 60s. Damage 159/171/172/1034 +12 each and resistance 157/180/181/1033 +29 each. Activation FX b7 00 00 04, then b5 00 00 04 about every 10 s.",
            "Try the second dose before reuse clears. Expect a local wait or 80 ba reuse denial and one dose left. At 60 s the eight records remove and the FX stops. The item timer continues to 120 s. Check ACKs and a chat reply after expiry."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Coupler QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal(1L, await db.LongAsync("""
            SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND is_loaded=1 AND level>=1
                AND (ability_id & 0xffffffff)=2147485696
            """), "CSR has loaded Awakened rank one or above");
        const string stock = "SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid=47927";
        Check.Equal(2L, await db.LongAsync(stock), "Both Couplers are in bag 5");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync(WireFormat.ToHex(AbilityCooldowns.BuildTimer(47927, AbilityCooldowns.GroupTimer, 120, true)));
        await actor.WaitForRpcAsync("80 bc 15 00 ?? ?? 00 63 05 00 00 9f 00 0c 00 00 00 00 00 00 70 42");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 b7 00 00 04");
        Check.Equal(1L, await db.LongAsync(stock), "One Coupler remains after use");
        return Array.Empty<byte>();
    }
}
