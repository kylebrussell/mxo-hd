using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

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
        public readonly List<(byte[] Plain, ushort Sseq)> Sent = new();
        public byte[] encrypt(byte[] bytes, int length, ushort pss, ushort cseq, ushort sseq)
        { lock (Sent) Sent.Add((bytes.ToArray(), sseq)); return bytes; }
        public ArrayList decrypt(byte[] bytes, int length) => throw new NotSupportedException();
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
        public List<(ushort Sseq, int[] Counters)> Flush()
        {
            Client.FlushQueue(force: true);
            List<(byte[] Plain, ushort Sseq)> fresh;
            lock (Cipher.Sent) { fresh = Cipher.Sent.Skip(seen).ToList(); seen = Cipher.Sent.Count; }
            return fresh.Select(p => (p.Sseq, HeadlessClient.WireFormat.ParsePayload(p.Plain).Rpcs
                    .Select(r => (int)(ushort)(r.ListCounter + r.Index)).ToArray()))
                .Where(p => p.Item2.Length > 0).ToList();
        }

        public SequencedMessage[] Pending() =>
            Client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().ToArray();

        /// <summary>Makes the RPCs with these counters due (or not) by moving their last send time back.</summary>
        public void Age(long ms, params int[] counters)
        {
            long now = Environment.TickCount64;
            foreach (SequencedMessage m in Pending().Where(m => counters.Contains(m.rpcNumber))) m.lastRpcSendTick = now - ms;
        }

        public void Ack(ushort sseq)
        {
            Client.playerData.setPss(0);
            Client.DeletedAckedPackets(sseq);
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
}
