using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private sealed class TradeAccount
        {
            public readonly uint Character;
            public readonly TradeOffer Offer;
            public readonly Dictionary<int, TradeItem> Bag = new Dictionary<int, TradeItem>();
            public readonly List<byte[]> BagRecords = new List<byte[]>();
            public long Balance;
            public long InfoChange;

            public TradeAccount(uint character, TradeOffer offer)
            {
                Character = character;
                Offer = offer;
            }

            public TradeSettlement.Receipt Receipt() => new TradeSettlement.Receipt(
                (uint)(Balance + InfoChange), InfoChange, BagRecords);
        }

        public TradeItem GetTradeItem(uint character, byte slot)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var command = new MySqlCommand(
                "SELECT invId,slot,goid,count,purity FROM inventory WHERE charId=@character AND slot=@slot", connection);
            command.Parameters.AddWithValue("@character", character);
            command.Parameters.AddWithValue("@slot", slot);
            using var rows = command.ExecuteReader();
            if (!rows.Read()) return null;
            var item = ReadTradeRow(rows);
            return item.Goid == 0 || item.Quantity > ItemStacks.MaxStack || rows.Read() ? null : item;
        }

        public TradeSettlement SettleTrade(uint requester, TradeOffer requesterOffer, uint target, TradeOffer targetOffer)
        {
            if (requester == 0 || requester == target) return TradeSettlement.Denied("Invalid trade participants.");
            var first = new TradeAccount(requester, requesterOffer);
            // The QA trade bot has an offer and no persisted inventory or wallet.
            var second = new TradeAccount(target, targetOffer);
            var players = new[] { first, second }.Where(account => account.Character != 0)
                .OrderBy(account => account.Character).ToArray();
            first.InfoChange = targetOffer.Info - requesterOffer.Info;
            second.InfoChange = -first.InfoChange;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using (var engines = new MySqlCommand("SELECT COUNT(*) FROM information_schema.TABLES " +
                "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','inventory') AND ENGINE='InnoDB'", connection))
            {
                if (Convert.ToInt32(engines.ExecuteScalar()) != 2)
                    throw new InvalidOperationException("Secure Trade requires transactional character and inventory tables.");
            }
            using var tx = connection.BeginTransaction();

            // Use the same wallet order as mail and marketplace before locking either bag.
            foreach (var player in players)
            {
                using var cash = TradeCommand(tx, "SELECT cash FROM characters WHERE charId=@character FOR UPDATE",
                    "@character", player.Character);
                object stored = cash.ExecuteScalar();
                if (stored == null || stored is DBNull)
                    return TradeSettlement.Denied("A trader is no longer available.");
                player.Balance = Convert.ToInt64(stored);
                if (player.Offer.Info > player.Balance)
                    return TradeSettlement.Denied("A trader no longer has enough information.");
                if (player.Balance + player.InfoChange > int.MaxValue)
                    return TradeSettlement.Denied("A trader cannot hold that much information.");
            }
            foreach (var player in players)
            {
                using var bag = TradeCommand(tx,
                    "SELECT invId,slot,goid,count,purity FROM inventory WHERE charId=@character AND slot BETWEEN 0 AND 96 " +
                    "ORDER BY slot,invId FOR UPDATE", "@character", player.Character);
                using var rows = bag.ExecuteReader();
                while (rows.Read())
                {
                    TradeItem item = ReadTradeRow(rows);
                    if (!player.Bag.TryAdd(item.Slot, item))
                        return TradeSettlement.Denied("A backpack slot is ambiguous.");
                }
                foreach (TradeItem offered in player.Offer.Items.Values)
                {
                    if (!player.Bag.TryGetValue(offered.Slot, out var item) || item.InventoryId != offered.InventoryId ||
                        item.Goid != offered.Goid || item.Purity != offered.Purity || item.Quantity < offered.Quantity ||
                        DataMining.IsNonTradeable(item.Goid))
                        return TradeSettlement.Denied("An offered item changed. Review the offer and try again.");
                }
            }

            foreach (var player in players) RemoveTradeOffer(tx, player);
            if (!ReceiveTradeOffer(tx, first, targetOffer) ||
                (second.Character != 0 && !ReceiveTradeOffer(tx, second, requesterOffer)))
                return TradeSettlement.Denied("A trader does not have enough backpack space.");

            foreach (var player in players.Where(account => account.InfoChange != 0))
            {
                using var cash = TradeCommand(tx, "UPDATE characters SET cash=@cash WHERE charId=@character",
                    "@cash", player.Balance + player.InfoChange, "@character", player.Character);
                WriteTradeRow(cash);
            }
            tx.Commit();
            return new TradeSettlement(first.Receipt(), second.Character == 0 ? null : second.Receipt());
        }

        private static TradeItem ReadTradeRow(MySqlDataReader row)
        {
            long goid = row.GetInt64(2);
            return new TradeItem
            {
                InventoryId = row.GetUInt32(0),
                Slot = row.GetByte(1),
                Goid = goid > 0 && goid <= uint.MaxValue ? (uint)goid : 0,
                Quantity = ItemStacks.CountFromDb(row.GetInt32(3)),
                Purity = unchecked((uint)row.GetInt32(4))
            };
        }

        private static void RemoveTradeOffer(MySqlTransaction tx, TradeAccount player)
        {
            foreach (TradeItem offered in player.Offer.Items.Values)
            {
                TradeItem item = player.Bag[offered.Slot];
                int remaining = item.Quantity - offered.Quantity;
                if (remaining == 0)
                {
                    using var remove = TradeCommand(tx, "DELETE FROM inventory WHERE invId=@id", "@id", item.InventoryId);
                    WriteTradeRow(remove);
                    player.Bag.Remove(item.Slot);
                    player.BagRecords.Add(ServerPackets.BuildInventoryItemDelete(item.Slot));
                }
                else
                {
                    SetTradeStack(tx, player, item, remaining);
                }
            }
        }

        private static bool ReceiveTradeOffer(MySqlTransaction tx, TradeAccount player, TradeOffer offer)
        {
            foreach (TradeItem item in offer.Items.Values)
            {
                TradeItem stack = ItemStacks.IsStackable(item.Goid)
                    ? player.Bag.Values.FirstOrDefault(existing => existing.Goid == item.Goid &&
                        existing.Purity == item.Purity && existing.Quantity <= ItemStacks.MaxStack - item.Quantity)
                    : null;
                if (stack != null)
                {
                    SetTradeStack(tx, player, stack, stack.Quantity + item.Quantity);
                    continue;
                }

                ushort slot = TradeRules.FreeBagSlots(player.Bag.Keys).FirstOrDefault();
                if (slot == 0) return false;
                using var insert = TradeCommand(tx, "INSERT INTO inventory(charId,goid,slot,count,purity) " +
                    "VALUES(@character,@goid,@slot,@count,@purity)", "@character", player.Character, "@goid", item.Goid,
                    "@slot", slot, "@count", item.Quantity == 1 ? 0 : item.Quantity, "@purity", unchecked((int)item.Purity));
                WriteTradeRow(insert);
                player.Bag.Add(slot, new TradeItem
                {
                    InventoryId = (uint)insert.LastInsertedId, Slot = (byte)slot,
                    Goid = item.Goid, Quantity = item.Quantity, Purity = item.Purity
                });
                player.BagRecords.Add(ServerPackets.BuildInventoryItemAdd(slot, item.Goid, TradePackets.Descriptor(item.Purity, item.Quantity)));
            }
            return true;
        }

        private static void SetTradeStack(MySqlTransaction tx, TradeAccount player, TradeItem item, int quantity)
        {
            using var update = TradeCommand(tx, "UPDATE inventory SET count=@count WHERE invId=@id",
                "@count", quantity, "@id", item.InventoryId);
            WriteTradeRow(update);
            item.Quantity = quantity;
            player.BagRecords.Add(ItemStacks.BuildStackCount(item.Slot, quantity));
        }

        private static MySqlCommand TradeCommand(MySqlTransaction tx, string sql, params object[] parameters)
        {
            var command = new MySqlCommand(sql, tx.Connection, tx);
            for (int i = 0; i < parameters.Length; i += 2)
                command.Parameters.AddWithValue((string)parameters[i], parameters[i + 1]);
            return command;
        }

        private static void WriteTradeRow(MySqlCommand command)
        {
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("An inventory or wallet row changed during Secure Trade.");
        }
    }
}
