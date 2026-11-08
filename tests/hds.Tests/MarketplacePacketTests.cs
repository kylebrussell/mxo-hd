using System;
using System.Collections.Generic;
using hds;
using hds.shared;

namespace hds.Tests;

// Expected bytes are retail, from research/packet-dumps/community-intake/
// 20260606T201844Z-hdneo-mxopackets1-vendor-marketplace-selected and 20260607T013506Z-hdneo-mxopackets1-marketplace-vendor-selected
// (files named crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_* / *_request_open_marketplace_* / *_buy_*).
public class MarketplacePacketTests
{
    private static byte[] Hex(string hex) => StringUtils.hexStringToBytes(hex.Replace(" ", ""));
    private static string H(byte[] bytes) => Convert.ToHexString(bytes);
    private static string H(string hex) => hex.Replace(" ", "").ToUpperInvariant();

    private static MarketListing L(uint id, uint goid, uint descriptor, uint price, byte rep, uint created) =>
        new MarketListing {Id = id, GoId = goid, Descriptor = descriptor, Price = price, SellerRep = rep, Created = created};

    [Fact]
    public void BrowseResultMatchesRetailPants()
    {
        // marketplace_apeareal_pants_2_items_listed_: two pants listings.
        var rows = new List<MarketListing>
        {
            L(0x0012d1cb, 0x8c41, 0x18000000, 2000, 2, 0x4a6e22a8),
            L(0x0012d201, 0x5843, 0x34000000, 6000, 2, 0x4a6e5e65)
        };
        Assert.Equal(H("81 25 09 00 00 00 00 00 00 2c 00 41 8c 00 00 00 00 00 18 cb d1 12 00 d0 07 00 00 02 a8 22 6e 4a 00 43 58 00 00 00 00 00 34 01 d2 12 00 70 17 00 00 02 65 5e 6e 4a 00"),
            H(MarketplacePackets.BrowseResult(false, rows)));
    }

    [Fact]
    public void EmptyBrowseAndMorePagesFlagMatchRetail()
    {
        // marketplace_gloves (nothing listed); marketplace_rifles started "81 25 09 00 01 00 00 00 00 26 02" (25 rows, more).
        Assert.Equal(H("81 25 09 00 00 00 00 00 00 00 00"), H(MarketplacePackets.BrowseResult(false, new List<MarketListing>())));
        var page = new List<MarketListing>();
        for (int i = 0; i < 25; i++) page.Add(L((uint) i, 1, 0, 1, 0, 0));
        Assert.Equal(H("81 25 09 00 01 00 00 00 00 26 02"), H(MarketplacePackets.BrowseResult(true, page).AsSpan(0, 11).ToArray()));
    }

    [Fact]
    public void MyListingsMatchesRetailOpen()
    {
        // request_open_marketplace_loadingarea: one listing at 2,000,000 (delist price 100,000), without the sample's
        // 00 after the descriptor (the real client read the fields one byte early with it).
        var listing = new MarketListing {Id = 0x0012d186, GoId = 0x9af0, Descriptor = 0x04000000, Price = 2000000, DelistPrice = 100000, Created = 0x4a6e0830};
        Assert.Equal(H("81 2a 09 00 00 00 00 00 00 18 00 f0 9a 00 00 00 00 00 04 86 d1 12 00 80 84 1e 00 a0 86 01 00 30 08 6e 4a"),
            H(MarketplacePackets.MyListings(new List<MarketListing> {listing})));
        Assert.Equal(100000u, MarketplacePackets.DelistPriceFor(2000000));
    }

    [Fact]
    public void ListedMatchesRetailSwatDisguise()
    {
        // marketplace_place_swat_disguise_to_sell_for_1000_info.
        var listing = new MarketListing {Id = 0x0012d247, GoId = 0x04f4, Descriptor = 0x24000000, Price = 1000};
        Assert.Equal(H("81 2c 47 d2 12 00 f4 04 00 00 00 00 00 24 e8 03 00 00 1e 00 00 00 01 1a 00 00 00 00 00 00 00 00 00"),
            H(MarketplacePackets.Listed(listing, 1, 26)));
    }

    [Fact]
    public void BoughtMatchesRetailTShirt()
    {
        // buy_shielded_happy_grass_g-met_t-shirt_from_marketplace_for_6000_info.
        var listing = new MarketListing {Id = 0x0012d1ca, GoId = 0x8ce0, Descriptor = 0x38000000, Price = 6000};
        Assert.Equal(H("81 27 ca d1 12 00 04 e0 8c 00 00 00 00 00 38 70 17 00 00 00 00 00 00"),
            H(MarketplacePackets.Bought(listing, 4)));
        // The same purchase: the shirt went to slot 4 and info dropped by 6000 with reason 5.
        Assert.Equal(H("5e 04 00 e0 8c 00 00 00 00 00 38 01"), H(ServerPackets.BuildInventoryItemAdd(4, 0x8ce0, 0x00, 0x38)));
        Assert.Equal(H("80 e7 90 e8 ff ff ff ff ff ff 05 00 00 00"), H(ServerPackets.BuildInfoDelta(-6000, MarketplacePackets.InfoReasonMarketplace)));
    }

    [Fact]
    public void ParsesRetailRequests()
    {
        Assert.True(MarketplaceRequests.TryParseSearch(Hex("49 41 50 53 00 00 00 00 00 00 00 00 19 00 ff c9 9a 3b 00 00 00 00 01 00 00"), out var search));
        Assert.Equal("IAPS", MarketplaceRequests.CategoryText(search.Category));
        Assert.Equal(25, search.PageSize);
        Assert.Equal(999999999u, search.MaxPrice);
        Assert.Equal(0u, search.Start);

        Assert.True(MarketplaceRequests.TryParsePlacement(Hex("49 54 4f 74 e8 03 00 00 01 1a 00 00 00 00 00"), out var place));
        Assert.Equal("ITOt", MarketplaceRequests.CategoryText(place.Category));
        Assert.Equal(1000u, place.Price);
        Assert.Equal(1, place.Container);
        Assert.Equal(26, place.Slot);

        Assert.True(MarketplaceRequests.TryParseBuy(Hex("ca d1 12 00 04"), out uint listingId, out byte slot));
        Assert.Equal(0x0012d1cau, listingId);
        Assert.Equal(4, slot);
    }

    [Fact]
    public void CodeListingShowsInItsSearchCategory()
    {
        // Placed as "ITL\xf0" (lockpick code), searched as "ITLp".
        uint placed = BitConverter.ToUInt32(Hex("49 54 4c f0"), 0);
        uint searched = BitConverter.ToUInt32(Hex("49 54 4c 70"), 0);
        Assert.True(MarketplaceRequests.SameCategory(placed, searched));
        Assert.False(MarketplaceRequests.SameCategory(placed, BitConverter.ToUInt32(Hex("49 54 4f 74"), 0)));
    }
}
