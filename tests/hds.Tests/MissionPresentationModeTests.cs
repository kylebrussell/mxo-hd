using NativeQa.Modes;
namespace hds.Tests;
public class MissionPresentationModeTests
{
    [Fact]
    public void ModeSeedsTheCapturedInternuncioPrerequisitesWithoutForcedOutcomes()
    {
        var plan=new MissionPresentationMode().Plan;
        Assert.Equal(59000,plan.X);Assert.Equal(12200,plan.Z);
        Assert.Contains("(110,1201),(110,1204)",plan.AfterHelpers[0]);
        Assert.Contains("y=1095",plan.AfterHelpers[1]);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Empty(plan.EnvironmentOverrides);
    }
}
