using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace hds.accounts
{
    /// <summary>Limits for <see cref="LoginThrottle"/>.</summary>
    public sealed class LoginThrottleOptions
    {
        public int AccountFailures { get; set; } = 5;
        public TimeSpan AccountWindow { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan AccountBaseLock { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan AccountMaxLock { get; set; } = TimeSpan.FromMinutes(15);

        public int IpFailures { get; set; } = 20;
        public TimeSpan IpWindow { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan IpBaseLock { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan IpMaxLock { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Loopback is exempt from the per-IP limit (not the per-account one): everyone who plays through mxo-web's
        /// proxy reaches auth from the proxy's address, and one bad typist shouldn't lock them all out.
        /// MXO_AUTH_THROTTLE_LOOPBACK=1 turns the exemption off.
        /// </summary>
        public bool ExemptLoopback { get; set; } = true;

        /// <summary>Tracked keys per table; past this, idle entries are dropped first, then the oldest.</summary>
        public int MaxEntries { get; set; } = 100_000;

        public static LoginThrottleOptions FromEnvironment()
        {
            var options = new LoginThrottleOptions();
            if (Environment.GetEnvironmentVariable("MXO_AUTH_THROTTLE_LOOPBACK") == "1") options.ExemptLoopback = false;
            return options;
        }
    }

    /// <summary>
    /// Failed-login lockout with backoff, per account and per client address, in memory (a restart clears it).
    /// After N failures inside the window the key is locked; every further failure doubles the lock, up to a cap. A
    /// success clears the account's record but not the address's, so one working account can't be used to keep
    /// guessing others from the same address. Unknown usernames are tracked like real ones.
    /// </summary>
    public sealed class LoginThrottle
    {
        public static readonly LoginThrottle Shared = new LoginThrottle(LoginThrottleOptions.FromEnvironment(), () => DateTime.UtcNow);

        private sealed class Record
        {
            public readonly Queue<DateTime> Failures = new Queue<DateTime>();
            public int Strikes;
            public DateTime LockedUntil;
            public DateTime LastSeen;
        }

        private readonly LoginThrottleOptions options;
        private readonly Func<DateTime> clock;
        private readonly Dictionary<string, Record> accounts = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly Dictionary<string, Record> addresses = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly object gate = new object();

        public LoginThrottle(LoginThrottleOptions options, Func<DateTime> clock)
        {
            this.options = options ?? new LoginThrottleOptions();
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        public static string AccountKey(string username)
        {
            string key = (username ?? "").Trim().ToLowerInvariant();
            return key.Length > 64 ? key.Substring(0, 64) : key;
        }

        private string AddressKey(IPAddress address)
        {
            if (address == null) return null;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (options.ExemptLoopback && IPAddress.IsLoopback(address)) return null;
            return address.ToString();
        }

        /// <summary>True while the account or the address is locked; <paramref name="retryAfter"/> is the longer wait.</summary>
        public bool IsBlocked(string username, IPAddress address, out TimeSpan retryAfter)
        {
            DateTime now = clock();
            retryAfter = TimeSpan.Zero;
            lock (gate)
            {
                TimeSpan account = Remaining(accounts, AccountKey(username), now);
                string addressKey = AddressKey(address);
                TimeSpan ip = addressKey == null ? TimeSpan.Zero : Remaining(addresses, addressKey, now);
                retryAfter = account > ip ? account : ip;
                return retryAfter > TimeSpan.Zero;
            }
        }

        public void RecordFailure(string username, IPAddress address)
        {
            DateTime now = clock();
            lock (gate)
            {
                Fail(accounts, AccountKey(username), now, options.AccountFailures, options.AccountWindow,
                    options.AccountBaseLock, options.AccountMaxLock);
                string addressKey = AddressKey(address);
                if (addressKey != null)
                    Fail(addresses, addressKey, now, options.IpFailures, options.IpWindow, options.IpBaseLock, options.IpMaxLock);
            }
        }

        public void RecordSuccess(string username)
        {
            lock (gate)
            {
                accounts.Remove(AccountKey(username));
            }
        }

        private static TimeSpan Remaining(Dictionary<string, Record> table, string key, DateTime now)
        {
            return table.TryGetValue(key, out Record record) && record.LockedUntil > now ? record.LockedUntil - now : TimeSpan.Zero;
        }

        private void Fail(Dictionary<string, Record> table, string key, DateTime now, int threshold, TimeSpan window,
            TimeSpan baseLock, TimeSpan maxLock)
        {
            if (!table.TryGetValue(key, out Record record))
            {
                if (table.Count >= options.MaxEntries) Prune(table, now, window, options.MaxEntries);
                record = new Record();
                table[key] = record;
            }
            record.LastSeen = now;
            while (record.Failures.Count > 0 && now - record.Failures.Peek() > window) record.Failures.Dequeue();
            // Strikes decay once a full window passes without failures, so an old lockout doesn't double a new one.
            if (record.Failures.Count == 0 && record.LockedUntil + window < now) record.Strikes = 0;
            record.Failures.Enqueue(now);
            if (record.Failures.Count < threshold) return;

            double factor = Math.Pow(2, Math.Min(record.Strikes, 30));
            TimeSpan length = TimeSpan.FromTicks((long)Math.Min(maxLock.Ticks, baseLock.Ticks * factor));
            record.LockedUntil = now + length;
            record.Strikes++;
        }

        private static void Prune(Dictionary<string, Record> table, DateTime now, TimeSpan window, int maxEntries)
        {
            foreach (string stale in table.Where(p => p.Value.LockedUntil <= now && now - p.Value.LastSeen > window)
                         .Select(p => p.Key).ToList())
                table.Remove(stale);
            if (table.Count < maxEntries) return;
            int excess = table.Count - maxEntries * 9 / 10;
            foreach (string oldest in table.OrderBy(p => p.Value.LastSeen).Take(excess).Select(p => p.Key).ToList())
                table.Remove(oldest);
        }
    }
}
