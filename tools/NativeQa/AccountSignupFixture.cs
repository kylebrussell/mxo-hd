using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;

namespace NativeQa;

/// <summary>
/// NativeQa <c>account-signup</c>: a disposable server with password checks on (MXO_DEV_ANY_PASSWORD=0) and the
/// sign-up API, plus two accounts for the real client: one created through POST /api/signup, and one old-style row
/// (passwordmd5 only, the shared stock key) that migrates on its first good login. Both passwords are generated
/// throwaways written to credentials.json in the fixture folder, never printed. docs/ACCOUNTS.md has the design.
///
///   NativeQa &lt;repository&gt; [fixture-directory] [commit] account-signup
/// Stop it by creating &lt;fixture-directory&gt;/stop.
/// </summary>
public static class AccountSignupFixture
{
    public const string Mode = "account-signup";
    public const string SignupUrl = "http://127.0.0.1:11080";
    public const string NewCharacter = "SignupQA";
    public const string LegacyAccount = "QA_Legacy";
    public const string LegacyCharacter = "LegacyQA";
    private const uint NewCharId = 30311, LegacyCharId = 30312, LegacyUserId = 990312;
    private const string Handoff = "C:/Users/russe/AppData/Local/Temp/mxo-claude-codex-handoff.txt";

    public static Dictionary<string, string> Environment(NativeQaEndpoint endpoint)
    {
        var environment = NativeQaFixturePlan.EnvironmentFor("", endpoint);
        environment["MXO_DEV_ANY_PASSWORD"] = "0";
        environment["MXO_DEV_COMMANDS"] = "0";
        environment["MXO_SIGNUP_HTTP"] = "1";
        environment["MXO_SIGNUP_URL"] = SignupUrl;
        return environment;
    }

    public static async Task RunAsync(string root, string qaRoot, string commit, NativeQaEndpoint endpoint, string configuration)
    {
        if (!endpoint.IsPrimary) throw new ArgumentException("account-signup uses the primary loopback endpoint and the real client's ports.");
        using FileStream clientLock = new("C:/Users/russe/Developer/mxo-client.lock", FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, 4096, FileOptions.DeleteOnClose);
        endpoint.RequireAvailable();
        Directory.CreateDirectory(qaRoot);
        string rootLockPath = Path.Combine(qaRoot, ".nativeqa.lock");
        using FileStream rootLock = new(rootLockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        await clientLock.WriteAsync(Encoding.UTF8.GetBytes($"OpusD native account-signup QA {System.Environment.ProcessId}, {commit}"));
        await clientLock.FlushAsync();
        string statePath = Path.Combine(qaRoot, "state.json");
        string credentialsPath = Path.Combine(qaRoot, "credentials.json");
        File.Delete(credentialsPath);
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new { Status = "Preparing", Mode, BuildCommit = commit }));
        await AppendHandoffAsync($"Took mxo-client.lock for native account-signup QA, build {commit}. Disposable database only; no reality_hd rows are touched.");

