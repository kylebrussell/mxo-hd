using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace hds
{
    // This Class holds all Queue Submessages
    public class MessageQueue
    {
        public ArrayList RPCMessagesQueue;
        public ArrayList ObjectMessagesQueue;
        public ArrayList rawMessages;
        public ArrayList worldPackets; // These are final worldPackets to be resend
        public int ackOnlyCount = 0;

        public MessageQueue()
        {
            RPCMessagesQueue = new ArrayList();
            ObjectMessagesQueue = new ArrayList();
            rawMessages = new ArrayList();
        }

        
        /*
         * Add a RPC Message with size byte automatically to the queue
         */
        public void addRpcMessage(byte[] messageBlock)
        {
            if(MissionOutput.Stage(this,messageBlock,false,false))return;
            QueueRpc(messageBlock,null,0);
        }

        // Current sole caller is captured ability registration. No validity callbacks or owner locks in the queue.
        internal void AddWorldBoundRpc(byte[] messageBlock,int world,long transport) => QueueRpc(messageBlock,world,transport);
        private void QueueRpc(byte[] messageBlock,int? world,long transport)
        {
            byte[] rpcSizeByte;
            if (messageBlock.Length > 127)
            {
                rpcSizeByte = NumericalUtils.uint16ToByteArray((UInt16)(messageBlock.Length + 0x8000), 0);
            }
            else
            {
                rpcSizeByte = NumericalUtils.uint16ToByteArrayShort((UInt16)messageBlock.Length);
            }
            DynamicArray content = new DynamicArray();
            content.append(rpcSizeByte);
            content.append(messageBlock);

            SequencedMessage message = new SequencedMessage(content.getBytes()) { RpcWorldGeneration=world, RpcTransportEpoch=transport };
            // Same lock as the flush and ack code: the flush thread enumerates this list while packet
            // handlers add to it, and an unlocked Add crashed the server ("Collection was modified").
            lock (RPCMessagesQueue.SyncRoot)
            {
                RPCMessagesQueue.Add(message);
            }
        }

        // Called only within the existing transport admission gate, BEFORE any new RPC counter is assigned.
        internal void PruneUnassignedWorldRpcs(int world,long transport)
        {
            lock(RPCMessagesQueue.SyncRoot)
                for(int i=RPCMessagesQueue.Count-1;i>=0;i--)
                    if(((SequencedMessage)RPCMessagesQueue[i]).DiscardUnassignedWorldRpc(world,transport)) RPCMessagesQueue.RemoveAt(i);
        }

        public void addObjectMessage(byte[]messageBlock,bool isTimed)
        {
            if(MissionOutput.Stage(this,messageBlock,true,isTimed))return;
            SequencedMessage message = new SequencedMessage(messageBlock);
            message.isTimed = isTimed;
            lock (ObjectMessagesQueue.SyncRoot)
            {
                ObjectMessagesQueue.Add(message);
            }
            
        }

        /// <summary>Drops the RPCs already sent: after a comm reset the client counts from 0 again.</summary>
        public void ForgetSentRpcs()
        {
            lock (RPCMessagesQueue.SyncRoot)
            {
                for (int i = RPCMessagesQueue.Count - 1; i >= 0; i--)
                {
                    if (((SequencedMessage)RPCMessagesQueue[i]).rpcNumber >= 0)
                    {
                        RPCMessagesQueue.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>The client discarded every old-world view at comm reset, including unacked creations.</summary>
        public void ForgetWorldObjects()
        {
            lock (ObjectMessagesQueue.SyncRoot) ObjectMessagesQueue.Clear();
        }

        public void addRawMessage(byte[] messageBlock)
        {
            rawMessages.Add(messageBlock);
        }

    }
}
