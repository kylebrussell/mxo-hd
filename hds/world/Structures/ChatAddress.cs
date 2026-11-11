using System;
using hds.shared;
using MySqlConnector;

namespace hds
{
    /// <summary>
    /// The SOE chat address of a handle: <c>SOE+MXO+&lt;world&gt;+&lt;handle&gt;</c> (retail:
    /// <c>SOE+MXO+Vector-Hostile+Galishnikov</c>). client.dll's address check (0x6224e810) compares the three parts with
    /// its SOEPrefix ("SOE"), GamePrefix ("MXO") and WorldPrefix settings. WorldPrefix starts empty and is set at world
    /// entry (0x621707e0) to the name of the world the player logged in to, which the client only ever learns from the
    /// auth server's world list, that is from the <c>worlds</c> table. An address with any other world part is another
    /// server's: the client shows it as <c>World.Handle</c> and refuses mail attachments to it. So the world part must
    /// be that table's name, not WorldConfig's serverName (which said "Syntax" while the world list said "Recursion").
    /// </summary>
    public static class ChatAddress
    {
        private static readonly object Sync = new object();
        private static string worldName;

        /// <summary>Lets tests and tools fix the name.</summary>
        public static string WorldName
        {
            get
            {
                lock (Sync)
                {
                    return worldName ?? (worldName = LoadWorldName() ?? Store.worldConfig?.serverName ?? "");
                }
            }
            set
            {
                lock (Sync)
                {
                    worldName = value;
                }
            }
        }

        public static string Prefix => "SOE+MXO+" + WorldName + "+";

        public static string Of(string handle) => Prefix + handle;

        /// <summary>The world this server is: the first row of the auth world list (AuthHandler sends them in worldId order).</summary>
        private static string LoadWorldName()
        {
            try
            {
                var config = Store.config;
                using (var conn = new MySqlConnection("Server=" + config.dbParams.Host + ";Port=" + config.dbParams.Port +
                                                      ";Database=" + config.dbParams.DatabaseName + ";User ID=" +
                                                      config.dbParams.Username + ";Password=" + config.dbParams.Password +
                                                      ";Pooling=true;"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT name FROM worlds ORDER BY worldId LIMIT 1";
                        object value = cmd.ExecuteScalar();
                        string name = value == null || value is DBNull ? null : Convert.ToString(value).Trim();
                        return string.IsNullOrEmpty(name) ? null : name;
                    }
                }
            }
            catch (Exception e)
            {
                Output.WriteDebugLog("[CHAT] No world name from the worlds table, using serverName: " + e.Message);
                return null;
            }
        }
    }
}
