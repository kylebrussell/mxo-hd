using System.Text;

namespace HeadlessClient;

/// <summary>Where one server listens.</summary>
public sealed record ServerAddress(string Host, int AuthPort, int MarginPort, int WorldPort);

/// <summary>
/// A scripted player. Log in, enter the world, send RPCs and chat, and wait for what the server sends.
/// Waits for records go forward through what has arrived, so a sequence of waits asserts order. Use a pattern of hex
/// bytes with ?? wildcards ("81 87 19 00 00 8e"), matched against the start of the record.
/// </summary>
public sealed class HeadlessPlayer : IDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly MarginSession margin;
    private int rpcCursor;

    private HeadlessPlayer(string handle, uint charId, MarginSession margin, WorldSession world)
    {
        Handle = handle;
        CharId = charId;
        this.margin = margin;
        World = world;
    }

    public string Handle { get; }
    public uint CharId { get; }
    public WorldSession World { get; }

    /// <summary>Character data from the margin (05 inventory, 07 known abilities...).</summary>
    public IReadOnlyDictionary<byte, byte[]> CharacterData => margin.CharacterData;

    /// <summary>Auth, margin, then the world session up to the point the real client would finish loading the map.</summary>
    public static async Task<HeadlessPlayer> LoginAsync(ServerAddress server, string username, string handle, string password = "x")
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        CancellationToken cancel = timeout.Token;

        AuthTicket ticket = await AuthLogin.LoginAsync(server.Host, server.AuthPort, username, password, cancel);
        CharacterEntry character = ticket.Characters.FirstOrDefault(c => c.Handle == handle)
            ?? throw new InvalidOperationException($"{username} has no character {handle}; has: " +
                                                   string.Join(", ", ticket.Characters.Select(c => c.Handle)));

        MarginSession margin = new();
        WorldSession? world = null;
        try
        {
            await margin.ConnectAsync(server.Host, server.MarginPort, ticket, cancel);
            await margin.LoadCharacterAsync(character.CharId, cancel);

            world = new WorldSession(server.Host, server.WorldPort);
            world.SendSessionRequest(character.CharId);
            await margin.WaitForUdpSessionAsync(cancel);
            return new HeadlessPlayer(handle, character.CharId, margin, world);
        }
        catch
        {
            world?.Dispose();
            margin.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Tells the server the world is loaded (RPC 05) and waits for the self spawn (a creation record for view 2).
    /// </summary>
    public async Task EnterWorldAsync()
    {
        SendRpc("05");
        await WaitUntilAsync(() => World.CreationSnapshot().Any(c => c.ViewId == 2), "the self spawn", TimeSpan.FromSeconds(15));
    }

    public void SendRpc(byte[] record) => World.SendRpcs(record);

    public void SendRpc(string hex) => World.SendRpcs(WireFormat.FromHex(hex));

    /// <summary>A chat line or ?command: `28 &lt;type&gt; ...`. Layout from ChatHandler.processChat (retail captures).</summary>
    public void SendChat(string text, byte chatType = ChatTypes.Area)
    {
        byte[] body = Encoding.ASCII.GetBytes(text);
        List<byte> record = new() { 0x28, chatType, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00 };
        record.AddRange(BitConverter.GetBytes((ushort)(body.Length + 1)));
        record.AddRange(body);
        record.Add(0);
        SendRpc(record.ToArray());
    }

    /// <summary>Waits for the next record matching the pattern, from where the last wait stopped.</summary>
    public async Task<RpcRecord> WaitForRpcAsync(string pattern, TimeSpan? timeout = null)
    {
        BytePattern parsed = BytePattern.Parse(pattern);
        return await WaitForRpcAsync(r => parsed.IsMatch(r.Bytes), pattern, timeout);
    }

    /// <summary>Waits for the next record the predicate accepts, from where the last wait stopped.</summary>
    public async Task<RpcRecord> WaitForRpcAsync(Func<RpcRecord, bool> match, string description, TimeSpan? timeout = null)
    {
        RpcRecord? found = null;
        await WaitUntilAsync(() =>
        {
            List<RpcRecord> log = World.RpcSnapshot();
            for (int i = rpcCursor; i < log.Count; i++)
            {
                if (match(log[i]))
                {
                    found = log[i];
                    rpcCursor = i + 1;
                    return true;
                }
            }
            return false;
        }, description, timeout ?? DefaultTimeout);
        return found!;
    }

    /// <summary>Skips everything that has arrived so far, so later waits only see what comes next.</summary>
    public void SkipReceived() => rpcCursor = World.RpcSnapshot().Count;

    /// <summary>All records so far that match, regardless of where the waits are.</summary>
    public List<RpcRecord> Received(string pattern)
    {
        BytePattern parsed = BytePattern.Parse(pattern);
        return World.RpcSnapshot().Where(r => parsed.IsMatch(r.Bytes)).ToList();
    }

    /// <summary>Waits for a system chat or other record containing this text.</summary>
    public Task<RpcRecord> WaitForTextAsync(string text, TimeSpan? timeout = null)
    {
        byte[] needle = Encoding.Latin1.GetBytes(text);
        return WaitForRpcAsync(r => BytePattern.IndexOf(r.Bytes, needle, 0) >= 0, "text \"" + text + "\"", timeout);
    }

    /// <summary>Waits for a creation record for an object with this name: another player or an NPC.</summary>
    public async Task<ViewCreation> WaitForViewNamedAsync(string name, TimeSpan? timeout = null)
    {
        ViewCreation? found = null;
        await WaitUntilAsync(() =>
        {
            found = World.CreationSnapshot().LastOrDefault(c => c.Name == name);
            return found != null;
        }, "a view of " + name, timeout ?? TimeSpan.FromSeconds(15));
        return found!;
    }

    /// <summary>Creation records for objects of this goid.</summary>
    public List<ViewCreation> ViewsOfGoid(ushort goid) => World.CreationSnapshot().Where(c => c.Goid == goid).ToList();

    /// <summary>
    /// Waits for a 03 list holding the view id (u16 LE) followed by these bytes. View updates have no lengths, so this
    /// is a search through the raw list, not a parse: use a pattern long enough not to match by accident.
    /// </summary>
    public async Task<byte[]> WaitForViewUpdateAsync(ushort viewId, string pattern, TimeSpan? timeout = null)
    {
        BytePattern parsed = BytePattern.Parse($"{viewId & 0xff:x2} {viewId >> 8:x2} {pattern}");
        byte[]? section = null;
        await WaitUntilAsync(() =>
        {
            section = World.ViewSnapshot().FirstOrDefault(parsed.Occurs);
            return section != null;
        }, $"view {viewId} update {pattern}", timeout ?? DefaultTimeout);
        return section!;
    }

    /// <summary>Waits on any condition over what has arrived.</summary>
    public async Task WaitUntilAsync(Func<bool> condition, string description, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            long seen = World.Version;
            if (condition())
            {
                return;
            }
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"{Handle}: timed out after {timeout.TotalSeconds}s waiting for {description}. " +
                                           Describe());
            }
            while (World.Version == seen && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
    }

    /// <summary>A summary of what arrived, for failure messages: the last few records.</summary>
    public string Describe()
    {
        List<RpcRecord> log = World.RpcSnapshot();
        List<string> tail = log.Skip(Math.Max(0, log.Count - 12)).Select(r => r.Hex.Length > 80 ? r.Hex[..80] + "..." : r.Hex).ToList();
        string views = string.Join(", ", World.CreationSnapshot().Select(c => $"{c.ViewId}={c.Goid}/{c.Name}"));
        return $"Received {log.Count} RPCs, {World.ViewSnapshot().Count} view lists, creations [{views}]. " +
               $"Problems: {string.Join("; ", World.Problems)}. Last records: {string.Join(" | ", tail)}";
    }

    public void Dispose()
    {
        World.Dispose();
        margin.Dispose();
    }
}

