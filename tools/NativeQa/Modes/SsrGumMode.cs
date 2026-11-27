using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SsrGumMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "ssr-gum";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=45,y=495,rotation=128 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47360,5,2,0),(110,47107,6,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport or target. Stay out of combat. Activate SSR Gum in bag 5 via the Inventory item menu.",
            "Expect a 1 s cast and one gum left. PacketLog: 80 c6 05 00 or 80 b9 2d 05 with bag 5/item flag, 80 ac 2d 05, 69 01 05 00, self Run scale 02 80 08 cd cc 4c 3d. Gum has no separate reuse timer.",
            "Expect [BUFF] 1325 on for 60 s and [MODIFIER] char=110 source=1325 adds for 60 s: 524 +1, 564 -1, 638 +10, 575 -1, 519 +2, 1044 +1, 522 +1, 209 -5, 115 +5, 1032 +1, 342 +1, 157 -4. Look for the gum FX every 4 s (84 00 00 04 in the self view).",
            "While gum is active, activate Quicksilver Pill in bag 6. Run scale goes to .15 (9a 99 19 3e). Let gum expire after 60 s. All twelve gum records remove and the gum FX stops. Run scale drops to .10 (cd cc cc 3d) while Quicksilver stays active."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("SSR Gum QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        const string stock = "SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid=47360";
        Check.Equal(2L, await db.LongAsync(stock), "Two pieces of gum are in bag 5");
        Check.Equal(1L, await db.LongAsync("SELECT count FROM inventory WHERE charId=110 AND slot=6 AND goid=47107"),
            "The Quicksilver control is in bag 6");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 05 00");
        var cast = await actor.WaitForRpcAsync("80 ac 2d 05");
        Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "Gum casts in one second");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc 15 00 ?? ?? 00 2d 05 00 00 73 00 05 00 00 00 00 00 00 70 42");
        await actor.WaitForViewUpdateAsync(2, "02 80 08 cd cc 4c 3d");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 84 00 00 04");
        Check.Equal(1L, await db.LongAsync(stock), "One piece remains after the cast");
        return Array.Empty<byte>();
    }
}
