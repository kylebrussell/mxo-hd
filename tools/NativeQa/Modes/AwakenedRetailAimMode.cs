using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedRetailAimMode : NativeQaMode
{
    private const ushort Downtown = 2, Jump = 11, Sprint = 184;
    private const int Level = 25;
    private const int RoofX = -8239, RoofY = 31495, RoofZ = -23473;
    private const int TerraceX = -11400, TerraceY = 25095, TerraceZ = -23885;
    private const int ReplayX = -24230, ReplayY = 1895, ReplayZ = 31256;
    private const int ReplayLandingX = -23651, ReplayLandingZ = 23406;
    private static readonly byte RoofYaw = MobAggro.YawTowards(RoofX, RoofZ, TerraceX, TerraceZ);
    // Retail proxy_026, CSEQ 2280 and SSEQ 1842. The low-peak request is replayed without edits.
    private const string RetailAim = "80 c2 00 00 00 60 a6 18 d7 c0 00 00 00 00 ff 9b 9d 40 00 00 00 40 7a db d6 " +
        "40 e8 00 02 00 08 00 00 d4 75 43 01 66 28 be 04";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Jump, Sprint).Order().ToArray();

    public override string Name => "ability-parity-awakened-retail-aim";
    public override NativeQaModePlan Plan { get; } = new(Loadout, RoofX, RoofZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=IF(ability_id={AbilityMessages.AbilityGameObject(Jump)},24,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET district='downtown',districtId={Downtown},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"x={RoofX},y={RoofY},z={RoofZ},rotation={RoofYaw},innerStrC=100,innerStrM=100 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Jump, Sprint }, testMobFixture: "awakened-retail-aim.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "2,11,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR starts at the Hampton Green roof edge ({RoofX},{RoofY},{RoofZ}), outdoor NAV 481/4/30, facing QA Aim Terrace ({TerraceX},{TerraceY},{TerraceZ}), outdoor NAV 481/4/47. The terrace is 31.88 m ahead and 64 m below. Keep Sprint off. Do not walk off the roof.",
            "From the spawn, open Jump aim in page 2 slot 1. Look for QA Aim Terrace on the lower surface ahead. Real-click the terrace next to the marker. Stay in aim mode only while finding that surface. Save a screenshot of the aim view if the terrace is out of frame.",
            "Save the flag-1 C2, [NAV] peak/horizontal/travel, [VITALS] IS 100 to 80 on an accepted commit and the landing. At the marker with peak 1000 the distance branch takes about 3.99 s, compared with 2.856 s for a nearby aim. If the client picks another surface, save its exact request and outcome. After the jump settles, strafe on the landing.",
            $"To repeat from the roof, use ?tp {RoofX} {RoofY} {RoofZ} 1e1 4 1e - {RoofYaw:x}. Each accepted Hyper-Jump costs 20 IS. The original 78.7 m retail aim remains a separate headless replay."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        RequireOutdoorStand(RoofX, RoofY, RoofZ, 481, 4, 30);
        RequireOutdoorStand(TerraceX, TerraceY, TerraceZ, 481, 4, 47);
        RequireOutdoorStand(ReplayX, ReplayY, ReplayZ, 333, 4, 24);
        RequireOutdoorStand(ReplayLandingX, ReplayY, ReplayLandingZ, 232, 2, 8);
        // Sample the open section of the downward line. Outdoor NAV establishes the stands and checks for
        // intervening mapped floors. The stock client still has to establish camera framing and picking.
        for (int step = 1; step < 20; step++)
        {
            double f = step / 20.0;
            Check.That(!NavMesh.Locate(Downtown, RoofX + f * (TerraceX - RoofX),
                RoofY + 800 + f * (TerraceY - RoofY - 800), RoofZ + f * (TerraceZ - RoofZ), out _),
                $"Mapped geometry leaves the roof-to-terrace line open at sample {step}");
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.GetValueOrDefault(Jump) == 24 && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.GetValueOrDefault(need.Key) >= need.Value)),
            "Rank 24 and every loaded prerequisite fit stock memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await actor.WaitForViewNamedAsync("QA Aim Terrace");
        actor.SendChat($"?tp {ReplayX} {ReplayY} {ReplayZ} 14d 4 18");
        await actor.WaitForViewNamedAsync("QA Retail Jump Midpoint");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        byte[] frame = WireFormat.FromHex(RetailAim);
        Check.That(JumpRecords.TryStart(frame, out var request) && request.IsHyperJump == 1 &&
            request.Destination.Sector == 232 && request.Destination.Prefab == 2 && request.Destination.NavPoly == 8, "Untouched retail aim frame uses the verified landing polygon");
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

    private static void RequireOutdoorStand(int x, int y, int z, ushort sector, ushort region, ushort polygon)
    {
        Check.That(NavMesh.Locate(Downtown, x, y, z, out var stand) && !stand.indoors &&
            stand.sector == sector && stand.region == region && stand.polygon == polygon && Math.Abs(stand.floorY + 95 - y) < 1,
            $"Outdoor NAV stand ({x},{y},{z}) is on {sector}/{region}/{polygon}");
    }
}
