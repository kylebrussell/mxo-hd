using System;
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
        private IWorldEncryption cypher;

        private string key;

        private UInt32 lastUsedTime;
        private int deadSignals;
        private bool alive;
        private bool flushingQueueInProgress = false;
        private readonly MultiProtocolManager protocolManager;
        private readonly object processLock = new object();
        private readonly Queue<byte[]> receivedPackets = new Queue<byte[]>();
        private bool packetWorkerRunning;
        private readonly object flushLock = new object();
        private long lastFlushTick;
        private volatile bool flushPending;
        public const int FlushIntervalMs = 50;

        public ClientData playerData { get; set; }
        public MessageQueue messageQueue;
        public ViewManager viewMan { get; set; }
        public PlayerCharacter playerInstance { get; set; }

        public WorldClient(EndPoint _Remote, Socket _socket, string _key)
        {
            Remote = _Remote;
            socket = _socket;
            key = _key;
            cypher = new WorldEncryption();
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
            }
            lock (messageQueue.RPCMessagesQueue.SyncRoot)
            {
                messageQueue.RPCMessagesQueue.Clear();
            }
        }

        // This method sends one packet at a time to the game client 
        private bool sendPacket(byte[] data)
        {
            if (this.deadSignals == 0)
            {
                //Output.WriteLine("[SEND PACKET] PSS: " + playerData.getPss().ToString() + " SSEQ : " + playerData.getSseq().ToString() + " CSEQ: " + playerData.getCseq().ToString());
                try
                {
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, Remote, new AsyncCallback(FinalSendTo), Remote);
                    return true;
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("Socket Error " + ex.Message);
                    alive = false;
                }
            }
            return false;
        }

        private void FinalSendTo(IAsyncResult asyncResult)
        {
            try
            {
                socket.EndSend(asyncResult);
            }
            catch (Exception ex)
            {
                Output.WriteDebugLog("SendData Error: " + ex.Message);
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
            // ToDo: split flushQueue and resending 
            UInt32 currentTime = TimeUtils.getUnixTimeUint32();
            // Remove Packets that are resent more than 3 times and we didnt got an ack for it
            ArrayList ackedObjectMessages = new ArrayList();
            lock (messageQueue.ObjectMessagesQueue.SyncRoot)
            {
                foreach (SequencedMessage message in messageQueue.ObjectMessagesQueue)
                {
                    // Give up on object messages the client still hasn't acked after the last allowed send.
                    if (message.sendCounter >= SequencedMessage.MaxObjectSends)
                    {
                        ackedObjectMessages.Add(message);
                    }
                }
            }


            ArrayList ackedRPCMessages = new ArrayList();
            lock (messageQueue.RPCMessagesQueue.SyncRoot)
            {
                foreach (SequencedMessage message in messageQueue.RPCMessagesQueue)
                {
                    if (message.sendCounter >= SequencedMessage.MaxRpcSends)
                    {
                        Output.WriteDebugLog("[NET] Giving up on RPC " + message.rpcNumber + " after " + message.sendCounter + " sends");
                        ackedRPCMessages.Add(message);
                    }
                }
            }

            // Now delete them finally
            foreach (SequencedMessage deletedAckObject in ackedObjectMessages)
            {
                lock (messageQueue.ObjectMessagesQueue.SyncRoot)
                {
                    messageQueue.ObjectMessagesQueue.Remove(deletedAckObject);
                }
            }

            foreach (SequencedMessage deletedAckRpc in ackedRPCMessages)
            {
                lock (messageQueue.RPCMessagesQueue.SyncRoot)
                {
                    messageQueue.RPCMessagesQueue.Remove(deletedAckRpc);
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
            }

            long scaleFlushStart=ScaleDiagnostics.Start;

            // This resend the MessageQueue - should be called after parsing or after sending something out
            // or in a timed interval (keep alive for example)

            // Sends RAW MEssages
            SendRawMessages();
            ArrayList worldPackets = new ArrayList();

            WorldPacket packet = new WorldPacket(playerData);

            // Init encrypted Packet if we have MPM Messages
            if (messageQueue.RPCMessagesQueue.Count > 0 || messageQueue.ObjectMessagesQueue.Count > 0)
            {
                // we currently dont know if we send something out so we need to proove that in a way
                if (messageQueue.ObjectMessagesQueue.Count > 0)
                {
                    UInt16 sendingSSeq = playerData.calculateNextPossibleSseq();
                    long nowTick = Environment.TickCount64;
                    lock (messageQueue.ObjectMessagesQueue.SyncRoot)
                    {
                        foreach (SequencedMessage messageObjects in messageQueue.ObjectMessagesQueue)
                        {
                            // New messages, plus unacked ones whose resend interval has passed.
                            if (messageObjects.IsObjectSendDue(nowTick))
                            {
                                messageObjects.MarkObjectSent(nowTick);
                                bool canAddThePak = packet.addObjectContent(messageObjects);
                                if (canAddThePak == false)
                                {
                                    packet.isFinal = true;
                                    // if one sub has a timed param set, we just set it here too
                                    worldPackets.Add(packet);
                                    // Start new packet and add it to the queue 
                                    packet = new WorldPacket(playerData);
                                    packet.addObjectContent(messageObjects);
                                }
                                if (messageObjects.isTimed == true)
                                {
                                    packet.timed = true;
                                }
                            }
                        }
                    }
                }

                // Workaround: complete the packet so we dont mix up
                if (packet.ObjectMessages.Count > 0)
                {
                    packet.isFinal = true;
                    worldPackets.Add(packet);
                    packet = new WorldPacket(playerData);
                    packet.timed = false;
                }

                if (messageQueue.RPCMessagesQueue.Count > 0)
                {
                    lock (messageQueue.RPCMessagesQueue.SyncRoot)
                    {
                        long rpcNow = Environment.TickCount64;
                        foreach (SequencedMessage messageRPC in messageQueue.RPCMessagesQueue)
                        {
                            // New RPCs, and sent ones the client hasn't acked within RpcResendIntervalMs. They stay
                            // in counter order, so a resent one goes out ahead of anything newer.
                            if (messageRPC.IsRpcDue(rpcNow))
                            {
                                if (messageRPC.rpcNumber >= 0)
                                {
                                    Output.WriteDebugLog("[NET] Resending RPC " + messageRPC.rpcNumber + " (send " +
                                                         (messageRPC.sendCounter + 1) + ")");
                                }

                                messageRPC.lastRpcSendTick = rpcNow;
                                messageRPC.IncreaseSentCounter();
                                if (packet.addRPCContent(messageRPC) == false)
                                {
                                    packet.isFinal = true;
                                    worldPackets.Add(packet);
                                    // Start new packet and add it to the queue 
                                    packet = new WorldPacket(playerData);
                                    packet.addRPCContent(messageRPC);
                                }

                                if (messageRPC.isTimed == true)
                                {
                                    packet.timed = true;
                                }
                            }
                        }
                    }
                }

                // Check if the current not finalize packet has content and needs to be send
                if ((packet.isFinal == false) && (packet.ObjectMessages.Count > 0 || packet.RPCMessages.Count > 0))
                {
                    worldPackets.Add(packet);
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
                    WorldPacket ackPacket = new WorldPacket(playerData);
                    ackPacket.isFinal = true;
                    worldPackets.Add(ackPacket);
                }
                messageQueue.ackOnlyCount = 0;
            }


            // We have now PacketObjects - time to send them
            if (worldPackets.Count > 0)
            {
                #if DEBUG
                Output.WriteLine("[CLIENT] Flush the final Queue with " + worldPackets.Count.ToString() + " Packets");
                #endif 
                foreach (WorldPacket thePacket in worldPackets)
                {
                    playerData.IncrementSseq();

                    byte[] finalData = thePacket.getFinalData(playerData);

                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    byte[] encryptedData = cypher.encrypt(finalData, finalData.Length, playerData.getPss(),
                        playerData.getCseq(), playerData.getSseq());
                    if (thePacket.RPCMessages.Count > 0 && DropTest.ShouldDrop(++rpcPacketsSent))
                    {
                        Output.WriteDebugLog("[NET] Test: dropped RPC packet sseq " + playerData.getSseq());
                    }
                    else
                    {
                        bool dispatched=sendPacket(encryptedData);
                        if(dispatched && ScaleDiagnostics.Enabled)
                        {
                            foreach(var message in thePacket.RPCMessages)message.RecordScaleFirstSend(false);
                            foreach(var message in thePacket.ObjectMessages)message.RecordScaleFirstSend(true);
                        }
                    }

                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;


                    Output.WritePacketLog(this, finalData, "SERVER",
                        playerData.getPss().ToString(), playerData.getCseq().ToString(),
                        playerData.getSseq().ToString(), ts.TotalMilliseconds.ToString(), "ENCRYPT");
                }
            }

            ScaleDiagnostics.FlushCompleted(scaleFlushStart);
            flushingQueueInProgress = false;
            lastFlushTick = Environment.TickCount64;
            flushPending = false;
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
            // First identify which messages needs to be deleted from queue
            ArrayList ackedObjectMessages = new ArrayList();
            lock (messageQueue.ObjectMessagesQueue.SyncRoot)
            {
                foreach (SequencedMessage message in messageQueue.ObjectMessagesQueue)
                {
                    // Only a message that has gone out can be acked. A queued-but-unsent message
                    // still has neededAck 0, so without the sendCounter check any client ack that
                    // arrived before the next flush deleted it unsent. That dropped the self-spawn
                    // about half the time and left the client at "Negotiating with Servers...".
                    if (message.IsAckedBy(ackSeq))
                    {
                        ackedObjectMessages.Add(message);
                    }
                }
            }


            ArrayList ackedRPCMessages = new ArrayList();
            lock (messageQueue.RPCMessagesQueue.SyncRoot)
            {
                HashSet<UInt16> ackedSeqs = SequencedMessage.AckedSequences(ackSeq, playerData.getPss());
                foreach (SequencedMessage message in messageQueue.RPCMessagesQueue)
                {
                    // Gone once the client acks a packet it was in; until then FlushQueue sends it again
                    if (message.rpcNumber >= 0 && message.IsRpcAckedBy(ackedSeqs))
                    {
                        ackedRPCMessages.Add(message);
                    }
                }
            }

            // Now delete them finally
            foreach (SequencedMessage deletedAckObject in ackedObjectMessages)
            {
                lock (messageQueue.ObjectMessagesQueue.SyncRoot)
                {
                    messageQueue.ObjectMessagesQueue.Remove(deletedAckObject);
                }
            }

            foreach (SequencedMessage deletedAckRpc in ackedRPCMessages)
            {
                lock (messageQueue.RPCMessagesQueue.SyncRoot)
                {
                    messageQueue.RPCMessagesQueue.Remove(deletedAckRpc);
                }
            }
        }


        public byte[] DecryptReceivedPacket(byte[] packet)
        {
            byte[] processedPacket;
            byte[] temp;

            ArrayList decValues;

            // Here we check if the first byte is "1" which means that the packet is encrypted or not

            processedPacket = new byte[packet.Length - 1];
            ArrayUtils.copy(packet, 1, processedPacket, 0, packet.Length - 1);
            decValues = new ArrayList();
            decValues = cypher.decrypt(packet, packet.Length);
            if (decValues == null)
            {
                return null; // corrupt packet, already logged
            }
            processedPacket = (byte[]) decValues[0];


            if (processedPacket[0] == 0x82) //It's a timed data packet
            {
                temp = new byte[processedPacket.Length - 4];
                temp[0] = (byte) 0x02;
                ArrayUtils.copy(processedPacket, 5, temp, 1, processedPacket.Length - 4);
                processedPacket = temp;
            }


            //clData.setPss((UInt16) decValues[1]);
            playerData.setPss((UInt16) decValues[1]);
            playerData.setCseq((UInt16) decValues[2]);
            playerData.setACK((UInt16) decValues[3]);

            if (DropTest.Enabled)
            {
                Output.WriteDebugLog("[NET] Client acks sseq " + (UInt16) decValues[3]);
            }

            this.DeletedAckedPackets((UInt16) decValues[3]); // REMOVING FROM ACK QUEUE
            return processedPacket;
        }

        /// <summary>Called by the socket receiver before it arms the next receive, preserving datagram arrival order.</summary>
        internal void QueuePacket(byte[] packet)
        {
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
                processPacket(packet);
            }
        }

        public void processPacket(byte[] packet)
        {
            lock (processLock)
            {
                WorldClient previousClient = Store.currentClient;
                Store.currentClient = this;
                try
                {
                    // Update the last time we are called
                    lastUsedTime = TimeUtils.getUnixTimeUint32();

                    // Decryption start
                    bool encrypted = false;

                    byte[] processedPacket = null;
                    Stopwatch stopwatch = new Stopwatch();
                    if (packet.Length > 0)
                    {
                        if (packet[0] == 0x00)
                        {
                            // Plain text packet
                            processedPacket = packet;
                            Output.WritePacketLog(processedPacket, "CLIENT",
                                playerData.getPss().ToString(), playerData.getCseq().ToString(),
                                playerData.getACK().ToString());
                        }
                        else
                        {
                            encrypted = true;
                            stopwatch.Start();
                            processedPacket = DecryptReceivedPacket(packet);
                            stopwatch.Stop();
                            if (processedPacket == null)
                            {
                                return;
                            }
                            TimeSpan ts = stopwatch.Elapsed;

                            Output.WritePacketLog(this, processedPacket, "CLIENT",
                                playerData.getPss().ToString(), playerData.getCseq().ToString(), playerData.getACK().ToString(),
                                ts.TotalMilliseconds.ToString(), "DECRYPT");
                        }

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


        public bool Alive
        {
            get
            {
                if (IsBot)
                {
                    return alive;
                }
                UInt32 now = TimeUtils.getUnixTimeUint32();
                if (!this.alive || (now - lastUsedTime > 30))
                {
                    return false;
                }
                return true;
            }
            set { alive = value; }
        }
    }
}
