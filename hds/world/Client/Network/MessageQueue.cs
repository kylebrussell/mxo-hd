using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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
        // Per-viewer unsent movement coalescing, recycled movement message pool, and unsent-prefix cursor (guarded by ObjectMessagesQueue.SyncRoot).
        private readonly Dictionary<ushort, SequencedMessage> pendingMovementByView =
            new Dictionary<ushort, SequencedMessage>();
        private Stack<SequencedMessage> movementPool;
        private const int MaxPooledMovementMessages = 512;
        // Reusable scratch buffers for TakeRpcsToSend (guarded by RPCMessagesQueue.SyncRoot).
        private List<SequencedMessage> rpcSentScratch;
        private List<SequencedMessage> rpcFreshScratch;
        private static readonly List<SequencedMessage> EmptyRpcList = new List<SequencedMessage>(0);
        internal int FirstUnsentObjectIndex;
        internal int ReliableObjectCount;
        // Receipt and retry snapshots are published under the WorldClient transport fence.
        internal ClientDatagramReceipts ClientReceipts { get; } = new ClientDatagramReceipts();
        internal RpcResendBackoff ResendBackoff { get; } = new RpcResendBackoff();
        [ThreadStatic] private static MessageQueue worldSetupQueue;
        // The stock client can send spawn with its ACK still on the world command while it processes the setup burst.
        internal const long FirstWorldSetupRetryMs = 1000;
        private readonly object receiptSync=new object();
        private readonly System.Collections.Generic.HashSet<ObjectMessageReceipt> receipts=new System.Collections.Generic.HashSet<ObjectMessageReceipt>();
        private int activeReceiptCount;
        private int hasUnassignedWorldRpcs;
        private bool receiptSequenceKnown;
        private ushort receiptLastSequence;
        private long receiptLogicalSequence;
        internal int ObjectReceiptCount {get{lock(receiptSync)return receipts.Count;}}
        // Optional observer only: every SUCCESSFULLY dispatched packet advances the unambiguous 12-bit window.
        internal void RecordDispatchedReceipts(ushort sequence,System.Collections.Generic.IEnumerable<SequencedMessage> messages)
        {
            sequence=(ushort)(sequence&0xfff);
            lock(receiptSync)
            {
                if(receiptSequenceKnown)
                {
                    int delta=(sequence-receiptLastSequence)&0xfff;
                    if(delta==0)
                    {
                        if(receipts.Count>0)
                        {
                            receipts.RemoveWhere(r=>r.Finished);
                            Volatile.Write(ref activeReceiptCount, receipts.Count);
                        }
                        return;
                    }
                    if(delta>=0x800)
                    {
                        if(receipts.Count>0)
                        {
                            foreach(var r in receipts)r.Cancel();
                            receipts.Clear();
                            Volatile.Write(ref activeReceiptCount, 0);
                        }
                        receiptLogicalSequence=0;
                    }
                    else receiptLogicalSequence+=delta;
                }
                receiptSequenceKnown=true;receiptLastSequence=sequence;
                if(receipts.Count==0) return;
                foreach(var r in receipts)r.Advance(receiptLogicalSequence);
                foreach(var message in messages)message.ObjectReceipt?.Dispatched(sequence,receiptLogicalSequence);
                receipts.RemoveWhere(r=>r.Finished);
                Volatile.Write(ref activeReceiptCount, receipts.Count);
            }
        }
        internal void ObserveObjectReceipts(ushort ack,ushort bits)
        {
            if (Volatile.Read(ref activeReceiptCount) == 0) return;
            var exact=SequencedMessage.AckedSequences(ack,bits);
            lock(receiptSync)
            {
                if(receipts.Count==0){Volatile.Write(ref activeReceiptCount, 0);return;}
                foreach(var receipt in receipts)receipt.Observe(exact);
                receipts.RemoveWhere(r=>r.Finished);
                Volatile.Write(ref activeReceiptCount, receipts.Count);
            }
        }
        internal void CancelObjectReceipts()
        {
            lock(receiptSync)
            {
                foreach(var receipt in receipts)receipt.Cancel();
                receipts.Clear();
                Volatile.Write(ref activeReceiptCount, 0);
                receiptSequenceKnown=false;
                receiptLogicalSequence=0;
            }
        }

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

        internal void QueueWorldSetup(Action enqueue)
        {
            MessageQueue previous = worldSetupQueue;
            worldSetupQueue = this;
            try { enqueue(); }
            finally { worldSetupQueue = previous; }
        }

        // Explicit world-lifetime producers. No validity callbacks or owner locks in the queue.
        internal void AddWorldBoundRpc(byte[] messageBlock,int world,long transport) => QueueRpc(messageBlock,world,transport);
        // The view and its clock sample are one reliable response, including on retransmission.
        internal void AddTimedViewWithRpc(byte[] view, byte[] rpc, int world, long transport) =>
            QueueRpc(rpc, world, transport, new SequencedMessage(view));

        private void QueueRpc(byte[] messageBlock,int? world,long transport, SequencedMessage timedView = null)
        {
            int len = messageBlock.Length;
            byte[] framed;
            if (len > 127)
            {
                framed = new byte[2 + len];
                ushort header = (ushort)(len + 0x8000);
                framed[0] = (byte)(header >> 8);
                framed[1] = (byte)(header & 0xFF);
                Buffer.BlockCopy(messageBlock, 0, framed, 2, len);
            }
            else
            {
                framed = new byte[1 + len];
                framed[0] = (byte)len;
                Buffer.BlockCopy(messageBlock, 0, framed, 1, len);
            }

            SequencedMessage message = new SequencedMessage(framed)
            {
                RpcWorldGeneration = world, RpcTransportEpoch = transport, TimedViewWithRpc = timedView,
                WorldSetupRpc = worldSetupQueue == this
            };
            // Same lock as the flush and ack code: the flush thread enumerates this list while packet
            // handlers add to it, and an unlocked Add crashed the server ("Collection was modified").
            lock (RPCMessagesQueue.SyncRoot)
            {
                RPCMessagesQueue.Add(message);
                if (world.HasValue) Volatile.Write(ref hasUnassignedWorldRpcs, 1);
            }
        }

        /// <summary>
        /// The most RPC counters the client is sent ahead of its oldest unacked one. Retail kept about a hundred in flight
        /// at world entry (Unknown.log2: 61-163 out before the client acked past 60). New RPCs past this wait for acks,
        /// so a client that stops acking gets a bounded resend each interval rather than an ever-growing one.
        /// </summary>
        public const int MaxRpcsInFlight = 128;

        /// <summary>What a flush resends: the counters, the oldest one's send count, and how many.</summary>
        internal readonly struct RpcResend
        {
            internal readonly int First, Last, Count, OldestSend;
            internal RpcResend(int first, int last, int count, int oldestSend) { First = first; Last = last; Count = count; OldestSend = oldestSend; }
        }

        /// <summary>
        /// The reliable RPCs a flush sends, in counter order. Each sent-but-unacked RPC used to keep its own 500 ms resend
        /// timer, so RPCs first sent in different flushes went out again in different packets, each on its own phase: a
        /// packet carried 48-49 while 46-47, still unacked, went in another (the 52 53 54 46 47 50 51 48 49 pattern in
        /// the level-four native log). Now, once any of them is due, it and every unacked RPC before it go out again,
        /// oldest first; they then share a send time, so later resends stay one run starting at the oldest unacked
        /// counter. New RPCs follow, while the counter span from the oldest unacked one stays under MaxRpcsInFlight.
        /// </summary>
        internal System.Collections.Generic.List<SequencedMessage> TakeRpcsToSend(long nowTickMs, UInt16 nextCounter, out RpcResend resend,
            long? lastIngressTickMs = null)
        {
            var result = new List<SequencedMessage>();
            TakeRpcsToSendInto(nowTickMs, nextCounter, result, out resend, lastIngressTickMs);
            return result.Count == 0 ? EmptyRpcList : result;
        }

        internal void TakeRpcsToSendInto(long nowTickMs, UInt16 nextCounter, List<SequencedMessage> destination, out RpcResend resend,
            long? lastIngressTickMs = null)
        {
            destination.Clear();
            lock (RPCMessagesQueue.SyncRoot)
            {
                if (RPCMessagesQueue.Count == 0)
                {
                    resend = default;
                    return;
                }

                List<SequencedMessage> sent = rpcSentScratch;
                if (sent == null) rpcSentScratch = sent = new List<SequencedMessage>(8);
                List<SequencedMessage> fresh = rpcFreshScratch;
                if (fresh == null) rpcFreshScratch = fresh = new List<SequencedMessage>(8);
                sent.Clear();
                fresh.Clear();

                try
                {
                    SequencedMessage protectedRpc = null;
                    for (int i = 0; i < RPCMessagesQueue.Count; i++)
                    {
                        SequencedMessage message = (SequencedMessage)RPCMessagesQueue[i];
                        if (message.rpcNumber >= 0)
                        {
                            sent.Add(message);
                            if (message.WorldSetupRpc && message.sendCounter == 1 &&
                                nowTickMs - message.lastRpcSendTick < FirstWorldSetupRetryMs &&
                                (protectedRpc == null || Behind(message, nextCounter) > Behind(protectedRpc, nextCounter)))
                                protectedRpc = message;
                        }
                        else fresh.Add(message);
                    }

                    // Oldest first, across the 16-bit wrap: the oldest is the furthest behind the next counter.
                    if (sent.Count > 1)
                    {
                        // Insertion sort avoids per-call closure + Comparison delegate allocation for small in-flight sets.
                        for (int i = 1; i < sent.Count; i++)
                        {
                            SequencedMessage key = sent[i];
                            int keyBehind = Behind(key, nextCounter);
                            int j = i - 1;
                            while (j >= 0 && Behind(sent[j], nextCounter) < keyBehind)
                            {
                                sent[j + 1] = sent[j];
                                j--;
                            }
                            sent[j + 1] = key;
                        }
                    }

                    long interval = lastIngressTickMs.HasValue
                        ? ResendBackoff.Interval(nowTickMs, lastIngressTickMs.Value) : SequencedMessage.RpcResendIntervalMs;
                    int eligible = sent.Count;
                    if (protectedRpc != null && interval < FirstWorldSetupRetryMs)
                    {
                        // A later live RPC becoming due must not pull an earlier setup RPC into its first retry.
                        // If the original block mixed setup and other records, wait for the whole block.
                        eligible = sent.IndexOf(protectedRpc);
                        int block = protectedRpc.rpcBlockStart;
                        while (eligible > 0 && block >= 0 && sent[eligible - 1].rpcBlockStart == block) eligible--;
                    }
                    int due = -1;
                    for (int i = eligible - 1; i >= 0; i--)
                    {
                        if (nowTickMs - sent[i].lastRpcSendTick >= interval) { due = i; break; }
                    }
                    if (due >= 0 && sent[due].rpcBlockStart >= 0)
                    {
                        int blockStart = sent[due].rpcBlockStart;
                        while (due + 1 < sent.Count && sent[due + 1].rpcBlockStart == blockStart) due++;
                    }
                    if (due >= 0 && lastIngressTickMs.HasValue) ResendBackoff.RecordRetry();

                    int span = sent.Count == 0 ? 0 : Behind(sent[0], nextCounter);
                    int room = Math.Max(0, MaxRpcsInFlight - span);
                    int freshToTake = Math.Min(fresh.Count, room);
                    int total = (due + 1) + freshToTake;
                    resend = due < 0 ? default : new RpcResend(sent[0].rpcNumber, sent[due].rpcNumber, due + 1, sent[0].sendCounter);
                    if (total == 0)
                    {
                        return;
                    }

                    if (destination.Capacity < total) destination.Capacity = total;
                    for (int i = 0; i <= due; i++) destination.Add(sent[i]);
                    for (int i = 0; i < freshToTake; i++) destination.Add(fresh[i]);
                }
                finally
                {
                    sent.Clear();
                    fresh.Clear();
                }
            }
        }

        /// <summary>How many counters before nextCounter this RPC's is: 1 for the newest sent one.</summary>
        private static int Behind(SequencedMessage message, UInt16 nextCounter) => (UInt16)(nextCounter - (UInt16)message.rpcNumber);

        // Called only within the existing transport admission gate, BEFORE any new RPC counter is assigned.
        internal void PruneUnassignedWorldRpcs(int world,long transport)
        {
            if (Volatile.Read(ref hasUnassignedWorldRpcs) == 0) return;
            lock(RPCMessagesQueue.SyncRoot)
            {
                bool anyRemaining = false;
                for(int i=RPCMessagesQueue.Count-1;i>=0;i--)
                {
                    SequencedMessage msg = (SequencedMessage)RPCMessagesQueue[i];
                    if(msg.DiscardUnassignedWorldRpc(world,transport)) RPCMessagesQueue.RemoveAt(i);
                    else if(msg.RpcWorldGeneration.HasValue && msg.rpcNumber < 0) anyRemaining = true;
                }
                if(!anyRemaining) Volatile.Write(ref hasUnassignedWorldRpcs, 0);
            }
        }

        public void addObjectMessage(byte[]messageBlock,bool isTimed)
        {
            AddObject(messageBlock,isTimed,null);
        }
        internal void AddTrackedObject(byte[] messageBlock,ObjectMessageReceipt receipt)=>AddObject(messageBlock,false,receipt);
        internal SequencedMessage AddReliableObject(byte[] messageBlock, int world, long transport,
            SequencedMessage prerequisite = null, ObjectMessageReceipt receipt = null)
        {
            if (receipt != null) lock (receiptSync) { receipts.RemoveWhere(r => r.Finished); receipts.Add(receipt); Volatile.Write(ref activeReceiptCount, receipts.Count); }
            SequencedMessage message = new SequencedMessage(messageBlock)
            {
                ObjectReceipt = receipt,
                ReliableObject = true,
                ReliableObjectPrerequisite = prerequisite,
                ObjectWorldGeneration = world,
                ObjectTransportEpoch = transport
            };
            lock (ObjectMessagesQueue.SyncRoot)
            {
                ObjectMessagesQueue.Add(message);
                ReliableObjectCount++;
            }
            return message;
        }
        internal void PruneStaleReliableObjects(int world, long transport)
        {
            if (Volatile.Read(ref ReliableObjectCount) == 0) return;
            lock (ObjectMessagesQueue.SyncRoot)
            {
                if (ReliableObjectCount == 0) return;
                int write = 0;
                for (int i = 0; i < ObjectMessagesQueue.Count; i++)
                {
                    SequencedMessage message = (SequencedMessage)ObjectMessagesQueue[i];
                    if (message.DiscardStaleReliableObject(world, transport))
                    {
                        ReliableObjectCount--;
                        continue;
                    }
                    if (write != i) ObjectMessagesQueue[write] = message;
                    write++;
                }
                if (write < ObjectMessagesQueue.Count)
                {
                    ObjectMessagesQueue.RemoveRange(write, ObjectMessagesQueue.Count - write);
                    FirstUnsentObjectIndex = 0;
                }
            }
        }
        private void AddObject(byte[] messageBlock,bool isTimed,ObjectMessageReceipt receipt)
        {
            if(MissionOutput.Stage(this,messageBlock,true,isTimed,receipt))return;
            if(receipt!=null)lock(receiptSync){receipts.RemoveWhere(r=>r.Finished);receipts.Add(receipt);Volatile.Write(ref activeReceiptCount, receipts.Count);}
            SequencedMessage message = new SequencedMessage(messageBlock) {ObjectReceipt=receipt};
            message.isTimed = isTimed;
            lock (ObjectMessagesQueue.SyncRoot)
            {
                ObjectMessagesQueue.Add(message);
            }
            
        }

        /// <summary>Drops prior RPCs when the client resets its counters. LA starts with its own handshake.</summary>
        public void ForgetSentRpcs() => ForgetSentRpcs(false);

        internal void ForgetSentRpcs(bool enteringLoadingArea)
        {
            lock (RPCMessagesQueue.SyncRoot)
            {
                for (int i = RPCMessagesQueue.Count - 1; i >= 0; i--)
                {
                    if (enteringLoadingArea || ((SequencedMessage)RPCMessagesQueue[i]).rpcNumber >= 0)
                    {
                        RPCMessagesQueue.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>The client discarded every old-world view at comm reset, including unacked creations.</summary>
        public void ForgetWorldObjects()
        {
            lock (ObjectMessagesQueue.SyncRoot)
            {
                ObjectMessagesQueue.Clear();
                ClearPendingMovementsLocked();
            }
            CancelObjectReceipts();
        }

        /// <summary>
        /// Queues a player's forwarded movement/pose update for <paramref name="viewId"/>, coalescing in place
        /// with any unsent movement entry already waiting for the same viewer and view so each viewer gets at most
        /// one combined latest movement update per entity per flush without dropping Action or Yaw transitions.
        /// Stores movement fields inline on <see cref="SequencedMessage"/> and recycles retired shells to avoid
        /// per-viewer byte[] and object allocations across 200-player movement fan-out.
        /// </summary>
        internal void AddOrCoalesceMovement(ushort viewId, SelfViewUpdate state, byte[] forwardedState)
        {
            if (state == null || forwardedState == null || forwardedState.Length < 2) return;
            byte newFlags = (byte)(state.Flags & ~SelfViewUpdate.Flag20);
            if (newFlags == 0) return;

            uint pos0 = 0, pos1 = 0, pos2 = 0;
            bool hasPos = state.HasPosition;
            if (hasPos)
            {
                int posOff = forwardedState.Length - 12;
                ReadOnlySpan<byte> posSpan = forwardedState.AsSpan(posOff, 12);
                pos0 = BinaryPrimitives.ReadUInt32LittleEndian(posSpan.Slice(0, 4));
                pos1 = BinaryPrimitives.ReadUInt32LittleEndian(posSpan.Slice(4, 4));
                pos2 = BinaryPrimitives.ReadUInt32LittleEndian(posSpan.Slice(8, 4));
            }

            AddOrCoalesceMovementCore(viewId, newFlags, state.Action, state.Yaw, hasPos, pos0, pos1, pos2);
        }

        /// <summary>Zero-allocation overload reading float coordinate bits directly from <see cref="SelfViewUpdate"/>.</summary>
        internal void AddOrCoalesceMovement(ushort viewId, SelfViewUpdate state)
        {
            if (state == null) return;
            byte newFlags = (byte)(state.Flags & ~SelfViewUpdate.Flag20);
            if (newFlags == 0) return;

            uint pos0 = 0, pos1 = 0, pos2 = 0;
            bool hasPos = state.HasPosition;
            if (hasPos)
            {
                pos0 = BitConverter.SingleToUInt32Bits(state.X);
                pos1 = BitConverter.SingleToUInt32Bits(state.Y);
                pos2 = BitConverter.SingleToUInt32Bits(state.Z);
            }

            AddOrCoalesceMovementCore(viewId, newFlags, state.Action, state.Yaw, hasPos, pos0, pos1, pos2);
        }

        private void AddOrCoalesceMovementCore(ushort viewId, byte newFlags, byte? action, byte? yaw, bool hasPos, uint pos0, uint pos1, uint pos2)
        {
            lock (ObjectMessagesQueue.SyncRoot)
            {
                if (ObjectMessagesQueue.Count == 0 && pendingMovementByView.Count > 0)
                {
                    pendingMovementByView.Clear();
                    FirstUnsentObjectIndex = 0;
                }

                if (pendingMovementByView.TryGetValue(viewId, out SequencedMessage existing) && !existing.isSent)
                {
                    existing.MovementFlags = (byte)(existing.MovementFlags | newFlags);
                    if (action.HasValue) existing.MovementAction = action.Value;
                    if (yaw.HasValue) existing.MovementYaw = yaw.Value;
                    if (hasPos)
                    {
                        existing.MovementPos0 = pos0;
                        existing.MovementPos1 = pos1;
                        existing.MovementPos2 = pos2;
                    }
                    existing.InvalidateMovementContent();
                    return;
                }

                SequencedMessage message;
                if (movementPool != null && movementPool.Count > 0)
                {
                    message = movementPool.Pop();
                    message.ResetForMovement(viewId, newFlags, action ?? 0, yaw ?? 0, pos0, pos1, pos2);
                }
                else
                {
                    message = new SequencedMessage(null)
                    {
                        MovementViewId = viewId,
                        MovementFlags = newFlags,
                        MovementAction = action ?? 0,
                        MovementYaw = yaw ?? 0,
                        MovementPos0 = pos0,
                        MovementPos1 = pos1,
                        MovementPos2 = pos2
                    };
                }
                ObjectMessagesQueue.Add(message);
                pendingMovementByView[viewId] = message;
            }
        }

        internal static byte[] BuildViewUpdateFast(ushort viewId, byte[] data)
        {
            byte[] result = new byte[2 + data.Length];
            result[0] = (byte)(viewId & 0xFF);
            result[1] = (byte)((viewId >> 8) & 0xFF);
            Buffer.BlockCopy(data, 0, result, 2, data.Length);
            return result;
        }

        /// <summary>Called under ObjectMessagesQueue.SyncRoot when an ACKed/expired movement message is removed.</summary>
        internal void RecycleMovementMessageLocked(SequencedMessage message)
        {
            if (message.MovementViewId == 0 || message.ReliableObject || message.ObjectReceipt != null)
            {
                return;
            }
            if (movementPool == null) movementPool = new Stack<SequencedMessage>(64);
            if (movementPool.Count < MaxPooledMovementMessages)
            {
                message.InvalidateMovementContent();
                movementPool.Push(message);
            }
        }

        /// <summary>Called under ObjectMessagesQueue.SyncRoot when an object message is marked sent.</summary>
        internal void OnObjectMessageSentLocked(SequencedMessage message)
        {
            if (message.MovementViewId != 0 &&
                pendingMovementByView.TryGetValue(message.MovementViewId, out SequencedMessage current) &&
                ReferenceEquals(current, message))
            {
                pendingMovementByView.Remove(message.MovementViewId);
            }
        }

        internal void ClearPendingMovement(ushort viewId)
        {
            lock (ObjectMessagesQueue.SyncRoot)
            {
                pendingMovementByView.Remove(viewId);
            }
        }

        internal void ClearPendingMovementsLocked()
        {
            pendingMovementByView.Clear();
            FirstUnsentObjectIndex = 0;
            ReliableObjectCount = 0;
        }

        public void addRawMessage(byte[] messageBlock)
        {
            rawMessages.Add(messageBlock);
        }

    }
}
