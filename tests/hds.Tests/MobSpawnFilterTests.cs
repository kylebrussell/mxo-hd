using hds;

namespace hds.Tests;

public class MobSpawnFilterTests
{
    [Theory]
    [InlineData("test", true)]
    [InlineData("TEST", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("full", false)]
    public void WholeWorldUnlessAskedForTheTestList(string? setting, bool test)
    {
        Assert.Equal(test, MobSpawnFilter.UseTestList(setting!));
    }

    [Fact]
    public void AnExactRepeatIsTheSameNpc()
    {
        var seen = new MobSpawnFilter.Seen();
        Assert.False(seen.IsRepeat(1, "Randy", 100, 200));
        Assert.True(seen.IsRepeat(1, "Randy", 100, 200));
    }

    [Fact]
    public void WithinFiveMetresIsTheSameNpc()
    {
        var seen = new MobSpawnFilter.Seen();
        Assert.False(seen.IsRepeat(1, "Randy", 100, 200));
        Assert.True(seen.IsRepeat(1, "Randy", 599, 200));
        Assert.False(seen.IsRepeat(1, "Randy", 600, 200));
    }

    [Fact]
    public void CellEdgesDontHideARepeat()
    {
        var seen = new MobSpawnFilter.Seen();
        Assert.False(seen.IsRepeat(1, "Randy", 499, 499));
        Assert.True(seen.IsRepeat(1, "Randy", 501, 501));
        Assert.False(seen.IsRepeat(1, "Randy", -250, -250));
        Assert.True(seen.IsRepeat(1, "Randy", -251, -249));
    }

    [Fact]
    public void AnotherNameOrDistrictIsAnotherNpc()
    {
        var seen = new MobSpawnFilter.Seen();
        Assert.False(seen.IsRepeat(1, "Randy", 100, 200));
        Assert.False(seen.IsRepeat(1, "Bell Madonna", 100, 200));
        Assert.False(seen.IsRepeat(2, "Randy", 100, 200));
    }
}
