using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace hds
{
    public class SequencedMessage
    {
        // Created with the queue entry; delayed/timed entries remain included in first-send latency.
        private long scaleEnqueued=ScaleDiagnostics.Start;
        private int scaleFirstSent;
        public void RecordScaleFirstSend(bool objectMessage)
        {
            if(scaleEnqueued!=0 && System.Threading.Interlocked.Exchange(ref scaleFirstSent,1)==0)
                ScaleDiagnostics.FirstSend(scaleEnqueued,objectMessage);
        }
        internal ObjectMessageReceipt ObjectReceipt;
        internal SequencedMessage TimedViewWithRpc;
        internal bool ReliableObject;
        internal SequencedMessage ReliableObjectPrerequisite;
        internal bool ReliableObjectAcked;
        internal bool ReliableObjectGapObserved;
        internal int? ObjectWorldGeneration;
        internal long ObjectTransportEpoch;
        private int firstReliableObjectPacket = -1;
        private HashSet<UInt16> reliableObjectPackets;
        internal ushort MovementViewId;
        internal byte MovementFlags;
        internal byte MovementAction;
        internal byte MovementYaw;
        internal uint MovementPos0;
        internal uint MovementPos1;
        internal uint MovementPos2;
        private byte[] _content;
        public byte[] content
        {
            get
            {
                if (_content != null) return _content;
                if (MovementViewId != 0)
                {
                    _content = MaterializeMovementContent();
                }
                return _content;
            }
            set => _content = value;
        }
        internal int ObjectContentLength
        {
            get
            {
                if (_content != null) return _content.Length;
                if (MovementViewId == 0) return 0;
                byte flags = MovementFlags;
                return 4
                    + ((flags & SelfViewUpdate.FlagAction) != 0 ? 1 : 0)
                    + ((flags & SelfViewUpdate.FlagYaw) != 0 ? 1 : 0)
                    + ((flags & SelfViewUpdate.FlagPosition) != 0 ? 12 : 0);
            }
        }
        internal void WriteObjectContent(byte[] destination, ref int offset)
        {
            if (_content != null)
            {
                Buffer.BlockCopy(_content, 0, destination, offset, _content.Length);
                offset += _content.Length;
                return;
            }
            if (MovementViewId == 0) return;
            destination[offset++] = (byte)(MovementViewId & 0xFF);
            destination[offset++] = (byte)((MovementViewId >> 8) & 0xFF);
            destination[offset++] = SelfViewEntry.PartState;
            byte flags = MovementFlags;
            destination[offset++] = flags;
            if ((flags & SelfViewUpdate.FlagAction) != 0) destination[offset++] = MovementAction;
            if ((flags & SelfViewUpdate.FlagYaw) != 0) destination[offset++] = MovementYaw;
            if ((flags & SelfViewUpdate.FlagPosition) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, 4), MovementPos0);
                BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset + 4, 4), MovementPos1);
                BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset + 8, 4), MovementPos2);
                offset += 12;
            }
        }
        private byte[] MaterializeMovementContent()
        {
            byte[] buf = new byte[ObjectContentLength];
            int cursor = 0;
            WriteObjectContent(buf, ref cursor);
            return buf;
        }
        internal void InvalidateMovementContent() => _content = null;
        internal void ResetForMovement(ushort viewId, byte flags, byte action, byte yaw, uint pos0, uint pos1, uint pos2)
        {
            scaleEnqueued = ScaleDiagnostics.Start;
            scaleFirstSent = 0;
            ObjectReceipt = null;
            TimedViewWithRpc = null;
            ReliableObject = false;
            ReliableObjectPrerequisite = null;
            ReliableObjectAcked = false;
            ReliableObjectGapObserved = false;
            ObjectWorldGeneration = null;
            ObjectTransportEpoch = 0;
            firstReliableObjectPacket = -1;
            firstRpcPacket = -1;
            MovementViewId = viewId;
            MovementFlags = flags;
            MovementAction = action;
            MovementYaw = yaw;
            MovementPos0 = pos0;
            MovementPos1 = pos1;
            MovementPos2 = pos2;
            _content = null;
            sendCounter = 0;
            resendTime = 0;
            neededAck = 0;
            hasNeededAck = false;
            isTimed = false;
            isSent = false;
            lastSentTick = 0;
        }
        public UInt16 sendCounter;
        private int resendTime;
        private UInt16 neededAck;
        private bool hasNeededAck;
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
        internal bool WorldSetupRpc;
        // Only explicitly world-bound producer records use this lifetime; ordinary reliable RPCs are unchanged.
        internal int? RpcWorldGeneration;
        internal long RpcTransportEpoch;
        internal bool DiscardUnassignedWorldRpc(int world,long transport) => rpcNumber < 0 &&
            RpcWorldGeneration.HasValue && (RpcWorldGeneration.Value != world || RpcTransportEpoch != transport);
        // Inline single-send SSEQ avoids allocating a HashSet<ushort> for the 99.9% of RPCs
        // acknowledged on their first transmission; _rpcPackets is allocated only on retries or direct access.
        private int firstRpcPacket = -1;
        private HashSet<UInt16> _rpcPackets;
        public HashSet<UInt16> rpcPackets => EnsureRpcPackets(Volatile.Read(ref firstRpcPacket));
        public long lastRpcSendTick;

        /// <summary>How long an unacked RPC waits before it is sent again (Reality: the ping, 30 ms to 1 s).</summary>
        public const long RpcResendIntervalMs = 500;

        /// <summary>An RPC is sent at most this many times, then dropped.</summary>
        public const int MaxRpcSends = 60;

        public bool IsRpcDue(long nowTickMs) => rpcNumber < 0 || nowTickMs - lastRpcSendTick >= RpcResendIntervalMs;

        public const int AckHistoryBits = 7;
        public const byte ForceAckBit = 0x80;
        internal const byte AckHistoryMask = (1 << AckHistoryBits) - 1;

        /// <summary>
        /// Zero-allocation value representation of the 12-bit ACK sequence and 7-bit history window
        /// equivalent to <see cref="AckedSequences"/>.
        /// </summary>
        internal readonly struct AckWindow
        {
            private readonly UInt16 latest;
            private readonly byte bits;

            public AckWindow(UInt16 ackSeq, UInt16 ackBits)
            {
                latest = (UInt16)(ackSeq & 0xFFF);
                bits = (byte)(ackBits & AckHistoryMask);
            }

            public bool Contains(UInt16 sequence)
            {
                int diff = (latest - (sequence & 0xFFF)) & 0xFFF;
                if (diff == 0) return true;
                return diff <= AckHistoryBits && (bits & (1 << (diff - 1))) != 0;
            }
        }

        private HashSet<UInt16> EnsureRpcPackets(int seed)
        {
            HashSet<UInt16> packets = Volatile.Read(ref _rpcPackets);
            if (packets == null)
            {
                var created = new HashSet<UInt16>();
                if (seed >= 0) created.Add((UInt16)seed);
                packets = Interlocked.CompareExchange(ref _rpcPackets, created, null) ?? created;
            }
            if (seed >= 0)
            {
                lock (packets) packets.Add((UInt16)seed);
            }
            return packets;
        }

        /// <summary>Whether any packet this RPC went out in is among those the client acked.</summary>
        public bool IsRpcAckedBy(ICollection<UInt16> ackedSeqs)
        {
            int first = Volatile.Read(ref firstRpcPacket);
            if (first >= 0 && ackedSeqs.Contains((UInt16)first))
            {
                return true;
            }
            HashSet<UInt16> packets = Volatile.Read(ref _rpcPackets);
            if (packets == null) return false;
            lock (packets)
            {
                foreach (UInt16 seq in packets)
                {
                    if (ackedSeqs.Contains(seq))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool IsRpcAckedBy(in AckWindow acked)
        {
            int first = Volatile.Read(ref firstRpcPacket);
            if (first >= 0 && acked.Contains((UInt16)first))
            {
                return true;
            }
            HashSet<UInt16> packets = Volatile.Read(ref _rpcPackets);
            if (packets == null) return false;
            lock (packets)
            {
                foreach (UInt16 seq in packets)
                {
                    if (acked.Contains(seq))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void RecordRpcPacket(UInt16 sequence)
        {
            int seq = sequence & 0xFFF;
            int existing = Interlocked.CompareExchange(ref firstRpcPacket, seq, -1);
            if (existing == -1 || existing == seq)
            {
                HashSet<UInt16> existingSet = Volatile.Read(ref _rpcPackets);
                if (existingSet != null)
                {
                    lock (existingSet) existingSet.Add((UInt16)seq);
                }
                return;
            }

            HashSet<UInt16> packets = EnsureRpcPackets(existing);
            lock (packets)
            {
                packets.Add((UInt16)seq);
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
            neededAck = (UInt16)(ackSSeq & 0xFFF);
            hasNeededAck = true;
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

        /// <summary>
        /// Wait longer before the first retry of a spawn-critical reliable object message unless the client
        /// explicitly reports a gap past it, avoiding duplicate ViewCreates on slow-but-lossless cold logins.
        /// </summary>
        public const long FirstReliableObjectRetryMs = 1000;

        /// <summary>Steady resend cadence for spawn-critical reliable object messages (matches RPCs).</summary>
        public const long ReliableObjectResendIntervalMs = RpcResendIntervalMs;

        /// <summary>Spawn-critical reliable object messages match MaxRpcSends (30 s at 500 ms) so they survive cold-login loss bursts.</summary>
        public const int MaxReliableObjectSends = MaxRpcSends;

        private long lastSentTick;

        /// <summary>
        /// Resending unacked object messages is off unless MXO_OBJECT_RESEND=1. With it on, the real
        /// client stopped acting on server RPC lists partway through some sessions (vendor windows
        /// and chat stopped appearing), so it stays opt-in until that is understood.
        /// </summary>
        public static bool ObjectResendEnabled = Environment.GetEnvironmentVariable("MXO_OBJECT_RESEND") == "1";

        internal bool DiscardStaleReliableObject(int world, long transport) =>
            ReliableObject && ObjectWorldGeneration.HasValue &&
            (ObjectWorldGeneration.Value != world || ObjectTransportEpoch != transport);

        private HashSet<UInt16> EnsureReliableObjectPackets(int seed)
        {
            HashSet<UInt16> packets = Volatile.Read(ref reliableObjectPackets);
            if (packets == null)
            {
                var created = new HashSet<UInt16>();
                if (seed >= 0) created.Add((UInt16)seed);
                packets = Interlocked.CompareExchange(ref reliableObjectPackets, created, null) ?? created;
            }
            if (seed >= 0)
            {
                lock (packets) packets.Add((UInt16)seed);
            }
            return packets;
        }

        internal void RecordReliableObjectPacket(UInt16 sequence)
        {
            if (!ReliableObject) return;
            int seq = sequence & 0xFFF;
            int existing = Interlocked.CompareExchange(ref firstReliableObjectPacket, seq, -1);
            if (existing == -1 || existing == seq)
            {
                HashSet<UInt16> existingSet = Volatile.Read(ref reliableObjectPackets);
                if (existingSet != null)
                {
                    lock (existingSet) existingSet.Add((UInt16)seq);
                }
                return;
            }

            HashSet<UInt16> packets = EnsureReliableObjectPackets(existing);
            lock (packets)
            {
                packets.Add((UInt16)seq);
            }
        }

        internal bool IsReliableObjectAckedBy(ICollection<UInt16> ackedSeqs)
        {
            if (!ReliableObject) return false;
            int first = Volatile.Read(ref firstReliableObjectPacket);
            if (first >= 0 && ackedSeqs.Contains((UInt16)first)) return true;
            HashSet<UInt16> packets = Volatile.Read(ref reliableObjectPackets);
            if (packets == null) return false;
            lock (packets)
            {
                foreach (UInt16 seq in packets)
                {
                    if (ackedSeqs.Contains(seq))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal bool IsReliableObjectAckedBy(in AckWindow acked)
        {
            if (!ReliableObject) return false;
            int first = Volatile.Read(ref firstReliableObjectPacket);
            if (first >= 0 && acked.Contains((UInt16)first)) return true;
            HashSet<UInt16> packets = Volatile.Read(ref reliableObjectPackets);
            if (packets == null) return false;
            lock (packets)
            {
                foreach (UInt16 seq in packets)
                {
                    if (acked.Contains(seq))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal void ClearReliableObjectPackets()
        {
            Volatile.Write(ref firstReliableObjectPacket, -1);
            HashSet<UInt16> packets = Volatile.Read(ref reliableObjectPackets);
            if (packets == null) return;
            lock (packets)
            {
                packets.Clear();
            }
        }

        /// <summary>
        /// Object messages stay queued until the client acks the packet that carried them
        /// (DeletedAckedPackets). One is due when it hasn't been sent yet; spawn-critical reliable
        /// object messages resend until exact-ACKed (and after their prerequisite is ACKed); ordinary
        /// object messages resend only when ObjectResendEnabled is set.
        /// </summary>
        public bool IsObjectSendDue(long nowTickMs)
        {
            if (!isSent)
            {
                return true;
            }

            if (ReliableObject)
            {
                if (ReliableObjectAcked || sendCounter >= MaxReliableObjectSends)
                {
                    return false;
                }
                long interval = (sendCounter == 1 && !ReliableObjectGapObserved)
                    ? FirstReliableObjectRetryMs
                    : ReliableObjectResendIntervalMs;
                return nowTickMs - lastSentTick >= interval;
            }

            if (!ObjectResendEnabled)
            {
                return false;
            }

            return sendCounter < MaxObjectSends && nowTickMs - lastSentTick >= ObjectResendIntervalMs;
        }

        /// <summary>
        /// True when a client ack up to ackSeq covers this message across the 12-bit SSEQ wrap (0..4095).
        /// A message that has never been sent or has not yet been assigned its outbound SSEQ can't be acked:
        /// its neededAck is still 0, which would otherwise match any ack before getFinalData assigns its SSEQ.
        /// </summary>
        public bool IsAckedBy(UInt16 ackSeq)
        {
            return sendCounter > 0 && hasNeededAck && (((ackSeq - neededAck) & 0xFFF) < 0x800);
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
