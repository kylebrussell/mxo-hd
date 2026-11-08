using hds;

namespace hds.Tests;

public class SecureTradeTests
{
    private static string H(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", " ").ToLowerInvariant();

    [Fact]
    public void RequestResultLayoutMatchesClient()
    {
        // client.dll class at 0x62145320: size 0x0f, +2 name offset, +4 status, +8 info flag, +9 info, +0d slot, +0e quantity
        byte[] record = TradePackets.BuildRequestResult("Bob", TradePackets.RequestOk, 500, 17, 2);
        Assert.Equal("81 83 0f 00 00 00 00 00 01 f4 01 00 00 11 02 04 00 42 6f 62 00", H(record));
    }

    [Fact]
    public void RefusalIsAStatusOnly()
    {
        byte[] record = TradePackets.BuildRequestResult("", TradePackets.RequestTooFar, 0, TradePackets.NoSlot, 0);
        Assert.Equal("81 83 0f 00 02 00 00 18 00 00 00 00 00 6b 00 01 00 00", H(record));
    }

    [Fact]
    public void OfferedLayoutMatchesClient()
    {
        // 0x621454c0: size 0x12, +2 name offset, +4 info flag, +5 info, +9 class, +0d instance, +11 quantity
        byte[] record = TradePackets.BuildOffered("Al", 0, 2399, 0x0c040000, 2);
        Assert.Equal("81 84 12 00 00 00 00 00 00 5f 09 00 00 00 00 04 0c 02 03 00 41 6c 00", H(record));
    }

    [Fact]
    public void ChangedLayoutMatchesClient()
    {
        // 0x62145660: size 0x13, +2 add, +3 yours, +4 info flag, +5 info, +9 slot, +0a class, +0e instance, +12 quantity
        byte[] record = TradePackets.BuildChanged(true, false, 0, TradePackets.NoSlot, 2565, 0x0c000000, 1);
        Assert.Equal(0x13, record.Length);
        Assert.Equal("81 86 01 00 00 00 00 00 00 6b 05 0a 00 00 00 00 00 0c 01", H(record));
    }

    [Fact]
    public void StatusLayoutMatchesClient()
    {
        // 0x62145820: size 0x16, +2 status, +6 given, +0a received, +0e signed info change
        byte[] record = TradePackets.BuildStatus(TradePackets.StatusComplete, 1, 2, -1000);
        Assert.Equal(0x16, record.Length);
        Assert.Equal("81 87 19 00 00 8e 01 00 00 00 02 00 00 00 18 fc ff ff ff ff ff ff", H(record));
    }

    [Fact]
    public void ParsesRequest()
    {
        // After the opcode: target u32, info flag, info u32, slot, quantity
        byte[] data = { 0x1f, 0x00, 0x0a, 0x00, 0x01, 0xe8, 0x03, 0x00, 0x00, 0x6b, 0x00 };
        Assert.True(TradeRequests.TryParseRequest(data, out TradeRequests.Request request));
        Assert.Equal(0x000a001fu, request.TargetId);
        Assert.Equal(1000u, request.Info);
        Assert.Equal(TradePackets.NoSlot, request.Slot);
        Assert.False(TradeRequests.TryParseRequest(new byte[10], out _));
    }

    [Fact]
    public void RequestInfoIgnoredWithoutFlag()
    {
        byte[] data = { 1, 0, 0, 0, 0, 0xe8, 0x03, 0, 0, 5, 0 };
        Assert.True(TradeRequests.TryParseRequest(data, out TradeRequests.Request request));
        Assert.Equal(0u, request.Info);
        Assert.Equal(5, request.Slot);
    }

    [Fact]
    public void ParsesAction()
    {
        // After the opcode: action, info flag, info u32, slot, quantity
        byte[] data = { TradeRequests.ActionAdd, 0, 0, 0, 0, 0, 22, 3 };
        Assert.True(TradeRequests.TryParseChange(data, out TradeRequests.Change change));
        Assert.Equal(TradeRequests.ActionAdd, change.Action);
        Assert.False(change.InfoTrade);
        Assert.Equal(22, change.Slot);
        Assert.Equal(3, change.ItemQuantity);
        Assert.False(TradeRequests.TryParseChange(new byte[7], out _));
    }

    [Fact]
    public void DescriptorCarriesTheStackAmount()
    {
        Assert.Equal(0x0c000000u, TradePackets.Descriptor(0, 1));
        Assert.Equal(0x0c080000u, TradePackets.Descriptor(0, 3));
        Assert.Equal(0x04040000u, TradePackets.Descriptor(0x04000000, 2));
    }

    [Fact]
    public void AcceptsClearTogether()
    {
        TradeSession session = new TradeSession(null!, null!);
        session.RequesterOffer.Accepted = true;
        Assert.False(session.BothAccepted);
        session.TargetOffer.Accepted = true;
        Assert.True(session.BothAccepted);
        session.ClearAccepts();
        Assert.False(session.RequesterOffer.Accepted);
        Assert.False(session.TargetOffer.Accepted);
    }

    [Fact]
    public void FreeSlotsSkipOccupied()
    {
        List<ushort> free = TradeRules.FreeBagSlots(new HashSet<int> { 1, 2, 4 });
        Assert.Equal((ushort)3, free[0]);
        Assert.Equal((ushort)5, free[1]);
        Assert.Equal(96, free[^1]);
    }
}
