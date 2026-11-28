using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
using hds.shared;

namespace hds.accounts
{
    /// <summary>The users row fields a login needs.</summary>
    public sealed class LoginAccount
    {
        public uint UserId { get; set; }
        public string Username { get; set; }
        public int Status { get; set; }
        /// <summary>users.password_digest (<see cref="PasswordHasher"/> format) or null before migration.</summary>
        public string Digest { get; set; }
        /// <summary>users.passwordmd5, the pre-migration value; cleared once the digest is written.</summary>
        public string LegacyMd5 { get; set; }
    }

    public sealed class NewAccount
    {
        public string Username { get; set; }
        public string Digest { get; set; }
        public AccountKey Key { get; set; }
        public string Email { get; set; }
    }

    public interface ILoginStore
    {
        /// <summary>Case-insensitive lookup; null when there's no such account.</summary>
        LoginAccount FindForLogin(string username);
        /// <summary>Stores a new digest and clears every legacy password column.</summary>
        void UpgradeDigest(uint userId, string digest);
    }

    public sealed class UsernameTakenException : Exception
    {
        public UsernameTakenException() : base("Username taken") { }
    }

    public interface ISignupStore
    {
        bool UsernameTaken(string username);
        /// <summary>Inserts the account; throws <see cref="UsernameTakenException"/> on a duplicate.</summary>
        uint Create(NewAccount account);
    }

    /// <summary>Account rows in users, through MySqlConnector with parameters only.</summary>
    public sealed class AccountStore : ILoginStore, ISignupStore
    {
        /// <summary>Column added by <see cref="EnsureSchema"/> (and present in SQL/reality_hd.sql).</summary>
        public const string DigestColumn = "password_digest";

        private static readonly object SchemaGate = new object();
        private static readonly object CreateGate = new object();
        private static volatile CachedConfigConnection cachedConfig;
        private readonly string connectionString;

        private sealed class CachedConfigConnection
        {
            public readonly string Host;
            public readonly int Port;
            public readonly string DatabaseName;
            public readonly string Username;
            public readonly string Password;
            public readonly string ConnectionString;

            public CachedConfigConnection(DbParams db, string connectionString)
            {
                Host = db.Host;
                Port = db.Port;
                DatabaseName = db.DatabaseName;
                Username = db.Username;
                Password = db.Password;
                ConnectionString = connectionString;
            }

            public bool Matches(DbParams db)
            {
                return Port == db.Port &&
                       string.Equals(Host, db.Host, StringComparison.Ordinal) &&
                       string.Equals(DatabaseName, db.DatabaseName, StringComparison.Ordinal) &&
                       string.Equals(Username, db.Username, StringComparison.Ordinal) &&
                       string.Equals(Password, db.Password, StringComparison.Ordinal);
            }
        }

        public AccountStore(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public static AccountStore FromConfig()
        {
            DbParams db = Store.config.dbParams;
            CachedConfigConnection cached = cachedConfig;
            if (cached != null && cached.Matches(db))
            {
                return new AccountStore(cached.ConnectionString);
            }

            var builder = new MySqlConnectionStringBuilder
            {
                Server = db.Host,
                Port = (uint)db.Port,
                Database = db.DatabaseName,
                UserID = db.Username,
                Password = db.Password,
                Pooling = true,
                ConvertZeroDateTime = true,
            };
            string built = builder.ConnectionString;
            cachedConfig = new CachedConfigConnection(db, built);
            return new AccountStore(built);
        }

        private MySqlConnection Open()
        {
            var connection = new MySqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Startup migration, in the repo's Ensure* style: adds users.password_digest if it's missing, and the unique
        /// username key if an old dump lacks it (skipped with a warning when duplicate names exist). Idempotent.
        /// </summary>
        public void EnsureSchema()
        {
            lock (SchemaGate)
            {
                using MySqlConnection connection = Open();
                using MySqlCommand command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND COLUMN_NAME='" + DigestColumn + "'";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    command.CommandText = "ALTER TABLE users ADD COLUMN " + DigestColumn + " VARCHAR(255) NULL DEFAULT NULL AFTER passwordmd5";
                    command.ExecuteNonQuery();
                    Output.WriteLine("Accounts: added users." + DigestColumn);
                }
                command.CommandText = "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND COLUMN_NAME='username' AND NON_UNIQUE=0";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    try
                    {
                        command.CommandText = "ALTER TABLE users ADD UNIQUE KEY username (username)";
                        command.ExecuteNonQuery();
                    }
                    catch (MySqlException)
                    {
                        Output.WriteLine("Accounts: WARNING users.username has duplicates (case-insensitive), so it has no unique key. Sign-up still checks names first.");
                    }
                }
            }
        }

