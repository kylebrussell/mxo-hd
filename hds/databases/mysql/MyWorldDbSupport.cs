using System;
using System.Collections.Generic;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public uint FindSupportGroup(string name, bool faction)
        {
            object id = ScalarQuery(faction ? "SELECT id FROM factions WHERE name=@name AND deleted_at IS NULL LIMIT 1" :
                "SELECT id FROM crews WHERE crew_name=@name AND (deleted_at IS NULL OR deleted_at < '1971-01-01') LIMIT 1",
                new KeyValuePair<string, object>("@name", name));
            return id == null || id == DBNull.Value ? 0 : Convert.ToUInt32(id);
        }
        private void EnsureLocalSupport() => ExecuteNonResultQuery("CREATE TABLE IF NOT EXISTS local_support_tickets (" +
            "id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,charId INT UNSIGNED NOT NULL,handle VARCHAR(128) NOT NULL," +
            "body TEXT NOT NULL,created INT UNSIGNED NOT NULL,modified INT UNSIGNED NOT NULL,cancelled TINYINT NOT NULL DEFAULT 0," +
            "INDEX owner_idx(charId)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

        public List<LocalSupport.Ticket> SupportTickets(uint character)
        {
            EnsureLocalSupport(); OpenConnection();
            try
            {
                using var c = conn.CreateCommand(); c.CommandText = "SELECT id,handle,body,created,modified,cancelled FROM local_support_tickets WHERE charId=" + character + " ORDER BY id DESC LIMIT 100";
                using var r = c.ExecuteReader(); var tickets = new List<LocalSupport.Ticket>();
                while (r.Read()) tickets.Add(new LocalSupport.Ticket { Id = Convert.ToUInt32(r[0]), Character = character,
                    Handle = Convert.ToString(r[1]), Text = Convert.ToString(r[2]), Created = Convert.ToUInt32(r[3]),
                    Modified = Convert.ToUInt32(r[4]), Cancelled = Convert.ToBoolean(r[5]) });
                return tickets;
            }
            finally { CloseConnection(); }
        }

        public uint SaveSupportTicket(uint character, string handle, uint ticket, string text, bool cancel)
        {
            EnsureLocalSupport(); OpenConnection();
            try
            {
                using var tx = conn.BeginTransaction(); using var c = conn.CreateCommand(); c.Transaction = tx;
                void Parameter(string name, object value) { var p = c.CreateParameter(); p.ParameterName = name; p.Value = value; c.Parameters.Add(p); }
                Parameter("@char", character); Parameter("@handle", handle); Parameter("@body", text);
                Parameter("@id", ticket); Parameter("@now", TimeUtils.getUnixTimeUint32());
                if (ticket == 0 && !cancel)
                {
                    c.CommandText = "SELECT COUNT(*) FROM local_support_tickets WHERE charId=@char AND cancelled=0 FOR UPDATE";
                    if (Convert.ToInt32(c.ExecuteScalar()) >= 10) return 0;
                    c.CommandText = "INSERT INTO local_support_tickets(charId,handle,body,created,modified) VALUES(@char,@handle,@body,@now,@now)";
                    c.ExecuteNonQuery(); c.CommandText = "SELECT LAST_INSERT_ID()"; ticket = Convert.ToUInt32(c.ExecuteScalar());
                }
                else
                {
                    c.CommandText = cancel
                        ? "UPDATE local_support_tickets SET cancelled=1,modified=@now WHERE id=@id AND charId=@char AND cancelled=0"
                        : "UPDATE local_support_tickets SET body=@body,modified=@now WHERE id=@id AND charId=@char AND cancelled=0";
                    if (c.ExecuteNonQuery() != 1) return 0;
                }
                tx.Commit(); return ticket;
            }
            finally { CloseConnection(); }
        }
    }
}
