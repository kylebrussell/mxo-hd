using System.Diagnostics;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task SilentRpcBackoff(ScenarioContext context)
    {
        var player = (await context.AddPlayersInWorldAsync(new CharacterSeed("SilentRpcPeer")))[0];
        await Task.Delay(700); // let the last login ACK flush
        int offset = context.Server.DebugLog().Length;
        var clock = Stopwatch.StartNew();
        // Stock ElapsedTime echo leaves a reliable reply outstanding. Close the UDP socket immediately
        // after the request, even if the reply would otherwise race the automatic ACK.
        string endpoint = player.World.DisconnectAfterRpc(WireFormat.FromHex("81 4d 34 12 00 00"));
        string peer = $", char {player.CharId}; client acked sseq ";
        int Resends() => context.Server.DebugLog()[offset..].Split('\n')
            .Count(line => line.Contains("[NET] Resending RPC ") && line.Contains(peer));
        bool Reaped() => context.Server.FullOutput().Contains("Removed inactive Client with Key " + endpoint);

        await Task.Delay(TimeSpan.FromSeconds(7));
        int before = Resends();
        Check.That(before >= 4 && before <= 15, $"Normal initial retries then backoff. Observed {before}");
        await Task.Delay(TimeSpan.FromSeconds(14));
        int slower = Resends() - before;
        Check.That(slower is >= 2 and <= 5, $"Quiet endpoint retries at roughly the four-second rate. Observed {slower}");
        Check.That(!Reaped(), "The endpoint remains reserved during the normal reap window");
        while (!Reaped() && clock.Elapsed < TimeSpan.FromSeconds(38))
            await Task.Delay(250);
        Check.That(Reaped() && clock.Elapsed >= TimeSpan.FromSeconds(29), "The usual 30-second silence reap still runs");
        int terminal = Resends();
        await Task.Delay(TimeSpan.FromSeconds(5));
        Check.Equal(terminal, Resends(), "No resend after endpoint removal");
        Console.WriteLine($"SILENT RPC PASS: initial {before}, slow {slower}, total {terminal}, endpoint released.");
    }
}
