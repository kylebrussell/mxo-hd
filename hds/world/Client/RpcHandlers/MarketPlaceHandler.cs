using System;
using System.Collections.Generic;
using System.Linq;
using hds.databases;
using hds.shared;

namespace hds{

    /// <summary>
    /// The marketplace: open (81 29 -> 81 2a, my listings), search (81 24 -> 81 25), list an item (81 2b -> 5f, 81 2c)
    /// and buy (81 26 -> 5e, 80 e7, 80 e4, 81 27), in the order retail sent them (see MarketplacePackets). The seller
    /// is paid when the item sells: at once if online (80 e7 / 80 e4 like the buyer), otherwise in the database.
    /// </summary>
    public class MarketPlaceHandler{

        internal static MarketplaceDb Db = new MarketplaceDb();

        private static readonly object BuyLock = new object();

        private static ServerPackets Packets => new ServerPackets();

        private static void Notice(WorldClient client, string text)
        {
            Packets.sendSystemChatMessage(client, text, "SYSTEM");
        }

        /// <summary>
        /// The connected client of this character, in the world or not: the Loading Area, where the marketplace is used,
        /// doesn't count as on-world. A seller there was treated as offline and credited only in the database, and their
        /// next save wrote their old information back over it.
        /// </summary>
        private static WorldClient FindOnline(UInt32 charId)
        {
            lock (WorldServer.Clients)
            {
                return WorldServer.Clients.Values.FirstOrDefault(c => c != null && c.playerData != null &&
                                                                      c.playerData.getCharID() == charId);
            }
        }

        /// <summary>Client 81 29 00 00 00 00 01: the marketplace window opened.</summary>
        public void ProcessOpen(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            List<MarketListing> mine = Db.FindBySeller(client.playerData.getCharID());
            client.messageQueue.addRpcMessage(MarketplacePackets.MyListings(mine));
            client.FlushQueue();
        }

        /// <summary>Client 81 24: search one category.</summary>
        public void processMarketplaceList(ref byte[] packet)
        {
            MarketplaceRequests.Search search;
            if (!MarketplaceRequests.TryParseSearch(packet, out search))
            {
                return;
            }

            UInt32 maxPrice = search.MaxPrice == 0 ? UInt32.MaxValue : search.MaxPrice;
            // One row more than a page tells whether there are more pages.
            List<MarketListing> rows = Db.FindForSale(search.Category, search.MinPrice, maxPrice, (int) search.Start, search.PageSize + 1);
            bool more = rows.Count > search.PageSize;
            if (more)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            Output.WriteDebugLog("[MARKET] Search " + MarketplaceRequests.CategoryText(search.Category) + ": " + rows.Count + " listing(s)");
            Store.currentClient.messageQueue.addRpcMessage(MarketplacePackets.BrowseResult(more, rows));
            Store.currentClient.FlushQueue();
        }

        /// <summary>Client 81 2b: put an inventory item on the market.</summary>
        public void ProcessPlaceItem(ref byte[] packet)
        {
            MarketplaceRequests.Placement placement;
            if (!MarketplaceRequests.TryParsePlacement(packet, out placement))
            {
                return;
            }

            WorldClient client = Store.currentClient;
            UInt32 charId = client.playerData.getCharID();
            if (placement.Price == 0)
            {
                Notice(client, "Set a price first.");
                return;
            }

            UInt32 goId, purity;
            Db.GetInventoryItem(charId, placement.Slot, out goId, out purity);
            if (goId == 0)
            {
                // Container 00 held a lockpick code in retail (code storage); this server only stores the backpack.
                Notice(client, "There is no item in that slot.");
                return;
            }

            // A listing holds one item: listing a stack would delete the whole stack and sell one.
            if (Db.GetInventoryCount(charId, placement.Slot) > 1)
            {
                Notice(client, "Only single items can be listed. Split the stack first.");
                return;
            }

            MarketListing listing = new MarketListing
            {
                Category = placement.Category,
                GoId = goId,
                // Items saved before purity was kept have 0; retail's login inventory used 00 00 00 0c.
                Descriptor = purity != 0 ? purity : 0x0c000000,
                Price = placement.Price,
                DelistPrice = MarketplacePackets.DelistPriceFor(placement.Price),
                SellerCharId = charId,
                SellerRep = client.playerInstance.OrganizationID.getValue()[0],
                Created = TimeUtils.getUnixTimeUint32()
            };

            Db.RemoveInventoryItem(charId, placement.Slot);
            Db.Insert(listing);
            Output.WriteDebugLog("[MARKET] " + charId + " listed goid " + goId + " for " + listing.Price + " as " +
                                 MarketplaceRequests.CategoryText(listing.Category) + " (listing " + listing.Id + ")");

            // Retail: 5f (item leaves the slot), then 81 2c.
            client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(placement.Slot));
            client.messageQueue.addRpcMessage(MarketplacePackets.Listed(listing, placement.Container, placement.Slot));
            client.FlushQueue();
        }

