using System.Diagnostics;
namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// A retail jack-out (80 fc 02, the server's 80 fd after 30 s, then 80 fe 00) releases the character at once, so
    /// an immediate relog succeeds. Closing the sockets without 80 fe 00 still holds the character for the idle timeout.
    /// </summary>
    private static async Task JackoutImmediateRelog(ScenarioContext context)
    {
        var p = await context.AddPlayerAsync(new CharacterSeed("JackRelog"));
        await p.EnterWorldAsync();
        p.SkipReceived();
        p.SendRpc("80 fc 02");
        await p.WaitForRpcAsync("80 fd", TimeSpan.FromSeconds(40));
        p.SkipReceived();
        p.SendRpc("80 fe 00");
        await p.WaitForRpcAsync("80 fd");
        uint charId = p.CharId;
        string user = "user" + charId, handle = p.Handle;
        p.Dispose();

        var clock = Stopwatch.StartNew();
        HeadlessPlayer? again = null;
        Exception? last = null;
        while (again == null && clock.Elapsed < TimeSpan.FromSeconds(3))
        {
            try { again = await HeadlessPlayer.LoginAsync(context.Server.Address, user, handle); }
            catch (InvalidOperationException error) when (error.Message.Contains("already in use"))
            {
                last = error;
                await Task.Delay(250);
            }
        }
        Check.That(again != null, "Relog within 3 s of 80 fe 00 (last refusal: " + last?.Message + ")");
        Console.WriteLine($"JACKOUT RELOG accepted after {clock.ElapsedMilliseconds} ms");
        await again!.EnterWorldAsync();
        again.SendRpc("81 48");
        await again.WaitForRpcAsync("81 49");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT is_online FROM characters WHERE charId={charId}"),
            "The relogged character is online once");
        Check.Equal(0, again.World.Problems.Count, "Relog framing");

        // A plain close is not a quit: the character stays reserved until the idle reap.
        again.Dispose();
        await Task.Delay(1500);
        bool held = false;
        try { (await HeadlessPlayer.LoginAsync(context.Server.Address, user, handle)).Dispose(); }
        catch (InvalidOperationException error) when (error.Message.Contains("already in use")) { held = true; }
        Check.That(held, "Closing without 80 fe 00 still holds the character for the idle timeout");
    }
}
