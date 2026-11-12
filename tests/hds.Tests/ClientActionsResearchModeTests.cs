using NativeQa.Modes;
namespace hds.Tests;
public class ClientActionsResearchModeTests
{
    [Fact]
    public void ResearchProfileDoesNotGrantGameplayOrDiagnosticAuthority()
    {
        var p=new ClientActionsResearchMode().Plan;
        Assert.Equal(17000,p.X);Assert.Equal(2300,p.Z);Assert.Equal(new ushort[]{2,17},p.Abilities);
        Assert.Empty(p.BeforeLoadout);Assert.Empty(p.AfterLoadout);Assert.Empty(p.AfterHelpers);
        Assert.Empty(p.EnvironmentOverrides);Assert.Empty(p.ExtraQaMetadata);
        Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
    }
}
