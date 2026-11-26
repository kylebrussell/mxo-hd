using System;
using Microsoft.AspNetCore.Builder;
using hds.shared;

namespace hds.accounts
{
    /// <summary>hds entry points for accounts: the sign-up API at startup and the one-off rekey command.</summary>
    public static class AccountCommands
    {
        private static WebApplication signup;

        public static void StartSignupIfEnabled()
        {
            SignupSettings settings = SignupSettings.FromEnvironment();
            if (!settings.Enabled) return;
            var service = new SignupService(AccountStore.FromConfig(), SignupHost.BotCheckFor(settings));
            signup = SignupHost.StartAsync(settings, service).GetAwaiter().GetResult();
            Console.CancelKeyPress += (sender, e) =>
            {
                try
                {
                    signup?.StopAsync().GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    // shutting down anyway
                }
            };
        }

        /// <summary>
        /// <c>dotnet hds.dll --rekey-accounts [--all]</c>: gives every account whose RSA key is shared with another
        /// account (every row in the stock SQL dump shares one) or invalid its own fresh key. --all rekeys every account.
        /// Safe to run on a live server: a client already past auth keeps working with the key in its ticket, and the
        /// next login hands out the new one. Prints counts only, never keys.
        /// </summary>
        public static int RekeyAccounts(bool all)
        {
            try
            {
                Store.config = new ServerConfig("Config.xml");
                Store.config.LoadDbParams();
                var store = AccountStore.FromConfig();
                store.EnsureSchema();
                AccountStore.RekeyResult result = store.RekeySharedAccounts(all);
                Console.WriteLine($"Rekey: {result.Accounts} accounts, {result.Shared} had a shared key, {result.Invalid} an invalid one; {result.Rekeyed} given a fresh key.");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Rekey failed: " + e.GetType().Name + ": " + e.Message);
                return 1;
            }
        }
    }
}
