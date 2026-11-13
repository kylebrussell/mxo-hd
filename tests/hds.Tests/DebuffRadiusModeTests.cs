using NativeQa.Modes;
namespace hds.Tests;
public class DebuffRadiusModeTests
{
    [Fact]
    public void ModeUsesActual345AndEnemyBoundariesWithoutRandomOverrides()
    {
        var plan=new DebuffRadiusMode().Plan;
        Assert.Contains((ushort)345,plan.Abilities);Assert.Contains((ushort)333,plan.Abilities);
        Assert.DoesNotContain((ushort)75,plan.Abilities);
        Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Empty(plan.EnvironmentOverrides);Assert.Empty(plan.Bootstrap);
        Assert.Equal(HeadlessClient.DebuffRadiusFixture.TargetsSql(6111,6112,6113,6114),plan.AfterHelpers[1]);
        Assert.Contains("WHEN 6112 THEN 24939",plan.AfterHelpers[1]);
    }
}
