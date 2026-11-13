using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public void EnsureVendorBuyback()
        {
            using var connection=new MySqlConnection(connectionString);connection.Open();
            using var c=connection.CreateCommand();
            c.CommandText=@"CREATE TABLE IF NOT EXISTS vendor_last_sale (
char_id BIGINT UNSIGNED NOT NULL PRIMARY KEY, session_token CHAR(32) NOT NULL, vendor_id INT UNSIGNED NOT NULL,
original_id INT UNSIGNED NOT NULL, raw_goid BIGINT NOT NULL, raw_count INT NOT NULL, purity INT NOT NULL,
original_slot TINYINT UNSIGNED NOT NULL, paid INT UNSIGNED NOT NULL,
original_created VARCHAR(32) NOT NULL, original_updated VARCHAR(32) NOT NULL
) ENGINE=InnoDB";
            c.ExecuteNonQuery();RequireBuybackEngines(connection);
        }
        private static void RequireBuybackEngines(MySqlConnection connection)
        {
            using var c=connection.CreateCommand();
            c.CommandText="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','inventory','vendor_last_sale') AND ENGINE='InnoDB'";
            if(Convert.ToInt32(c.ExecuteScalar())!=3)throw new InvalidOperationException("Vendor receipts require transactional character, inventory and receipt tables.");
        }
        private static List<VendorBuyback.Item> BuybackInventory(MySqlCommand c)
        {
            c.CommandText="SELECT invId,goid,count,purity,slot,CAST(created AS CHAR),CAST(updated AS CHAR) FROM inventory WHERE charId=@char ORDER BY invId FOR UPDATE";
            var rows=new List<VendorBuyback.Item>();using var r=c.ExecuteReader();
            while(r.Read())rows.Add(new VendorBuyback.Item(r.GetUInt32(0),r.GetInt64(1),r.GetInt32(2),r.GetInt32(3),r.GetUInt16(4),r.GetString(5),r.GetString(6)));
            return rows;
        }
        private static long? BuybackCash(MySqlCommand c)
        {
            c.CommandText="SELECT cash FROM characters WHERE charId=@char FOR UPDATE";
            object cash=c.ExecuteScalar();return cash==null||cash==DBNull.Value?(long?)null:Convert.ToInt64(cash);
        }
        private static void ReceiptLock(MySqlCommand c)
        {
            c.CommandText="SELECT char_id FROM vendor_last_sale WHERE char_id=@char FOR UPDATE";c.ExecuteScalar();
        }
        public VendorBuyback.Result SellVendorItem(uint character,ushort slot,string session,uint vendor)
        {
            if(!VendorBuyback.ReceiptSlot(slot)||!ReceiptSession(session))return VendorBuyback.Result.Denied("That sale is not available.");
            using var connection=new MySqlConnection(connectionString);connection.Open();RequireBuybackEngines(connection);
            using var tx=connection.BeginTransaction();using var c=connection.CreateCommand();c.Transaction=tx;c.Parameters.AddWithValue("@char",character);
            long? before=BuybackCash(c);if(!before.HasValue)return VendorBuyback.Result.Denied("The character is unavailable.");
            var rows=BuybackInventory(c);ReceiptLock(c);var matches=rows.Where(r=>r.Slot==slot).ToArray();
            if(matches.Length!=1)return VendorBuyback.Result.Denied(matches.Length==0?"There is nothing to sell in that slot.":"That inventory slot is ambiguous.");
            var item=matches[0];
            if(!VendorBuyback.TryDescriptor(item,out uint goid,out _))return VendorBuyback.Result.Denied("That item's exact properties cannot be represented for buyback.");
            uint quote=VendorPricing.GetSellPrice(goid);if(quote==0)return VendorBuyback.Result.Denied("This item cannot be sold.");
            uint cash=VendorTrading.Credit(before.Value,quote,out uint paid);
            c.Parameters.AddWithValue("@id",item.OriginalId);c.Parameters.AddWithValue("@goid",item.RawGoid);
            c.Parameters.AddWithValue("@count",item.RawCount);c.Parameters.AddWithValue("@purity",item.Purity);c.Parameters.AddWithValue("@slot",item.Slot);
            c.CommandText="DELETE FROM inventory WHERE charId=@char AND invId=@id AND goid=@goid AND count=@count AND purity=@purity AND slot=@slot";
            if(c.ExecuteNonQuery()!=1)throw new InvalidOperationException("Selected sale row changed.");
            c.Parameters.AddWithValue("@session",session);c.Parameters.AddWithValue("@vendor",vendor);c.Parameters.AddWithValue("@paid",paid);
            c.Parameters.AddWithValue("@created",item.Created);c.Parameters.AddWithValue("@updated",item.Updated);
            c.CommandText=@"INSERT INTO vendor_last_sale(char_id,session_token,vendor_id,original_id,raw_goid,raw_count,purity,original_slot,paid,original_created,original_updated)
VALUES(@char,@session,@vendor,@id,@goid,@count,@purity,@slot,@paid,@created,@updated)
ON DUPLICATE KEY UPDATE session_token=@session,vendor_id=@vendor,original_id=@id,raw_goid=@goid,raw_count=@count,purity=@purity,original_slot=@slot,paid=@paid,original_created=@created,original_updated=@updated";
            c.ExecuteNonQuery();c.Parameters.AddWithValue("@cash",cash);
            c.CommandText="UPDATE characters SET cash=@cash WHERE charId=@char";if(c.ExecuteNonQuery()!=1&&cash!=before.Value)throw new InvalidOperationException("Sale wallet changed.");
            tx.Commit();return new VendorBuyback.Result(true,item,cash,paid);
        }
        internal static bool ReceiptSession(string session)=>session!=null&&session.Length==32&&session.All(Uri.IsHexDigit);
        public VendorBuyback.Result BuyBackVendorItem(uint character,string session,uint vendor)
        {
            if(!ReceiptSession(session))return VendorBuyback.Result.Denied("There is no current last-sale receipt.");
            using var connection=new MySqlConnection(connectionString);connection.Open();RequireBuybackEngines(connection);
            using var tx=connection.BeginTransaction();using var c=connection.CreateCommand();c.Transaction=tx;c.Parameters.AddWithValue("@char",character);
            long? before=BuybackCash(c);if(!before.HasValue)return VendorBuyback.Result.Denied("The character is unavailable.");
            var rows=BuybackInventory(c);
            c.CommandText="SELECT session_token,vendor_id,original_id,raw_goid,raw_count,purity,original_slot,paid,original_created,original_updated FROM vendor_last_sale WHERE char_id=@char FOR UPDATE";
            VendorBuyback.Item item;uint paid;
            using(var r=c.ExecuteReader())
            {
                if(!r.Read()||r.GetString(0)!=session||r.GetUInt32(1)!=vendor)return VendorBuyback.Result.Denied("There is no last sale for this vendor and world session.");
                item=new VendorBuyback.Item(r.GetUInt32(2),r.GetInt64(3),r.GetInt32(4),r.GetInt32(5),r.GetUInt16(6),r.GetString(8),r.GetString(9));paid=r.GetUInt32(7);
            }
            if(!VendorBuyback.TryDescriptor(item,out _,out _))return VendorBuyback.Result.Denied("That receipt has unsupported item properties.");
            if(before.Value<paid||before.Value<0||before.Value>int.MaxValue)return VendorBuyback.Result.Denied("You do not have enough information to buy back this item.");
            var occupied=rows.Select(r=>(int)r.Slot).ToHashSet();int slot=Enumerable.Range(1,96).FirstOrDefault(s=>!occupied.Contains(s));
            if(slot==0)return VendorBuyback.Result.Denied("Your inventory is full. The last-sale receipt was kept.");
            c.Parameters.AddWithValue("@slot",slot);c.Parameters.AddWithValue("@goid",item.RawGoid);c.Parameters.AddWithValue("@count",item.RawCount);c.Parameters.AddWithValue("@purity",item.Purity);
            // New row identity/timestamps; original identity and possibly-zero timestamps remain receipt provenance.
            c.CommandText="INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(@char,@goid,@slot,@count,@purity)";c.ExecuteNonQuery();
            uint cash=(uint)(before.Value-paid);c.Parameters.AddWithValue("@cash",cash);
            c.CommandText="UPDATE characters SET cash=@cash WHERE charId=@char";if(c.ExecuteNonQuery()!=1&&paid!=0)throw new InvalidOperationException("Buyback wallet changed.");
            c.Parameters.AddWithValue("@session",session);c.Parameters.AddWithValue("@vendor",vendor);
            c.CommandText="DELETE FROM vendor_last_sale WHERE char_id=@char AND session_token=@session AND vendor_id=@vendor";
            if(c.ExecuteNonQuery()!=1)throw new InvalidOperationException("Buyback receipt changed.");
            tx.Commit();var restored=new VendorBuyback.Item(item.OriginalId,item.RawGoid,item.RawCount,item.Purity,(ushort)slot,item.Created,item.Updated);
            return new VendorBuyback.Result(true,restored,cash,paid);
        }
    }
}
