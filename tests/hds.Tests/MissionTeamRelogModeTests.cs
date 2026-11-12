using NativeQa;
namespace hds.Tests;
public class MissionTeamRelogModeTests
{
    [Fact]
    public void ModeProvidesOnlyKnownMissionPreparationWithoutRandomOverrides()
    {
        var m=NativeQaModes.Find("mission-team-relog"); Assert.Equal("mission-team-relog",m.Name);
        Assert.Equal(new ushort[]{2,17},m.Plan.Abilities); Assert.True(m.Plan.DevCommands);
        Assert.False(m.Plan.GmCharacter110); Assert.False(m.Plan.FixedCallbackRolls); Assert.Empty(m.Plan.EnvironmentOverrides);
        Assert.Equal(2202,m.Plan.ExtraQaMetadata["MissionId"].GetInt32());
        Assert.Contains(m.Plan.AfterLoadout,s=>s.Contains("(110,2201)"));
        Assert.False(NativeQaModes.Find("unknown-mission-team").Plan.DevCommands);
    }
}
