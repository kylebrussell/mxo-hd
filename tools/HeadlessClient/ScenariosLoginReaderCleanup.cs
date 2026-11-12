namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task LoginReaderCleanup(ScenarioContext context)
    {
        bool rejected = false;
        try
        {
            // Signed MEDIUMINT accepts -1. The valid yaw succeeds before UInt16 health mapping fails.
            // LoginAsync owns and disposes its margin/world sockets when the UDP reply never arrives.
            using var unexpected = await context.AddPlayerAsync(new CharacterSeed("ReaderBroken")
                { Rotation = 192, Health = -1, MaxHealth = 300 });
        }
        catch (OperationCanceledException) { rejected = true; }
        Check.That(rejected, "the malformed disposable actor fails initial UDP login");
        long badId = await context.Database.LongAsync("SELECT charId FROM characters WHERE handle='ReaderBroken'");
        Check.Equal(-1L, await context.Database.LongAsync($"SELECT healthC FROM characters WHERE charId={badId}"), "no invented health repair");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT is_online FROM characters WHERE charId={badId}"), "failed actor never entered world");

        string output = context.Server.FullOutput();
        var failures = context.Server.OutputLines($"[CLIENT] Packet handling failed for charId {badId}:")
            .Select(line => line.TrimEnd('\r')).ToArray();
        Check.That(failures.Length > 0, "the actual provider-backed mapping failure was logged");
        foreach (var group in failures.GroupBy(line => line, StringComparer.Ordinal))
        {
            string line = group.Key;
            Check.That(line.Contains("System.OverflowException") && line.Contains("UInt16"), "exact expected conversion exception");
            int start = output.IndexOf(line, StringComparison.Ordinal);
            string stack = output[(start + line.Length)..];
            int next = stack.IndexOf("\n[", StringComparison.Ordinal); if (next >= 0) stack = stack[..next];
            Check.That(stack.Contains("System.Decimal.ToUInt16") && stack.Contains("MyWorldDbAccess.SetPlayerValues") &&
                stack.Contains("PlayerHandler.processInitUDPSession"), "full owned initial-UDP mapping stack");
            // Exact observed line AND occurrence count, after LoginAsync has disposed the failed client's sockets.
            context.AcknowledgeExpectedServerDiagnostic(line, group.Count());
        }

        var healthy = (await context.AddPlayersInWorldAsync(new CharacterSeed("ReaderHealthy")
            { X = 23828, Y = 495, Z = 13984, Rotation = 64, Health = 300, MaxHealth = 300 }))[0];
        Check.Equal((byte)64, healthy.World.CreationSnapshot().Last(v => v.ViewId == 2).Attributes["Yaw Interval"][0], "valid yaw remains native-equivalent");
        Check.Equal(300L, await context.Database.LongAsync($"SELECT healthC FROM characters WHERE charId={healthy.CharId}"), "healthy row loads normally");
        healthy.SendRpc("80 fe 00"); await healthy.WaitForRpcAsync("80 fd");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT is_online FROM characters WHERE charId={healthy.CharId}"), "normal quit releases healthy actor");
        healthy.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(33)); // ordinary >30s world/margin reservation cleanup
        using (var relog = await HeadlessPlayer.LoginAsync(context.Server.Address, "user" + healthy.CharId, healthy.Handle))
        {
            await relog.EnterWorldAsync();
            Check.Equal((byte)64, relog.World.CreationSnapshot().Last(v => v.ViewId == 2).Attributes["Yaw Interval"][0], "ordinary same-server relog");
            Check.Equal(0, relog.World.Problems.Count, "relog wire parser");
        }
        Check.That(!context.Server.FullOutput().Contains("already in use", StringComparison.OrdinalIgnoreCase), "no poisoned connection or character reservation");
        Check.That(!context.ServerProblems().Any(), "no additional or repeated unacknowledged server failure");
        Check.Equal(0, healthy.World.Problems.Count, "healthy login wire parser");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "mxo-login-reader-cleanup-success-debug.log"),
            context.Server.FullOutput() + "\n--- DebugLog ---\n" + context.Server.DebugLog());
        Console.WriteLine($"  login reader: real signed health-1 row, valid yaw192, exact initial-UDP UInt16 overflow ({failures.Length} acknowledged occurrence(s)); failed sockets disposed; healthy login/logout/relog on same server; no already-in-use error, no health repair.");
    }
}