        using ThrowawayDatabase database = await ThrowawayDatabase.CreateAsync(root, "native_signup");
        ServerInstance? server = null;
        try
        {
            // Old-style account first, so its row looks exactly like the stock dump's (MD5 + shared key).
            string legacyPassword = Scenarios.ThrowawayPassword();
            await Scenarios.AddLegacyAccountAsync(database, LegacyUserId, LegacyAccount, legacyPassword);
            await Scenarios.AddCharacterAsync(database, LegacyUserId, LegacyCharId, LegacyCharacter, 23828, 495, 13984);

            // Same snapshot rule as the other native modes: a rebuild mid-QA can't change the running server.
            string sourceBin = Path.Combine(root, "hds", "bin", configuration, "net8.0");
            string snapshot = Path.Combine(qaRoot, "snapshot");
            string targetBin = Path.Combine(snapshot, "hds", "bin", configuration, "net8.0");
            foreach (string file in Directory.EnumerateFiles(sourceBin, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(targetBin, Path.GetRelativePath(sourceBin, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
            string assemblySha = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(targetBin, "hds.dll"))));
            server = await ServerInstance.StartAsync(snapshot, database.Name, Environment(endpoint));
            endpoint.RequireReady(server);

            // The new account, through the real endpoint.
            using HttpClient http = new() { BaseAddress = new Uri(SignupUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
            string username = "QA" + RandomNumberGenerator.GetInt32(10000, 99999);
            string password = Scenarios.ThrowawayPassword();
            HttpResponseMessage created = await http.PostAsJsonAsync("api/signup", new { username, password });
            if (created.StatusCode != System.Net.HttpStatusCode.Created)
                throw new InvalidOperationException("Sign-up failed: " + (int)created.StatusCode + " " + await created.Content.ReadAsStringAsync());
            uint userId = (uint)await database.LongAsync($"SELECT userId FROM users WHERE username='{username}'");
            await Scenarios.AddCharacterAsync(database, userId, NewCharId, NewCharacter, 23828, 495, 13984);

            // Prove the new account's credentials at auth before handing over (auth only: no world session to clean up).
            using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20)))
            {
                AuthTicket ticket = await AuthLogin.LoginAsync(server.ConfiguredAddress.Host, server.ConfiguredAddress.AuthPort, username, password, timeout.Token);
                if (ticket.UserId != userId || ticket.Characters.All(c => c.Handle != NewCharacter))
                    throw new InvalidOperationException("Headless auth check of the new account didn't return its character.");
            }

            await File.WriteAllTextAsync(credentialsPath, JsonSerializer.Serialize(new
            {
                Note = "Throwaway test accounts in the disposable database " + database.Name + ". Never reused.",
                SignupUrl,
                NewAccount = new { Username = username, Password = password, Character = NewCharacter, UserId = userId },
                LegacyAccount = new { Username = LegacyAccount, Password = legacyPassword, Character = LegacyCharacter, UserId = LegacyUserId },
            }, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new
            {
                Status = "Ready", Mode, BuildCommit = commit, Database = database.Name, Directory = server.WorkingDirectory,
                Server = server.ConfiguredAddress, ServerPid = server.ProcessId, SnapshotAssemblySha256 = assemblySha,
                Credentials = credentialsPath, SignupUrl,
            }, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine("Native account-signup QA ready. Credentials: " + credentialsPath + " (not printed).");
            foreach (string step in Steps(credentialsPath)) Console.WriteLine("READY " + step);

            bool legacyMigrated = false, newLoggedIn = false;
            DateTime deadline = DateTime.UtcNow.AddHours(4);
            while (!File.Exists(Path.Combine(qaRoot, "stop")) && DateTime.UtcNow < deadline && !server.HasExited)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                if (!legacyMigrated && await database.LongAsync(
                        $"SELECT passwordmd5 IS NULL AND password_digest IS NOT NULL FROM users WHERE userId={LegacyUserId}") == 1)
                {
                    legacyMigrated = true;
                    Console.WriteLine($"READY {LegacyAccount} migrated: passwordmd5 cleared, password_digest written. Relog it to check the new digest.");
                }
                if (!newLoggedIn && await database.LongAsync($"SELECT is_online FROM characters WHERE charId={NewCharId}") == 1)
                {
                    newLoggedIn = true;
                    Console.WriteLine($"READY {NewCharacter} is in the world.");
                }
                foreach (string refusal in new[] { "Login refused: too many failed attempts", "Margin: refused", "World: refused" })
                    foreach (string line in server.OutputLines(refusal).Take(1))
                        if (seen.Add(line)) Console.WriteLine("SERVER " + line.TrimEnd('\r'));
            }
            await File.WriteAllTextAsync(Path.Combine(qaRoot, "result.json"), JsonSerializer.Serialize(new
            {
                LegacyMigrated = legacyMigrated, NewAccountEnteredWorld = newLoggedIn,
                LegacyRow = await database.ScalarAsync($"SELECT CONCAT('md5=',passwordmd5 IS NOT NULL,',digest=',password_digest IS NOT NULL) FROM users WHERE userId={LegacyUserId}"),
                At = DateTime.UtcNow,
            }));
        }
        finally
        {
            server?.Dispose();
            await File.WriteAllTextAsync(Path.Combine(qaRoot, "stopped.json"), JsonSerializer.Serialize(new { Database = database.Name, At = DateTime.UtcNow }));
            await AppendHandoffAsync("Native account-signup server stopped; its disposable database is dropped. Releasing mxo-client.lock.");
        }
    }

    private static readonly HashSet<string> seen = new(StringComparer.Ordinal);

    public static IEnumerable<string> Steps(string credentialsPath) => new[]
    {
        $"1. Open {credentialsPath}. NewAccount was created through POST {SignupUrl}/api/signup; LegacyAccount is an old passwordmd5 row with the shared stock key.",
        "2. Launcher: log in as NewAccount with its password (real password check: MXO_DEV_ANY_PASSWORD is off). Expect the character list with " + NewCharacter + ", select it, enter the world, move a little.",
        "3. Exit to the launcher (or quit), wait ~35 s, log in as NewAccount again: same character, enters the world (relog).",
        "4. Log in as NewAccount with a wrong password once: expect a refusal message (code 0x11000002, username/password mismatch); note the client's text. Then the right password still works.",
        $"5. Log in as {LegacyAccount} with its password, select {LegacyCharacter}, enter the world. The fixture prints 'READY {LegacyAccount} migrated' once the row is rewritten.",
        $"6. Relog {LegacyAccount}: works against the new digest.",
        "Optional: 5 wrong passwords in a row on one account, then the right one: expect the lockout refusal (0x1100000D) for 30 s; note the client's text.",
        "Stop: create the file 'stop' in the fixture folder.",
    };

    private static async Task AppendHandoffAsync(string message)
    {
        string line = $"\nUpdate {DateTime.Now:HH:mm} ET (OpusD NativeQa): {message}\n";
        for (int attempt = 0; ; attempt++)
        {
            try { await File.AppendAllTextAsync(Handoff, line); return; }
            catch (IOException) when (attempt < 4) { await Task.Delay(50); }
        }
    }
}
