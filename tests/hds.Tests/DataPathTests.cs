using hds;

namespace hds.Tests;

public class DataPathTests
{
    [Fact]
    public void ResolveDataPathNormalizesWindowsSeparators()
    {
        string path = DataLoader.ResolveDataPath(@"data\missions");

        // Both separators are normalised to the platform's own, so the other one must be gone.
        string foreignSeparator = Path.DirectorySeparatorChar == '/' ? "\\" : "/";
        Assert.DoesNotContain(foreignSeparator, path);
        Assert.EndsWith(Path.Combine("data", "missions"), path);
    }

    [Fact]
    public void DataPathBuildsPathUnderDataDirectory()
    {
        string path = DataLoader.DataPath("missions", "mission_zion_1.xml");

        Assert.EndsWith(Path.Combine("data", "missions", "mission_zion_1.xml"), path);
    }
}
