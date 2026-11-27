namespace HeadlessClient;

public static partial class Scenarios
{
    public const string SplitStackFaultSql = "SET SESSION sql_mode=''; ALTER TABLE inventory ADD CONSTRAINT qa_split_destination " +
        "CHECK (slot <> 4 OR goid <> 7332 OR count <> 4);";

    private static async Task AtomicBagStacks(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("StackOwner")
        {
            X = 17000, Y = 495, Z = 6400, Items = new[] { (1, 512u, 1), (2, 531u, 1), (3, 7332u, 10) }
        });
        await context.Database.ExecuteAsync(SplitStackFaultSql);
        await player.EnterWorldAsync();
        await ProveAtomicBagStacksAsync(player, context.Database);
    }

    public static async Task ProveAtomicBagStacksAsync(HeadlessPlayer player, ThrowawayDatabase db)
    {
        const string kept = "Your items were kept.";
        const string sourceTen = "69 01 03 00 00 00 24 00 00 00";
        const string sourceSeven = "69 01 03 00 00 00 18 00 00 00";
        const string sourceThree = "69 01 04 00 00 00 08 00 00 00";
        const string added = "5e 04 00 a4 1c 00 00 00 00 08 0c 01";
        const string removed = "5f 04 00 01";
        const string merge = "67 04 00 03 00 00";

        player.SkipReceived();
        player.SendRpc("68 03 04 04 00 00 00 00");
        await player.WaitForRpcAsync(sourceTen);
        await player.WaitForTextAsync(kept);
        Check.Equal(0, player.Received("5e 04 00").Count, "The failed split publishes no destination");
        Check.Equal(0, player.Received("69 01 03 00 00 00 14 00 00 00").Count, "The failed split publishes no debit");
        await Slots(10, 0);

        player.SendRpc("68 03 04 03 00 00 00 00");
        await player.WaitForRpcAsync(sourceSeven);
        await player.WaitForRpcAsync(added);
        await Slots(7, 3);

        // Reject the merge's destination update after it deletes the entire source stack.
        await db.ExecuteAsync("SET SESSION sql_mode=''; ALTER TABLE inventory ADD CONSTRAINT qa_merge_destination " +
            "CHECK (slot <> 3 OR goid <> 7332 OR count <> 10)");
        player.SendRpc(merge);
        await player.WaitForRpcAsync(sourceThree);
        await player.WaitForTextAsync(kept);
        Check.Equal(0, player.Received(removed).Count, "The failed merge publishes no source removal");
        Check.Equal(1, player.Received(sourceTen).Count, "The failed merge publishes no destination increment");
        await Slots(7, 3);

        await db.ExecuteAsync("ALTER TABLE inventory DROP CHECK qa_merge_destination");
        player.SendRpc(merge);
        await player.WaitForRpcAsync(sourceTen);
        await player.WaitForRpcAsync(removed);
        await Slots(10, 0);

        player.SendRpc(merge);
        await player.WaitForTextAsync("an empty slot");
        await Slots(10, 0);
        Check.Equal(1, player.Received(added).Count, "Exactly one successful split");
        Check.Equal(1, player.Received(removed).Count, "Exactly one successful merge");
        Check.Equal(0, player.World.Problems.Count, "Personal stack move records decode");

        async Task Slots(int sourceCount, int destinationCount)
        {
            Check.Equal((long)sourceCount, await Count(3), "Committed bag 3 quantity");
            Check.Equal((long)destinationCount, await Count(4), "Committed bag 4 quantity");
        }

        Task<long> Count(int slot) => db.LongAsync(
            $"SELECT COALESCE(SUM(GREATEST(count,1)),0) FROM inventory WHERE charId={player.CharId} AND slot={slot} AND goid=7332");
    }
}
