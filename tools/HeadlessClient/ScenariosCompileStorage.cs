using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CompileStorageDepletion(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("StorageCompiler") { Level = 50,
            Items = new[] { (3, 1271u, 1), (4, 7336u, 1) } });
        const ushort slot = 4; // Deliberately the same number as the bag ingredient.
        const ushort sentinel = 0x1c3;
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={player.CharId} AND slot IN (4,30,{sentinel}); " +
            $"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},4,{CodeStorage.BaseValue(7336)},2,0)," +
            $"({player.CharId},{sentinel},{CodeStorage.BaseValue(7335)},0,0)," +
            $"({player.CharId},30,{unchecked((int)(0x80000000u | 277u << 10))},50,1);");
        await player.EnterWorldAsync();
        player.SkipReceived();

        // The gated deterministic native-QA command shares Compile/TakeOne with 81 2e.
        // Bag is used first, then one of three stored bits: storage remains and gets no delete.
        player.SendChat("?compile 7458");
        await player.WaitForRpcAsync("5f 04 00 01");
        await player.WaitForRpcAsync("69 00 04 00 01 54 0e 84 00 00");
        await player.WaitForTextAsync("Compiled");
        Check.Equal(0, player.Received("5f 04 00 00").Count, "A nonempty archive entry stays visible");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={player.CharId} AND slot=4"), "Two stored bits remain");

        // Genuine compile request consumes the repeated ingredient from storage twice: 2 -> 1 -> removed.
        player.SkipReceived();
        player.SendRpc($"81 2e {Le(7458u << 7)} 09 00 03 01 00 {Le(2u)} {Le(7336u)}");
        await player.WaitForRpcAsync("69 00 04 00 00 54 0e 84 00 00");
        await player.WaitForRpcAsync("5f 04 00 00");
        await player.WaitForRpcAsync($"81 2f 01 ?? {Le(7458u)}");
        Check.Equal(1, player.Received("5f 04 00 00").Count, "Final stored ingredient removed exactly once");
        Check.Equal(1, player.Received("5f 04 00 01").Count, "Storage depletion sends no additional bag deletion");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={player.CharId} AND slot=4"), "Archive row deleted");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={player.CharId} AND goid=7458"), "Two compile results");
        player.SendChat("?compile 7458");
        await player.WaitForTextAsync("Missing to compile");
        Check.Equal(1, player.Received("5f 04 00 00").Count, "Rejected replay sends no phantom removal");
        Check.Equal(0, player.World.Problems.Count, "Original server packets decode and stay ordered");

        // Fresh server + margin login proves the removed entry does not return; keep an unrelated high-slot entry.
        string username = await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING (userId) WHERE charId={player.CharId}");
        using ServerInstance reloaded = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name,
            new Dictionary<string, string> { ["MXO_DEV_COMMANDS"] = "0" });
        using HeadlessPlayer relogged = await HeadlessPlayer.LoginAsync(reloaded.Address, username, player.Handle);
        byte[] known = relogged.CharacterData[0x07];
        byte[] removed = BitConverter.GetBytes(slot).Concat(BitConverter.GetBytes(CodeStorage.Pack(7336, 1))).ToArray();
        byte[] kept = BitConverter.GetBytes(sentinel).Concat(BitConverter.GetBytes(CodeStorage.Pack(7335, 1))).ToArray();
        Check.That(BytePattern.IndexOf(known, removed, 0) < 0, "Depleted entry absent at fresh login");
        Check.That(BytePattern.IndexOf(known, kept, 0) >= 0, "Unrelated storage entry survives");
        await relogged.EnterWorldAsync();
        relogged.SendChat("?codeupload"); // Disabled helper cannot move the fragment results.
        relogged.SendChat("?compile 7458");
        await relogged.WaitForTextAsync("?compile is a dev command");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={player.CharId} AND goid=7458"), "Disabled QA command cannot grant anything");
        Check.Equal(0, relogged.World.Problems.Count, "Fresh server packets decode and stay ordered");

        // The native-QA upload shortcut uses the same hardline path and remains gated above.
        player.SendChat("?codeupload");
        await player.WaitForRpcAsync("81 18 00 00 00 00 00 00");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=7458"), "Enabled upload helper moves code fragments");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT SUM(level+1) FROM char_abilities WHERE char_id={player.CharId} AND ability_id={CodeStorage.BaseValue(7458)}"), "Upload helper stores both results");
    }
}
