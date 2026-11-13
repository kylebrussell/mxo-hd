using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HeadlessClient;

/// <summary>
/// A database for one run, in the mxo-hd-mysql container, made from SQL/reality_hd.sql. Same rules as
/// scripts/throwaway-db.sh. Everything goes through `docker exec`, and passwords stay in the container's environment.
/// </summary>
public sealed class ThrowawayDatabase : IDisposable
{
    public const string Prefix = "mxotest_";
    private const string Container = "mxo-hd-mysql";

    public string Name { get; }

    private ThrowawayDatabase(string name)
    {
        Name = name;
    }

    public static async Task<ThrowawayDatabase> CreateAsync(string repositoryRoot, string label)
    {
        string name = Prefix + label + "_" + Guid.NewGuid().ToString("N")[..10];
        ThrowawayDatabase database = new(name);
        await RunInContainerAsync(
            "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot -e \"CREATE DATABASE \\`$1\\` CHARACTER SET latin1; " +
            "GRANT ALL PRIVILEGES ON \\`mxotest\\\\_%\\`.* TO \\`$MYSQL_USER\\`@\\`%\\`;\"", name, null);
        string schema = await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "SQL", "reality_hd.sql"));
        await RunInContainerAsync("MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"", name, schema);
        return database;
    }

    /// <summary>Runs SQL as the app user, in this database. Returns tab-separated rows without a header.</summary>
    public Task<string> ExecuteAsync(string sql)
    {
        return RunInContainerAsync("MYSQL_PWD=\"$MYSQL_PASSWORD\" exec mysql -u\"$MYSQL_USER\" -N -B \"$1\"", Name, sql);
    }

    /// <summary>Fixed failure DDL only inside this harness-owned mxotest database; never changes server security settings.</summary>
    public Task<string> InstallMissionRollbackTriggerAsync(bool completing, bool accepting = false)
    {
        string condition=accepting?"OLD.payload IS NULL AND NEW.payload IS NOT NULL":completing?"NEW.payload IS NULL AND OLD.payload IS NOT NULL":
            "NEW.payload IS NOT NULL AND JSON_CONTAINS(JSON_EXTRACT(NEW.payload,'$.Done'),'1') AND NOT JSON_CONTAINS(JSON_EXTRACT(OLD.payload,'$.Done'),'1')";
        string sql="DELIMITER $$\nCREATE TRIGGER mission_test_rollback BEFORE UPDATE ON char_active_missions FOR EACH ROW BEGIN IF "+condition+
            " THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='M1 deliberate precommit rollback'; END IF; END$$\nDELIMITER ;\n";
        return RunInContainerAsync("MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,sql);
    }
    /// <summary>Fixed failure point only in this harness-owned database, never a production setting.</summary>
    public Task<string> InstallAbilityRollbackTriggerAsync() =>
        RunInContainerAsync("MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,
            "DELIMITER $$\nCREATE TRIGGER ability_test_rollback BEFORE UPDATE ON char_abilities FOR EACH ROW BEGIN IF NEW.slot=502 AND NEW.is_loaded=1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='deliberate ability rollback'; END IF; END$$\nDELIMITER ;\n");
    public Task<string> RemoveAbilityRollbackTriggerAsync() =>
        RunInContainerAsync("MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,"DROP TRIGGER ability_test_rollback");

    public Task<string> RemoveMissionRollbackTriggerAsync() =>
        RunInContainerAsync("MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,"DROP TRIGGER mission_test_rollback");

    //Fixed late-mutation failure only in this harness-owned database; never changes server settings.
    public Task<string> InstallTrainingChargeFailureAsync(uint character) => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,
        $"DELIMITER $$\nCREATE TRIGGER training_test_charge_failure BEFORE UPDATE ON characters FOR EACH ROW BEGIN IF OLD.charId={character} AND NEW.cash<OLD.cash THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Training deliberate late charge failure'; END IF; END$$\nDELIMITER ;\n");
    public Task<string> RemoveTrainingChargeFailureAsync() => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,"DROP TRIGGER IF EXISTS training_test_charge_failure");
    public Task<string> InstallPrisonMoveFailureAsync(uint character) => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,
        "DELIMITER $$\nCREATE TRIGGER prison_move_rollback BEFORE UPDATE ON inventory FOR EACH ROW BEGIN IF NEW.charId="+character+
        " AND NEW.slot=106 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Prison deliberate precommit rollback'; END IF; END$$\nDELIMITER ;\n");
    public Task<string> RemovePrisonMoveFailureAsync() => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,"DROP TRIGGER prison_move_rollback");

    //Fixed late second-write failure only inside this harness-owned database; no settings/schema migration.
    public Task<string> InstallAttributeFailureAsync(uint character) => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,
        $"DELIMITER $$\nCREATE TRIGGER attribute_test_second_write BEFORE UPDATE ON char_abilities FOR EACH ROW BEGIN IF OLD.char_id={character} AND OLD.slot=601 AND NEW.level<>OLD.level THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Attribute deliberate second-write failure'; END IF; END$$\nDELIMITER ;\n");
    public Task<string> RemoveAttributeFailureAsync() => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,"DROP TRIGGER IF EXISTS attribute_test_second_write");

    public Task<string> InstallCreationFailureAsync(uint character) => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,
        $"DELIMITER $$\nCREATE TRIGGER creation_test_late_write BEFORE INSERT ON char_abilities FOR EACH ROW BEGIN IF NEW.char_id={character} AND NEW.slot=604 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Creation deliberate final attribute failure'; END IF; END$$\nDELIMITER ;\n");
    public Task<string> RemoveCreationFailureAsync() => RunInContainerAsync(
        "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot \"$1\"",Name,"DROP TRIGGER IF EXISTS creation_test_late_write");

    public async Task<string> ScalarAsync(string sql) => (await ExecuteAsync(sql)).Trim();

    public async Task<long> LongAsync(string sql) => long.Parse(await ScalarAsync(sql), CultureInfo.InvariantCulture);

    private static async Task<string> RunInContainerAsync(string script, string name, string? input)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal) || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            throw new ArgumentException("Refusing database name " + name);
        }

        ProcessStartInfo start = new("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new[] { "exec", "-i", Container, "sh", "-c", script, "sh", name })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Couldn't start docker");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (input != null)
        {
            await process.StandardInput.WriteAsync(input);
        }
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"mysql failed for {name}: {await error}");
        }
        return await output;
    }

    public void Dispose()
    {
        RunInContainerAsync("MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot -e \"DROP DATABASE IF EXISTS \\`$1\\`;\"", Name, null)
            .GetAwaiter().GetResult();
    }

    /// <summary>Drops databases left by runs that were killed.</summary>
    public static async Task DropLeftoversAsync()
    {
        const string list = "MYSQL_PWD=\"$MYSQL_ROOT_PASSWORD\" exec mysql -uroot -N -B -e \"SHOW DATABASES LIKE 'mxotest\\\\_run\\\\_%';\"";
        string names = await RunInContainerAsync(list, Prefix + "list", null);
        foreach (string name in names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            new ThrowawayDatabase(name).Dispose();
        }
    }
}

