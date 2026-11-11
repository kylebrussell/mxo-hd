using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>/ignore and its removal (80 e0 / 80 e2), and the list coming back at the next login.</summary>
    private static async Task IgnoreListAddRemoveAndRelog(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        HeadlessPlayer alice = players[0];
        string prefix = "SOE+MXO+";

        alice.SendRpc(new byte[] { 0x80, 0xe0, 4, 0 }.Concat(Sized("bob")).ToArray());
        RpcRecord added = await alice.WaitForRpcAsync("80 e1 08 00 84 00 00 8e");
        string world = await context.Database.ScalarAsync("SELECT name FROM worlds ORDER BY worldId LIMIT 1");
        Check.Equal(prefix + world + "+Bob", StringAtOffset(added.Bytes, 2), "The answer names Bob as a local player, spelt as his handle is");

        alice.SendRpc(new byte[] { 0x80, 0xe0, 4, 0 }.Concat(Sized("Nobody")).ToArray());
        await alice.WaitForRpcAsync("80 e1 08 00 41 00 00 0e");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM ignorelist WHERE charId={alice.CharId}"), "Ignores stored");

        // A fresh server on the same database: the list comes with the friend list at world entry
        string username = await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING (userId) WHERE charId={alice.CharId}");
        using ServerInstance reloaded = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name);
        using HeadlessPlayer relogged = await HeadlessPlayer.LoginAsync(reloaded.Address, username, alice.Handle);
        await relogged.EnterWorldAsync();
        RpcRecord listed = await relogged.WaitForRpcAsync("80 e1 08 00 3d 00 00 8e");
        Check.That(StringAtOffset(listed.Bytes, 2).EndsWith("+Bob"), "Bob listed at login");

        relogged.SendRpc(new byte[] { 0x80, 0xe2, 4, 0 }.Concat(Sized(StringAtOffset(listed.Bytes, 2))).ToArray());
        await relogged.WaitForRpcAsync("80 e3 08 00 84 00 00 8e");
        relogged.SendRpc(new byte[] { 0x80, 0xe2, 4, 0 }.Concat(Sized("Bob")).ToArray());
        await relogged.WaitForRpcAsync("80 e3 08 00 58 00 00 0e");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM ignorelist WHERE charId={alice.CharId}"), "Ignores after removing");
    }
}
