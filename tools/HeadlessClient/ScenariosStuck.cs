namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// /stuck (81 55, local policy): a living player out of combat is sent to the Loading Area through the booth's exit
    /// (80 fd 00000000 0100, confirmed with 80 fe 01); a second request inside five minutes and a request from the
    /// Loading Area are refused with a notice and no world change; malformed bodies are ignored.
    /// </summary>
    private static async Task StuckToLoadingArea(ScenarioContext context)
    {
        var p = (await context.AddPlayersInWorldAsync(new CharacterSeed("Stuck")))[0];
        int exits = p.Received("80 fd").Count;
        p.SendRpc("81 55 00");
        p.SendRpc("81 4d 01 00 00 00"); await p.WaitForRpcAsync("81 4d 01 00 00 00");
        Check.Equal(exits, p.Received("80 fd").Count, "A malformed /stuck body is ignored");

        p.SendRpc("81 55");
        await p.WaitForTextAsync("Local /stuck: returning you to the Loading Area");
        await p.WaitForRpcAsync("80 fd 00 00 00 00 01 00");
        p.SendRpc("81 55");
        await p.WaitForTextAsync("You can use /stuck again in");
        Check.Equal(exits + 1, p.Received("80 fd").Count, "The cooldown refusal sends no second exit");

        p.SendRpc("80 fe 01");
        await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");
        await p.WaitForLoadingAreaAsync();
        p.SendRpc("05");
        await Task.Delay(500);
        p.SendRpc("81 55");
        await p.WaitForTextAsync("You are already in the Loading Area");
        Check.Equal(exits + 1, p.Received("80 fd").Count, "No exit is sent from the Loading Area");
        Check.Equal(0, p.World.Problems.Count, "No client parser problems");
    }
}
