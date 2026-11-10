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

    [Fact]
    public void AnAckCoversItsSequenceAndTheOnesItsBitsName()
    {
        // The client's steady ack bits: 42 (bits 1, 3, 5) for our every-other-sequence packets
        Assert.Equal(new HashSet<ushort> { 134, 132, 130, 128 }, SequencedMessage.AckedSequences(134, 42));
        // 40 (bits 3, 5): the packet two back was lost
        Assert.DoesNotContain((ushort)132, SequencedMessage.AckedSequences(134, 40));
        // 12-bit sequences wrap
        Assert.Contains((ushort)4094, SequencedMessage.AckedSequences(0, 2));
    }

    [Fact]
    public void AnRpcIsDoneWhenAPacketItWentOutInIsAcked()
    {
        SequencedMessage rpc = Message(4);
        rpc.rpcNumber = 61;
        rpc.rpcPackets.Add(132);

        Assert.False(rpc.IsRpcAckedBy(SequencedMessage.AckedSequences(134, 40)));
        rpc.rpcPackets.Add(140); // resent
        Assert.True(rpc.IsRpcAckedBy(SequencedMessage.AckedSequences(140, 0)));
    }

    [Fact]
    public void AnUnackedRpcIsSentAgainAfterTheInterval()
    {
        SequencedMessage rpc = Message(4);
        Assert.True(rpc.IsRpcDue(1000)); // never sent

        rpc.rpcNumber = 3;
        rpc.lastRpcSendTick = 1000;
        Assert.False(rpc.IsRpcDue(1000 + SequencedMessage.RpcResendIntervalMs - 1));
        Assert.True(rpc.IsRpcDue(1000 + SequencedMessage.RpcResendIntervalMs));
    }

    [Fact]
    public void ResentAndNewRpcsGoOutAsBlocksOfFollowingCounters()
    {
        SequencedMessage Numbered(int n) { SequencedMessage m = Message(2); m.rpcNumber = n; return m; }

        List<List<SequencedMessage>> blocks = WorldPacket.RpcBlocks(new[] { Numbered(62), Numbered(61), Numbered(70), Numbered(71), Numbered(72) });

        Assert.Equal(2, blocks.Count);
        Assert.Equal(new[] { 61, 62 }, blocks[0].Select(m => m.rpcNumber));
        Assert.Equal(new[] { 70, 71, 72 }, blocks[1].Select(m => m.rpcNumber));
    }
}
