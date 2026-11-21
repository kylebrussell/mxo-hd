using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedRetailAimMode : NativeQaMode
{
    private const ushort Downtown = 2, Jump = 11, Sprint = 184;
    private const int Level = 25, X = -24230, Y = 1895, Z = 31256, LandingX = -23651, LandingZ = 23406;
    private const int MidpointX = -24000, MidpointZ = 28000;
    // Retail proxy_026, CSEQ 2280 and SSEQ 1842. The low-peak request is replayed without edits.
    private const string RetailAim = "80 c2 00 00 00 60 a6 18 d7 c0 00 00 00 00 ff 9b 9d 40 00 00 00 40 7a db d6 " +
        "40 e8 00 02 00 08 00 00 d4 75 43 01 66 28 be 04";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Jump, Sprint).Order().ToArray();

    public override string Name => "ability-parity-awakened-retail-aim";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=IF(ability_id={AbilityMessages.AbilityGameObject(Jump)},24,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET district='downtown',districtId={Downtown},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,LandingX,LandingZ)},innerStrC=100,innerStrM=100 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Jump, Sprint }, testMobFixture: "awakened-retail-aim.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "2,11,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR starts at the verified Pillsen takeoff ({X},{Y},{Z}), NAV 333/4/24. The original Baldwin Heights landing is ({LandingX},{Y},{LandingZ}), NAV 232/2/8. Rank 24 reaches the stock 80 m cap. Leave Sprint off and stay on the printed stand.",
            $"Page 2 slot 1 opens Jump aim. Aim at the street near QA Retail Jump Midpoint ({MidpointX},{Y},{MidpointZ}), 32.6 m ahead, and real-click once. If the stock client picks that ground at peak 1000, expect about 4.08 s and IS 100 to 80. Wait 12 s, then strafe. Save the flag-1 C2, actual landing and [NAV] peak/horizontal/travel.",
            "The far marker comes into view nearer the landing. An aim that reaches the original 78.7 m endpoint at a comparable low peak takes about 9.8 s.",
            "If the aim solver chooses closer ground or a different peak, retain its exact request and logged travel. The untouched low-peak retail packet is covered headlessly. No command forces the stock client's destination or peak."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(Downtown, X, Y, Z, out var takeoff) &&
            takeoff is { sector: 333, region: 4, polygon: 24, indoors: false } && Math.Abs(takeoff.floorY + 95 - Y) < 1,
            "The Pillsen stand is on the same outdoor floor as the retail takeoff");
        Check.That(NavMesh.Locate(Downtown, LandingX, Y, LandingZ, out var landing) &&
            landing is { sector: 232, region: 2, polygon: 8, indoors: false } && Math.Abs(landing.floorY + 95 - Y) < 1,
            "The Baldwin Heights marker is on the captured landing polygon");
        Check.That(NavMesh.Locate(Downtown, MidpointX, Y, MidpointZ, out var midpoint) &&
            midpoint is { sector: 232, region: 6, polygon: 12, indoors: false } && Math.Abs(midpoint.floorY + 95 - Y) < 1,
            "The visible aim marker is on the same outdoor floor as the captured route");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.GetValueOrDefault(Jump) == 24 && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.GetValueOrDefault(need.Key) >= need.Value)),
            "Rank 24 and every loaded prerequisite fit stock memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await actor.WaitForViewNamedAsync("QA Retail Jump Midpoint");

        byte[] frame = WireFormat.FromHex(RetailAim);
        Check.That(JumpRecords.TryStart(frame, out var request) && request.IsHyperJump == 1 &&
            request.Destination.Sector == landing.sector && request.Destination.Prefab == landing.region &&
            request.Destination.NavPoly == landing.polygon, "Untouched retail aim frame uses the verified landing polygon");
        int sentAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(frame);
        var echo = await StockAbilityProfile.ObserveAsync(actor, sentAt, packet =>
            packet.Bytes.AsSpan().SequenceEqual(JumpRecords.EncodeClockEcho(request.TimeStamp)), "Matching retail clock echo");
        var prefix = WireFormat.FromHex("02 00 03 01 08 00 80 80 bc 00 d4 75 43 04 00");
        byte[] views = echo.DatagramViewSection ?? throw new InvalidOperationException("Jump echo has no view section.");
        int start = views.AsSpan().IndexOf(prefix);
        Check.That(start >= 0 && echo.DatagramSimTime.HasValue, "The timed reply carries the low peak exactly");
        double duration = BitConverter.ToUInt32(views, start + 15) / 1000.0 - echo.DatagramSimTime!.Value;
        Check.That(Math.Abs(duration - 9.84) < .3, $"The original route travels at retail speed ({duration:F3}s)");
        Check.That(frame.AsSpan(2, 24).SequenceEqual(views.AsSpan(start + 19, 24)), "The jump preserves the literal retail landing");
        Check.Equal((ushort)80, BitConverter.ToUInt16(views, start + 44), "A single stock-form start costs 20 IS");
        await actor.WaitForViewNamedAsync("QA Retail Jump Landing");
        Check.Equal(0, actor.World.Problems.Count, "Retail aim framing");
        return Array.Empty<byte>();
    }
}
