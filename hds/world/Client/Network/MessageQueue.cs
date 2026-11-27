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
        // Per-viewer unsent movement coalescing and unsent-prefix cursor (guarded by ObjectMessagesQueue.SyncRoot).
        private readonly System.Collections.Generic.Dictionary<ushort, SequencedMessage> pendingMovementByView =
            new System.Collections.Generic.Dictionary<ushort, SequencedMessage>();
        internal int FirstUnsentObjectIndex;
        // Receipt and retry snapshots are published under the WorldClient transport fence.
        internal ClientDatagramReceipts ClientReceipts { get; } = new ClientDatagramReceipts();
        internal RpcResendBackoff ResendBackoff { get; } = new RpcResendBackoff();
        [ThreadStatic] private static MessageQueue worldSetupQueue;
        // The stock client can send spawn with its ACK still on the world command while it processes the setup burst.
        internal const long FirstWorldSetupRetryMs = 1000;
        private readonly object receiptSync=new object();
        private readonly System.Collections.Generic.HashSet<ObjectMessageReceipt> receipts=new System.Collections.Generic.HashSet<ObjectMessageReceipt>();
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
                    if(delta==0){receipts.RemoveWhere(r=>r.Finished);return;}
                    if(delta>=0x800){foreach(var r in receipts)r.Cancel();receipts.Clear();receiptLogicalSequence=0;}
                    else receiptLogicalSequence+=delta;
                }
                receiptSequenceKnown=true;receiptLastSequence=sequence;
                foreach(var r in receipts)r.Advance(receiptLogicalSequence);
                foreach(var message in messages)message.ObjectReceipt?.Dispatched(sequence,receiptLogicalSequence);
                receipts.RemoveWhere(r=>r.Finished);
            }
        }
        internal void ObserveObjectReceipts(ushort ack,ushort bits)
        {
            var exact=SequencedMessage.AckedSequences(ack,bits);
            lock(receiptSync){foreach(var receipt in receipts)receipt.Observe(exact);receipts.RemoveWhere(r=>r.Finished);}
        }
        internal void CancelObjectReceipts()
        {lock(receiptSync){foreach(var receipt in receipts)receipt.Cancel();receipts.Clear();receiptSequenceKnown=false;receiptLogicalSequence=0;}}

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

            SequencedMessage message = new SequencedMessage(content.getBytes())
            {
                RpcWorldGeneration = world, RpcTransportEpoch = transport, TimedViewWithRpc = timedView,
                WorldSetupRpc = worldSetupQueue == this
            };
            // Same lock as the flush and ack code: the flush thread enumerates this list while packet
            // handlers add to it, and an unlocked Add crashed the server ("Collection was modified").
            lock (RPCMessagesQueue.SyncRoot)
            {
                RPCMessagesQueue.Add(message);
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
            var sent = new System.Collections.Generic.List<SequencedMessage>();
            var fresh = new System.Collections.Generic.List<SequencedMessage>();
            SequencedMessage protectedRpc = null;
            lock (RPCMessagesQueue.SyncRoot)
            {
                foreach (SequencedMessage message in RPCMessagesQueue)
                {
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
            }

            // Oldest first, across the 16-bit wrap: the oldest is the furthest behind the next counter.
            sent.Sort((a, b) => Behind(b, nextCounter).CompareTo(Behind(a, nextCounter)));
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

            var result = new System.Collections.Generic.List<SequencedMessage>(due + 1 + fresh.Count);
            for (int i = 0; i <= due; i++) result.Add(sent[i]);
            resend = due < 0 ? default : new RpcResend(sent[0].rpcNumber, sent[due].rpcNumber, due + 1, sent[0].sendCounter);

            int span = sent.Count == 0 ? 0 : Behind(sent[0], nextCounter);
            int room = Math.Max(0, MaxRpcsInFlight - span);
            for (int i = 0; i < fresh.Count && i < room; i++) result.Add(fresh[i]);
            return result;
        }

        /// <summary>How many counters before nextCounter this RPC's is: 1 for the newest sent one.</summary>
        private static int Behind(SequencedMessage message, UInt16 nextCounter) => (UInt16)(nextCounter - (UInt16)message.rpcNumber);

        // Called only within the existing transport admission gate, BEFORE any new RPC counter is assigned.
        internal void PruneUnassignedWorldRpcs(int world,long transport)
        {
            lock(RPCMessagesQueue.SyncRoot)
                for(int i=RPCMessagesQueue.Count-1;i>=0;i--)
                    if(((SequencedMessage)RPCMessagesQueue[i]).DiscardUnassignedWorldRpc(world,transport)) RPCMessagesQueue.RemoveAt(i);
        }

        public void addObjectMessage(byte[]messageBlock,bool isTimed)
        {
            AddObject(messageBlock,isTimed,null);
        }
        internal void AddTrackedObject(byte[] messageBlock,ObjectMessageReceipt receipt)=>AddObject(messageBlock,false,receipt);
        private void AddObject(byte[] messageBlock,bool isTimed,ObjectMessageReceipt receipt)
        {
            if(MissionOutput.Stage(this,messageBlock,true,isTimed,receipt))return;
            if(receipt!=null)lock(receiptSync){receipts.RemoveWhere(r=>r.Finished);receipts.Add(receipt);}
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
        /// </summary>
        internal void AddOrCoalesceMovement(ushort viewId, SelfViewUpdate state, byte[] forwardedState)
        {
            if (state == null || forwardedState == null || forwardedState.Length < 2) return;
            byte newFlags = (byte)(state.Flags & ~SelfViewUpdate.Flag20);
            if (newFlags == 0) return;

            lock (ObjectMessagesQueue.SyncRoot)
            {
                if (ObjectMessagesQueue.Count == 0 && pendingMovementByView.Count > 0)
                {
                    pendingMovementByView.Clear();
                    FirstUnsentObjectIndex = 0;
                }

                if (pendingMovementByView.TryGetValue(viewId, out SequencedMessage existing) && !existing.isSent)
                {
                    byte mergedFlags = (byte)(existing.MovementFlags | newFlags);
                    if (state.Action.HasValue) existing.MovementAction = state.Action.Value;
                    if (state.Yaw.HasValue) existing.MovementYaw = state.Yaw.Value;
                    if (state.HasPosition)
                    {
                        if (existing.MovementPosition == null) existing.MovementPosition = new byte[12];
                        Buffer.BlockCopy(forwardedState, forwardedState.Length - 12, existing.MovementPosition, 0, 12);
                    }
                    existing.MovementFlags = mergedFlags;
                    existing.content = WriteMovementPayload(viewId, existing, existing.content);
                    return;
                }

                byte[] payload = BuildViewUpdateFast(viewId, forwardedState);
                byte[] posCopy = null;
                if (state.HasPosition)
                {
                    posCopy = new byte[12];
                    Buffer.BlockCopy(forwardedState, forwardedState.Length - 12, posCopy, 0, 12);
                }

                SequencedMessage message = new SequencedMessage(payload)
                {
                    MovementViewId = viewId,
                    MovementFlags = newFlags,
                    MovementAction = state.Action ?? 0,
                    MovementYaw = state.Yaw ?? 0,
                    MovementPosition = posCopy
                };
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

        private static byte[] WriteMovementPayload(ushort viewId, SequencedMessage msg, byte[] reuse)
        {
            byte flags = msg.MovementFlags;
            int len = 4
                + ((flags & SelfViewUpdate.FlagAction) != 0 ? 1 : 0)
                + ((flags & SelfViewUpdate.FlagYaw) != 0 ? 1 : 0)
                + ((flags & SelfViewUpdate.FlagPosition) != 0 ? 12 : 0);
            byte[] buf = reuse != null && reuse.Length == len ? reuse : new byte[len];
            buf[0] = (byte)(viewId & 0xFF);
            buf[1] = (byte)((viewId >> 8) & 0xFF);
            buf[2] = SelfViewEntry.PartState;
            buf[3] = flags;
            int cursor = 4;
            if ((flags & SelfViewUpdate.FlagAction) != 0) buf[cursor++] = msg.MovementAction;
            if ((flags & SelfViewUpdate.FlagYaw) != 0) buf[cursor++] = msg.MovementYaw;
            if ((flags & SelfViewUpdate.FlagPosition) != 0) Buffer.BlockCopy(msg.MovementPosition, 0, buf, cursor, 12);
            return buf;
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
        }

        public void addRawMessage(byte[] messageBlock)
        {
            rawMessages.Add(messageBlock);
        }

    }
}
