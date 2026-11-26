using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using hds.accounts;

namespace HeadlessClient;

/// <summary>
/// Server-side accounts end to end (docs/ACCOUNTS.md): sign up over HTTP, log in through auth, margin and world with
/// the real password, relog; a legacy MD5 row migrating on its first login; lockout; and margin/world refusing
/// tickets and sessions the auth server didn't issue. Runs with password checks on (MXO_DEV_ANY_PASSWORD=0).
/// </summary>
public static partial class Scenarios
{
    public const string AccountSignupScenario = "account-signup";

    // A property, not a field: Scenarios.All (another file of this partial class) may be initialized first.
    public static IReadOnlyDictionary<string, string> AccountSignupEnvironment => new Dictionary<string, string>(StillNpcs)
    {
        ["MXO_DEV_ANY_PASSWORD"] = "0",
        ["MXO_SIGNUP_HTTP"] = "1",
        ["MXO_SIGNUP_URL"] = "http://127.0.0.1:0", // Kestrel picks a free port; the server prints it
    };

    private const uint UsernameMismatch = 0x11000002;
    private const uint MaxAttempts = 0x1100000D;

    /// <summary>The sign-up URL a server started with <see cref="AccountSignupEnvironment"/> bound to.</summary>
    public static Uri SignupUrl(ServerInstance server)
    {
        string line = server.OutputLines("Sign-up API listening on ").LastOrDefault()
                      ?? throw new ScenarioFailure("the server didn't start its sign-up API");
        string url = line[(line.IndexOf("listening on ", StringComparison.Ordinal) + "listening on ".Length)..].Split(' ', ',')[0].Trim();
        return new Uri(url.TrimEnd('/') + "/");
    }

    /// <summary>A throwaway password that fits the sign-up rules (and the launcher's text box).</summary>
    public static string ThrowawayPassword() =>
        "qa-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();

