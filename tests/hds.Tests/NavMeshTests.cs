using hds;

namespace hds.Tests;

/// <summary>The client's nav meshes (data/navmesh) against retail's own-view position records 03 02 00 01 1c.</summary>
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
        Assert.False(at.indoors);
        Assert.Equal(sector, at.sector);
        Assert.Equal(region, at.region);
        Assert.Equal(polygon, at.polygon);
        Assert.InRange(y - 95 - at.floorY, -15.0, 15.0);
    }

    [Theory]
    // Every interior record in research/packet-dumps: elevator rides (Downtown 265, Slums 759, 405, 1214), building floors,
    // the tutorial (district 0, sectors 0 and 1)
    [InlineData(0, 6700.0, 1595.0, -12300.0, 0, 40, 32)]
    [InlineData(0, 7600.0, 3995.0, -16150.0, 0, 41, 82)]
    [InlineData(0, 6950.0, 295.0, -11100.0, 0, 42, 38)]
    [InlineData(0, 5757.9, -1105.0, -9750.9, 0, 43, 14)]
    [InlineData(0, 5800.0, -1105.0, -13700.0, 0, 43, 116)]
    [InlineData(0, 5100.0, 495.0, -16700.0, 0, 44, 58)]
    [InlineData(0, -25300.0, 1595.0, 51700.0, 1, 40, 32)]
    [InlineData(0, -24400.0, 3995.0, 47850.0, 1, 41, 82)]
    [InlineData(0, -26582.2, -1105.0, 54096.4, 1, 43, 4)]
    [InlineData(0, -25307.2, -1105.0, 54445.7, 1, 43, 88)]
    [InlineData(0, -26200.0, -1105.0, 50300.0, 1, 43, 116)]
    [InlineData(0, -26900.0, 495.0, 47300.0, 1, 44, 58)]
    [InlineData(1, 45140.2, 2095.0, 6288.6, 35, 78, 4)]
    [InlineData(1, 45092.1, 95.0, 6129.0, 35, 84, 9)]
    [InlineData(3, 46846.1, 1695.0, -7563.5, 62, 79, 5)]
    [InlineData(3, 44387.0, 2495.0, -3707.1, 62, 87, 4)]
    [InlineData(3, 44320.5, -1105.0, -3840.1, 62, 96, 2)]
    [InlineData(1, 23540.7, 1295.0, 15548.3, 209, 76, 16)]
    [InlineData(1, 23828.3, 1295.0, 13984.9, 209, 82, 3)]
    [InlineData(1, 3789.2, 1695.0, 13413.7, 258, 19, 3)]
    [InlineData(1, 3797.6, 95.0, 13544.3, 258, 24, 4)]
    [InlineData(2, -31074.2, -105.0, -6660.0, 265, 116, 6)]
    [InlineData(2, -31145.0, -4905.0, -6884.1, 265, 119, 7)]
    [InlineData(2, -31074.2, -6505.0, -6660.0, 265, 120, 6)]
    [InlineData(2, -31074.2, -8105.0, -6660.0, 265, 121, 6)]
    [InlineData(2, -31074.2, -9705.0, -6660.0, 265, 122, 6)]
    [InlineData(2, -31074.2, -11305.0, -6660.0, 265, 123, 6)]
    [InlineData(2, -31074.2, -12905.0, -6660.0, 265, 124, 6)]
    [InlineData(2, -31074.2, -14505.0, -6660.0, 265, 125, 6)]
    [InlineData(2, -31074.2, -16105.0, -6660.0, 265, 126, 6)]
    [InlineData(2, -31074.2, -17705.0, -6660.0, 265, 127, 6)]
    [InlineData(2, -31132.9, -17705.0, -6871.9, 265, 127, 7)]
    [InlineData(2, -31074.2, -19305.0, -6660.0, 265, 128, 6)]
    [InlineData(2, -31074.2, -20905.0, -6660.0, 265, 129, 6)]
    [InlineData(2, -31074.2, -22505.0, -6660.0, 265, 130, 6)]
    [InlineData(2, -31074.2, -24105.0, -6660.0, 265, 131, 6)]
    [InlineData(2, -33577.9, 1095.0, -2557.4, 265, 133, 7)]
    [InlineData(1, 10806.5, 2095.0, -8929.1, 328, 38, 9)]
    [InlineData(1, 10682.4, 1695.0, -8798.3, 328, 39, 7)]
    [InlineData(1, 10682.4, 95.0, -8798.3, 328, 44, 7)]
    [InlineData(1, 60216.1, 2495.0, 32968.4, 405, 25, 7)]
    [InlineData(1, 60174.4, 95.0, 32944.5, 405, 31, 5)]
    [InlineData(1, -63710.6, 2095.0, 23043.8, 759, 87, 3)]
    [InlineData(1, -63710.6, 1695.0, 23043.8, 759, 88, 3)]
    [InlineData(1, -63710.6, 1295.0, 23043.8, 759, 89, 3)]
    [InlineData(1, -63710.6, 895.0, 23043.8, 759, 90, 3)]
    [InlineData(1, -63710.6, 495.0, 23043.8, 759, 92, 3)]
    [InlineData(1, -63710.6, 95.0, 23043.8, 759, 93, 3)]
    [InlineData(1, 18532.7, 1695.0, 3169.6, 915, 25, 54)]
    [InlineData(1, 8307.3, 895.0, 13099.6, 1214, 45, 48)]
    [InlineData(1, 9842.2, 895.0, 13694.8, 1214, 55, 9)]
    [InlineData(1, 9830.9, 95.0, 13498.0, 1214, 57, 3)]
    [InlineData(1, 46639.1, 2095.0, 22197.5, 1334, 92, 4)]
    [InlineData(1, 59640.5, -105.0, 11482.5, 1349, 114, 3)]
    public void IndoorsIsRetailsPolygon(ushort district, double x, double y, double z, int sector, int region, int polygon)
    {
        Load();
        Assert.True(NavMesh.Locate(district, x, y, z, out NavLocation at));
        Assert.True(at.indoors);
        Assert.Equal(sector, at.sector);
        Assert.Equal(region, at.region);
        Assert.Equal(polygon, at.polygon);
    }

    [Fact]
    public void RecordBytesAreSectorRegionPolygonLast()
    {
        NavLocation at = new NavLocation { sector = 0x193, region = 7, polygon = 0x0c };
        Assert.Equal(new byte[] { 0x93, 0x01, 0x07, 0x00, 0x0c, 0x00, 0xec }, at.RecordBytes(0xec));
    }
}
