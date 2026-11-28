using System;
using System.Collections.Generic;
using System.Data.Common;
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
            var publication = new AbilityAcquisition.Publication(client);
            if (placement.Container == 0 && !client.playerData.DistrictIsLA())
            {
                RefusePlacement(client, publication, placement, "List archive codes from the Loading Area marketplace.");
                return;
            }
            var listing = new MarketListing
            {
                Category = placement.Category,
                Price = placement.Price,
                DelistPrice = MarketplacePackets.DelistPriceFor(placement.Price),
                SellerCharId = publication.Character,
                SellerRep = client.playerInstance.OrganizationID.getValue()[0],
                Created = TimeUtils.getUnixTimeUint32()
            };

            string refusal;
            lock (client.playerData.craftingSync)
            {
                try
                {
                    refusal = Db.Place(listing, placement);
                }
                catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
                {
                    Output.WriteDebugLog("[MARKET] Listing transaction rolled back: " + ex.Message);
                    refusal = "The listing could not be posted. Your item was kept.";
                }
            }
            if (refusal != null)
            {
                RefusePlacement(client, publication, placement, refusal);
                return;
            }

            byte[] remove = placement.Container == 0 ? CodeStorage.BuildRemove(placement.Slot)
                : ServerPackets.BuildInventoryItemDelete(placement.Slot);
            Output.WriteDebugLog($"[MARKET] {publication.Character} listed class={listing.GoId} descriptor={listing.Descriptor:x8} " +
                $"container={placement.Container} slot={placement.Slot} price={listing.Price} id={listing.Id}");
            if (publication.Publish(client, remove,
                MarketplacePackets.Listed(listing, placement.Container, placement.Slot, placement.ClientUiData)))
                client.FlushQueue();
        }

        private static void RefusePlacement(WorldClient client, AbilityAcquisition.Publication publication,
            MarketplaceRequests.Placement placement, string reason)
        {
            if (publication.Publish(client, MarketplacePackets.PlaceRefused(placement, CentralStorage.InvalidTransaction),
                ServerPackets.BuildSystemChatMessage(reason, "SYSTEM")))
                client.FlushQueue();
        }

        /// <summary>Client 81 26 buys a listing or de-lists the buyer's own listing.</summary>
        public void ProcessBuyItem(ref byte[] packet)
        {
            if (!MarketplaceRequests.TryParseBuy(packet, out uint listingId, out byte requestedSlot)) return;

            WorldClient buyer = Store.currentClient;
            var publication = new AbilityAcquisition.Publication(buyer);
            lock (BuyLock)
            {
                MarketplaceDb.Purchase purchase;
                try
                {
                    lock (buyer.playerData.craftingSync)
                        purchase = Db.Checkout(listingId, publication.Character, requestedSlot, buyer.playerData.DistrictIsLA());
                }
                catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
                {
                    Output.WriteDebugLog("[MARKET] Purchase transaction rolled back: " + ex.Message);
                    purchase = new MarketplaceDb.Purchase(CentralStorage.InvalidTransaction);
                }
                if (purchase.Status != 0)
                {
                    RefuseBuy(buyer, publication, listingId, requestedSlot, purchase.Status);
                    return;
                }
                PublishPurchase(buyer, publication, purchase, requestedSlot);
            }
        }

        private static void PublishPurchase(WorldClient buyer, AbilityAcquisition.Publication publication,
            MarketplaceDb.Purchase purchase, byte requestedSlot)
        {
            MarketListing listing = purchase.Listing;
            byte amount = (byte)(listing.Descriptor >> 16);
            byte type = (byte)(listing.Descriptor >> 24);
            byte[] delivery = listing.IsArchiveCode ? CodeStorage.BuildAdd(purchase.Slot, listing.ArchiveGoid, listing.ArchiveCharges)
                : ServerPackets.BuildInventoryItemAdd(purchase.Slot, listing.GoId, amount, type);
            const uint reason = MarketplacePackets.InfoReasonMarketplace;
            byte replySlot = listing.IsArchiveCode ? requestedSlot : (byte)purchase.Slot;
            if (publication.PublishPurchase(buyer, purchase.BuyerCash, delivery,
                ServerPackets.BuildInfoDelta(-(long)purchase.Cost, reason), ServerPackets.BuildInfoCurrent(purchase.BuyerCash, reason),
                MarketplacePackets.Bought(listing, replySlot, purchase.Cost)))
                buyer.FlushQueue();
            Output.WriteDebugLog($"[MARKET] {publication.Character} checked out id={listing.Id} archive={listing.IsArchiveCode} " +
                $"slot={purchase.Slot} cost={purchase.Cost} cash={purchase.BuyerCash}");

            if (!purchase.SellerCash.HasValue) return;
            WorldClient seller = FindOnline(listing.SellerCharId);
            if (seller == null) return;
            uint sellerCash = purchase.SellerCash.Value;
            var sellerPublication = new AbilityAcquisition.Publication(seller);
            if (sellerPublication.Character == listing.SellerCharId &&
                sellerPublication.PublishPurchase(seller, sellerCash, ServerPackets.BuildInfoDelta(purchase.Cost, reason),
                    ServerPackets.BuildInfoCurrent(sellerCash, reason),
                    ServerPackets.BuildSystemChatMessage("Your marketplace item sold for " + purchase.Cost + " information.", "SYSTEM")))
                seller.FlushQueue();
        }

        private static void RefuseBuy(WorldClient buyer, AbilityAcquisition.Publication publication,
            uint listingId, byte requestedSlot, uint status)
        {
            // The Loading Area purchase controls wait for the correlated 81 27 response.
            if (publication.Publish(buyer, MarketplacePackets.BuyRefused(listingId, requestedSlot, status)))
                buyer.FlushQueue();
        }
    }
}
