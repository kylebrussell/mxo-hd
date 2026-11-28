using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MarketplaceListingRefusalMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;
    private const uint Seller = 110;

    public override string Name => "marketplace-listing-refusal";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"UPDATE characters SET y={Y},rotation=128,cash=2000 WHERE charId={Seller};",
            Scenarios.MarketplaceListingSourceSql(Seller), Scenarios.MarketplaceListingFaultSql },
        Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "CSR stands at verified Mara Central NAV (17000,495,2300). Use the booth radial > Loading Area. Open Marketplace. The Lock Pick code in Possessions has one charge, archive slot 396.",
            $"Drag that code into Selling and list for {Scenarios.RefusedListingPrice} Info. The disclosed disposable SQL CHECK refuses this price. Expect [MARKET] Listing transaction rolled back, 81 2c with status 9c 02 00 0c, and 'The listing could not be posted. Your item was kept.' The code remains, Selling has no posted row and Info stays 2,000.",
            $"Dismiss any popup. In the same Marketplace session, use the same source to list for {Scenarios.RetryListingPrice} Info. Drag it from Possessions again if the source selection cleared. Expect 5f 8c 01 00 then 81 2c with status zero. The code leaves Possessions, one listing shows at {Scenarios.RetryListingPrice}, and Info remains 2,000. Close and reopen Marketplace to check the listing persists."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "Mara Central has a verified outdoor hardline stand");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={Seller} " +
            $"AND slot={Scenarios.ListingSourceSlot} AND ability_id={CodeStorage.BaseValue(4)} AND level=0 AND is_loaded=0"),
            "CSR has one owned Lock Pick charge");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE charID={Seller}"),
            "CSR starts with no listings");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='marketplace' AND CONSTRAINT_NAME='{Scenarios.ListingFaultName}'"),
            "The disclosed listing-price CHECK is armed");

        if (context.StockProbe is not { } seller) return Array.Empty<byte>();
        await seller.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, seller);
        await Scenarios.ProveMarketplaceListingRetryAsync(seller, db);
        return Array.Empty<byte>();
    }
}
