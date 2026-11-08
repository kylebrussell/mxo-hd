using hds;

namespace hds.Tests;

public class RpcReceiveWindowTests
{
    private static readonly byte[] Buy = { 0x81, 0x0e, 0x01 };

    [Fact]
    public void TheSameRpcResentIsCaught()
    {
        var window = new RpcReceiveWindow();
        Assert.False(window.IsResend(0x57, 0x10e, Buy));
        Assert.True(window.IsResend(0x57, 0x10e, Buy));
        Assert.True(window.IsResend(0x57, 0x10e, Buy));
    }

    [Fact]
    public void AnIdenticalRequestWithANewNumberIsNew()
    {
        // Buying the same item twice on purpose
        var window = new RpcReceiveWindow();
        Assert.False(window.IsResend(0x57, 0x10e, Buy));
        Assert.False(window.IsResend(0x58, 0x10e, Buy));
    }

    [Fact]
    public void ARestartedNumberWithOtherBytesIsNew()
    {
        // Retail logs restart their numbering on a world reload: the same number then carries another RPC
        var window = new RpcReceiveWindow();
        Assert.False(window.IsResend(1, 0x10e, Buy));
        Assert.False(window.IsResend(1, 0xb9, new byte[] { 0x0c, 0x00 }));
        Assert.False(window.IsResend(1, 0x10e, Buy));
    }

    [Fact]
    public void TheHeaderCounts()
    {
        var window = new RpcReceiveWindow();
        Assert.False(window.IsResend(9, 0x10e, Buy));
        Assert.False(window.IsResend(9, 0x111, Buy));
    }

    [Fact]
    public void ResetForgets()
    {
        var window = new RpcReceiveWindow();
        Assert.False(window.IsResend(5, 0x10e, Buy));
        window.Reset();
        Assert.False(window.IsResend(5, 0x10e, Buy));
    }

    [Fact]
    public void OldNumbersAgeOut()
    {
        var window = new RpcReceiveWindow();
        Assert.False(window.IsResend(0, 0x10e, Buy));
        for (int i = 1; i <= RpcReceiveWindow.Capacity; i++)
        {
            Assert.False(window.IsResend((ushort)i, 0x10e, Buy));
        }
        Assert.False(window.IsResend(0, 0x10e, Buy));
    }
}
