using System;
using System.Net;

namespace hds.accounts
{
    public enum LoginOutcome
    {
        Ok,
        BadCredentials,
        Locked,
        Banned,
        Inactive,
        Unavailable,
    }

    public sealed class LoginResult
    {
        public LoginOutcome Outcome { get; set; }
        public uint UserId { get; set; }
        /// <summary>Set when the stored password was rewritten this login (MD5 to digest, or older parameters).</summary>
        public bool Migrated { get; set; }
        public TimeSpan RetryAfter { get; set; }
    }

    /// <summary>
    /// The auth server's password decision, apart from the wire format so it can be tested: lockout, lookup,
    /// verification (digest, or the legacy MD5 with transparent migration), then account status.
    /// </summary>
    public sealed class AccountLogin
    {
        /// <summary>account_status values. 0 is the only one that may log in.</summary>
        public const int StatusActive = 0;
        public const int StatusBanned = 1;
        public const int StatusDisabled = 2;

        private readonly ILoginStore store;
        private readonly LoginThrottle throttle;

        public AccountLogin(ILoginStore store, LoginThrottle throttle)
        {
            this.store = store;
            this.throttle = throttle;
        }

        /// <param name="password">The password bytes exactly as the launcher sent them.</param>
        /// <param name="acceptAnyPassword">MXO_DEV_ANY_PASSWORD: skip the check (and never migrate, since the real password is unknown).</param>
        public LoginResult Authenticate(string username, byte[] password, IPAddress address, bool acceptAnyPassword)
        {
            if (!acceptAnyPassword && throttle.IsBlocked(username, address, out TimeSpan wait))
                return new LoginResult { Outcome = LoginOutcome.Locked, RetryAfter = wait };

            LoginAccount account;
            try
            {
                account = store.FindForLogin(username);
            }
            catch (Exception e)
            {
                Output.WriteLine("Auth: account lookup failed (" + e.GetType().Name + ")");
                return new LoginResult { Outcome = LoginOutcome.Unavailable };
            }

            bool ok;
            bool migrated = false;
            if (account == null)
            {
                if (!acceptAnyPassword) PasswordHasher.DummyVerify(password);
                ok = false;
            }
            else if (acceptAnyPassword)
            {
                ok = true;
            }
            else if (!string.IsNullOrEmpty(account.Digest))
            {
                ok = PasswordHasher.Verify(password, account.Digest, out bool rehash);
                if (ok && rehash) migrated = TryUpgrade(account.UserId, password);
            }
            else if (!string.IsNullOrEmpty(account.LegacyMd5))
            {
                ok = PasswordHasher.VerifyLegacyMd5(password, account.LegacyMd5);
                if (ok) migrated = TryUpgrade(account.UserId, password);
            }
            else
            {
                PasswordHasher.DummyVerify(password);
                ok = false;
            }

            if (!ok)
            {
                if (!acceptAnyPassword) throttle.RecordFailure(username, address);
                return new LoginResult { Outcome = LoginOutcome.BadCredentials };
            }
            throttle.RecordSuccess(username);

            // Status is checked only after the password, so a stranger can't learn that an account is banned.
            LoginOutcome outcome = account.Status == StatusActive ? LoginOutcome.Ok
                : account.Status == StatusBanned ? LoginOutcome.Banned
                : LoginOutcome.Inactive;
            return new LoginResult { Outcome = outcome, UserId = account.UserId, Migrated = migrated };
        }

        private bool TryUpgrade(uint userId, byte[] password)
        {
            try
            {
                store.UpgradeDigest(userId, PasswordHasher.Hash(password));
                Output.WriteLine("Auth: account " + userId + " password storage upgraded");
                return true;
            }
            catch (Exception e)
            {
                // The login itself succeeded; the next one tries the upgrade again.
                Output.WriteLine("Auth: password upgrade for account " + userId + " failed (" + e.GetType().Name + ")");
                return false;
            }
        }
    }
}
