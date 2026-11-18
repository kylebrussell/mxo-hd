using hds;

namespace HeadlessClient;

public static class WorldNpcCornerFixture
{
    public const string Name = "Blackwood Goof";
    public const byte Level = 5;
    public const int StandX = 19090, StandY = 95, StandZ = -10105;
    public static double[] Stand => new double[] { StandX, StandY, StandZ };
    public static double[] Aggro => new double[] { 19041, 95, -9744 };
    public const int FarX = 20218, FarY = 95, FarZ = -9510;
    private const int MovingRange = 1200, StationaryRange = 1500;
    public static double[] Turn => new double[] { FarX, FarY, FarZ };
    public static byte FarYaw => MobAggro.YawTowards(FarX, FarZ, Aggro[0], Aggro[2]);
    private static double[] Origin => new double[] { 18924, 95, -8852 };

    public static Dictionary<string, string> Environment => new()
        { ["MXO_MOBS"] = "world", ["MXO_AGGRO"] = "1", ["MXO_AGGRO_RANGE"] = (MovingRange / 100).ToString(), ["MXO_MOB_WANDER"] = "0" };

    public static Dictionary<string, string> StationaryEnvironment => new(Environment)
        { ["MXO_AGGRO_RANGE"] = (StationaryRange / 100).ToString() };

    public static void CheckGeometry(string root)
    {
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var point in new[] { Origin, Stand, Aggro, Turn })
            Check.That(NavMesh.Locate(1, point[0], point[1], point[2], out var at) &&
                !at.indoors && Math.Abs(point[1] - at.floorY - 95) < 1, "Chase marker has a grounded outdoor stand");

        Check.That(Straight(Stand, Aggro) && Straight(Aggro, Turn) && Straight(Origin, Aggro) &&
            !Straight(Origin, Turn), "Mara corner blocks the shortcut but the player can run around it");
        Check.That(Distance(Origin, Stand) > MovingRange &&
            Distance(Origin, Aggro) < MovingRange, "Stand and aggro markers straddle the moving test's attack radius");
        Check.That(Distance(Origin, Turn) < StationaryRange,
            "The stationary target is inside its fixture radius with a traversable route around the corner");
    }

    public static OpenWorldCurrentNpc.Current Observe(HeadlessPlayer player) =>
        OpenWorldCurrentNpc.Require(player.World.CurrentOwnViewLife(), 18924, 95, -8852,
            Name, Level, Convert.FromHexString("B3010058"), birthRadius: 1);

    public static bool Straight(double[] from, double[] to) =>
        NavMesh.HasSightPath(1, from[0], from[1], from[2], to[0], to[1], to[2]);

    public static double Distance(double[] a, double[] b) =>
        Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[2] - b[2], 2));

    public static string Directions =>
        $"Mara, slums_441: CSR starts at {FarX},{FarY},{FarZ} on the far side of the corner. " +
        "Stand still after login. Watch Blackwood Goof approach from 18924,95,-8852 and enter ordinary interlock.";
}
