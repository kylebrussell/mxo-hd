using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hds
{
    public class SequencedMessage
    {
        // Created with the queue entry; delayed/timed entries remain included in first-send latency.
        private readonly long scaleEnqueued=ScaleDiagnostics.Start;
        private int scaleFirstSent;
        public void RecordScaleFirstSend(bool objectMessage)
        {
            if(scaleEnqueued!=0 && System.Threading.Interlocked.Exchange(ref scaleFirstSent,1)==0)
                ScaleDiagnostics.FirstSend(scaleEnqueued,objectMessage);
        }
        internal ObjectMessageReceipt ObjectReceipt;
        internal SequencedMessage TimedViewWithRpc;
        internal ushort MovementViewId;
        internal byte MovementFlags;
        internal byte MovementAction;
        internal byte MovementYaw;
        internal byte[] MovementPosition;
        public byte[] content;
        public UInt16 sendCounter;
        private int resendTime;
        private UInt16 neededAck;
        public bool isTimed;
        public bool isSent = false;

        // Reliable RPCs, as Reality (rajkosto/mxoemu, GameClient::FlushQueue) sent them: an RPC keeps the counter it was
        // first sent with, remembers the packets that carried it, and goes out again until the client acks one of them.
        // The client acks a packet's sequence and, in its ack bits, the 8 before it, so a lost one shows up as a gap.
        public int rpcNumber = -1;
        // Immutable starting counter of the protocol-0x04 block this RPC was first numbered into. Stock client.dll
        // CLTMessageBargeGuar::RCCInfoGuar::HandleInput (0x623C73B0..0x623C7798) checks only the block start sequence
        // against m_nextExpectedSeq and skips an entire block when blockStart < expected, so resends must never merge
        // previously-sent blocks or fresh RPCs into a wider overlapping block.
        public int rpcBlockStart = -1;
        // Only explicitly world-bound producer records use this lifetime; ordinary reliable RPCs are unchanged.
        internal int? RpcWorldGeneration;
        internal long RpcTransportEpoch;
        internal bool DiscardUnassignedWorldRpc(int world,long transport) => rpcNumber < 0 &&
            RpcWorldGeneration.HasValue && (RpcWorldGeneration.Value != world || RpcTransportEpoch != transport);
        public readonly HashSet<UInt16> rpcPackets = new HashSet<UInt16>();
        public long lastRpcSendTick;

        /// <summary>How long an unacked RPC waits before it is sent again (Reality: the ping, 30 ms to 1 s).</summary>
        public const long RpcResendIntervalMs = 500;

        /// <summary>An RPC is sent at most this many times, then dropped.</summary>
        public const int MaxRpcSends = 60;

        public bool IsRpcDue(long nowTickMs) => rpcNumber < 0 || nowTickMs - lastRpcSendTick >= RpcResendIntervalMs;

        public const int AckHistoryBits = 7;
        public const byte ForceAckBit = 0x80;
        internal const byte AckHistoryMask = (1 << AckHistoryBits) - 1;

        /// <summary>Whether any packet this RPC went out in is among those the client acked.</summary>
        public bool IsRpcAckedBy(ICollection<UInt16> ackedSeqs)
        {
            lock (rpcPackets)
            {
                foreach (UInt16 seq in rpcPackets)
                {
                    if (ackedSeqs.Contains(seq))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void RecordRpcPacket(UInt16 sequence)
        {
            lock (rpcPackets)
            {
                rpcPackets.Add(sequence);
            }
        }

        /// <summary>
        /// The server sequences an ACK covers: the reported sequence and up to seven preceding ones.
        /// PSS bit 7 requests an ACK from the peer. It does not acknowledge a sequence.
        /// </summary>
        public static HashSet<UInt16> AckedSequences(UInt16 ackSeq, UInt16 ackBits)
        {
            HashSet<UInt16> acked = new HashSet<UInt16> { (UInt16)(ackSeq & 0xFFF) };
            for (int i = 0; i < AckHistoryBits; i++)
            {
                if ((ackBits & (1 << i)) != 0)
                {
                    acked.Add((UInt16)((ackSeq - i - 1) & 0xFFF));
                }
            }

            return acked;
        }

        public SequencedMessage(byte[] _content)
        {
            content = _content;
            sendCounter = 0;
            resendTime = 0;
            neededAck = 0;
            isTimed = false;
        }

        

        public void setNeededAck(UInt16 ackSSeq)
        {
            neededAck = ackSSeq;
        }

        public UInt16 getNeededAck()
        {
            return neededAck;
        }

        public void IncreaseSentCounter()
        {
            sendCounter++;
        }

        /// <summary>How long an unacked object message waits before it is sent again.</summary>
        public const long ObjectResendIntervalMs = 1500;

        /// <summary>An object message is sent at most this many times, then dropped.</summary>
        public const int MaxObjectSends = 10;

        private long lastSentTick;

        /// <summary>
        /// Resending unacked object messages is off unless MXO_OBJECT_RESEND=1. With it on, the real
        /// client stopped acting on server RPC lists partway through some sessions (vendor windows
        /// and chat stopped appearing), so it stays opt-in until that is understood.
        /// </summary>
        public static bool ObjectResendEnabled = Environment.GetEnvironmentVariable("MXO_OBJECT_RESEND") == "1";

        /// <summary>
        /// Object messages stay queued until the client acks the packet that carried them
        /// (DeletedAckedPackets). One is due when it hasn't been sent yet; with ObjectResendEnabled,
        /// also when it was sent at least ObjectResendIntervalMs ago and is still unacked. The old
        /// resend check compared a counter (0, 4000, 8000...) against Unix time and never passed.
        /// </summary>
        public bool IsObjectSendDue(long nowTickMs)
        {
            if (!isSent)
            {
                return true;
            }

            if (!ObjectResendEnabled)
            {
                return false;
            }

            return sendCounter < MaxObjectSends && nowTickMs - lastSentTick >= ObjectResendIntervalMs;
        }

        /// <summary>
        /// True when a client ack up to ackSeq covers this message. A message that has never been
        /// sent can't be acked: its neededAck is still 0, which every ack would otherwise match.
        /// </summary>
        public bool IsAckedBy(UInt16 ackSeq)
        {
            return sendCounter > 0 && neededAck <= ackSeq;
        }

        public void MarkObjectSent(long nowTickMs)
        {
            isSent = true;
            lastSentTick = nowTickMs;
            sendCounter++;
        }
        
        public void increaseResendTime()
        {
            resendTime = resendTime + 4000;
        }

        public int getResendTime()
        {
            return resendTime;
        }
    }
}
