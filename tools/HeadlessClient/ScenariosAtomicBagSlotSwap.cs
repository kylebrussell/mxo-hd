namespace HeadlessClient;

public static partial class Scenarios
{
    public static string BagSwapFaultSql(uint character) => "SET SESSION sql_mode=''; " +
        "ALTER TABLE inventory ADD CONSTRAINT qa_swap_second_slot CHECK " +
        $"(charId <> {character} OR slot <> 3 OR goid <> 2399);";

    private static async Task AtomicBagSlotSwap(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("BagSwapOwner")
        {
            X = 17000, Y = 495, Z = 6400, Items = new[] { (3, 512u, 1), (4, 2399u, 2), (5, 531u, 1) }
        });
        await context.Database.ExecuteAsync(BagSwapFaultSql(player.CharId));
        await player.EnterWorldAsync();
        await ProveAtomicBagSlotSwapAsync(player, context.Database);
    }

    public static async Task ProveAtomicBagSlotSwapAsync(HeadlessPlayer player, ThrowawayDatabase db)
    {
        string pick = await Row(3), pill = await Row(4), tool = await Row(5);
        Check.That(pick.Contains(":512:") && pill.Contains(":2399:") && tool.Contains(":531:"),
            "The fixture contains the three expected item identities");

        const string swap = "65 03 00 04 00 00";
        player.SkipReceived();
        player.SendRpc(swap);
        await player.WaitForRpcAsync("65 03 00 03 00 00");
        await player.WaitForTextAsync("Your items were kept.");
        Check.Equal(0, player.Received(swap).Count, "A failed second update sends no swapped projection");
        Check.Equal(pick, await Row(3), "The lock pick is rolled back into slot 3");
        Check.Equal(pill, await Row(4), "The pill stack never leaves slot 4");

        await db.ExecuteAsync("ALTER TABLE inventory DROP CHECK qa_swap_second_slot");
        player.SendRpc(swap);
        await player.WaitForRpcAsync(swap);
        Check.Equal(pill, await Row(3), "The same pill row and count move to slot 3");
        Check.Equal(pick, await Row(4), "The same lock pick row moves to slot 4");

        player.SendRpc("65 04 00 06 00");
        await player.WaitForRpcAsync("65 04 00 06 00 00");
        Check.Equal(pick, await Row(6), "An occupied-to-empty move preserves the row");
        Check.Equal(0L, await Occupied(4), "The vacated slot is empty");
        Check.Equal(tool, await Row(5), "The unrelated row is unchanged");

        player.SendRpc("65 04 00 03 00");
        await player.WaitForRpcAsync("65 04 00 04 00 00");
        await player.WaitForTextAsync("Your items were kept.");
        Check.Equal(pill, await Row(3), "An empty source cannot displace the destination");

        player.SendRpc("65 03 00 05 00 01");
        await player.WaitForRpcAsync("65 03 00 03 00 00");
        await player.WaitForTextAsync("That inventory move is unavailable.");
        Check.Equal(tool, await Row(5), "A central-storage selector cannot swap bag items");
        Check.Equal(pill, await Row(3), "An unsupported move changes neither item");
        Check.Equal(1, player.Received(swap).Count, "Exactly one successful swap was published");
        Check.Equal(0, player.World.Problems.Count, "The same-slot refusal and swap records decode");

        Task<string> Row(int slot) => db.ScalarAsync(
            $"SELECT CONCAT(invId,':',goid,':',count,':',purity) FROM inventory WHERE charId={player.CharId} AND slot={slot}");
        Task<long> Occupied(int slot) =>
            db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND slot={slot}");
    }
}