        public LoginAccount FindForLogin(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            using MySqlConnection connection = Open();
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT userId, username, account_status, " + DigestColumn + ", passwordmd5 FROM users " +
                                  "WHERE LOWER(username)=LOWER(@name) ORDER BY userId LIMIT 2";
            command.Parameters.AddWithValue("@name", username);
            using MySqlDataReader reader = command.ExecuteReader();
            var found = new List<LoginAccount>();
            while (reader.Read())
            {
                found.Add(new LoginAccount
                {
                    UserId = Convert.ToUInt32(reader.GetValue(0)),
                    Username = reader.GetString(1),
                    Status = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    Digest = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LegacyMd5 = reader.IsDBNull(4) ? null : reader.GetString(4),
                });
            }
            // Two rows differing only in case are ambiguous: refuse rather than guess which one the player meant.
            return found.Count == 1 ? found[0] : null;
        }

        public void UpgradeDigest(uint userId, string digest)
        {
            using MySqlConnection connection = Open();
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE users SET " + DigestColumn + "=@digest, passwordmd5=NULL, passwordHash=NULL, passwordSalt=NULL WHERE userId=@id";
            command.Parameters.AddWithValue("@digest", digest);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        public bool UsernameTaken(string username)
        {
            using MySqlConnection connection = Open();
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM users WHERE LOWER(username)=LOWER(@name)";
            command.Parameters.AddWithValue("@name", username);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        public uint Create(NewAccount account)
        {
            // users is MyISAM in the stock dump (no transactions), so creation is serialized in-process and the unique
            // username key catches anything else.
            lock (CreateGate)
            {
                if (UsernameTaken(account.Username)) throw new UsernameTakenException();
                using MySqlConnection connection = Open();
                using MySqlCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO users (username, publicExponent, publicModulus, privateExponent, account_status, " +
                                      "sessionid, passwordmd5, " + DigestColumn + ", email_adress) " +
                                      "VALUES (@name, @e, @n, @d, 0, '', NULL, @digest, @email)";
                command.Parameters.AddWithValue("@name", account.Username);
                command.Parameters.AddWithValue("@e", account.Key.PublicExponent);
                command.Parameters.AddWithValue("@n", account.Key.Modulus);
                command.Parameters.AddWithValue("@d", account.Key.PrivateExponent);
                command.Parameters.AddWithValue("@digest", account.Digest);
                command.Parameters.AddWithValue("@email", string.IsNullOrEmpty(account.Email) ? (object)DBNull.Value : account.Email);
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
                {
                    throw new UsernameTakenException();
                }
                return (uint)command.LastInsertedId;
            }
        }

        /// <summary>account_status for a user id, or null if the row is gone.</summary>
        public int? GetStatus(uint userId)
        {
            using MySqlConnection connection = Open();
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT account_status FROM users WHERE userId=@id";
            command.Parameters.AddWithValue("@id", userId);
            object value = command.ExecuteScalar();
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        /// <summary>True when the character exists, isn't deleted, and belongs to the account.</summary>
        public bool CharacterBelongsTo(uint charId, uint userId)
        {
            using MySqlConnection connection = Open();
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM characters WHERE charId=@char AND userId=@user AND is_deleted=0";
            command.Parameters.AddWithValue("@char", charId);
            command.Parameters.AddWithValue("@user", userId);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        public sealed class RekeyResult
        {
            public int Accounts;
            public int Rekeyed;
            public int Shared;
            public int Invalid;
        }

        /// <summary>
        /// Gives every account whose key is shared with another account, or isn't a valid 768-bit e=17 key, a fresh one.
        /// Accounts that already have a unique valid key are left alone, so it's safe to run again. Prints nothing
        /// about the keys themselves.
        /// </summary>
        public RekeyResult RekeySharedAccounts(bool all = false)
        {
            var rows = new List<(uint Id, byte[] N, byte[] D, ushort E)>();
            using (MySqlConnection connection = Open())
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT userId, publicModulus, privateExponent, publicExponent FROM users ORDER BY userId";
                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add((Convert.ToUInt32(reader.GetValue(0)),
                        reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1),
                        reader.IsDBNull(2) ? null : (byte[])reader.GetValue(2),
                        reader.IsDBNull(3) ? (ushort)0 : Convert.ToUInt16(reader.GetValue(3))));
                }
            }
            var result = new RekeyResult { Accounts = rows.Count };
            var counts = rows.Where(r => r.N != null).GroupBy(r => Convert.ToHexString(r.N))
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var row in rows)
            {
                bool shared = row.N != null && counts[Convert.ToHexString(row.N)] > 1;
                bool invalid = !AccountKeys.IsValid(row.N, row.D, row.E);
                if (shared) result.Shared++;
                if (invalid) result.Invalid++;
                if (!all && !shared && !invalid) continue;
                AccountKey key = AccountKeys.Generate();
                using MySqlConnection connection = Open();
                using MySqlCommand update = connection.CreateCommand();
                update.CommandText = "UPDATE users SET publicExponent=@e, publicModulus=@n, privateExponent=@d WHERE userId=@id";
                update.Parameters.AddWithValue("@e", key.PublicExponent);
                update.Parameters.AddWithValue("@n", key.Modulus);
                update.Parameters.AddWithValue("@d", key.PrivateExponent);
                update.Parameters.AddWithValue("@id", row.Id);
                update.ExecuteNonQuery();
                result.Rekeyed++;
            }
            return result;
        }
    }
}
