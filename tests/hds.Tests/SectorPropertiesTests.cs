using hds;

namespace hds.Tests;

/// <summary>
/// The client's per-sector properties (data/sector_properties.csv) and hardline network (data/hardline_network.csv),
/// checked against the server's older data and retail captures.
/// </summary>
public class SectorPropertiesTests
{
    private static string Data(string file)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", file)))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        return Path.Combine(dir, "data", file);
    }

    private static void Load()
    {
        SectorProperties.Load(Data("sector_properties.csv"));
        HardlineNetwork.Load(Data("hardline_network.csv"));
    }

    [Fact]
    public void SectorsMatchTheOldNeighbourhoodMap()
    {
        Load();
        Dictionary<ushort, string> old = MissionSites.LoadNeighbourhoods(Data("slums_neighbourhoods.csv"));
        Assert.Equal(1493, old.Count);
        foreach (KeyValuePair<ushort, string> kv in old)
        {
            // The old file has the designers' "facade" on 4 sectors whose neighbourhood id is Facade
            Assert.Equal(kv.Value, SectorProperties.Get(1, kv.Key)!.NeighbourhoodOrName, ignoreCase: true);
        }
    }

    [Fact]
    public void APositionFindsItsSector()
    {
        Load();
        // The first static of data/staticObjects_slums_1.csv: sector 0 at 99640, 8350
        SectorInfo first = SectorProperties.At(1, 99640, 8350)!;
        Assert.Equal(0, first.sector);
        Assert.Equal("Moriah Projects", first.neighbourhood);
        Assert.Equal(0x38000006u, first.neighbourhoodId);

        // Mara Central's booth (static 0x393001ea in sector 915, TravelTests)
        Assert.Equal(915, SectorProperties.At(1, 17000, 2200)!.sector);
        Assert.Null(SectorProperties.At(1, 900000, 900000));
    }

    [Fact]
    public void LevelsAreRetailsNpcLevels()
    {
        Load();
        // The median level of the captured NPCs in data/mob_parsed.csv per neighbourhood
        Assert.Equal(4, SectorProperties.Get(1, 1)!.level);                 // Tabor Park
        Assert.Equal(7, SectorProperties.Get(1, 0)!.level);                 // Moriah Projects
        Assert.Equal(18, SectorProperties.Get(1, 2)!.level);                // Gunness Lake
        Assert.Equal(4, SectorProperties.LevelAt(1, 17000, 2200));          // Mara
        Assert.Equal(0, SectorProperties.LevelAt(1, 900000, 900000));
    }

    [Fact]
    public void ContactsMissionAreasAndPvp()
    {
        Load();
        // Contact Type is the contact's id in resource/missions/definitions/sponsors.xml: 1 Amber, in Tabor Park
        IReadOnlyList<SectorInfo> amber = SectorProperties.SectorsOfContact(1);
        Assert.Contains(amber, s => s.metrId == 1 && s.neighbourhood == "Tabor Park");
        Assert.All(amber, s => Assert.Equal("Amber", s.contact));
        Assert.Empty(SectorProperties.SectorsOfContact(9999));

        // Missions only off the scenery ring in the cities; unknown sectors stay allowed
        Assert.True(SectorProperties.MissionAreasEnabled(1, 0));
        Assert.False(SectorProperties.MissionAreasEnabled(1, 5));           // a Facade sector
        Assert.True(SectorProperties.MissionAreasEnabled(1, 60000));

        // PvP zones: only the constructs
        Assert.False(SectorProperties.IsPvpZone(1, 17000, 2200));
        Assert.Contains(Enumerable.Range(0, 400).Select(i => SectorProperties.Get(4, (ushort)i)), s => s != null && s.pvpZone);
    }

    [Fact]
    public void GangTypeSplitsIntoAreaAndTier()
    {
        Load();
        SectorInfo gunness = SectorProperties.Get(1, 2)!; // Gang Type 37
        Assert.Equal(37, gunness.GangArea);
        Assert.Equal(0, gunness.GangTier);
        Assert.Equal(-1, SectorProperties.Get(1, 0)!.gangType); // Richland has none
        Assert.Equal(0, SectorProperties.Get(1, 0)!.GangArea);
    }

    [Fact]
    public void TheNetworkListsTheClientsHardlines()
    {
        Load();
        Assert.True(HardlineNetwork.Covers(1));
        Assert.False(HardlineNetwork.Covers(12)); // constructs stay on data_hardlines

        List<ushort> slums = HardlineNetwork.Ids(1);
        Assert.Equal(54, slums.Count);           // 55 in the file, less 154 (no name)
        Assert.Contains((ushort)83, slums);      // missing from data_hardlines
        Assert.Contains((ushort)143, slums);
        Assert.DoesNotContain((ushort)154, slums);

        List<ushort> downtown = HardlineNetwork.Ids(2);
        Assert.Equal(63, downtown.Count);
        Assert.DoesNotContain((ushort)30, downtown);
        Assert.Contains((ushort)72, downtown);
        Assert.Equal(23, HardlineNetwork.Ids(3).Count);

        Assert.Equal("Richland.Mara (Central)", HardlineNetwork.Get(1, 152)!.name);
        Assert.Equal("96 Kaplan Course", HardlineNetwork.Get(1, 152)!.address);
    }

    [Fact]
    public void ABoothIsItsOwnHardline()
    {
        Load();
        // PacketsSpawnedInMaraCThenExitedViaHardline: the booth at 17000, 2200 prompted 80 fb 98 (152, Mara Central)
        Assert.Equal(152, HardlineNetwork.Nearest(1, 17000, 2200));
        Assert.Equal(0, HardlineNetwork.Nearest(99, 0, 0));
    }

    [Theory]
    // district, hardline, where retail put the player after 81 8e (first position of its self-spawn)
    [InlineData(1, 152, 17000.0, 495.0, 2300.0)]        // entilsar_enti_lastday2
    [InlineData(1, 149, 15550.0, 95.0, -142500.0)]      // proxy_023_teleports_...complete_richland (data_hardlines was 1,169 off)
    [InlineData(1, 113, 107670.0, -505.0, -149110.0)]   // proxy_023_teleports
    [InlineData(1, 72, 39150.0, 95.0, -21455.0)]        // proxy_023_teleports
    [InlineData(2, 72, -178350.0, 95.0, -71750.0)]      // proxy_023_teleports (not in data_hardlines)
    [InlineData(2, 88, -77650.0, 1295.0, -3350.0)]      // crashover2k_008_some_pvp_and_teleports
    [InlineData(3, 23, -65400.0, 95.0, 32200.0)]        // RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline
    [InlineData(3, 17, 36480.0, -1105.0, -30770.0)]     // the same
    public void ArrivalIsRetails(ushort district, ushort hid, double x, double y, double z)
    {
        Load();
        Hardline h = HardlineNetwork.Get(district, hid)!;
        Assert.Equal(x, h.spotX);
        Assert.Equal(z, h.spotZ);
        Assert.InRange(h.spotY - y, -10.0, 10.0);
    }
}
