using hds;

namespace hds.Tests;

public class NavMeshProjectionTests
{
    private static NavMesh.Surface Room(int id, double x0, double z0, double x1, double z1, double floor = 0) =>
        new(new[] { x0, z0, x1, z0, x1, z1, x0, z1 }, 0, 0, floor,
            new NavLocation { sector = 1, region = 1, polygon = (ushort)id, indoors = true });

    [Fact]
    public void SnapsTheWallOffsetIntoThePlayersRoom()
    {
        NavMesh.Surface[] rooms = { Room(0, 0, 0, 400, 800), Room(1, 500, 0, 1000, 800) };
        Assert.True(NavMesh.ProjectSurfaces(rooms, 600, 95, 500, 800, new[] { 350d, 95, 400 }, true, out NavPoint point));
        Assert.InRange(point.x, 20, 380);
        Assert.Equal(500, point.z);
        Assert.Equal(95, point.y);
        Assert.Equal(0, point.location.polygon);
    }

    [Fact]
    public void PlayerArrivalsStayVisibleEvenWhenTheNextRoomHasADistantDoor()
    {
        NavMesh.Surface[] rooms =
        {
            Room(0, 0, 0, 400, 800), Room(1, 500, 0, 1000, 800), Room(2, 380, 700, 520, 800)
        };
        double[] player = { 350, 95, 400 };
        Assert.True(NavMesh.ProjectSurfaces(rooms, 600, 95, 500, 800, player, true, out NavPoint nearby));
        Assert.InRange(nearby.x, 20, 380);
        // An object-relative arrival may use that room: the corridor is a provable route around the wall.
        Assert.True(NavMesh.ProjectSurfaces(rooms, 600, 95, 500, 800, player, false, out NavPoint atObject));
        Assert.Equal(600, atObject.x);
        Assert.Equal(500, atObject.z);
    }

    [Fact]
    public void PartialSharedEdgesConnectButCornersDont()
    {
        NavMesh.Surface first = Room(0, 0, 0, 400, 800);
        Assert.True(first.Connects(Room(1, 400, 200, 600, 400)));
        Assert.False(first.Connects(Room(2, 400, 800, 600, 1000)));
        Assert.False(first.Connects(Room(3, 400, 200, 600, 400, 400)));
    }

    [Fact]
    public void TheClosestFloorDoesNotMoveAnArrivalUpstairs()
    {
        NavMesh.Surface[] floors = { Room(0, 0, 0, 400, 800), Room(1, 0, 0, 1000, 800, 400) };
        Assert.True(NavMesh.ProjectSurfaces(floors, 600, 95, 500, 800, null, false, out NavPoint point));
        Assert.Equal(380, point.x);
        Assert.Equal(95, point.y);
        Assert.Equal(0, point.location.polygon);
    }

    [Fact]
    public void AMezzanineWithinLocateToleranceDoesNotWinByHorizontalDistance()
    {
        NavMesh.Surface[] floors = { Room(0, 0, 0, 400, 800), Room(1, 0, 0, 1000, 800, 100) };
        Assert.True(NavMesh.ProjectSurfaces(floors, 600, 95, 500, 800, null, false, out NavPoint point));
        Assert.Equal(380, point.x);
        Assert.Equal(95, point.y);
    }

    [Fact]
    public void SlopingFloorsSetTheNpcHeightAtTheSnappedPoint()
    {
        NavMesh.Surface slope = new(new double[] { 0, 0, 400, 0, 400, 800, 0, 800 }, 0.1, 0, 0, default);
        Assert.True(NavMesh.ProjectSurfaces(new[] { slope }, 500, 130, 300, 800, null, false, out NavPoint point));
        Assert.Equal(380, point.x);
        Assert.Equal(133, point.y);
    }

    [Fact]
    public void AnAlreadyWalkableRespawnKeepsItsPosition()
    {
        Assert.True(NavMesh.ProjectSurfaces(new[] { Room(0, 0, 0, 400, 800) }, 200, 95, 300, 800,
            new[] { 250d, 95, 300 }, false, out NavPoint point));
        Assert.Equal(new double[] { 200, 95, 300 }, point.Position);
    }

