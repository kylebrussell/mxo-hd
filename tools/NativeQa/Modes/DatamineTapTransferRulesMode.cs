using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class DatamineTapTransferRulesMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendZ = 6520;
    private const string Partner = "QAFriend";

    public override string Name => "datamine-tap-transfer-rules";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=0,cash=500 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
                (110,7332,3,1,0),(110,7333,4,1,0),(110,47276,5,1,0);
            """ }, new[] { """
            UPDATE characters SET x=17000,y=495,z=6520,cash=50 WHERE handle='QAFriend';
            DELETE FROM inventory WHERE charId=(SELECT charId FROM characters WHERE handle='QAFriend') AND slot<=96;
            DELETE FROM mail WHERE ownerCharId=(SELECT charId FROM characters WHERE handle='QAFriend');
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara street NAV CSR (17000,495,6400), QAFriend (17000,495,6520). No teleport, dev command, or helper action. Use /target QAFriend to locate the helper and turn toward them if needed. Bag 3/4 hold Code Bits 1/2, bag 5 is Datamine Tap 1.",
            "Drag bag 5 onto QAFriend. A sent 81 82 should get 81 83 status 04 00 00 18 and leave the bag and trade window alone. Note if the stock client stops the drag locally.",
            "Drag Code Bit 1 from bag 3 onto QAFriend to open Secure Trade. Drag bag 5 into Your Items. If the client sends 81 85 action 0, expect 'This item cannot be traded.' and only the Code Bit in the offer. Cancel the window normally.",
            "Right-click Code Bit 2 in bag 4 > Email this item. Enter QAFriend, subject Bit control and a nonempty body. Send without Info. Expect 81 e6 success and only bag 4 emptied.",
            "From a fresh compose, try right-click bag 5 > Email this item. If that option is absent, record the local refusal. If the client attaches it, send to QAFriend with subject Tap refusal, a nonempty body and 200 Info. Expect 'This item cannot be attached to mail.', 81 e6 status 68 00 00 0e, 500 Info and bag 5 unchanged."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Datamine transfer QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(OnStreet(X, Y, Z) && OnStreet(X, Y, FriendZ), "Both trade stands are on open Mara NAV ground");
        Check.Equal(3L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
              ((slot=3 AND goid=7332) OR (slot=4 AND goid=7333) OR (slot=5 AND goid=47276))
            """), "CSR has the two Code Bits and the tap");

        var partner = await HeadlessPlayer.LoginAsync(context.Address, Partner, Partner);
        context.Helpers.Add(Partner, partner);
        await partner.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, partner);
        if (context.StockProbe is { } holder)
        {
            await holder.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, holder);
            await Scenarios.ProveDatamineTapTransferRulesAsync(holder, partner, db);
        }
        return Array.Empty<byte>();
    }

    private static bool OnStreet(int x, int y, int z) =>
        NavMesh.Locate(1, x, y, z, out var stand) && !stand.indoors && stand.floorY + 95 == y;
}
