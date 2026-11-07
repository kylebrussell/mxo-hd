using hds;

namespace hds.Tests;

public class SequencedMessageTests
{
    [Fact]
    public void NewObjectMessageIsDueImmediately()
    {
        SequencedMessage message = new SequencedMessage(new byte[] {0x01});

        Assert.True(message.IsObjectSendDue(1_000));
    }

    [Fact]
    public void SentObjectMessageWaitsForTheResendInterval()
    {
        SequencedMessage message = new SequencedMessage(new byte[] {0x01});
        message.MarkObjectSent(1_000);

        Assert.False(message.IsObjectSendDue(1_000 + SequencedMessage.ObjectResendIntervalMs - 1));
        Assert.True(message.IsObjectSendDue(1_000 + SequencedMessage.ObjectResendIntervalMs));
    }

    [Fact]
    public void UnackedObjectMessageIsResentUntilTheSendLimit()
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
    }
}
