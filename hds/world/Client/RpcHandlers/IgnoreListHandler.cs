using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using hds.databases;
using hds.shared;
using MySqlConnector;

namespace hds
{
    /// <summary>
    /// The ignore list (/ignore, the buddy list's Ignore tab): client 80 e0 adds, 80 e2 removes; the server answers
    /// 80 e1 / 80 e3. No capture has either, so this is client.dll's: 80 e1 is &lt;name offset u16&gt; &lt;status u32&gt;,
    /// and the client (0x62255bb0) adds the name and prints "Ignoring %s." when the status is a success (top bit
    /// set), "Failed to ignore %s." otherwise. The client filters what it ignores itself: it hides chat, refuses to
    /// mail an ignored player, and deletes mail from one (81 fe). The server only keeps the list.
    /// </summary>
    public static class IgnoreListPackets
    {
        public const UInt32 Success = 0x8e000084;                 // LTOBJSRV_SOECHAT_SUCCESS
        public const UInt32 Listed = 0x8e00003d;                  // LTOBJSRV_CONTACT_LIST_SETUP_PLAYER_IGNORED (at login; a guess)
        public const UInt32 NoSuchPlayer = 0x0e000041;            // LTOBJSRV_SOECHAT_DESTAVATARDOESNTEXIST
        public const UInt32 NotIgnored = 0x0e000058;              // LTOBJSRV_SOECHAT_IGNORENOTFOUND
        public const UInt32 Ignoring = 0x0e000054;                // LTOBJSRV_SOECHAT_IGNORING (23rd name: 0x3d + index)

