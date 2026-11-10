using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hds
{
    public class SequencedMessage
    {
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
        public readonly HashSet<UInt16> rpcPackets = new HashSet<UInt16>();
        public long lastRpcSendTick;

        /// <summary>How long an unacked RPC waits before it is sent again (Reality: the ping, 30 ms to 1 s).</summary>
        public const long RpcResendIntervalMs = 500;

        /// <summary>An RPC is sent at most this many times, then dropped.</summary>
        public const int MaxRpcSends = 60;

        public bool IsRpcDue(long nowTickMs) => rpcNumber < 0 || nowTickMs - lastRpcSendTick >= RpcResendIntervalMs;

        /// <summary>Whether any packet this RPC went out in is among those the client acked.</summary>
        public bool IsRpcAckedBy(ICollection<UInt16> ackedSeqs)
        {
            foreach (UInt16 seq in rpcPackets)
            {
                if (ackedSeqs.Contains(seq))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The server sequences an ack covers: the acked one, and for each set ack bit i the one i + 1 before it. Our
        /// packets go out every other sequence, and the client's steady ack bits are 42 and 170 (bits 1, 3, 5 and 7: the
        /// sent ones 2, 4, 6 and 8 back); 40 or 34 are the gaps a lost packet leaves.
        /// </summary>
        public static HashSet<UInt16> AckedSequences(UInt16 ackSeq, UInt16 ackBits)
        {
            HashSet<UInt16> acked = new HashSet<UInt16> { (UInt16)(ackSeq & 0xFFF) };
            for (int i = 0; i < 8; i++)
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
            content = new byte[_content.Length];
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
