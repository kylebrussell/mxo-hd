using System.Buffers.Binary;
using System.Globalization;
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
    private const int X = 17000, Y = 495, Z = 6400;
    private static readonly (string Handle, int X, int Z)[] Stands =
    {
        ("QAFriend", 17200, Z), ("QAFar", 17400, Z), ("QAOther", 17100, Z)
    };

    public override string Name => "ability-parity-hacker-mission-utilities";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Map, RecallMember, RecallTeam, CombatStyles.HackerStyle }, X, Z,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { $"UPDATE characters SET alignment=3, repMero=100, y={Y}, rotation={MobAggro.YawTowards(X, Z, Stands[0].X, Z)} WHERE charId=110" }
            .Concat(Stands.Select(stand =>
                $"UPDATE characters SET x={stand.X}, y=495, z={stand.Z}, alignment=3, pvpflag=0 WHERE handle='{stand.Handle}'"))
            .ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Map, RecallMember, RecallTeam, CombatStyles.HackerStyle },
        missionFixture: "locked-safe.xml", testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_UTILITY_MAP_QA_REVEAL"] = "1", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "44,382,486", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Step A. CSR starts on outdoor NAV (17000,495,6400), facing QAFriend (17200,495,6400). QAFar (17400,495,6400) and QAOther (17100,495,6400) are visible on the same pavement. Try 486 in Self Defense, then choose Hacker Style on page 2 slot 4. Form a mission team with QAFriend and QAFar using the radial and helper accept. Keep QAOther outside.",
            "Step B. Select QAFriend and press page 2 slot 2 (382). Watch QAFriend's avatar actually move beside CSR. QAFar and QAOther must stay in place. Then walk several meters away from QAFriend along the open street so both helpers must visibly move on the next cast.",
            "Step C. With 44 loaded, take the Locked Safe Fixture from Flood and approach its printed safe stand. Confirm CSR gets the Safe 1 minimap ping. Clear selection and use slot 3 Recall Mission Team. Both teammates phase out with effect 0x280006d1 and arrive beside CSR; QAOther stays outside the team. Expect a 2 s cast, 75 IS and 180 s own reuse.",
            "Step D. Expect [UTILITY] Download Mission Map: team pings sent owner=110 recipient=6111 and recipient=6112. Move QAOther near the stand through helper teleport if you want the same-floor exclusion. There should be no team ping for 6113. Enter ?qaunload 44 and check the solo and both team clear lines. Pings expire within 3 s. Reloading 44 does not refresh the old mission."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (var stand in Stands)
            StarterHackerPassiveFixture.CheckNav(context, stand.X);
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
        byte[] teamRequest = Convert.FromHexString(StockRecallTeam);
        var refusedStyle = await StarterHackerControlTargetMode.RefusedAsync(actor, RecallTeam, teamRequest, AbilityRefusals.WrongStyle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var recallRow = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Single(row => AbilityCatalog.IdOf(row.Descriptor) == RecallTeam);
        var recallArchive = new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [RecallTeam] = recallRow };
        await StockAbilityProfile.ChangeLoadoutAsync(actor, recallArchive, new[] { RecallTeam }, load: false);
        var refusedUnloaded = await StarterHackerControlTargetMode.RefusedAsync(actor, RecallTeam, teamRequest, AbilityRefusals.NotLoaded);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, recallArchive, new[] { RecallTeam }, load: true);
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
        var outsiderViewBefore = await actor.WaitForViewNamedAsync(outsider.Handle);
        uint outsiderHandle = DuelPackets.Handle(outsiderViewBefore.ViewId, outsiderViewBefore.SpawnId);
        var refusedSelf = await StarterHackerControlTargetMode.RefusedAsync(actor, RecallMember,
            StarterHackerControlTargetMode.WithHandle(StockRecallMember, 0));
        var refusedNonMember = await StarterHackerControlTargetMode.RefusedAsync(actor, RecallMember,
            StarterHackerControlTargetMode.WithHandle(StockRecallMember, outsiderHandle), AbilityRefusals.NotGroupTarget);
        uint stale = DuelPackets.Handle(friendView.ViewId, (ushort)(friendView.SpawnId ^ 1));
        var refusedStale = await StarterHackerControlTargetMode.RefusedAsync(actor, RecallMember,
            StarterHackerControlTargetMode.WithHandle(StockRecallMember, stale));
        byte[] request = Convert.FromHexString(StockRecallMember);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), friendView.ViewId | ((uint)friendView.SpawnId << 16));

        int singleStart = server.DebugLog().Length;
        var single = await CheckRecallAsync(actor, outsider, request, new[] { friend }, new[] { far, outsider }, (X, Y, Z));
        Check.That(server.DebugLog()[singleStart..].Contains($"[ABILITY-COST] char={actor.CharId} ability=382 paid=30 "),
            "Recall Team Member pays the stored 30 IS");
        int returnCursor = friend.World.ViewSnapshot().Count;
        var rest = Stands.Single(value => value.Handle == friend.Handle);
        friend.SendChat($"?tp {rest.X} {Y} {rest.Z} 0 0 0");
        await WaitForPositionAsync(friend, returnCursor, OwnPosition(rest.X, Y, rest.Z), "restored helper stand");
        ushort transaction = 5;
        int travelAt = actor.World.ViewSnapshot().Count;
        var map = await DownloadMissionMapMode.AcceptAndRevealAsync(actor, db, server, () => transaction++, context.RepositoryRoot);
        var missionStand = CurrentStand(actor, travelAt);
        int outsiderView = outsider.World.ViewSnapshot().Count;
        outsider.SendChat(FormattableString.Invariant($"?tp {missionStand.X + 60} {missionStand.Y} {missionStand.Z} 0 0 0"));
        await WaitForPositionAsync(outsider, outsiderView,
            OwnPosition(missionStand.X + 60, missionStand.Y, missionStand.Z), "Outsider is on the same floor");
        var recipients = new[] { friend, far };
        var pingStarts = recipients.ToDictionary(member => member, member => member.World.RpcSnapshot().Count);
        int outsiderAt = outsider.World.RpcSnapshot().Count;
        int teamStart = server.DebugLog().Length;
        var team = await CheckRecallAsync(actor, outsider, teamRequest, recipients, new[] { outsider }, missionStand);
        Check.That(server.DebugLog()[teamStart..].Contains($"[ABILITY-COST] char={actor.CharId} ability=486 paid=75 "),
            "Recall Mission Team pays the stored 75 IS");
        foreach (var member in recipients)
            await StockAbilityProfile.ObserveAsync(member, pingStarts[member], HasMissionPing, $"{member.Handle} receives a mission objective ping");
        Check.That(!outsider.World.RpcSnapshot().Skip(outsiderAt).Any(HasMissionPing), "A same-floor outsider sees no team ping");
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Single(row => AbilityCatalog.IdOf(row.Descriptor) == Map);
        int beforeUnload = server.DebugLog().Length;
        var stoppedAt = recipients.ToDictionary(member => member, member => member.World.RpcSnapshot().Count);
        await StockAbilityProfile.ChangeLoadoutAsync(actor,
            new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [Map] = archive }, new[] { Map }, load: false);
        await actor.WaitUntilAsync(() => server.DebugLog()[beforeUnload..].Contains("Download Mission Map: ping cleared Safe 1", StringComparison.Ordinal) &&
            recipients.All(member => server.DebugLog()[beforeUnload..].Contains($"team pings cleared owner=110 recipient={member.CharId}")),
            "Unloading the map clears refresh for the leader and both teammates", HeadlessPlayer.DefaultTimeout);
        // Read the first post-unload barriers before measuring absence across two skipped refresh intervals.
        foreach (var member in recipients)
        {
            member.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(member, stoppedAt[member], frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes), "Team refresh boundary");
            stoppedAt[member] = member.World.RpcSnapshot().Count;
        }
        await StockAbilityProfile.ChangeLoadoutAsync(actor,
            new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [Map] = archive }, new[] { Map }, load: true);
        await Task.Delay(TimeSpan.FromSeconds(4.2));
        Check.That(recipients.All(member => !member.World.RpcSnapshot().Skip(stoppedAt[member]).Any(HasMissionPing)),
            "The old team reveal stops refreshing on unload and does not restart on reload");
        Check.Equal(0, actor.World.Problems.Count, "Mission-utility team and map startup framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-mission-utilities.json"),
            JsonSerializer.Serialize(new
            {
                RefusedSelf = refusedSelf.Hex, RefusedNonMember = refusedNonMember.Hex, RefusedStale = refusedStale.Hex,
                TeamWrongStyle = refusedStyle.Hex, TeamUnloaded = refusedUnloaded.Hex,
                SingleRecall = single, TeamRecall = team, Outsider = outsider.Handle, map.Flags, Map = map.Reveal.Hex
            }));
        return Array.Empty<byte>();
    }

    private static async Task<object[]> CheckRecallAsync(HeadlessPlayer actor, HeadlessPlayer outsider, byte[] request,
        HeadlessPlayer[] recipients, HeadlessPlayer[] stationary, (float X, float Y, float Z) stand)
    {
        var watchers = new[] { actor, outsider };
        var players = watchers.Concat(recipients).Concat(stationary).Distinct().ToArray();
        var cursors = players.ToDictionary(player => player, player => player.World.ViewSnapshot().Count);
        var creations = watchers.ToDictionary(watcher => watcher, watcher => watcher.World.CreationSnapshot());
        string coordinates = PositionHex(stand.X, stand.Y, stand.Z);
        var teleport = OwnPosition(stand.X, stand.Y, stand.Z);
        ushort ability = BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2));
        bool single = ability == RecallMember;
        const uint expectedFx = 0x280006d1;
        byte[] ownFx = ServerPackets.BuildPlayerAbilityEffect(expectedFx, 0, true)[..^1];
        byte[] ownTeleport = { 2, 0, 1, 0x1c };
        int started = actor.World.RpcSnapshot().Count;
        actor.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(actor, ability, 2);
        await EvidenceRetestWire.TimerAsync(actor, started, ability, AbilityCooldowns.OwnTimer, single ? 30 : 180);
        var proof = new List<object>();
        foreach (var recipient in recipients)
        {
            byte[] own = await WaitForPositionAsync(recipient, cursors[recipient], teleport, "own recall arrival");
            var seen = recipient.World.ViewSnapshot().Skip(cursors[recipient]).ToArray();
            int phaseOut = Array.FindIndex(seen, frame => frame.AsSpan().IndexOf(ownFx) >= 0);
            int arrival = Array.FindIndex(seen, teleport.Occurs);
            bool inOrder = phaseOut >= 0 && (phaseOut < arrival || (phaseOut == arrival &&
                own.AsSpan().IndexOf(ownFx) < own.AsSpan().IndexOf(ownTeleport)));
            Check.That(inOrder,
                $"{recipient.Handle} sees the team-member phase-out before their recall arrival");
            proof.Add(new { Recipient = recipient.Handle, PhaseOut = WireFormat.ToHex(seen[phaseOut]), Own = WireFormat.ToHex(own) });
            foreach (var watcher in watchers)
            {
                var before = creations[watcher].LastOrDefault(view => view.Name == recipient.Handle);
                BytePattern? move = before == null ? null :
                    BytePattern.Parse($"{StockAbilityProfile.LittleEndianHex(before.ViewId)} 01 0c ?? {coordinates}");
                ViewCreation? arrived = null;
                byte[]? observed = null;
                await watcher.WaitUntilAsync(() =>
                {
                    arrived = watcher.World.CreationSnapshot().Skip(creations[watcher].Count)
                        .FirstOrDefault(view => view.Name == recipient.Handle &&
                            EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, stand.Y, stand.Z }));
                    if (move != null) observed = watcher.World.ViewSnapshot().Skip(cursors[watcher]).FirstOrDefault(move.Occurs);
                    return arrived != null || observed != null;
                }, $"{recipient.Handle} recall lands in {watcher.Handle}'s live view", HeadlessPlayer.DefaultTimeout);
                proof.Add(new { Recipient = recipient.Handle, Observer = watcher.Handle, View = arrived?.ViewId ?? before!.ViewId,
                    Arrival = arrived != null ? PositionHex(stand.X, stand.Y, stand.Z) : WireFormat.ToHex(observed!) });
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

    private static bool HasMissionPing(RpcRecord frame) =>
        frame.Bytes.Length >= 11 && BytePattern.Parse("80 c5 05 00 03").IsMatch(frame.Bytes);

    private static (float X, float Y, float Z) CurrentStand(HeadlessPlayer actor, int cursor)
    {
        byte[] teleport = WireFormat.FromHex("02 00 01 1c");
        foreach (byte[] list in actor.World.ViewSnapshot().Skip(cursor).Reverse())
        {
            int start = list.AsSpan().IndexOf(teleport);
            if (start >= 0 && start + 24 <= list.Length)
                return (BitConverter.ToSingle(list, start + 12), BitConverter.ToSingle(list, start + 16), BitConverter.ToSingle(list, start + 20));
        }
        var initial = EngageFoesFixture.Position(actor.World.CreationSnapshot().Last(view => view.ViewId == 2));
        return ((float)initial[0], (float)initial[1], (float)initial[2]);
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
