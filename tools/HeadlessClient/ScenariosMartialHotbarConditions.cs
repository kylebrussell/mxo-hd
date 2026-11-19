namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task MartialHotbarConditions(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(NearNpc("MartialHotbar"));
        await player.EnterWorldAsync();
        var target = await player.WaitForViewNamedAsync("WinterFrost");
        player.SendRpc($"81 51 {Le(target.ViewId)} {target.SpawnId:x2}");

        // Stock checks the selected NPC's complete required mask. Exercise each state in isolation,
        // with no combat rounds adding unrelated condition bits to the same view.
        const string flags = "04 80 80 80 80 80 80 02";
        var cases = new[]
        {
            (Ability: 197, Command: "daze", Effect: "884/199", Mask: "02 00 00 00"),
            (Ability: 412, Command: "offbalance", Effect: "885/200", Mask: "04 00 00 00"),
            (Ability: 262, Command: "stagger", Effect: "883/201", Mask: "08 00 00 00")
        };
        foreach (var sample in cases)
        {
            int cursor = player.World.ViewSnapshot().Count;
            player.SendChat($"?combatdepth {sample.Command} 2");
            await player.WaitForTextAsync($"applied {sample.Effect} to WinterFrost for 2 seconds");
            var apply = BytePattern.Parse($"{Le(target.ViewId)} {flags} {sample.Mask}");
            var clear = BytePattern.Parse($"{Le(target.ViewId)} {flags} 00 00 00 00");
            await player.WaitUntilAsync(() =>
            {
                var updates = player.World.ViewSnapshot().Skip(cursor).ToList();
                int appliedAt = updates.FindIndex(apply.Occurs);
                return appliedAt >= 0 && updates.Skip(appliedAt + 1).Any(clear.Occurs);
            }, $"condition for ability {sample.Ability} is sent and then cleared", TimeSpan.FromSeconds(10));
            Console.WriteLine($"  ability {sample.Ability}: selected NPC flags {sample.Mask}, then cleared");
        }
    }
}
