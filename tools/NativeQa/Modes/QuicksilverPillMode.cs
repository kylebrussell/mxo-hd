using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class QuicksilverPillMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "quicksilver-pill";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=45,y=495,rotation=128 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot=5;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47107,5,2,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport or target. Stay out of combat. Open Inventory and use the stacked Quicksilver Pill in bag 5 via the item context menu or double-click.",
            "Expect a 1 s cast, one pill left, armor presentation and a 30 s item timer. PacketLog: 80 c6 05 00 or 80 b9 fa 04 with bag 5/item flag, 80 ac fa 04, 69 01 05 00, 80 bd 01 03 b8 with 30 s.",
            "Expect 80 bc source 1274 add records for 115 Run +10 and 183/1009/1010/1011 defense +12 each for 300 s, plus self Run scale .10 and armor bit 0x0100. DebugLog: [BUFF] 1274 on for 300 s and [MODIFIER] char=110 source=1274 stat=115 add value=+10 duration=300s, each defense +12.",
            "Try the second pill before 30 s. Expect no second cast or consumption. Keep the buff up to verify the records, armor and Run scale clear at expiry if time allows."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Quicksilver QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal(45L, await db.LongAsync("SELECT level FROM characters WHERE charId=110"), "CSR meets the pill's level requirement");
        const string stock = "SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid=47107";
        Check.Equal(2L, await db.LongAsync(stock), "Both Quicksilver Pills are in bag 5");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 05 00");
        var cast = await actor.WaitForRpcAsync("80 ac fa 04");
        Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "Quicksilver casts in one second");
        await actor.WaitForRpcAsync("69 01 05 00");
        await actor.WaitForRpcAsync(WireFormat.ToHex(AbilityCooldowns.BuildTimer(47107, AbilityCooldowns.GroupTimer, 30, true)));
        await actor.WaitForViewUpdateAsync(2, "02 80 08 cd cc cc 3d");
        Check.Equal(1L, await db.LongAsync(stock), "One pill remains after a successful cast");
        return Array.Empty<byte>();
    }
}
