using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedJumpRankRangeMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, NearX = 18800, FarX = 21150;
    private const int InteriorX = 17100, Rank10X = 20850, Rank11X = 21150;
    private const ushort Jump = 11, Sprint = 184;
    private const string Return = "?tp 17000 495 6400 4e2 12 4 - 40";
    private const string InteriorStand = "?tp 17100 495 6400 4e2 12 1 - 40";
    // PR689 64bfb2be native CSEQ 56 at the interior stand.
    private const string InteriorForward = "80 c2 00 00 00 00 80 75 d4 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 00 b9 40 e2 04 02 00 00 00 00 f0 20 46 01 69 9f ed 16";
    // PR689 PacketLog lines 762–763 and 1424–1425, the complete cancelled forward requests.
    private const string Rank10Forward = "80 c2 00 00 00 00 80 5c d4 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 00 b9 40 e2 04 02 00 00 00 00 f0 20 46 01 62 6e 88 16";
    private const string Rank11Forward = "80 c2 00 00 00 00 80 a7 d4 40 00 00 00 00 00 f0 7e 40 00 00 00 20 00 00 b9 40 e2 04 01 00 00 00 00 f0 20 46 01 d4 3b 89 16";
    // PR703 stock takeoff report when the cost check blocked 80 c2.
    private const string TakeoffReport = "80 c4 00 00 00 00 40 b3 d0 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 00 b9 40 e2 04 12 00 01 00";
    // Retail proxy_026 CSEQ 2280, 245.828125 peak. Only the destination and NAV are retargeted.
    private const string CapturedStart = "80 c2 00 00 00 60 a6 18 d7 c0 00 00 00 00 ff 9b 9d 40 00 00 00 40 7a db d6 " +
        "40 e8 00 02 00 08 00 00 d4 75 43 01 66 28 be 04";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Jump, Sprint).Order().ToArray();

    public override string Name => "ability-parity-awakened-jump-rank-range";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=IF(ability_id=2147494912,10,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,FarX,Z)},cash=2000000,innerStrC=99,innerStrM=99 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Jump, Sprint }, testMobFixture: "awakened-jump-rank-range.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "2,11,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR starts at ({X},{Y},{Z}) facing QA Jump Near at ({NearX},{Y},{Z}), with 99 IS held. Leave Sprint off. Click page 2 slot 1 for Jump aim and use a real mouse click on the open street near the marker about 18 m east. Land and strafe. Save the first 80 c2 and [NAV] Hyper-Jump line. A low-peak 18 m sample whose vertical time is shorter should take about 2.25 s. Expect IS 99->79.",
            $"Type {InteriorStand}. From normal view use Ctrl+Space, wait 12 s and strafe near X=20950. Expect the [NAV] full-peak control at 38.5 m in about 4.81 s, one clock echo, normal landing and IS 79->59. Return to the printed stand and train Hyper-Jump 10 to 11 for 24,200 Info.",
            $"Make two rank-11 forward jumps with {InteriorStand} and a strafe between them. Expect landings near X=21250 and IS 59->39->19. Return to the same stand.",
            "At 19 IS try Ctrl+Space once. Expect the stock local cost gate and no accepted jump. If the stock client sends its lone 80 c4 takeoff report, save it and verify there is no jump view response and no Jump cancel line. Then use plain Space once. Expect IS to stay at 19."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Jump range needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        for (int x = X; x <= Rank11X + InteriorX - X; x += 50) Ground(x);
        Check.That(Ground(X).polygon == 4 && Ground(InteriorX) is { sector: 1250, region: 18, polygon: 1 },
            "Diagnostic takeoff remains in region 18 and leaves the original boundary polygon");
        Check.That(Ground(Rank10X).region == 2 && Ground(Rank10X + InteriorX - X).region == 2 &&
            Ground(Rank11X).region == 1 && Ground(Rank11X + InteriorX - X).region == 1,
            "The shifted takeoff preserves each landing region");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        ushort expectedIs = 99;
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
        actor.SendChat(InteriorStand);
        await actor.WaitForTextAsync("Teleported to 4e2/12");
        int orphanViews = actor.World.ViewSnapshot().Count;
        int orphanRpcs = actor.World.RpcSnapshot().Count;
        actor.World.SendRpcs(WireFormat.FromHex(TakeoffReport), WireFormat.FromHex("81 48"));
        await StockAbilityProfile.ObserveAsync(actor, orphanRpcs, packet => BytePattern.Parse("81 49").IsMatch(packet.Bytes),
            "Lone stock takeoff report barrier");
        Check.That(!actor.World.ViewSnapshot().Skip(orphanViews).Any(BytePattern.Parse("02 00 03 09 08 00").Occurs),
            "The stock C4 that follows a local refusal is silent without an accepted C2");
        await ReturnAsync();
        await JumpAsync(NearX);
        await ReturnAsync();
        await ReplayForwardAsync(Rank10Forward, Rank10X);
        actor.SkipReceived();
        actor.SendChat(InteriorStand);
        await actor.WaitForTextAsync("Teleported to 4e2/12");
        await ReplayForwardAsync(InteriorForward, Rank10X + InteriorX - X, startX: InteriorX, bundleTakeoffReport: true);
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

        int refusedAt = actor.World.RpcSnapshot().Count;
        int viewAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(Rank11Forward);
        await StockAbilityProfile.ObserveAsync(actor, refusedAt, packet =>
            AbilityRefusals.TryDecode(packet.Bytes, out var refusal) && refusal.AbilityID == Jump &&
            refusal.ErrorID == AbilityRefusals.InsufficientInnerStrength && refusal.InnerstrengthDeficit == 1,
            "A flag-1 stock start at 19 IS is refused with a deficit of one");
        actor.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(actor, refusedAt, packet => BytePattern.Parse("81 49").IsMatch(packet.Bytes),
            "Post-shortfall barrier");
        Check.That(!actor.World.RpcSnapshot().Skip(refusedAt).Any(packet => packet.Opcode == 0xc3) &&
            !actor.World.ViewSnapshot().Skip(viewAt).Any(BytePattern.Parse("02 00 03 01 08 00 80 80 bc").Occurs),
            "The shortfall sends no jump view or clock echo");
        Check.Equal((ushort)19, expectedIs, "Four accepted starts leave 19 IS");
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

        async Task ReplayForwardAsync(string capturedHex, int expectedX, int startX = X, bool bundleTakeoffReport = false)
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
            var origin = Ground(startX);
            var takeoff = new JumpRecords.Destination(Bits(startX + 1), Bits(Y), Bits(Z),
                origin.sector, origin.region, origin.polygon);
            byte[] report = JumpRecords.Encode(new JumpRecords.Cancel(takeoff));
            if (startX == InteriorX)
                Check.That(report.AsSpan().SequenceEqual(WireFormat.FromHex(
                    "80 c4 00 00 00 00 40 b3 d0 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 00 b9 40 e2 04 12 00 01 00")),
                    "Replay uses the recorded interior takeoff report including the start polygon");
            await ObserveJumpAsync(bytes, request, startX, Math.Abs(expectedX - startX) / 800.0, report, bundleTakeoffReport);
        }

        async Task JumpAsync(int x)
        {
            Check.That(JumpRecords.TryStart(WireFormat.FromHex(CapturedStart), out var captured) && captured.IsHyperJump == 1,
                "The captured full flag-1 start decodes");
            NavLocation nav = Ground(x);
            var destination = new JumpRecords.Destination(Bits(x), Bits(Y), Bits(Z), nav.sector, nav.region, nav.polygon);
            var request = new JumpRecords.Start(destination, captured.PeakBits, captured.IsHyperJump, captured.TimeStamp);
            await ObserveJumpAsync(JumpRecords.Encode(request), request, travelSeconds: (x - X) / 800.0);
        }

        async Task ObserveJumpAsync(byte[] bytes, JumpRecords.Start request, int? takeoffX = null, double? travelSeconds = null,
            byte[]? report = null, bool bundleTakeoffReport = false)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int viewCursor = actor.World.ViewSnapshot().Count;
            int logCursor = context.ActualServer!.DebugLog().Length;
            if (report != null && bundleTakeoffReport)
                actor.World.SendRpcs(bytes, report);
            else
                actor.SendRpc(bytes);
            var echo = JumpRecords.EncodeClockEcho(request.TimeStamp);
            var echoRecord = await StockAbilityProfile.ObserveAsync(actor, cursor,
                packet => packet.Bytes.AsSpan().SequenceEqual(echo), "Stock clock echo");
            if (report != null && !bundleTakeoffReport)
                actor.SendRpc(report);
            if (report != null)
                await actor.WaitUntilAsync(() => context.ActualServer.DebugLog()[logCursor..].Contains(
                    $"[NAV] Jump takeoff report {takeoffX!.Value + 1},{Y},{Z}", StringComparison.Ordinal),
                    "Classified the stock takeoff position without cancelling", HeadlessPlayer.DefaultTimeout);
            string xyz = WireFormat.ToHex(bytes[2..26]);
            string peak = WireFormat.ToHex(BitConverter.GetBytes(request.PeakBits));
            var arrival = BytePattern.Parse($"02 00 03 01 08 00 80 80 bc {peak} 04 00 ?? ?? ?? ?? {xyz}");
            byte[] viewBytes = echoRecord.DatagramViewSection ??
                throw new InvalidOperationException("The clock echo has no view section.");
            Check.That(echoRecord.DatagramSimTime.HasValue && arrival.Occurs(viewBytes),
                $"Timed datagram carries the exact clock echo and jump view for X={request.Destination.X}");
            expectedIs -= 20;
            byte[] deltaPrefix = WireFormat.FromHex("02 00 03 01 08 00 80 80 bc");
            int start = viewBytes.AsSpan().IndexOf(deltaPrefix);
            Check.That(start >= 0 && viewBytes[start + 43] == 0x10, "The start delta exposes the own IS field");
            Check.Equal(expectedIs, BitConverter.ToUInt16(viewBytes, start + 44),
                "The exact echo datagram reports one 20 IS takeoff debit");
            if (travelSeconds.HasValue)
            {
                uint end = BitConverter.ToUInt32(viewBytes, start + 15);
                double duration = end / 1000.0 - echoRecord.DatagramSimTime!.Value;
                Check.That(Math.Abs(duration - travelSeconds.Value) < 0.3,
                    $"Jump duration {duration:F3} s agrees with expected travel {travelSeconds.Value:F3} s");
            }
            if (takeoffX.HasValue)
            {
                const int deltaLength = 46;
                int snapshot = start + deltaLength;
                Check.That(WireFormat.TryReadJumpSnapshot(viewBytes, snapshot, out int snapshotEnd, out var fields) &&
                    snapshotEnd == viewBytes.Length && fields.Length == 48,
                    "Combined 03 list carries the complete captured 03 09 layout");
                Check.That(Math.Abs(BitConverter.ToSingle(viewBytes, snapshot + 6) - takeoffX!.Value) < 1 &&
                    Math.Abs(BitConverter.ToSingle(viewBytes, snapshot + 10) - Y) < 1 &&
                    Math.Abs(BitConverter.ToSingle(viewBytes, snapshot + 14) - Z) < 1,
                    "The 03 09 position is the current takeoff point on verified NAV");
                uint samplesMs = BitConverter.ToUInt32(viewBytes, snapshot + 18);
                Check.That(Math.Abs(samplesMs / 1000.0 - echoRecord.DatagramSimTime!.Value) < 0.3,
                    "The 03 09 sim sample agrees with the timed datagram");
                Check.That(fields[16].AsSpan().SequenceEqual(viewBytes.AsSpan(start + 9, 4)) &&
                    fields[17].AsSpan().SequenceEqual(viewBytes.AsSpan(start + 13, 2)) &&
                    fields[18].AsSpan().SequenceEqual(viewBytes.AsSpan(start + 15, 4)) &&
                    fields[19].AsSpan().SequenceEqual(viewBytes.AsSpan(start + 19, 24)) &&
                    fields[25].AsSpan().SequenceEqual(viewBytes.AsSpan(start + 44, 2)),
                    "Both updates agree on peak, flags, end, destination and current IS");
                Check.Equal((byte)1, fields[0][0], "The full default RSI selector keeps the ordinary avatar");
            }
            else
            {
                Check.Equal(46, viewBytes.Length, "Short control retains the existing view shape");
            }
            actor.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(actor, cursor, packet => BytePattern.Parse("81 49").IsMatch(packet.Bytes), "Post-jump barrier");
            if (report != null)
            {
                var snapBack = BytePattern.Parse("02 00 03 09 08 00 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 80 80 b8 14 00");
                Check.That(!actor.World.ViewSnapshot().Skip(viewCursor).Any(snapBack.Occurs),
                    "Neither bundled nor separate takeoff reports produces a snap-back update");
            }
            var records = actor.World.RpcSnapshot().Skip(cursor).ToArray();
            Check.Equal(1, records.Count(packet => packet.Bytes.AsSpan().SequenceEqual(echo)), "Exactly one echo per stock jump");
            Check.That(!records.Any(packet => ManageBonusRecord.TryDecode(packet.Bytes, out var bonus) &&
                bonus!.AddRemoveType == 1 && bonus.SourceId is Jump or Sprint), "Jump keeps the loaded and active movement modifiers");
        }

        static ulong Bits(double value) => unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
    }
}
