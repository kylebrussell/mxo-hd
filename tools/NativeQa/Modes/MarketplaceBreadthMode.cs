using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;
using hds.databases;

namespace NativeQa.Modes;

public sealed class MarketplaceBreadthMode : NativeQaMode
{
    private const string Seller = "QAFriend";
    private const string SecondBuyer = "QAOther";
    private const uint ItemCategory = 0x30424346; // FCB0, the native Code Bit search category
    private const uint FirstItem = 7332, SecondItem = 7333;
    private const uint FirstPrice = 300, SecondPrice = 420;
    private const uint StartingCash = 1000;
    private const byte SecondBuyerSlot = 20;
    private uint firstListing, secondListing;
    private HeadlessPlayer? loadingAreaSeller;

    internal override bool HelperConnectionReady(HeadlessPlayer helper) =>
        ReferenceEquals(helper, loadingAreaSeller)
            ? !helper.World.CurrentOwnViewLife().OwnViewReady
            : base.HelperConnectionReady(helper);

    public override string Name => "marketplace-breadth";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { $"""
            UPDATE characters SET y=495,cash={StartingCash} WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;
            """ },
        new[] { $"""
            UPDATE characters SET x=17120,y=495,z=2300,cash={StartingCash} WHERE handle='{Seller}';
            UPDATE characters SET x=17220,y=495,z=2300,cash={StartingCash} WHERE handle='{SecondBuyer}';
            DELETE FROM inventory WHERE charId IN (SELECT charId FROM characters WHERE handle IN ('{Seller}','{SecondBuyer}'))
                AND slot BETWEEN 1 AND 96;
            INSERT INTO inventory (charId,goid,slot,count,purity)
                SELECT charId,{FirstItem},5,1,0 FROM characters WHERE handle='{Seller}'
                UNION ALL
                SELECT charId,{SecondItem},6,1,0 FROM characters WHERE handle='{Seller}';
            """ },
        readyMessages: new[] { "Marketplace QA has two verified listings from QAFriend in Loading Area. "
            + "Search Codes > Code Fragments > Code Bit, buy Code Bit 1 at 300, and use QAOther to consume the 420 listing for the stale-listing check. "
            + "See docs/GAMEPLAY-BREADTH-PEERS.md." });

    private sealed record Listings(uint First, uint Second);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Marketplace QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Marketplace QA requires a live server.");
        foreach (string name in new[] { Seller, SecondBuyer })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var seller = context.Helpers[Seller];
        var buyer = context.Helpers[SecondBuyer];
        loadingAreaSeller = null;
        await EnterLoadingAreaAsync(seller);
        firstListing = await ListAsync(seller, FirstItem, FirstPrice, 5);
        secondListing = await ListAsync(seller, SecondItem, SecondPrice, 6);
        // Both listing replies followed the real Loading Area handshake on this same connection.
        loadingAreaSeller = seller;
        Check.Equal(2L, await database.LongAsync($"""
            SELECT COUNT(*) FROM marketplace WHERE charID={seller.CharId} AND category='{MarketplaceDb.CategoryKey(ItemCategory)}' AND is_sold=0 AND
              ((id={firstListing} AND itemID={FirstItem} AND price={FirstPrice} AND delistPrice={FirstPrice / 20}) OR
               (id={secondListing} AND itemID={SecondItem} AND price={SecondPrice} AND delistPrice={SecondPrice / 20}));
            """), "Both native-format listings have the expected price and delist fee");
        Check.Equal(0L, await database.LongAsync($"""
            SELECT COUNT(*) FROM inventory WHERE charId={seller.CharId} AND slot IN (5,6);
            """), "Listing removed both items from the seller bag");
        buyer.SendRpc($"81 24 {Le(ItemCategory)} {Le(0)} 00 00 00 00 19 00 {Le(999999999)} {Le(0)} 01 00 00");
        var search = await buyer.WaitForRpcAsync("81 25 09 00");
        Check.That(HasListing(search.Bytes, firstListing, FirstItem, FirstPrice) &&
            HasListing(search.Bytes, secondListing, SecondItem, SecondPrice), "Ordinary search returned both fixture listings");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "marketplace-listings.json"),
            JsonSerializer.Serialize(new Listings(firstListing, secondListing)));
        return Array.Empty<byte>();
    }

    public async Task<object> SendActionAsync(HeadlessPlayer actor, JsonElement request,
        IReadOnlyDictionary<string, HeadlessPlayer> helpers, ThrowawayDatabase database)
    {
        string? step = request.GetProperty("step").GetString();
        if (step == "buy-stale" && actor.Handle == SecondBuyer)
        {
            Check.Equal(1L, await database.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE id={secondListing} AND is_sold=0"),
                "The stale-check listing is still available");
            actor.SendRpc($"81 26 {Le(secondListing)} {SecondBuyerSlot:x2}");
            await actor.WaitForRpcAsync($"81 27 {Le(secondListing)} {SecondBuyerSlot:x2} {Le(SecondItem)}");
            return "QAOther bought the selected listing";
        }
        if (step != "verify-sales" || actor.Handle != Seller)
            throw new ArgumentException("Use buy-stale with QAOther or verify-sales with QAFriend.");

        uint sellerCash = StartingCash + FirstPrice + SecondPrice;
        await actor.WaitForRpcAsync($"80 e4 {Le(sellerCash)}");
        await actor.WaitForTextAsync("sold for 420");
        Check.Equal(2L, await database.LongAsync($"SELECT COUNT(*) FROM marketplace WHERE id IN ({firstListing},{secondListing}) AND is_sold=1"),
            "Each listing sold once");
        Check.Equal((long)sellerCash, await database.LongAsync($"SELECT cash FROM characters WHERE charId={actor.CharId}"),
            "Loading Area seller received both sales");
        await RequirePurchase(database, 110, FirstItem, StartingCash - FirstPrice);
        Check.Equal(0L, await database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid={SecondItem} AND slot BETWEEN 1 AND 96"),
            "The stale request did not grant a second item to CSR_Tester");
        await RequirePurchase(database, helpers[SecondBuyer].CharId, SecondItem, StartingCash - SecondPrice);
        return new { SellerInformation = sellerCash, BuyerInformation = StartingCash - FirstPrice,
            HelperBuyerInformation = StartingCash - SecondPrice };
    }

    private static async Task RequirePurchase(ThrowawayDatabase database, uint buyer, uint item, uint cash)
    {
        // Inventory persists single items with count 0 (ItemStacks.CountFromDb).
        Check.Equal(1L, await database.LongAsync($"SELECT COALESCE(SUM(GREATEST(1,count)),0) FROM inventory WHERE charId={buyer} AND goid={item} AND slot BETWEEN 1 AND 96"),
            "The buyer received exactly one item");
        Check.Equal((long)cash, await database.LongAsync($"SELECT cash FROM characters WHERE charId={buyer}"),
            "The buyer paid exactly once");
    }

    private static bool HasListing(byte[] reply, uint listing, uint item, uint price)
    {
        const int headerSize = 11, rowSize = 22;
        if (reply.Length < headerSize) return false;
        int size = BinaryPrimitives.ReadUInt16LittleEndian(reply.AsSpan(9));
        if (size % rowSize != 0 || size != reply.Length - headerSize) return false;
        for (int offset = headerSize; offset < reply.Length; offset += rowSize)
        {
            var row = reply.AsSpan(offset, rowSize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(row) == item &&
                BinaryPrimitives.ReadUInt32LittleEndian(row[8..]) == listing &&
                BinaryPrimitives.ReadUInt32LittleEndian(row[12..]) == price) return true;
        }
        return false;
    }

    private static async Task EnterLoadingAreaAsync(HeadlessPlayer player)
    {
        player.SendRpc("80 fc 01");
        await player.WaitForRpcAsync("80 fd");
        player.SendRpc("80 fe 01");
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08");
        await player.WaitForRpcAsync("81 09");
        player.SendRpc("05");
    }

    private static async Task<uint> ListAsync(HeadlessPlayer seller, uint item, uint price, byte slot)
    {
        seller.SendRpc($"81 2b {Le(ItemCategory)} {Le(price)} 01 {slot:x2} 00 00 00 00 00");
        await seller.WaitForRpcAsync($"5f {slot:x2} 00 01");
        var listed = await seller.WaitForRpcAsync("81 2c");
        Check.Equal(item, BitConverter.ToUInt32(listed.Bytes, 6), "Listed item");
        Check.Equal(price, BitConverter.ToUInt32(listed.Bytes, 14), "Listed price");
        return BitConverter.ToUInt32(listed.Bytes, 2);
    }

    private static string Le(uint value) => WireFormat.ToHex(BitConverter.GetBytes(value));
}
