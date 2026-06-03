using hds;

namespace hds.Tests;

public class VendorPricingTests
{
    [Fact]
    public void BuyPriceUsesDecodedVendorPriceWhenAvailable()
    {
        Assert.Equal((uint)6800, VendorPricing.GetBuyPrice(11666));
    }

    [Fact]
    public void SellPriceIsOneTenthDecodedVendorPrice()
    {
        Assert.Equal((uint)680, VendorPricing.GetSellPrice(11666));
    }

    [Fact]
    public void SellPriceIsZeroForNonSellableItems()
    {
        Assert.Equal((uint)0, VendorPricing.GetSellPrice(39231));
    }

    [Fact]
    public void BuyPriceFallsBackWhenDecodedPriceIsMissing()
    {
        Assert.Equal((uint)100, VendorPricing.GetBuyPrice(1));
    }

    [Fact]
    public void SellPriceFallsBackToDecodedVendorBuybackDefault()
    {
        Assert.Equal((uint)100, VendorPricing.GetSellPrice(1));
    }

    [Fact]
    public void SellPriceForZeroUsesNoBuyBackListFallback()
    {
        Assert.Equal((uint)1, VendorPricing.GetSellPrice(0));
    }

    [Fact]
    public void SellPriceForFullPriceAbilityReturnsDecodedVendorPrice()
    {
        Assert.Equal((uint)72000, VendorPricing.GetSellPrice(2147497984));
    }

    [Fact]
    public void SellPriceForFullPriceAbilityIncludesLevelPremium()
    {
        Assert.Equal((uint)74600, VendorPricing.GetSellPrice(2147497984 + 3));
    }

    [Fact]
    public void SellPriceForNormalAbilityUsesOneTenthBuybackWithLevelPremium()
    {
        Assert.Equal((uint)29380, VendorPricing.GetSellPrice(2147867648 + 4));
    }

    [Fact]
    public void SellPriceForAbilityUsesVendorPriceEvenWhenAbilityRowIsNonSellable()
    {
        Assert.Equal((uint)100, VendorPricing.GetSellPrice(2148473856));
    }

    [Fact]
    public void SellPriceForUnknownAbilityFallsBackToDefaultBuyback()
    {
        Assert.Equal((uint)100, VendorPricing.GetSellPrice(0x80000001));
    }

    [Fact]
    public void TryApplyBuyDeductsPriceWhenCashIsAvailable()
    {
        bool result = VendorPricing.TryApplyBuy(7000, 11666, out uint newCash, out uint price);

        Assert.True(result);
        Assert.Equal((uint)6800, price);
        Assert.Equal((uint)200, newCash);
    }

    [Fact]
    public void TryApplyBuyRejectsPurchaseWhenCashIsInsufficient()
    {
        bool result = VendorPricing.TryApplyBuy(75, 11666, out uint newCash, out uint price);

        Assert.False(result);
        Assert.Equal((uint)6800, price);
        Assert.Equal((uint)75, newCash);
    }

    [Fact]
    public void LoadPriceDataParsesUnsignedAbilityObjectIds()
    {
        IReadOnlyDictionary<uint, VendorPriceEntry> prices =
            VendorPricing.LoadPriceData(Path.Combine("data", "vendor_prices.csv"));

        Assert.True(prices.TryGetValue(2147867648, out VendorPriceEntry? entry));
        Assert.Equal("ReviveRSIAbility", entry.CodeName);
        Assert.Equal((uint)288000, entry.VendorPrice);
        Assert.False(entry.NonSellable);
    }
}
