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
    public void TryApplySellClampsCashAtSignedAccountMax()
    {
        // currentCash already at the old unsigned max; adding any buyback must clamp to the actual signed column, not overflow.
        bool result = VendorPricing.TryApplySell(UInt32.MaxValue, 11666, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal((uint)680, price);
        Assert.Equal((uint)Int32.MaxValue, newCash);
    }

    [Fact]
    public void TryApplySellTreatsNegativeCashAsZeroFloor()
    {
        bool result = VendorPricing.TryApplySell(-100, 11666, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal((uint)680, price);
        Assert.Equal((uint)680, newCash);
    }

    // Request bytes recorded from the real 7.6005 client against this server (2026-11-06):
    // the item in DB slot 0 sent "00 00", the item in DB slot 11 sent "00 0b".
    [Theory]
    [InlineData(new byte[] {0x00, 0x00}, 0)]
    [InlineData(new byte[] {0x00, 0x0b}, 11)]
    // Retail capture sell fields (docs/generated/packet-research-summary.md) are later inventory pages.
    [InlineData(new byte[] {0x00, 0x45}, 69)]
    [InlineData(new byte[] {0x00, 0x55}, 85)]
    [InlineData(new byte[] {0x00, 0x58}, 88)]
    public void ParseSellSlotReadsNativeSlotByte(byte[] request, int expectedSlot)
    {
        Assert.Equal((ushort)expectedSlot, VendorHandler.ParseSellSlot(request));
    }

    [Theory]
    // The real client offered "Recycle Detect Vulnerability for $i 500?" (10,000 ability code).
    [InlineData(0x80001c00u, 500u)]
    // Retail recycles of 30,000 weapons paid 1,500.
    [InlineData(7873u, 1500u)]
    public void RecyclePaysFivePercentOfTheVendorPrice(uint goId, uint expected)
    {
        Assert.Equal(expected, VendorPricing.GetRecyclePrice(goId));
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
