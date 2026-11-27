namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DatamineTapCarryFx(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("TapObserver") { X = 17060, Y = 495, Z = 6400 },
            new CharacterSeed("TapCarrier")
            {
                X = 17000, Y = 495, Z = 6400, Level = 50,
                Items = new[] { (5, 47276u, 1), (6, 596u, 1) }
            });
        await ProveDatamineTapCarryFxAsync(players[1], context.Database, players[0]);
    }

    public static async Task ProveDatamineTapCarryFxAsync(HeadlessPlayer carrier, ThrowawayDatabase db,
        HeadlessPlayer? observer = null)
    {
        var selfFx = BytePattern.Parse("02 00 02 80 80 80 80 30 78 00 00 04");
        BytePattern? observerFx = null;
        if (observer != null)
        {
            var view = await observer.WaitForViewNamedAsync(carrier.Handle);
            observerFx = BytePattern.Parse($"{Le(view.ViewId)} 02 80 80 80 0c 78 00 00 04");
        }

        await WaitForPulses(2);
        int carrierBeforeDrop = Count(carrier, selfFx);
        int observerBeforeDrop = Count(observer, observerFx);
        carrier.SendRpc("5c 05 00 01");
        await carrier.WaitForRpcAsync("5f 05 00 01");
        await carrier.WaitUntilAsync(() => carrier.ViewsOfGoid(47276).Count > 0,
            "the dropped tap has a ground view", TimeSpan.FromSeconds(8));
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=47276 AND district=1"),
            "The drop commits one ground tap");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={carrier.CharId} AND slot=6 AND goid=596"),
            "The unrelated light luggable stays in the bag");

        await Task.Delay(TimeSpan.FromSeconds(5));
        Check.Equal(carrierBeforeDrop, Count(carrier, selfFx), "The ground tap stops the carrier's pulses");
        Check.Equal(observerBeforeDrop, Count(observer, observerFx), "The ground tap stops observed pulses");

        var dropped = carrier.ViewsOfGoid(47276).Last();
        carrier.SendRpc($"80 c7 {MissionNpcId(dropped)} 04 00");
        await carrier.WaitForRpcAsync("5e ?? ?? ac b8 00 00");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={carrier.CharId} AND goid=47276"),
            "The pickup restores one carried tap");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=47276"),
            "The pickup removes the ground copy");

        await WaitForPulses(carrierBeforeDrop + 2);
        Check.Equal(0, Count(observer, selfFx), "The observer does not carry or self-play the tap effect");
        Check.Equal(0, carrier.World.Problems.Count + (observer?.World.Problems.Count ?? 0),
            "The stock drop, pickup and effect frames decode");

        async Task WaitForPulses(int count)
        {
            await carrier.WaitUntilAsync(() => Count(carrier, selfFx) >= count,
                $"{count} own tap effect updates", TimeSpan.FromSeconds(12));
            if (observer != null)
                await observer.WaitUntilAsync(() => Count(observer, observerFx) >= count,
                    $"{count} remote tap effect updates", TimeSpan.FromSeconds(4));
        }

        static int Count(HeadlessPlayer? player, BytePattern? fx) =>
            player == null || fx == null ? 0 : player.World.ViewSnapshot().Count(fx.Occurs);
    }
}
