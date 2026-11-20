namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task StealthActiveDetection(ScenarioContext context)
    {
        const ushort detection = 298, invisibility = 1116;
        var spy = await context.AddPlayerAsync(SixthSenseFixture.At("Veiled") with
        {
            Organization = 1, OwnedLoadedAbilities = new (ushort, ushort)[] { (175, 25) }
        });
        var low = await context.AddPlayerAsync(SixthSenseFixture.At("LowDetect", 17500) with
        {
            Organization = 2, Items = new[] { (2, 2388u, 1) }, OwnedLoadedAbilities = new (ushort, ushort)[] { (detection, 1) }
        });
        var high = await context.AddPlayerAsync(SixthSenseFixture.At("HighDetect", 17800) with
        {
            Organization = 2, Items = new[] { (2, 2388u, 1) }, OwnedLoadedAbilities = new (ushort, ushort)[] { (detection, 50) }
        });
        foreach (var player in new[] { spy, low, high }) await DebuffRadiusFixture.EnterFlaggedAsync(player);

        var lowView = await low.WaitForViewNamedAsync(spy.Handle);
        var highView = await high.WaitForViewNamedAsync(spy.Handle);
        Cast(spy, invisibility);
        await spy.WaitForRpcAsync(ModPattern(invisibility, 115));
        await low.WaitForViewUpdateAsync(1, $"01 01 00 {Le(lowView.ViewId)}");
        await high.WaitForViewUpdateAsync(1, $"01 01 00 {Le(highView.ViewId)}");

        // Compared against 150. Low is at most 103 and high is at least 200.
        Cast(low, detection);
        await low.WaitForViewUpdateAsync(2, "02 80 80 80 10 c3 00");
        Check.Equal(1, CreationsNamed(low, spy.Handle), "Low Detection does not recreate the hidden view");
        Check.Equal(0, spy.Received(ModPattern(invisibility, 115, off: true)).Count, "Invisibility survives low Detection");

        Cast(high, detection);
        await spy.WaitForRpcAsync(ModPattern(invisibility, 115, off: true));
        await spy.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 9d 05 00 28 ??");
        foreach (var observer in new[] { low, high })
        {
            await observer.WaitUntilAsync(() => CreationsNamed(observer, spy.Handle) > 1,
                $"{observer.Handle} gets the revealed player's view", TimeSpan.FromSeconds(10));
            Check.Equal(0, observer.World.Problems.Count, $"{observer.Handle} sees no protocol error");
        }
    }
}
