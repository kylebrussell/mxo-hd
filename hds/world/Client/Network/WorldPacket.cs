using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hds
{
    public class WorldPacket
    {
        // Holds the ArrayList for the Packets and proove if the size is reached
        public List<SequencedMessage> RPCMessages = new List<SequencedMessage>();
        public List<SequencedMessage> ObjectMessages = new List<SequencedMessage>();
        public UInt16 neededAckSSeq;
        
        public DynamicArray content;
        public bool isFinal = false;
        public UInt16 bufferSize = 0;
        private UInt16 maxBuffer = 1300;
        public ClientData playerData;
        public bool timed = false;
        public bool isRPCReset = false;

        /// <summary>Minimum gap between two sim-time stamped (0x82) packets.</summary>
        public const float SimTimeIntervalSeconds = 3.0f;

        // Keep modifier bursts small on both first sends and retries. In native QA a
        // twelve-record SSR Gum datagram stalled until retries reached the client.
        private const int MaxModifierRpcs = 6;
        private int modifierRpcCount;

        // QueueRpc has already prefixed the record with its one- or two-byte length.
        private static bool IsModifierRpc(SequencedMessage message)
        {
            byte[] record = message.content;
            if (record.Length < 3) return false;
            int opcode = (record[0] & 0x80) == 0 ? 1 : 2;
            return record.Length > opcode + 1 && record[opcode] == 0x80 && record[opcode + 1] == 0xbc;
        }

        public WorldPacket(ClientData _playerData)
        {
            this.playerData = _playerData;
        }

        internal void Reset(ClientData _playerData)
        {
            RPCMessages.Clear();
            ObjectMessages.Clear();
            neededAckSSeq = 0;
            content = null;
            isFinal = false;
            bufferSize = 0;
            playerData = _playerData;
            timed = false;
            isRPCReset = false;
            modifierRpcCount = 0;
        }

        public void setFinalContent(byte[] content)
        {

        }

        // A local named bufferSize used to shadow the field here, so the field never grew and a packet took any number of
        // messages: a teleport's burst of 66 RPCs went out as one 6,400-byte packet, and the client, which never took
        // it, stopped processing RPCs for the rest of the session. A message's length prefix is up to 2 bytes; the packet
        // carries its RPC count in one. An empty packet takes any message, however large.
        public bool addRPCContent(SequencedMessage rpcMessage)
        {
            bool modifier = IsModifierRpc(rpcMessage);
            if (modifier && modifierRpcCount == MaxModifierRpcs) return false;
            if (!IsEmpty && (RPCMessages.Count >= 255 || checkIfBufferIsFull(rpcMessage.content.Length + 2)))
            {
                return false;
            }

            if (rpcMessage.TimedViewWithRpc is { } view)
            {
                // Leave earlier RPC counters in their own packet; the paired view starts this datagram.
                if (!IsEmpty) return false;
                addObjectContent(view);
                timed = true;
            }

            this.RPCMessages.Add(rpcMessage);
            if (modifier) modifierRpcCount++;
            bufferSize = (UInt16)Math.Min(UInt16.MaxValue, bufferSize + rpcMessage.content.Length + 2);
            return true;
        }

        /// <summary>
        /// Adds an already-numbered protocol-0x04 RPC block atomically so resends never split an immutable block across
        /// datagrams (stock client.dll CLTMessageBargeGuar drops overlapping/repacked blocks whose start &lt; expected).
        /// </summary>
        internal bool addRPCBlock(IList<SequencedMessage> block)
        {
            if (block == null || block.Count == 0) return true;
            int totalBytes = IsEmpty ? 0 : 3;
            int blockModifiers = 0;
            bool hasTimedView = false;
            for (int i = 0; i < block.Count; i++)
            {
                totalBytes += block[i].content.Length + 2;
                if (IsModifierRpc(block[i])) blockModifiers++;
                if (block[i].TimedViewWithRpc != null) hasTimedView = true;
            }
            // Fresh blocks are assigned only after packet packing, so each immutable
            // block fits alone. Check the total before combining it with other blocks.
            if (modifierRpcCount + blockModifiers > MaxModifierRpcs) return false;
            if (!IsEmpty && (hasTimedView || RPCMessages.Count + block.Count > 255 || checkIfBufferIsFull(totalBytes)))
            {
                return false;
            }
            for (int i = 0; i < block.Count; i++)
            {
                SequencedMessage message = block[i];
                if (message.TimedViewWithRpc is { } view)
                {
                    addObjectContent(view);
                    timed = true;
                }
                this.RPCMessages.Add(message);
            }
            modifierRpcCount += blockModifiers;
            bufferSize = (UInt16)Math.Min(UInt16.MaxValue, bufferSize + totalBytes);
            return true;
        }

        public bool addObjectContent(SequencedMessage objectMessage)
        {
            int contentLen = objectMessage.ObjectContentLength;
            if (!IsEmpty && (ObjectMessages.Count >= 255 || checkIfBufferIsFull(contentLen + 2)))
            {
                return false;
            }

            this.ObjectMessages.Add(objectMessage);
            bufferSize = (UInt16)Math.Min(UInt16.MaxValue, bufferSize + contentLen + 2);
            return true;
        }

        public bool IsEmpty => RPCMessages.Count == 0 && ObjectMessages.Count == 0;

        public bool checkIfBufferIsFull(int sizeOfPossibleAddedMessage)
        {
            if (bufferSize + sizeOfPossibleAddedMessage > maxBuffer)
            {
                return true;
            }
            return false;
        }

        internal void PruneUnassignedWorldRpcs(int world,long transport)
        {
            int count = RPCMessages.Count;
            if (count == 0) return;

            int write = 0;
            for (int i = 0; i < count; i++)
            {
                SequencedMessage message = RPCMessages[i];
                if (message.DiscardUnassignedWorldRpc(world, transport))
                {
                    if (IsModifierRpc(message)) modifierRpcCount--;
                    if (message.TimedViewWithRpc is { } view) ObjectMessages.Remove(view);
                    continue;
                }
                if (write != i) RPCMessages[write] = message;
                write++;
            }
            if (write < count)
            {
                RPCMessages.RemoveRange(write, count - write);
            }
        }

        internal int EstimateMaxPlaintextSize()
        {
            int size = 5;
            int objCount = ObjectMessages.Count;
            if (objCount > 0)
            {
                size += 3;
                for (int i = 0; i < objCount; i++)
                {
                    size += ObjectMessages[i].ObjectContentLength;
                }
            }
            int rpcCount = RPCMessages.Count;
            if (rpcCount > 0)
            {
                size += 2;
                for (int i = 0; i < rpcCount; i++)
                {
                    size += 3 + RPCMessages[i].content.Length;
                }
            }
            return size;
        }

        /// <summary>
        /// Serializes the finalized MPM plaintext payload directly into <paramref name="destination"/> starting at
        /// <paramref name="startOffset"/> (e.g. offset 14 of WorldEncryption's rented work buffer), avoiding
        /// intermediate DynamicArray and byte[] allocations on every outbound datagram.
        /// </summary>
        internal int WriteFinalData(ClientData playerData, byte[] destination, int startOffset)
        {
            int cursor = startOffset;
            playerData.IncrementSseq();
            this.neededAckSSeq = playerData.getSseq();

            // Stamp the sim time on the first packet and then at most every SimTimeIntervalSeconds.
            // This used to subtract raw float bit patterns, so when it fired was arbitrary.
            float simNow = TimeUtils.GetSimTimeSeconds();
            if (playerData.lastSimTimeUpdate == 0 || simNow - playerData.lastSimTimeSeconds >= SimTimeIntervalSeconds)
            {
                timed = true;
                playerData.lastSimTimeUpdate = 1;
                playerData.lastSimTimeSeconds = simNow;
            }

            playerData.getRPCShutDown();
            if (timed)
            {
                destination[cursor++] = playerData.waitForRPCShutDown ? (byte)0xc2 : (byte)0x82;
                BinaryPrimitives.WriteSingleLittleEndian(destination.AsSpan(cursor, 4), simNow);
                cursor += 4;
            }
            else
            {
                destination[cursor++] = playerData.waitForRPCShutDown ? (byte)0x42 : (byte)0x02;
            }

            WriteObjectMessageData(destination, ref cursor);
            WriteRpcMessageData(playerData, destination, ref cursor);
            return cursor - startOffset;
        }

        public byte[] getFinalData(ClientData playerData)
        {
            int maxLen = EstimateMaxPlaintextSize();
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxLen);
            try
            {
                int len = WriteFinalData(playerData, rented, 0);
                byte[] result = new byte[len];
                Buffer.BlockCopy(rented, 0, result, 0, len);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private void WriteObjectMessageData(byte[] destination, ref int cursor)
        {
            int count = ObjectMessages.Count;
            if (count == 0) return;

            destination[cursor++] = (byte)ProtocolHeaders.OBJECT_VIEW_PROTOCOL;
            for (int i = 0; i < count; i++)
            {
                SequencedMessage message = ObjectMessages[i];
                message.increaseResendTime();
                message.setNeededAck(this.neededAckSSeq);
                message.RecordReliableObjectPacket(this.neededAckSSeq);
                message.WriteObjectContent(destination, ref cursor);
            }
            // Add ViewId 0 to tell our Client that Object Protocol is done now
            destination[cursor++] = 0x00;
            destination[cursor++] = 0x00;
        }

        private void WriteRpcMessageData(ClientData data, byte[] destination, ref int cursor)
        {
            int count = RPCMessages.Count;
            if (count == 0) return;

            // New RPCs take the next counters and share one immutable block start per packet; resent RPCs keep
            // their original counter and block start (stock client.dll CLTMessageBargeGuar skips any overlapping
            // block whose start < m_nextExpectedSeq, so resends must never merge into a wider block).
            UInt16 counter = data.getRPCCounter();
            int freshBlockStart = -1;
            for (int i = 0; i < count; i++)
            {
                SequencedMessage message = RPCMessages[i];
                if (message.rpcNumber < 0)
                {
                    if (freshBlockStart < 0) freshBlockStart = counter;
                    message.rpcNumber = counter++;
                    message.rpcBlockStart = freshBlockStart;
                    Output.WriteBonusDebugLog(data.getCharID(), message.content);
                }
            }

            data.setRPCCounter(counter);

            UInt16 sseq = (UInt16)(neededAckSSeq & 0xFFF);
            long now = Environment.TickCount64;

            // Fast path: 99%+ of outbound RPC packets are a single contiguous block in order (<= 255 RPCs).
            bool singleBlock = count <= 255;
            if (singleBlock)
            {
                for (int i = 1; i < count; i++)
                {
                    SequencedMessage prev = RPCMessages[i - 1];
                    SequencedMessage curr = RPCMessages[i];
                    bool sameBlock = (prev.rpcBlockStart < 0 && curr.rpcBlockStart < 0) ||
                                     prev.rpcBlockStart == curr.rpcBlockStart;
                    if (!sameBlock || (UInt16)(prev.rpcNumber + 1) != (UInt16)curr.rpcNumber)
                    {
                        singleBlock = false;
                        break;
                    }
                }
            }

            if (singleBlock)
            {
                destination[cursor++] = (byte)ProtocolHeaders.RPC_PROTOCOL;
                destination[cursor++] = 1;
                UInt16 startNum = (UInt16)RPCMessages[0].rpcNumber;
                destination[cursor++] = (byte)(startNum >> 8);
                destination[cursor++] = (byte)(startNum & 0xFF);
                destination[cursor++] = (byte)count;
                for (int i = 0; i < count; i++)
                {
                    SequencedMessage message = RPCMessages[i];
                    byte[] bytes = message.content;
                    Buffer.BlockCopy(bytes, 0, destination, cursor, bytes.Length);
                    cursor += bytes.Length;
                    message.RecordRpcPacket(sseq);
                    message.lastRpcSendTick = now;
                }
                return;
            }

            // 04 <blocks>, then each block's first counter, its count and its RPCs: a resent RPC and new ones can
            // share a packet, as Reality's OrderedPacket did
            List<List<SequencedMessage>> blocks = RpcBlocks(RPCMessages);
            destination[cursor++] = (byte)ProtocolHeaders.RPC_PROTOCOL;
            destination[cursor++] = (byte)blocks.Count;
            for (int b = 0; b < blocks.Count; b++)
            {
                List<SequencedMessage> block = blocks[b];
                UInt16 startNum = (UInt16)block[0].rpcNumber;
                destination[cursor++] = (byte)(startNum >> 8);
                destination[cursor++] = (byte)(startNum & 0xFF);
                destination[cursor++] = (byte)block.Count;
                for (int i = 0; i < block.Count; i++)
                {
                    SequencedMessage message = block[i];
                    byte[] bytes = message.content;
                    Buffer.BlockCopy(bytes, 0, destination, cursor, bytes.Length);
                    cursor += bytes.Length;
                    message.RecordRpcPacket(sseq);
                    message.lastRpcSendTick = now;
                }
            }
        }

        public void generateRpcMessageData()
        {
            if (RPCMessages.Count > 0)
            {
                int maxLen = EstimateMaxPlaintextSize();
                byte[] rented = ArrayPool<byte>.Shared.Rent(maxLen);
                try
                {
                    int cursor = 0;
                    WriteRpcMessageData(playerData, rented, ref cursor);
                    byte[] slice = new byte[cursor];
                    Buffer.BlockCopy(rented, 0, slice, 0, cursor);
                    content.append(slice);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// RPCs in counter order (handling 16-bit wrap), split where counters stop following on or where two RPCs
        /// originated in different immutable protocol-0x04 blocks.
        /// </summary>
        public static List<List<SequencedMessage>> RpcBlocks(IEnumerable<SequencedMessage> messages)
        {
            List<SequencedMessage> list = messages.ToList();
            bool wraps = list.Any(m => m.rpcNumber >= 0xC000) && list.Any(m => m.rpcNumber >= 0 && m.rpcNumber < 0x4000);
            IEnumerable<SequencedMessage> ordered = wraps
                ? list.OrderBy(m => (m.rpcNumber + 0x8000) & 0xFFFF)
                : list.OrderBy(m => m.rpcNumber);

            List<List<SequencedMessage>> blocks = new List<List<SequencedMessage>>();
            foreach (SequencedMessage message in ordered)
            {
                List<SequencedMessage> last = blocks.Count > 0 ? blocks[blocks.Count - 1] : null;
                bool sameOriginalBlock = last != null &&
                    ((last[last.Count - 1].rpcBlockStart < 0 && message.rpcBlockStart < 0) ||
                     last[last.Count - 1].rpcBlockStart == message.rpcBlockStart);
                if (sameOriginalBlock && (UInt16)(last[last.Count - 1].rpcNumber + 1) == (UInt16)message.rpcNumber && last.Count < 255)
                {
                    last.Add(message);
                }
                else
                {
                    blocks.Add(new List<SequencedMessage> { message });
                }
            }

            return blocks;
        }

        // Called only after sendPacket actually succeeds, using this final encoded packet's SSEQ.
        internal void RecordDispatchedObjectReceipts(MessageQueue queue)
        {queue.RecordDispatchedReceipts(neededAckSSeq,ObjectMessages);}

        public void generateObjectMessageData()
        {
            if (ObjectMessages.Count > 0)
            {
                int maxLen = EstimateMaxPlaintextSize();
                byte[] rented = ArrayPool<byte>.Shared.Rent(maxLen);
                try
                {
                    int cursor = 0;
                    WriteObjectMessageData(rented, ref cursor);
                    byte[] slice = new byte[cursor];
                    Buffer.BlockCopy(rented, 0, slice, 0, cursor);
                    content.append(slice);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }
    }
}
