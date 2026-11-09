using hds;

namespace hds.Tests;

/// <summary>
/// How many messages one packet takes. A teleport once queued 66 debug chats and they went out as one 6,400-byte packet:
/// the client never took it and stopped processing the server's RPCs for the rest of the session.
/// </summary>
public class WorldPacketTests
{
    private static SequencedMessage Message(int bytes) => new SequencedMessage(new byte[bytes]);

    [Fact]
    public void APacketStopsTakingMessagesAtItsSizeLimit()
    {
        WorldPacket packet = new WorldPacket(null!);
        int taken = 0;
        while (packet.addRPCContent(Message(98)))
        {
            taken++;
        }

        Assert.Equal(13, taken); // 13 x (98 + 2) = 1,300
        Assert.Equal(1300, packet.bufferSize);
    }

    [Fact]
    public void AnEmptyPacketTakesAMessageOfAnySize()
    {
        WorldPacket packet = new WorldPacket(null!);

        Assert.True(packet.addRPCContent(Message(2000)));
        Assert.False(packet.addRPCContent(Message(1)));
    }

    [Fact]
    public void ObjectMessagesCountTowardsTheLimitToo()
    {
        WorldPacket packet = new WorldPacket(null!);

        Assert.True(packet.addObjectContent(Message(700)));
        Assert.False(packet.addObjectContent(Message(700)));
        Assert.Single(packet.ObjectMessages);
        Assert.Empty(packet.RPCMessages);
    }

    [Fact]
    public void ThePacketsRpcCountFitsInAByte()
    {
        WorldPacket packet = new WorldPacket(null!);
        int taken = 0;
        while (packet.addRPCContent(Message(1)))
        {
            taken++;
        }

        Assert.Equal(255, taken);
    }
}