public static class ChatTypes
{
    public const byte Area = 0x10;
}

/// <summary>Hex bytes with ?? wildcards.</summary>
public sealed class BytePattern
{
    private readonly int?[] bytes;

    private BytePattern(int?[] bytes)
    {
        this.bytes = bytes;
    }

    public static BytePattern Parse(string pattern)
    {
        string compact = pattern.Replace(" ", "");
        if (compact.Length % 2 != 0)
        {
            throw new ArgumentException("Pattern needs whole bytes: " + pattern);
        }
        int?[] parsed = new int?[compact.Length / 2];
        for (int i = 0; i < parsed.Length; i++)
        {
            string pair = compact.Substring(i * 2, 2);
            parsed[i] = pair == "??" ? null : Convert.ToInt32(pair, 16);
        }
        return new BytePattern(parsed);
    }

    /// <summary>Matches the start of the data.</summary>
    public bool IsMatch(byte[] data) => MatchesAt(data, 0);

    /// <summary>Matches anywhere in the data.</summary>
    public bool Occurs(byte[] data)
    {
        for (int i = 0; i + bytes.Length <= data.Length; i++)
        {
            if (MatchesAt(data, i))
            {
                return true;
            }
        }
        return false;
    }

    private bool MatchesAt(byte[] data, int start)
    {
        if (start + bytes.Length > data.Length)
        {
            return false;
        }
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i].HasValue && bytes[i] != data[start + i])
            {
                return false;
            }
        }
        return true;
    }

    public static int IndexOf(byte[] data, byte[] needle, int start)
    {
        for (int i = start; i + needle.Length <= data.Length; i++)
        {
            if (data.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }
        return -1;
    }
}
