using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SmithRagePillMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, DummyX = 17200;

    public override string Name => "smith-rage-pill";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=64,healthC=5000,healthM=5000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,107);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,46994,5,2,0);
            """ }, Array.Empty<string>(), DevCommands: true, testMobFixture: "awakened-power-shot-cap.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport. CSR faces the Melee Dummy two metres east (17200,495,2300). At rest use Smith's Rage Pill from Inventory bag 5.",
            "Expect 1.5 s cast (80 ac e9 04), stack 2 to 1 and FX 28 00 00 04. Look for [MODIFIER] char=110 source=1257 for 300s with 159/171/172/1034 +11, Toughness 144 +20 and Enrage 93. There is no separate item reuse timer.",
            "Right-click the dummy and choose Close Combat. Strafe and reselect if needed. In interlock try Withdraw. Expect it blocked locally or [ENRAGE] withdraw suppressed if a 44 reaches the server.",
            "Use bag 5 again. Expect another 1.5 s cast, the six old 1257 records removed then six added, and an empty slot. Use ?die to check the six new records and Enrage clear while chat still works. Check there are at most six 80 bc records per datagram."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Smith's Rage QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _) && NavMesh.Locate(1, DummyX, Y, Z, out _) &&
            NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "CSR and the dummy have clear NAV stands");
        const string stock = "SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid=46994";
        Check.Equal(2L, await db.LongAsync(stock), "Two Smith's Rage Pills are in bag 5");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await actor.WaitForViewNamedAsync("Melee Dummy");
        actor.SendRpc("80 c6 05 00");
        var cast = await actor.WaitForRpcAsync("80 ac e9 04");
        Check.Equal(1.5f, BitConverter.ToSingle(cast.Bytes, 14), "Smith's Rage casts in 1.5 seconds");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc 11 00 ?? ?? 00 e9 04 00 00 5d 00 00 00 00 00 00 00 00 96 43");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 28 00 00 04");
        Check.Equal(1L, await db.LongAsync(stock), "One pill remains after the cast");
        return Array.Empty<byte>();
    }
}
