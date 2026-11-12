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
            Items = new[] { (3, 1271u, 1), (4, 7336u, 2), (5, 2399u, 1) } });
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},30,{unchecked((int)(0x80000000u | 277u << 10))},50,1)," +
            $"({player.CharId},31,{unchecked((int)(0x80000000u | 276u << 10))},50,1);");
        await player.EnterWorldAsync();
        player.SendRpc("81 31 14 01 00 00 05 6b 00");
        await player.WaitForRpcAsync("81 30 5f 09 00 00 01");
        Check.That(player.Received("5f 05 00 01").Count > 0, "Decompiled source left the bag");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_recipes WHERE charId={player.CharId} AND goid=2399"), "Learned recipe persists");
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
    }
}
