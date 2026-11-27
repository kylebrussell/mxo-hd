using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

/// <summary>
/// Reliable RPC resends stay in counter order. The level-four native run (MXO_MOBS=world) left RPC 46 unacked for 39
/// sends while 47-64 piled up behind it, and the resends went out as 52 53 54 | 46 47 | 50 51 | 48 49: each RPC kept
/// its own 500 ms timer, so ones first sent in different flushes were resent in different packets on their own phases.
/// </summary>
public class RpcOrderedResendTests
{
    private sealed class SequenceCipher : IWorldEncryption
    {
        public readonly List<(byte[] Plain, ushort Sseq, ushort Pss)> Sent = new();
        public byte[] encrypt(byte[] bytes, int length, ushort pss, ushort cseq, ushort sseq)
        { lock (Sent) Sent.Add((bytes.ToArray(), sseq, pss)); return bytes; }
        private readonly WorldEncryption inbound = new();
        public ArrayList decrypt(byte[] bytes, int length) => inbound.decrypt(bytes, length);
    }

    private sealed class Rig : IDisposable
    {
        public readonly WorldClient Client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        public readonly SequenceCipher Cipher = new();
        private readonly Socket receiver = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private readonly Socket sender = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private int seen;

        public Rig()
        {
            receiver.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            Set("flushLock", new object()); Set("<TransportFence>k__BackingField", new WorldSendFence());
            Set("cypher", Cipher); Set("socket", sender); Set("Remote", receiver.LocalEndPoint!);
            Client.playerData = new ClientData(); Client.playerData.setOnWorld(true);
            Client.messageQueue = new MessageQueue(); Client.viewMan = new();
        }

        private void Set(string field, object value) => typeof(WorldClient)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Client, value);

        public void Queue(int count)
        {
            for (int i = 0; i < count; i++) Client.messageQueue.addRpcMessage(new byte[] { 0x81, 0x4d, 1, 2, 3, 4 });
        }

        /// <summary>Flushes and returns the packets that carried RPCs: their sequence and RPC counters as sent.</summary>
        public List<(ushort Sseq, int[] Counters)> Flush() =>
            FlushWithBlocks().Select(p => (p.Sseq, p.Counters)).ToList();

        /// <summary>Flushes and returns the packets that carried RPCs along with their protocol-0x04 block headers.</summary>
        public List<(ushort Sseq, int[] Counters, (ushort Counter, int Count)[] Blocks)> FlushWithBlocks()
        {
            Client.FlushQueue(force: true);
            List<(byte[] Plain, ushort Sseq, ushort Pss)> fresh;
            lock (Cipher.Sent) { fresh = Cipher.Sent.Skip(seen).ToList(); seen = Cipher.Sent.Count; }
            return fresh.Select(p =>
                {
                    var payload = HeadlessClient.WireFormat.ParsePayload(p.Plain);
                    return (p.Sseq,
                        payload.Rpcs.Select(r => (int)(ushort)(r.ListCounter + r.Index)).ToArray(),
                        payload.RpcLists.ToArray());
                })
                .Where(p => p.Item2.Length > 0).ToList();
        }

        public void QueuePayload(int count, int payloadBytes)
        {
            byte[] body = new byte[payloadBytes];
            body[0] = 0x81; body[1] = 0x4d;
            for (int i = 0; i < count; i++) Client.messageQueue.addRpcMessage((byte[])body.Clone());
        }

        public SequencedMessage[] Pending() =>
            Client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().ToArray();

        /// <summary>Makes the RPCs with these counters due (or not) by moving their last send time back.</summary>
        public void Age(long ms, params int[] counters)
        {
            long now = Environment.TickCount64;
            foreach (SequencedMessage m in Pending().Where(m => counters.Contains(m.rpcNumber))) m.lastRpcSendTick = now - ms;
        }

        private readonly WorldEncryption peer = new();
        private ushort clientSequence;

        public void Ack(ushort sseq, ushort bits = 0) => ReceiveAck(++clientSequence, sseq, bits);

