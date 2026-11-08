using System;
using System.Collections.Generic;
using System.Data;
using hds.shared;
using MySqlConnector;

namespace hds.databases
{
    /// <summary>
    /// Marketplace and inventory queries for the marketplace. Kept apart from MyWorldDbAccess (own pooled
    /// connections, parameterized SQL) so it does not collide with other work on that class.
    /// Table <c>marketplace</c>: id, category (hex of the 4-byte tag), itemID, purity (item descriptor),
    /// delistPrice, price, charID (seller), charRep, is_sold, created (unix time).
    /// </summary>
    public class MarketplaceDb
    {
        private static readonly object IdLock = new object();

        private static MySqlConnection Open()
        {
            var config = Store.config;
            var conn = new MySqlConnection("Server=" + config.dbParams.Host + ";Port=" + config.dbParams.Port +
                                           ";Database=" + config.dbParams.DatabaseName + ";User ID=" +
                                           config.dbParams.Username + ";Password=" + config.dbParams.Password +
                                           ";Pooling=true;");
            conn.Open();
            return conn;
        }

        private static MySqlCommand Cmd(MySqlConnection conn, string sql, params object[] nameValues)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i + 1 < nameValues.Length; i += 2)
            {
                cmd.Parameters.AddWithValue((string) nameValues[i], nameValues[i + 1]);
            }

