using System;
using hds.accounts;
using HDS.Network;

namespace hds.auth
{
    /// <summary>
    /// A refused login's AS_AuthReply, in the shape the stock launcher (7.6005 launcher.exe) reads.
    ///
    /// The launcher parses every AS_AuthReply the same way before it looks at the status (0x443470, called from the
    /// opcode-0x0b case of 0x448140): it reads nine u16 array offsets at body +5, +0x0b, +0x0d, +0x0f, +0x11, +0x13,
    /// +0x19, +0x1b and +0x1d, follows each non-zero one to a [u16 length][bytes] array, and NUL-terminates two of them in
    /// place. The old 11-byte error reply stopped at +0x0b, so those offsets came from whatever followed it in memory and
    /// the launcher (most likely) faulted reading or writing through them: native QA saw its window close ~3 s after
    /// submit with no dialog and no crash event.
    /// With the full fixed part (31 bytes, sent as 33 like the success reply) and every offset zero, all arrays are empty,
    /// 0x448140 sees Status != 0, returns 3, and the mediator posts the status as a login error (from static analysis of the
    /// disassembly, pending native QA). The UI then maps it to its own string table (0x409305): see docs/ACCOUNTS.md for the table.
    /// </summary>
    public static class AuthErrorReply
    {
        /// <summary>Opcode through the last array offset (+0x1d u16) plus the two bytes hds's success header also carries.</summary>
        public const int BodySize = 0x21;

        /// <summary>The body (from the opcode), unframed.</summary>
        public static byte[] Body(uint status)
        {
            if (status == 0) throw new ArgumentException("An error reply needs a non-zero status.", nameof(status));
            byte[] body = new byte[BodySize];
            body[0] = (byte)AuthOpcode.AS_AuthReply;
            BitConverter.GetBytes(status).CopyTo(body, 1);
            return body;
        }

        /// <summary>The framed message for the auth TCP stream.</summary>
        public static byte[] Framed(uint status) => AuthHandler.FrameAuthMessage(Body(status));

        /// <summary>
        /// The status to send for each refusal, chosen by the dialog the stock launcher shows for it (string IDs from
        /// launcher.exe's string table):
        ///   bad name or password -> LTAS_USERNAMEPASSWORDMISMATCH -> 114 "Your username or password is incorrect. Please
        ///     try again..." (225, the *CAPS LOCK* variant, when Caps Lock is on);
        ///   banned and disabled -> LTAS_ACCOUNTISBANNED -> 274 "Your account has been suspended by a Game Master...".
        ///     LTAS_ACCOUNTINACTIVE would show 128 "Your account has been disabled..." but the launcher then also
        ///     ShellExecutes InactiveAccountURL (default http://expired.lith.thematrixonline.net/, a domain we don't
        ///     control) in the player's browser, so it isn't used;
        ///   locked out -> LTAS_DATABASECONNDOWN -> 116 "The auth servers are unavailable. Please try again later." The
        ///     launcher has no lockout text for players: LTAS_RADIUSMAXATTEMPTS maps to 216 "SecurID authentication
        ///     failed due to networking error. Contact Technical Operations.", which is worse;
        ///   server trouble -> LTAS_DATABASECONNDOWN -> 116 as well.
        /// </summary>
        public static uint StatusFor(LoginOutcome outcome)
        {
            switch (outcome)
            {
                case LoginOutcome.Banned:
                case LoginOutcome.Inactive:
                    return (uint)AuthErrorCode.LTAS_ACCOUNTISBANNED;
                case LoginOutcome.Locked:
                case LoginOutcome.Unavailable:
                    return (uint)AuthErrorCode.LTAS_DATABASECONNDOWN;
                case LoginOutcome.Ok:
                    throw new ArgumentException("Not a refusal.", nameof(outcome));
                default:
                    return (uint)AuthErrorCode.LTAS_USERNAMEPASSWORDMISMATCH;
            }
        }
    }
}
