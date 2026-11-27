using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task RsiModificationPills(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("RsiPillActor")
        {
            Items = new[] { (5, 46954u, 2), (6, 46758u, 1), (7, 46873u, 1) }
        });
        var db = context.Database;
        await db.ExecuteAsync($"DELETE FROM rsivalues WHERE charid={actor.CharId}; " +
            $"INSERT INTO rsivalues(charid,shirt,shirtcolor) VALUES({actor.CharId},6,12)");
        await actor.EnterWorldAsync();
        var watcher = (await context.AddPlayersInWorldAsync(new CharacterSeed("RsiPillObserver") { X = 17120 }))[0];
        var view = await watcher.WaitForViewNamedAsync(actor.Handle);

        actor.SendRpc("80 c6 06 00");
        await actor.WaitForTextAsync("not compatible with your character");
        Cast(actor, 1245);
        await actor.WaitForRpcAsync("80 ba dd 04");
        Check.Equal(0, actor.Received("80 ac dd 04").Count, "Wrong-avatar and unowned use do not start a cast");
        Check.Equal(1L, await BagCount(6), "The incompatible pill stays in the bag");

        actor.SendRpc("80 c6 05 00");
        var started = await actor.WaitForRpcAsync("80 ac dd 04");
        Check.Equal(3f, BitConverter.ToSingle(started.Bytes, 14), "Shipped cast length");
        actor.SendRpc("65 05 00 08 00");
        await actor.WaitForRpcAsync("65 05 00 08 00 00");
        await actor.WaitUntilAsync(() => context.Server.DebugLog().Contains("Item 46954 cast cancelled"),
            "moving the pill cancels the cast", TimeSpan.FromSeconds(8));
        Check.Equal(2L, await BagCount(8), "Cancellation retains the whole moved stack");
        Check.Equal("0 0 6 12", await Traits(), "Cancellation changes no appearance");
        actor.SendRpc("65 08 00 05 00");
        await actor.WaitForRpcAsync("65 08 00 05 00 00");

        // Fail the RSI update after the pending decrement to prove the inventory write rolls back.
        await db.ExecuteAsync($"ALTER TABLE rsivalues ADD CONSTRAINT reject_hair_change CHECK (charid <> {actor.CharId} OR hair <> 2)");
        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("80 ac dd 04");
        await actor.WaitForTextAsync("Your pill was kept.");
        Check.Equal(2L, await BagCount(5), "Rollback preserves the stack");
        Check.Equal("0 0 6 12", await Traits(), "Rollback preserves the appearance");
        Check.Equal(0, actor.Received("69 01 05 00 00 00").Count, "Rollback sends no stack decrement");
        await db.ExecuteAsync("ALTER TABLE rsivalues DROP CONSTRAINT reject_hair_change");

        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("80 ac dd 04");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForTextAsync("Your RSI has been updated.");
        Check.Equal(1L, await BagCount(5), "Exactly one hair pill is consumed");
        Check.Equal("2 0 6 12", await Traits(), "Hair descriptor 107 resolves to packed ordinal 2 and leaves clothing alone");

        await CheckAppearance(2, 0);
        CastSweepItem(actor, 1245, 7); // The 80 b9 item flag and bag slot use the same owned path.
        await actor.WaitForRpcAsync("80 ac dd 04");
        await actor.WaitForRpcAsync("5f 07 00 01");
        await actor.WaitForTextAsync("Your RSI has been updated.");
        await CheckAppearance(2, 2);
        Check.Equal(0L, await BagCount(7), "The last tattoo pill leaves its slot");
        Check.Equal("2 2 6 12", await Traits(), "The tattoo cast preserves existing hair and clothing");

        string username = await db.ScalarAsync($"SELECT username FROM users JOIN characters USING (userId) WHERE charId={actor.CharId}");
        actor.SendRpc("80 fe 00");
        await actor.WaitForRpcAsync("80 fd");
        using var coldServer = await ServerInstance.StartAsync(context.RepositoryRoot, db.Name);
        using var relogged = await HeadlessPlayer.LoginAsync(coldServer.Address, username, actor.Handle);
        Check.Equal(107u, BitConverter.ToUInt32(relogged.CharacterData[2], 12), "Returning Margin preview uses the new hair descriptor");
        Check.Equal(2u, BitConverter.ToUInt32(relogged.CharacterData[2], 20), "Returning Margin preview uses the new tattoo");
        Check.Equal("2 2 6 12", await Traits(), "Normal logout preserves the committed RSI");

        Task<long> BagCount(int slot) => db.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={actor.CharId} AND slot={slot}");
        Task<string> Traits() => db.ScalarAsync($"SELECT CONCAT(hair,' ',tattoo,' ',shirt,' ',shirtcolor) FROM rsivalues WHERE charid={actor.CharId}");

        async Task CheckAppearance(int hair, int tattoo)
        {
            var rsi = new int[22];
            rsi[4] = 6;
            rsi[10] = hair;
            rsi[12] = 12;
            rsi[19] = tattoo;
            string packed = WireFormat.ToHex(PacketsUtils.getRSIBytes(rsi)) + " 00 00";
            await actor.WaitForViewUpdateAsync(2, "02 80 01 " + packed);
            await watcher.WaitForViewUpdateAsync(view.ViewId, "02 08 " + packed);
        }
    }
}
