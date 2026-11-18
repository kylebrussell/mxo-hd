using System.Text.Json;
using HeadlessClient;
using NativeQa.Modes;

namespace hds.Tests;

public class NativeQaSecureTradeTests
{
    [Theory]
    [InlineData("{}", 3)]
    [InlineData("{\"slot\":1}", 1)]
    [InlineData("{\"slot\":4}", 4)]
    [InlineData("{\"slot\":96}", 96)]
    public void PartnerOfferUsesTheSelectedBagSlot(string request, int expectedSlot)
    {
        using var json = JsonDocument.Parse(request);
        byte[] wire = WireFormat.FromHex(SecureTradeMode.BuildPartnerOffer(json.RootElement, 0x1234, 0x56));

        Assert.Equal(new byte[] { 0x81, 0x82, 0x34, 0x12, 0x56, 0, 0, 0, 0, 0, 0, (byte)expectedSlot, 0 }, wire);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("97")]
    [InlineData("-1")]
    [InlineData("256")]
    [InlineData("1.5")]
    [InlineData("null")]
    [InlineData("\"4\"")]
    public void PartnerOfferRejectsNonBagSlots(string slot)
    {
        using var json = JsonDocument.Parse($"{{\"slot\":{slot}}}");

        Assert.Throws<ArgumentException>(() => SecureTradeMode.BuildPartnerOffer(json.RootElement, 0x1234, 0x56));
    }
}
