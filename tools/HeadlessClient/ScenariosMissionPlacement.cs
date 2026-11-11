using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task MissionArrivalNavmesh(ScenarioContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Placement") { Level = 50 }))[0];
        player.SendRpc("80 94 05 00 03");
        await player.WaitForTextAsync("Arrival Placement Fixture");
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9c");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(player);
        ViewCreation guide = await player.WaitForViewNamedAsync("Guide");
        double[] start = PositionOf(guide);
        Check.That(NavMesh.Locate(1, start[0], start[1], start[2], out NavLocation floor), "The site NPC was placed on a floor");
        double[] boundary = FindBadOffset(start, floor.floorY) ??
            throw new ScenarioFailure("This site has no walkable point whose old three-metre offset crosses a navmesh gap");
        await MoveForMissionTest(player, boundary[0], boundary[1], boundary[2]);
        player.SendRpc($"80 c7 {MissionNpcId(guide)} 09 00");
        ViewCreation security = await player.WaitForViewNamedAsync("Security");
        ViewCreation courier = await player.WaitForViewNamedAsync("Courier");
        ViewCreation machine = await player.WaitForViewNamedAsync("Machine Program");
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(v => v.Name == "Zion Soldier") == 2,
            "Object-relative wave", TimeSpan.FromSeconds(15));
        List<ViewCreation> soldiers = player.World.CreationSnapshot().Where(v => v.Name == "Zion Soldier").ToList();
        List<double[]> occupied = new() { start };
        foreach (ViewCreation npc in new[] { security, courier, machine }.Concat(soldiers))
        {
            double[] at = PositionOf(npc);
            Check.That(occupied.All(p => Math.Pow(p[0] - at[0], 2) + Math.Pow(p[2] - at[2], 2) >= 9999),
                npc.Name + " does not overlap another projected arrival");
            occupied.Add(at);
            Check.That(NavMesh.Locate(1, at[0], at[1], at[2], out NavLocation location), npc.Name + " has a walkable polygon");
            Check.That(Math.Abs(at[1] - location.floorY - 95) < 0.01, npc.Name + " stands at floor height");
            Check.That(Math.Abs(location.floorY - floor.floorY) < 1, npc.Name + " stays on the same floor");
            Check.That(NavMesh.Project(1, at[0], at[1], at[2], out _, 1, boundary,
                requireDirect: npc.Name != "Zion Soldier"), npc.Name + " is reachable from the player");
        }
        foreach (ViewCreation hostile in new[] { security, machine }.Concat(soldiers)) await DefeatMissionNpc(player, hostile);
        await player.WaitUntilAsync(() => player.Received("80 a0 02 06 00 01").Count == 1, "Arrival kill goal", TimeSpan.FromSeconds(15));
        int seen = player.World.CreationSnapshot().Count;
        await TalkToMissionNpc(player, "Guide");
        ViewCreation turned = await WaitForNewNpc(player, "Courier", seen);
        Check.That(turned.Attributes.ContainsKey("IsEnemy"), "The courier changed side");
        Check.That(PositionOf(turned).Zip(PositionOf(courier)).All(p => Math.Abs(p.First - p.Second) < 0.01),
            "A walkable side-change respawn stays in place");
        await DefeatMissionNpc(player, turned);
        await player.WaitForRpcAsync("80 a2 00", TimeSpan.FromSeconds(15));
    }

    private static double[] PositionOf(ViewCreation npc)
    {
        byte[] p = npc.Attributes["Position"];
        return new[] { BitConverter.ToDouble(p, 0), BitConverter.ToDouble(p, 8), BitConverter.ToDouble(p, 16) };
    }

    private static double[]? FindBadOffset(double[] centre, double floor)
    {
        for (int radius = 0; radius <= 1600; radius += 50)
            for (int step = 0; step < 48; step++)
            {
                double x = centre[0] + radius * Math.Cos(step * Math.PI / 24);
                double z = centre[2] + radius * Math.Sin(step * Math.PI / 24);
                if (!NavMesh.Locate(1, x, floor + 95, z, out NavLocation at) || Math.Abs(at.floorY - floor) > 1) continue;
                double oldX = x + 300 * Math.Cos(0.6), oldZ = z + 300 * Math.Sin(0.6);
                if (!NavMesh.Locate(1, oldX, floor + 95, oldZ, out NavLocation offset) || Math.Abs(offset.floorY - floor) > 1)
                    return new[] { x, floor + 95, z };
            }
        return null;
    }
}