            return cmd;
        }

        public static string CategoryKey(UInt32 category) => category.ToString("x8");

        private static MarketListing Read(IDataRecord r)
        {
            return new MarketListing
            {
                Id = Convert.ToUInt32(r["id"]),
                Category = Convert.ToUInt32(Convert.ToString(r["category"]), 16),
                GoId = Convert.ToUInt32(Convert.ToInt64(r["itemID"])),
                Descriptor = unchecked((UInt32) Convert.ToInt32(r["purity"])),
                DelistPrice = Convert.ToUInt32(r["delistPrice"]),
                Price = Convert.ToUInt32(r["price"]),
                SellerCharId = Convert.ToUInt32(r["charID"]),
                SellerRep = (byte) Convert.ToInt32(r["charRep"]),
                Sold = Convert.ToInt32(r["is_sold"]) != 0,
                Created = Convert.ToUInt32(r["created"])
            };
        }

        /// <summary>Unsold listings; the category filter ignores the last byte's top bit (see MarketplaceRequests.SameCategory).</summary>
        public List<MarketListing> FindForSale(UInt32 category, UInt32 minPrice, UInt32 maxPrice, int skip, int take)
        {
            var result = new List<MarketListing>();
            using (var conn = Open())
            using (var cmd = Cmd(conn,
                       "SELECT * FROM marketplace WHERE is_sold = 0 AND category IN (@a, @b) AND price BETWEEN @min AND @max " +
                       "ORDER BY price, id LIMIT @skip, @take",
                       "@a", CategoryKey(category & 0x7fffffff), "@b", CategoryKey(category | 0x80000000),
                       "@min", (long) minPrice, "@max", (long) maxPrice, "@skip", skip, "@take", take))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    result.Add(Read(r));
                }
            }

            return result;
        }

        public List<MarketListing> FindBySeller(UInt32 charId)
        {
            var result = new List<MarketListing>();
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT * FROM marketplace WHERE charID = @c AND is_sold = 0 ORDER BY id", "@c", (long) charId))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    result.Add(Read(r));
                }
            }

            return result;
        }

        public MarketListing Find(UInt32 listingId)
        {
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT * FROM marketplace WHERE id = @id", "@id", (long) listingId))
            using (var r = cmd.ExecuteReader())
            {
                return r.Read() ? Read(r) : null;
            }
        }

        /// <summary>Stores the listing and gives it the next id (the table has no auto increment).</summary>
        public void Insert(MarketListing listing)
        {
            lock (IdLock)
            {
                using (var conn = Open())
                {
                    object max = Cmd(conn, "SELECT COALESCE(MAX(id), 0) FROM marketplace").ExecuteScalar();
                    listing.Id = Convert.ToUInt32(max) + 1;
                    Cmd(conn,
                        "INSERT INTO marketplace (id, category, itemID, purity, delistPrice, price, charID, charRep, is_sold, created) " +
                        "VALUES (@id, @cat, @goid, @purity, @delist, @price, @char, @rep, 0, @created)",
                        "@id", (long) listing.Id, "@cat", CategoryKey(listing.Category), "@goid", (long) listing.GoId,
                        "@purity", unchecked((int) listing.Descriptor), "@delist", (long) listing.DelistPrice,
                        "@price", (long) listing.Price, "@char", (long) listing.SellerCharId, "@rep", (int) listing.SellerRep,
                        "@created", (long) listing.Created).ExecuteNonQuery();
                }
            }
        }

        /// <summary>Marks the listing sold; false if someone else bought it first.</summary>
        public bool MarkSold(UInt32 listingId)
        {
            using (var conn = Open())
            {
                return Cmd(conn, "UPDATE marketplace SET is_sold = 1 WHERE id = @id AND is_sold = 0", "@id", (long) listingId)
                    .ExecuteNonQuery() == 1;
            }
        }

        /// <summary>The item in one of the player's inventory slots (goid 0 when empty).</summary>
        public void GetInventoryItem(UInt32 charId, UInt16 slot, out UInt32 goId, out UInt32 purity)
        {
            goId = 0;
            purity = 0;
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT goid, purity FROM inventory WHERE charId = @c AND slot = @s LIMIT 1",
                       "@c", (long) charId, "@s", (int) slot))
            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    goId = Convert.ToUInt32(Convert.ToInt64(r.GetValue(0)));
                    purity = unchecked((UInt32) Convert.ToInt32(r.GetValue(1)));
                }
            }
        }

        /// <summary>Items in the stack at that slot (the count column; 0 and 1 both mean a single item).</summary>
        public int GetInventoryCount(UInt32 charId, UInt16 slot)
        {
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT count FROM inventory WHERE charId = @c AND slot = @s LIMIT 1",
                       "@c", (long) charId, "@s", (int) slot))
            {
                object value = cmd.ExecuteScalar();
                return value == null || value is DBNull ? 0 : Convert.ToInt32(value);
            }
        }

        public void RemoveInventoryItem(UInt32 charId, UInt16 slot)
        {
            using (var conn = Open())
            {
                Cmd(conn, "DELETE FROM inventory WHERE charId = @c AND slot = @s LIMIT 1", "@c", (long) charId, "@s", (int) slot)
                    .ExecuteNonQuery();
            }
        }

        public void AddInventoryItem(UInt32 charId, UInt16 slot, UInt32 goId, UInt32 purity)
        {
            using (var conn = Open())
            {
                Cmd(conn, "INSERT INTO inventory (charId, goid, slot, count, purity, created) VALUES (@c, @g, @s, 0, @p, NOW())",
                    "@c", (long) charId, "@g", (long) goId, "@s", (int) slot, "@p", unchecked((int) purity)).ExecuteNonQuery();
            }
        }

        /// <summary>The first free backpack slot (0..96, the worn range starts at 97), or -1 when full.</summary>
        public int FirstFreeSlot(UInt32 charId)
        {
            var used = new HashSet<int>();
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT slot FROM inventory WHERE charId = @c", "@c", (long) charId))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    used.Add(Convert.ToInt32(r.GetValue(0)));
                }
            }

            for (int slot = 0; slot < 97; slot++)
            {
                if (!used.Contains(slot))
                {
                    return slot;
                }
            }

            return -1;
        }

        /// <summary>Credits an offline seller (the online case goes through the player's live info).</summary>
        public void AddCash(UInt32 charId, UInt32 amount)
        {
            using (var conn = Open())
            {
                Cmd(conn, "UPDATE characters SET cash = cash + @a WHERE charId = @c", "@a", (long) amount, "@c", (long) charId)
                    .ExecuteNonQuery();
            }
        }
    }
}
