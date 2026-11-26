using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using hds.accounts;
using hds.auth;

namespace hds.Tests;

// Server-side accounts: password storage and MD5 migration, per-account RSA keys, the margin ticket check, login
// lockout and sign-up validation. docs/ACCOUNTS.md describes the design.
public class AccountSecurityTests
{
    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
    private static string Md5Hex(string text) => Convert.ToHexString(MD5.HashData(Ascii(text))).ToLowerInvariant();

    // users row 28 ("loluser") in SQL/reality_hd.sql: the one key every stock row shares.
    private const string TemplateModulus = "ed7707ab64f87a51dd9020f73752e9455386a402bb539c64f8a4310ec211499dd503da74187ee109c325cf07ec0a2fcf904c15cf38918b9782578d589294b6bc0033802ee099e8a8163d3a1d8e1a7b3238ce4b58a20c01b47180823c3adc0b1d";
    private const string TemplatePrivate = "5acba10cd3c86b012f1176042bbdd1a990e0a81f2981d9ea5f119a496851e7710daea65990e537ed24f7de37bc22033809a6d66cab7205376859c0794708d71a6279537218a3d3496096ad23532179f1bb71315e64ffbee609b98d8f08e05887";

    // ---------- PasswordHasher ----------

    [Fact]
    public void Digest_round_trips_and_rejects_a_wrong_password()
    {
        string digest = PasswordHasher.Hash(Ascii("correct horse battery"), 2_000);
        Assert.StartsWith("$pbkdf2-sha256$v=1$i=2000$", digest);
        Assert.True(PasswordHasher.Verify(Ascii("correct horse battery"), digest, out _));
        Assert.False(PasswordHasher.Verify(Ascii("correct horse batterY"), digest, out _));
        Assert.False(PasswordHasher.Verify(Array.Empty<byte>(), digest, out _));
    }

    [Fact]
    public void Default_parameters_are_versioned_and_salted()
    {
        string first = PasswordHasher.Hash(Ascii("same password"));
        string second = PasswordHasher.Hash(Ascii("same password"));
        Assert.StartsWith("$pbkdf2-sha256$v=1$i=" + PasswordHasher.DefaultIterations + "$", first);
        Assert.NotEqual(first, second); // per-user salt
        Assert.True(PasswordHasher.Verify(Ascii("same password"), first, out bool rehash));
        Assert.False(rehash);
        Assert.DoesNotContain("same password", first);
    }

