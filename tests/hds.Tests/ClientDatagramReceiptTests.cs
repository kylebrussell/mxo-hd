using hds;
using Xunit;

namespace hds.Tests;

public sealed class ClientDatagramReceiptTests
{
    [Fact]
    public void RetailResetAndNextPacketAcknowledgeClientHistory()
    {
        // actions_combat.log.txt 187/191 and 197/201. In each pair, the client's Flags
        // are different from the server's response. The reset epoch begins at sequence 0.
        var window = new ClientDatagramReceipts();
        var cipher = new WorldEncryption();
        foreach (var row in new[]
        {
            (ClientSequence: (ushort)1, ClientFlags: (byte)0, ServerSequence: (ushort)2, ServerFlags: (byte)1),
            (ClientSequence: (ushort)2, ClientFlags: (byte)2, ServerSequence: (ushort)3, ServerFlags: (byte)3)
        })
        {
            window.Observe(row.ClientSequence);
            var reply = cipher.encrypt(new byte[] { 2 }, 1, window.Bits, window.Latest, row.ServerSequence);
            var decoded = cipher.decrypt(reply, reply.Length);
            Assert.NotEqual(row.ClientFlags, window.Bits);
            Assert.Equal((ushort)row.ServerFlags, (ushort)decoded[1]);
            Assert.Equal(row.ServerSequence, (ushort)decoded[2]);
            Assert.Equal(row.ClientSequence, (ushort)decoded[3]);
        }
    }

    [Fact]
    public void LateArrivalFillsOnlyItsOwnBitAcrossWrap()
    {
        var window = new ClientDatagramReceipts();
        window.Observe(0x700);
        window.Observe(0xe00);
        window.Observe(0xffe);
        window.Observe(1);
        Assert.Equal((ushort)1, window.Latest);
        Assert.Equal(0b100, window.Bits);
        window.Observe(0);
        window.Observe(0xff7);
        window.Observe(1);
        Assert.Equal(0b101, window.Bits);
        window.Reset();
        Assert.Equal((ushort)0, window.Latest);
        Assert.Equal(0, window.Bits);
    }

    [Fact]
    public void LatePacketEightBehindDoesNotSetTheForceAckBit()
    {
        var window = new ClientDatagramReceipts();
        window.Observe(8);
        Assert.Equal(0, window.Bits);
        window.Observe(0);
        Assert.Equal(0, window.Bits);
        window.Observe(1);
        Assert.Equal(0x40, window.Bits);
    }

    [Fact]
    public void BackoffRequiresBothSilenceAndStalledReliableRetries()
    {
        var backoff = new RpcResendBackoff();
        backoff.ObserveAck(0x700);
        backoff.ObserveAck(0xe00);
        backoff.ObserveAck(0xffe);
        for (int i = 0; i < RpcResendBackoff.StalledCycles; i++) backoff.RecordRetry();
        Assert.Equal(SequencedMessage.RpcResendIntervalMs, backoff.Interval(9000, 8990));
        Assert.Equal(RpcResendBackoff.SlowIntervalMs, backoff.Interval(9000, 0));
        backoff.ObserveAck(0xffd);
        Assert.Equal(RpcResendBackoff.SlowIntervalMs, backoff.Interval(9000, 0));
        backoff.ObserveAck(1);
        Assert.Equal(SequencedMessage.RpcResendIntervalMs, backoff.Interval(9000, 0));

        for (int i = 0; i < RpcResendBackoff.StalledCycles; i++) backoff.RecordRetry();
        backoff.Reset();
        Assert.Equal(SequencedMessage.RpcResendIntervalMs, backoff.Interval(9000, 0));
    }
}
