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
        // 42 (bits 1, 3, 5): the steady ack bits our packets got when they went out every other sequence
        Assert.Equal(new HashSet<ushort> { 134, 132, 130, 128 }, SequencedMessage.AckedSequences(134, 42));
        // 40 (bits 3, 5): the packet two back was lost
        Assert.DoesNotContain((ushort)132, SequencedMessage.AckedSequences(134, 40));
        // 12-bit sequences wrap
        Assert.Contains((ushort)4094, SequencedMessage.AckedSequences(0, 2));
    }

    [Fact]
    public void ForceAckBitNeverAcknowledgesAnEighthPacket()
    {
        // The stock idle ACK of 1883 has PSS ff (86b9468137d4, PacketLog 19589).
        var acks = SequencedMessage.AckedSequences(1883, 0xff);
        Assert.Equal(Enumerable.Range(1876, 8).Select(s => (ushort)s), acks.OrderBy(s => s));
        Assert.DoesNotContain((ushort)1875, acks);
        Assert.Equal(new HashSet<ushort> { 0 }, SequencedMessage.AckedSequences(0, 0x80));
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

    [Fact]
    public void RpcBlocksSplitsDifferentOriginalBlocksEvenWhenCountersAreContiguous()
    {
        SequencedMessage Numbered(int n, int blockStart)
        {
            SequencedMessage m = Message(2);
            m.rpcNumber = n;
            m.rpcBlockStart = blockStart;
            return m;
        }

        List<List<SequencedMessage>> blocks = WorldPacket.RpcBlocks(new[]
        {
            Numbered(0x73, 0x73),
            Numbered(0x74, 0x74),
            Numbered(0x75, 0x74),
            Numbered(0x76, 0x76),
            Numbered(0x77, 0x76),
        });

        Assert.Equal(3, blocks.Count);
        Assert.Equal(new[] { 0x73 }, blocks[0].Select(m => m.rpcNumber));
        Assert.Equal(new[] { 0x74, 0x75 }, blocks[1].Select(m => m.rpcNumber));
        Assert.Equal(new[] { 0x76, 0x77 }, blocks[2].Select(m => m.rpcNumber));
    }

    [Fact]
    public void RpcBlocksOrdersAcrossSixteenBitWrap()
    {
        SequencedMessage Numbered(int n, int blockStart)
        {
            SequencedMessage m = Message(2);
            m.rpcNumber = n;
            m.rpcBlockStart = blockStart;
            return m;
        }

        List<List<SequencedMessage>> blocks = WorldPacket.RpcBlocks(new[]
        {
            Numbered(0, 65535),
            Numbered(65535, 65535),
            Numbered(1, 1),
            Numbered(65534, 65534),
        });

        Assert.Equal(3, blocks.Count);
        Assert.Equal(new[] { 65534 }, blocks[0].Select(m => m.rpcNumber));
        Assert.Equal(new[] { 65535, 0 }, blocks[1].Select(m => m.rpcNumber));
        Assert.Equal(new[] { 1 }, blocks[2].Select(m => m.rpcNumber));
    }
}
