using System;
using System.Numerics;
using System.Security.Cryptography;

namespace hds.accounts
{
    /// <summary>An account's RSA key as users stores it: n and d big-endian, 96 bytes each, e = 17.</summary>
    public sealed class AccountKey
    {
        public AccountKey(byte[] modulus, byte[] privateExponent)
        {
            Modulus = modulus;
            PrivateExponent = privateExponent;
        }

        public byte[] Modulus { get; }
        public byte[] PrivateExponent { get; }
        public ushort PublicExponent => AccountKeys.PublicExponent;
    }

    /// <summary>
    /// Per-account RSA keys. The auth ticket has room for exactly a 96-byte modulus (signed data bytes 82..178, after
    /// "00 11"), margin hardcodes e = 17 when it encrypts the session key to it, and the client receives d (Twofish
    /// encrypted) to decrypt that. MxOEmu captures in research/packet-dumps show the same shape: a unique 768-bit,
    /// e = 17 key per account. .NET's RSA can't pick e, so the key is built here from two 384-bit primes.
    ///
    /// d goes to the account's owner on every login by design; it has to stay secret from everyone else, which is why
    /// every account gets its own.
    /// </summary>
    public static class AccountKeys
    {
        public const int ModulusBytes = 96;
        public const int ModulusBits = ModulusBytes * 8;
        public const ushort PublicExponent = 17;

        private static readonly BigInteger E = PublicExponent;
        private static readonly int[] SmallPrimes = BuildSmallPrimes(2000);

        public static AccountKey Generate()
        {
            while (true)
            {
                BigInteger p = RandomPrime(ModulusBits / 2);
                BigInteger q = RandomPrime(ModulusBits / 2);
                if (p == q) continue;
                BigInteger n = p * q;
                if (n.GetBitLength() != ModulusBits) continue;
                BigInteger phi = (p - 1) * (q - 1);
                BigInteger d = ModInverse(E, phi);
                if (d.IsZero) continue;
                byte[] modulus = ToFixed(n);
                byte[] privateExponent = ToFixed(d);
                if (!IsValid(modulus, privateExponent, PublicExponent)) continue;
                return new AccountKey(modulus, privateExponent);
            }
        }

        /// <summary>
        /// True when n is a full 768-bit modulus, e is 17, and d undoes e (checked with raw RSA on random values, the
        /// way margin and the client use the pair).
        /// </summary>
        public static bool IsValid(byte[] modulus, byte[] privateExponent, ushort publicExponent)
        {
            if (modulus == null || privateExponent == null || publicExponent != PublicExponent) return false;
            if (modulus.Length != ModulusBytes || privateExponent.Length != ModulusBytes) return false;
            BigInteger n = FromBigEndian(modulus);
            BigInteger d = FromBigEndian(privateExponent);
            if (n.GetBitLength() != ModulusBits || n.IsEven || d <= BigInteger.One || d >= n) return false;
            for (int i = 0; i < 3; i++)
            {
                BigInteger m = FromBigEndian(RandomNumberGenerator.GetBytes(ModulusBytes - 1));
                if (m <= BigInteger.One) continue;
                BigInteger c = BigInteger.ModPow(m, E, n);
                if (BigInteger.ModPow(c, d, n) != m) return false;
            }
            return true;
        }

        public static BigInteger FromBigEndian(byte[] value) => new BigInteger(value, isUnsigned: true, isBigEndian: true);

        /// <summary>Big-endian, left-padded with zeros to 96 bytes.</summary>
        public static byte[] ToFixed(BigInteger value)
        {
            byte[] raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (raw.Length > ModulusBytes) throw new ArgumentOutOfRangeException(nameof(value));
            byte[] fixedBytes = new byte[ModulusBytes];
            Buffer.BlockCopy(raw, 0, fixedBytes, ModulusBytes - raw.Length, raw.Length);
            return fixedBytes;
        }

        private static BigInteger RandomPrime(int bits)
        {
            int bytes = bits / 8;
            while (true)
            {
                byte[] buffer = RandomNumberGenerator.GetBytes(bytes);
                buffer[0] |= 0xC0;            // top two bits: p*q then always has exactly 2*bits bits
                buffer[bytes - 1] |= 0x01;    // odd
                BigInteger candidate = FromBigEndian(buffer);
                if ((candidate - 1) % E == 0) continue; // e must be invertible mod p-1
                if (IsProbablePrime(candidate)) return candidate;
            }
        }

        internal static bool IsProbablePrime(BigInteger n)
        {
            if (n < 2) return false;
            foreach (int small in SmallPrimes)
            {
                if (n == small) return true;
                if (n % small == 0) return false;
            }
            BigInteger d = n - 1;
            int r = 0;
            while (d.IsEven)
            {
                d >>= 1;
                r++;
            }
            int byteLength = n.GetByteCount(isUnsigned: true);
            for (int round = 0; round < 40; round++)
            {
                BigInteger a;
                do
                {
                    a = FromBigEndian(RandomNumberGenerator.GetBytes(byteLength)) % n;
                } while (a < 2 || a > n - 2);
                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) continue;
                bool composite = true;
                for (int i = 1; i < r; i++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == n - 1)
                    {
                        composite = false;
                        break;
                    }
                }
                if (composite) return false;
            }
            return true;
        }

        private static BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            BigInteger oldR = a, r = m, oldS = 1, s = 0;
            while (!r.IsZero)
            {
                BigInteger quotient = BigInteger.Divide(oldR, r);
                (oldR, r) = (r, oldR - quotient * r);
                (oldS, s) = (s, oldS - quotient * s);
            }
            if (oldR != 1) return BigInteger.Zero;
            BigInteger result = oldS % m;
            return result.Sign < 0 ? result + m : result;
        }

        private static int[] BuildSmallPrimes(int limit)
        {
            bool[] composite = new bool[limit + 1];
            var primes = new System.Collections.Generic.List<int>();
            for (int i = 2; i <= limit; i++)
            {
                if (composite[i]) continue;
                primes.Add(i);
                for (int j = i * i; j <= limit; j += i) composite[j] = true;
            }
            return primes.ToArray();
        }
    }
}
