using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;

namespace hds.accounts
{
    /// <summary>
    /// Password storage for users.password_digest. The stock launcher sends the plaintext password (inside the
    /// Twofish-encrypted challenge answer), so this hashes the password bytes exactly as the launcher sends them.
    ///
    /// Format: <c>$pbkdf2-sha256$v=1$i=600000$&lt;salt b64&gt;$&lt;hash b64&gt;</c>. PBKDF2-HMAC-SHA256 is built into
    /// .NET (no new package, nothing native) and 600,000 iterations is OWASP's current figure for it. The scheme, version
    /// and iteration count are stored with every hash, so a later Argon2id "v=2" can be added next to this one and
    /// rows move over on their next login (<see cref="Verify"/> reports <c>needsRehash</c>).
    ///
    /// Never log a password or a digest.
    /// </summary>
    public static class PasswordHasher
    {
        public const string Scheme = "pbkdf2-sha256";
        public const int Version = 1;
        public const int DefaultIterations = 600_000;
        public const int SaltBytes = 16;
        public const int HashBytes = 32;

        /// <summary>Iterations below this are refused outright (a damaged or hand-edited row).</summary>
        public const int MinimumIterations = 1_000;
        private const int MaximumIterations = 10_000_000;

        // One PBKDF2 run at 600k is ~0.2 s of CPU. Bound how many run at once so a flood of logins or sign-ups can't
        // tie up every core; the rate limits and lockouts keep the queue short.
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));

        // A digest of a random password, so an unknown user costs as much as a wrong password.
        private static readonly Lazy<string> DummyDigest = new Lazy<string>(() =>
            Hash(RandomNumberGenerator.GetBytes(16)));

        public static string Hash(byte[] password) => Hash(password, DefaultIterations);

        public static string Hash(byte[] password, int iterations)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (iterations < MinimumIterations || iterations > MaximumIterations)
                throw new ArgumentOutOfRangeException(nameof(iterations));
            byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
            byte[] hash = Derive(password, salt, iterations, HashBytes);
            return "$" + Scheme + "$v=" + Version + "$i=" + iterations.ToString(CultureInfo.InvariantCulture) + "$" +
                   Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Checks a password against a stored digest. <paramref name="needsRehash"/> is true when the password matched
        /// but the digest uses older parameters than <see cref="DefaultIterations"/>.
        /// </summary>
        public static bool Verify(byte[] password, string stored, out bool needsRehash)
        {
            needsRehash = false;
            if (password == null || !TryParse(stored, out int iterations, out byte[] salt, out byte[] expected))
                return false;
            byte[] actual = Derive(password, salt, iterations, expected.Length);
            bool match = CryptographicOperations.FixedTimeEquals(actual, expected);
            needsRehash = match && iterations < DefaultIterations;
            return match;
        }

        /// <summary>The pre-migration check: users.passwordmd5 holds MD5(password) as hex.</summary>
        public static bool VerifyLegacyMd5(byte[] password, string storedHex)
        {
            if (password == null || string.IsNullOrWhiteSpace(storedHex)) return false;
            byte[] expected;
            try
            {
                expected = Convert.FromHexString(storedHex.Trim());
            }
            catch (FormatException)
            {
                return false;
            }
            if (expected.Length != 16) return false;
            return CryptographicOperations.FixedTimeEquals(MD5.HashData(password), expected);
        }

        /// <summary>Spends the same time as a real check, for accounts that don't exist or have no usable digest.</summary>
        public static void DummyVerify(byte[] password)
        {
            Verify(password ?? Array.Empty<byte>(), DummyDigest.Value, out _);
        }

        /// <summary>True when the stored value is a digest this class wrote (any version it can read).</summary>
        public static bool IsDigest(string stored) => TryParse(stored, out _, out _, out _);

        private static byte[] Derive(byte[] password, byte[] salt, int iterations, int length)
        {
            Gate.Wait();
            try
            {
                return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
            }
            finally
            {
                Gate.Release();
            }
        }

        private static bool TryParse(string stored, out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0;
            salt = null;
            hash = null;
            if (string.IsNullOrEmpty(stored)) return false;
            // "", scheme, v=1, i=N, salt, hash
            string[] parts = stored.Split('$');
            if (parts.Length != 6 || parts[0].Length != 0 || parts[1] != Scheme || parts[2] != "v=" + Version ||
                !parts[3].StartsWith("i=", StringComparison.Ordinal) ||
                !int.TryParse(parts[3].Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out iterations) ||
                iterations < MinimumIterations || iterations > MaximumIterations)
                return false;
            try
            {
                salt = Convert.FromBase64String(parts[4]);
                hash = Convert.FromBase64String(parts[5]);
            }
            catch (FormatException)
            {
                return false;
            }
            return salt.Length >= 8 && hash.Length >= 16 && hash.Length <= 64;
        }
    }
}
