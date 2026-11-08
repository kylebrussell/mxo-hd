using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>One marketplace listing (table <c>marketplace</c>).</summary>
    public class MarketListing
    {
        public UInt32 Id;
        /// <summary>The client's four-byte category tag as a little-endian u32, e.g. "IAPS" (pants) = 0x53504149.</summary>
        public UInt32 Category;
        public UInt32 GoId;
        /// <summary>The item's four-byte descriptor (as in 5e: 00 00 &lt;amount&gt; &lt;type&gt;), little-endian.</summary>
        public UInt32 Descriptor;
        public UInt32 Price;
        public UInt32 DelistPrice;
        public UInt32 SellerCharId;
        public byte SellerRep;
        public bool Sold;
        /// <summary>Unix time the item was listed.</summary>
        public UInt32 Created;
    }

    /// <summary>
    /// Marketplace RPCs, from the retail captures in
    /// research/packet-dumps/community-intake/20260606T201844Z-hdneo-mxopackets1-vendor-marketplace-selected and
    /// 20260607T013506Z-hdneo-mxopackets1-marketplace-vendor-selected (AfterWhoruNeo opening the loading-area
    /// marketplace, browsing every category, listing a SWAT disguise and a lockpick code for 1000, buying a t-shirt
    /// for 6000). Each builder is checked against those bytes in MarketplacePacketTests.
    /// </summary>
    internal static class MarketplacePackets
    {
        /// <summary>Info change reason retail used for a marketplace purchase (80 e7 / 80 e4 ... 05 00 00 00).</summary>
        public const UInt32 InfoReasonMarketplace = 5;

        /// <summary>Retail's listing length field in 81 2c (1e 00 00 00); read as days on the market.</summary>
        public const UInt32 ListingDays = 30;

        /// <summary>Retail's delist price was 5% of the asking price (100,000 for 2,000,000).</summary>
        public static UInt32 DelistPriceFor(UInt32 price) => price / 20;

        /// <summary>81 25 09 00 &lt;more pages u32&gt; 00 &lt;length u16&gt; then 22 bytes per listing:
        /// goid, descriptor, listing id, price, seller rep (u8), listed time, 00. Retail answered a 25-per-page
        /// request with 25 rows and "more" = 1 (rifles, tools), an empty category with all zeros.</summary>
        public static byte[] BrowseResult(bool morePages, IList<MarketListing> listings)
        {
            PacketContent rows = new PacketContent();
            foreach (MarketListing listing in listings)
            {
                rows.AddUint32(listing.GoId, 1);
                rows.AddUint32(listing.Descriptor, 1);
                rows.AddUint32(listing.Id, 1);
                rows.AddUint32(listing.Price, 1);
                rows.AddByte(listing.SellerRep);
                rows.AddUint32(listing.Created, 1);
                rows.AddByte(0x00);
            }

            return Framed(0x8125, morePages ? 1u : 0u, rows.ReturnFinalPacket(), rows.ReturnFinalPacket().Length);
        }

        /// <summary>81 2a, the player's own listings when the marketplace opens: 24 bytes per listing, goid,
        /// descriptor, listing id, price, delist price, listed time. Retail's one sample had a 00 after the descriptor
        /// (25 bytes, length still 24); with that byte the 7.6005 client read every field one byte early (a 101,000
        /// listing showed 25,856,000, and De-list sent listing id 256 for 1), so it is left out.</summary>
        public static byte[] MyListings(IList<MarketListing> listings)
        {
            PacketContent rows = new PacketContent();
            foreach (MarketListing listing in listings)
            {
                rows.AddUint32(listing.GoId, 1);
                rows.AddUint32(listing.Descriptor, 1);
                rows.AddUint32(listing.Id, 1);
                rows.AddUint32(listing.Price, 1);
                rows.AddUint32(listing.DelistPrice, 1);
                rows.AddUint32(listing.Created, 1);
            }

            return Framed(0x812a, 0, rows.ReturnFinalPacket(), 24 * listings.Count);
        }

        /// <summary>81 2c: the item is on the market. Listing id, goid, descriptor, price, days (30), then the
        /// container byte and slot the client listed it from, then eight zero bytes.</summary>
        public static byte[] Listed(MarketListing listing, byte container, UInt16 slot)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x812c, 0);
            pak.AddUint32(listing.Id, 1);
            pak.AddUint32(listing.GoId, 1);
            pak.AddUint32(listing.Descriptor, 1);
            pak.AddUint32(listing.Price, 1);
            pak.AddUint32(ListingDays, 1);
            pak.AddByte(container);
            pak.AddUint16(slot, 1);
            pak.AddByteArray(new byte[8]);
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 27: purchase done. Listing id, the slot byte from the request, goid, descriptor, price, 0.</summary>
        public static byte[] Bought(MarketListing listing, byte slot)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(0x8127, 0);
            pak.AddUint32(listing.Id, 1);
            pak.AddByte(slot);
            pak.AddUint32(listing.GoId, 1);
            pak.AddUint32(listing.Descriptor, 1);
            pak.AddUint32(listing.Price, 1);
            pak.AddUint32(0, 1);
            return pak.ReturnFinalPacket();
        }

        private static byte[] Framed(UInt16 header, UInt32 flag, byte[] rows, int length)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(header, 0);
            pak.AddUint16(9, 1);
            pak.AddUint32(flag, 1);
            pak.AddByte(0x00);
            pak.AddUint16((UInt16) length, 1);
            pak.AddByteArray(rows);
            return pak.ReturnFinalPacket();
        }
    }

    /// <summary>Parsers for the client's marketplace RPCs (rpcData = bytes after the two-byte header).</summary>
    public static class MarketplaceRequests
    {
        public struct Search
        {
            public UInt32 Category;
            public UInt32 MinPrice;
            public UInt16 PageSize;
            public UInt32 MaxPrice;
            public UInt32 Start;
        }

        public struct Placement
        {
            public UInt32 Category;
            public UInt32 Price;
            public byte Container;
            public UInt16 Slot;
        }

        /// <summary>81 24 &lt;category&gt; &lt;u32 0&gt; &lt;u32 0&gt; &lt;u16 25&gt; &lt;u32 999999999&gt; &lt;u32 0&gt; 01 00 00 (every retail
        /// search). Read as category, min price, (unknown), page size, max price, first row; only category,
        /// page size and max price ever varied from what is shown, so the rest is a guess.</summary>
        public static bool TryParseSearch(byte[] rpcData, out Search search)
        {
            search = new Search();
            if (rpcData == null || rpcData.Length < 22)
            {
                return false;
            }

            search.Category = BitConverter.ToUInt32(rpcData, 0);
            search.MinPrice = BitConverter.ToUInt32(rpcData, 4);
            search.PageSize = BitConverter.ToUInt16(rpcData, 12);
            search.MaxPrice = BitConverter.ToUInt32(rpcData, 14);
            search.Start = BitConverter.ToUInt32(rpcData, 18);
            if (search.PageSize == 0 || search.PageSize > 100)
            {
                search.PageSize = 25;
            }

            return true;
        }

        /// <summary>81 2b &lt;category&gt; &lt;price u32&gt; &lt;container u8&gt; &lt;slot u16&gt; 00 00 00 00, e.g. the SWAT disguise
        /// "49 54 4f 74 e8 03 00 00 01 1a 00 00 00 00 00" (inventory slot 26, answered with "5f 1a 00 01").</summary>
        public static bool TryParsePlacement(byte[] rpcData, out Placement placement)
        {
            placement = new Placement();
            if (rpcData == null || rpcData.Length < 11)
            {
                return false;
            }

            placement.Category = BitConverter.ToUInt32(rpcData, 0);
            placement.Price = BitConverter.ToUInt32(rpcData, 4);
            placement.Container = rpcData[8];
            placement.Slot = BitConverter.ToUInt16(rpcData, 9);
            return true;
        }

        /// <summary>81 26 &lt;listing id u32&gt; &lt;slot u8&gt;: buy, e.g. "ca d1 12 00 04" (the item then went to slot 4).</summary>
        public static bool TryParseBuy(byte[] rpcData, out UInt32 listingId, out byte slot)
        {
            listingId = 0;
            slot = 0;
            if (rpcData == null || rpcData.Length < 5)
            {
                return false;
            }

            listingId = BitConverter.ToUInt32(rpcData, 0);
            slot = rpcData[4];
            return true;
        }

        /// <summary>Retail placed a lockpick code under "ITL\xf0" but searched lockpicks as "ITLp": the last byte's
        /// top bit differs. Compare categories with that bit ignored so a listing shows in its search.</summary>
        public static bool SameCategory(UInt32 a, UInt32 b) => (a & 0x7fffffff) == (b & 0x7fffffff);

        public static string CategoryText(UInt32 category)
        {
            char[] text = new char[4];
            for (int i = 0; i < 4; i++)
            {
                byte b = (byte) ((category >> (8 * i)) & 0x7f);
                text[i] = b >= 0x20 ? (char) b : '?';
            }

            return new string(text);
        }
    }
}
