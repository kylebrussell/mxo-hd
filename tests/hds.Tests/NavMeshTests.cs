using hds;

namespace hds.Tests;

/// <summary>The client's outdoor nav meshes (data/navmesh) against retail's own-view position records 03 02 00 01 1c.</summary>
public class NavMeshTests
{
    private static void Load()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "sector_properties.csv")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        SectorProperties.Load(Path.Combine(dir, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(dir, "data", "navmesh"));
    }

    [Theory]
    // district, x, y, z, and retail's sector, region, polygon at that spot (research/packet-dumps)
    [InlineData(3, -48349.6, 95.0, -31817.8, 261, 1, 0)]       // actions_combat_marketplace_abilitychange
    [InlineData(3, -48001.3, 95.0, -30839.2, 261, 6, 0)]
    [InlineData(3, -48986.4, 95.0, -31890.4, 261, 10, 0)]
    [InlineData(3, 47123.1, -1105.0, -53818.7, 345, 6, 0)]
    [InlineData(3, 37059.0, -1105.0, -30970.7, 375, 42, 61)]   // RajkoPacketsNearTheEnd
    [InlineData(3, 37313.9, -1105.0, -31626.0, 375, 49, 0)]
    [InlineData(3, 35676.5, -1105.0, -31318.0, 375, 52, 8)]
    [InlineData(1, 64230.6, 85.0, -92447.3, 311, 1, 11)]       // entilsar lastday2
    [InlineData(1, 63242.0, 95.0, -91737.3, 311, 5, 1)]
    [InlineData(1, 39913.7, 95.0, -21402.3, 403, 5, 3)]        // ProxyM2
    [InlineData(1, 40464.7, 85.0, -20978.8, 403, 7, 11)]       // under a walkway at 800
    [InlineData(1, 40266.6, 95.0, -21183.1, 403, 7, 12)]
    [InlineData(1, 16800.1, -705.0, 24741.0, 422, 45, 0)]      // luizoe Recursion
    [InlineData(1, 6131.7, -705.0, 59064.7, 783, 22, 0)]
    [InlineData(1, 5457.8, -705.0, 59622.3, 783, 24, 0)]
    [InlineData(2, -151763.7, -705.0, -102931.5, 290, 44, 51)]
    [InlineData(2, -146314.1, 695.0, 28528.2, 429, 1, 0)]      // medanon niobe3
    [InlineData(2, -150114.7, -715.0, -106441.5, 489, 8, 0)]
    [InlineData(2, -77816.8, 1295.0, -1216.8, 663, 7, 84)]     // crashover2k_008
    [InlineData(2, -8484.0, 1895.0, 48992.0, 751, 57, 0)]      // luizoe recursion1
    public void OutdoorsIsRetailsPolygon(ushort district, double x, double y, double z, int sector, int region, int polygon)
    {
        Load();
        Assert.True(NavMesh.Locate(district, x, y, z, out NavLocation at));
        Assert.Equal(sector, at.sector);
        Assert.Equal(region, at.region);
        Assert.Equal(polygon, at.polygon);
        Assert.InRange(y - 95 - at.floorY, -15.0, 15.0);
    }

    [Theory]
    // Retail had these in interior spaces (elevator floors, a lobby): no outdoor polygon may claim them
    [InlineData(1, 60216.1, 2495.0, 32968.4)]  // 405 region 25
    [InlineData(1, 60174.4, 95.0, 32944.5)]    // 405 region 31 (a lobby)
    [InlineData(1, -63710.6, 495.0, 23043.8)]  // 759 region 92
    [InlineData(2, -31074.2, -6505.0, -6660.0)] // 265 region 120
    public void InsideABuildingIsNotOutdoors(ushort district, double x, double y, double z)
    {
        Load();
        Assert.False(NavMesh.Locate(district, x, y, z, out _));
    }

    [Fact]
    public void RecordBytesAreSectorRegionPolygonLast()
    {
        NavLocation at = new NavLocation { sector = 0x193, region = 7, polygon = 0x0c };
        Assert.Equal(new byte[] { 0x93, 0x01, 0x07, 0x00, 0x0c, 0x00, 0xec }, at.RecordBytes(0xec));
    }
}
