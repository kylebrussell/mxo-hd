using System;
using hds;

namespace hds.Tests;

public class VendorSellTests
{
    [Fact]
    public void TryApplySellCreditsBuybackPriceForKnownItem()
    {
        // goid 11666 has a decoded VendorPrice of 6800, buyback = 6800 / 10 = 680.
        bool result = VendorPricing.TryApplySell(1000, 11666, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal((uint)680, price);
        Assert.Equal((uint)1680, newCash);
    }

    [Fact]
    public void TryApplySellMatchesGetSellPrice()
    {
        uint expected = VendorPricing.GetSellPrice(11666);

        bool result = VendorPricing.TryApplySell(0, 11666, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal(expected, price);
        Assert.Equal(expected, newCash);
    }

    [Fact]
    public void TryApplySellRejectsNonSellableItem()
    {
        // goid 39231 is flagged NonSellable -> GetSellPrice returns 0 -> sell rejected, cash kept.
        bool result = VendorPricing.TryApplySell(5000, 39231, out uint newCash, out uint price);

        Assert.False(result);
        Assert.Equal((uint)0, price);
        Assert.Equal((uint)5000, newCash);
    }

    [Fact]
    public void TryApplySellClampsCashAtUInt32Max()
    {
        // currentCash already at the max; adding any buyback must clamp, not overflow.
        bool result = VendorPricing.TryApplySell(UInt32.MaxValue, 11666, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal((uint)680, price);
        Assert.Equal(UInt32.MaxValue, newCash);
    }

    [Fact]
    public void TryApplySellTreatsNegativeCashAsZeroFloor()
    {
        bool result = VendorPricing.TryApplySell(-100, 11666, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal((uint)680, price);
        Assert.Equal((uint)680, newCash);
    }

    [Fact]
    public void TryApplySellUsesDefaultBuybackForUnknownItem()
    {
        // goid 1 is not in the decoded csv -> GetSellPrice falls back to DefaultBuybackPrice (100).
        bool result = VendorPricing.TryApplySell(0, 1, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal((uint)100, price);
        Assert.Equal((uint)100, newCash);
    }
}
