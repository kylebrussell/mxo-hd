using System;
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
            if (!IsEmpty && (ObjectMessages.Count >= 255 || checkIfBufferIsFull(objectMessage.content.Length + 2)))
            {
                return false;
            }

            this.ObjectMessages.Add(objectMessage);
            bufferSize = (UInt16)Math.Min(UInt16.MaxValue, bufferSize + objectMessage.content.Length + 2);
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
            RPCMessages.RemoveAll(message =>
            {
                if (!message.DiscardUnassignedWorldRpc(world, transport)) return false;
                if (IsModifierRpc(message)) modifierRpcCount--;
                if (message.TimedViewWithRpc is { } view) ObjectMessages.Remove(view);
                return true;
            });
        }

        public byte[] getFinalData(ClientData playerData)
        {
            // TODO: Sim Time with Client (not real time) on every 4 Local SSEQ we send
            

            content = new DynamicArray(bufferSize + 32);
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

                if (playerData.waitForRPCShutDown==true)
                {
                    content.append((byte)0xc2);
                }
                else
                {
                    content.append((byte)0x82);
                }

                content.append(TimeUtils.getCurrentSimTime());
            }
            else
            {

                if (playerData.waitForRPCShutDown == true)
                {
                    content.append((byte)0x42);
                }
                else
                {
                    content.append((byte)0x02);
                }

            }

            // Merge all Message together and generate the Final Packet Header
            generateObjectMessageData();
            generateRpcMessageData();
            return content.getBytes();
            
        }

        public void generateRpcMessageData()
        {
            // Example : 04 01 00 15 01 + submessages
            // Strcut: uint8 04 header + uint8 listcount + uint16 currentRPCCounter + submessageCounter (list1)
            // If you have 2 lists , the data struct is : uint8 submessagecount + submessages

            // TODO!!! FINALIZE!!
            // Here goes code from RPCPacket (As it should be generated on the fly)
            if (RPCMessages.Count > 0)
            {
                // New RPCs take the next counters and share one immutable block start per packet; resent RPCs keep
                // their original counter and block start (stock client.dll CLTMessageBargeGuar skips any overlapping
                // block whose start < m_nextExpectedSeq, so resends must never merge into a wider block).
                UInt16 counter = playerData.getRPCCounter();
                int freshBlockStart = -1;
                foreach (SequencedMessage message in RPCMessages)
                {
                    if (message.rpcNumber < 0)
                    {
                        if (freshBlockStart < 0) freshBlockStart = counter;
                        message.rpcNumber = counter++;
                        message.rpcBlockStart = freshBlockStart;
                        Output.WriteBonusDebugLog(playerData.getCharID(), message.content);
                    }
                }

                playerData.setRPCCounter(counter);

                // 04 <blocks>, then each block's first counter, its count and its RPCs: a resent RPC and new ones can
                // share a packet, as Reality's OrderedPacket did
                List<List<SequencedMessage>> blocks = RpcBlocks(RPCMessages);
                content.append((byte)ProtocolHeaders.RPC_PROTOCOL);
                content.append((byte)blocks.Count);
                UInt16 sseq = (UInt16)(neededAckSSeq & 0xFFF);
                long now = Environment.TickCount64;
                foreach (List<SequencedMessage> block in blocks)
                {
                    content.append(NumericalUtils.uint16ToByteArray((UInt16)block[0].rpcNumber, 0));
                    content.append(NumericalUtils.uint16ToByteArrayShort((UInt16)block.Count));
                    foreach (SequencedMessage message in block)
                    {
                        content.append(message.content);
                        message.RecordRpcPacket(sseq);
                        message.lastRpcSendTick = now;
                    }
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
            // ToDo: add the SSEQ to the MessageQueue Objects
            if (ObjectMessages.Count > 0)
            {
                content.append((byte)ProtocolHeaders.OBJECT_VIEW_PROTOCOL);
                // Add ViewId 0 to tell our Client that Object Protocol is done now
                foreach (SequencedMessage message in ObjectMessages)
                {
                    message.increaseResendTime();
                    message.setNeededAck(this.neededAckSSeq);
                    message.RecordReliableObjectPacket(this.neededAckSSeq);
                    content.append(message.content);
                }
                content.append(0x00);
                content.append(0x00);


            }
            
        }



        
    }
}
