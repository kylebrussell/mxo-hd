using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task ClientCombatActions(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("Actions") { Level = 50, InnerStrength = 500 });
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},30,{unchecked((int)(0x80000000u | 133u << 10))},50,1);");
        await player.EnterWorldAsync();
        player.SendRpc("3e 6b 85 00 00 00 00 00");
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 85 00 00 00");
        player.SendRpc("43 02 00");
        Cast(player, 10);
        await player.WaitForRpcAsync("80 bd 05 0a 00 00 00 00 00 00 01");
        player.SendRpc("80 bb 0a 00");
        await player.WaitForRpcAsync("80 bd 05 0a 00 00 00 00 00 00 00");
        Cast(player, 1064);
        await player.WaitForRpcAsync("80 bd 05 28 04 00 00 00 00 00 01");
        player.SendRpc("80 bb 14 01");
        player.SendRpc("81 21 03");
        await player.WaitForRpcAsync("81 22 00 00 00 00 00 00 00 00 01 00 00 00");
        Check.Equal(0, player.Received("81 2a").Count, "Container puts never open the marketplace");
        Check.Equal(0, player.Received("80 bd 05 28 04 00 00 00 00 00 00").Count, "Unrelated buff remains on");
        player.SendChat("?dismissbuff 17");
        await player.WaitForTextAsync("Buff dismissal refused: 17");
        player.SendChat("?dismissbuff 1064");
        await player.WaitForRpcAsync("80 bd 05 28 04 00 00 00 00 00 00");
        await player.WaitForTextAsync("Buff dismissal requested: 1064");
    }

    private static async Task BuffDismissDevDisabled(ScenarioContext context)
    {
        var player = (await context.AddPlayersInWorldAsync(new CharacterSeed("NoDismissDev") { Level=50 }))[0];
        Cast(player, 1064);
        await player.WaitForRpcAsync("80 bd 05 28 04 00 00 00 00 00 01");
        player.SendChat("?dismissbuff 1064");
        player.SendRpc("81 48");
        await player.WaitForRpcAsync("81 49");
        Check.Equal(0, player.Received("80 bd 05 28 04 00 00 00 00 00 00").Count,
            "MXO_DEV_COMMANDS=0 blocks the QA shortcut");
        player.SendRpc("80 bb 28 04");
        await player.WaitForRpcAsync("80 bd 05 28 04 00 00 00 00 00 00");
        Check.Equal(0, player.World.Problems.Count, "Native80bb remains available with QA commands disabled");
    }

    private static async Task NativeCraftingRequests(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("CraftActions") { Level = 50,
            Items = new[] { (2, 613u, 1), (3, 1271u, 1), (4, 7336u, 2),
                (5, 696u, 1), (6, 696u, 1), (7, 2435u, 2) } });
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},30,{unchecked((int)(0x80000000u | 277u << 10))},50,1)," +
            $"({player.CharId},31,{unchecked((int)(0x80000000u | 276u << 10))},50,1);");
        await player.EnterWorldAsync();
        // Shotgun 696 starts unknown. The client hint cannot confer ownership of a recipe.
        Check.That(!CraftingHandler.KnownRecipes.Contains(696), "The shotgun recipe must start unknown");
        await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, " +
            "goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY(charId,goid)) ENGINE=InnoDB;" +
            $"ALTER TABLE char_recipes ADD CONSTRAINT reject_decompile_recipe CHECK (charId <> {player.CharId} OR goid <> 696)");
        player.SendRpc("81 31 14 01 00 00 05 02 01");
        await player.WaitForRpcAsync("81 2f 00 05 b8 02 00 00");
        await player.WaitForTextAsync("The item could not be decompiled.");
        Check.Equal(2L, await SourceCount(), "A rejected recipe insert rolls back the source removal");
        Check.Equal(0, player.Received("81 30 b8 02 00 00").Count, "Failed commit never reports a learned recipe");
        await context.Database.ExecuteAsync("ALTER TABLE char_recipes DROP CONSTRAINT reject_decompile_recipe");
        await player.WaitForRpcAsync("80 bd 01 14 01 04 00 00 20 41 00");
        player.SendRpc("81 31 14 01 00 00 05 02 01");
        await player.WaitForRpcAsync("81 30 b8 02 00 00 02");
        await player.WaitForRpcAsync("81 2f 01 05 b8 02 00 00");
        Check.Equal(1L, await SourceCount(), "Learning consumes exactly one source");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_recipes WHERE charId={player.CharId} AND goid=696"), "Learned recipe persists");
        Check.Equal(2L, await FragmentCount(), "Learning grants no fragments");

        string values = string.Join(",", Enumerable.Range(1, 96).Except(new[] { 2, 3, 4, 6, 7 })
            .Select(slot => $"({player.CharId},11666,{slot},1)"));
        await context.Database.ExecuteAsync($"INSERT INTO inventory(charId,goid,slot,count) VALUES {values}");
        const string recover = "81 31 14 01 00 00 06 02 00";
        await player.WaitForRpcAsync("80 bd 01 14 01 04 00 00 20 41 00");
        player.SendRpc(recover);
        await player.WaitForRpcAsync("81 2f 00 06 b8 02 00 00");
        await player.WaitForTextAsync("Make room for the recovered fragments.");
        Check.Equal(1L, await SourceCount(), "The full bag retains the source");
        Check.Equal(2L, await FragmentCount(), "A full bag grants no partial recovery");

        // Deny the last fragment insert after prior inserts and a source removal in the same transaction.
        await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={player.CharId} AND goid=11666;" +
            "SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION'; " +
            $"ALTER TABLE inventory ADD CONSTRAINT reject_recovered_fragment CHECK (charId <> {player.CharId} OR goid <> 2451)");
        await player.WaitForRpcAsync("80 bd 01 14 01 04 00 00 20 41 00");
        player.SendRpc(recover);
        await player.WaitForRpcAsync("81 2f 00 06 b8 02 00 00");
        await player.WaitForTextAsync("The item could not be decompiled.");
        Check.Equal(1L, await SourceCount(), "The failed grant rolls back the source");
        Check.Equal(2L, await FragmentCount(), "The failed grant rolls back every fragment");
        Check.Equal(0, player.Received("5e 01 00 8c 09 00 00").Count, "Rollback publishes no new bag item");
        await context.Database.ExecuteAsync("SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION'; " +
            "ALTER TABLE inventory DROP CONSTRAINT reject_recovered_fragment");

        await player.WaitForRpcAsync("80 bd 01 14 01 04 00 00 20 41 00");
        player.SendRpc(recover);
        var removal = await player.WaitForRpcAsync("5f 06 00 01");
        var grant = await player.WaitForRpcAsync("5e 01 00 8c 09 00 00 00 00 00 00 01");
        var nextGrant = await player.WaitForRpcAsync("5e 05 00 91 09 00 00 00 00 00 00 01");
        var recycledSlot = await player.WaitForRpcAsync("5e 06 00 93 09 00 00 00 00 00 00 01");
        var stacked = await player.WaitForRpcAsync("69 01 07 00 00 00 08 00 00 00");
        var finished = await player.WaitForRpcAsync("81 2f 01 06 b8 02 00 00");
        Check.That(removal.DatagramSequence.HasValue && new[] { grant, nextGrant, recycledSlot, stacked, finished }
            .All(record => record.DatagramSequence == removal.DatagramSequence && record.ListCounter == removal.ListCounter),
            "The source removal, grants and result share a single RPC list");
        Check.Equal(0L, await SourceCount(), "Committed recovery spends the second source");
        Check.Equal(6L, await FragmentCount(), "The four recipe fragments arrive once in the bag");
        Check.Equal(1, player.Received("81 30 b8 02 00 00").Count, "Recovery does not relearn the recipe");
        Check.Equal(1000L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"), "Decompile spends no Info");

        CompilerTool(player,3,true);
        await player.WaitForRpcAsync(ModPattern(969,968));
        string compile = $"81 2e {Le(7458u << 7)} 09 00 03 01 00 {Le(2u)} {Le(7336u)}";
        player.SendRpc(compile);
        await player.WaitForRpcAsync($"81 2f 01 ?? {Le(7458u)}");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=7336"), "Both ingredients consumed");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=7458"), "One result granted");
        Check.Equal(880L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"), "Client/script .12 info cost");
        player.SendRpc(compile);
        await player.WaitForTextAsync("need the recipe's ingredients");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={player.CharId} AND goid=7458"), "Replay did not duplicate result");
        Check.Equal(880L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"), "Rejected replay spent nothing");

        Task<long> SourceCount() => context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=696");
        Task<long> FragmentCount() => context.Database.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={player.CharId} AND goid IN (2435,2444,2449,2451)");
    }
}
