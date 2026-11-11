using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using hds.shared;
using MySqlConnector;

namespace hds.databases
{
    /// <summary>Where messages live. Messages that are purged (status 5) are removed, not kept.</summary>
    public interface IMailStore
    {
        /// <summary>Stores a new message and gives it an id.</summary>
        UInt32 Add(MailMessage message);

        /// <summary>The owner's messages, newest first.</summary>
        List<MailMessage> List(UInt32 ownerCharId);

        MailMessage Get(UInt32 ownerCharId, UInt32 id);

        /// <summary>Saves status, folder and attachments.</summary>
        void Update(MailMessage message);

        bool Delete(UInt32 ownerCharId, UInt32 id);

        int Count(UInt32 ownerCharId);
    }

    /// <summary>For tests and for running without a database.</summary>
    public class InMemoryMailStore : IMailStore
    {
        private readonly object sync = new object();
        private readonly List<MailMessage> messages = new List<MailMessage>();
        private UInt32 nextId = 1;

        public UInt32 Add(MailMessage message)
        {
            lock (sync)
            {
                message.Id = nextId++;
                messages.Add(message.Copy());
                return message.Id;
            }
        }

        public List<MailMessage> List(UInt32 ownerCharId)
        {
            lock (sync)
            {
                return messages.Where(m => m.OwnerCharId == ownerCharId)
                    .OrderByDescending(m => m.SentTime).ThenByDescending(m => m.Id).Select(m => m.Copy()).ToList();
            }
        }

        public MailMessage Get(UInt32 ownerCharId, UInt32 id)
        {
            lock (sync)
            {
                return messages.FirstOrDefault(m => m.OwnerCharId == ownerCharId && m.Id == id)?.Copy();
            }
        }

        public void Update(MailMessage message)
        {
            lock (sync)
            {
                int index = messages.FindIndex(m => m.Id == message.Id && m.OwnerCharId == message.OwnerCharId);
                if (index >= 0)
                {
                    messages[index] = message.Copy();
                }
            }
        }

        public bool Delete(UInt32 ownerCharId, UInt32 id)
        {
            lock (sync)
            {
                return messages.RemoveAll(m => m.OwnerCharId == ownerCharId && m.Id == id) > 0;
            }
        }

        public int Count(UInt32 ownerCharId)
        {
            lock (sync)
            {
                return messages.Count(m => m.OwnerCharId == ownerCharId);
            }
        }
    }

    /// <summary>
    /// Table <c>mail</c> (made on first use, and in SQL/reality_hd.sql): one row per message and owner. Attachments
    /// are kept as text, <c>goid:purity:count</c> separated by commas.
    /// </summary>
    public class MySqlMailStore : IMailStore
    {
        private static readonly object EnsureLock = new object();
        private static bool ensured;

        public const string CreateTable =
            "CREATE TABLE IF NOT EXISTS mail (" +
            "id INT UNSIGNED NOT NULL AUTO_INCREMENT, " +
            "ownerCharId INT UNSIGNED NOT NULL, " +
            "sender VARCHAR(128) NOT NULL DEFAULT '', " +
            "folder VARCHAR(64) NOT NULL DEFAULT '', " +
            "category VARCHAR(32) NOT NULL DEFAULT 'PLAYER', " +
            "sentTime INT UNSIGNED NOT NULL DEFAULT 0, " +
            "status INT UNSIGNED NOT NULL DEFAULT 1, " +
            "subject VARCHAR(255) NOT NULL DEFAULT '', " +
            "body TEXT NOT NULL, " +
            "items VARCHAR(1024) NOT NULL DEFAULT '', " +
            "PRIMARY KEY (id), KEY owner (ownerCharId)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

