using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    public const int RefusedListingPrice = 1100, RetryListingPrice = 1200;
    public const int ListingSourceSlot = 396;
    public const string ListingFaultName = "qa_marketplace_listing_price";
    private const int ListingCash = 2000;

    public static string MarketplaceListingSourceSql(uint character) => $"""
        DELETE FROM char_abilities WHERE char_id={character} AND slot={ListingSourceSlot};
        INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
        VALUES({character},{ListingSourceSlot},{CodeStorage.BaseValue(4)},0,0,'QA Lock Pick code');
        """;

    public static string MarketplaceListingFaultSql =>
        $"ALTER TABLE marketplace ADD CONSTRAINT {ListingFaultName} CHECK (price<>{RefusedListingPrice});";

    private static async Task MarketplaceListingRefusal(ScenarioContext context)
    {
        var seller = (await context.AddPlayersInWorldAsync(new CharacterSeed("ListingSeller") { Info = ListingCash }))[0];
        var db = context.Database;
        await db.ExecuteAsync(MarketplaceListingSourceSql(seller.CharId));
        await db.ExecuteAsync(MarketplaceListingFaultSql);
        await ProveMarketplaceListingRetryAsync(seller, db);
    }

    public static async Task ProveMarketplaceListingRetryAsync(HeadlessPlayer seller, ThrowawayDatabase db)
    {
        uint character = seller.CharId;
        Task<long> SourceCount() => db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={character} " +
            $"AND slot={ListingSourceSlot} AND ability_id={CodeStorage.BaseValue(4)} AND level=0 AND is_loaded=0");
        Task<long> ListingCount() => db.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE charID={character}");
        Task<long> Cash() => db.LongAsync($"SELECT cash FROM characters WHERE charId={character}");
        string slot = Le((ushort)ListingSourceSlot);
        // Stock Place Item leaves ClientUIData zero. Packet tests also exercise a nonzero echo.
        const string stockUi = "00 00 00 00";
        string Place(uint price) => $"81 2b 49 54 4c f0 {Le(price)} 00 {slot} {stockUi}";
        string refusal = $"81 2c {Le(0u)} {Le(0u)} {Le(0u)} {Le(0u)} {Le(0u)} 00 {slot} {stockUi} 9c 02 00 0c";

        seller.SendRpc(Place(RefusedListingPrice));
        await seller.WaitForRpcAsync(refusal);
        await seller.WaitForTextAsync("List archive codes from the Loading Area marketplace.");
        Check.Equal(1L, await SourceCount(), "Wrong-world refusal keeps the code");

        await GoToLoadingAreaAsync(seller);
        seller.SendRpc("81 29 00 00 00 00 01");
        await seller.WaitForRpcAsync("81 2a 09 00 00 00 00 00 00 00 00");
        int before = seller.World.RpcSnapshot().Count;
        seller.SendRpc(Place(RefusedListingPrice));
        await seller.WaitForRpcAsync(refusal);
        await seller.WaitForTextAsync("The listing could not be posted. Your item was kept.");
        Check.Equal(1L, await SourceCount(), "Refused write keeps the code and its charge");
        Check.Equal(0L, await ListingCount(), "Refused write leaves no listing");
        Check.Equal((long)ListingCash, await Cash(), "Refused write keeps the wallet");
        Check.That(!seller.World.RpcSnapshot().Skip(before).Any(r => r.Bytes is [0x5f, 0x8c, 0x01, 0x00, ..]),
            "Refusal publishes no archive removal");

        seller.SendRpc(Place(RetryListingPrice));
        await seller.WaitForRpcAsync($"5f {slot} 00");
        var listed = await seller.WaitForRpcAsync($"81 2c ?? ?? ?? ?? 2e 00 00 00 01 02 00 00 " +
            $"{Le((uint)RetryListingPrice)} 1e 00 00 00 00 {slot} {stockUi} 00 00 00 00");
        uint id = BitConverter.ToUInt32(listed.Bytes, 2);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE id={id} AND charID={character} " +
            $"AND is_sold=0 AND itemID=46 AND purity=513 AND price={RetryListingPrice}"), "Retry posts one owned code");
        Check.Equal(0L, await SourceCount(), "Retry removes the archive source");
        Check.Equal((long)ListingCash, await Cash(), "Posting keeps the wallet");

        seller.SendRpc(Place(RetryListingPrice));
        await seller.WaitForRpcAsync(refusal);
        await seller.WaitForTextAsync("There is no item in that marketplace source slot.");
        Check.Equal(1L, await ListingCount(), "Stale retry cannot post a duplicate");
    }
}
