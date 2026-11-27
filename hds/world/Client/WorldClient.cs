using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using hds.shared;

namespace hds
{
    public class WorldClient
    {
        private EndPoint Remote;
        private Socket socket;
        private volatile IWorldEncryption cypher;

        private string key;

        private UInt32 lastUsedTime;
        private int deadSignals;
        private bool alive;
        private object livenessLock;
        private bool explicitlyStopped, reapClaimed, reapInProgress;
        private long ingressGeneration;
        private long lastIngressTick = Environment.TickCount64;
        internal Func<uint> LivenessClock;
        private object LivenessSync => LazyInitializer.EnsureInitialized(ref livenessLock);
        private uint LivenessNow => LivenessClock==null?TimeUtils.getUnixTimeUint32():LivenessClock();
        internal (uint Last,long Generation,bool Claimed) IngressSnapshot
        {get {lock(LivenessSync)return(lastUsedTime,ingressGeneration,reapClaimed);}}
        private bool Terminal {get {lock(LivenessSync)return explicitlyStopped||reapClaimed;}}
        private bool NoteIngress()
        {
            lock(LivenessSync)
            {
                if(explicitlyStopped||reapClaimed||!alive)return false;
                lastUsedTime=LivenessNow;ingressGeneration++;
                Interlocked.Exchange(ref lastIngressTick, Environment.TickCount64);
                return true;
            }
        }
        // Called only under the endpoint dictionary gate. No cleanup/SQL or other owner locks here.
        internal bool TryClaimInactive()
        {
            lock(LivenessSync)
            {
                if(reapClaimed){if(reapInProgress)return false;reapInProgress=true;return true;}
                if(alive&&!explicitlyStopped&&(IsBot||unchecked(LivenessNow-lastUsedTime)<=30))return false;
                reapClaimed=true;reapInProgress=true;alive=false;return true;
            }
        }
        // Preserve the existing cleanup retry after a failure, while the endpoint stays terminal.
        internal void ReleaseFailedReap(){lock(LivenessSync)if(reapClaimed)reapInProgress=false;}
        private bool flushingQueueInProgress = false;
        // Reusable per-client flush scratch (accessed strictly while flushingQueueInProgress is true).
        private List<WorldPacket> flushPackets;
        private Stack<WorldPacket> flushPacketPool;
        private const int MaxPooledFlushPackets = 8;
        private readonly MultiProtocolManager protocolManager;
        private readonly object processLock = new object();
        private readonly Queue<byte[]> receivedPackets = new Queue<byte[]>();
        private bool packetWorkerRunning;
        private readonly object flushLock = new object();
        internal WorldSendFence TransportFence { get; } = new WorldSendFence();
        private long lastFlushTick;
        private volatile bool flushPending;
        public const int FlushIntervalMs = 50;

        public ClientData playerData { get; set; }
        public MessageQueue messageQueue;
        public ViewManager viewMan { get; set; }
        public PlayerCharacter playerInstance { get; set; }
        internal int VisibilitySampleIndex = -1;

