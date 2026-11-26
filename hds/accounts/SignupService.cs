using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace hds.accounts
{
    public sealed class SignupRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        /// <summary>Cloudflare Turnstile token from the sign-up page, when the bot check is on.</summary>
        public string TurnstileToken { get; set; }
    }

    public enum SignupOutcome
    {
        Created,
        InvalidUsername,
        InvalidPassword,
        InvalidEmail,
        UsernameTaken,
        BotCheckFailed,
        Unavailable,
    }

    public sealed class SignupResult
    {
        public SignupOutcome Outcome { get; set; }
        public uint UserId { get; set; }
        public string Username { get; set; }
    }

    /// <summary>A pluggable "is this a person" check, run before anything else touches the request.</summary>
    public interface IBotCheck
    {
        Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken cancel);
    }

    /// <summary>Local development default: no check.</summary>
    public sealed class NoBotCheck : IBotCheck
    {
        public Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken cancel) => Task.FromResult(true);
    }

    /// <summary>
    /// Cloudflare Turnstile server-side verification (https://developers.cloudflare.com/turnstile/get-started/server-side-validation/).
    /// Fails closed: no token, a timeout or an error all mean "no".
    /// </summary>
    public sealed class TurnstileBotCheck : IBotCheck
    {
        private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private readonly string secret;

        public TurnstileBotCheck(string secret)
        {
            this.secret = secret;
        }

        public async Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(token) || token.Length > 2048) return false;
            var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
            if (!string.IsNullOrEmpty(remoteIp)) form["remoteip"] = remoteIp;
            try
            {
                using HttpResponseMessage response = await Http.PostAsync(VerifyUrl, new FormUrlEncodedContent(form), cancel);
                if (!response.IsSuccessStatusCode) return false;
                using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return body.RootElement.TryGetProperty("success", out JsonElement success) && success.ValueKind == JsonValueKind.True;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Sign-up rules, apart from HTTP: bot check, validation, uniqueness, hashing, a fresh key, insert. The plaintext
    /// password exists only in the request object and the one hashing call.
    /// </summary>
    public sealed class SignupService
    {
        private readonly ISignupStore store;
        private readonly IBotCheck botCheck;
        private readonly Func<byte[], string> hash;
        private readonly Func<AccountKey> generateKey;

        public SignupService(ISignupStore store, IBotCheck botCheck, Func<byte[], string> hash = null, Func<AccountKey> generateKey = null)
        {
            this.store = store;
            this.botCheck = botCheck ?? new NoBotCheck();
            this.hash = hash ?? PasswordHasher.Hash;
            this.generateKey = generateKey ?? AccountKeys.Generate;
        }

        public async Task<SignupResult> SignupAsync(SignupRequest request, string remoteIp, CancellationToken cancel)
        {
            if (request == null) return new SignupResult { Outcome = SignupOutcome.InvalidUsername };
            if (!await botCheck.VerifyAsync(request.TurnstileToken, remoteIp, cancel))
                return new SignupResult { Outcome = SignupOutcome.BotCheckFailed };

            string username = request.Username?.Trim();
            string email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            if (!AccountRules.IsValidUsername(username)) return new SignupResult { Outcome = SignupOutcome.InvalidUsername };
            if (!AccountRules.IsValidPassword(request.Password)) return new SignupResult { Outcome = SignupOutcome.InvalidPassword };
            if (!AccountRules.IsValidEmail(email)) return new SignupResult { Outcome = SignupOutcome.InvalidEmail };
            if (string.Equals(username, request.Password, StringComparison.OrdinalIgnoreCase))
                return new SignupResult { Outcome = SignupOutcome.InvalidPassword };

            try
            {
                if (store.UsernameTaken(username)) return new SignupResult { Outcome = SignupOutcome.UsernameTaken };
                byte[] password = AccountRules.PasswordBytes(request.Password);
                string digest;
                AccountKey key;
                try
                {
                    digest = await Task.Run(() => hash(password), cancel);
                    key = await Task.Run(generateKey, cancel);
                }
                finally
                {
                    Array.Clear(password, 0, password.Length);
                }
                uint id = store.Create(new NewAccount { Username = username, Digest = digest, Key = key, Email = email });
                return new SignupResult { Outcome = SignupOutcome.Created, UserId = id, Username = username };
            }
            catch (UsernameTakenException)
            {
                return new SignupResult { Outcome = SignupOutcome.UsernameTaken };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Output.WriteLine("Signup: failed (" + e.GetType().Name + ")");
                return new SignupResult { Outcome = SignupOutcome.Unavailable };
            }
        }
    }
}
