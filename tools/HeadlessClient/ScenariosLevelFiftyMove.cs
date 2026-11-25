namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// A character seeded straight at level 50 (exact level-50 experience, the template's loaded abilities, stored
    /// health 500/500) spawns alive and can walk: a self-view move is accepted, not answered with a correction.
    /// </summary>
    private static async Task LevelFiftySeedMoves(ScenarioContext context)
    {
        foreach (var (name, level, abilities) in new[]
                 {
                     ("L50Move", 50, (IReadOnlyList<(ushort, ushort)>?)null),
                     ("L50Bare", 50, Array.Empty<(ushort, ushort)>()),
                     ("L10Move", 10, (IReadOnlyList<(ushort, ushort)>?)null)
                 })
        {
            var p = await context.AddPlayerAsync(new CharacterSeed(name)
            {
                Level = level, Experience = (int)hds.PlayerProgression.ExperienceForLevel(level), OwnedLoadedAbilities = abilities
            });
            await p.EnterWorldAsync();
            p.SendRpc("81 48");
            await p.WaitForRpcAsync("81 49");
            await Task.Delay(1500);
            int corrections = 0;
            for (int step = 1; step <= 4; step++)
            {
                int before = p.World.ViewSnapshot().Count;
                p.World.SendViewList(WireFormat.FromHex("02 00 01 08")
                    .Concat(BitConverter.GetBytes(17020f + 30 * step)).Concat(BitConverter.GetBytes(495f))
                    .Concat(BitConverter.GetBytes(2693f)).Concat(new byte[] { 0, 0 }).ToArray());
                p.SendRpc("81 48");
                await p.WaitForRpcAsync("81 49");
                await Task.Delay(300);
                corrections += p.World.ViewSnapshot().Skip(before).Count(v => v.Length >= 4 && v[0] == 2 && v[1] == 0 && v[2] == 1 && v[3] == 0x0c);
            }
            Console.WriteLine($"LEVEL-MOVE {name} level{level} corrections={corrections} problems={p.World.Problems.Count}");
            Check.Equal(0, corrections, $"{name}: a level-{level} seed's walk is accepted");
            p.SendRpc("80 fe 00");
            await p.WaitForRpcAsync("80 fd");
            p.Dispose();
        }
    }
}
