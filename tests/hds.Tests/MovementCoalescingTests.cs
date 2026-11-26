using System.Buffers.Binary;
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
}
