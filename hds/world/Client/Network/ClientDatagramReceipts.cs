using System;

namespace hds
{
    /// <summary>Peer datagrams received in the current transport epoch, including bare acknowledgments.</summary>
    internal sealed class ClientDatagramReceipts
    {
        // Retail starts from implicit sequence zero. The first real client sequence (one) reports bit zero.
        internal ushort Latest { get; private set; }
        internal byte Bits { get; private set; }

        internal void Reset() { Latest = 0; Bits = 0; }

        internal void Observe(ushort sequence)
        {
            sequence = (ushort)(sequence & 0xfff);
            int advance = (sequence - Latest) & 0xfff;
            if (advance == 0) return;

            if (advance < 0x800)
            {
                Bits = advance > SequencedMessage.AckHistoryBits ? (byte)0
                    : (byte)(((Bits << advance) | (1 << (advance - 1))) & SequencedMessage.AckHistoryMask);
                Latest = sequence;
            }
            else
            {
                int behind = (Latest - sequence) & 0xfff;
                if (behind <= SequencedMessage.AckHistoryBits) Bits |= (byte)(1 << (behind - 1));
            }
        }
    }

    /// <summary>Retry at a lower rate only when ack progress and endpoint ingress have both stalled.</summary>
    internal sealed class RpcResendBackoff
    {
        internal const int StalledCycles = 4;
        internal const long QuietMs = 5000;
        internal const long SlowIntervalMs = 4000;
        private ushort latestAck;
        private int retryCycles;

        internal void Reset() { latestAck = 0; retryCycles = 0; }

        internal void ObserveAck(ushort ack)
        {
            int advance = (ack - latestAck) & 0xfff;
            if (advance == 0 || advance >= 0x800) return;
            latestAck = (ushort)(ack & 0xfff);
            retryCycles = 0;
        }

        internal void RecordRetry()
        {
            if (retryCycles < StalledCycles) retryCycles++;
        }

        internal long Interval(long now, long lastIngress) =>
            retryCycles >= StalledCycles && now - lastIngress >= QuietMs
                ? SlowIntervalMs : SequencedMessage.RpcResendIntervalMs;
    }
}