        public WorldClient(EndPoint _Remote, Socket _socket, string _key)
        {
            Remote = _Remote;
            socket = _socket;
            key = _key;
            // A key nobody has until the UDP init names a verified character and UseSessionKey installs that login's
            // key: datagrams from anyone else fail the CRC and are dropped.
            cypher = new WorldEncryption(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
            protocolManager = new MultiProtocolManager();

            messageQueue = new MessageQueue();

            // Create the mpm structure for all world usage

            playerData = new ClientData();
            viewMan = new ViewManager();

            playerData.setUniqueKey(key);
            playerData.setRPCCounter((UInt16) 0); //not hardcoded, RPC server system its AWESOME now!
            playerInstance = Store.world.objMan.GetAssignedObject(key);


            alive = true;
            deadSignals = 0;
            // The client is in WorldServer.Clients before its first packet is processed. With this still 0 the visibility
            // thread could see it as 30+ seconds silent and remove it right then: its replies, queued for the flush thread,
            // never went out, and the player stayed stuck at login (found by tools/HeadlessClient).
            lastUsedTime = TimeUtils.getUnixTimeUint32();
        }


        /// <summary>Switches this session to the key margin generated for this login (see PlayerHandler.processInitUDPSession).</summary>
        public void UseSessionKey(byte[] sessionKey)
        {
            cypher = new WorldEncryption(sessionKey);
        }

        /// <summary>
        /// A player the server plays itself (dev partners such as ?duelbot): a full character in the world that other
        /// clients see and fight, with no connection behind it. What is sent to it is dropped.
        /// </summary>
        public bool IsBot { get; private set; }

        public static WorldClient CreateBot(string key)
        {
            lock (WorldServer.Clients)
            {
                Store.world.objMan.PushClient(key); // the object table is guarded by this lock everywhere else
            }
            return new WorldClient(key);
        }

        private WorldClient(string botKey)
        {
            IsBot = true;
            key = botKey;
            messageQueue = new MessageQueue();
            playerData = new ClientData { isBot = true };
            viewMan = new ViewManager();
            playerData.setUniqueKey(key);
            playerInstance = Store.world.objMan.GetAssignedObject(key);
            alive = true;
            lastUsedTime = TimeUtils.getUnixTimeUint32();
        }

        private void DropQueuedMessages()
        {
            lock (messageQueue.ObjectMessagesQueue.SyncRoot)
            {
                messageQueue.ObjectMessagesQueue.Clear();
                messageQueue.ClearPendingMovementsLocked();
            }
            lock (messageQueue.RPCMessagesQueue.SyncRoot)
            {
                messageQueue.RPCMessagesQueue.Clear();
            }
        }

        /// <summary>Requests that FlushThread send queued updates on its next tick without taking flushLock on a broadcaster's thread.</summary>
        internal void MarkFlushPending()
        {
            if (IsBot)
            {
                DropQueuedMessages();
                return;
            }
            flushPending = true;
        }

        // This method sends one packet at a time to the game client 
        private bool sendPacket(byte[] data) => sendPacket(data, data.Length);

        private bool sendPacket(byte[] data, int length)
        {
            if (this.deadSignals == 0)
            {
                //Output.WriteLine("[SEND PACKET] PSS: " + playerData.getPss().ToString() + " SSEQ : " + playerData.getSseq().ToString() + " CSEQ: " + playerData.getCseq().ToString());
                try
                {
                    // Synchronous UDP SendTo hands the datagram straight to the kernel send buffer in order,
                    // avoiding per-packet AsyncCallback/IAsyncResult/IOCP completions and async reordering.
                    socket.SendTo(data, 0, length, SocketFlags.None, Remote);
                    return true;
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("Socket Error " + ex.Message);
                    Alive = false;
                }
            }
            return false;
        }

        private WorldPacket RentFlushPacket()
        {
            if (flushPacketPool != null && flushPacketPool.Count > 0)
            {
                WorldPacket reused = flushPacketPool.Pop();
                reused.Reset(playerData);
                return reused;
            }
            return new WorldPacket(playerData);
        }

        private void ReturnFlushPacket(WorldPacket packet)
        {
            if (packet == null) return;
            packet.RPCMessages.Clear();
            packet.ObjectMessages.Clear();
            packet.content = null;
            if (flushPacketPool == null) flushPacketPool = new Stack<WorldPacket>(MaxPooledFlushPackets);
            if (flushPacketPool.Count < MaxPooledFlushPackets)
            {
                flushPacketPool.Push(packet);
            }
        }


        // Resend packets that are not acked
        public void CheckAndResend()
        {
            // First try to resent every packet
            FlushQueue(true);
            // And then clean Not Acked Packets
            CleanNakPackets();
        }

        private void CleanNakPackets()
        {
            lock (messageQueue.ObjectMessagesQueue.SyncRoot)
            {
                ArrayList q = messageQueue.ObjectMessagesQueue;
                int write = 0;
                for (int i = 0; i < q.Count; i++)
                {
                    SequencedMessage message = (SequencedMessage)q[i];
                    // Give up on object messages the client still hasn't acked after the last allowed send.
                    int maxSends = message.ReliableObject ? SequencedMessage.MaxReliableObjectSends : SequencedMessage.MaxObjectSends;
                    if (message.sendCounter >= maxSends)
                    {
                        if (message.ReliableObject)
                        {
                            messageQueue.ReliableObjectCount--;
                            Output.WriteDebugLog("[NET] Giving up on spawn object after " + message.sendCounter +
                                                 " sends, char " + playerData.getCharID());
                        }
                        messageQueue.OnObjectMessageSentLocked(message);
                        messageQueue.RecycleMovementMessageLocked(message);
                        continue;
                    }
                    if (write != i) q[write] = message;
                    write++;
                }
                if (write < q.Count)
                {
                    q.RemoveRange(write, q.Count - write);
                    messageQueue.FirstUnsentObjectIndex = 0;
                }
            }

            lock (messageQueue.RPCMessagesQueue.SyncRoot)
            {
                ArrayList q = messageQueue.RPCMessagesQueue;
                int write = 0;
                for (int i = 0; i < q.Count; i++)
                {
                    SequencedMessage message = (SequencedMessage)q[i];
                    if (message.sendCounter >= SequencedMessage.MaxRpcSends)
                    {
                        Output.WriteDebugLog("[NET] Giving up on RPC " + message.rpcNumber + " after " + message.sendCounter + " sends");
                        continue;
                    }
                    if (write != i) q[write] = message;
                    write++;
                }
                if (write < q.Count)
                {
                    q.RemoveRange(write, q.Count - write);
                }
            }
        }

        // Flush the MessageQueue Lists to the Client
        private int rpcPacketsSent;

        public void FlushQueue(bool sent = false, bool force = false)
        {
            if(MissionOutput.Buffered(messageQueue))return;
            if (IsBot)
            {
                DropQueuedMessages();
                return;
            }
            if (!force && !sent)
            {
                long now = Environment.TickCount64;
                if (now - lastFlushTick < FlushIntervalMs)
                {
                    flushPending = true;
                    return;
                }
            }

            lock (flushLock)
            {
                if (flushingQueueInProgress)
                {
                    flushPending = true;
                    return;
                }
                flushingQueueInProgress = true;
                flushPending = false; // consume only requests that preceded this flush
            }

            long scaleFlushStart=0;
            List<WorldPacket> worldPackets = flushPackets;
            if (worldPackets == null)
            {
                worldPackets = new List<WorldPacket>(4);
                flushPackets = worldPackets;
            }
            worldPackets.Clear();
            WorldPacket packet = null;
            try
            {
            scaleFlushStart=ScaleDiagnostics.Start;
            long outgoingEpoch = TransportFence.Capture();

            // This resend the MessageQueue - should be called after parsing or after sending something out
            // or in a timed interval (keep alive for example)

            // Sends RAW MEssages
            SendRawMessages();

            packet = RentFlushPacket();

            if (!TransportFence.TryRun(outgoingEpoch, () =>
            {
            // Init encrypted Packet if we have MPM Messages
            if (messageQueue.RPCMessagesQueue.Count > 0 || messageQueue.ObjectMessagesQueue.Count > 0)
            {
                // we currently dont know if we send something out so we need to proove that in a way
                messageQueue.PruneStaleReliableObjects(playerData.worldGeneration, outgoingEpoch);
                if (messageQueue.ObjectMessagesQueue.Count > 0)
                {
                    long nowTick = Environment.TickCount64;
                    int reliableResendCount = 0;
                    int reliableOldestSend = 0;
                    lock (messageQueue.ObjectMessagesQueue.SyncRoot)
                    {
                        ArrayList q = messageQueue.ObjectMessagesQueue;
                        bool canSkipSent = !SequencedMessage.ObjectResendEnabled && messageQueue.ReliableObjectCount == 0;
                        int start = 0;
                        if (canSkipSent &&
                            messageQueue.FirstUnsentObjectIndex > 0 &&
                            messageQueue.FirstUnsentObjectIndex <= q.Count &&
                            ((SequencedMessage)q[messageQueue.FirstUnsentObjectIndex - 1]).isSent)
                        {
                            start = messageQueue.FirstUnsentObjectIndex;
                        }

                        HashSet<SequencedMessage> resentReliableObjects = null;
                        for (int i = start; i < q.Count; i++)
                        {
                            SequencedMessage messageObjects = (SequencedMessage)q[i];
                            // New messages, plus unacked ones whose resend interval has passed (and dependent
                            // spawn-critical updates pulled behind a resent prerequisite in the same flush).
                            bool due = messageObjects.IsObjectSendDue(nowTick);
                            bool followPrerequisite = !due &&
                                messageObjects.ReliableObject &&
                                !messageObjects.ReliableObjectAcked &&
                                messageObjects.ReliableObjectPrerequisite != null &&
                                resentReliableObjects != null &&
                                resentReliableObjects.Contains(messageObjects.ReliableObjectPrerequisite) &&
                                messageObjects.sendCounter < SequencedMessage.MaxReliableObjectSends;
                            if (due || followPrerequisite)
                            {
                                if (messageObjects.ReliableObject && messageObjects.isSent)
                                {
                                    if (resentReliableObjects == null) resentReliableObjects = new HashSet<SequencedMessage>();
                                    resentReliableObjects.Add(messageObjects);
                                    reliableResendCount++;
                                    int nextSend = messageObjects.sendCounter + 1;
                                    if (nextSend > reliableOldestSend) reliableOldestSend = nextSend;
                                }
                                messageObjects.MarkObjectSent(nowTick);
                                messageQueue.OnObjectMessageSentLocked(messageObjects);
                                bool canAddThePak = packet.addObjectContent(messageObjects);
                                if (canAddThePak == false)
                                {
                                    packet.isFinal = true;
                                    // if one sub has a timed param set, we just set it here too
                                    worldPackets.Add(packet);
                                    // Start new packet and add it to the queue 
                                    packet = RentFlushPacket();
                                    packet.addObjectContent(messageObjects);
                                }
                                if (messageObjects.isTimed == true)
                                {
                                    packet.timed = true;
                                }
                            }
                        }

                        if (canSkipSent)
                        {
                            messageQueue.FirstUnsentObjectIndex = q.Count;
                        }
                    }

                    if (reliableResendCount > 0)
                    {
                        Output.WriteDebugLog("[NET] Resending " + reliableResendCount + " spawn object(s) (send " +
                                             reliableOldestSend + "), char " + playerData.getCharID() +
                                             "; client acked sseq " + playerData.getACK() +
                                             " bits " + playerData.getPss().ToString("x2"));
                    }
                }

                // Workaround: complete the packet so we dont mix up
                if (packet.ObjectMessages.Count > 0)
                {
                    packet.isFinal = true;
                    worldPackets.Add(packet);
                    packet = RentFlushPacket();
                    packet.timed = false;
                }

                messageQueue.PruneUnassignedWorldRpcs(playerData.worldGeneration,outgoingEpoch);
                if (messageQueue.RPCMessagesQueue.Count > 0)
                {
                    lock (messageQueue.RPCMessagesQueue.SyncRoot)
                    {
                        long rpcNow = Environment.TickCount64;
                        // Unacked RPCs that are due go out again as one run from the oldest unacked counter, then new
                        // ones, all in counter order and within MessageQueue.MaxRpcsInFlight (TakeRpcsToSend).
                        List<SequencedMessage> rpcsToSend = messageQueue.TakeRpcsToSend(rpcNow, playerData.getRPCCounter(),
                            out MessageQueue.RpcResend resend, Interlocked.Read(ref lastIngressTick));
                        if (resend.Count > 0)
                        {
                            // One line per flush. The char id says which session (a fixture's probe shares the log), and
                            // the client's last ack says whether it is acking anything at all.
                            Output.WriteDebugLog("[NET] Resending RPC " + resend.First + " (send " + (resend.OldestSend + 1) + ")" +
                                                 (resend.Count > 1 ? " through " + resend.Last + ", " + resend.Count + " RPCs" : "") +
                                                 ", char " + playerData.getCharID() + "; client acked sseq " + playerData.getACK() +
                                                 " bits " + playerData.getPss().ToString("x2"));
                        }
                        int rpcIndex = 0;
                        while (rpcIndex < rpcsToSend.Count)
                        {
                            SequencedMessage first = rpcsToSend[rpcIndex];
                            if (first.rpcNumber >= 0 && first.rpcBlockStart >= 0)
                            {
                                List<SequencedMessage> existingBlock = new List<SequencedMessage> { first };
                                int blockStart = first.rpcBlockStart;
                                rpcIndex++;
                                while (rpcIndex < rpcsToSend.Count &&
                                       rpcsToSend[rpcIndex].rpcNumber >= 0 &&
                                       rpcsToSend[rpcIndex].rpcBlockStart == blockStart)
                                {
                                    existingBlock.Add(rpcsToSend[rpcIndex]);
                                    rpcIndex++;
                                }

                                foreach (SequencedMessage m in existingBlock)
                                {
                                    m.lastRpcSendTick = rpcNow;
                                    m.IncreaseSentCounter();
                                }

                                if (packet.addRPCBlock(existingBlock) == false)
                                {
                                    packet.isFinal = true;
                                    worldPackets.Add(packet);
                                    packet = RentFlushPacket();
                                    packet.addRPCBlock(existingBlock);
                                }

                                if (existingBlock.Any(m => m.isTimed))
                                {
                                    packet.timed = true;
                                }
                                continue;
                            }

                            rpcIndex++;
                            first.lastRpcSendTick = rpcNow;
                            first.IncreaseSentCounter();
                            if (packet.addRPCContent(first) == false)
                            {
                                packet.isFinal = true;
                                worldPackets.Add(packet);
                                // Start new packet and add it to the queue 
                                packet = RentFlushPacket();
                                packet.addRPCContent(first);
                            }

                            if (first.isTimed == true)
                            {
                                packet.timed = true;
                            }
                        }
                    }
                }

                // Check if the current not finalize packet has content and needs to be send
                if ((packet.isFinal == false) && (packet.ObjectMessages.Count > 0 || packet.RPCMessages.Count > 0))
                {
                    worldPackets.Add(packet);
                    packet = null;
                }
            }

            // We have nothing - but we should really ack this
            if (messageQueue.ackOnlyCount > 0)
            {
                for (int i = 0; i < messageQueue.ackOnlyCount; i++)
                {
                    // Ack-only packets are a bare 02 (retail too). This used to set isFinal/timed on
                    // `packet` - the content packet - instead of ackPacket, which stamped sim time on
                    // whatever content went out in the same flush.
                    WorldPacket ackPacket = RentFlushPacket();
                    ackPacket.isFinal = true;
                    worldPackets.Add(ackPacket);
                }
                messageQueue.ackOnlyCount = 0;
            }


            })) { flushPending = true; return; }

            // We have now PacketObjects - time to send them
            if (worldPackets.Count > 0)
            {
                #if DEBUG
                Output.WriteLine("[CLIENT] Flush the final Queue with " + worldPackets.Count.ToString() + " Packets");
                #endif 
                for (int pi = 0; pi < worldPackets.Count; pi++)
                {
                    WorldPacket thePacket = worldPackets[pi];
                    if (!TransportFence.TryRun(outgoingEpoch, () =>
                    {
                    // A world generation can change after preparation. Drop only unnumbered producer-owned records.
                    thePacket.PruneUnassignedWorldRpcs(playerData.worldGeneration,outgoingEpoch);
                    messageQueue.PruneUnassignedWorldRpcs(playerData.worldGeneration,outgoingEpoch);
                    // A bare reset ACK can arrive after the client resets its sequence history.
                    // The reliable reset RPC already acknowledges the last request.
                    if (thePacket.IsEmpty && playerData.waitForRPCShutDown) return;

                    IWorldEncryption activeCypher = cypher;
                    if (activeCypher is WorldEncryption worldCrypto)
                    {
                        // Zero-alloc pooled send path: serialize plaintext straight into workBuf[14..],
                        // encrypt into a pooled wire buffer, dispatch synchronously, and return both buffers.
                        int maxPlain = thePacket.EstimateMaxPlaintextSize();
                        byte[] workBuf = ArrayPool<byte>.Shared.Rent(14 + maxPlain + 16);
                        byte[] wireBuf = null;
                        try
                        {
                            int payloadLen = thePacket.WriteFinalData(playerData, workBuf, 14);
                            long encryptStart = Output.PacketLogsEnabled ? Stopwatch.GetTimestamp() : 0;

                            // Keep the stock-compatible envelope until the world reset is complete. The
                            // old and new client counters can overlap while RESETDONE is still in flight.
                            bool transitioning = playerData.worldChangePending || (workBuf[14] & 0x40) != 0;
                            ushort receiptBits = transitioning ? playerData.getPss() : messageQueue.ClientReceipts.Bits;
                            ushort receiptSequence = transitioning ? playerData.getCseq() : messageQueue.ClientReceipts.Latest;
                            worldCrypto.EncryptFromWorkBuffer(workBuf, payloadLen, receiptBits, receiptSequence,
                                playerData.getSseq(), out wireBuf, out int wireLen);

                            if ((thePacket.RPCMessages.Count > 0 && DropTest.ShouldDrop(++rpcPacketsSent)) ||
                                DropTest.ShouldDropSseq(playerData.getSseq()))
                            {
                                Output.WriteDebugLog("[NET] Test: dropped packet sseq " + playerData.getSseq());
                            }
                            else
                            {
                                bool dispatched = sendPacket(wireBuf, wireLen);
                                if (dispatched) thePacket.RecordDispatchedObjectReceipts(messageQueue);
                                if (dispatched && ScaleDiagnostics.Enabled)
                                {
                                    for (int mi = 0; mi < thePacket.RPCMessages.Count; mi++)
                                        thePacket.RPCMessages[mi].RecordScaleFirstSend(false);
                                    for (int mi = 0; mi < thePacket.ObjectMessages.Count; mi++)
                                        thePacket.ObjectMessages[mi].RecordScaleFirstSend(true);
                                }
                            }

                            if (Output.PacketLogsEnabled)
                            {
                                byte[] finalData = new byte[payloadLen];
                                Buffer.BlockCopy(workBuf, 14, finalData, 0, payloadLen);
                                Output.WritePacketLog(this, finalData, "SERVER",
                                    receiptBits.ToString(), receiptSequence.ToString(),
                                    playerData.getSseq().ToString(),
                                    Stopwatch.GetElapsedTime(encryptStart).TotalMilliseconds.ToString(), "ENCRYPT");
                            }
                        }
                        finally
                        {
                            if (wireBuf != null) ArrayPool<byte>.Shared.Return(wireBuf);
                            ArrayPool<byte>.Shared.Return(workBuf);
                        }
                    }
                    else
                    {
                        // Custom IWorldEncryption path (e.g. unit-test recording ciphers).
                        byte[] finalData = thePacket.getFinalData(playerData);
                        long encryptStart = Output.PacketLogsEnabled ? Stopwatch.GetTimestamp() : 0;

                        bool transitioning = playerData.worldChangePending || (finalData[0] & 0x40) != 0;
                        ushort receiptBits = transitioning ? playerData.getPss() : messageQueue.ClientReceipts.Bits;
                        ushort receiptSequence = transitioning ? playerData.getCseq() : messageQueue.ClientReceipts.Latest;
                        byte[] encryptedData = activeCypher.encrypt(finalData, finalData.Length, receiptBits,
                            receiptSequence, playerData.getSseq());
                        if ((thePacket.RPCMessages.Count > 0 && DropTest.ShouldDrop(++rpcPacketsSent)) ||
                            DropTest.ShouldDropSseq(playerData.getSseq()))
                        {
                            Output.WriteDebugLog("[NET] Test: dropped packet sseq " + playerData.getSseq());
                        }
                        else
                        {
                            bool dispatched = sendPacket(encryptedData);
                            if (dispatched) thePacket.RecordDispatchedObjectReceipts(messageQueue);
                            if (dispatched && ScaleDiagnostics.Enabled)
                            {
                                for (int mi = 0; mi < thePacket.RPCMessages.Count; mi++)
                                    thePacket.RPCMessages[mi].RecordScaleFirstSend(false);
                                for (int mi = 0; mi < thePacket.ObjectMessages.Count; mi++)
                                    thePacket.ObjectMessages[mi].RecordScaleFirstSend(true);
                            }
                        }

                        if (Output.PacketLogsEnabled)
                        {
                            Output.WritePacketLog(this, finalData, "SERVER",
                                receiptBits.ToString(), receiptSequence.ToString(),
                                playerData.getSseq().ToString(),
                                Stopwatch.GetElapsedTime(encryptStart).TotalMilliseconds.ToString(), "ENCRYPT");
                        }
                    }
                    })) flushPending = true;
                }
            }

            }
            finally
            {
                if (packet != null) ReturnFlushPacket(packet);
                for (int pi = 0; pi < worldPackets.Count; pi++)
                {
                    ReturnFlushPacket(worldPackets[pi]);
                }
                worldPackets.Clear();
                lock (flushLock)
                {
                    flushingQueueInProgress = false;
                    lastFlushTick = Environment.TickCount64;
                    // Preserve requests raised during this flush, including reset/setup retries.
                }
                ScaleDiagnostics.FlushCompleted(scaleFlushStart);
            }
        }

        private void SendRawMessages()
        {
            lock (messageQueue.rawMessages.SyncRoot)
            {
                if (messageQueue.rawMessages.Count > 0)
                {
                    foreach (byte[] message in messageQueue.rawMessages)
                    {
                        Output.WriteUnencryptedPacketLog(message, "SERVER");
                        sendPacket(message);
                    }
                }
                messageQueue.rawMessages.Clear();
            }
        }

        public void DeletedAckedPackets(UInt16 ackSeq)
        {
            messageQueue.ObserveObjectReceipts(ackSeq,playerData.getPss());
            HashSet<UInt16> ackedSeqs = null;
            lock (messageQueue.ObjectMessagesQueue.SyncRoot)
            {
                ArrayList q = messageQueue.ObjectMessagesQueue;
                int write = 0;
                for (int i = 0; i < q.Count; i++)
                {
                    SequencedMessage message = (SequencedMessage)q[i];
                    if (message.ReliableObject)
                    {
                        if (ackedSeqs == null) ackedSeqs = SequencedMessage.AckedSequences(ackSeq, playerData.getPss());
                        bool prerequisiteAcked = message.ReliableObjectPrerequisite == null ||
                                                 message.ReliableObjectPrerequisite.ReliableObjectAcked;
                        bool sequenceAcked = message.IsReliableObjectAckedBy(ackedSeqs);
                        if (prerequisiteAcked && sequenceAcked)
                        {
                            message.ReliableObjectAcked = true;
                            messageQueue.ReliableObjectCount--;
                            continue;
                        }
                        if (!prerequisiteAcked && sequenceAcked)
                        {
                            // This spawn-critical update arrived before its prerequisite (e.g. own-view nav position
                            // arrived while self ViewCreate was lost and discarded by the client). Clear the premature
                            // SSEQ record so it resends behind its prerequisite rather than retiring early.
                            message.ClearReliableObjectPackets();
                            message.ReliableObjectGapObserved = true;
                        }
                        else if (message.IsAckedBy(ackSeq))
                        {
                            // Client ACKed past this SSEQ without setting its PSS bit; trigger the faster retry cadence.
                            message.ReliableObjectGapObserved = true;
                        }
                    }
                    else if (message.IsAckedBy(ackSeq))
                    {
                        // Only a message that has gone out can be acked. A queued-but-unsent message
                        // still has neededAck 0, so without the sendCounter check any client ack that
                        // arrived before the next flush deleted it unsent. That dropped the self-spawn
                        // about half the time and left the client at "Negotiating with Servers...".
                        messageQueue.RecycleMovementMessageLocked(message);
                        continue;
                    }
                    if (write != i) q[write] = message;
                    write++;
                }
                if (write < q.Count)
                {
                    q.RemoveRange(write, q.Count - write);
                    messageQueue.FirstUnsentObjectIndex = 0;
                }
            }

            lock (messageQueue.RPCMessagesQueue.SyncRoot)
            {
                ArrayList q = messageQueue.RPCMessagesQueue;
                if (q.Count > 0)
                {
                    if (ackedSeqs == null) ackedSeqs = SequencedMessage.AckedSequences(ackSeq, playerData.getPss());
                    int write = 0;
                    for (int i = 0; i < q.Count; i++)
                    {
                        SequencedMessage message = (SequencedMessage)q[i];
                        // Gone once the client acks a packet it was in; until then FlushQueue sends it again
                        if (message.rpcNumber >= 0 && message.IsRpcAckedBy(ackedSeqs))
                        {
                            continue;
                        }
                        if (write != i) q[write] = message;
                        write++;
                    }
                    if (write < q.Count)
                    {
                        q.RemoveRange(write, q.Count - write);
                    }
                }
            }
        }


        public byte[] DecryptReceivedPacket(byte[] packet)
        {
            byte[] processedPacket;
            ushort pss;
            ushort cseq;
            ushort ackSeq;

            IWorldEncryption activeCypher = cypher;
            if (activeCypher is WorldEncryption worldCrypto)
            {
                if (!worldCrypto.TryDecrypt(packet, packet.Length, out processedPacket, out pss, out cseq, out ackSeq, stripTimedHeader: true))
                {
                    return null; // corrupt packet, already logged
                }
            }
            else
            {
                ArrayList decValues = activeCypher.decrypt(packet, packet.Length);
                if (decValues == null)
                {
                    return null; // corrupt packet, already logged
                }
                processedPacket = (byte[])decValues[0];
                pss = (UInt16)decValues[1];
                cseq = (UInt16)decValues[2];
                ackSeq = (UInt16)decValues[3];

                if (processedPacket[0] == 0x82) //It's a timed data packet
                {
                    byte[] temp = new byte[processedPacket.Length - 4];
                    temp[0] = (byte)0x02;
                    ArrayUtils.copy(processedPacket, 5, temp, 1, processedPacket.Length - 4);
                    processedPacket = temp;
                }
            }

            // Fence reset before publishing its new envelope headers, not later in Parse.
            bool resetReceived = (processedPacket[0] & 0x40) != 0;
            TransportFence.PublishIncoming(resetReceived, () =>
            {
            if (resetReceived)
            {
                // The RESETDONE envelope still carries the old epoch's sequence numbers.
                messageQueue.ClientReceipts.Reset();
                messageQueue.ResendBackoff.Reset();
            }
            else
            {
                messageQueue.ClientReceipts.Observe(cseq);
                messageQueue.ResendBackoff.ObserveAck(ackSeq);
            }
            playerData.setPss(pss);
            playerData.setCseq(cseq);
            playerData.setACK(ackSeq);

            if (DropTest.Enabled)
            {
                Output.WriteDebugLog("[NET] Client acks sseq " + ackSeq);
            }

            this.DeletedAckedPackets(ackSeq); // REMOVING FROM ACK QUEUE
            });
            return processedPacket;
        }

        /// <summary>Called by the socket receiver before it arms the next receive, preserving datagram arrival order.</summary>
        internal void QueuePacket(byte[] packet)
        {
            // The existing policy counts raw endpoint activity before decryption. A busy serialized
            // worker must not make real incoming keepalives appear to be30 seconds of silence.
            if(!NoteIngress())return;
            lock (receivedPackets)
            {
                receivedPackets.Enqueue(packet);
                if (packetWorkerRunning)
                {
                    return;
                }
                packetWorkerRunning = true;
            }
            ThreadPool.QueueUserWorkItem(_ => ProcessQueuedPackets());
        }

        private void ProcessQueuedPackets()
        {
            while (true)
            {
                if(Terminal){lock(receivedPackets){receivedPackets.Clear();packetWorkerRunning=false;}return;}
                byte[] packet;
                lock (receivedPackets)
                {
                    if (receivedPackets.Count == 0)
                    {
                        packetWorkerRunning = false;
                        return;
                    }
                    packet = receivedPackets.Dequeue();
                }
                ProcessPacketCore(packet);
            }
        }
        internal int PendingPacketCount {get {lock(receivedPackets)return receivedPackets.Count;}}

        public void processPacket(byte[] packet)
        {
            // Preserve the direct entry's raw-arrival contract; queued work was stamped at ingress.
            if(NoteIngress())ProcessPacketCore(packet);
        }
        private void ProcessPacketCore(byte[] packet)
        {
            lock (processLock)
            {
                if(Terminal)return;
                WorldClient previousClient = Store.currentClient;
                Store.currentClient = this;
                try
                {
                    // Decryption start
                    bool encrypted = false;

                    byte[] processedPacket = null;
                    if (packet.Length > 0)
                    {
                        if (packet[0] == 0x00)
                        {
                            // Plain text packet
                            processedPacket = packet;
                            if (Output.PacketLogsEnabled)
                            {
                                Output.WritePacketLog(processedPacket, "CLIENT",
                                    playerData.getPss().ToString(), playerData.getCseq().ToString(),
                                    playerData.getACK().ToString());
                            }
                        }
                        else
                        {
                            encrypted = true;
                            long decryptStart = Output.PacketLogsEnabled ? Stopwatch.GetTimestamp() : 0;
                            processedPacket = DecryptReceivedPacket(packet);
                            if (processedPacket == null)
                            {
                                return;
                            }

                            if (Output.PacketLogsEnabled)
                            {
                                Output.WritePacketLog(this, processedPacket, "CLIENT",
                                    playerData.getPss().ToString(), playerData.getCseq().ToString(), playerData.getACK().ToString(),
                                    Stopwatch.GetElapsedTime(decryptStart).TotalMilliseconds.ToString(), "DECRYPT");
                            }
                        }

                        using(MissionProxyIntroduction.Incoming(this,encrypted && (processedPacket[0]&0x40)==0))
                            protocolManager.Parse(encrypted, processedPacket);
                        FlushQueue();
                    }
                }
                catch (Exception ex)
                {
                    // This runs on the socket receive callback: an exception escaping here ends the process and
                    // drops every player, so log it and drop just this packet.
                    Output.WriteLine("[CLIENT] Packet handling failed for charId " + playerData.getCharID() + ": " + ex);
                }
                finally
                {
                    Store.currentClient = previousClient;
                }
            }
        }

        public bool HasPendingMessages()
        {
            if (messageQueue.ackOnlyCount > 0)
            {
                return true;
            }

            lock (messageQueue.rawMessages.SyncRoot)
            {
                if (messageQueue.rawMessages.Count > 0)
                {
                    return true;
                }
            }

            lock (messageQueue.ObjectMessagesQueue.SyncRoot)
            {
                if (messageQueue.ObjectMessagesQueue.Count > 0)
                {
                    return true;
                }
            }

            lock (messageQueue.RPCMessagesQueue.SyncRoot)
            {
                if (messageQueue.RPCMessagesQueue.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void TickFlush()
        {
            if (!flushPending && !HasPendingMessages())
            {
                return;
            }

            FlushQueue(false, true);
        }


        internal bool IsCurrentEndpointOwner
        {
            get
            {
                var registry=WorldServer.Clients;
                if(registry==null || key==null)return false;
                lock(registry)return ReferenceEquals(registry,WorldServer.Clients) &&
                    registry.TryGetValue(key,out var owner) && ReferenceEquals(owner,this);
            }
        }

        public bool Alive
        {
            get
            {
                lock(LivenessSync)return alive&&!explicitlyStopped&&!reapClaimed&&
                    (IsBot||unchecked(LivenessNow-lastUsedTime)<=30);
            }
            set {lock(LivenessSync){if(!value){alive=false;explicitlyStopped=true;}else if(!explicitlyStopped&&!reapClaimed)alive=true;}}
        }
    }
}
