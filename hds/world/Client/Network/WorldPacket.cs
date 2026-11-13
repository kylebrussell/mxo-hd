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
        
        public DynamicArray content = new DynamicArray();
        public bool isFinal = false;
        public UInt16 bufferSize = 0;
        private UInt16 maxBuffer = 1300;
        public ClientData playerData;
        public bool timed = false;
        public bool isRPCReset = false;

        /// <summary>Minimum gap between two sim-time stamped (0x82) packets.</summary>
        public const float SimTimeIntervalSeconds = 3.0f;

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
            if (!IsEmpty && (RPCMessages.Count >= 255 || checkIfBufferIsFull(rpcMessage.content.Length + 2)))
            {
                return false;
            }

            this.RPCMessages.Add(rpcMessage);
            bufferSize = (UInt16)Math.Min(UInt16.MaxValue, bufferSize + rpcMessage.content.Length + 2);
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

        internal void PruneUnassignedWorldRpcs(int world,long transport) =>
            RPCMessages.RemoveAll(message=>message.DiscardUnassignedWorldRpc(world,transport));

        public byte[] getFinalData(ClientData playerData)
        {
            // TODO: Sim Time with Client (not real time) on every 4 Local SSEQ we send
            

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
                // New RPCs take the next counters; one resent keeps its own (Reality's MsgBlock sequenceId)
                UInt16 counter = playerData.getRPCCounter();
                foreach (SequencedMessage message in RPCMessages)
                {
                    if (message.rpcNumber < 0)
                    {
                        message.rpcNumber = counter++;
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

        /// <summary>RPCs in counter order, split where the counters stop following on.</summary>
        public static List<List<SequencedMessage>> RpcBlocks(IEnumerable<SequencedMessage> messages)
        {
            List<List<SequencedMessage>> blocks = new List<List<SequencedMessage>>();
            foreach (SequencedMessage message in messages.OrderBy(m => m.rpcNumber))
            {
                List<SequencedMessage> last = blocks.Count > 0 ? blocks[blocks.Count - 1] : null;
                if (last != null && (UInt16)(last[last.Count - 1].rpcNumber + 1) == (UInt16)message.rpcNumber && last.Count < 255)
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
                    content.append(message.content);
                }
                content.append(0x00);
                content.append(0x00);


            }
            
        }



        
    }
}
