using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedJumpRedirectMode : NativeQaMode
{
    private const ushort District = 3, Jump = 11, Sprint = 184;
    private const int Level = 19, X = 31391, Y = -505, Z = 16331;
    // Retail Wan Chai proxy_026 CSEQ 2559. The fixture plays this without editing the destination or timestamp.
    private const string RetailStart = "80 c2 00 00 00 40 bb 77 e2 40 00 00 00 00 00 44 91 c0 00 00 00 60 ba 41 d0 " +
        "40 5c 01 33 00 0c 00 00 f0 20 46 01 1a f5 cf 04";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Jump, Sprint).Order().ToArray();

    public override string Name => "ability-parity-awakened-jump-redirect";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={Level} WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET district='international',districtId={District},level={Level}," +
            $"exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=62,innerStrC=100,innerStrM=100 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Jump, Sprint }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [JumpRedirectQa.Mode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "2,11,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Rank-19 Hyper-Jump in Wan Chai. CSR (31391,-505,16331), outdoor NAV 348/124/0, yaw 3e. The original retail destination (37821.85,-1105,16646.91), outdoor NAV 348/51/12, is ~64 m ahead. Leave Sprint off.",
            "From the spawn, use normal-view Ctrl+Space. Wait about two seconds then type ?jret before landing. This dev-only command feeds the exact retained C4 into the server so the stock client can show the resulting airborne redirect. It does not require an unverified in-air stock gesture.",
            "Expect the one stock start and clock echo, IS 100->80, [NAV] Jump cancel to the original stand, and [NAV] Hyper-Jump redirect from a higher, in-flight point with the remainder of the original travel time. Save the 03 09 view and observe the landing near the start. No further IS debit or clock echo from the redirect.",
            "If chat is blocked during flight, capture the start and landing anyway and record the missing redirect. A correction during the first second can be the stock takeoff report. It is distinct from the fixture command and must stay silent."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        byte[] start = WireFormat.FromHex(RetailStart), redirect = WireFormat.FromHex(JumpRedirectQa.RetailCancel);
        Check.That(JumpRecords.TryStart(start, out var jump) && jump.IsHyperJump == 1, "The retail start is a flag-1 jump");
        Check.That(JumpRecords.TryCancel(redirect, out var cancel), "The retail redirect is a full C4");
        RequireStand(X, Y, Z, 348, 124, 0);
        RequireStand(jump.Destination.X, jump.Destination.Y, jump.Destination.Z, 348, 51, 12);
        RequireStand(cancel.Destination.X, cancel.Destination.Y, cancel.Destination.Z, 348, 124, 0);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(rows.GetValueOrDefault(Jump) == Level && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => rows.GetValueOrDefault(need.Key) >= need.Value)),
            "Rank-19 Jump and all learned prerequisites fit memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(start);
        var echo = await StockAbilityProfile.ObserveAsync(actor, cursor, packet =>
            packet.Bytes.AsSpan().SequenceEqual(JumpRecords.EncodeClockEcho(jump.TimeStamp)), "The retail C2 has one matching C3");
        byte[] viewAtStart = echo.DatagramViewSection ?? throw new InvalidOperationException("Jump echo needs its view section.");
        int startAt = viewAtStart.AsSpan().IndexOf(WireFormat.FromHex("02 00 03 01 08 00 80 80 bc 00 f0 20 46 04 00"));
        Check.That(startAt >= 0, "The initial reply includes the original full-peak route");
        uint deadline = BitConverter.ToUInt32(viewAtStart, startAt + 15);
        Check.Equal((ushort)80, BitConverter.ToUInt16(viewAtStart, startAt + 44), "Takeoff spends 20 IS");

        await Task.Delay(TimeSpan.FromMilliseconds(1250));
        int viewCursor = actor.World.ViewSnapshot().Count;
        int rpcCursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(redirect);
        var header = WireFormat.FromHex("02 00 03 09 08 00");
        var mask = WireFormat.FromHex("80 80 b8 14 00");
        byte[]? reply = null;
        int offset = -1;
        await actor.WaitUntilAsync(() =>
        {
            foreach (var view in actor.World.ViewSnapshot().Skip(viewCursor))
            {
                int index = view.AsSpan().IndexOf(header);
                if (index < 0 || view.Length < index + 59 || !view.AsSpan(index + 22, mask.Length).SequenceEqual(mask)) continue;
                reply = view;
                offset = index;
                return true;
            }
            return false;
        }, "The retail C4 produces its airborne 03 09 redirect", HeadlessPlayer.DefaultTimeout);
        var response = reply!;
        float currentX = BitConverter.ToSingle(response, offset + 6), currentY = BitConverter.ToSingle(response, offset + 10);
        Check.That(currentX > X && currentX < jump.Destination.X && currentY > Y + 500,
            "Redirect begins from a point airborne along the route");
        uint stamp = BitConverter.ToUInt32(response, offset + 18);
        Check.That(unchecked((int)(deadline - stamp)) > 1000, "The redirect occurs during the original flight");
        Check.Equal(deadline, BitConverter.ToUInt32(response, offset + 27), "Redirect preserves the established flight deadline");
        Check.That(response.AsSpan(offset + 31, 24).SequenceEqual(redirect.AsSpan(2, 24)),
            "All three returned coordinates retain the literal stock C4 bits");
        Check.Equal((ushort)80, BitConverter.ToUInt16(response, offset + 56), "Redirect leaves Inner Strength unchanged");
        Check.That(!actor.World.RpcSnapshot().Skip(rpcCursor).Any(packet => packet.Opcode == 0xc3),
            "The redirect has no second clock echo");
        int repeat = actor.World.RpcSnapshot().Count;
        actor.SendRpc(start);
        await StockAbilityProfile.ObserveAsync(actor, repeat, packet =>
            packet.Bytes.AsSpan().SequenceEqual(JumpRecords.EncodeClockEcho(jump.TimeStamp)), "The stock fixture repeat enters a new flight");
        await Task.Delay(TimeSpan.FromMilliseconds(1250));
        actor.SendChat("?jret");
        await actor.WaitForTextAsync("Fixture replayed the captured Wan Chai jump redirect.");
        Check.Equal(0, actor.World.Problems.Count, "Retail redirect wire framing");
        return Array.Empty<byte>();
    }

    private static void RequireStand(double x, double y, double z, ushort sector, ushort region, ushort polygon)
    {
        Check.That(NavMesh.Locate(District, x, y, z, out var stand) && !stand.indoors &&
            stand.sector == sector && stand.region == region && stand.polygon == polygon && Math.Abs(stand.floorY + 95 - y) < 1,
            $"Wan Chai stand ({x},{y},{z}) is on outdoor NAV {sector}/{region}/{polygon}");
    }
}
