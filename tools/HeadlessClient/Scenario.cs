using System.Globalization;

namespace HeadlessClient;

/// <summary>What a scenario wants a test character to start with.</summary>
public sealed record CharacterSeed(string Handle)
{
    public double X { get; init; } = 17020;
    public double Y { get; init; } = 495;
    public double Z { get; init; } = 2693;
    /// <summary>Persisted protocol yaw interval; double permits legacy malformed-row login regressions.</summary>
    public double Rotation { get; init; }
    public int DistrictId { get; init; } = 1;
    public string District { get; init; } = "slums";
    public int Info { get; init; } = 1000;
    public int Level { get; init; } = 10;
    public int Experience { get; init; }
    public int Health { get; init; } = 500;
    public int MaxHealth { get; init; } = 500;
    public int InnerStrength { get; init; } = 200;
    public int MaxInnerStrength { get; init; } = 200;
    public int CrewId { get; init; }

    /// <summary>OrganizationID (the alignment column): 0 none, 1 Zion, 2 Machines, 3 Merovingian.</summary>
    public int Organization { get; init; }

    /// <summary>Bag or worn items: slot, goid, and a stack count where it matters.</summary>
    public IReadOnlyList<(int Slot, uint Goid, int Count)> Items { get; init; } = Array.Empty<(int, uint, int)>();
    public IReadOnlyList<(ushort Ability, ushort Level)>? OwnedLoadedAbilities { get; init; }
    /// <summary>Optional earlier receipts installed before Margin login, never active progress.</summary>
    public IReadOnlyList<int>? CompletedMissions { get; init; }
    /// <summary>Optional exact attribute rows installed before Margin/initial UDP; null preserves the legacy seed.</summary>
    public IReadOnlyDictionary<byte,ushort>? AttributeValues { get; init; }
}

/// <summary>One scenario's world: its own server and throwaway database. Players made here go away with it.</summary>
public sealed class ScenarioContext : IAsyncDisposable
{
    // The accounts in SQL/reality_hd.sql all share one key. Copying it from a real row saves generating RSA keys.
    private const int TemplateUser = 28;
    private const int TemplateCharacter = 2;

    private readonly List<HeadlessPlayer> players = new();
    private readonly ExpectedServerDiagnostics expectedServerDiagnostics = new();
    private int nextId = 1000;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint,string> attributeRowsBeforeLogin = new();
    public string AttributeRowsBeforeLogin(uint character) => attributeRowsBeforeLogin[character];

    public ThrowawayDatabase Database { get; }
    public ServerInstance Server { get; }

    public string RepositoryRoot { get; }

    private ScenarioContext(string repositoryRoot, ThrowawayDatabase database, ServerInstance server)
    {
        RepositoryRoot = repositoryRoot;
        Database = database;
        Server = server;
    }