    [Fact]
    public void MissingOrDistantFloorsDoNotProduceAnArbitraryPosition()
    {
        Assert.False(NavMesh.ProjectSurfaces(Array.Empty<NavMesh.Surface>(), 0, 95, 0, 800, null, false, out _));
        Assert.False(NavMesh.ProjectSurfaces(new[] { Room(0, 0, 0, 400, 800) }, 5000, 95, 300, 800, null, false, out _));
    }

    [Theory]
    [InlineData(1, 60216.1, 2495, 32968.4)]
    [InlineData(1, 23828.3, 1295, 13984.9)]
    [InlineData(2, -31074.2, -6505, -6660)]
    [InlineData(3, 46846.1, 1695, -7563.5)]
    [InlineData(0, 7600, 3995, -16150)]
    public void ShippedRotatedInteriorsProjectOntoTheSameFloor(ushort district, double x, double y, double z)
    {
        Load();
        Assert.True(NavMesh.Locate(district, x, y, z, out NavLocation before));
        Assert.True(NavMesh.Project(district, x + 250, y + 40, z + 200, out NavPoint point,
            reachableFrom: new[] { x, y, z }, requireDirect: true));
        Assert.True(NavMesh.Locate(district, point.x, point.y, point.z, out NavLocation after));
        Assert.InRange(Math.Abs(before.floorY - after.floorY), 0, 1);
        Assert.InRange(Math.Abs(point.y - 95 - after.floorY), 0, 0.01);
    }

    [Fact]
    public void AnUnavailableTargetUsesTheSitesNpcSpot()
    {
        Load();
        double[] fallback = { 60216.1, 2495, 32968.4 };
        Assert.True(MissionPlacement.Resolve(1, new double[] { 9999999, 2495, 9999999 }, null,
            new[] { fallback }, false, out NavPoint point, out bool usedFallback));
        Assert.True(usedFallback);
        Assert.True(NavMesh.Locate(1, point.x, point.y, point.z, out _));
        Assert.InRange(Math.Abs(point.y - fallback[1]), 0, 1);
    }

    [Fact]
    public void FallbacksDoNotStackActorsOnTheSameProjectedPoint()
    {
        Load();
        double[] fallback = { 60216.1, 2495, 32968.4 };
        double[] unavailable = { 9999999, 2495, 9999999 };
        Assert.True(MissionPlacement.Resolve(1, unavailable, null, new[] { fallback }, false,
            out NavPoint first, out _));
        Assert.True(MissionPlacement.Resolve(1, unavailable, first.Position, new[] { fallback }, false,
            out NavPoint second, out _, new[] { first.Position }));
        Assert.True(Math.Pow(first.x - second.x, 2) + Math.Pow(first.z - second.z, 2) >= 10000);
        Assert.True(NavMesh.Project(1, second.x, second.y, second.z, out _, 1, first.Position));
    }

    [Fact]
    public void SiteArrivalsStayInsideTheAllocatedAreaEvenWhenMoreOfTheFloorIsWalkable()
    {
        Load();
        // Native QA: this walkable logged NPC spot was outside the rectangle that starts the area's timers.
        var site = new MissionSite { floorY = 2149, minX = 10497.5, maxX = 10897.5, minZ = -8970, maxZ = -8570 };
        double[] adjacent = { 9314.69335938, 2095, -8735.30664062 };
        Assert.True(NavMesh.Locate(1, adjacent[0], adjacent[1], adjacent[2], out _));
        double[] reference = MissionPlacement.ReferenceFor(1, site, new double[] { 13300, 2095, 6250 }, false);
        Assert.NotNull(reference);
        Assert.True(MissionPlacement.Resolve(1, adjacent, reference, new[] { adjacent }, false, out NavPoint placed, out _, site: site));
        Assert.True(NavMesh.Project(1, placed.x, placed.y, placed.z, out _, 1, reference));
        Assert.InRange(placed.x, site.minX - 400, site.maxX + 400);
        Assert.InRange(placed.z, site.minZ - 400, site.maxZ + 400);
    }

    private static void Load()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "sector_properties.csv"))) dir = Path.GetDirectoryName(dir)!;
        SectorProperties.Load(Path.Combine(dir, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(dir, "data", "navmesh"));
    }
}
