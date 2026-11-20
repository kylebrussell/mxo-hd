using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerMissionTeamUtilitiesMode : NativeQaMode
{
    private const ushort Map = UtilityAbilities.DownloadMissionMap;
    private const ushort RecallMember = UtilityAbilities.RecallTeamMember, RecallTeam = UtilityAbilities.RecallMissionTeam;
    // Complete stock requests from the PR483 native capture. Only the live direct object of 382 is variable.
    private const string StockRecallMember = "80b97e01040002000000000000000000000000000000000000000000000000e08f767241086b0000000000";
    private const string StockRecallTeam = "80b9e601000000000000000000000000000000000000000000000000585cae0001000000086b0000000000";
    private const int X = 24800, Y = 495, Z = -1498;
    private static readonly (string Handle, int X, int Z)[] Stands =
    {
        ("QAFriend", 25183, -1177), ("QAFar", 25719, -2269), ("QAOther", 25100, -1498)
    };

    public override string Name => "ability-parity-hacker-mission-utilities";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Map, RecallMember, RecallTeam }, X, Z,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET alignment=3, repMero=100, y=495 WHERE charId=110" }
            .Concat(Stands.Select(stand =>
                $"UPDATE characters SET x={stand.X}, y=495, z={stand.Z}, alignment=3, pvpflag=0 WHERE handle='{stand.Handle}'"))
            .ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Map, RecallMember, RecallTeam },
        missionFixture: "locked-safe.xml", environmentOverrides: new() { ["MXO_UTILITY_MAP_QA_REVEAL"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "44,382,486", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Step A. Form a mission team with QAFriend and QAFar using the normal radial and helper accept. Keep QAOther outside. Their initial stands are separate and within view.",
            "Step B. Select QAFriend and press page 2 slot 2 (382). Watch QAFriend's avatar actually move beside CSR. QAFar and QAOther must stay in place. Then walk several meters away from QAFriend along the open street so both helpers must visibly move on the next cast.",
            "Step C. Clear selection and press page 2 slot 3 (486). Confirm both teammates arrive and QAOther stays put. Watch both avatars actually move beside CSR and record the before/after views.",
            "Step D. With 44 loaded, take the Locked Safe Fixture from Flood and approach the safe using the printed stand. Confirm the objective map ping. Use ?qaunload 44 and confirm refresh stops. The page 2 slot for 44 identifies the passive; its effect is triggered by mission acceptance."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable mission-utility server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Expected a disposable mission-utility database.");
        await actor.EnterWorldAsync();
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Handle);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, 495, stand.Z }),
                $"{stand.Handle} is alive and visible on the documented stand");
        }
        var friend = context.Helpers["QAFriend"];
        var far = context.Helpers["QAFar"];
        var outsider = context.Helpers["QAOther"];
        await HackerParitySweepMode.InviteTeamMembersAsync(actor, new[] { friend, far });
        foreach (var member in new[] { friend, far })
            await outsider.WaitForViewNamedAsync(member.Handle);
        var friendView = await actor.WaitForViewNamedAsync(friend.Handle);
        byte[] request = Convert.FromHexString(StockRecallMember);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), friendView.ViewId | ((uint)friendView.SpawnId << 16));

        var single = await CheckRecallAsync(actor, outsider, request, new[] { friend }, new[] { far, outsider });
        int returnCursor = friend.World.ViewSnapshot().Count;
        var rest = Stands.Single(value => value.Handle == friend.Handle);
        friend.SendChat($"?tp {rest.X} {Y} {rest.Z} 0 0 0");
        await WaitForPositionAsync(friend, returnCursor, OwnPosition(rest.X, Y, rest.Z), "restored helper stand");
        var team = await CheckRecallAsync(actor, outsider, Convert.FromHexString(StockRecallTeam),
            new[] { friend, far }, new[] { outsider });

        ushort transaction = 5;
        var map = await DownloadMissionMapMode.AcceptAndRevealAsync(actor, db, server, () => transaction++);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Single(row => AbilityCatalog.IdOf(row.Descriptor) == Map);
        int beforeUnload = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(actor,
            new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [Map] = archive }, new[] { Map }, load: false);
        await actor.WaitUntilAsync(() => server.DebugLog()[beforeUnload..].Contains("Download Mission Map: ping cleared Safe 1", StringComparison.Ordinal),
            "Unloading the map clears its refresh while the team remains formed", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Mission-utility team and map startup framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-mission-utilities.json"),
            JsonSerializer.Serialize(new { SingleRecall = single, TeamRecall = team, Outsider = outsider.Handle, map.Flags, Map = map.Reveal.Hex }));
        return Array.Empty<byte>();
    }

    private static async Task<object[]> CheckRecallAsync(HeadlessPlayer actor, HeadlessPlayer outsider, byte[] request,
        HeadlessPlayer[] recipients, HeadlessPlayer[] stationary)
    {
        var watchers = new[] { actor, outsider };
        var players = watchers.Concat(recipients).Concat(stationary).Distinct().ToArray();
        var cursors = players.ToDictionary(player => player, player => player.World.ViewSnapshot().Count);
        string coordinates = PositionHex(X, Y, Z);
        var teleport = OwnPosition(X, Y, Z);
        actor.SendRpc(request);
        var proof = new List<object>();
        foreach (var recipient in recipients)
        {
            byte[] own = await WaitForPositionAsync(recipient, cursors[recipient], teleport, "own recall arrival");
            proof.Add(new { Recipient = recipient.Handle, Own = WireFormat.ToHex(own) });
            foreach (var watcher in watchers)
            {
                var view = await watcher.WaitForViewNamedAsync(recipient.Handle);
                string viewHex = StockAbilityProfile.LittleEndianHex(view.ViewId);
                var move = BytePattern.Parse($"{viewHex} 01 0c ?? {coordinates}");
                byte[] observed = await WaitForPositionAsync(watcher, cursors[watcher], move, $"{recipient.Handle} recall on view {view.ViewId}");
                proof.Add(new { Recipient = recipient.Handle, Observer = watcher.Handle, View = view.ViewId, Move = WireFormat.ToHex(observed) });
            }
        }

        foreach (var player in stationary)
        {
            Check.That(!player.World.ViewSnapshot().Skip(cursors[player]).Any(teleport.Occurs),
                $"{player.Handle} receives no own recall arrival");
            var view = await actor.WaitForViewNamedAsync(player.Handle);
            string viewHex = StockAbilityProfile.LittleEndianHex(view.ViewId);
            var move = BytePattern.Parse($"{viewHex} 01 0c ?? {coordinates}");
            Check.That(!actor.World.ViewSnapshot().Skip(cursors[actor]).Any(move.Occurs),
                $"{player.Handle} receives no observer recall move");
        }

        foreach (var player in players) Check.Equal(0, player.World.Problems.Count, $"{player.Handle} recall framing");
        return proof.ToArray();
    }

    private static BytePattern OwnPosition(float x, float y, float z) =>
        BytePattern.Parse($"02 00 01 1c ?? ?? ?? ?? ?? ?? ?? ?? {PositionHex(x, y, z)}");

    private static string PositionHex(float x, float y, float z) =>
        string.Join(" ", new[] { x, y, z }.Select(value => WireFormat.ToHex(BitConverter.GetBytes(value))));

    private static async Task<byte[]> WaitForPositionAsync(HeadlessPlayer player, int cursor, BytePattern pattern, string description)
    {
        byte[]? record = null;
        await player.WaitUntilAsync(() => (record = player.World.ViewSnapshot().Skip(cursor).FirstOrDefault(pattern.Occurs)) != null,
            description, HeadlessPlayer.DefaultTimeout);
        return record!;
    }
}