        private static MySqlConnection Open()
        {
            var config = Store.config;
            var conn = new MySqlConnection("Server=" + config.dbParams.Host + ";Port=" + config.dbParams.Port +
                                           ";Database=" + config.dbParams.DatabaseName + ";User ID=" +
                                           config.dbParams.Username + ";Password=" + config.dbParams.Password +
                                           ";Pooling=true;");
            conn.Open();
            lock (EnsureLock)
            {
                if (!ensured)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = CreateTable;
                        cmd.ExecuteNonQuery();
                    }
                    ensured = true;
                }
            }
            return conn;
        }

        private static MySqlCommand Cmd(MySqlConnection conn, string sql, params object[] nameValues)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i + 1 < nameValues.Length; i += 2)
            {
                cmd.Parameters.AddWithValue((string)nameValues[i], nameValues[i + 1]);
            }
            return cmd;
        }

        public static string FormatItems(IEnumerable<MailItem> items)
        {
            return string.Join(",", items.Select(i => i.Goid.ToString(CultureInfo.InvariantCulture) + ":" +
                                                       i.Purity.ToString(CultureInfo.InvariantCulture) + ":" +
                                                       i.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static List<MailItem> ParseItems(string text)
        {
            List<MailItem> items = new List<MailItem>();
            foreach (string entry in (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 3 && UInt32.TryParse(parts[0], out UInt32 goid) && UInt32.TryParse(parts[1], out UInt32 purity) &&
                    int.TryParse(parts[2], out int count))
                {
                    items.Add(new MailItem { Goid = goid, Purity = purity, Count = count });
                }
            }
            return items;
        }

        private static MailMessage Read(IDataRecord r)
        {
            return new MailMessage
            {
                Id = Convert.ToUInt32(r["id"]),
                OwnerCharId = Convert.ToUInt32(r["ownerCharId"]),
                Sender = Convert.ToString(r["sender"]),
                Folder = Convert.ToString(r["folder"]),
                Category = Convert.ToString(r["category"]),
                SentTime = Convert.ToUInt32(r["sentTime"]),
                Status = Convert.ToUInt32(r["status"]),
                Subject = Convert.ToString(r["subject"]),
                Body = Convert.ToString(r["body"]),
                Items = ParseItems(Convert.ToString(r["items"]))
            };
        }

        /// <summary>A character's handle, or null.</summary>
        public static string HandleByCharId(UInt32 charId)
        {
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT handle FROM characters WHERE charId = @c LIMIT 1", "@c", (long)charId))
            {
                object value = cmd.ExecuteScalar();
                return value == null || value is DBNull ? null : Convert.ToString(value);
            }
        }

        public UInt32 Add(MailMessage message)
        {
            using (var conn = Open())
            using (var cmd = Cmd(conn,
                       "INSERT INTO mail (ownerCharId, sender, folder, category, sentTime, status, subject, body, items) " +
                       "VALUES (@o, @s, @f, @c, @t, @st, @sub, @b, @i); SELECT LAST_INSERT_ID();",
                       "@o", (long)message.OwnerCharId, "@s", message.Sender, "@f", message.Folder, "@c", message.Category,
                       "@t", (long)message.SentTime, "@st", (long)message.Status, "@sub", message.Subject, "@b", message.Body,
                       "@i", FormatItems(message.Items)))
            {
                message.Id = Convert.ToUInt32(cmd.ExecuteScalar());
                return message.Id;
            }
        }

        public List<MailMessage> List(UInt32 ownerCharId)
        {
            var result = new List<MailMessage>();
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT * FROM mail WHERE ownerCharId = @o ORDER BY sentTime DESC, id DESC",
                       "@o", (long)ownerCharId))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    result.Add(Read(r));
                }
            }
            return result;
        }

        public MailMessage Get(UInt32 ownerCharId, UInt32 id)
        {
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT * FROM mail WHERE ownerCharId = @o AND id = @id LIMIT 1",
                       "@o", (long)ownerCharId, "@id", (long)id))
            using (var r = cmd.ExecuteReader())
            {
                return r.Read() ? Read(r) : null;
            }
        }

        public void Update(MailMessage message)
        {
            using (var conn = Open())
            {
                Cmd(conn, "UPDATE mail SET status = @st, folder = @f, items = @i WHERE ownerCharId = @o AND id = @id",
                    "@st", (long)message.Status, "@f", message.Folder, "@i", FormatItems(message.Items),
                    "@o", (long)message.OwnerCharId, "@id", (long)message.Id).ExecuteNonQuery();
            }
        }

        public bool Delete(UInt32 ownerCharId, UInt32 id)
        {
            using (var conn = Open())
            {
                return Cmd(conn, "DELETE FROM mail WHERE ownerCharId = @o AND id = @id", "@o", (long)ownerCharId,
                    "@id", (long)id).ExecuteNonQuery() > 0;
            }
        }

        public int Count(UInt32 ownerCharId)
        {
            using (var conn = Open())
            {
                return Convert.ToInt32(Cmd(conn, "SELECT COUNT(*) FROM mail WHERE ownerCharId = @o", "@o", (long)ownerCharId)
                    .ExecuteScalar());
            }
        }
    }
}
