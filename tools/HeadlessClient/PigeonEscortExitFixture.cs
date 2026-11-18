using System.Globalization;
using System.Xml.Linq;
using hds;

namespace HeadlessClient;

public static class PigeonEscortExitFixture
{
    public const int Mission = 205204;
    public const int Area = 2;
    public const ushort Npc = 2;

    public static bool IsSample(MissionProgress.SavedState state) => state.Mission == Mission && state.Phase == Area - 1;

    public static string[] Describe(MissionProgress.SavedState state)
    {
        if (!IsSample(state)) return Array.Empty<string>();

        var lines = new List<string>();
        var pigeon = state.Mobs.SingleOrDefault(mob => mob.Npc == Npc);
        if (pigeon != null)
            lines.Add($"Pigeon at {Point(new[] { pigeon.X, pigeon.Y, pigeon.Z })}. Talk to him, then walk to the floor car with him.");
        var route = FindRoute(state);
        if (route == null)
            lines.Add("Pigeon exit: no NAV-valid stand at the current floor car.");
        else
        {
            var stand = route.Floor.Standing!;
            var car = route.Floor.Position;
            byte yaw = MobAggro.YawTowards(stand[0], stand[2], car[0], car[2]);
            lines.Add($"Pigeon exit: floor car {route.Floor.Key} at {Point(car)}; stand {Point(stand)}, face {yaw:X2} (yaw hex). Use the car with Pigeon beside you.");
        }
        return lines.ToArray();
    }

    internal static MervFollowupStreetEntry.Plan? FindRoute(MissionProgress.SavedState state)
    {
        var route = MervFollowupStreetEntry.ForSite(state);
        var stand = route?.Floor.Standing;
        if (stand?.Length != 3 || !NavMesh.Locate(1, stand[0], stand[1], stand[2], out var nav) ||
            !nav.indoors || Math.Abs(stand[1] - (nav.floorY + 95)) > 1)
            return null;
        return route;
    }

    private static string Point(IEnumerable<double> point) =>
        string.Join(",", point.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));
}

public static partial class Scenarios
{
    public static async Task CheckPigeonEscortExitAsync(ScenarioContext context, HeadlessPlayer player,
        MissionProgress.SavedState staged)
    {
        Check.That(PigeonEscortExitFixture.IsSample(staged), "Pigeon escort fixture requires 205204:2");
        var route = PigeonEscortExitFixture.FindRoute(staged)
            ?? throw new ScenarioFailure("Pigeon exit has no NAV-valid stand at the current floor car");
        var definition = XElement.Load(Path.Combine(context.RepositoryRoot, "data", "missions", "machines_5_2_4_weit.xml"));
        var area = definition.Elements("phase").ElementAt(staged.Phase);
        await SweepRide(context, player, staged, area, true, new List<object>());

        ushort npc = PigeonEscortExitFixture.Npc;
        var pigeon = await SweepNpc(context, player, npc);
        player.SkipReceived();
        player.SendRpc($"80 c7 {MissionNpcId(pigeon)} 09 00");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        var beforeExit = await MervFollowupCheckpoint(context, player);
        var stand = route.Floor.Standing!;
        await MoveForMissionTest(player, stand[0], stand[1], stand[2]);

        var currentLife = player.World.CurrentOwnViewLife();
        await player.WaitUntilAsync(() =>
        {
            var current = MervFollowupCheckpoint(context, player).GetAwaiter().GetResult();
            Check.That(PigeonEscortExitFixture.IsSample(current) && !current.Done.Contains(2),
                "Goal 2 stays open at the upstairs car");
            try
            {
                ZionAfter1212FollowerReadiness.Require(player.World.CurrentOwnViewLife(), currentLife.Life,
                    player.Handle, beforeExit, current, new[] { npc }, stand);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }, "Pigeon following within escort range of the floor car", TimeSpan.FromSeconds(20));

        int rideCursor = player.World.ViewSnapshot().Count;
        player.SendRpc($"80 c8 {Le(route.Floor.Key)} 03 00");
        await player.WaitForRpcAsync("80 a0 02 06 00 01", TimeSpan.FromSeconds(30));
        NpcInteriorComputerState.FreshTeleport(player.World.CurrentOwnViewLife(), currentLife, rideCursor, route.Lobby.Standing!);
        var afterExit = await MervFollowupCheckpoint(context, player);
        string identity = beforeExit.Mobs.Single(mob => mob.Npc == npc).Identity;
        Check.That(afterExit.Mission == staged.Mission && afterExit.Phase == staged.Phase + 1 &&
            afterExit.Following.Contains(npc) && afterExit.Mobs.Any(mob =>
                mob.Npc == npc && mob.Identity == identity && !mob.Dead && mob.Health > 0),
            "Goal 2 advances to area 3 with the same living Pigeon following");
    }
}
