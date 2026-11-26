using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MarketplaceDb
    {
        // Prefer the upper portion of the shared archive array for marketplace deliveries.
        internal const ushort ArchiveDeliveryFirstSlot = 384;

        internal sealed class Purchase
        {
            public readonly uint Status, Cost, BuyerCash;
            public readonly ushort Slot;
            public readonly MarketListing Listing;
            public readonly uint? SellerCash;

            public Purchase(uint status, MarketListing listing = null, ushort slot = 0,
                uint cost = 0, uint buyerCash = 0, uint? sellerCash = null)
            {
                Status = status;
                Listing = listing;
                Slot = slot;
                Cost = cost;
                BuyerCash = buyerCash;
                SellerCash = sellerCash;
            }
        }

        private static MySqlCommand TransactionCommand(MySqlTransaction transaction, string sql, params object[] args)
        {
            var command = Cmd(transaction.Connection, sql, args);
            command.Transaction = transaction;
            return command;
        }

        /// <summary>Move the exact requested bag item or archive entry into a listing.</summary>
        internal string Place(MarketListing listing, MarketplaceRequests.Placement request)
        {
            if (request.Container > 1 || (request.Container == 0 ? request.Slot >= CentralStorage.Slots : request.Slot > MyWorldDbAccess.LastBagSlot))
                return "That marketplace source slot is unavailable.";
            if (listing.Price == 0 || listing.Price > int.MaxValue)
                return "Choose a marketplace price between 1 and 2147483647.";

            lock (IdLock)
            {
                using var connection = Open();
                using var transaction = connection.BeginTransaction();
                bool archive = request.Container == 0;
                var keys = new object[] { "@character", listing.SellerCharId, "@slot", request.Slot };
                string query = archive
                    ? "SELECT ability_id,level,is_loaded FROM char_abilities WHERE char_id=@character AND slot=@slot FOR UPDATE"
                    : "SELECT goid,count,purity FROM inventory WHERE charId=@character AND slot=@slot FOR UPDATE";
                using (var command = TransactionCommand(transaction, query, keys))
                using (var rows = command.ExecuteReader())
                {
                    if (!rows.Read()) return "There is no item in that marketplace source slot.";
                    uint item = unchecked((uint)Convert.ToInt64(rows[0]));
                    long storedCount = rows.GetInt64(1);
                    if (archive)
                    {
                        bool flagged = CodeStorage.IsCode(item);
                        uint goid = flagged ? CodeStorage.GoidOf(item) : item >> 7;
                        long charges = flagged ? storedCount + 1 : storedCount;
                        var recipeItem = CraftingRules.Find(goid);
                        // Accept legacy Write Code rows as well as canonical flagged storage rows.
                        if (rows.GetBoolean(2) || (item & 127u) != 0 || charges < 1 || charges > 127 ||
                            (flagged ? item != unchecked((uint)CodeStorage.BaseValue(goid)) : (item & 0x80000000u) != 0) ||
                            !OrdinaryItemCode.CanMaterialize(recipeItem) ||
                            !MarketplaceRequests.SameCategory(listing.Category, recipeItem.Category))
                            return "Only supported item codes can be listed from the archive.";
                        listing.Category |= 0x80000000u;
                        listing.GoId = AbilityProgression.AbilityCodeItem;
                        listing.Descriptor = OrdinaryItemCode.Packed(goid << 7, (int)charges);
                    }
                    else
                    {
                        if (item == 0 || (storedCount != 0 && storedCount != 1))
                            return "Only single bag items can be listed. Split the stack first.";
                        listing.GoId = item;
                        uint descriptor = unchecked((uint)rows.GetInt32(2));
                        listing.Descriptor = descriptor == 0 ? 0x0c000000 : descriptor;
                        if (listing.IsArchiveCode) return "That bag item cannot be listed as an archive code.";
                    }
                    if (rows.Read()) return "That marketplace source slot is ambiguous.";
                }
                string remove = archive ? "DELETE FROM char_abilities WHERE char_id=@character AND slot=@slot"
                    : "DELETE FROM inventory WHERE charId=@character AND slot=@slot";
                using (var command = TransactionCommand(transaction, remove, keys))
                    if (command.ExecuteNonQuery() != 1) return "That marketplace source changed. Try again.";

                using (var command = TransactionCommand(transaction, "SELECT COALESCE(MAX(id), 0) FROM marketplace"))
                    listing.Id = Convert.ToUInt32(command.ExecuteScalar()) + 1;
                using (var command = TransactionCommand(transaction, @"
                    INSERT INTO marketplace (id, category, itemID, purity, delistPrice, price, charID, charRep, is_sold, created)
                    VALUES (@id, @category, @item, @descriptor, @delist, @price, @character, @rep, 0, @created)
                    ", "@id", listing.Id, "@category", CategoryKey(listing.Category), "@item", unchecked((int)listing.GoId),
                    "@descriptor", unchecked((int)listing.Descriptor), "@delist", listing.DelistPrice, "@price", listing.Price,
                    "@character", listing.SellerCharId, "@rep", listing.SellerRep, "@created", listing.Created))
                    if (command.ExecuteNonQuery() != 1) return "The marketplace could not save the listing.";
                transaction.Commit();
                return null;
            }
        }

        /// <summary>Commit delivery, listing state and both wallets together.</summary>
        internal Purchase Checkout(uint listingId, uint buyer, byte requestedSlot, bool canReceiveArchiveCode)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            MarketListing listing;
            using (var command = TransactionCommand(transaction, "SELECT * FROM marketplace WHERE id=@id FOR UPDATE", "@id", listingId))
            using (var rows = command.ExecuteReader())
            {
                if (!rows.Read()) return new Purchase(MarketplacePackets.ItemNotInMarketplace);
                listing = Read(rows);
            }
            if (listing.Sold) return new Purchase(MarketplacePackets.ItemNotInMarketplace);
            bool delist = listing.SellerCharId == buyer;
            uint cost = delist ? listing.DelistPrice : listing.Price;
            if (listing.IsArchiveCode && !canReceiveArchiveCode)
                return new Purchase(CentralStorage.InvalidTransaction);

            long buyerBalance = ReadBalance(transaction, Math.Min(buyer, listing.SellerCharId));
            long sellerBalance = delist ? buyerBalance : ReadBalance(transaction, Math.Max(buyer, listing.SellerCharId));
            // The two account rows are locked in character-ID order.
            if (buyer > listing.SellerCharId) (buyerBalance, sellerBalance) = (sellerBalance, buyerBalance);
            if (buyerBalance < cost) return new Purchase(MarketplacePackets.NotEnoughInformation);
            if (!delist && (sellerBalance < 0 || sellerBalance + cost > int.MaxValue))
                return new Purchase(CentralStorage.InvalidTransaction);

            ushort slot;
            if (listing.IsArchiveCode)
            {
                var recipeItem = CraftingRules.Find(listing.ArchiveGoid);
                if (!OrdinaryItemCode.CanMaterialize(recipeItem) ||
                    !MarketplaceRequests.SameCategory(listing.Category, recipeItem.Category))
                    return new Purchase(CentralStorage.InvalidStorageItem);
                var occupied = ReadSlots(transaction, "SELECT slot FROM char_abilities WHERE char_id=@character AND slot<520 FOR UPDATE", buyer);
                int available = Enumerable.Range(ArchiveDeliveryFirstSlot, CentralStorage.Slots - ArchiveDeliveryFirstSlot)
                    .FirstOrDefault(candidate => !occupied.Contains(candidate), -1);
                if (available < 0) return new Purchase(CentralStorage.StorageFull);
                slot = (ushort)available;
                using var command = TransactionCommand(transaction, @"
                    INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name,added)
                    VALUES(@character,@slot,@item,@level,0,'Marketplace item code',NOW())
                    ", "@character", buyer, "@slot", slot, "@item", CodeStorage.BaseValue(listing.ArchiveGoid),
                    "@level", listing.ArchiveCharges - 1);
                if (command.ExecuteNonQuery() != 1) return new Purchase(CentralStorage.InvalidTransaction);
            }
            else
            {
                var occupied = ReadSlots(transaction, "SELECT slot FROM inventory WHERE charId=@character AND slot<=96 FOR UPDATE", buyer);
                int free = requestedSlot <= MyWorldDbAccess.LastBagSlot && !occupied.Contains(requestedSlot) ? requestedSlot
                    : Enumerable.Range(0, MyWorldDbAccess.LastBagSlot + 1).FirstOrDefault(candidate => !occupied.Contains(candidate), -1);
                if (free < 0) return new Purchase(MarketplacePackets.InventoryFull);
                slot = (ushort)free;
                using var command = TransactionCommand(transaction, @"
                    INSERT INTO inventory(charId,goid,slot,count,purity,created) VALUES(@character,@item,@slot,0,@descriptor,NOW())
                    ", "@character", buyer, "@slot", slot, "@item", listing.GoId, "@descriptor", unchecked((int)listing.Descriptor));
                if (command.ExecuteNonQuery() != 1) return new Purchase(CentralStorage.InvalidTransaction);
            }

            uint buyerCash = (uint)(buyerBalance - cost);
            uint? sellerCash = delist ? (uint?)null : (uint)(sellerBalance + cost);
            if (cost != 0)
            {
                WriteBalance(transaction, buyer, buyerCash);
                if (sellerCash.HasValue) WriteBalance(transaction, listing.SellerCharId, sellerCash.Value);
            }
            using (var command = TransactionCommand(transaction, "UPDATE marketplace SET is_sold=1 WHERE id=@id", "@id", listingId))
                if (command.ExecuteNonQuery() != 1) return new Purchase(MarketplacePackets.ItemNotInMarketplace);
            transaction.Commit();
            return new Purchase(0, listing, slot, cost, buyerCash, sellerCash);
        }

        private static HashSet<int> ReadSlots(MySqlTransaction transaction, string sql, uint character)
        {
            using var command = TransactionCommand(transaction, sql, "@character", character);
            using var rows = command.ExecuteReader();
            var occupied = new HashSet<int>();
            while (rows.Read()) occupied.Add(rows.GetInt32(0));
            return occupied;
        }

        private static long ReadBalance(MySqlTransaction transaction, uint character)
        {
            using var command = TransactionCommand(transaction, "SELECT cash FROM characters WHERE charId=@character FOR UPDATE", "@character", character);
            return command.ExecuteScalar() is int cash ? cash : -1;
        }

        private static void WriteBalance(MySqlTransaction transaction, uint character, uint cash)
        {
            using var command = TransactionCommand(transaction, "UPDATE characters SET cash=@cash WHERE charId=@character",
                "@character", character, "@cash", cash);
            if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Marketplace wallet write failed.");
        }
    }
}
