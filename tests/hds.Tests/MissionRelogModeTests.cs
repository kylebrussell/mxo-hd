using NativeQa;

namespace hds.Tests;

public class MissionRelogModeTests
{
    [Fact]
    public void ModeUsesKnownMissionAndNormalAcceptanceWithoutForcedCombatControls()
    {
        var mode=NativeQaModes.Find("mission-relog");Assert.Equal("mission-relog",mode.Name);
        Assert.Equal(new ushort[]{2,17},mode.Plan.Abilities);Assert.True(mode.Plan.DevCommands);
        Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Empty(mode.Plan.EnvironmentOverrides);
        Assert.Equal(2202,mode.Plan.ExtraQaMetadata["MissionId"].GetInt32());
        Assert.Equal(1,mode.Plan.ExtraQaMetadata["InitialPhase"].GetInt32());
        Assert.Contains(mode.Plan.AfterLoadout,s=>s.Contains("(110,2201)"));
        Assert.False(NativeQaModes.Find("unknown-mission").Plan.DevCommands);
    }
}
