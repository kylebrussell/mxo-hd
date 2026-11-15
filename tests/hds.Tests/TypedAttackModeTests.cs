using NativeQa;
using NativeQa.Modes;
namespace hds.Tests;
public class TypedAttackModeTests
{
    [Fact]
    public void DiscoveredModeLoadsOnlyNormalAbilitiesAndUsesNoLiveRngOverride()
    {
        var plan=NativeQaModes.Find("typed-attack").Plan;
        Assert.Contains((ushort)134,plan.Abilities);Assert.Contains((ushort)97,plan.Abilities);Assert.Contains((ushort)314,plan.Abilities);
        Assert.DoesNotContain((ushort)32,plan.Abilities);Assert.DoesNotContain((ushort)701,plan.Abilities);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Empty(plan.EnvironmentOverrides);Assert.Equal("",plan.Bootstrap);
        Assert.Contains(plan.ReadyMessages,m=>m.Contains("NOT normal queued viral proof"));
    }
}
