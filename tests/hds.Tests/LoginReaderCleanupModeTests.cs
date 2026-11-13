using NativeQa;
namespace hds.Tests;
public class LoginReaderCleanupModeTests
{
    [Fact]
    public void NativeModeUsesOnlyAnOrdinaryValidActor()
    {
        var p = NativeQaModes.Find("login-reader-cleanup").Plan;
        Assert.Equal(new ushort[] { 2, 17 }, p.Abilities); Assert.Equal(23828, p.X); Assert.Equal(13984, p.Z);
        Assert.False(p.DevCommands); Assert.False(p.GmCharacter110); Assert.False(p.FixedCallbackRolls);
        Assert.Empty(p.BeforeLoadout); Assert.Empty(p.AfterHelpers);
        var seed = Assert.Single(p.AfterLoadout); Assert.Contains("district='slums',districtId=1,y=495,rotation=126,healthC=300,healthM=300", seed);
        Assert.True(p.ExtraQaMetadata["NativeActorHasValidHealth"].GetBoolean());
        var e = NativeQaFixturePlan.EnvironmentFor("login-reader-cleanup");
        Assert.Equal("0", e["MXO_DEV_COMMANDS"]); Assert.Equal("", e["MXO_GM_CHARACTER_IDS"]);
        Assert.Equal("", e["MXO_CALLBACK_QA_ROLL15"]); Assert.Equal("", e["MXO_CALLBACK_QA_ROLL100"]);
    }
}
