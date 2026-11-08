using hds;

namespace hds.Tests;

public class HealthRegenTests
{
    [Fact]
    public void RegainsOnePercentOfMaxPerSecondOutOfCombat()
    {
        Assert.Equal((ushort)410, HealthRegen.Next(400, 500, false, false, 2.0)); // +5/s, as retail at 500 max
        Assert.Equal((ushort)500, HealthRegen.Next(498, 500, false, false, 2.0)); // capped at max
        Assert.Equal((ushort)2, HealthRegen.Next(1, 50, false, false, 1.0));      // at least 1
    }

    [Fact]
    public void NoRegenInCombatWhenDeadOrFull()
    {
        Assert.Null(HealthRegen.Next(400, 500, true, false, 2.0));
        Assert.Null(HealthRegen.Next(400, 500, false, true, 2.0));
        Assert.Null(HealthRegen.Next(0, 500, false, false, 2.0));
        Assert.Null(HealthRegen.Next(500, 500, false, false, 2.0));
    }
}
