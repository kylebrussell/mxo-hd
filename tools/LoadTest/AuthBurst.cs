using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
using hds.accounts;

public sealed record AuthBurstOptions(string Root, string? Output, int Clients, int TimeoutSeconds, bool DevAnyPassword, int? Workers, int? MaxQueue, bool AllowBusy)
{
    public static AuthBurstOptions Parse(string[] args)
    {
        string root = Directory.GetCurrentDirectory();
        string? output = null;
        int clients = 200;
        int timeoutSeconds = 180;
        bool devAnyPassword = false;
        int? workers = null;
        int? maxQueue = null;
        bool allowBusy = false;

        for (int i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException("Missing value for " + args[i]);
            switch (args[i])
            {
                case "--auth-burst": break;
                case "--root": root = Path.GetFullPath(Value()); break;
                case "--output": output = Path.GetFullPath(Value()); break;
                case "--clients": clients = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--timeout-seconds": timeoutSeconds = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--dev-any-password": devAnyPassword = true; break;
                case "--workers": workers = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--max-queue": maxQueue = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--allow-busy": allowBusy = true; break;
                default: throw new ArgumentException("Unknown auth-burst option: " + args[i]);
            }
        }

        if (clients < 1 || clients > 2000 || timeoutSeconds < 1 || timeoutSeconds > 600)
            throw new ArgumentException("Auth-burst bounds exceeded.");
        return new(root, output, clients, timeoutSeconds, devAnyPassword, workers, maxQueue, allowBusy);
    }
}

public static class AuthBurst
{
    private const string BurstPassword = "BurstPassword123!";
    private const string BusyStatusText = "status 1100000f";

    public static async Task<int> RunAsync(string[] args)
    {
        AuthBurstOptions o = AuthBurstOptions.Parse(args);
        if (!File.Exists(Path.Combine(o.Root, "SQL", "reality_hd.sql")))
            throw new ArgumentException("Repository SQL fixture not found at " + o.Root);

        var env = new Dictionary<string, string>
        {
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_AGGRO"] = "0",
            ["MXO_MOBS"] = "test",
            ["MXO_DEBUG_LOG"] = "0",
            ["MXO_PACKET_LOG"] = "0",
            ["MXO_RPC_LOG"] = "0",
            ["MXO_CLIENT_VIEW_LOG"] = "0",
            ["MXO_CHAT_DEBUG"] = "0",
            ["MXO_DROP_RPC_PACKET"] = "0",
            ["MXO_DEV_ANY_PASSWORD"] = o.DevAnyPassword ? "1" : "0",
        };
        if (o.Workers.HasValue)
            env["MXO_AUTH_WORKERS"] = o.Workers.Value.ToString(CultureInfo.InvariantCulture);
        if (o.MaxQueue.HasValue)
            env["MXO_AUTH_MAX_QUEUE"] = o.MaxQueue.Value.ToString(CultureInfo.InvariantCulture);

        // Compute one valid 600,000-iteration PBKDF2-SHA256 digest so every server-side login runs full verification.
        string digest = PasswordHasher.Hash(Encoding.ASCII.GetBytes(BurstPassword));
        string escapedDigest = digest.Replace("'", "''");

        await using var context = await ScenarioContext.StartAsync(o.Root, "authburst" + o.Clients, env);

        // Seed all accounts and characters in one SQL batch so the burst hits AuthServer simultaneously.
        var sql = new StringBuilder();
        sql.AppendLine("SET @tpl_e = (SELECT publicExponent FROM users WHERE userId = 28);");
        sql.AppendLine("SET @tpl_n = (SELECT publicModulus FROM users WHERE userId = 28);");
        sql.AppendLine("SET @tpl_d = (SELECT privateExponent FROM users WHERE userId = 28);");
        sql.AppendLine("INSERT INTO users (userId, username, publicExponent, publicModulus, privateExponent, account_status, passwordmd5, password_digest) VALUES");
        for (int i = 0; i < o.Clients; i++)
        {
            int id = 10000 + i;
            string username = $"burst{i:D4}";
            sql.Append(FormattableString.Invariant($"({id}, '{username}', @tpl_e, @tpl_n, @tpl_d, 0, NULL, '{escapedDigest}')"));
            sql.AppendLine(i + 1 < o.Clients ? "," : ";");
        }
        sql.AppendLine("INSERT INTO characters (charId, userId, worldId, status, handle, firstName, lastName, background, x, y, z, rotation, healthC, healthM, innerStrC, innerStrM, level, profession, alignment, pvpflag, conquest_points, exp, cash, district, districtId, factionId, crewId, is_deleted, is_online, created) VALUES");
        for (int i = 0; i < o.Clients; i++)
        {
            int id = 10000 + i;
            string handle = $"Burst{i:D4}";
            sql.Append(FormattableString.Invariant($"({id}, {id}, 1, 0, '{handle}', 'First', 'Last', '', 17020, 495, 2693, 0, 500, 500, 200, 200, 10, 2, 0, 0, 0, 0, 1000, 'slums', 1, 0, 0, 0, 0, NOW())"));
            sql.AppendLine(i + 1 < o.Clients ? "," : ";");
        }
        await context.Database.ExecuteAsync(sql.ToString());

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latenciesMs = new List<double>(o.Clients);
        int busyRefusals = 0;
        var failures = new List<object>();
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(o.TimeoutSeconds));