        /// <summary>
        /// Client 81 26 &lt;listing id&gt; &lt;slot&gt;: buy a listing. The client's De-list button sends the same request
        /// for the player's own listing: the item comes back for the delist price (no capture shows retail's answer,
        /// so it gets the buyer's records).
        /// </summary>
        public void ProcessBuyItem(ref byte[] packet)
        {
            UInt32 listingId;
            byte requestedSlot;
            if (!MarketplaceRequests.TryParseBuy(packet, out listingId, out requestedSlot))
            {
                return;
            }

            WorldClient buyer = Store.currentClient;
            UInt32 buyerId = buyer.playerData.getCharID();
            MarketListing listing;
            UInt16 slot;
            long newInfo;
            bool delist;
            UInt32 cost;
            lock (BuyLock)
            {
                listing = Db.Find(listingId);
                if (listing == null || listing.Sold)
                {
                    Notice(buyer, "That item has already been sold.");
                    return;
                }

                delist = listing.SellerCharId == buyerId;
                cost = delist ? listing.DelistPrice : listing.Price;

                long info = buyer.playerData.getInfo();
                if (info < cost)
                {
                    Notice(buyer, "You do not have enough information.");
                    return;
                }

                UInt32 occupied, ignored;
                Db.GetInventoryItem(buyerId, requestedSlot, out occupied, out ignored);
                int free = occupied == 0 && requestedSlot < 97 ? requestedSlot : Db.FirstFreeSlot(buyerId);
                if (free < 0)
                {
                    Notice(buyer, "Your inventory is full.");
                    return;
                }

                if (!Db.MarkSold(listingId))
                {
                    Notice(buyer, "That item has already been sold.");
                    return;
                }

                slot = (UInt16) free;
                Db.AddInventoryItem(buyerId, slot, listing.GoId, listing.Descriptor);
                newInfo = info - cost;
                buyer.playerData.setInfo(newInfo);
                Store.dbManager.WorldDbHandler.SaveInfo(buyer, newInfo);
            }

            // Retail order: 5e (item into the slot), 80 e7 (-price, reason 5), 80 e4 (new info), 81 27.
            byte amount = (byte) ((listing.Descriptor >> 16) & 0xff);
            byte type = (byte) (listing.Descriptor >> 24);
            buyer.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemAdd(slot, listing.GoId, amount, type));
            buyer.messageQueue.addRpcMessage(ServerPackets.BuildInfoDelta(-(long) cost, MarketplacePackets.InfoReasonMarketplace));
            buyer.messageQueue.addRpcMessage(ServerPackets.BuildInfoCurrent((UInt32) newInfo, MarketplacePackets.InfoReasonMarketplace));
            buyer.messageQueue.addRpcMessage(MarketplacePackets.Bought(listing, (byte) slot));
            buyer.FlushQueue();
            Output.WriteDebugLog("[MARKET] " + buyerId + (delist ? " delisted" : " bought") + " listing " + listing.Id +
                                 " for " + cost);

            if (!delist)
            {
                PaySeller(listing);
            }
        }

        /// <summary>Retail's seller side is not captured: pay at once, with the buyer's packets for an info change.</summary>
        private static void PaySeller(MarketListing listing)
        {
            WorldClient seller = FindOnline(listing.SellerCharId);
            if (seller == null)
            {
                Db.AddCash(listing.SellerCharId, listing.Price);
                return;
            }

            long info = seller.playerData.getInfo() + listing.Price;
            seller.playerData.setInfo(info);
            Store.dbManager.WorldDbHandler.SaveInfo(seller, info);
            seller.messageQueue.addRpcMessage(ServerPackets.BuildInfoDelta(listing.Price, MarketplacePackets.InfoReasonMarketplace));
            seller.messageQueue.addRpcMessage(ServerPackets.BuildInfoCurrent((UInt32) info, MarketplacePackets.InfoReasonMarketplace));
            seller.FlushQueue();
            Notice(seller, "Your marketplace item sold for " + listing.Price + " information.");
        }
    }
}
