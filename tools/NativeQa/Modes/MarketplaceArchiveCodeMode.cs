using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MarketplaceArchiveCodeMode : NativeQaMode
{
    public override string Name => "marketplace-archive-code";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=128,cash=2000 WHERE charId=110;
            DELETE FROM char_abilities WHERE char_id=110 AND slot=396;
            INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
            VALUES(110,396,512,1,0,'QA Lock Pick code');
            """ }, new[] { """
            DELETE FROM char_abilities WHERE slot=396 AND char_id=(SELECT charId FROM characters WHERE handle='QAFriend');
            INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
            SELECT charId,396,512,1,0,'QA Lock Pick code' FROM characters WHERE handle='QAFriend';
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "From the Mara Central stand (17000,495,2300), use the booth radial > Loading Area. In Marketplace's listing pane, select the Lock Pick code from the Code Archive source and list it for 1,200 Info. Confirm the archive row leaves and My Listings shows a Lock Pick code at 1,200. The captured listing request starts 81 2b 49 54 4c f0; the source is container 0, slot 396.",
            "Search Lock Picks and Buy the 1,000 Info code. Confirm Info 2,000 to 1,000 and one Lock Pick code in the archive with one charge. Check the bag does not gain an item. Optionally De-list your own 1,200 Info code, which costs 60 Info and restores a second one-charge entry. [MARKET] checked out logs archive=True, slot, cost and cash."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Marketplace QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, 17000, 495, 2300, out _), "Mara Central stand has NAV ground");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND slot=396 AND ability_id=512 AND level=1"),
            "CSR has the known one-charge Lock Pick code");
        using var seller = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await seller.EnterWorldAsync();
        seller.SendRpc("80 fc 01");
        await seller.WaitForRpcAsync("80 fd");
        seller.SendRpc("80 fe 01");
        await seller.WaitForRpcAsync("81 07");
        seller.SendRpc("81 08 00 00 00 00");
        await seller.WaitForLoadingAreaAsync();
        seller.SendRpc("05");
        seller.SendRpc(Scenarios.ListLockPickCode);
        await seller.WaitForRpcAsync("5f 8c 01 00");
        var reply = await seller.WaitForRpcAsync("81 2c ?? ?? ?? ?? 2e 00 00 00 01 02 00 00");
        uint listing = BitConverter.ToUInt32(reply.Bytes, 2);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE id={listing} AND is_sold=0 AND itemID=46 AND purity=513 AND price=1000"),
            "The helper listed an actual archive code through the captured request");
        Console.WriteLine($"QAFriend's Lock Pick code listing {listing} is available for 1,000 Info before CSR lists their code.");
        return Array.Empty<byte>();
    }
}
