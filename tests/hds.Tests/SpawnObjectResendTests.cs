using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;
using hds;

namespace hds.Tests;

public class SpawnObjectResendTests
{
    private static WorldClient CreateTestClient(out ClientData data, out MessageQueue queue)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        data = new ClientData();
        data.setCharID(110);
        data.worldGeneration = 1;
        queue = new MessageQueue();
        client.playerData = data;
        client.messageQueue = queue;
        return client;
    }

    [Fact]
    public void ReliableSpawnObjectSurvivesCumulativeAckWithPssGapAndResends()
    {
        var client = CreateTestClient(out var data, out var queue);
        long epoch = 42;
        var selfSpawn = queue.AddReliableObject(new byte[] { 0x01, 0x00, 0x0c, 0x0c }, 1, epoch);
        var initialNav = queue.AddReliableObject(new byte[] { 0x02, 0x00, 0x01, 0x1c }, 1, epoch, selfSpawn);
        Assert.Equal(2, queue.ReliableObjectCount);

        // Simulate first send on SSEQ 4 (selfSpawn) and SSEQ 6 (initialNav).
        long t0 = 10_000;
        selfSpawn.MarkObjectSent(t0);
        selfSpawn.setNeededAck(4);
        selfSpawn.RecordReliableObjectPacket(4);

        initialNav.MarkObjectSent(t0);
        initialNav.setNeededAck(6);
        initialNav.RecordReliableObjectPacket(6);

        // Exact pr1248-stuck-load ACK: SSEQ 7 PSS 0x78 (0..3 and 7 received; 4, 5, 6 missing).
        data.setPss(0x78);
        client.DeletedAckedPackets(7);

        Assert.Equal(2, queue.ObjectMessagesQueue.Count);
        Assert.Equal(2, queue.ReliableObjectCount);
        Assert.False(selfSpawn.ReliableObjectAcked);
        Assert.False(initialNav.ReliableObjectAcked);
        Assert.True(selfSpawn.ReliableObjectGapObserved);
        Assert.True(initialNav.ReliableObjectGapObserved);

        // Because a gap was explicitly observed, retry fires at 500 ms (not 1000 ms).
        Assert.False(selfSpawn.IsObjectSendDue(t0 + SequencedMessage.ReliableObjectResendIntervalMs - 1));
        Assert.True(selfSpawn.IsObjectSendDue(t0 + SequencedMessage.ReliableObjectResendIntervalMs));
        Assert.True(initialNav.IsObjectSendDue(t0 + SequencedMessage.ReliableObjectResendIntervalMs));

        // Simulate resend packed into SSEQ 9.
        long t1 = t0 + SequencedMessage.ReliableObjectResendIntervalMs;
        selfSpawn.MarkObjectSent(t1);
        selfSpawn.setNeededAck(9);
        selfSpawn.RecordReliableObjectPacket(9);
        initialNav.MarkObjectSent(t1);
        initialNav.setNeededAck(9);
        initialNav.RecordReliableObjectPacket(9);

        // Client receives SSEQ 9 and ACKs SSEQ 9 PSS 0x7f.
        data.setPss(0x7f);
        client.DeletedAckedPackets(9);

        Assert.Empty(queue.ObjectMessagesQueue);
        Assert.Equal(0, queue.ReliableObjectCount);
        Assert.True(selfSpawn.ReliableObjectAcked);
        Assert.True(initialNav.ReliableObjectAcked);
    }

    [Fact]
    public void PrematureNavAckBeforeLostSelfViewClearsRecordAndResendsBehindSelfView()
    {
        var client = CreateTestClient(out var data, out var queue);
        long epoch = 7;
        var selfSpawn = queue.AddReliableObject(new byte[] { 0x01, 0x00, 0x0c, 0x0c }, 1, epoch);
        var initialNav = queue.AddReliableObject(new byte[] { 0x02, 0x00, 0x01, 0x1c }, 1, epoch, selfSpawn);

        long t0 = 20_000;
        selfSpawn.MarkObjectSent(t0);
        selfSpawn.setNeededAck(4);
        selfSpawn.RecordReliableObjectPacket(4);

        initialNav.MarkObjectSent(t0);
        initialNav.setNeededAck(6);
        initialNav.RecordReliableObjectPacket(6);

        // Exact pr1250-stuck-load ACK: SSEQ 7 PSS 0x69 (6 arrived early before view 2 existed; 4 & 5 dropped).
        data.setPss(0x69);
        client.DeletedAckedPackets(7);

        // Neither may retire yet, and initialNav's premature SSEQ 6 record must be cleared.
        Assert.Equal(2, queue.ObjectMessagesQueue.Count);
        Assert.Equal(2, queue.ReliableObjectCount);
        Assert.False(selfSpawn.ReliableObjectAcked);
        Assert.False(initialNav.ReliableObjectAcked);
        Assert.True(selfSpawn.ReliableObjectGapObserved);
        Assert.True(initialNav.ReliableObjectGapObserved);
        Assert.False(initialNav.IsReliableObjectAckedBy(new ushort[] { 6 }));

        // Even if selfSpawn alone is resent on SSEQ 9 and client ACKs SSEQ 9 PSS 0x7f (whose 7-bit mask still covers 6),
        // initialNav must NOT retire on the old premature SSEQ 6.
        selfSpawn.MarkObjectSent(t0 + 500);
        selfSpawn.setNeededAck(9);
        selfSpawn.RecordReliableObjectPacket(9);

        data.setPss(0x7f);
        client.DeletedAckedPackets(9);

        Assert.True(selfSpawn.ReliableObjectAcked);
        Assert.Single(queue.ObjectMessagesQueue);
        Assert.Same(initialNav, queue.ObjectMessagesQueue[0]);
        Assert.Equal(1, queue.ReliableObjectCount);
        Assert.False(initialNav.ReliableObjectAcked);

        // Once initialNav resends on SSEQ 10 and is ACKed, it retires cleanly.
        initialNav.MarkObjectSent(t0 + 500);
        initialNav.setNeededAck(10);
        initialNav.RecordReliableObjectPacket(10);
        data.setPss(0x7f);
        client.DeletedAckedPackets(10);

        Assert.Empty(queue.ObjectMessagesQueue);
        Assert.Equal(0, queue.ReliableObjectCount);
        Assert.True(initialNav.ReliableObjectAcked);
    }

    [Fact]
    public void HealthyFirstSendWaitsGraceWindowBeforeRetryAndOrdinaryObjectsStayNonResent()
    {
        var client = CreateTestClient(out var data, out var queue);
        var selfSpawn = queue.AddReliableObject(new byte[] { 0x01, 0x00, 0x0c, 0x0c }, 1, 1);
        queue.addObjectMessage(new byte[] { 0x05, 0x00, 0x01, 0x02 }, false);
        var ordinary = (SequencedMessage)queue.ObjectMessagesQueue[1]!;

        long t0 = 50_000;
        selfSpawn.MarkObjectSent(t0);
        selfSpawn.setNeededAck(4);
        selfSpawn.RecordReliableObjectPacket(4);

        ordinary.MarkObjectSent(t0);
        ordinary.setNeededAck(5);

        // Without an observed gap, first retry waits FirstReliableObjectRetryMs (1000 ms).
        Assert.False(selfSpawn.IsObjectSendDue(t0 + SequencedMessage.ReliableObjectResendIntervalMs));
        Assert.False(selfSpawn.IsObjectSendDue(t0 + SequencedMessage.FirstReliableObjectRetryMs - 1));
        Assert.True(selfSpawn.IsObjectSendDue(t0 + SequencedMessage.FirstReliableObjectRetryMs));

        // Ordinary object messages keep ObjectResendEnabled == false semantics.
        if (!SequencedMessage.ObjectResendEnabled)
        {
            Assert.False(ordinary.IsObjectSendDue(t0 + 60_000));
        }

        // Ordinary object still retires on cumulative ACK SSEQ 7 PSS 0x78, while reliable selfSpawn stays.
        data.setPss(0x78);
        client.DeletedAckedPackets(7);
        Assert.Single(queue.ObjectMessagesQueue);
        Assert.Same(selfSpawn, queue.ObjectMessagesQueue[0]);
        Assert.Equal(1, queue.ReliableObjectCount);
    }

    [Fact]
    public void ReliableObjectsHandle12BitWrapAndWorldEpochPruning()
    {
        var client = CreateTestClient(out var data, out var queue);
        var msg = queue.AddReliableObject(new byte[] { 0x01, 0x00, 0x0c, 0x0c }, 3, 99);

        msg.MarkObjectSent(1000);
        msg.setNeededAck(4095);
        msg.RecordReliableObjectPacket(4095);

        // Ack SSEQ 1 with PSS bit 1 set (1 - 2 = 4095 mod 4096).
        data.setPss(0x02);
        client.DeletedAckedPackets(1);
        Assert.Empty(queue.ObjectMessagesQueue);
        Assert.Equal(0, queue.ReliableObjectCount);

        // Stale world generation or transport epoch is pruned cleanly.
        queue.AddReliableObject(new byte[] { 0x01, 0x00, 0x0c, 0x0c }, 3, 99);
        Assert.Equal(1, queue.ReliableObjectCount);
        queue.PruneStaleReliableObjects(4, 99);
        Assert.Empty(queue.ObjectMessagesQueue);
        Assert.Equal(0, queue.ReliableObjectCount);

        // ForgetWorldObjects resets ReliableObjectCount.
        queue.AddReliableObject(new byte[] { 0x01, 0x00, 0x0c, 0x0c }, 4, 100);
        Assert.Equal(1, queue.ReliableObjectCount);
        queue.ForgetWorldObjects();
        Assert.Empty(queue.ObjectMessagesQueue);
        Assert.Equal(0, queue.ReliableObjectCount);
    }
}
