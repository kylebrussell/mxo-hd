using System;
using System.Text;
using System.Text.RegularExpressions;

namespace hds.accounts
{
    /// <summary>
    /// What a new account may look like. The launcher sends the username and password as single-byte text (ASCII),
    /// the ticket keeps 32 bytes of username, and users.username is varchar(32) latin1. A conservative subset of that
    /// is used here so whatever the sign-up page accepts can be typed into the stock launcher and arrives byte-identical:
    /// usernames are 3-20 letters, digits or underscores starting with a letter; passwords are 10-64 printable ASCII
    /// characters (space to tilde).
    /// </summary>
    public static class AccountRules
    {
        public const int UsernameMin = 3;
        public const int UsernameMax = 20;
        public const int PasswordMin = 10;
        public const int PasswordMax = 64;
        public const int EmailMax = 254;

        private static readonly Regex UsernamePattern = new Regex("^[A-Za-z][A-Za-z0-9_]{" + (UsernameMin - 1) + "," + (UsernameMax - 1) + "}$",
            RegexOptions.CultureInvariant);
        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]{1,64}@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$",
            RegexOptions.CultureInvariant);

        public static bool IsValidUsername(string username) => username != null && UsernamePattern.IsMatch(username);

        public static bool IsValidPassword(string password)
        {
            if (password == null || password.Length < PasswordMin || password.Length > PasswordMax) return false;
            foreach (char c in password)
                if (c < 0x20 || c > 0x7e) return false;
            return true;
        }

        /// <summary>Empty is fine (email is optional); otherwise a plain address of at most 254 characters.</summary>
        public static bool IsValidEmail(string email) =>
            string.IsNullOrEmpty(email) || (email.Length <= EmailMax && EmailPattern.IsMatch(email));

        /// <summary>The bytes the launcher will send for this password, which is what gets hashed.</summary>
        public static byte[] PasswordBytes(string password) => Encoding.ASCII.GetBytes(password);
    }
}