        public byte[] ReceiveAck(ushort sequence, ushort sseq, ushort bits)
        {
            byte[] bare = { 0x02 };
            byte[] datagram = peer.encrypt(bare, bare.Length, bits, sseq, sequence);
            byte[] decoded = Client.DecryptReceivedPacket(datagram);
            Assert.Equal(bare, decoded);
            return decoded;
        }

        public void Dispose() { receiver.Dispose(); sender.Dispose(); }
    }

    private static int[] Range(int from, int count) => Enumerable.Range(from, count).ToArray();

    [Fact]
    public void AResendIsOneAscendingRunFromTheOldestUnackedRpc()
    {
        using var rig = new Rig();
        // Rounds 3, 4 and 5 of the native fight: two RPCs each, in their own flushes, none acked
        rig.Queue(2); Assert.Equal(Range(0, 2), rig.Flush().Single().Counters);
        rig.Queue(2); Assert.Equal(Range(2, 2), rig.Flush().Single().Counters);
        rig.Queue(2); Assert.Equal(Range(4, 2), rig.Flush().Single().Counters);

        // 2-3 have waited out the resend interval; 0-1 and 4-5 were sent again or first sent more recently. Per-RPC
        // timers sent 2-3 alone here, ahead of the still-unacked 0-1.
        rig.Age(SequencedMessage.RpcResendIntervalMs + 50, 2, 3);
        rig.Age(100, 0, 1, 4, 5);
        rig.Queue(3);
        var packets = rig.Flush();
        int[] wire = packets.SelectMany(p => p.Counters).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 6, 7, 8 }, wire); // 0-3 again, oldest first, then the new 6-8; 4-5 aren't due
        Assert.Equal(0, packets[0].Counters[0]);

        // The run shares one send time from now on: the next resend is again everything unacked, in order
        rig.Age(SequencedMessage.RpcResendIntervalMs + 1, Range(0, 9));
        Assert.Equal(Range(0, 9), rig.Flush().SelectMany(p => p.Counters).ToArray());
    }

    [Fact]
    public void AcksRemoveExactlyTheRpcsOfTheAckedPacketsAndTheRestResendInOrder()
    {
        using var rig = new Rig();
        rig.Queue(3); var first = rig.Flush().Single();
        rig.Queue(3); var second = rig.Flush().Single();
        rig.Queue(3); var third = rig.Flush().Single();
        Assert.Equal(new[] { Range(0, 3), Range(3, 3), Range(6, 3) }, new[] { first.Counters, second.Counters, third.Counters });
        // One sequence per packet, as retail
        Assert.Equal(first.Sseq + 1, second.Sseq);
        Assert.Equal(second.Sseq + 1, third.Sseq);

        // The client acks only the middle packet: its RPCs are done, the ones on either side are not
        rig.Ack(second.Sseq);
        Assert.Equal(new[] { 0, 1, 2, 6, 7, 8 }, rig.Pending().Select(m => m.rpcNumber).ToArray());

        rig.Age(SequencedMessage.RpcResendIntervalMs + 1, 0, 1, 2, 6, 7, 8);
        var resent = rig.Flush();
        Assert.Equal(new[] { 0, 1, 2, 6, 7, 8 }, resent.SelectMany(p => p.Counters).ToArray());
        Assert.All(rig.Pending(), m => Assert.Equal(2, m.sendCounter));

        // The resend went out in one packet; an ack of the next sequence whose bit 0 names it covers it
        var again = Assert.Single(resent);
        rig.Client.playerData.setPss(0x01);
        rig.Client.DeletedAckedPackets((ushort)(again.Sseq + 1));
        Assert.Empty(rig.Pending());
    }

    [Theory]
    [InlineData(348, 357)]
    [InlineData(1837, 1883)]
    public void NativeBareAckReplyCarriesHistoryWithoutRequestingAnotherAck(
        ushort clientSequence, ushort serverSequence)
    {
        // Both captures resume after the client repeats its last ACK with bit 7 set.
        // 3190e49da1d5 lines 4192/4341. 86b9468137d4 lines 19416/19589.
        using var rig = new Rig();
        var protocol = new MultiProtocolManager();
        var previous = Store.currentClient;
        Store.currentClient = rig.Client;
        try
        {
            for (int sequence = clientSequence - 8; sequence < clientSequence; sequence++)
                rig.ReceiveAck((ushort)sequence, (ushort)(serverSequence - 1), 0x7f);

            rig.Client.playerData.setSseq(serverSequence);
            void AcceptAndFlush(ushort client, ushort bits)
            {
                protocol.Parse(true, rig.ReceiveAck(client, serverSequence, bits));
                rig.Flush();
            }

            AcceptAndFlush(clientSequence, 0x7f);
            var reply = Assert.Single(rig.Cipher.Sent);
            Assert.True(HeadlessClient.WireFormat.ParsePayload(reply.Plain).IsAckOnly);
            Assert.Equal(serverSequence + 1, reply.Sseq);
            Assert.Equal((ushort)0x7f, reply.Pss);

            AcceptAndFlush((ushort)(clientSequence + 1), 0xff);
            Assert.Equal((ushort)0x7f, rig.Cipher.Sent.Last().Pss);
            Assert.Equal(serverSequence + 2, rig.Cipher.Sent.Last().Sseq);
            Assert.Equal(2, rig.Cipher.Sent.Count);
        }
        finally
        {
            Store.currentClient = previous;
        }
    }

    [Theory]
    // Native master at PacketLog 4212/4436/4454, and pre-1222 at 19441/19594/19622.
    [InlineData(43, 357, 360, 381, 382, 0x7f,
        "4700000001023a00000000801f0005000c004a001a0003000000010000000001034c00000000800c0017001e001b00",
        "45010002000100000001023a00000000801f0005000c004a0000000201042300000000802900320014004a0000000000010500030005000300000000000100000000000000000000001200feff05b501000000000000000000")]
    [InlineData(52, 1883, 1887, 1905, 1905, 0,
        "4700000001023500000000800e002700070008001a0003000000010000000001032f0000000080590048003b000c00",
        "45010002000000000001023500000000800e0027000700080000000101043d0000000080300049002200510000000000010500030005000300000000000800000000000000090007001200feffb2a504000000000000000000")]
    public void CapturedIdleAckCannotRetireCombatBeforeTheFirstCoveringReceipt(
        ushort counter, ushort staleAck, ushort firstSend, ushort lastSend, ushort finalAck, ushort finalBits,
        string nextMove, string round)
    {
        using var rig = new Rig();
        rig.Client.playerData.setRPCCounter(counter);
        rig.Client.playerData.setSseq((ushort)(firstSend - 1));
        rig.Client.messageQueue.addRpcMessage(Convert.FromHexString(nextMove));
        rig.Client.messageQueue.addRpcMessage(Convert.FromHexString(round));
        var first = Assert.Single(rig.Flush());
        Assert.Equal(firstSend, first.Sseq);
        Assert.Equal(Range(counter, 2), first.Counters);
        rig.Ack(staleAck, 0x7f);
        Assert.Equal(2, rig.Pending().Length);

        // Advance past the datagrams between the first and final transmissions of this same pair.
        rig.Client.playerData.setSseq((ushort)(lastSend - 1));
        rig.Age(SequencedMessage.RpcResendIntervalMs + 1, Range(counter, 2));
        var retry = Assert.Single(rig.Flush());
        Assert.Equal(lastSend, retry.Sseq);
        Assert.Equal(first.Counters, retry.Counters);
        rig.Ack(finalAck, finalBits);
        Assert.Empty(rig.Pending());
        Assert.Empty(rig.Flush());
    }

    [Fact]
    public void AClientAcknowledgingEveryDatagramGetsNoRpcResends()
    {
        using var rig = new Rig();
        rig.Client.playerData.setSseq(4090);
        for (int i = 0; i < 18; i++)
        {
            rig.Queue(3);
            var packet = Assert.Single(rig.Flush());
            Assert.Equal(Range(i * 3, 3), packet.Counters);
            var pending = rig.Pending();
            rig.Ack(packet.Sseq, 0x7f);
            foreach (var rpc in pending) rpc.lastRpcSendTick -= SequencedMessage.RpcResendIntervalMs;
            Assert.Empty(rig.Flush());
            Assert.All(pending, rpc => Assert.Equal((ushort)1, rpc.sendCounter));
        }
        Assert.Empty(rig.Pending());
    }

    [Fact]
    public void ABacklogPastTheWindowWaitsForAcksInsteadOfGrowingTheResend()
    {
        using var rig = new Rig();
        int window = MessageQueue.MaxRpcsInFlight;
        rig.Queue(window + 100);
        var sent = rig.Flush();
        Assert.Equal(Range(0, window), sent.SelectMany(p => p.Counters).ToArray());
        Assert.Equal(100, rig.Pending().Count(m => m.rpcNumber < 0));

        // A client that isn't acking gets the same bounded run again, and nothing new
        rig.Age(SequencedMessage.RpcResendIntervalMs + 1, Range(0, window));
        Assert.Equal(Range(0, window), rig.Flush().SelectMany(p => p.Counters).ToArray());

        // Once it acks, the rest follows in order
        foreach (var packet in sent) rig.Ack(packet.Sseq);
        Assert.Equal(Range(window, 100), rig.Flush().SelectMany(p => p.Counters).ToArray());
    }

    [Fact]
    public void ResendOrderFollowsTheCounterAcrossTheSixteenBitWrap()
    {
        var queue = new MessageQueue();
        foreach (int number in new[] { 1, 65534, 0, 65535 })
        {
            queue.addRpcMessage(new byte[] { 0x81, 0x4d });
            var m = (SequencedMessage)queue.RPCMessagesQueue[queue.RPCMessagesQueue.Count - 1]!;
            m.rpcNumber = number; m.sendCounter = 1; m.lastRpcSendTick = number == 0 ? 0 : 10_000;
        }
        queue.addRpcMessage(new byte[] { 0x81, 0x4d });

        // Only counter 0 is due, so it brings the two before it (65534, 65535) but not 1, then the new RPC
        var take = queue.TakeRpcsToSend(10_000, 2, out MessageQueue.RpcResend resend);
        Assert.Equal(new[] { 65534, 65535, 0, -1 }, take.Select(m => m.rpcNumber).ToArray());
        Assert.Equal((65534, 0, 3), (resend.First, resend.Last, resend.Count));
    }

    [Fact]
    public void NothingIsResentBeforeTheInterval()
    {
        using var rig = new Rig();
        rig.Queue(4); rig.Flush();
        Assert.Empty(rig.Flush());
        Assert.All(rig.Pending(), m => Assert.Equal(1, m.sendCounter));
    }

    [Fact]
    public void ResendsPreserveImmutableRpcBlockBoundariesForStockClient()
    {
        // Reproduces pr1227b-native SSEQ 287 (0x73..0x77) + SSEQ 291 (0x73..0x79):
        // stock client.dll CLTMessageBargeGuar::RCCInfoGuar::HandleInput (0x623C73B0..0x623C7798) checks only
        // blockStart < m_nextExpectedSeq and skips the entire block. If a resend merged previously-sent blocks
        // (0,1)+(1,2)+(3,2)+(5,2) into (0,7) after the client received (0,5), the stock client skipped (0,7)
        // completely and permanently deadlocked at expected=5 (blocking all subsequent RPCs including 81 1a/81 19).
        using var rig = new Rig();
        rig.Queue(1); Assert.Equal(new[] { ((ushort)0, 1) }, rig.FlushWithBlocks().Single().Blocks);
        rig.Queue(2); Assert.Equal(new[] { ((ushort)1, 2) }, rig.FlushWithBlocks().Single().Blocks);
        rig.Queue(2); Assert.Equal(new[] { ((ushort)3, 2) }, rig.FlushWithBlocks().Single().Blocks);
        rig.Queue(2); Assert.Equal(new[] { ((ushort)5, 2) }, rig.FlushWithBlocks().Single().Blocks);

        // First resend covers 0..4; second resend 100 ms later covers 0..6 (before ACK for first resend arrives).
        rig.Age(SequencedMessage.RpcResendIntervalMs + 50, 0, 1, 2, 3, 4);
        var firstResend = rig.FlushWithBlocks().Single();
        Assert.Equal(new[] { ((ushort)0, 1), ((ushort)1, 2), ((ushort)3, 2) }, firstResend.Blocks);

        rig.Age(SequencedMessage.RpcResendIntervalMs + 50, 0, 1, 2, 3, 4, 5, 6);
        var secondResend = rig.FlushWithBlocks().Single();
        Assert.Equal(new[] { ((ushort)0, 1), ((ushort)1, 2), ((ushort)3, 2), ((ushort)5, 2) }, secondResend.Blocks);

        // Feed both resend packets through stock CLTMessageBargeGuar's exact block-start sequence rule.
        ushort expected = 0;
        var delivered = new List<ushort>();
        void ReceiveStockBargeGuar((ushort Counter, int Count)[] blocks)
        {
            foreach (var (start, count) in blocks)
            {
                if (((start - expected) & 0xFFFF) >= 0x8000) continue; // stock client skips whole block
                Assert.Equal(expected, start);
                for (int i = 0; i < count; i++) delivered.Add((ushort)(start + i));
                expected = (ushort)(expected + count);
            }
        }

        ReceiveStockBargeGuar(firstResend.Blocks);
        Assert.Equal((ushort)5, expected);

        ReceiveStockBargeGuar(secondResend.Blocks);
        Assert.Equal((ushort)7, expected);

        // Server receives ACK for secondResend, then sends 81 1a + 81 19 (counters 7..8): delivers cleanly.
        rig.Ack(secondResend.Sseq);
        Assert.Empty(rig.Pending());
        rig.Queue(2);
        var lootOpen = rig.FlushWithBlocks().Single();
        Assert.Equal(new[] { ((ushort)7, 2) }, lootOpen.Blocks);
        ReceiveStockBargeGuar(lootOpen.Blocks);
        Assert.Equal((ushort)9, expected);
        Assert.Equal(Enumerable.Range(0, 9).Select(i => (ushort)i).ToArray(), delivered.ToArray());
    }

    [Fact]
    public void ResendsNeverMergeWithFreshRpcsAndNeverSplitExistingBlocksAcrossMtu()
    {
        using var rig = new Rig();
        rig.QueuePayload(2, 400); // block (0, 2) = ~804 bytes
        var first = rig.FlushWithBlocks().Single();
        Assert.Equal(new[] { ((ushort)0, 2) }, first.Blocks);

        // Resend block (0, 2) together with a fresh RPC (2, 1): must stay two distinct blocks, not (0, 3).
        rig.Age(SequencedMessage.RpcResendIntervalMs + 50, 0, 1);
        rig.QueuePayload(1, 200);
        var combined = rig.FlushWithBlocks().Single();
        Assert.Equal(new[] { ((ushort)0, 2), ((ushort)2, 1) }, combined.Blocks);

        // Queue another ~804-byte block (3, 2) and resend all three blocks: MTU split must happen BETWEEN blocks,
        // never slicing block (3, 2) across two datagrams.
        rig.QueuePayload(2, 400);
        Assert.Equal(new[] { ((ushort)3, 2) }, rig.FlushWithBlocks().Single().Blocks);

        rig.Age(SequencedMessage.RpcResendIntervalMs + 50, 0, 1, 2, 3, 4);
        var packets = rig.FlushWithBlocks();
        Assert.Equal(2, packets.Count);
        Assert.Equal(new[] { ((ushort)0, 2), ((ushort)2, 1) }, packets[0].Blocks);
        Assert.Equal(new[] { ((ushort)3, 2) }, packets[1].Blocks);
    }
}