    /// <summary>Gives an existing account a character copied from the template, as AddPlayerAsync does.</summary>
    public static async Task AddCharacterAsync(ThrowawayDatabase database, uint userId, uint charId, string handle,
        double x = 17020, double y = 495, double z = 2693)
    {
        await database.ExecuteAsync(FormattableString.Invariant($@"
INSERT INTO characters (charId, userId, worldId, status, handle, firstName, lastName, background, x, y, z, rotation,
    healthC, healthM, innerStrC, innerStrM, level, profession, alignment, pvpflag, conquest_points, exp, cash,
    district, districtId, factionId, crewId, is_deleted, is_online, created)
  VALUES ({charId}, {userId}, 1, 0, '{handle}', 'New', 'Account', '', {x}, {y}, {z}, 0,
    500, 500, 200, 200, 10, 2, 0, 0, 0, 0, 1000, 'slums', 1, 0, 0, 0, 0, NOW());
CREATE TEMPORARY TABLE rsi_copy AS SELECT * FROM rsivalues WHERE charid = 2;
UPDATE rsi_copy SET charid = {charId};
INSERT INTO rsivalues SELECT * FROM rsi_copy;
DROP TEMPORARY TABLE rsi_copy;
INSERT INTO char_abilities (char_id, slot, ability_id, level, is_loaded, ability_name)
  SELECT {charId}, slot, ability_id, level, is_loaded, ability_name FROM char_abilities WHERE char_id = 2;
"));
    }

    /// <summary>A pre-migration account: passwordmd5 only, and the shared template key, like every stock row.</summary>
    public static async Task AddLegacyAccountAsync(ThrowawayDatabase database, uint userId, string username, string password)
    {
        string md5 = Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(password))).ToLowerInvariant();
        await database.ExecuteAsync($@"
INSERT INTO users (userId, username, publicExponent, publicModulus, privateExponent, account_status, passwordmd5)
  SELECT {userId}, '{username}', publicExponent, publicModulus, privateExponent, 0, '{md5}' FROM users WHERE userId = 28;");
    }

    private static async Task<uint> RefusedStatusAsync(ServerAddress server, string username, string password)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        try
        {
            await AuthLogin.LoginAsync(server.Host, server.AuthPort, username, password, timeout.Token);
        }
        catch (InvalidOperationException refused) when (refused.Message.Contains("refused: status ", StringComparison.Ordinal))
        {
            string hex = refused.Message[(refused.Message.LastIndexOf(' ') + 1)..];
            return uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        throw new ScenarioFailure($"{username} logged in with a password that should have been refused");
    }

    private static async Task QuitAsync(HeadlessPlayer player)
    {
        player.SendRpc("80 fe 00");
        await player.WaitForRpcAsync("80 fd");
        player.Dispose();
    }

    private static async Task AccountSignup(ScenarioContext context)
    {
        ServerAddress server = context.Server.Address;
        using HttpClient http = new() { BaseAddress = SignupUrl(context.Server), Timeout = TimeSpan.FromSeconds(30) };

        // Health, then validation and the generic errors.
        HttpResponseMessage health = await http.GetAsync("api/health");
        Check.Equal(HttpStatusCode.OK, health.StatusCode, "health endpoint");
        string username = "Signup" + RandomNumberGenerator.GetInt32(100000, 999999);
        string password = ThrowawayPassword();
        Check.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("api/signup", new { username = "x!", password })).StatusCode, "bad username refused");
        Check.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("api/signup", new { username, password = "short" })).StatusCode, "short password refused");
        Check.Equal(HttpStatusCode.UnsupportedMediaType, (await http.PostAsync("api/signup", new StringContent("username=a"))).StatusCode, "non-JSON refused");

        HttpResponseMessage created = await http.PostAsJsonAsync("api/signup", new { username, password, email = "qa@example.com" });
        string createdBody = await created.Content.ReadAsStringAsync();
        Check.Equal(HttpStatusCode.Created, created.StatusCode, "sign-up: " + createdBody);
        Check.That(!createdBody.Contains(password, StringComparison.Ordinal), "the reply never echoes the password");
        HttpResponseMessage taken = await http.PostAsJsonAsync("api/signup", new { username = username.ToUpperInvariant(), password = ThrowawayPassword() });
        Check.Equal(HttpStatusCode.Conflict, taken.StatusCode, "same name in another case is taken");
        Check.That((await taken.Content.ReadAsStringAsync()).Contains("username_taken"), "taken is the one distinguishable error");

        // The row: a digest and no MD5, a fresh key unlike the shared template one, the email.
        uint userId = (uint)await context.Database.LongAsync($"SELECT userId FROM users WHERE username='{username}'");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT password_digest LIKE '$pbkdf2-sha256$v=1$i=600000$%' AND passwordmd5 IS NULL FROM users WHERE userId={userId}"), "new account stores only the versioned digest");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM users u JOIN users t ON t.userId=28 WHERE u.userId={userId} AND u.publicModulus=t.publicModulus"), "new account doesn't share the template key");
        Check.Equal(96L, await context.Database.LongAsync($"SELECT LENGTH(publicModulus) FROM users WHERE userId={userId}"), "96-byte modulus");
        Check.Equal("qa@example.com", await context.Database.ScalarAsync($"SELECT email_adress FROM users WHERE userId={userId}"), "email stored");
        Check.That(!context.Server.FullOutput().Contains(password, StringComparison.Ordinal), "the server never logs the password");

        // Wrong password: a framed, generic refusal (the same code as an unknown name).
        Check.Equal(UsernameMismatch, await RefusedStatusAsync(server, username, password + "x"), "wrong password refused generically");
        Check.Equal(UsernameMismatch, await RefusedStatusAsync(server, "NoSuchUser" + userId, password), "unknown user gets the same code");

        // Full login with the new account's own key, into the world, then a relog.
        const uint charId = 30301; // rsivalues.charid is a signed smallint
        await AddCharacterAsync(context.Database, userId, charId, "SignupQA");
        AuthTicket ticket;
        using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20)))
            ticket = await AuthLogin.LoginAsync(server.Host, server.AuthPort, username.ToLowerInvariant(), password, timeout.Token);
        Check.Equal(userId, ticket.UserId, "ticket names the new account (case-insensitive login)");
        Check.Equal(await context.Database.ScalarAsync($"SELECT HEX(publicModulus) FROM users WHERE userId={userId}"),
            Convert.ToHexString(ticket.PublicModulus), "ticket carries the account's own modulus");
        HeadlessPlayer first = await HeadlessPlayer.LoginAsync(server, username, "SignupQA", password);
        await first.EnterWorldAsync();
        Check.That(first.World.Problems.Count == 0, "first login wire: " + string.Join("; ", first.World.Problems));
        await QuitAsync(first);
        await Task.Delay(TimeSpan.FromSeconds(33)); // the ordinary >30s world/margin reservation cleanup
        using (HeadlessPlayer relog = await HeadlessPlayer.LoginAsync(server, username, "SignupQA", password))
        {
            await relog.EnterWorldAsync();
            Check.That(relog.World.Problems.Count == 0, "relog wire: " + string.Join("; ", relog.World.Problems));
            await QuitAsync(relog);
        }

        // A legacy MD5 row: refused with a wrong password (and not migrated), migrated by the first good login,
        // and from then on checked against the digest.
        const uint legacyId = 990302;
        string legacyPassword = ThrowawayPassword();
        await AddLegacyAccountAsync(context.Database, legacyId, "LegacyQA", legacyPassword);
        await AddCharacterAsync(context.Database, legacyId, 30302, "LegacyQA");
        Check.Equal(UsernameMismatch, await RefusedStatusAsync(server, "LegacyQA", legacyPassword + "x"), "legacy wrong password");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT passwordmd5 IS NOT NULL AND password_digest IS NULL FROM users WHERE userId={legacyId}"), "no migration on a failed login");
        using (HeadlessPlayer legacy = await HeadlessPlayer.LoginAsync(server, "LegacyQA", "LegacyQA", legacyPassword))
        {
            await legacy.EnterWorldAsync();
            await QuitAsync(legacy);
        }
        Check.Equal(1L, await context.Database.LongAsync($"SELECT passwordmd5 IS NULL AND passwordHash IS NULL AND password_digest LIKE '$pbkdf2-sha256$%' FROM users WHERE userId={legacyId}"), "legacy row migrated");
        Check.That(context.Server.OutputLines($"Auth: account {legacyId} password storage upgraded").Count == 1, "migration logged once, without secrets");
        using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20)))
            await AuthLogin.LoginAsync(server.Host, server.AuthPort, "LegacyQA", legacyPassword, timeout.Token);
        Check.Equal(UsernameMismatch, await RefusedStatusAsync(server, "LegacyQA", legacyPassword + "x"), "migrated row still refuses a wrong password");

        // Margin only takes tickets the auth server signed: change the user id and it hangs up.
        byte[] forged = (byte[])ticket.SignedData.Clone();
        BitConverter.GetBytes(legacyId).CopyTo(forged, 1);
        var forgedTicket = new AuthTicket { Signature = ticket.Signature, SignedData = forged, PrivateExponent = ticket.PrivateExponent, Characters = ticket.Characters };
        bool marginRefused = false;
        using (MarginSession margin = new())
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            try { await margin.ConnectAsync(server.Host, server.MarginPort, forgedTicket, timeout.Token); }
            catch (Exception) { marginRefused = true; }
        }
        Check.That(marginRefused, "margin refuses a forged ticket");
        Check.That(context.Server.OutputLines("Margin: refused a ticket the auth server didn't issue").Count >= 1, "margin logged the forgery");

        // The world opens no UDP session for a character no verified margin connection holds.
        using (WorldSession stray = new(server.Host, server.WorldPort))
        {
            stray.SendSessionRequest(30302);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        Check.That(context.Server.OutputLines("World: refused a UDP session for character 30302").Count >= 1, "world refuses an unverified UDP session");

        // Lockout: five failures lock the account, so even the right password is refused for a while.
        const uint lockId = 990303;
        string lockPassword = ThrowawayPassword();
        await AddLegacyAccountAsync(context.Database, lockId, "LockQA", lockPassword);
        for (int i = 0; i < 5; i++)
            Check.Equal(UsernameMismatch, await RefusedStatusAsync(server, "LockQA", "wrong-" + i), "failure " + (i + 1));
        Check.Equal(MaxAttempts, await RefusedStatusAsync(server, "LockQA", lockPassword), "locked account refuses the right password");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT passwordmd5 IS NOT NULL FROM users WHERE userId={lockId}"), "no migration while locked");

        // Banned and disabled accounts stop at auth (after the password).
        await context.Database.ExecuteAsync($"UPDATE users SET account_status=1 WHERE userId={userId}");
        Check.Equal(0x11000003u, await RefusedStatusAsync(server, username, password), "banned account");
        await context.Database.ExecuteAsync($"UPDATE users SET account_status=2 WHERE userId={userId}");
        Check.Equal(0x11000007u, await RefusedStatusAsync(server, username, password), "disabled account");

        Check.That(!context.Server.FullOutput().Contains(password, StringComparison.Ordinal) &&
                   !context.Server.FullOutput().Contains(legacyPassword, StringComparison.Ordinal), "no password in the server output");
        Check.That(!context.ServerProblems().Any(), "server problems: " + string.Join("; ", context.ServerProblems()));
        Console.WriteLine($"  account-signup: signed up account {userId}, logged in, relogged; legacy {legacyId} migrated; forged ticket, stray UDP, lockout, ban and disable refused");
    }
}
