using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class InnerStrengthBoosterMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "inner-strength-booster";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128,innerStrC=100,innerStrM=1500 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot=5;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47363,5,2,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport or target. Stay out of combat. CSR starts at 100/1500 IS. Let two or three ordinary +8 IS ticks pass.",
            "Activate the Inner Strength Booster in bag 5 via its Inventory menu or double-click. Expect a 1 s cast, one booster left, and [MODIFIER] char=110 source=1333 stat=114 add value=+20 duration=1200s. A visual pulse should recur about every 4 s.",
            "Watch six consecutive IS updates. The last minus the first should be 48, with +9/+10 per tick. PacketLog own IS update starts 02 00 02 80 80 80 10 then u16 IS. No instant grant is expected.",
            "If the buff control accepts Shift+right-click, dismiss the booster. Expect [MODIFIER] 1333/114 remove and subsequent +8 ticks. Check the second booster stays in bag 5. Stock request is 80 bb 35 05."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Booster QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal("100 1500", await db.ScalarAsync("SELECT CONCAT(innerStrC,' ',innerStrM) FROM characters WHERE charId=110"),
            "CSR's IS is below the cap with room for natural regeneration");
        const string stock = "SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid=47363";
        Check.Equal(2L, await db.LongAsync(stock), "Two boosters are in bag 5");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 05 00");
        var cast = await actor.WaitForRpcAsync("80 ac 35 05");
        Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "A booster casts for one second");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc 15 00 ?? ?? 00 35 05 00 00 72 00 14 00 00 00 00 00 00 96 44");
        Check.Equal(1L, await db.LongAsync(stock), "The second booster remains after the cast");
        return Array.Empty<byte>();
    }
}
