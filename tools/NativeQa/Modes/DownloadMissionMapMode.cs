using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class DownloadMissionMapMode : NativeQaMode
{
    private const ushort Map = UtilityAbilities.DownloadMissionMap;
    private static readonly Regex MapRoll = new(@"\[UTILITY\] Download Mission Map: access flags (\d+)");
    private const string PingSent = "[UTILITY] Download Mission Map: ping sent Safe 1";
    private const string PingCleared = "[UTILITY] Download Mission Map: ping cleared Safe 1";
    private static readonly BytePattern OpenMap = BytePattern.Parse("80 c5 05 00 03");

    public override string Name => "ability-parity-download-mission-map";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Map }, 23828, 13984, Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET alignment=3, repMero=100, y=495 WHERE charId=110" },
        DevCommands: true, loadAbilityAncestors: true, missionFixture: "locked-safe.xml",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_UTILITY_MAP_QA_REVEAL"] = "1" },
        readyMessages: new[]
        {
            "Disposable reveal roll is fixed at 99. Call Flood and accept Locked Safe Fixture. Follow the mission route or use its printed QA stand. Check 'ping sent Safe 1' in the log and glance at the yellow minimap ping.",
            "Unload Download Mission Map (44) in the Archive. Check 'ping cleared Safe 1' and let its 3s display expire. Reload 44 near the safe and confirm there is no new 'ping sent Safe 1' in this mission.",
            "Abandon and accept once with 44 loaded. Return near the safe and confirm a new 'ping sent Safe 1' and yellow ping."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var map = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Single(row => AbilityCatalog.IdOf(row.Descriptor) == Map);
        var rows = new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [Map] = map };
        Check.Equal((ushort)10, map.Level, "The map is trained to its shipped cap");
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable server.");
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Expected a disposable database.");
        await actor.EnterWorldAsync();

        ushort transaction = 5;
        ushort NextTransaction() => transaction++;
        var first = await AcceptAndRevealAsync(actor, database, server, NextTransaction);
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
        var second = await AcceptAndRevealAsync(actor, database, server, NextTransaction);

        var unload = await StockAbilityProfile.ObserveAsync(actor, unloadAt,
            packet => BytePattern.Parse("80 b3 2c 00").IsMatch(packet.Bytes), "44 unload acknowledgement");
        var reload = await StockAbilityProfile.ObserveAsync(actor, reloadAt,
            packet => BytePattern.Parse("80 b2 2c 00").IsMatch(packet.Bytes), "44 reload acknowledgement");
        Check.Equal(0, actor.World.Problems.Count, "Mission map framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "download-mission-map.json"),
            JsonSerializer.Serialize(new { FirstReveal = first.Reveal.Hex, FirstFlags = first.Flags, Unload = unload.Hex, Reload = reload.Hex,
                OldFlags = 0, OldRevealCount = oldRevealCount, PingSent, PingCleared, FreshReveal = second.Reveal.Hex, FreshFlags = second.Flags },
                new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static async Task AbandonAsync(HeadlessPlayer actor)
    {
        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc("80 a6");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => BytePattern.Parse("80 a2 08").IsMatch(packet.Bytes),
            "The old mission is abandoned");
    }

    private static async Task<(RpcRecord Reveal, int Flags)> AcceptAndRevealAsync(
        HeadlessPlayer actor, ThrowawayDatabase database, ServerInstance server, Func<ushort> nextTransaction)
    {
        int start = actor.World.RpcSnapshot().Count;
        int beforeAccept = server.DebugLog().Length;
        string listId = StockAbilityProfile.LittleEndianHex(nextTransaction());
        actor.SendRpc($"80 94 {listId} 03");
        var list = BytePattern.Parse($"80 97 {listId}");
        await StockAbilityProfile.ObserveAsync(actor, start,
            packet => list.IsMatch(packet.Bytes), "Flood mission list");
        actor.SendRpc($"80 98 {StockAbilityProfile.LittleEndianHex(nextTransaction())} 00 00 01 02 00");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => BytePattern.Parse("80 99").IsMatch(packet.Bytes), "Mission details");
        actor.SendRpc($"80 9b {StockAbilityProfile.LittleEndianHex(nextTransaction())} 01");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => BytePattern.Parse("80 9e 00").IsMatch(packet.Bytes), "Mission accepted");
        await actor.WaitUntilAsync(() => MapRoll.IsMatch(server.DebugLog()[beforeAccept..]),
            "Mission acceptance rolled for map access", HeadlessPlayer.DefaultTimeout);
        int flags = int.Parse(MapRoll.Match(server.DebugLog()[beforeAccept..]).Groups[1].Value);
        Check.That((flags & 1) != 0, "The disposable roll reveals the objective on the first acceptance");
        await Scenarios.ApproachMissionObjectFixture(database, actor, 1);
        var reveal = await StockAbilityProfile.ObserveAsync(actor, start,
            packet => OpenMap.IsMatch(packet.Bytes) && packet.Bytes.Length > 5, "Minimap objective reveal");
        await actor.WaitUntilAsync(() => server.DebugLog()[beforeAccept..].Contains(PingSent),
            "The server identifies the safe in the ping log", HeadlessPlayer.DefaultTimeout);
        return (reveal, flags);
    }
}
