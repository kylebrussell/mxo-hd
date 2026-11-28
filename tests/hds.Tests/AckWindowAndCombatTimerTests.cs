using System;
using System.Collections.Generic;
using System.Threading;
using hds;

namespace hds.Tests;

public class AckWindowAndCombatTimerTests
{
    [Fact]
    public void AckWindowMatchesAckedSequencesExhaustivelyAcross12BitWrap()
    {
        ushort[] ackSeqs = { 0, 1, 7, 8, 42, 0xFFE, 0xFFF, 0x1005 };
        foreach (ushort ackSeq in ackSeqs)
        {
            for (ushort ackBits = 0; ackBits <= 0xFF; ackBits++)
            {
                HashSet<ushort> expected = SequencedMessage.AckedSequences(ackSeq, ackBits);
                var window = new SequencedMessage.AckWindow(ackSeq, ackBits);

                for (int delta = -2; delta <= 12; delta++)
                {
                    ushort candidate = (ushort)((ackSeq - delta) & 0xFFF);
                    Assert.Equal(expected.Contains(candidate), window.Contains(candidate));
                }
            }
        }
    }

    [Fact]
    public void SequencedMessageInlineSingleSendAndRetryPromotionMatchAckWindow()
    {
        var rpc = new SequencedMessage(new byte[] { 0x01, 0x02 });
        rpc.rpcNumber = 10;
        rpc.RecordRpcPacket(0x0FFE);

        var ackBefore = new SequencedMessage.AckWindow(0x0FFD, 0x7F);
        var ackExact = new SequencedMessage.AckWindow(0x0FFE, 0x00);
        var ackViaHistory = new SequencedMessage.AckWindow(0x0001, 0x04); // 0x0001 - 3 = 0x0FFE (bit 2 = 0x04)

        Assert.False(rpc.IsRpcAckedBy(in ackBefore));
        Assert.True(rpc.IsRpcAckedBy(in ackExact));
        Assert.True(rpc.IsRpcAckedBy(in ackViaHistory));
        Assert.True(rpc.IsRpcAckedBy(SequencedMessage.AckedSequences(0x0001, 0x04)));

        // Second distinct SSEQ promotes to HashSet while preserving the first sequence.
        rpc.RecordRpcPacket(0x0005);
        var ackRetry = new SequencedMessage.AckWindow(0x0005, 0x00);
        Assert.True(rpc.IsRpcAckedBy(in ackExact));
        Assert.True(rpc.IsRpcAckedBy(in ackRetry));
        Assert.Contains((ushort)0x0FFE, rpc.rpcPackets);
        Assert.Contains((ushort)0x0005, rpc.rpcPackets);

        // Reliable object tracking + ClearReliableObjectPackets.
        var obj = new SequencedMessage(new byte[] { 0x04, 0x00 }) { ReliableObject = true };
        obj.RecordReliableObjectPacket(0x0010);
        Assert.True(obj.IsReliableObjectAckedBy(new SequencedMessage.AckWindow(0x0010, 0x00)));
        Assert.True(obj.IsReliableObjectAckedBy(new SequencedMessage.AckWindow(0x0012, 0x02)));
        obj.RecordReliableObjectPacket(0x0018);
        Assert.True(obj.IsReliableObjectAckedBy(new SequencedMessage.AckWindow(0x0018, 0x00)));
        obj.ClearReliableObjectPackets();
        Assert.False(obj.IsReliableObjectAckedBy(new SequencedMessage.AckWindow(0x0010, 0x00)));
        Assert.False(obj.IsReliableObjectAckedBy(new SequencedMessage.AckWindow(0x0018, 0x00)));
    }

    [Fact]
    public void MessageQueueTakeRpcsToSendIntoReusesDestinationAndMatchesLegacyOrder()
    {
        var queue = new MessageQueue();
        queue.addRpcMessage(new byte[] { 0x80, 0x01 });
        queue.addRpcMessage(new byte[] { 0x80, 0x02 });

        var scratch = new List<SequencedMessage>(4);
        queue.TakeRpcsToSendInto(1000, 5, scratch, out MessageQueue.RpcResend resend1);
        Assert.Equal(2, scratch.Count);
        Assert.Equal(0, resend1.Count);

        // Mark first RPC sent and due, second fresh.
        scratch[0].rpcNumber = 4;
        scratch[0].rpcBlockStart = 4;
        scratch[0].sendCounter = 1;
        scratch[0].lastRpcSendTick = 100;

        queue.TakeRpcsToSendInto(1000, 5, scratch, out MessageQueue.RpcResend resend2);
        Assert.Equal(2, scratch.Count);
        Assert.Equal(1, resend2.Count);
        Assert.Equal(4, resend2.First);
        Assert.Equal(4, scratch[0].rpcNumber);
        Assert.Equal(-1, scratch[1].rpcNumber);
    }

    [Fact]
    public void CombatSessionTimerContainsExceptionsAndStopsIdempotently()
    {
        var session = new CombatSession(null, null, 0, CombatSession.CombatType.Melee);
        int ticks = 0;
        int endEvents = 0;
        using var sawTick = new ManualResetEventSlim(false);

        session.OnCombatTick += _ =>
        {
            int n = Interlocked.Increment(ref ticks);
            if (n == 1)
            {
                sawTick.Set();
                throw new InvalidOperationException("simulated tick fault");
            }
        };
        session.OnCombatEnd += (_, _) => Interlocked.Increment(ref endEvents);

        session.Start(25);
        try
        {
            Assert.True(sawTick.Wait(2000));
            // Wait briefly to confirm a second tick still fires after the caught fault.
            Thread.Sleep(80);
            Assert.True(Volatile.Read(ref ticks) >= 2);
        }
        finally
        {
            session.Stop(CombatSession.CombatEndReason.AttackerFled);
            session.Stop(CombatSession.CombatEndReason.AttackerFled); // idempotent
        }

        Assert.False(session.IsActive);
        Assert.Equal(1, endEvents);

        int ticksAtStop = Volatile.Read(ref ticks);
        Thread.Sleep(80);
        Assert.Equal(ticksAtStop, Volatile.Read(ref ticks));
    }
}
