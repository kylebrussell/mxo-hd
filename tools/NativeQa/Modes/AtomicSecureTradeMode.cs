using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AtomicSecureTradeMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, PartnerZ = 6520;
    private const string Partner = SecureTradeMode.Partner;

    public override string Name => "secure-trade-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=0,cash=1000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,7332,3,1,0);
            """ }, new[] { """
            UPDATE characters SET x=17000,y=495,z=6520,cash=500 WHERE handle='QAFriend';
            DELETE FROM inventory WHERE charId=(SELECT charId FROM characters WHERE handle='QAFriend') AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity)
                SELECT charId,7333,4,2,0 FROM characters WHERE handle='QAFriend';
            """, Scenarios.TradeRollbackFixtureSql },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara street NAV CSR (17000,495,6400), QAFriend (17000,495,6520). No teleport or dev commands. Use /target QAFriend and turn if needed. CSR bag 3 holds one Code Bit 1 and CSR starts at 1,000 Info. QAFriend is headless with 500 Info.",
            "QA-only SQL fault in the disposable DB rejects a QAFriend balance of 300 to force the second wallet write to fail after the inventory changes. The fixture can retry at a different amount.",
            """Drag CSR bag 3 onto QAFriend. While the trade is open submit {"actor":"QAFriend","action":"trade","step":"add-info","information":200}, then {"actor":"QAFriend","action":"trade","step":"accept"}. Once their offer is visible click CSR Accept.""",
            "Expect both windows to close (81 87 23 00 00 0e) and visible 'Your items and information were kept.' CSR still has bag 3 and 1,000 Info. DebugLog: [TRADE] Settlement rolled back. No 5f, 5e, 80 e4 or completion from the refusal.",
            "Open again with bag 3. Repeat helper add-info with 250, then helper accept, then click CSR Accept. Expect 81 87 19 00 00 8e, bag 3 empty, CSR 1,250 Info and QAFriend 250 Info. QAFriend receives one Bit. Confirm one [TRADE] Complete."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Atomic trade QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(OnStreet(Z) && OnStreet(PartnerZ), "Both starts are on open Mara NAV");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=3 AND goid=7332"),
            "CSR has the outgoing Bit in slot 3");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='qa_trade_second_wallet'"),
            "The disposable database has the second-wallet fault");

        var partner = await HeadlessPlayer.LoginAsync(context.Address, Partner, Partner);
        context.Helpers.Add(Partner, partner);
        await partner.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, partner);
        Check.That(partner.World.CurrentOwnViewLife().OwnViewReady, "QAFriend entered the world");
        if (context.StockProbe is { } owner)
        {
            await owner.EnterWorldAsync();
            await Scenarios.ProveSecureTradeAtomicAsync(owner, partner, db);
        }
        return Array.Empty<byte>();
    }

    private static bool OnStreet(int z) =>
        NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y;
}
