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
        /// Object messages stay queued until the client acks the packet that carried them
        /// (DeletedAckedPackets). One is due when it hasn't been sent yet, or when it was sent at
        /// least ObjectResendIntervalMs ago and is still unacked. The old check compared a counter
        /// (0, 4000, 8000...) against Unix time, so a message was sent exactly once; a single lost
        /// self-spawn left the client at "Negotiating with Servers..." for good.
        /// </summary>
        public bool IsObjectSendDue(long nowTickMs)
        {
            if (!isSent)
            {
                return true;
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
