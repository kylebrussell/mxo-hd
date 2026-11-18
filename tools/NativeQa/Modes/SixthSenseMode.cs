using HeadlessClient;
using System.Text.Json;

namespace NativeQa.Modes;

public sealed class SixthSenseMode : NativeQaMode
{
    private const string Near = "QAEnemy", Far = "QAFar", Probe = "QAFriend";

    public override string Name => "sixth-sense";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 149 }, SixthSenseFixture.X, SixthSenseFixture.Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET y={SixthSenseFixture.Y}, alignment=1, healthC=500, healthM=500 WHERE charId=110",
            $"UPDATE characters SET x=CASE handle WHEN '{Near}' THEN {SixthSenseFixture.NearX} WHEN '{Far}' THEN {SixthSenseFixture.FarX} ELSE {SixthSenseFixture.X} END, "
                + $"y={SixthSenseFixture.Y}, z={SixthSenseFixture.Z}, alignment=CASE handle WHEN '{Probe}' THEN 1 ELSE 2 END, "
                + $"healthC=500, healthM=500 WHERE handle IN ('{Near}', '{Far}', '{Probe}')",
        },
        environmentOverrides: new() { ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15" },
        readyMessages: new[] { "CSR110 starts on the verified scan origin, QAEnemy at 8 m and QAFar at 16 m. "
            + "Use Sixth Sense once in Actions. Expect one red minimap dot east for ten seconds, the scan visual/10 IS, "
            + "and no second red dot at QAFar. See docs/ABILITY-PASSIVE-CONSUMERS.md." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Sixth Sense requires a fixture server.");
        foreach (var (name, x) in new[] { (Near, SixthSenseFixture.NearX), (Far, SixthSenseFixture.FarX) })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, helper);
            await DebuffRadiusFixture.EnterFlaggedAsync(helper);
            SixthSenseFixture.AssertPosition(helper, x);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        using var probe = await HeadlessPlayer.LoginAsync(context.Address, Probe, Probe);
        await probe.EnterWorldAsync();
        SixthSenseFixture.AssertPosition(probe, SixthSenseFixture.X);
        var far = await probe.WaitForViewNamedAsync(Far);
        probe.SendRpc(new byte[] { 0x80, 0xb9, 0x95, 0 }.Concat(BitConverter.GetBytes(far.ViewId)).ToArray());
        byte[] map = (await probe.WaitForRpcAsync(SixthSenseFixture.RedPing)).Bytes;
        Check.Equal(11, map.Length, "Fixture scan returns just one red marker in range");
        await context.Helpers[Near].WaitForRpcAsync(SixthSenseFixture.Discovery);
        context.Helpers[Far].SendRpc("81 48");
        await context.Helpers[Far].WaitForRpcAsync("81 49");
        Check.Equal(0, context.Helpers[Far].Received(SixthSenseFixture.Scan).Count, "Far helper is outside the scan");
        Check.Equal(0, probe.World.Problems.Count, "The scan probe has a healthy connection");
        NativeQaReadiness.RequireNoAmbientAggro(server, probe);
        probe.SendRpc("80 fc 01");
        await probe.WaitForRpcAsync("80 fd");
        probe.SendRpc("80 fe 01");
        await probe.WaitForRpcAsync("81 07");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "sixth-sense-bootstrap.json"),
            JsonSerializer.Serialize(new
            {
                Origin = new[] { SixthSenseFixture.X, SixthSenseFixture.Y, SixthSenseFixture.Z },
                NearMetres = 8, FarMetres = 16, Map = WireFormat.ToHex(map),
            }));
        return Array.Empty<byte>();
    }
}
