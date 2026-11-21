using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedJumpRankRangeMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, NearX = 18800, FarX = 21150;
    private const ushort Jump = 11, Sprint = 184;
    private const string Return = "?tp 17000 495 6400 4e2 12 4 - 40";
    // PR672 PacketLog lines 1462–1463 and 1784–1785, complete rank 10 and rank 11 forward requests.
    private const string Rank10Forward = "80 c2 00 00 00 00 80 5c d4 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 00 b9 40 e2 04 02 00 00 00 00 f0 20 46 01 58 c9 d2 15";
    private const string Rank11Forward = "80 c2 00 00 00 00 80 a7 d4 40 00 00 00 00 00 f0 7e 40 00 00 00 20 00 00 b9 40 e2 04 01 00 00 00 00 f0 20 46 01 76 fa d3 15";
    // PR658 lines 2726–2727. Destination coordinates and NAV are replaced with this fixture's measured stands.
    private const string CapturedStart = "80 c2 00 00 00 e0 8b 38 d8 40 00 00 00 00 00 f0 7e 40 00 00 00 c0 db b8 a4 " +
        "c0 14 04 10 00 03 00 00 00 7a 44 01 65 43 4a 15";
    private const string SprintRequest = "80b9b80000000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Jump, Sprint).Order().ToArray();

    public override string Name => "ability-parity-awakened-jump-rank-range";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=IF(ability_id=2147494912,10,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,FarX,Z)},cash=2000000,innerStrC=500,innerStrM=500 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Jump, Sprint }, testMobFixture: "awakened-jump-rank-range.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "2,11,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR ({X},{Y},{Z}) faces QA Jump Near ({NearX},{Y},{Z}) and QA Jump Far ({FarX},{Y},{Z}) on the same outdoor NAV floor. Stand still with no selection and leave Sprint off. Both forward trials start from the normal standing view. The near marker is for the optional aimed jump only.",
            $"At rank 10 confirm [MODIFIER] source=11 stat=567 +30. From normal standing view, press Ctrl+Space once. Leave the jump hotbar button alone before takeoff so no aim mode remains pending. Release both keys, then wait 12 seconds with no further input. A successful landing is a normal strafe near x=20850, 38.5 m from start. Save the first post-landing movement XYZ and any 80 c4 cancel. One flag-1 80 c2 and one matching 80 c3 echo.",
            $"Return with {Return}. In Upgrade, train Hyper-Jump 10→11 for 24,200 Info; source=11 stat=567 +30 removes and +33 adds. Close Upgrade before using the same normal-view Ctrl+Space. Wait 12 seconds again. The expected ground destination is near x=21150, 41.5 m from start. Capture the first ordinary strafe after the landing. If either attempt cancels to start, report the 80 c4 and its destination.",
            $"Optional control: return with {Return}, press page 2 slot 2, and repeat the rank-11 near jump. Expect 184/115,877,878 +15 with no removals during the jump. One clock echo, no maximum [VITALS] changes or [PVP] rounds. Stop Sprint after landing."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Jump range needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        for (int x = X; x <= FarX; x += 100) Ground(x);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(rows[Jump].Level == 10 && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => rows.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Rank-10 Hyper-Jump and all required ranks fit stock memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await actor.WaitForViewNamedAsync("QA Jump Near");
        await actor.WaitForViewNamedAsync("QA Jump Far");
        await BonusAsync(0, Jump, 567, 30, 0, false);
        await JumpAsync(NearX);
        await ReturnAsync();
        await ReplayForwardAsync(Rank10Forward, 20850);
        await ReturnAsync();

        int trainAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"80 b7 0b 00 {StockAbilityProfile.LittleEndianHex(rows[Jump].Slot)} 01");
        var trained = ServerPackets.BuildAbilityUpgraded(Jump, 11);
        await StockAbilityProfile.ObserveAsync(actor, trainAt, packet => packet.Bytes.AsSpan().SequenceEqual(trained),
            "Hyper-Jump trains to rank 11 live");
        await BonusAsync(trainAt, Jump, 567, 30, 0, true);
        await BonusAsync(trainAt, Jump, 567, 33, 0, false);
        await ReplayForwardAsync(Rank11Forward, FarX);
        await ReturnAsync();

        int toggles = actor.World.RpcSnapshot().Count;
        actor.SendRpc(SprintRequest);
        foreach (ushort stat in new ushort[] { 115, 877, 878 })
            await BonusAsync(toggles, Sprint, stat, 15, 0, false);
        await JumpAsync(NearX);
        Check.Equal(0, actor.World.Problems.Count, "Jump rank-range request framing");
        return Array.Empty<byte>();

        NavLocation Ground(int x)
        {
            Check.That(NavMesh.Locate(1, x, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Jump corridor stand ({x},{Y},{Z}) is on outdoor NAV");
            return at;
        }

        Task<RpcRecord> BonusAsync(int cursor, ushort source, ushort stat, int value, int duration, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, cursor, packet => StockAbilityProfile.Bonus(packet, source, stat, value, duration, remove),
                $"Source {source}/{stat} {value}, remove={remove}");

        async Task ReturnAsync()
        {
            actor.SendChat(Return);
            await actor.WaitForTextAsync("Teleported to 4e2/12");
        }

        async Task ReplayForwardAsync(string capturedHex, int expectedX)
        {
            byte[] bytes = WireFormat.FromHex(capturedHex);
            Check.That(JumpRecords.TryStart(bytes, out var request) && request.IsHyperJump == 1,
                "The recorded forward request decodes as a flag-1 jump");
            Check.Equal((double)expectedX, request.Destination.X, "Recorded forward destination X");
            Check.That(Math.Abs(request.Destination.Y - Y) < 1 && Math.Abs(request.Destination.Z - Z) < 1,
                "Forward landing stays on the same verified outdoor corridor");
            var ground = Ground(expectedX);
            Check.That(request.Destination.Sector == ground.sector && request.Destination.Prefab == ground.region &&
                request.Destination.NavPoly == ground.polygon, "Stock and server agree on forward landing NAV");
            await ObserveJumpAsync(bytes, request);
        }

        async Task JumpAsync(int x)
        {
            Check.That(JumpRecords.TryStart(WireFormat.FromHex(CapturedStart), out var captured) && captured.IsHyperJump == 1,
                "The captured full flag-1 start decodes");
            NavLocation nav = Ground(x);
            var destination = new JumpRecords.Destination(Bits(x), Bits(Y), Bits(Z), nav.sector, nav.region, nav.polygon);
            var request = new JumpRecords.Start(destination, captured.PeakBits, captured.IsHyperJump, captured.TimeStamp);
            await ObserveJumpAsync(JumpRecords.Encode(request), request);
        }

        async Task ObserveJumpAsync(byte[] bytes, JumpRecords.Start request)
        {
            int cursor = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(bytes);
            var echo = JumpRecords.EncodeClockEcho(request.TimeStamp);
            await StockAbilityProfile.ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(echo), "Stock clock echo");
            string xyz = WireFormat.ToHex(bytes[2..26]);
            string peak = WireFormat.ToHex(BitConverter.GetBytes(request.PeakBits));
            var arrival = BytePattern.Parse($"02 00 03 01 08 00 80 80 bc {peak} 04 00 ?? ?? ?? ?? {xyz}");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).Any(arrival.Occurs),
                $"Own jump step carries the verified landing at X={request.Destination.X}", HeadlessPlayer.DefaultTimeout);
            actor.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(actor, cursor, packet => BytePattern.Parse("81 49").IsMatch(packet.Bytes), "Post-jump barrier");
            var records = actor.World.RpcSnapshot().Skip(cursor).ToArray();
            Check.Equal(1, records.Count(packet => packet.Bytes.AsSpan().SequenceEqual(echo)), "Exactly one echo per stock jump");
            Check.That(!records.Any(packet => ManageBonusRecord.TryDecode(packet.Bytes, out var bonus) &&
                bonus!.AddRemoveType == 1 && bonus.SourceId is Jump or Sprint), "Jump keeps the loaded and active movement modifiers");
        }

        static ulong Bits(double value) => unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
    }
}
