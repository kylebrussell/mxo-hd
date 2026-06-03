using hds;

namespace hds.Tests;

public class DataPathTests
{
    [Fact]
    public void ResolveDataPathNormalizesWindowsSeparators()
    {
        string path = DataLoader.ResolveDataPath(@"data\missions");

        Assert.DoesNotContain("\\", path);
        Assert.EndsWith(Path.Combine("data", "missions"), path);
    }

    [Fact]
    public void DataPathBuildsPathUnderDataDirectory()
    {
        string path = DataLoader.DataPath("missions", "mission_zion_1.xml");

        Assert.EndsWith(Path.Combine("data", "missions", "mission_zion_1.xml"), path);
    }
}
