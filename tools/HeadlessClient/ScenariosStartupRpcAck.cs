namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task StartupRpcAckGrace(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("StartupAck"), holdWorldSetupAcks: true);
        await player.WaitForRpcAsync("06"); // First world command.
        await player.WaitForRpcAsync(rpc => rpc.DatagramSequence is > 1, "setup RPCs after the world command");

        // Both stock captures sent spawn with ACK 1/01 even though the setup burst was on the wire.
        player.SendRpc("05");
        await Task.Delay(650); // Longer than the steady-state 500 ms, inside the one-second setup window.
        player.World.ReleaseWorldSetupAcks();
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Any(c => c.ViewId == 2),
            "the self spawn", TimeSpan.FromSeconds(15));
        player.SendRpc("0c 01");
        player.SendRpc("81 48");
        await player.WaitForRpcAsync("81 49");
        string log = context.Server.DebugLog();
        Check.That(!log.Split('\n').Any(line => line.Contains("Resending RPC") && line.Contains($"char {player.CharId};")),
            "A delayed startup ACK should retire the original batch without a retry");
        Check.Equal(0, player.World.Problems.Count, "Startup and post-entry traffic stays framed");
    }
}