        private static byte[] Record(byte opcode, string name, UInt32 status)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(name ?? "");
                writer.Write((byte)0x80);
                writer.Write(opcode);
                writer.Write((UInt16)8);
                writer.Write(status);
                writer.Write((UInt16)(bytes.Length + 1));
                writer.Write(bytes);
                writer.Write((byte)0);
                return stream.ToArray();
            }
        }

        /// <summary><c>80 e1 08 00 &lt;status&gt; &lt;sized name&gt;</c>, laid out like the friend list's 80 d7.</summary>
        public static byte[] BuildAdded(string nameWithPrefix, UInt32 status) => Record(0xe1, nameWithPrefix, status);

        /// <summary><c>80 e3 08 00 &lt;status&gt; &lt;sized name&gt;</c>.</summary>
        public static byte[] BuildRemoved(string nameWithPrefix, UInt32 status) => Record(0xe3, nameWithPrefix, status);

        /// <summary>The name in 80 e0 / 80 e2 (after the two opcode bytes): &lt;offset u16&gt;, the sized name there.</summary>
        public static bool TryParseName(byte[] rpcData, out string name)
        {
            name = null;
            return rpcData != null && rpcData.Length >= 4 &&
                   SocialRequests.TryReadSized(rpcData, (rpcData[0] | (rpcData[1] << 8)) - 2, out name);
        }
    }

    /// <summary>Table <c>ignorelist</c> (made on first use): who ignores which handle.</summary>
    public class IgnoreListDb
    {
        private static readonly object EnsureLock = new object();
        private static bool ensured;

        public const string CreateTable =
            "CREATE TABLE IF NOT EXISTS ignorelist (charId INT UNSIGNED NOT NULL, handle VARCHAR(64) NOT NULL, " +
            "PRIMARY KEY (charId, handle)) ENGINE=InnoDB DEFAULT CHARSET=latin1";

        private static MySqlConnection Open()
        {
            var config = Store.config;
            var conn = new MySqlConnection("Server=" + config.dbParams.Host + ";Port=" + config.dbParams.Port +
                                           ";Database=" + config.dbParams.DatabaseName + ";User ID=" +
                                           config.dbParams.Username + ";Password=" + config.dbParams.Password +
                                           ";Pooling=true;");
            conn.Open();
            EnsureTable(conn);
            return conn;
        }

        internal static void EnsureTable(MySqlConnection conn)
        {
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
        }

        private static MySqlCommand Cmd(MySqlConnection conn, string sql, UInt32 charId, string handle = null)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@c", (long)charId);
            if (handle != null)
            {
                cmd.Parameters.AddWithValue("@h", handle);
            }
            return cmd;
        }

        public virtual List<string> List(UInt32 charId)
        {
            var handles = new List<string>();
            using (var conn = Open())
            using (var cmd = Cmd(conn, "SELECT handle FROM ignorelist WHERE charId = @c ORDER BY handle", charId))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    handles.Add(r.GetString(0));
                }
            }
            return handles;
        }

        public virtual void Add(UInt32 charId, string handle)
        {
            using (var conn = Open())
            {
                Cmd(conn, "INSERT IGNORE INTO ignorelist (charId, handle) VALUES (@c, @h)", charId, handle).ExecuteNonQuery();
            }
        }

        public virtual bool Remove(UInt32 charId, string handle)
        {
            using (var conn = Open())
            {
                return Cmd(conn, "DELETE FROM ignorelist WHERE charId = @c AND handle = @h", charId, handle).ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Server-side ignore: the online players' stored lists, kept in memory so delivery never queries the database.
    /// The stock client also hides ignored lines itself. client.dll's chat result table has LTOBJSRV_SOECHAT_IGNORING,
    /// so the original chat service answered a tell to a player who ignored the sender with that code.
    /// </summary>
    public static class IgnoreFilter
    {
        private static readonly ConcurrentDictionary<UInt32, HashSet<string>> lists = new ConcurrentDictionary<UInt32, HashSet<string>>();

        internal static int TrackedCharacterCount => lists.Count;

        internal static void Load(UInt32 charId, IEnumerable<string> handles)
        {
            if (handles == null)
            {
                lists.TryRemove(charId, out _);
                return;
            }
            var set = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);
            if (set.Count == 0)
            {
                lists.TryRemove(charId, out _);
                return;
            }
            lists[charId] = set;
        }

        internal static void RemovePlayer(UInt32 charId) => lists.TryRemove(charId, out _);

        internal static void Add(UInt32 charId, string handle)
        {
            var list = lists.GetOrAdd(charId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            lock (list) list.Add(handle);
        }

        internal static void Remove(UInt32 charId, string handle)
        {
            if (!lists.TryGetValue(charId, out var list)) return;
            lock (list) list.Remove(handle);
        }

        /// <summary>Whether the receiver ignores the sender's handle.</summary>
        public static bool Ignores(WorldClient receiver, string senderHandle)
        {
            if (receiver?.playerData == null || string.IsNullOrEmpty(senderHandle) ||
                !lists.TryGetValue(receiver.playerData.getCharID(), out var list)) return false;
            lock (list) return list.Contains(senderHandle);
        }

        public static bool Ignores(WorldClient receiver, WorldClient sender) =>
            sender != null && Ignores(receiver, SocialHandler.HandleOf(sender));
    }

    public class IgnoreListHandler
    {
        internal static IgnoreListDb Db = new IgnoreListDb();

        /// <summary>Resolves a handle to its spelling in the database, or null when nobody has it.</summary>
        internal static Func<string, string> CanonicalHandle = handle =>
        {
            UInt32 charId = Store.dbManager.WorldDbHandler.GetCharIdByHandle(handle);
            return charId == 0 ? null : MySqlMailStore.HandleByCharId(charId);
        };

        private static string Prefix => ChatAddress.Prefix;

        /// <summary>Client 80 e0: ignore a player.</summary>
        public static void ProcessAdd(WorldClient client, byte[] rpcData)
        {
            if (!IgnoreListPackets.TryParseName(rpcData, out string name))
            {
                return;
            }
            string handle = MailPackets.HandleOf(name);
            string known = handle.Length > 0 ? CanonicalHandle(handle) : null;
            if (known == null)
            {
                SocialHandler.Send(client, IgnoreListPackets.BuildAdded(name, IgnoreListPackets.NoSuchPlayer));
                return;
            }
            Db.Add(client.playerData.getCharID(), known);
            IgnoreFilter.Add(client.playerData.getCharID(), known);
            SocialHandler.Send(client, IgnoreListPackets.BuildAdded(Prefix + known, IgnoreListPackets.Success));
        }

        /// <summary>Client 80 e2: stop ignoring a player.</summary>
        public static void ProcessRemove(WorldClient client, byte[] rpcData)
        {
            if (!IgnoreListPackets.TryParseName(rpcData, out string name))
            {
                return;
            }
            string handle = MailPackets.HandleOf(name);
            bool removed = Db.Remove(client.playerData.getCharID(), handle);
            IgnoreFilter.Remove(client.playerData.getCharID(), handle);
            SocialHandler.Send(client, IgnoreListPackets.BuildRemoved(name, removed ? IgnoreListPackets.Success : IgnoreListPackets.NotIgnored));
        }

        /// <summary>At world entry, after the friend list: the stored ignores, so the client filters them again.</summary>
        public static void SendList(WorldClient client)
        {
            UInt32 charId = client.playerData.getCharID();
            try
            {
                List<string> handles = Db.List(charId);
                IgnoreFilter.Load(charId, handles);
                foreach (string handle in handles)
                {
                    client.messageQueue.addRpcMessage(IgnoreListPackets.BuildAdded(Prefix + handle, IgnoreListPackets.Listed));
                }
            }
            catch (Exception e)
            {
                IgnoreFilter.Load(charId, null);
                Output.WriteDebugLog("[IGNORE] Could not load the ignore list: " + e.Message);
            }
        }
    }
}
