using NativeQa;

namespace hds.Tests;

public class VendorEconomyModeTests
{
    [Fact]
    public void NativeEconomyFixtureCanOpenTheExistingVendorWithoutAnInvisibleNpc()
    {
        var plan = NativeQaModes.Find("vendor-economy").Plan;
        Assert.Equal(17000, plan.X);
        Assert.Equal(2300, plan.Z);
        Assert.Equal("1", NativeQaFixturePlan.EnvironmentFor("vendor-economy")["MXO_DEV_COMMANDS"]);
        Assert.Equal(98, plan.ExtraQaMetadata["VendorFixtureId"].GetInt32());
        Assert.Equal("?clientactions vendor", plan.ExtraQaMetadata["VendorUiOpener"].GetString());
        Assert.Contains(plan.AfterLoadout, sql => sql.Contains("(110,11666,11,1,0)"));
        Assert.False(plan.GmCharacter110);
        Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("0", NativeQaFixturePlan.EnvironmentFor("unknown-vendor-mode")["MXO_DEV_COMMANDS"]);
        Assert.Equal("", NativeQaFixturePlan.EnvironmentFor("vendor-economy")["MXO_GM_CHARACTER_IDS"]);
    }
}
