using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MarketplaceArchiveCodeMode : NativeQaMode
{
    public override string Name => "marketplace-archive-code";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { $"""
            UPDATE characters SET y=495,rotation=128,cash=2000 WHERE charId=110;
            DELETE FROM char_abilities WHERE char_id=110 AND slot=396;
            INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
            VALUES(110,396,{CodeStorage.BaseValue(4)},0,0,'QA Lock Pick code');
            """ }, new[] { $"""
            DELETE FROM char_abilities WHERE slot=396 AND char_id=(SELECT charId FROM characters WHERE handle='QAFriend');
            INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
            SELECT charId,396,{CodeStorage.BaseValue(4)},0,0,'QA Lock Pick code' FROM characters WHERE handle='QAFriend';
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "From Mara Central (17000,495,2300), use the booth radial > Loading Area. Drag the Lock Pick code from Marketplace Possessions into Selling and list for 1,200 Info. The code leaves the archive and Fee 36 takes Info to 1,964. The listing starts 81 2b 49 54 4c f0, source container 0, slot 396.",
            "Search Codes > Item Codes > Tools > Lockpicks. Buy QAFriend's 1,000 Info listing. Expect 964 Info left and a one-charge code. Equipment > Code Archive > Compile should give 61 00, one Lock Pick in the bag and 844 Info. [MARKET] logs archive=True, slot=384. [COMPILE60] logs item=4 cost=120 remaining=0.",
            "Return to Marketplace and De-list CSR's 1,200 Info listing for its recorded fee of 36. Expect a one-charge archive entry and 808 Info. Re-list at 1,200 from slot 384 and expect 772 Info."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Marketplace QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, 17000, 495, 2300, out _), "Mara Central stand has NAV ground");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND slot=396 AND ability_id={CodeStorage.BaseValue(4)} AND level=0"),
            "CSR has the flagged one-charge Lock Pick code");
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
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE id={listing} AND is_sold=0 AND itemID=46 AND purity=513 AND price=1000 AND delistPrice=30"),
            "The helper listed an actual archive code through the captured request");
        Console.WriteLine($"QAFriend's Lock Pick code listing {listing} is available for 1,000 Info before CSR lists their code.");
        return Array.Empty<byte>();
    }
}
