namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AtomicLuggableTransfers(ScenarioContext context)
    {
        var carrier = await context.AddPlayerAsync(new CharacterSeed("LuggableOwner")
        {
            X = 17000, Y = 495, Z = 6400, Level = 50, Items = new[] { (3, 531u, 1), (5, 596u, 1), (6, 47276u, 1) }
        });
        await context.Database.InstallLuggableTransferFailuresAsync(carrier.CharId);
        await carrier.EnterWorldAsync();
        var observer = await context.AddPlayerAsync(new CharacterSeed("LuggableWitness") { X = 17060, Y = 495, Z = 6400 });
        await observer.EnterWorldAsync();
        await ProveAtomicLuggableTransfersAsync(carrier, context.Database, observer);

        // The same item and exact drop request can be retried after a temporary write failure.
        await context.Database.ExecuteAsync("ALTER TABLE world_luggables DROP CHECK qa_luggable_drop");
        carrier.SendRpc("5c 05 00 01");
        await carrier.WaitForRpcAsync("5f 05 00 01");
        Check.Equal(1L, await context.Database.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=596"),
            "The exact drop retry creates one ground row");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={carrier.CharId} AND goid=596"),
            "The exact drop retry removes the carried object");
    }

    public static async Task ProveAtomicLuggableTransfersAsync(
        HeadlessPlayer carrier, ThrowawayDatabase db, HeadlessPlayer? observer = null)
    {
        string original = await BagRow(5);
        Check.That(original.Contains(":596:") && (await BagRow(6)).Contains(":47276:") &&
            (await BagRow(3)).Contains(":531:") && await BagRow(1) == "" && await BagRow(2) == "",
            "Known inventory layout and open first two slots");

        carrier.SkipReceived();
        carrier.SendRpc("5c 05 00 01");
        await carrier.WaitForRpcAsync("65 05 00 05 00 00");
        await carrier.WaitForTextAsync("Your item was kept.");
        Check.Equal(original, await BagRow(5), "A failed ground insert restores the identical bag row");
        Check.Equal(0L, await GroundRows(596), "A refused drop does not create a persisted copy");
        Check.Equal(0, carrier.Received("5f 05 00 01").Count + carrier.ViewsOfGoid(596).Count +
            (observer?.ViewsOfGoid(596).Count ?? 0), "A refused drop never removes or projects the object");

        carrier.SendRpc("5c 06 00 01");
        await carrier.WaitForRpcAsync("5f 06 00 01");
        await carrier.WaitUntilAsync(() => carrier.ViewsOfGoid(47276).Count == 1 &&
            (observer == null || observer.ViewsOfGoid(47276).Count == 1), "Both clients receive the committed ground tap",
            HeadlessPlayer.DefaultTimeout);
        var dropped = carrier.ViewsOfGoid(47276).Single();
        var watched = observer?.ViewsOfGoid(47276).Single();
        Check.Equal(1L, await GroundRows(47276), "Exactly one persisted ground tap");
        Check.Equal("", await BagRow(6), "The successful drop clears bag 6");

        string take = $"80 c7 {MissionNpcId(dropped)} 04 00";
        carrier.SendRpc(take);
        await carrier.WaitForTextAsync("The luggable is still on the ground.");
        Check.Equal("", await BagRow(1), "The failed ground deletion rolls back the inserted tap");
        Check.Equal(1L, await GroundRows(47276), "The same tap remains available for retry");
        Check.Equal(0, carrier.Received("5e ?? ?? ac b8 00 00").Count, "The failed pickup sends no inventory add");
        Check.That(!carrier.World.ViewSnapshot().Any(DeleteOf(dropped).Occurs) &&
            (observer == null || !observer.World.ViewSnapshot().Any(DeleteOf(watched!).Occurs)),
            "The failed pickup leaves both views in place");

        carrier.SendRpc("65 03 00 01 00 00");
        await carrier.WaitForRpcAsync("65 03 00 01 00 00");
        carrier.SendRpc(take);
        await carrier.WaitForRpcAsync("5e 02 00 ac b8 00 00");
        await carrier.WaitForViewUpdateAsync(1, $"01 01 00 {Le(dropped.ViewId)}");
        if (observer != null) await observer.WaitForViewUpdateAsync(1, $"01 01 00 {Le(watched!.ViewId)}");
        Check.Equal(0L, await GroundRows(47276), "Pickup removes the persisted ground tap");
        Check.That((await BagRow(2)).Contains(":47276:"), "Retry places the tap in the next free slot");
        carrier.SendRpc(take);
        carrier.SendRpc("81 48");
        await carrier.WaitForRpcAsync("81 49");
        Check.Equal(1, carrier.Received("5e ?? ?? ac b8 00 00").Count, "The stale handle cannot award a second tap");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={carrier.CharId} AND goid=47276"),
            "Exactly one tap is carried");
        Check.Equal(0, carrier.World.Problems.Count + (observer?.World.Problems.Count ?? 0), "Both clients decode the transfer replies");

        Task<string> BagRow(int slot) => db.ScalarAsync($"SELECT CONCAT(invId,':',goid,':',count,':',purity) " +
            $"FROM inventory WHERE charId={carrier.CharId} AND slot={slot}");
        Task<long> GroundRows(int goid) => db.LongAsync($"SELECT COUNT(*) FROM world_luggables WHERE district=1 AND goid={goid}");
        static BytePattern DeleteOf(ViewCreation view) => BytePattern.Parse($"01 00 01 01 00 {Le(view.ViewId)}");
    }
}
