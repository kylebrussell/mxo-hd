using System;
using System.Text;
using hds.accounts;

namespace hds.auth
{
    /// <summary>What margin learns from a ticket it accepted.</summary>
    public sealed class VerifiedTicket
    {
        public uint UserId { get; set; }
        public string Username { get; set; }
        public uint ExpiresUnix { get; set; }
        public byte[] PublicModulus { get; set; }
    }

    /// <summary>
    /// The margin ticket inside AS_AuthReply: a 182-byte signed structure and the auth server key's signature over it.
    /// The client hands both back unchanged in margin's CertConnectRequest, so margin can check that the auth server
    /// issued it (and so the user id and key in it) instead of trusting whatever a client sends.
    ///
    /// Layout (little-endian integers): 01, userId u32, username[33] NUL-padded, 00 01, 4 zero bytes, expiry u32 (unix
    /// seconds), 32 zero bytes, 00 11, account modulus[96] big-endian, created u32.
    /// </summary>
    public static class AuthTicketCodec
    {
        public const int SignedDataSize = 182;
        public const int SignatureSize = 128;
        public const int UserIdOffset = 1;
        public const int UsernameOffset = 5;
        public const int UsernameField = 33;
        public const int ExpiryOffset = 44;
        public const int ModulusOffset = 82;
        public const int CreatedOffset = 178;

        /// <summary>How long a ticket stays valid after it's issued (the client's own expiry field).</summary>
        public const int LifetimeSeconds = 600;

        /// <summary>
        /// How long past that expiry margin still takes it. The client and mxo-web's proxy keep the ticket for later margin
        /// connects (back to character select and in again), so a strict ten minutes would end long sessions; the
        /// signature is what stops forgery. MXO_TICKET_GRACE_SECONDS overrides it.
        /// </summary>
        public static int GraceSeconds =>
            int.TryParse(Environment.GetEnvironmentVariable("MXO_TICKET_GRACE_SECONDS"), out int seconds) && seconds >= 0
                ? seconds
                : 24 * 60 * 60;

        public static byte[] BuildSignedData(uint userId, string username, uint expiresUnix, byte[] modulus, uint created)
        {
            if (username == null || username.Length == 0 || username.Length >= UsernameField)
                throw new ArgumentException("Username must be 1 to 32 characters.", nameof(username));
            if (modulus == null || modulus.Length != AccountKeys.ModulusBytes)
                throw new ArgumentException("Account modulus must be 96 bytes.", nameof(modulus));
            byte[] data = new byte[SignedDataSize];
            data[0] = 0x01;
            BitConverter.GetBytes(userId).CopyTo(data, UserIdOffset);
            for (int i = 0; i < username.Length; i++) data[UsernameOffset + i] = (byte)username[i];
            data[38] = 0x00;
            data[39] = 0x01;
            BitConverter.GetBytes(expiresUnix).CopyTo(data, ExpiryOffset);
            data[80] = 0x00;
            data[81] = 0x11;
            Buffer.BlockCopy(modulus, 0, data, ModulusOffset, modulus.Length);
            BitConverter.GetBytes(created).CopyTo(data, CreatedOffset);
            return data;
        }

        public static byte[] Sign(byte[] signedData) => new MxoRSA().signWithMD5(new Md5().digest(signedData));

        public static bool TryVerify(byte[] signature, byte[] signedData, uint nowUnix, out VerifiedTicket ticket)
        {
            return TryVerify(signature, signedData, nowUnix, GraceSeconds, out ticket);
        }

        public static bool TryVerify(byte[] signature, byte[] signedData, uint nowUnix, int graceSeconds, out VerifiedTicket ticket)
        {
            ticket = null;
            if (signature == null || signedData == null || signature.Length != SignatureSize ||
                signedData.Length != SignedDataSize || signedData[0] != 0x01 || signedData[80] != 0x00 || signedData[81] != 0x11)
                return false;
            if (!MxoRSA.VerifyWithServerKey(new Md5().digest(signedData), signature)) return false;

            uint expires = BitConverter.ToUInt32(signedData, ExpiryOffset);
            if ((ulong)expires + (ulong)Math.Max(0, graceSeconds) < nowUnix) return false;

            int nameLength = Array.IndexOf(signedData, (byte)0, UsernameOffset, UsernameField) - UsernameOffset;
            if (nameLength <= 0) return false;
            byte[] modulus = new byte[AccountKeys.ModulusBytes];
            Buffer.BlockCopy(signedData, ModulusOffset, modulus, 0, modulus.Length);
            ticket = new VerifiedTicket
            {
                UserId = BitConverter.ToUInt32(signedData, UserIdOffset),
                Username = Encoding.ASCII.GetString(signedData, UsernameOffset, nameLength),
                ExpiresUnix = expires,
                PublicModulus = modulus,
            };
            return ticket.UserId != 0;
        }
    }
}
