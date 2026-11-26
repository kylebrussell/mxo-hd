using System.Buffers.Binary;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class MovementCoalescingTests
{
    private static (SelfViewUpdate Update, byte[] Forwarded) ParsePos(float x, float y, float z, byte? action = null, byte? yaw = null)
    {
        byte flags = SelfViewUpdate.FlagPosition;
        if (action.HasValue) flags |= SelfViewUpdate.FlagAction;
        if (yaw.HasValue) flags |= SelfViewUpdate.FlagYaw;

        var buf = new List<byte> { flags };
        if (action.HasValue) buf.Add(action.Value);
        if (yaw.HasValue) buf.Add(yaw.Value);
        buf.AddRange(BitConverter.GetBytes(x));
        buf.AddRange(BitConverter.GetBytes(y));
        buf.AddRange(BitConverter.GetBytes(z));
        byte[] raw = buf.ToArray();

        Assert.True(SelfViewUpdate.TryParse(raw, 0, out SelfViewUpdate update));
        byte[] forwarded = update.ToForwardedUpdate(raw);
        Assert.NotNull(forwarded);
        return (update, forwarded);
    }

    [Fact]
    public void Repeated_position_updates_for_same_view_coalesce_to_latest_position()
    {
        var q = new MessageQueue();
        var (u1, f1) = ParsePos(100f, 495f, 200f);
        var (u2, f2) = ParsePos(150f, 495f, 250f);
        var (u3, f3) = ParsePos(300f, 495f, 400f);

        q.AddOrCoalesceMovement(12, u1, f1);
        q.AddOrCoalesceMovement(12, u2, f2);
        q.AddOrCoalesceMovement(12, u3, f3);

        var only = Assert.IsType<SequencedMessage>(Assert.Single(q.ObjectMessagesQueue));
        Assert.Equal(MessageQueue.BuildViewUpdateFast(12, f3), only.content);
    }

    [Fact]
    public void Coalescing_preserves_prior_action_and_yaw_when_later_update_carries_position_only()
    {
        var q = new MessageQueue();
        var (u1, f1) = ParsePos(100f, 495f, 200f, action: 0x3e, yaw: 0x40);
        var (u2, f2) = ParsePos(220f, 495f, 330f);

        q.AddOrCoalesceMovement(15, u1, f1);
        q.AddOrCoalesceMovement(15, u2, f2);

        var only = Assert.IsType<SequencedMessage>(Assert.Single(q.ObjectMessagesQueue));
        Assert.Equal((ushort)15, BinaryPrimitives.ReadUInt16LittleEndian(only.content));
        Assert.Equal(SelfViewEntry.PartState, only.content[2]);
        Assert.Equal((byte)(SelfViewUpdate.FlagAction | SelfViewUpdate.FlagYaw | SelfViewUpdate.FlagPosition), only.content[3]);
        Assert.Equal(0x3e, only.content[4]);
        Assert.Equal(0x40, only.content[5]);
        Assert.Equal(220f, BitConverter.ToSingle(only.content, 6));
        Assert.Equal(495f, BitConverter.ToSingle(only.content, 10));
        Assert.Equal(330f, BitConverter.ToSingle(only.content, 14));
    }

    [Fact]
    public void Distinct_views_and_non_movement_messages_stay_separate_and_sent_entries_do_not_mutate()
    {
        var q = new MessageQueue();
        var (a1, fa1) = ParsePos(10f, 0f, 20f);
        var (a2, fa2) = ParsePos(30f, 0f, 40f);
        var (b1, fb1) = ParsePos(50f, 0f, 60f);

        q.addObjectMessage(new byte[] { 0x01, 0x00, 0x02 }, false);
        q.AddOrCoalesceMovement(10, a1, fa1);
        q.AddOrCoalesceMovement(11, b1, fb1);
        q.AddOrCoalesceMovement(10, a2, fa2);

        Assert.Equal(3, q.ObjectMessagesQueue.Count);
        var firstA = (SequencedMessage)q.ObjectMessagesQueue[1]!;
        Assert.Equal(MessageQueue.BuildViewUpdateFast(10, fa2), firstA.content);

        // Once sent, a subsequent move for view 10 appends a new entry instead of mutating the sent one.
        byte[] sentSnapshot = (byte[])firstA.content.Clone();
        firstA.MarkObjectSent(100);
        q.OnObjectMessageSentLocked(firstA);

        var (a3, fa3) = ParsePos(99f, 0f, 88f);
        q.AddOrCoalesceMovement(10, a3, fa3);

        Assert.Equal(4, q.ObjectMessagesQueue.Count);
        Assert.Equal(sentSnapshot, firstA.content);
        Assert.Equal(MessageQueue.BuildViewUpdateFast(10, fa3), ((SequencedMessage)q.ObjectMessagesQueue[3]!).content);
    }

    [Fact]
    public void Clear_pending_movement_and_queue_clear_reset_coalescing_state()
    {
        var q = new MessageQueue();
        var (u1, f1) = ParsePos(1f, 2f, 3f);
        var (u2, f2) = ParsePos(4f, 5f, 6f);

        q.AddOrCoalesceMovement(20, u1, f1);
        q.ClearPendingMovement(20);
        q.AddOrCoalesceMovement(20, u2, f2);
        Assert.Equal(2, q.ObjectMessagesQueue.Count);

        // Direct external Clear() as used by unit tests must not leave a stale coalescing reference.
        q.ObjectMessagesQueue.Clear();
        q.AddOrCoalesceMovement(20, u1, f1);
        Assert.Single(q.ObjectMessagesQueue);
    }

    private sealed class RecordingCipher : IWorldEncryption
    {
        public readonly List<byte[]> Plain = new();
        public readonly List<(ushort Bits, ushort ClientSequence, ushort ServerSequence)> Headers = new();
        private readonly WorldEncryption inbound = new();

        public byte[] encrypt(byte[] bytes, int length, ushort pss, ushort cseq, ushort sseq)
        {
            lock (Plain)
            {
                Plain.Add(bytes.ToArray());
                Headers.Add((pss, cseq, sseq));
            }
            return bytes;
        }

        public ArrayList decrypt(byte[] bytes, int length) => inbound.decrypt(bytes, length);
    }

    [Fact]
    public void Single_client_acking_every_packet_with_coalesced_and_deferred_movements_produces_zero_rpc_resends()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        var fence = new WorldSendFence();
        var recorder = new RecordingCipher();
        var peer = new WorldEncryption();
        using var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        receiver.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        static void Set(WorldClient c, string field, object value) =>
            typeof(WorldClient).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, value);

        Set(client, "flushLock", new object());
        Set(client, "<TransportFence>k__BackingField", fence);
        Set(client, "cypher", recorder);
        Set(client, "socket", sender);
        Set(client, "Remote", receiver.LocalEndPoint!);
        client.playerData = new ClientData();
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();

        ushort clientSeq = 0;
        ushort clientLastServerSeq = 0;
        byte clientAckBits = 0;

        void ObserveServerSeq(ushort sseq)
        {
            int advance = (sseq - clientLastServerSeq) & 0xfff;
            if (advance > 0 && advance < 0x800)
            {
                clientAckBits = advance > 7 ? (byte)0 : (byte)(((clientAckBits << advance) | (1 << (advance - 1))) & 0x7f);
                clientLastServerSeq = sseq;
            }
        }

        void SendClientAck()
        {
            clientSeq = (ushort)((clientSeq + 1) & 0xfff);
            byte[] payload = { 0x02 };
            byte[] wire = peer.encrypt(payload, payload.Length, clientAckBits, clientLastServerSeq, clientSeq);
            Assert.Equal(payload, client.DecryptReceivedPacket(wire));
        }

        var trackedRpcs = new List<SequencedMessage>();
        int recordedCursor = 0;

        for (int round = 0; round < 25; round++)
        {
            // Coalesce multiple movements for the same views and defer via MarkFlushPending.
            var (m1, f1) = ParsePos(100f + round, 495f, 200f + round, action: 0x3e, yaw: 0x20);
            var (m2, f2) = ParsePos(110f + round, 495f, 210f + round);
            var (m3, f3) = ParsePos(300f + round, 495f, 400f + round, yaw: 0x40);
            client.messageQueue.AddOrCoalesceMovement(10, m1, f1);
            client.messageQueue.AddOrCoalesceMovement(10, m2, f2);
            client.messageQueue.AddOrCoalesceMovement(11, m3, f3);
            client.MarkFlushPending();

            // Interleave an ordinary non-movement object update and reliable RPCs.
            client.messageQueue.addObjectMessage(new byte[] { 0x02, 0x00, 0x02, 0x80, 0x80, 0x80, 0x40, (byte)round }, false);
            client.messageQueue.addRpcMessage(new byte[] { 0x81, 0x49, (byte)round });
            var firstRpc = (SequencedMessage)client.messageQueue.RPCMessagesQueue[client.messageQueue.RPCMessagesQueue.Count - 1]!;
            trackedRpcs.Add(firstRpc);

            if (round % 3 == 0)
            {
                client.messageQueue.addRpcMessage(new byte[] { 0x81, 0x4b, (byte)round });
                trackedRpcs.Add((SequencedMessage)client.messageQueue.RPCMessagesQueue[client.messageQueue.RPCMessagesQueue.Count - 1]!);
            }

            // Flush via FlushThread's TickFlush (deferred path) on even rounds and direct FlushQueue on odd rounds.
            if (round % 2 == 0)
            {
                client.TickFlush();
            }
            else
            {
                client.FlushQueue(force: true);
            }

            Assert.True(recorder.Headers.Count > recordedCursor, $"Round {round} emitted at least one datagram");
            for (int i = recordedCursor; i < recorder.Headers.Count; i++)
            {
                var header = recorder.Headers[i];
                Assert.Equal(0, header.Bits & 0x80);
                ObserveServerSeq(header.ServerSequence);
            }
            recordedCursor = recorder.Headers.Count;

            // Ack every emitted datagram (object packet + RPC packet) with stock 7-bit receipt history.
            SendClientAck();

            // Immediately retired on the first covering ACK: no RPCs or object messages left queued.
            Assert.Empty(client.messageQueue.RPCMessagesQueue);
            Assert.Empty(client.messageQueue.ObjectMessagesQueue);

            // A subsequent resend sweep must emit nothing.
            client.CheckAndResend();
            Assert.Equal(recordedCursor, recorder.Headers.Count);
        }

        Assert.All(trackedRpcs, rpc => Assert.Equal((ushort)1, rpc.sendCounter));
        Assert.True(client.messageQueue.ClientReceipts.Bits == 0x7f, "ClientReceipts saturates at 7 bits (0x7f), never 0xff");
    }
}
