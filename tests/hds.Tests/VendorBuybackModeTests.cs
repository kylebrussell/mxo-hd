using NativeQa;
namespace hds.Tests;
public class VendorBuybackModeTests
{
    [Fact] public void DiscoveredModeUsesNormalSaleAndExistingWindowOpenerWithoutRandomOverrides()
    {
        var plan=NativeQaModes.Find("vendor-buyback").Plan;
        Assert.Equal(new ushort[]{2,17},plan.Abilities);Assert.Equal(17000,plan.X);Assert.Equal(2300,plan.Z);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(98,plan.ExtraQaMetadata["VendorFixtureId"].GetInt32());
        Assert.Contains(plan.AfterLoadout,s=>s.Contains("(110,11666,11,0,872415232)"));
        Assert.Contains(plan.AfterLoadout,s=>s.Contains("slot BETWEEN 1 AND 96"));
        Assert.DoesNotContain(plan.AfterLoadout,s=>s.Contains("slot<97"));
        Assert.Contains(plan.ReadyMessages,s=>s.Contains("?clientactions vendor")&&s.Contains("Buy Back"));
        Assert.False(NativeQaModes.Find("").Plan.DevCommands);
        Assert.DoesNotContain(plan.AfterLoadout,s=>s.Contains("vendor_last_sale"));
    }
}