    [Fact]
    public void Older_parameters_ask_for_a_rehash()
    {
        string weak = PasswordHasher.Hash(Ascii("password1234"), 5_000);
        Assert.True(PasswordHasher.Verify(Ascii("password1234"), weak, out bool rehash));
        Assert.True(rehash);
        Assert.False(PasswordHasher.Verify(Ascii("password1235"), weak, out rehash));
        Assert.False(rehash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ba2bb6cc7d44b5b63dd4cef48baed88c")]
    [InlineData("$pbkdf2-sha256$v=2$i=2000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$pbkdf2-sha256$v=1$i=10$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$pbkdf2-sha256$v=1$i=2000$not base64$AAAA")]
    public void Malformed_digests_never_verify(string? stored)
    {
        Assert.False(PasswordHasher.Verify(Ascii("anything at all"), stored!, out _));
        Assert.False(PasswordHasher.IsDigest(stored!));
    }

    [Fact]
    public void Tampered_digest_fails()
    {
        string digest = PasswordHasher.Hash(Ascii("tamper proof pw"), 2_000);
        string[] parts = digest.Split('$');
        byte[] hash = Convert.FromBase64String(parts[5]);
        hash[0] ^= 1;
        parts[5] = Convert.ToBase64String(hash);
        Assert.False(PasswordHasher.Verify(Ascii("tamper proof pw"), string.Join('$', parts), out _));
    }

    [Fact]
    public void Legacy_md5_check_matches_the_old_column()
    {
        Assert.True(PasswordHasher.VerifyLegacyMd5(Ascii("hunter2hunter2"), Md5Hex("hunter2hunter2")));
        Assert.True(PasswordHasher.VerifyLegacyMd5(Ascii("hunter2hunter2"), Md5Hex("hunter2hunter2").ToUpperInvariant()));
        Assert.False(PasswordHasher.VerifyLegacyMd5(Ascii("hunter2hunter3"), Md5Hex("hunter2hunter2")));
        Assert.False(PasswordHasher.VerifyLegacyMd5(Ascii("x"), "not hex"));
        Assert.False(PasswordHasher.VerifyLegacyMd5(Ascii("x"), null!));
    }

    // ---------- AccountLogin: verification, MD5 migration, status, lockout ----------

    private sealed class FakeLoginStore : ILoginStore
    {
        public readonly Dictionary<string, LoginAccount> Rows = new(StringComparer.OrdinalIgnoreCase);
        public int Upgrades;
        public LoginAccount Add(uint id, string name, string? md5 = null, string? digest = null, int status = 0)
        {
            var row = new LoginAccount { UserId = id, Username = name, LegacyMd5 = md5, Digest = digest, Status = status };
            Rows[name] = row;
            return row;
        }
        public LoginAccount? FindForLogin(string username) => Rows.TryGetValue(username, out var row) ? row : null;
        public void UpgradeDigest(uint userId, string digest)
        {
            LoginAccount row = Rows.Values.Single(r => r.UserId == userId);
            row.Digest = digest;
            row.LegacyMd5 = null;
            Upgrades++;
        }
    }

    private static readonly IPAddress Remote = IPAddress.Parse("203.0.113.7");
    private static LoginThrottle NewThrottle(Func<DateTime>? clock = null) =>
        new(new LoginThrottleOptions(), clock ?? (() => new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void Legacy_md5_account_migrates_on_its_next_good_login()
    {
        var store = new FakeLoginStore();
        LoginAccount row = store.Add(28, "loluser", md5: Md5Hex("oldpassword1"));
        var login = new AccountLogin(store, NewThrottle());

        LoginResult wrong = login.Authenticate("loluser", Ascii("oldpassword2"), Remote, false);
        Assert.Equal(LoginOutcome.BadCredentials, wrong.Outcome);
        Assert.Equal(0, store.Upgrades);
        Assert.NotNull(row.LegacyMd5);

        LoginResult first = login.Authenticate("LOLUSER", Ascii("oldpassword1"), Remote, false);
        Assert.Equal(LoginOutcome.Ok, first.Outcome);
        Assert.Equal(28u, first.UserId);
        Assert.True(first.Migrated);
        Assert.Null(row.LegacyMd5);
        Assert.True(PasswordHasher.IsDigest(row.Digest));

        // From now on only the new digest is used, and it isn't rewritten again.
        LoginResult second = login.Authenticate("loluser", Ascii("oldpassword1"), Remote, false);
        Assert.Equal(LoginOutcome.Ok, second.Outcome);
        Assert.False(second.Migrated);
        Assert.Equal(1, store.Upgrades);
        Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("loluser", Ascii("oldpassword2"), Remote, false).Outcome);
    }

    [Fact]
    public void Unknown_user_and_wrong_password_look_the_same()
    {
        var store = new FakeLoginStore();
        store.Add(5, "real", digest: PasswordHasher.Hash(Ascii("realpassword"), 2_000));
        var login = new AccountLogin(store, NewThrottle());
        Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("nobody", Ascii("realpassword"), Remote, false).Outcome);
        Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("real", Ascii("wrongpassword"), Remote, false).Outcome);
        Assert.Equal(0u, login.Authenticate("nobody", Ascii("x"), Remote, false).UserId);
    }

    [Fact]
    public void Row_without_any_password_never_logs_in()
    {
        var store = new FakeLoginStore();
        store.Add(9, "empty");
        var login = new AccountLogin(store, NewThrottle());
        Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("empty", Array.Empty<byte>(), Remote, false).Outcome);
        Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("empty", Ascii("anything"), Remote, false).Outcome);
    }

    [Fact]
    public void Status_is_honoured_only_after_the_password()
    {
        var store = new FakeLoginStore();
        store.Add(1, "banned", digest: PasswordHasher.Hash(Ascii("banned-pass"), 2_000), status: AccountLogin.StatusBanned);
        store.Add(2, "disabled", digest: PasswordHasher.Hash(Ascii("disabled-pass"), 2_000), status: AccountLogin.StatusDisabled);
        store.Add(3, "odd", digest: PasswordHasher.Hash(Ascii("odd-pass"), 2_000), status: 7);
        var login = new AccountLogin(store, NewThrottle());
        Assert.Equal(LoginOutcome.Banned, login.Authenticate("banned", Ascii("banned-pass"), Remote, false).Outcome);
        Assert.Equal(LoginOutcome.Inactive, login.Authenticate("disabled", Ascii("disabled-pass"), Remote, false).Outcome);
        Assert.Equal(LoginOutcome.Inactive, login.Authenticate("odd", Ascii("odd-pass"), Remote, false).Outcome);
        // A stranger with the wrong password learns nothing about the ban.
        Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("banned", Ascii("guess"), Remote, false).Outcome);
        // MXO_DEV_ANY_PASSWORD doesn't get around a ban either.
        Assert.Equal(LoginOutcome.Banned, login.Authenticate("banned", Ascii("guess"), Remote, true).Outcome);
    }

    [Fact]
    public void Lockout_stops_even_the_right_password_until_it_expires()
    {
        DateTime now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var store = new FakeLoginStore();
        store.Add(4, "target", digest: PasswordHasher.Hash(Ascii("rightpassword"), 2_000));
        var login = new AccountLogin(store, NewThrottle(() => now));
        for (int i = 0; i < 5; i++)
            Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("target", Ascii("guess" + i), Remote, false).Outcome);
        LoginResult locked = login.Authenticate("target", Ascii("rightpassword"), Remote, false);
        Assert.Equal(LoginOutcome.Locked, locked.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(30), locked.RetryAfter);
        now = now.AddSeconds(31);
        Assert.Equal(LoginOutcome.Ok, login.Authenticate("TARGET", Ascii("rightpassword"), Remote, false).Outcome);
    }

    [Fact]
    public void Dev_any_password_never_migrates_or_counts_failures()
    {
        var store = new FakeLoginStore();
        LoginAccount row = store.Add(28, "loluser", md5: Md5Hex("oldpassword1"));
        var login = new AccountLogin(store, NewThrottle());
        for (int i = 0; i < 10; i++)
            Assert.Equal(LoginOutcome.Ok, login.Authenticate("loluser", Ascii("placeholder"), Remote, true).Outcome);
        Assert.Equal(0, store.Upgrades);
        Assert.NotNull(row.LegacyMd5);
        Assert.Equal(LoginOutcome.BadCredentials, login.Authenticate("nobody", Ascii("placeholder"), Remote, true).Outcome);
    }

    // ---------- LoginThrottle ----------

    [Fact]
    public void Account_lock_backs_off_and_clears_on_success()
    {
        DateTime now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var throttle = NewThrottle(() => now);
        for (int i = 0; i < 4; i++) throttle.RecordFailure("Alice", Remote);
        Assert.False(throttle.IsBlocked("alice", Remote, out _));
        throttle.RecordFailure("alice", Remote);
        Assert.True(throttle.IsBlocked("ALICE", Remote, out TimeSpan wait));
        Assert.Equal(TimeSpan.FromSeconds(30), wait);

        now = now.AddSeconds(31);
        Assert.False(throttle.IsBlocked("alice", Remote, out _));
        throttle.RecordFailure("alice", Remote);
        Assert.True(throttle.IsBlocked("alice", Remote, out wait));
        Assert.Equal(TimeSpan.FromSeconds(60), wait);

        now = now.AddSeconds(61);
        throttle.RecordSuccess("alice");
        Assert.False(throttle.IsBlocked("alice", Remote, out _));
        throttle.RecordFailure("alice", Remote);
        Assert.False(throttle.IsBlocked("alice", Remote, out _));
    }

    [Fact]
    public void Account_lock_is_capped()
    {
        DateTime now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var throttle = NewThrottle(() => now);
        TimeSpan longest = TimeSpan.Zero;
        for (int i = 0; i < 30; i++)
        {
            throttle.RecordFailure("bob", null!);
            if (!throttle.IsBlocked("bob", null!, out TimeSpan wait)) continue;
            if (wait > longest) longest = wait;
            now += wait + TimeSpan.FromSeconds(1);
        }
        Assert.Equal(TimeSpan.FromMinutes(15), longest);
    }

    [Fact]
    public void Address_lock_spans_accounts_and_survives_a_success()
    {
        DateTime now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var throttle = NewThrottle(() => now);
        for (int i = 0; i < 20; i++) throttle.RecordFailure("victim" + i, Remote);
        throttle.RecordSuccess("someone");
        Assert.True(throttle.IsBlocked("someone", Remote, out TimeSpan wait));
        Assert.Equal(TimeSpan.FromMinutes(1), wait);
        Assert.False(throttle.IsBlocked("someone", IPAddress.Parse("198.51.100.1"), out _));
    }

    [Fact]
    public void Loopback_is_exempt_from_the_address_limit_but_not_the_account_one()
    {
        DateTime now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var throttle = NewThrottle(() => now);
        for (int i = 0; i < 50; i++) throttle.RecordFailure("web" + i, IPAddress.Loopback);
        Assert.False(throttle.IsBlocked("fresh", IPAddress.Loopback, out _));
        Assert.True(throttle.IsBlocked("web0", IPAddress.Loopback, out _) == false);
        for (int i = 0; i < 5; i++) throttle.RecordFailure("webuser", IPAddress.Loopback);
        Assert.True(throttle.IsBlocked("webuser", IPAddress.Loopback, out _));

        var strict = new LoginThrottle(new LoginThrottleOptions { ExemptLoopback = false }, () => now);
        for (int i = 0; i < 20; i++) strict.RecordFailure("web" + i, IPAddress.Loopback);
        Assert.True(strict.IsBlocked("fresh", IPAddress.Loopback, out _));
    }

    [Fact]
    public void Old_failures_fall_out_of_the_window()
    {
        DateTime now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var throttle = NewThrottle(() => now);
        for (int i = 0; i < 4; i++) throttle.RecordFailure("carol", Remote);
        now = now.AddMinutes(16);
        throttle.RecordFailure("carol", Remote);
        Assert.False(throttle.IsBlocked("carol", Remote, out _));
    }

    // ---------- AccountKeys ----------

    [Fact]
    public void Generated_keys_fit_the_ticket_and_are_unique()
    {
        AccountKey first = AccountKeys.Generate();
        AccountKey second = AccountKeys.Generate();
        foreach (AccountKey key in new[] { first, second })
        {
            Assert.Equal(96, key.Modulus.Length);
            Assert.Equal(96, key.PrivateExponent.Length);
            Assert.Equal(17, key.PublicExponent);
            Assert.True((key.Modulus[0] & 0x80) != 0, "768-bit modulus: top bit set");
            Assert.True(AccountKeys.IsValid(key.Modulus, key.PrivateExponent, key.PublicExponent));
        }
        Assert.NotEqual(Convert.ToHexString(first.Modulus), Convert.ToHexString(second.Modulus));
    }

    [Fact]
    public void Generated_key_works_the_way_margin_and_the_client_use_it()
    {
        // Margin: OAEP-SHA1 encryption to (n, e=00 00 00 11) with .NET's RSA, as MarginEncryption does.
        AccountKey key = AccountKeys.Generate();
        var margin = new MarginEncryption();
        margin.setUserPubKey(new byte[] { 0x00, 0x00, 0x00, 0x11 }, key.Modulus);
        byte[] secret = RandomNumberGenerator.GetBytes(33);
        byte[] cipher = margin.encryptUsersPublic(secret);
        // Client: raw RSA with d, then OAEP decode (HeadlessClient's copy of what the client does).
        Assert.Equal(secret, HeadlessClient.AuthLogin.DecryptWithAccountKey(cipher, key.Modulus, key.PrivateExponent));
    }

    [Fact]
    public void Shared_template_key_is_still_valid()
    {
        Assert.True(AccountKeys.IsValid(Convert.FromHexString(TemplateModulus), Convert.FromHexString(TemplatePrivate), 17));
    }

    [Fact]
    public void Broken_keys_are_rejected()
    {
        AccountKey key = AccountKeys.Generate();
        byte[] wrongD = (byte[])key.PrivateExponent.Clone();
        wrongD[95] ^= 2;
        Assert.False(AccountKeys.IsValid(key.Modulus, wrongD, 17));
        Assert.False(AccountKeys.IsValid(key.Modulus, key.PrivateExponent, 3));
        Assert.False(AccountKeys.IsValid(key.Modulus[1..], key.PrivateExponent, 17));
        Assert.False(AccountKeys.IsValid(null!, key.PrivateExponent, 17));
        Assert.False(AccountKeys.IsValid(new byte[96], key.PrivateExponent, 17));
    }

    [Fact]
    public void Primality_test_agrees_on_known_values()
    {
        Assert.True(AccountKeys.IsProbablePrime(BigInteger.Parse("170141183460469231731687303715884105727"))); // 2^127-1
        Assert.False(AccountKeys.IsProbablePrime(BigInteger.Parse("170141183460469231731687303715884105729")));
        Assert.False(AccountKeys.IsProbablePrime(561)); // Carmichael
        Assert.True(AccountKeys.IsProbablePrime(7919));
    }

    // ---------- AuthTicketCodec (margin's ticket check) ----------

    private static (byte[] Signature, byte[] Signed) Ticket(uint userId, uint expires, byte[]? modulus = null)
    {
        byte[] signed = AuthTicketCodec.BuildSignedData(userId, "TicketUser", expires,
            modulus ?? Convert.FromHexString(TemplateModulus), 1234);
        return (AuthTicketCodec.Sign(signed), signed);
    }

    [Fact]
    public void Issued_ticket_verifies_and_yields_its_user_and_key()
    {
        AccountKey key = AccountKeys.Generate();
        var (signature, signed) = Ticket(777, 2_000_000_000, key.Modulus);
        Assert.Equal(182, signed.Length);
        Assert.Equal(128, signature.Length);
        Assert.True(AuthTicketCodec.TryVerify(signature, signed, 2_000_000_000 - 5, 0, out VerifiedTicket? ticket));
        Assert.Equal(777u, ticket!.UserId);
        Assert.Equal("TicketUser", ticket.Username);
        Assert.Equal(key.Modulus, ticket.PublicModulus);
    }

    [Fact]
    public void Forged_or_altered_tickets_are_refused()
    {
        var (signature, signed) = Ticket(777, 2_000_000_000);
        byte[] otherUser = (byte[])signed.Clone();
        BitConverter.GetBytes(2u).CopyTo(otherUser, AuthTicketCodec.UserIdOffset);
        Assert.False(AuthTicketCodec.TryVerify(signature, otherUser, 1_900_000_000, 0, out _));

        byte[] otherKey = (byte[])signed.Clone();
        otherKey[AuthTicketCodec.ModulusOffset + 10] ^= 1;
        Assert.False(AuthTicketCodec.TryVerify(signature, otherKey, 1_900_000_000, 0, out _));

        byte[] badSignature = (byte[])signature.Clone();
        badSignature[50] ^= 1;
        Assert.False(AuthTicketCodec.TryVerify(badSignature, signed, 1_900_000_000, 0, out _));

        // A ticket "signed" by anyone but the auth server key.
        using RSA stranger = RSA.Create(1024);
        byte[] strangerSignature = stranger.SignData(new Md5().digest(signed), HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
        Assert.False(AuthTicketCodec.TryVerify(strangerSignature, signed, 1_900_000_000, 0, out _));

        Assert.False(AuthTicketCodec.TryVerify(signature, signed[..181], 1_900_000_000, 0, out _));
        Assert.False(AuthTicketCodec.TryVerify(signature[..127], signed, 1_900_000_000, 0, out _));
    }

    [Fact]
    public void Expiry_allows_the_grace_period_only()
    {
        var (signature, signed) = Ticket(777, 1_000_000);
        Assert.True(AuthTicketCodec.TryVerify(signature, signed, 1_000_000, 0, out _));
        Assert.False(AuthTicketCodec.TryVerify(signature, signed, 1_000_001, 0, out _));
        Assert.True(AuthTicketCodec.TryVerify(signature, signed, 1_000_000 + 3600, 3600, out _));
        Assert.False(AuthTicketCodec.TryVerify(signature, signed, 1_000_000 + 3601, 3600, out _));
    }

    [Fact]
    public void Ticket_layout_matches_what_headless_and_margin_read()
    {
        AccountKey key = AccountKeys.Generate();
        byte[] signed = AuthTicketCodec.BuildSignedData(0x01020304, "abc", 0x11223344, key.Modulus, 0x55667788);
        Assert.Equal(0x01, signed[0]);
        Assert.Equal(0x01020304u, BitConverter.ToUInt32(signed, 1));
        Assert.Equal(Ascii("abc"), signed[5..8]);
        Assert.All(signed[8..38], b => Assert.Equal(0, b));
        Assert.Equal(new byte[] { 0x00, 0x01 }, signed[38..40]);
        Assert.Equal(0x11223344u, BitConverter.ToUInt32(signed, 44));
        Assert.Equal(new byte[] { 0x00, 0x11 }, signed[80..82]);
        Assert.Equal(key.Modulus, signed[82..178]); // HeadlessClient AuthTicket.PublicModulus
        Assert.Equal(0x55667788u, BitConverter.ToUInt32(signed, 178));
        Assert.Throws<ArgumentException>(() => AuthTicketCodec.BuildSignedData(1, new string('a', 33), 0, key.Modulus, 0));
    }

    // ---------- AccountRules and SignupService ----------

    [Theory]
    [InlineData("Neo", true)]
    [InlineData("CSR_AbilityQA", true)]
    [InlineData("a1234567890123456789", true)]
    [InlineData("a12345678901234567890", false)]
    [InlineData("ab", false)]
    [InlineData("1neo", false)]
    [InlineData("_neo", false)]
    [InlineData("neo neo", false)]
    [InlineData("neo'--", false)]
    [InlineData("néo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Username_rules(string? name, bool valid) => Assert.Equal(valid, AccountRules.IsValidUsername(name!));

    [Theory]
    [InlineData("0123456789", true)]
    [InlineData("with spaces ~!@#$%", true)]
    [InlineData("012345678", false)]
    [InlineData("tab\tinside password", false)]
    [InlineData("unicode ü password", false)]
    [InlineData(null, false)]
    public void Password_rules(string? password, bool valid) => Assert.Equal(valid, AccountRules.IsValidPassword(password!));

    [Fact]
    public void Password_length_cap()
    {
        Assert.True(AccountRules.IsValidPassword(new string('x', 64)));
        Assert.False(AccountRules.IsValidPassword(new string('x', 65)));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("neo@example.com", true)]
    [InlineData("neo@localhost", false)]
    [InlineData("not an email", false)]
    public void Email_rules(string? email, bool valid) => Assert.Equal(valid, AccountRules.IsValidEmail(email!));

    private sealed class FakeSignupStore : ISignupStore
    {
        public readonly List<NewAccount> Created = new();
        public bool Fail;
        public bool UsernameTaken(string username) => Created.Any(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase));
        public uint Create(NewAccount account)
        {
            if (Fail) throw new InvalidOperationException("db down");
            if (UsernameTaken(account.Username)) throw new UsernameTakenException();
            Created.Add(account);
            return (uint)(1000 + Created.Count);
        }
    }

    private sealed class FixedBotCheck : IBotCheck
    {
        public bool Answer = true;
        public string? SeenToken;
        public Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken cancel)
        {
            SeenToken = token;
            return Task.FromResult(Answer);
        }
    }

    private static SignupService Service(FakeSignupStore store, IBotCheck? bot = null) =>
        new(store, bot ?? new NoBotCheck(), password => PasswordHasher.Hash(password, 2_000));

    [Fact]
    public async Task Signup_creates_a_hashed_account_with_its_own_key()
    {
        var store = new FakeSignupStore();
        SignupResult result = await Service(store).SignupAsync(
            new SignupRequest { Username = "  NewPlayer ", Password = "a fine passphrase", Email = "p@example.com" }, "203.0.113.1", default);
        Assert.Equal(SignupOutcome.Created, result.Outcome);
        Assert.Equal("NewPlayer", result.Username);
        NewAccount row = Assert.Single(store.Created);
        Assert.True(PasswordHasher.Verify(Ascii("a fine passphrase"), row.Digest, out _));
        Assert.DoesNotContain("a fine passphrase", row.Digest);
        Assert.True(AccountKeys.IsValid(row.Key.Modulus, row.Key.PrivateExponent, row.Key.PublicExponent));
        Assert.Equal("p@example.com", row.Email);
    }

    [Fact]
    public async Task Signup_refuses_duplicates_case_insensitively()
    {
        var store = new FakeSignupStore();
        var service = Service(store);
        Assert.Equal(SignupOutcome.Created, (await service.SignupAsync(new SignupRequest { Username = "Trinity", Password = "first password!" }, "", default)).Outcome);
        Assert.Equal(SignupOutcome.UsernameTaken, (await service.SignupAsync(new SignupRequest { Username = "TRINITY", Password = "second password" }, "", default)).Outcome);
        Assert.Single(store.Created);
    }

    [Theory]
    [InlineData("x", "a long enough password", null, SignupOutcome.InvalidUsername)]
    [InlineData("Morpheus", "short", null, SignupOutcome.InvalidPassword)]
    [InlineData("Morpheus", "morpheus", null, SignupOutcome.InvalidPassword)]
    [InlineData("Morpheus10", "morpheus10", null, SignupOutcome.InvalidPassword)]
    [InlineData("Morpheus", "a long enough password", "bad", SignupOutcome.InvalidEmail)]
    public async Task Signup_validation(string name, string password, string? email, SignupOutcome expected)
    {
        var store = new FakeSignupStore();
        Assert.Equal(expected, (await Service(store).SignupAsync(new SignupRequest { Username = name, Password = password, Email = email }, "", default)).Outcome);
        Assert.Empty(store.Created);
    }

    [Fact]
    public async Task Signup_bot_check_runs_first_and_fails_closed()
    {
        var store = new FakeSignupStore();
        var bot = new FixedBotCheck { Answer = false };
        SignupResult result = await Service(store, bot).SignupAsync(
            new SignupRequest { Username = "Smith", Password = "a long enough password", TurnstileToken = "tok" }, "203.0.113.1", default);
        Assert.Equal(SignupOutcome.BotCheckFailed, result.Outcome);
        Assert.Equal("tok", bot.SeenToken);
        Assert.Empty(store.Created);
        Assert.False(await new TurnstileBotCheck("secret").VerifyAsync("", "203.0.113.1", default));
    }

    [Fact]
    public async Task Signup_store_failure_is_generic()
    {
        var store = new FakeSignupStore { Fail = true };
        SignupResult result = await Service(store).SignupAsync(new SignupRequest { Username = "Oracle", Password = "a long enough password" }, "", default);
        Assert.Equal(SignupOutcome.Unavailable, result.Outcome);
    }
}
