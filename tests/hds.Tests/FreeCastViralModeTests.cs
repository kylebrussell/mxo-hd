using NativeQa;
namespace hds.Tests;
public class FreeCastViralModeTests
{
    [Theory] [InlineData(true)] [InlineData(false)]
    public void DiscoveredProfilesForceOnlyOwnedContestDraws(bool hit)
    {
        string name="freecast-viral-"+(hit?"hit":"deflect");var mode=NativeQaModes.Find(name);Assert.Equal(name,mode.Name);
        var e=NativeQaFixturePlan.EnvironmentFor(name);
        Assert.True(mode.Plan.DevCommands);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Equal("",e["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);
        Assert.Equal(hit?"32766":"0",e["MXO_FREECAST_VIRAL_QA_ATTACK15"]);Assert.Equal(hit?"0":"32766",e["MXO_FREECAST_VIRAL_QA_DEFENSE15"]);
        Assert.Contains((ushort)33,mode.Plan.Abilities);Assert.Contains((ushort)57,mode.Plan.Abilities);
        Assert.Contains("PARTIAL",mode.Plan.ExtraQaMetadata["FreeCastViralPolicy"].GetString());
        Assert.Equal("",NativeQaFixturePlan.EnvironmentFor("")["MXO_FREECAST_VIRAL_QA"]);
    }
}
