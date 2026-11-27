using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class TacticBoostConsumablesMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "tactic-boost-consumables";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=45,y=495,rotation=128 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,7,8);
            INSERT INTO inventory(charId,goid,slot,count,purity)
            VALUES (110,8322,5,2,0),(110,8323,6,1,0),(110,8324,7,1,0),(110,8325,8,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport or target. Open Inventory. Out of combat, Use/Activate the Tactic Booster in bag 5 or double-click it. Expect a 1 s cast, one booster left and [MODIFIER] source=548 stat=163 add value=+5 duration=600s. Wait about six seconds to see the pulse FX.",
            "Activate bag 6. Expect the same cast length, bag 6 empty, source 548 +5 removed and 549 +10 added for 600 s.",
            "Try the remaining bag 5 booster while 549 is active. If stock sends the request, expect 'A stronger Tactic Boost is already active.' In either case, no cast, no spend and +10 stays.",
            "Activate bag 7 then bag 8. Expect 549 +10 removed and 550 +15 added, then 550 removed and 551 +20 added, all stat 163 for 600 s. Only the newest boost should be active. PacketLog: 80 ac 24/25/26/27 02 for the four casts and 5f 06/07/08 00 01 for the single items."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Tactic Boost QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal(45L, await db.LongAsync("SELECT level FROM characters WHERE charId=110"), "CSR meets all four level gates");
        Check.Equal(5L, await db.LongAsync("SELECT SUM(count) FROM inventory WHERE charId=110 AND goid=slot+8317 AND slot BETWEEN 5 AND 8"),
            "The four Tactic Booster tiers are in bags 5 through 8, with two of tier 1");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 05 00");
        var cast = await actor.WaitForRpcAsync("80 ac 24 02");
        Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "Tier 1 casts in one second");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc 15 00 ?? ?? 00 24 02 00 00 a3 00 05 00 00 00 00 00 00 16 44");
        Check.Equal(1L, await db.LongAsync("SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid=8322"), "One tier 1 booster remains");
        return Array.Empty<byte>();
    }
}
