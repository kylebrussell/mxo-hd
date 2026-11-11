using hds;

namespace hds.Tests;

public class StaticObjectIndexTests
{
    [Fact]
    public void SameStaticIdResolvesInItsOwnDistrict()
    {
        // Azuna in Downtown and an unrelated Slums object share 3d 03 90 28.
        var slums = new StaticWorldObject { metrId = 1, mxoStaticId = 0x2890033d, pos_x = -69955, pos_z = -28730 };
        var azuna = new StaticWorldObject { metrId = 2, mxoStaticId = 0x2890033d, pos_x = -146900, pos_z = 50200 };
        var index = new StaticObjectIndex();
        index.Add(slums);
        index.Add(azuna);
        index.Add(new StaticWorldObject { metrId = 2, mxoStaticId = 0x2890033d });

        Assert.Same(slums, index.Find(0x2890033d, 1));
        Assert.Same(azuna, index.Find(0x2890033d, 2));
        Assert.Null(index.Find(0x2890033d, 3));
        Assert.Same(slums, index.Find(0x2890033d));
        Assert.Null(index.Find(123));
    }
}
