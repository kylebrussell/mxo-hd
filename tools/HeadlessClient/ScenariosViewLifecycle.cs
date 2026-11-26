using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// NEXT-STEPS visibility check: a player who leaves the view radius loses the other player's and the NPC's views
    /// (and the other player loses theirs), standing still creates nothing twice, and coming back creates them again
    /// once. The Mara hardline booth at 17000,545,2200 (staticObjects_slums_1.csv) follows the same lifecycle.
    /// Subway cars have their own scenarios (subway-*).
    /// </summary>
    private static async Task ViewRangeLifecycle(ScenarioContext context)
    {
        var (walker, stayer, stayerSeen, walkerSeen) = await TwoNearby(context, NearNpc("Walker"), NearNpc("Stayer", 150));
        var (npcSeen, _) = await SharedNpc(walker, stayer);
        await Task.Delay(TimeSpan.FromSeconds(3));
        Check.Equal(1, walker.World.CreationSnapshot().Count(v => v.Name == "Stayer"), "Standing still creates the other player once");
        Check.Equal(1, stayer.World.CreationSnapshot().Count(v => v.Name == "Walker"), "The other player sees the walker once");
        Check.Equal(1, walker.ViewsOfGoid(MobGoid).Count(v => v.ViewId == npcSeen.ViewId), "Standing still creates the NPC once");
        int walkerCreations = walker.World.CreationSnapshot().Count, stayerCreations = stayer.World.CreationSnapshot().Count;

        static byte[] At(double x, double z) => new byte[] { 2, 0, 1, 8 }.Concat(BitConverter.GetBytes((float)x))
            .Concat(BitConverter.GetBytes((float)NpcY)).Concat(BitConverter.GetBytes((float)z)).Concat(new byte[] { 0, 0 }).ToArray();
        walker.World.SendViewList(At(100000, 100000));
        await walker.WaitForViewUpdateAsync(1, $"01 01 00 {Le(stayerSeen.ViewId)}", TimeSpan.FromSeconds(10));
        await walker.WaitForViewUpdateAsync(1, $"01 01 00 {Le(npcSeen.ViewId)}", TimeSpan.FromSeconds(10));
        await stayer.WaitForViewUpdateAsync(1, $"01 01 00 {Le(walkerSeen.ViewId)}", TimeSpan.FromSeconds(10));

        walker.World.SendViewList(At(NpcX + 200, NpcZ));
        await walker.WaitUntilAsync(() => walker.World.CreationSnapshot().Skip(walkerCreations).Any(v => v.Name == "Stayer") &&
            walker.World.CreationSnapshot().Skip(walkerCreations).Any(v => v.Goid == MobGoid && v.Name == npcSeen.Name),
            "Returning recreates the other player and the NPC", TimeSpan.FromSeconds(15));
        await stayer.WaitUntilAsync(() => stayer.World.CreationSnapshot().Skip(stayerCreations).Any(v => v.Name == "Walker"),
            "The other player sees the walker come back", TimeSpan.FromSeconds(15));
        await Task.Delay(TimeSpan.FromSeconds(3));
        Check.Equal(1, walker.World.CreationSnapshot().Skip(walkerCreations).Count(v => v.Name == "Stayer"), "The return creates the other player once");
        Check.Equal(1, walker.World.CreationSnapshot().Skip(walkerCreations).Count(v => v.Goid == MobGoid && v.Name == npcSeen.Name), "The return creates the NPC once");
        Check.Equal(1, stayer.World.CreationSnapshot().Skip(stayerCreations).Count(v => v.Name == "Walker"), "The return creates the walker once for the other player");

        int beforeBooth = walker.World.CreationSnapshot().Count;
        walker.World.SendViewList(At(17000, 2600));
        await walker.WaitUntilAsync(() => walker.World.CreationSnapshot().Skip(beforeBooth).Any(v => v.Goid == HardlineBooths.PhoneBoothType),
            "The Mara hardline booth is created in range", TimeSpan.FromSeconds(10));
        var booth = walker.World.CreationSnapshot().Skip(beforeBooth).Last(v => v.Goid == HardlineBooths.PhoneBoothType);
        walker.World.SendViewList(At(100000, 100000));
        await walker.WaitForViewUpdateAsync(1, $"01 01 00 {Le(booth.ViewId)}", TimeSpan.FromSeconds(10));
        int afterLeave = walker.World.CreationSnapshot().Count;
        walker.World.SendViewList(At(17000, 2600));
        await walker.WaitUntilAsync(() => walker.World.CreationSnapshot().Skip(afterLeave).Any(v => v.Goid == HardlineBooths.PhoneBoothType),
            "The booth is created again on return", TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(2));
        Check.Equal(1, walker.World.CreationSnapshot().Skip(afterLeave).Count(v => v.Goid == HardlineBooths.PhoneBoothType), "The return creates the booth once");
        Check.Equal(0, walker.World.Problems.Count + stayer.World.Problems.Count, "Views stay well framed");
        Console.WriteLine($"View range lifecycle: player, NPC {npcSeen.Name} and the Mara booth deleted out of range and recreated once on return.");
    }
}
