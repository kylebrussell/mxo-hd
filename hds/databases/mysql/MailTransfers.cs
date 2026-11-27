using System;
using System.Collections.Generic;
using MySqlConnector;

namespace hds.databases
{
    public interface IMailTransfer : IDisposable
    {
        long ReadCash(uint character);
        void SetCash(uint character, uint cash);
        List<(ushort Slot, MailItem Item)> ReadBag(uint character);
        void RemoveItem(uint character, ushort slot);
        void AddItem(uint character, ushort slot, MailItem item);
        int InboxCount(uint character);
        void AddMessage(MailMessage message);
        MailMessage ReadMessage(uint character, uint id);
        void SetAttachments(MailMessage message);
        void Commit();
    }

    public partial class MySqlMailStore
    {
        internal IMailTransfer BeginTransfer()
        {
            var connection = Open();
            try
            {
                using var command = Cmd(connection, "SELECT COUNT(*) FROM information_schema.TABLES " +
                    "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','inventory','mail') AND ENGINE='InnoDB'");
                if (Convert.ToInt32(command.ExecuteScalar()) != 3)
                    throw new InvalidOperationException("Mail transfers require transactional character, inventory and mail tables.");
                return new Transfer(connection, connection.BeginTransaction());
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private sealed class Transfer : IMailTransfer
        {
            private readonly MySqlConnection connection;
            private readonly MySqlTransaction transaction;

            internal Transfer(MySqlConnection connection, MySqlTransaction transaction)
            {
                this.connection = connection;
                this.transaction = transaction;
            }

            private MySqlCommand Prepare(string sql, params object[] parameters)
            {
                var command = Cmd(connection, sql, parameters);
                command.Transaction = transaction;
                return command;
            }

            public long ReadCash(uint character)
            {
                using var command = Prepare("SELECT cash FROM characters WHERE charId=@c FOR UPDATE", "@c", character);
                object result = command.ExecuteScalar();
                return result == null ? -1 : Convert.ToInt64(result);
            }

            public void SetCash(uint character, uint cash)
            {
                using var command = Prepare("UPDATE characters SET cash=@cash WHERE charId=@c", "@c", character, "@cash", cash);
                RequireWrite(command);
            }

            public List<(ushort Slot, MailItem Item)> ReadBag(uint character)
            {
                using var command = Prepare("SELECT slot,goid,purity,count FROM inventory " +
                    "WHERE charId=@c AND slot BETWEEN 0 AND 96 ORDER BY slot FOR UPDATE", "@c", character);
                using var rows = command.ExecuteReader();
                var bag = new List<(ushort, MailItem)>();
                while (rows.Read())
                {
                    bag.Add((rows.GetUInt16(0), new MailItem
                    {
                        Goid = unchecked((uint)rows.GetInt64(1)),
                        Purity = unchecked((uint)rows.GetInt32(2)),
                        Count = ItemStacks.CountFromDb(rows.GetInt32(3))
                    }));
                }
                return bag;
            }

            public void RemoveItem(uint character, ushort slot)
            {
                using var command = Prepare("DELETE FROM inventory WHERE charId=@c AND slot=@slot", "@c", character, "@slot", slot);
                RequireWrite(command);
            }

            public void AddItem(uint character, ushort slot, MailItem item)
            {
                using var command = Prepare("INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(@c,@item,@slot,@count,@purity)",
                    "@c", character, "@item", item.Goid, "@slot", slot, "@count", item.Count > 1 ? item.Count : 0,
                    "@purity", unchecked((int)item.Purity));
                RequireWrite(command);
            }

            public int InboxCount(uint character)
            {
                using var command = Prepare("SELECT COUNT(*) FROM mail WHERE ownerCharId=@c", "@c", character);
                return Convert.ToInt32(command.ExecuteScalar());
            }

            public void AddMessage(MailMessage message) => Insert(connection, transaction, message);

            public MailMessage ReadMessage(uint character, uint id)
            {
                using var command = Prepare("SELECT * FROM mail WHERE ownerCharId=@c AND id=@id FOR UPDATE", "@c", character, "@id", id);
                using var rows = command.ExecuteReader();
                return rows.Read() ? Read(rows) : null;
            }

            public void SetAttachments(MailMessage message)
            {
                using var command = Prepare("UPDATE mail SET items=@items WHERE ownerCharId=@c AND id=@id",
                    "@items", FormatItems(message.Items), "@c", message.OwnerCharId, "@id", message.Id);
                RequireWrite(command);
            }

            private static void RequireWrite(MySqlCommand command)
            {
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("A mail transfer row changed before it could be written.");
            }

            public void Commit() => transaction.Commit();

            public void Dispose()
            {
                try { transaction.Dispose(); }
                finally { connection.Dispose(); }
            }
        }
    }
}