    public static async Task<ScenarioContext> StartAsync(string repositoryRoot, string label,
        IReadOnlyDictionary<string, string>? environment = null, string? missionFixture = null)
    {
        ThrowawayDatabase database = await ThrowawayDatabase.CreateAsync(repositoryRoot, label);
        try
        {
            ServerInstance server = await ServerInstance.StartAsync(repositoryRoot, database.Name, environment, missionFixture);
            return new ScenarioContext(repositoryRoot, database, server);
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    /// <summary>Only an authenticated account fixture; normal Margin requests must create its character and RSI.</summary>
    public async Task<string> AddCreationAccountAsync()
    {
        int id = Interlocked.Increment(ref nextId);
        string username = "creation" + id;
        await Database.ExecuteAsync($@"INSERT INTO users (userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
SELECT {id},'{username}',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId={TemplateUser}");
        return username;
    }

    /// <summary>Adds an account and character to the database, then logs in with it (not yet in the world).</summary>
    public async Task<HeadlessPlayer> AddPlayerAsync(CharacterSeed seed)
    {
        int id = Interlocked.Increment(ref nextId);
        string username = "user" + id;
        string sql = FormattableString.Invariant($@"
INSERT INTO users (userId, username, publicExponent, publicModulus, privateExponent, account_status, passwordmd5)
  SELECT {id}, '{username}', publicExponent, publicModulus, privateExponent, 0, passwordmd5 FROM users WHERE userId = {TemplateUser};
INSERT INTO characters (charId, userId, worldId, status, handle, firstName, lastName, background, x, y, z, rotation,
    healthC, healthM, innerStrC, innerStrM, level, profession, alignment, pvpflag, conquest_points, exp, cash,
    district, districtId, factionId, crewId, is_deleted, is_online, created)
  VALUES ({id}, {id}, 1, 0, '{seed.Handle}', 'First', 'Last', '', {seed.X}, {seed.Y}, {seed.Z}, {seed.Rotation},
    {seed.Health}, {seed.MaxHealth}, {seed.InnerStrength}, {seed.MaxInnerStrength}, {seed.Level}, 2, {seed.Organization}, 0, 0, {seed.Experience}, {seed.Info},
    '{seed.District}', {seed.DistrictId}, 0, {seed.CrewId}, 0, 0, NOW());
CREATE TEMPORARY TABLE rsi_copy AS SELECT * FROM rsivalues WHERE charid = {TemplateCharacter};
UPDATE rsi_copy SET charid = {id};
INSERT INTO rsivalues SELECT * FROM rsi_copy;
DROP TEMPORARY TABLE rsi_copy;
INSERT INTO char_abilities (char_id, slot, ability_id, level, is_loaded, ability_name)
  SELECT {id}, slot, ability_id, level, is_loaded, ability_name FROM char_abilities WHERE char_id = {TemplateCharacter};
");
        if (seed.OwnedLoadedAbilities != null)
        {
            sql += $"DELETE FROM char_abilities WHERE char_id={id};\n";
            foreach(var row in seed.OwnedLoadedAbilities.Select((a,index)=>(a,index)))
                sql += $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES({id},{400+row.index},{0x80000000L|((long)row.a.Ability<<10)},{row.a.Level},1,'QA owned before login');\n";
        }
        foreach ((int slot, uint goid, int count) in seed.Items)
        {
            sql += FormattableString.Invariant($"INSERT INTO inventory (charId, goid, slot, count, purity) VALUES ({id}, {goid}, {slot}, {count}, 0);\n");
        }
        if(seed.AttributeValues!=null)
        {
            if(seed.AttributeValues.Keys.Any(a=>!hds.AttributeAllocation.Known(a)))throw new ArgumentException("Unknown fixture attribute");
            sql+=$"DELETE FROM char_abilities WHERE char_id={id} AND slot IN (598,599,601,602,604);\n";
            foreach(var value in seed.AttributeValues.OrderBy(v=>v.Key))
                sql+=$"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({id},{hds.CharacterAttributes.SlotOf(value.Key)},{hds.CharacterAttributes.RowValueOf(value.Key)},{value.Value},0);\n";
        }
        if (seed.CompletedMissions != null)
        {
            if (seed.CompletedMissions.Any(id=>id<=0)) throw new ArgumentException("Invalid prior mission receipt");
            foreach (int mission in seed.CompletedMissions.Distinct())
                sql += $"INSERT IGNORE INTO char_missions(charId,missionId) VALUES({id},{mission});\n";
        }
        await Database.ExecuteAsync(sql);

        if(seed.AttributeValues!=null)
            attributeRowsBeforeLogin[(uint)id]=await Database.ScalarAsync($"SELECT id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={id} AND slot IN (598,599,601,602,604) ORDER BY slot,id");

        HeadlessPlayer player = await HeadlessPlayer.LoginAsync(Server.Address, username, seed.Handle);
        lock (players)
        {
            players.Add(player);
        }
        return player;
    }

    /// <summary>Adds several players and brings them all into the world.</summary>
    public async Task<HeadlessPlayer[]> AddPlayersInWorldAsync(params CharacterSeed[] seeds)
    {
        List<HeadlessPlayer> added = new();
        foreach (CharacterSeed seed in seeds)
        {
            HeadlessPlayer player = await AddPlayerAsync(seed);
            await player.EnterWorldAsync();
            added.Add(player);
        }
        return added.ToArray();
    }

    /// <summary>The problems a real client would have run into: corrupt packets, RPC counter gaps, unreadable records.</summary>
    public IEnumerable<string> ClientProblems() => players.SelectMany(p => p.World.Problems.Select(problem => p.Handle + ": " + problem));

    /// <summary>What the server's own output says went wrong: handlers that threw, a visibility pass that failed.</summary>
    public IEnumerable<string> ServerProblems() =>
        expectedServerDiagnostics.Unacknowledged(new[] { "Packet handling failed", "Visibility pass failed", "Unhandled exception", "[MISSION] Tick failed" }
            .SelectMany(Server.OutputLines)
            .Select(line => line.TrimEnd('\r')))
            .Select(line => line.Length > 300 ? line[..300] : line);

    /// <summary>A scenario must first verify the full expected failure. No wildcard/prefix suppression is supported.</summary>
    public void AcknowledgeExpectedServerDiagnostic(string exactLine, int count = 1)
    {
        int observed = Server.FullOutput().Split('\n').Count(line => line.TrimEnd('\r') == exactLine);
        if (count <= 0 || observed < expectedServerDiagnostics.Count(exactLine) + count)
            throw new ArgumentException("The exact diagnostic/count has not occurred on this server.", nameof(exactLine));
        expectedServerDiagnostics.Acknowledge(exactLine, count);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (HeadlessPlayer player in players)
        {
            player.Dispose();
        }
        Server.Dispose();
        await Task.Run(Database.Dispose);
    }
}

public sealed class ScenarioFailure : Exception
{
    public ScenarioFailure(string message) : base(message)
    {
    }
}

public static class Check
{
    public static void That(bool condition, string description)
    {
        if (!condition)
        {
            throw new ScenarioFailure(description);
        }
    }

    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new ScenarioFailure($"{what}: expected {expected}, got {actual}");
        }
    }

    public static string Hex(long value) => value.ToString("x", CultureInfo.InvariantCulture);
}

public sealed record Scenario(string Name, Func<ScenarioContext, Task> Run,
    IReadOnlyDictionary<string, string>? Environment = null, string? MissionFixture = null, TimeSpan? Timeout = null);
