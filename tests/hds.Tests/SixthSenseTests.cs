using hds;
using Xunit;

namespace hds.Tests;

public class SixthSenseTests
{
    [Theory]
    [InlineData(0, 50, 0, false)]
    [InlineData(0, 51, 0, true)]
    [InlineData(17, 33, 0, false)]
    [InlineData(18, 33, 0, true)]
    [InlineData(0, 0, 1, false)]
    [InlineData(0, 1, 200, true)]
    public void ScanUsesTheShippedStrictThreshold(int roll, int detection, int stealth, bool visible)
        => Assert.Equal(visible, SixthSense.Detects(roll, detection, stealth));

    [Theory]
    [InlineData(1500, 0, 0, true)]
    [InlineData(1501, 0, 0, false)]
    [InlineData(0, 1501, 0, false)]
    [InlineData(900, 1200, 0, true)]
    [InlineData(901, 1200, 0, false)]
    public void ScanUsesTheFullThreeDimensionalRadius(int x, int y, int z, bool within)
        => Assert.Equal(within, SixthSense.WithinRadius((0, 0, 0), (x, y, z)));

    [Fact]
    public void ScanReadsTheStoredDetectionStat()
    {
        var modifier = Assert.Single(AbilityCatalog.Find(SixthSense.Ability).Modifiers);
        Assert.Equal(SixthSense.Detection, modifier.Target);
        Assert.Equal(4d, modifier.Number("ValueScale"));
        Assert.Equal(2d, modifier.Number("ValueFunction"));
    }
}
