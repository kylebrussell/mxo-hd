using HeadlessClient;
using System.Text.RegularExpressions;

namespace NativeQa;

internal static class NativeQaReadiness
{
    public static async Task<byte[]> BootstrapAsync(NativeQaMode mode, NativeQaBootstrapContext context)
    {
        var acknowledgement = await mode.AfterStartAsync(context);
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing fixture server.");
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Missing disposable fixture database.");
        Check.That(!server.HasExited, "Fixture server is still running");
        foreach (var (name, helper) in context.Helpers)
        {
            Check.That(mode.HelperConnectionReady(helper) && helper.World.Problems.Count == 0,
                $"Required helper {name} reached the expected session state with a healthy connection");
            Check.That(await database.LongAsync(
                $"SELECT COUNT(*) FROM characters WHERE charId={helper.CharId} AND is_online=1 AND healthC>0") == 1,
                $"Required helper {name} is online and alive");
        }

        string output = server.FullOutput();
        Check.That(!new[] { "Packet handling failed", "Visibility pass failed", "Unhandled exception", "[MISSION] Tick failed" }
            .Any(marker => output.Contains(marker, StringComparison.Ordinal)), "Fixture server diagnostics are clean");
        return acknowledgement;
    }

    public static void RequireNoAmbientAggro(ServerInstance server, HeadlessPlayer player)
    {
        string pattern = $@"\[AGGRO\].* (?:opens fire on|attacks|pursues) {player.CharId}(?![0-9])";
        var attack = server.DebugLog().Split('\n').FirstOrDefault(line => Regex.IsMatch(line, pattern));
        if (attack != null)
            throw new ScenarioFailure($"Unexpected ambient threat to {player.Handle}: {attack.Trim()}");
    }
}
