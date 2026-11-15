namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task QaLoadoutCommands(ScenarioContext context)
    {
        var player=await context.AddPlayerAsync(new CharacterSeed("LoadoutQA") { Level=50 });
        int Code(ushort id)=>unchecked((int)(0x80000000u | (uint)id<<10));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={player.CharId}; " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},300,{Code(37)},15,0),({player.CharId},301,{Code(7)},5,0)," +
            $"({player.CharId},302,{Code(871)},50,0)");
        await player.EnterWorldAsync(); player.SkipReceived();
        player.SendChat("?qaload 37");
        await player.WaitForRpcAsync("80 b2 25 00 0f 00 2c 01");
        await player.WaitForRpcAsync("80 b2 ca 03 00 00 08 02");
        await player.WaitForRpcAsync(PassiveRecord(37,649,15));
        Check.Equal(1L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={player.CharId} AND slot=300"),"Normal handler loads owned15, including hidden970");
        player.SendChat("?qaunload 37");
        await player.WaitForRpcAsync("80 b3 25 00"); await player.WaitForRpcAsync("80 b3 ca 03");
        await player.WaitForRpcAsync(PassiveRecord(37,649,15,true));
        Check.Equal(0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={player.CharId} AND slot=300"),"Normal unload removes parent, grant and passive");
        foreach(ushort id in new ushort[]{7,871})
        {
            player.SkipReceived(); player.SendChat("?qaload "+id);
            await player.WaitForTextAsync("Your character level is too low");
            await player.WaitForTextAsync("QA loadout submitted");
            Check.Equal(0,player.Received($"80 b2 {Le(id)}").Count,"Canonical level99 gate still refuses "+id);
        }
        player.SkipReceived();
        player.SendChat("?qaload 970"); await player.WaitForTextAsync("exactly one owned archive row");
        player.SendChat("?qaload 37 6111"); await player.WaitForTextAsync("Usage: ?qaload");
        Check.Equal(3L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={player.CharId}"),"No hidden/unowned row synthesized");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT SUM(is_loaded) FROM char_abilities WHERE char_id={player.CharId}"),"All rejected attempts preserve unload state");
        player.SendChat("?qaload 37"); await player.WaitForRpcAsync("80 b2 25 00 0f 00 2c 01");
        // A new server/login must read the result from the ordinary persisted loadout.
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={player.CharId}");
        player.Dispose();
        using var reloaded=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,
            new Dictionary<string,string>{{"MXO_DEV_COMMANDS","0"}});
        using var relogged=await HeadlessPlayer.LoginAsync(reloaded.Address,username,"LoadoutQA");
        await relogged.EnterWorldAsync(); await relogged.WaitForRpcAsync(PassiveRecord(37,649,15));
        relogged.SendChat("?qaunload 37"); await relogged.WaitForTextAsync("QA loadout requires MXO_DEV_COMMANDS=1");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={relogged.CharId} AND slot=300"),"Disabled helper leaves persisted loadout intact");
    }

    private static async Task QaLoadoutDisabled(ScenarioContext context)
    {
        var player=await context.AddPlayerAsync(new CharacterSeed("NoLoadoutQA"){Level=50});
        await player.EnterWorldAsync();
        string before=await context.Database.ScalarAsync($"SELECT COALESCE(SUM(is_loaded),0) FROM char_abilities WHERE char_id={player.CharId}");
        foreach(string command in new[]{"?qaload 7","?qaunload 2"})
        { player.SkipReceived(); player.SendChat(command); await player.WaitForTextAsync("QA loadout requires MXO_DEV_COMMANDS=1"); }
        Check.Equal(before,await context.Database.ScalarAsync($"SELECT COALESCE(SUM(is_loaded),0) FROM char_abilities WHERE char_id={player.CharId}"),"Disabled load and unload preserve all rows");
    }
}
