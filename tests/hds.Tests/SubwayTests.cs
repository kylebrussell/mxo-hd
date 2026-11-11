using hds;

namespace hds.Tests;

/// <summary>The subway lines from the client's .metr files (data/subway_network.csv) and the ride's records.</summary>
public class SubwayTests
{
    private static string Data()
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "data", "navmesh")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }
        return Path.Combine(dir, "data");
    }

    private static void Load()
    {
        string data = Data();
        SubwayNetwork.Load(Path.Combine(data, "subway_network.csv"));
        SectorProperties.Load(Path.Combine(data, "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
    }

    private static string H(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    [Fact]
    public void LinesAreTheClients()
    {
        Load();
        Assert.Equal(44, SubwayNetwork.All.Count);
        Assert.Equal(35, SubwayNetwork.All.Count(s => s.IsStation));
        // Richland's Red Line has eight stations, in the client's order (81 3c indexes them)
        var red = SubwayNetwork.All.Where(s => s.worldId == SubwayNetwork.RichlandWorld && s.network == 1).ToList();
        Assert.Equal(8, red.Count);
        Assert.Equal("Richland - Tabor Park [ Red Line ]", red[1].name);
        Assert.Equal("Richland - Mara [ Red Line ]", red[7].name);
        Assert.Equal((ushort)12, SubwayNetwork.WorldOf(1));
        Assert.Equal((ushort)7, SubwayNetwork.WorldOf(2));
        Assert.Equal((ushort)169, SubwayNetwork.WorldOf(3));
    }

    [Fact]
    public void LinksToOtherDistrictsAreNotStations()
    {
        Load();
        SubwayStation downtown = SubwayNetwork.Get(12, 4, 1)!;
        Assert.Equal("Downtown", downtown.name);
        Assert.True(downtown.IsLink);
        Assert.False(downtown.IsStation);
        // Tabor Park on the Yellow Line names its own world in the link field: the client treats it as a station
        SubwayStation tabor = SubwayNetwork.Get(12, 4, 3)!;
        Assert.False(tabor.IsLink);
        Assert.True(tabor.IsStation);
        // The unnamed test line is not a station
        Assert.False(SubwayNetwork.Get(12, 0, 0)!.IsStation);
    }

    [Fact]
    public void EveryStationStandsOnTheNavMesh()
    {
        Load();
        foreach (SubwayStation s in SubwayNetwork.All.Where(s => s.IsStation))
        {
            Assert.True(NavMesh.Project(s.metrId, s.x, s.y + 95, s.z, out NavPoint p, 400), s.name);
            Assert.InRange(p.y - 95 - s.y, -10, 10);
            double[] at = SubwayHandler.Arrival(s);
            Assert.Equal(s.x, at[0]);
            Assert.Equal(s.z, at[2]);
        }
    }

    [Fact]
    public void NearestStationToATaborParkCar()
    {
        Load();
        // The Red Line car at Tabor Park (staticObjects_slums_1, goid 6568) stands 403 units from its connector
        SubwayStation s = SubwayNetwork.Nearest(1, 67997, -832, 47506, out double distance)!;
        Assert.Equal("Richland - Tabor Park [ Red Line ]", s.name);
        Assert.InRange(distance, 0, SubwayHandler.MaxStationDistance);
    }

    [Fact]
    public void RecordsAreTheClients()
    {
        // 81 3a <x y z f64> (0x621429c0), 81 3e <seconds u8> (0x620fccd0 reads byte +2)
        Assert.Equal("813a" + "0000000000b3f040" + "0000000000c089c0" + "0000000000b0e740",
            H(SubwayPackets.BuildOpenMap(68400, -824, 48512)));
        Assert.Equal("813e3c", H(SubwayPackets.BuildCountdown(60)));
        // 81 3c <world u16> <network u16> <connector u16> (0x620fc520, size 8)
        Assert.True(SubwayPackets.TryParsePick(Convert.FromHexString("0c0001000000"), out ushort w, out ushort n, out ushort c));
        Assert.Equal((12, 1, 0), (w, n, c));
        Assert.False(SubwayPackets.TryParsePick(new byte[4], out _, out _, out _));
    }

    [Fact]
    public void PicksAreChecked()
    {
        Load();
        SubwayStation from = SubwayNetwork.Get(12, 1, 1)!, to = SubwayNetwork.Get(12, 1, 0)!;
        Assert.Null(SubwayHandler.CheckPick(from, to, true, 10));
        Assert.Null(SubwayHandler.CheckPick(from, SubwayNetwork.Get(7, 2, 0), true, 10)); // across districts
        Assert.NotNull(SubwayHandler.CheckPick(null, to, false, 0));
        Assert.NotNull(SubwayHandler.CheckPick(from, to, true, 120));
        Assert.NotNull(SubwayHandler.CheckPick(from, from, true, 10));
        Assert.NotNull(SubwayHandler.CheckPick(from, SubwayNetwork.Get(12, 4, 1), true, 10));
        Assert.NotNull(SubwayHandler.CheckPick(from, null, true, 10));
    }

    [Fact]
    public void CarEntityIdsStayClearOfNpcIds()
    {
        // Moriah Projects' car (district 1, mxoStaticId 8) was entity "1" + "8" = 18, an NPC's id too
        var moriah = new StaticWorldObject { metrId = 1, mxoStaticId = 8 };
        var tabor = new StaticWorldObject { metrId = 1, mxoStaticId = 0x02b0052d };
        Assert.NotEqual(18ul, hds.world.Structures.Subway.EntityId(moriah));
        Assert.True(hds.world.Structures.Subway.EntityId(moriah) > uint.MaxValue);
        Assert.NotEqual(hds.world.Structures.Subway.EntityId(moriah), hds.world.Structures.Subway.EntityId(tabor));
        Assert.NotEqual(hds.world.Structures.Subway.EntityId(moriah), hds.world.Structures.Subway.EntityId(new StaticWorldObject { metrId = 2, mxoStaticId = 8 }));
    }
}
