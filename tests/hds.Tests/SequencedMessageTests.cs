using hds;

namespace hds.Tests;

public class SequencedMessageTests
{
    [Fact]
    public void RpcPacketHistoryCanBeAcknowledgedWhileAnotherSendIsRecorded()
    {
        var message = new SequencedMessage(new byte[] { 0x01 });
        var noAcknowledgments = new HashSet<ushort>();
        Parallel.Invoke(
            () =>
            {
                for (ushort sequence = 0; sequence < 4096; sequence++)
                {
                    message.RecordRpcPacket(sequence);
                    Thread.Yield();
                }
            },
            () =>
            {
                for (int attempt = 0; attempt < 4096; attempt++)
                {
                    Assert.False(message.IsRpcAckedBy(noAcknowledgments));
                }
            });
        Assert.True(message.IsRpcAckedBy(new HashSet<ushort> { 4095 }));
    }

    [Fact]
    public void NewObjectMessageIsDueImmediately()
    {
        SequencedMessage message = new SequencedMessage(new byte[] {0x01});

        Assert.True(message.IsObjectSendDue(1_000));
    }

    [Fact]
    public void SentObjectMessageIsNotResentByDefault()
    {
        SequencedMessage message = new SequencedMessage(new byte[] {0x01});
        message.MarkObjectSent(1_000);

        Assert.False(message.IsObjectSendDue(1_000 + SequencedMessage.ObjectResendIntervalMs * 10));
    }

    [Fact]
    public void SentObjectMessageWaitsForTheResendInterval()
    {
        WithResend(() =>
        {
            SequencedMessage message = new SequencedMessage(new byte[] {0x01});
            message.MarkObjectSent(1_000);

            Assert.False(message.IsObjectSendDue(1_000 + SequencedMessage.ObjectResendIntervalMs - 1));
            Assert.True(message.IsObjectSendDue(1_000 + SequencedMessage.ObjectResendIntervalMs));
        });
    }

    private static void WithResend(Action test)
    {
        bool previous = SequencedMessage.ObjectResendEnabled;
        SequencedMessage.ObjectResendEnabled = true;
        try
        {
            test();
        }
        finally
        {
            SequencedMessage.ObjectResendEnabled = previous;
        }
    }

    [Fact]
    public void UnsentMessageIsNeverTreatedAsAcked()
    {
        // Regression: a queued self-spawn (neededAck 0) was deleted by the client's next ack
        // before it was ever sent, which stalled logins at "Negotiating with Servers...".
        SequencedMessage message = new SequencedMessage(new byte[] {0x01});

        Assert.False(message.IsAckedBy(0));
        Assert.False(message.IsAckedBy(ushort.MaxValue));
    }

    [Fact]
    public void SentMessageIsAckedOnceTheClientReachesItsSequence()
    {
        SequencedMessage message = new SequencedMessage(new byte[] {0x01});
        message.setNeededAck(10);
        message.MarkObjectSent(0);

        Assert.False(message.IsAckedBy(9));
        Assert.True(message.IsAckedBy(10));
        Assert.True(message.IsAckedBy(11));
    }

    [Fact]
    public void UnackedObjectMessageIsResentUntilTheSendLimit()
    {
        WithResend(() =>
        {
            SequencedMessage message = new SequencedMessage(new byte[] {0x01});
            long now = 0;
            int sends = 0;

            // Keep flushing past the resend interval as if the client never acks.
            for (int i = 0; i < SequencedMessage.MaxObjectSends * 2; i++)
            {
                if (message.IsObjectSendDue(now))
                {
                    message.MarkObjectSent(now);
                    sends++;
                }
                now += SequencedMessage.ObjectResendIntervalMs;
            }

            Assert.Equal(SequencedMessage.MaxObjectSends, sends);
            Assert.Equal(SequencedMessage.MaxObjectSends, message.sendCounter);
            Assert.False(message.IsObjectSendDue(now));
        });
    }
}
