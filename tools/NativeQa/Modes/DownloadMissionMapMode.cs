using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class DownloadMissionMapMode : NativeQaMode
{
    private const ushort Map = UtilityAbilities.DownloadMissionMap;
    private const int Level = 10, X = 24800, Y = 495, Z = -3048;
    private static readonly Regex MapRoll = new(@"\[UTILITY\] Download Mission Map: access flags (\d+)");
    private const string PingSent = "[UTILITY] Download Mission Map: ping sent Safe 1";
    private const string PingCleared = "[UTILITY] Download Mission Map: ping cleared Safe 1";
    private const string MapFixture = "download-mission-map.xml";
    private static readonly BytePattern OpenMap = BytePattern.Parse("80 c5 05 00 03");

    public override string Name => "ability-parity-download-mission-map";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Map }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET alignment=3, repMero=100,level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y} WHERE charId=110" },
        DevCommands: true, loadAbilityAncestors: true, missionFixture: MapFixture, testMobFixture: "martial-player-controls.csv",
        extraQaMetadata: new() { ["ScopedStockRows"] = "44", ["NativeCloseableRows"] = 1 },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_UTILITY_MAP_QA_REVEAL"] = "1" },
        readyMessages: new[]
        {
            $"Start on outdoor NAV ({X},{Y},{Z}). Type ?qaunload 44, then call Flood and accept Mission Map Fixture. Use the console's current READY Safe 1 stand. With no map roll, the radar should show no Safe 1 ping. Type ?qaload 44 in place and confirm the already accepted mission stays quiet.",
            "Abandon and call Flood again with 44 loaded. The disposable reveal roll is fixed at 99 for loaded acceptance only. At the current printed Safe 1 stand, expect flags 63, yellow Safe and entrance pings and a green Map Courier ping. Type ?utilitymap 7 and let the next refresh clear the courier and entrance while the safe stays visible. Type ?utilitymap 63 to restore them. These two tier switches are fixture controls.",
            "Use the printed READY Mission exit NAV stand. Check the yellow exit ping. With ?utilitymap 31 it clears; with ?utilitymap 63 it returns. Return to the Safe 1 stand for unloading.",
            "Type ?qaunload 44 with the ping visible. Check 'ping cleared Safe 1' and let its 3s display expire. Type ?qaload 44 near the safe and confirm there is no new 'ping sent Safe 1' in this mission. No [MODIFIER], maximum [VITALS] or [PVP] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Map acceptance needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var spawn) && !spawn.indoors && Math.Abs(spawn.floorY + 95 - Y) < 1,
            "The starter spawn is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var map = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Single(row => AbilityCatalog.IdOf(row.Descriptor) == Map);
        var rows = new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [Map] = map };
        Check.Equal((ushort)10, map.Level, "The map is trained to its shipped cap");
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable server.");
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Expected a disposable database.");
        await actor.EnterWorldAsync();

        ushort transaction = 5;
        ushort NextTransaction() => transaction++;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Map }, load: false);
        int absentAt = server.DebugLog().Length;
        int absentPackets = await AcceptAsync(actor, NextTransaction);
        await ApproachSafeAsync(actor, database, root, MapFixture);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Map }, load: true);
        actor.SendChat("?utilitymap");
        await actor.WaitForTextAsync("Utility map fixture flags=0");
        await Task.Delay(TimeSpan.FromSeconds(4.2));
        string absentLog = server.DebugLog()[absentAt..];
        Check.That(!MapRoll.IsMatch(absentLog) && !absentLog.Contains(PingSent) &&
            !actor.World.RpcSnapshot().Skip(absentPackets).Any(packet => OpenMap.IsMatch(packet.Bytes)),
            "Accepting without Map skips the roll and loading it afterwards cannot start this mission reveal");
        await AbandonAsync(actor);
        var first = await AcceptAndRevealAsync(actor, database, server, NextTransaction, root, MapFixture);

        const string courier = "Friendly Map Courier";
        const string entrance = "Mission entrance";
        await ExpectMarkerAsync("sent", courier);
        await ExpectMarkerAsync("sent", entrance);
        Check.That(actor.World.RpcSnapshot().Any(packet => OpenMap.IsMatch(packet.Bytes) &&
            Enumerable.Range(0, (packet.Bytes.Length - 7) / 4).Any(index => packet.Bytes[10 + index * 4] == 1)),
            "The courier is encoded with the stock green relationship");
        int tiersAt = server.DebugLog().Length;
        actor.SendChat("?utilitymap 7");
        await ExpectMarkerAsync("cleared", courier, tiersAt);
        await ExpectMarkerAsync("cleared", entrance, tiersAt);
        Check.That(!server.DebugLog()[tiersAt..].Contains(PingCleared), "The low tier keeps the objective ping");
        tiersAt = server.DebugLog().Length;
        actor.SendChat("?utilitymap 63");
        await ExpectMarkerAsync("sent", courier, tiersAt);
        await ExpectMarkerAsync("sent", entrance, tiersAt);

        int exitAt = server.DebugLog().Length;
        string checkpoint = await database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={actor.CharId}");
        var progress = MervFollowupCheckpointFixture.ParseHex(checkpoint)
            ?? throw new InvalidOperationException("The map fixture has no accepted mission checkpoint.");
        string lobby = LobbyStand(progress) ?? throw new InvalidOperationException("No walkable stand borders the mission lobby exit.");
        Console.WriteLine($"READY Mission exit NAV stand. Assisted move {lobby}");
        actor.SendChat(lobby);
        await actor.WaitForTextAsync("Teleported to");
        await ExpectMarkerAsync("sent", "Mission exit", exitAt);
        exitAt = server.DebugLog().Length;
        actor.SendChat("?utilitymap 31");
        await ExpectMarkerAsync("cleared", "Mission exit", exitAt);
        actor.SendChat("?utilitymap 63");
        await ExpectMarkerAsync("sent", "Mission exit", exitAt);
        await ApproachSafeAsync(actor, database, root, MapFixture);
        await ExpectMarkerAsync("sent", "Safe 1", exitAt);

        int beforeUnload = server.DebugLog().Length;
        int unloadAt = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Map }, load: false);
        await actor.WaitUntilAsync(() => server.DebugLog()[beforeUnload..].Contains(PingCleared),
            "Unload stops refreshing the safe's map ping", HeadlessPlayer.DefaultTimeout);
        int reloadAt = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Map }, load: true);
        actor.SendChat("?utilitymap");
        await actor.WaitForTextAsync("Utility map fixture flags=0");
        await Task.Delay(TimeSpan.FromSeconds(4.2));
        int oldRevealCount = actor.World.RpcSnapshot().Skip(reloadAt).Count(packet => OpenMap.IsMatch(packet.Bytes));
        Check.Equal(0, oldRevealCount,
            "A reloaded map does not reveal the old mission");
        Check.That(!server.DebugLog()[beforeUnload..].Contains(PingSent), "The old safe ping is not sent again");

        await AbandonAsync(actor);
        var second = await AcceptAndRevealAsync(actor, database, server, NextTransaction, root, MapFixture);

        var unload = await StockAbilityProfile.ObserveAsync(actor, unloadAt,
            packet => BytePattern.Parse("80 b3 2c 00").IsMatch(packet.Bytes), "44 unload acknowledgement");
        var reload = await StockAbilityProfile.ObserveAsync(actor, reloadAt,
            packet => BytePattern.Parse("80 b2 2c 00").IsMatch(packet.Bytes), "44 reload acknowledgement");
        Check.Equal(0, actor.World.Problems.Count, "Mission map framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "download-mission-map.json"),
            JsonSerializer.Serialize(new { FirstReveal = first.Reveal.Hex, FirstFlags = first.Flags, Unload = unload.Hex, Reload = reload.Hex,
                UnloadedAcceptanceFlags = 0, OldFlags = 0, OldRevealCount = oldRevealCount, PingSent, PingCleared, FreshReveal = second.Reveal.Hex, FreshFlags = second.Flags },
                new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();

        Task ExpectMarkerAsync(string action, string label, int cursor = 0) =>
            actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains($"Download Mission Map: ping {action} {label}"),
                $"Map tier {action} {label}", HeadlessPlayer.DefaultTimeout);
    }

    private static async Task AbandonAsync(HeadlessPlayer actor)
    {
        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc("80 a6");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => BytePattern.Parse("80 a2 08").IsMatch(packet.Bytes),
            "The old mission is abandoned");
    }

    internal static async Task<(RpcRecord Reveal, int Flags)> AcceptAndRevealAsync(
        HeadlessPlayer actor, ThrowawayDatabase database, ServerInstance server, Func<ushort> nextTransaction,
        string? repositoryRoot = null, string missionFixture = "locked-safe.xml")
    {
        int beforeAccept = server.DebugLog().Length;
        int start = await AcceptAsync(actor, nextTransaction);
        await actor.WaitUntilAsync(() => MapRoll.IsMatch(server.DebugLog()[beforeAccept..]),
            "Mission acceptance rolled for map access", HeadlessPlayer.DefaultTimeout);
        int flags = int.Parse(MapRoll.Match(server.DebugLog()[beforeAccept..]).Groups[1].Value);
        Check.That((flags & 1) != 0, "The disposable roll reveals the objective on the first acceptance");
        await ApproachSafeAsync(actor, database, repositoryRoot, missionFixture);
        var reveal = await StockAbilityProfile.ObserveAsync(actor, start,
            packet => OpenMap.IsMatch(packet.Bytes) && packet.Bytes.Length > 5, "Minimap objective reveal");
        await actor.WaitUntilAsync(() => server.DebugLog()[beforeAccept..].Contains(PingSent),
            "The server identifies the safe in the ping log", HeadlessPlayer.DefaultTimeout);
        return (reveal, flags);
    }

    private static async Task<int> AcceptAsync(HeadlessPlayer actor, Func<ushort> nextTransaction)
    {
        int start = actor.World.RpcSnapshot().Count;
        string listId = StockAbilityProfile.LittleEndianHex(nextTransaction());
        actor.SendRpc($"80 94 {listId} 03");
        var list = BytePattern.Parse($"80 97 {listId}");
        await StockAbilityProfile.ObserveAsync(actor, start,
            packet => list.IsMatch(packet.Bytes), "Flood mission list");
        actor.SendRpc($"80 98 {StockAbilityProfile.LittleEndianHex(nextTransaction())} 00 00 01 02 00");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => BytePattern.Parse("80 99").IsMatch(packet.Bytes), "Mission details");
        actor.SendRpc($"80 9b {StockAbilityProfile.LittleEndianHex(nextTransaction())} 01");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => BytePattern.Parse("80 9e 00").IsMatch(packet.Bytes), "Mission accepted");
        return start;
    }

    private static async Task ApproachSafeAsync(HeadlessPlayer actor, ThrowawayDatabase database, string? repositoryRoot,
        string missionFixture)
    {
        if (repositoryRoot != null)
        {
            string checkpoint = await database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={actor.CharId}");
            var progress = MervFollowupCheckpointFixture.ParseHex(checkpoint)
                ?? throw new InvalidOperationException("The accepted Safe fixture saved no mission checkpoint.");
            string fixture = Path.Combine(repositoryRoot, "tools", "HeadlessClient", "Fixtures", missionFixture);
            NavMesh.UseDirectory(Path.Combine(repositoryRoot, "data", "navmesh"));
            var approach = MissionCatalogObjectApproaches.Find(progress, repositoryRoot, fixture).Single(value => value.ObjectId == 1);
            Check.That(approach.Kind == "Safe" && approach.AssistedCommand != null, "The custom mission publishes a NAV-valid Safe stand");
            Console.WriteLine("READY " + approach.Describe());
            actor.SendChat(approach.AssistedCommand!);
            await actor.WaitForTextAsync("Teleported to");
        }
        else await Scenarios.ApproachMissionObjectFixture(database, actor, 1);
    }

    internal static string? LobbyStand(MissionProgress.SavedState progress)
    {
        if (progress.Exits is not [var exit]) return null;
        foreach (var (dx, dz) in new[] { (150, 0), (-150, 0), (0, 150), (0, -150), (0, 0) })
        {
            double x = exit[0] + dx, y = exit[1], z = exit[2] + dz;
            if (!NavMesh.Locate(1, x, y, z, out var floor) || Math.Abs(floor.floorY + 95 - y) > 100)
                continue;
            return FormattableString.Invariant($"?tp {x:0.###} {floor.floorY + 95:0.###} {z:0.###} {floor.sector:x} {floor.region:x} {floor.polygon:x}");
        }
        return null;
    }
}
