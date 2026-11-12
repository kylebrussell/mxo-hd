using NativeQa.Modes;
namespace hds.Tests;
public class ResistDamageModeTests
{
    [Fact]
    public void RadiusFixtureOwnsOnlyHealthResetPrivilegeAndConcreteBoundaries()
    {
        var plan=new ResistDamageMode().Plan;
        Assert.Contains((ushort)74,plan.Abilities);Assert.Contains((ushort)371,plan.Abilities);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Empty(plan.EnvironmentOverrides);Assert.Equal("",plan.Bootstrap);
        string sql=plan.AfterHelpers[1];
        Assert.Equal(HeadlessClient.ResistDamageFixture.TargetsSql(6111,6112,6113,6114,17000,2300),sql);
        Assert.Contains("WHEN 6111 THEN 17600",sql);Assert.Contains("WHEN 6112 THEN 17800",sql);Assert.Contains("WHEN 6113 THEN 17900",sql);
        Assert.False(new DefaultMode().Plan.DevCommands);
    }
}
