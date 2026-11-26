using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    public const string ListLockPickCode = "81 2b 49 54 4c f0 e8 03 00 00 00 8c 01 00 00 00 00";

    private static async Task VendorPurchaseFullBag(ScenarioContext context)
    {
        var buyer = (await context.AddPlayersInWorldAsync(new CharacterSeed("VendorBuyer")
        {
            X = 17000, Y = 495, Z = 2300, Info = 5000,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, 11666u, 1)).ToArray()
        }))[0];
        async Task RequireCash(long expected) => Check.Equal(expected, await context.Database.LongAsync(
            $"SELECT cash FROM characters WHERE charId={buyer.CharId}"), "Vendor wallet");

        buyer.SendChat("?clientactions vendor");
        await buyer.WaitForRpcAsync("81 0d");
        const string buy = "81 0e 04 00 00 00 00";
        buyer.SendRpc(buy);
        await buyer.WaitForTextAsync("Your inventory is full.");
        await buyer.WaitForRpcAsync("81 10 00 00 00 00 00");
        await RequireCash(5000);
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={buyer.CharId} AND goid=4"),
            "Full bag grants no Lock Pick");

        buyer.SendRpc("5d 0b 00 01 01");
        await buyer.WaitForRpcAsync("5f 0b 00 01");
        await RequireCash(5340);
        await context.Database.ExecuteAsync(
            "ALTER TABLE characters ADD CONSTRAINT vendor_buy_wallet CHECK (handle<>'VendorBuyer' OR cash>=5000)");
        buyer.SendRpc(buy);
        await buyer.WaitForTextAsync("The purchase could not be completed");
        await buyer.WaitForRpcAsync("81 10 00 00 00 00 00");
        await RequireCash(5340);
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={buyer.CharId} AND slot=11"),
            "A failed wallet update rolled back the delivery");
        await context.Database.ExecuteAsync("ALTER TABLE characters DROP CONSTRAINT vendor_buy_wallet");

        buyer.SendRpc(buy);
        await buyer.WaitForRpcAsync("5e 0b 00 04 00 00 00 00 00 00 10 01");
        await buyer.WaitForRpcAsync("81 0f 04 00 00 00 00 00 00 10 e8 03 00 00 00");
        await RequireCash(4340);
        Check.Equal(4L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={buyer.CharId} AND slot=11"),
            "One paid purchase filled the freed slot");
    }

    private static async Task MarketplaceArchiveCode(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("CodeSeller") { Info = 1000 },
            new CharacterSeed("CodeBuyer") { X = 17120, Info = 2000 },
            new CharacterSeed("CodeFull") { X = 17220, Info = 2000 });
        var seller = players[0];
        var buyer = players[1];
        var full = players[2];
        var db = context.Database;
        await db.ExecuteAsync($"""
            INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({seller.CharId},396,512,1,0);
            DELETE FROM char_abilities WHERE char_id={full.CharId} AND slot<520;
            INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded)
            WITH RECURSIVE slots(slot) AS (SELECT 0 UNION ALL SELECT slot+1 FROM slots WHERE slot<519)
            SELECT {full.CharId},slot,512,1,0 FROM slots;
            """);
        await GoToLoadingAreaAsync(seller);
        await GoToLoadingAreaAsync(buyer);
        await GoToLoadingAreaAsync(full);

        await db.ExecuteAsync("ALTER TABLE marketplace ADD CONSTRAINT refuse_lock_pick CHECK (price<1000)");
        seller.SendRpc(ListLockPickCode);
        await seller.WaitForTextAsync("Your item was kept.");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={seller.CharId} AND slot=396 AND ability_id=512 AND level=1"),
            "Failed listing insert rolls back the archive removal");
        await db.ExecuteAsync("ALTER TABLE marketplace DROP CONSTRAINT refuse_lock_pick");

        seller.SendRpc(ListLockPickCode);
        await seller.WaitForRpcAsync("5f 8c 01 00");
        var listed = await seller.WaitForRpcAsync("81 2c ?? ?? ?? ?? 2e 00 00 00 01 02 00 00 e8 03 00 00 1e 00 00 00 00 8c 01");
        uint listing = BitConverter.ToUInt32(listed.Bytes, 2);
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={seller.CharId} AND slot=396"),
            "Posting the captured archive request removes the owned code");
        seller.SendRpc(ListLockPickCode);
        await seller.WaitForTextAsync("no item in that marketplace source slot");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE charID={seller.CharId}"), "Stale listing request posts once");

        buyer.SendRpc("81 24 49 54 4c 70 00 00 00 00 00 00 00 00 19 00 ff c9 9a 3b 00 00 00 00 01 00 00");
        var search = await buyer.WaitForRpcAsync("81 25 09 00 00 00 00 00 00 16 00 2e 00 00 00 01 02 00 00");
        Check.Equal(listing, BitConverter.ToUInt32(search.Bytes, 19), "Ordinary Lock Pick search includes the code");

        string buy = $"81 26 {Le(listing)} 07";
        full.SendRpc(buy);
        await full.WaitForRpcAsync($"81 27 {Le(listing)} 07 00 00 00 00 00 00 00 00 00 00 00 00 c8 02 00 0c");
        Check.Equal(2000L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={full.CharId}"), "Archive-full refusal does not charge");
        Check.Equal(0L, await db.LongAsync($"SELECT is_sold FROM marketplace WHERE id={listing}"), "Refused listing stays available");

        buyer.SendRpc(buy);
        await buyer.WaitForRpcAsync("5e ?? ?? 00 00 00 00 01 02 00 00 00");
        await buyer.WaitForRpcAsync("80 e4 e8 03 00 00");
        await buyer.WaitForRpcAsync($"81 27 {Le(listing)} 07 2e 00 00 00 01 02 00 00 e8 03 00 00 00 00 00 00");
        await seller.WaitForRpcAsync("80 e4 d0 07 00 00");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={buyer.CharId} AND ability_id=512 AND level=1 AND is_loaded=0"),
            "The full code descriptor appears in the buyer's archive");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={buyer.CharId} AND goid=46"),
            "Archive delivery creates no bag entry");
        Check.Equal(1000L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={buyer.CharId}"), "Buyer paid the asking price");
        Check.Equal(2000L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={seller.CharId}"), "Seller received the asking price");

        buyer.SendRpc(buy);
        await buyer.WaitForRpcAsync($"81 27 {Le(listing)} 07 00 00 00 00 00 00 00 00 00 00 00 00 98 02 00 0c");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={buyer.CharId} AND ability_id=512"),
            "A stale buy cannot grant a second code");
    }
}