        Task[] tasks = Enumerable.Range(0, o.Clients).Select(i => Task.Run(async () =>
        {
            string username = $"burst{i:D4}";
            string expectedHandle = $"Burst{i:D4}";
            await gate.Task;
            var sw = Stopwatch.StartNew();
            try
            {
                AuthTicket ticket = await AuthLogin.LoginAsync(
                    context.Server.Address.Host,
                    context.Server.Address.AuthPort,
                    username,
                    BurstPassword,
                    cancel.Token);
                if (ticket.UserId != (uint)(10000 + i) || !ticket.Characters.Any(c => c.Handle == expectedHandle))
                    throw new InvalidDataException($"Ticket mismatch for {username}: uid={ticket.UserId} chars={ticket.Characters.Count}");
                sw.Stop();
                lock (latenciesMs) latenciesMs.Add(sw.Elapsed.TotalMilliseconds);
            }
            catch (InvalidOperationException ex) when (o.AllowBusy && ex.Message.Contains(BusyStatusText, StringComparison.OrdinalIgnoreCase))
            {
                sw.Stop();
                Interlocked.Increment(ref busyRefusals);
            }
            catch (Exception ex)
            {
                sw.Stop();
                lock (failures)
                {
                    failures.Add(new
                    {
                        ClientIndex = i,
                        Username = username,
                        ElapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
                        Error = ex.GetType().Name,
                        Message = ex.Message,
                        InnerError = ex.InnerException?.GetType().Name,
                        InnerMessage = ex.InnerException?.Message,
                    });
                }
            }
        })).ToArray();

        Stopwatch wall = Stopwatch.StartNew();
        gate.SetResult();
        await Task.WhenAll(tasks);
        wall.Stop();

        latenciesMs.Sort();
        double Percentile(double p)
        {
            if (latenciesMs.Count == 0) return 0;
            int idx = (int)Math.Ceiling(p / 100.0 * latenciesMs.Count) - 1;
            return latenciesMs[Math.Clamp(idx, 0, latenciesMs.Count - 1)];
        }

        double p50 = Math.Round(Percentile(50), 1);
        double p95 = Math.Round(Percentile(95), 1);
        double p99 = Math.Round(Percentile(99), 1);
        double max = latenciesMs.Count == 0 ? 0 : Math.Round(latenciesMs[^1], 1);
        double mean = latenciesMs.Count == 0 ? 0 : Math.Round(latenciesMs.Average(), 1);

        var failureCounts = failures
            .Cast<dynamic>()
            .GroupBy(f => (string)$"{f.Error}: {f.Message}")
            .OrderByDescending(g => g.Count())
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToArray();

        bool pass = (o.AllowBusy ? (latenciesMs.Count + busyRefusals == o.Clients) : (latenciesMs.Count == o.Clients))
            && failures.Count == 0
            && !context.Server.HasExited;

        var summary = new
        {
            Status = pass ? "PASS" : "FAIL",
            Clients = o.Clients,
            Succeeded = latenciesMs.Count,
            BusyRefusals = busyRefusals,
            Failed = failures.Count,
            DevAnyPassword = o.DevAnyPassword,
            WallSeconds = Math.Round(wall.Elapsed.TotalSeconds, 2),
            LoginMs = new { Mean = mean, P50 = p50, P95 = p95, P99 = p99, Max = max },
            ServerExited = context.Server.HasExited,
            FailureSummary = failureCounts,
            SampleFailures = failures.Take(10).ToArray(),
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(summary, jsonOptions);
        if (!string.IsNullOrEmpty(o.Output))
        {
            Directory.CreateDirectory(o.Output);
            await File.WriteAllTextAsync(Path.Combine(o.Output, "summary.json"), json);
            await File.WriteAllTextAsync(Path.Combine(o.Output, "server-output.log"), context.Server.FullOutput());
        }

        Console.WriteLine(json);
        Console.WriteLine($"Auth burst: {latenciesMs.Count}/{o.Clients} succeeded, {busyRefusals} busy, {failures.Count} failed in {wall.Elapsed.TotalSeconds:F2}s (p50={p50:F1}ms, p95={p95:F1}ms, max={max:F1}ms)");
        return pass ? 0 : 1;
    }
}