/// <summary>A server process with its own ports, working directory and database.</summary>
public sealed class ServerInstance : IDisposable
{
    private const string Host = "127.0.0.1";
    private readonly Process process;
    private readonly StringBuilder output = new();
    private readonly TaskCompletionSource running = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ServerAddress Address { get; }
    public string WorkingDirectory { get; }

    private ServerInstance(Process process, ServerAddress address, string workingDirectory)
    {
        this.process = process;
        Address = address;
        WorkingDirectory = workingDirectory;
    }

    public static async Task<ServerInstance> StartAsync(string repositoryRoot, string databaseName,
        IReadOnlyDictionary<string, string>? extraEnvironment = null, string? missionFixture = null)
    {
        string serverDll = FindServerDll(repositoryRoot);
        string workingDirectory = Path.Combine(Path.GetTempPath(), "mxo-headless", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(workingDirectory);
        if (missionFixture != null)
        {
            // A fixture replaces only this disposable server's mission catalog. Production data stays untouched.
            string missions = Path.Combine(workingDirectory, "data", "missions");
            Directory.CreateDirectory(missions);
            File.Copy(Path.Combine(repositoryRoot, "tools", "HeadlessClient", "Fixtures", missionFixture),
                Path.Combine(missions, missionFixture));
        }

        if (extraEnvironment != null && extraEnvironment.TryGetValue("HEADLESS_HARDLINE_UPLOAD_REQUIRED",out string upload))
        {
            // Only an isolated ordinary file in this new GUID directory, never the source/output config.
            string target=Path.GetFullPath(Path.Combine(workingDirectory,"WorldConfig.xml"));
            if(!target.StartsWith(Path.GetFullPath(workingDirectory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || File.Exists(target))
                throw new InvalidOperationException("Unsafe fixture config target");
            string source=Path.Combine(repositoryRoot,"WorldConfig.xml.dist");
            File.Copy(source,target);
            var xml=new System.Xml.XmlDocument();xml.Load(target);
            var node=xml.SelectSingleNode("//HardlineUploadRequired");
            if(node==null){node=xml.CreateElement("HardlineUploadRequired");xml.DocumentElement!.AppendChild(node);}
            node.InnerText=upload=="1"?"true":"false";xml.Save(target);
        }

        ServerAddress address = new(Host, FreeTcpPort(), FreeTcpPort(), FreeUdpPort());
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(serverDll);
        start.Environment["MXO_BIND_ADDRESS"] = Host;
        start.Environment["MXO_AUTH_PORT"] = address.AuthPort.ToString();
        start.Environment["MXO_MARGIN_PORT"] = address.MarginPort.ToString();
        start.Environment["MXO_WORLD_PORT"] = address.WorldPort.ToString();
        start.Environment["MXO_CONSOLE_PORT"] = FreeTcpPort().ToString();
        start.Environment["MXO_DB_NAME"] = databaseName;
        start.Environment["MXO_DEV_ANY_PASSWORD"] = "1";
        start.Environment["MXO_DEV_COMMANDS"] = "1";
        start.Environment["MXO_DEBUG_LOG"] = "1";
        if (extraEnvironment != null)
        {
            foreach ((string key, string value) in extraEnvironment)
            {
                if(key!="HEADLESS_HARDLINE_UPLOAD_REQUIRED")start.Environment[key] = value;
            }
        }

        Process process = new() { StartInfo = start };
        ServerInstance server = new(process, address, workingDirectory);
        process.OutputDataReceived += (_, line) => server.Capture(line.Data);
        process.ErrorDataReceived += (_, line) => server.Capture(line.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await server.running.Task.WaitAsync(TimeSpan.FromSeconds(120));
        }
        catch (Exception ex)
        {
            string tail = server.OutputTail(20);
            server.Dispose();
            throw new InvalidOperationException($"Server didn't start ({ex.Message}). Output:\n{tail}");
        }
        return server;
    }

    private void Capture(string? line)
    {
        if (line == null)
        {
            return;
        }
        lock (output)
        {
            output.AppendLine(line);
        }
        if (line.Contains("Im'running"))
        {
            running.TrySetResult();
        }
        else if (line.Contains("Unhandled exception") || line.Contains("Health checks not passed"))
        {
            running.TrySetException(new InvalidOperationException(line));
        }
    }

    public string SourceAssembly => process.StartInfo.ArgumentList[0];
    public int ProcessId => process.Id; // Read-only metrics for this harness-owned process.
    public bool HasExited => process.HasExited;

    public string OutputTail(int lines)
    {
        lock (output)
        {
            string[] all = output.ToString().Split('\n');
            return string.Join('\n', all.Skip(Math.Max(0, all.Length - lines)));
        }
    }

    /// <summary>Every line of the server's output holding this text; the server's own account of what it did.</summary>
    public List<string> OutputLines(string text)
    {
        lock (output)
        {
            return output.ToString().Split('\n').Where(l => l.Contains(text)).ToList();
        }
    }

    public string FullOutput()
    {
        lock (output)
        {
            return output.ToString();
        }
    }

    /// <summary>The server's debug log so far (MXO_DEBUG_LOG), for asserting what the handlers decided.</summary>
    public string DebugLog()
    {
        string path = Path.Combine(WorkingDirectory, "DebugLog.txt");
        if (!File.Exists(path))
        {
            return "";
        }
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static string FindServerDll(string repositoryRoot)
    {
        string binRoot = Path.Combine(repositoryRoot, "hds", "bin");
        string? newest = Directory.Exists(binRoot)
            ? Directory.EnumerateFiles(binRoot, "hds.dll", SearchOption.AllDirectories)
                .Where(path => File.Exists(Path.ChangeExtension(path, "runtimeconfig.json")))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        return newest ?? throw new FileNotFoundException("Build the server first: no hds.dll under " + binRoot);
    }

    // Ask the OS for a free port and let it go. Another run can take it in between, but then the server fails its bind
    // and the scenario fails loudly. Keeping a short global list stops two servers in this run getting the same one.
    private static readonly HashSet<int> Handed = new();

    private static int FreeTcpPort() => Unused(() =>
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    });

    private static int FreeUdpPort() => Unused(() =>
    {
        using UdpClient udp = new(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    });

    private static int Unused(Func<int> pick)
    {
        lock (Handed)
        {
            while (true)
            {
                int port = pick();
                if (Handed.Add(port))
                {
                    return port;
                }
            }
        }
    }

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        process.Dispose();
    }
}
