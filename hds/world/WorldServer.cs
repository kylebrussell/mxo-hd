using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using hds.resources.gameobjects;
using hds.shared;

namespace hds
{
    
    public class WorldServer
    {

        private IPEndPoint udplistener;
        private Thread listenThread;
        private int serverport;
        private Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            
        public const int SIO_UDP_CONNRESET = -1744830452;
        public const int DefaultMaxClients = 2048;
        public const int DefaultMaxUnauthenticatedClients = 512;

        public static Dictionary<string, WorldClient> Clients { get; set; }

        // Collection to store the spawned npcs 
        public static ArrayList mobs = ArrayList.Synchronized(new ArrayList());
        public static ArrayList subways = ArrayList.Synchronized(new ArrayList());
        public static UInt64 entityIdCounter = 1;
        public static ArrayList gameServerEntities = ArrayList.Synchronized(new ArrayList()); // should hold all GameObject Entities where a view can be created (static, dynamic etc.)
        private byte[] buffer;
        private volatile bool stopping;
        private long rejectedEndpoints;
        // Single-receive-thread endpoint cache avoiding Remote.ToString() and lock(Clients) on steady-state UDP packets.
        private readonly Dictionary<EndPoint, WorldClient> clientsByEndpoint = new Dictionary<EndPoint, WorldClient>();
        private readonly List<EndPoint> staleEndpointScratch = new List<EndPoint>();
        private Dictionary<string, WorldClient> endpointCacheSource;
        private readonly Dictionary<ulong, WorldClient> clientsByIpv4Endpoint = new Dictionary<ulong, WorldClient>();
        private readonly List<ulong> staleIpv4EndpointScratch = new List<ulong>();
        private long transientReceiveErrors;
        private const int EndpointCachePruneSlack = 64;

        internal static bool TryReadIpv4EndpointKey(SocketAddress address, out ulong key, out uint ipv4Raw, out ushort port)
        {
            if (address == null || address.Family != AddressFamily.InterNetwork || address.Size < 8)
            {
                key = 0;
                ipv4Raw = 0;
                port = 0;
                return false;
            }
            ReadOnlySpan<byte> raw = address.Buffer.Span;
            port = (ushort)((raw[2] << 8) | raw[3]);
            ipv4Raw = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(4, 4));
            key = ((ulong)ipv4Raw << 16) | port;
            return true;
        }

        internal static bool IsTransientReceiveError(SocketError error)
        {
            return error == SocketError.ConnectionReset ||
                   error == SocketError.MessageSize ||
                   error == SocketError.Interrupted ||
                   error == SocketError.WouldBlock ||
                   error == SocketError.TryAgain ||
                   error == SocketError.NoBufferSpaceAvailable;
        }

        /// <summary>Thread-safe entity ID allocator shared across UDP ingress, combat, missions, pets, and loaders.</summary>
        public static UInt64 NextEntityId()
        {
            return Interlocked.Increment(ref entityIdCounter) - 1;
        }

        internal static bool CanAdmitNewEndpointLocked(
            Dictionary<string, WorldClient> registry,
            int maxClients = DefaultMaxClients,
            int maxUnauthenticated = DefaultMaxUnauthenticatedClients)
        {
            if (registry == null) return false;
            if (registry.Count >= maxClients) return false;
            if (registry.Count < maxUnauthenticated) return true;

            int unauthenticated = 0;
            foreach (WorldClient existing in registry.Values)
            {
                if (existing != null && (existing.playerData == null || existing.playerData.getCharID() == 0))
                {
                    unauthenticated++;
                    if (unauthenticated >= maxUnauthenticated)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal int EndpointCacheCount => (clientsByEndpoint?.Count ?? 0) + (clientsByIpv4Endpoint?.Count ?? 0);

        internal static bool HasActiveCharacter(UInt32 charId)
        {
            if (charId == 0) return false;
            Dictionary<string, WorldClient> clients = Clients;
            if (clients == null) return false;
            lock (clients)
            {
                foreach (WorldClient client in clients.Values)
                {
                    if (client != null && client.playerData != null && client.playerData.getCharID() == charId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal void PruneStaleEndpointCache()
        {
            if (clientsByEndpoint != null && staleEndpointScratch != null)
            {
            staleEndpointScratch.Clear();
            foreach (KeyValuePair<EndPoint, WorldClient> kv in clientsByEndpoint)
            {
                WorldClient c = kv.Value;
                if (c == null || !c.Alive || c.IngressSnapshot.Claimed)
                {
                    staleEndpointScratch.Add(kv.Key);
                }
            }
            for (int i = 0; i < staleEndpointScratch.Count; i++)
            {
                clientsByEndpoint.Remove(staleEndpointScratch[i]);
            }
            staleEndpointScratch.Clear();
            }

            if (clientsByIpv4Endpoint != null && staleIpv4EndpointScratch != null)
            {
            staleIpv4EndpointScratch.Clear();
            foreach (KeyValuePair<ulong, WorldClient> kv in clientsByIpv4Endpoint)
            {
                WorldClient c = kv.Value;
                if (c == null || !c.Alive || c.IngressSnapshot.Claimed)
                {
                    staleIpv4EndpointScratch.Add(kv.Key);
                }
            }
            for (int i = 0; i < staleIpv4EndpointScratch.Count; i++)
            {
                clientsByIpv4Endpoint.Remove(staleIpv4EndpointScratch[i]);
            }
            staleIpv4EndpointScratch.Clear();
            }
        }
        
        public ObjectManager objMan { get; set; }


        public WorldServer()
        {

            Clients = new Dictionary<string, WorldClient>();
            objMan = new ObjectManager();

            listenThread = new Thread(ListenForAllClients);
            Output.WriteLine("[World Server] Set and ready at UDP port " + NetworkBind.WorldPort);
        }

        public void StartServer()
        {
            listenThread.Start();
        }

        public void StopServer()
        {
            stopping = true;
            try
            {
                socket?.Close();
            }
            catch
            {
            }
        }
        
        
        /// <summary>
        /// Receives datagrams synchronously on the dedicated World listener thread in arrival order using
        /// a reusable SocketAddress and value-type IPv4 endpoint cache (zero per-datagram IPEndPoint,
        /// SocketAsyncResult, or Remote.ToString() allocations on steady-state traffic).
        /// </summary>
        private void ReceiveLoop()
        {
            SocketAddress receivedAddress = new SocketAddress(socket.AddressFamily);
            while (!stopping)
            {
                int msgLen;
                try
                {
                    msgLen = socket.ReceiveFrom(buffer.AsSpan(), SocketFlags.None, receivedAddress);
                }
                catch (SocketException ex)
                {
                    if (stopping)
                    {
                        return;
                    }
                    if (IsTransientReceiveError(ex.SocketErrorCode))
                    {
                        long count = Interlocked.Increment(ref transientReceiveErrors);
                        if (count == 1 || (count & (count - 1)) == 0)
                        {
                            Output.WriteLine("[WORLD] Transient UDP receive error: " + ex.SocketErrorCode +
                                             " (count=" + count + "): " + ex.Message);
                        }
                        continue;
                    }
                    Output.WriteLine("[WORLD] Receive failed: " + ex.SocketErrorCode + " " + ex.Message);
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return; // The socket closed while a receive was waiting.
                }

                if (msgLen <= 0)
                {
                    continue;
                }

                DispatchDatagram(receivedAddress, msgLen);
            }
        }

        private WorldClient ResolveOrAdmitClient(Dictionary<string, WorldClient> registry, EndPoint remote, out int activeCount)
        {
            WorldClient currentClient = null;
            string remoteKey = remote.ToString();
            lock (registry)
            {
                if (!registry.TryGetValue(remoteKey, out currentClient))
                {
                    if (!CanAdmitNewEndpointLocked(registry))
                    {
                        long dropped = Interlocked.Increment(ref rejectedEndpoints);
                        if (dropped == 1 || (dropped & (dropped - 1)) == 0)
                        {
                            Output.WriteLine("[WORLD] Dropped unknown UDP endpoint " + remoteKey +
                                             " (client/unauth cap reached, total rejected=" + dropped + ")");
                        }
                        activeCount = registry.Count;
                    }
                    else
                    {
                        objMan.PushClient(remoteKey); // Push first, then create it
                        currentClient = new WorldClient(remote, socket, remoteKey);
                        gameServerEntities.Add(objMan.GetAssignedObject(remoteKey));
                        currentClient.playerData.setEntityId(NextEntityId());

                        registry.Add(remoteKey, currentClient);
                        activeCount = registry.Count;
                    }
                }
                else
                {
                    activeCount = registry.Count;
                }
            }
            return currentClient;
        }

        private void DispatchDatagram(SocketAddress receivedAddress, int msgLen)
        {
            WorldClient currentClient = null;
            byte[] pooledMessage = null;
            bool handedOff = false;

            try
            {
                pooledMessage = ArrayPool<byte>.Shared.Rent(msgLen);
                Buffer.BlockCopy(buffer, 0, pooledMessage, 0, msgLen);

                // Fast path on the single receive thread: avoid IPEndPoint/string allocation and lock(Clients)
                // while the client is active and still belongs to the same Clients dictionary.
                Dictionary<string, WorldClient> registry = Clients;
                if (!ReferenceEquals(endpointCacheSource, registry))
                {
                    clientsByEndpoint.Clear();
                    clientsByIpv4Endpoint.Clear();
                    endpointCacheSource = registry;
                }

                if (TryReadIpv4EndpointKey(receivedAddress, out ulong ipv4Key, out uint ipv4Raw, out ushort port))
                {
                    if (!clientsByIpv4Endpoint.TryGetValue(ipv4Key, out currentClient) ||
                        currentClient == null || !currentClient.Alive || currentClient.IngressSnapshot.Claimed)
                    {
                        if (currentClient != null)
                        {
                            clientsByIpv4Endpoint.Remove(ipv4Key);
                        }
                        EndPoint remote = new IPEndPoint(new IPAddress((long)ipv4Raw), port);
                        currentClient = ResolveOrAdmitClient(registry, remote, out int activeCount);
                        if (currentClient != null)
                        {
                            clientsByIpv4Endpoint[ipv4Key] = currentClient;
                        }
                        if (EndpointCacheCount > activeCount + EndpointCachePruneSlack)
                        {
                            PruneStaleEndpointCache();
                        }
                    }
                }
                else
                {
                    EndPoint remote = udplistener.Create(receivedAddress);
                    if (!clientsByEndpoint.TryGetValue(remote, out currentClient) ||
                        currentClient == null || !currentClient.Alive || currentClient.IngressSnapshot.Claimed)
                    {
                        if (currentClient != null)
                        {
                            clientsByEndpoint.Remove(remote);
                        }
                        currentClient = ResolveOrAdmitClient(registry, remote, out int activeCount);
                        if (currentClient != null)
                        {
                            clientsByEndpoint[remote] = currentClient;
                        }
                        if (EndpointCacheCount > activeCount + EndpointCachePruneSlack)
                        {
                            PruneStaleEndpointCache();
                        }
                    }
                }

                if (currentClient != null)
                {
                    currentClient.QueuePooledPacket(pooledMessage, msgLen);
                    handedOff = true;
                }
            }
            catch (Exception ex)
            {
                // One bad datagram must not stop the receive loop.
                Output.WriteLine("[WORLD] Datagram handling failed: " + ex.GetType().Name + " " + ex.Message);
            }
            finally
            {
                if (!handedOff && pooledMessage != null)
                {
                    ArrayPool<byte>.Shared.Return(pooledMessage);
                }
            }
        }

        private void ListenForAllClients()
        {
            // SIO_UDP_CONNRESET with FALSE (0) stops Windows from surfacing ICMP "port unreachable"
            // as WSAECONNRESET (10054) receive errors. Windows-only ioctl.
            if (OperatingSystem.IsWindows())
            {
                socket.IOControl(SIO_UDP_CONNRESET, new byte[4], null);
            }

            serverport = NetworkBind.WorldPort;
            udplistener = new IPEndPoint(NetworkBind.Address, serverport);
            buffer = new byte[4096];
            try
            {
                // Larger OS UDP rings prevent silent kernel drops under 200+ dense client bursts.
                socket.ReceiveBufferSize = 1024 * 1024;
                socket.SendBufferSize = 1024 * 1024;
            }
            catch (SocketException)
            {
                // Fall back to OS defaults if container/host caps socket buffer sizes.
            }
            socket.Bind(udplistener);

            try
            {
                ReceiveLoop();
            }
            catch (Exception ex)
            {
                Output.WriteDebugLog("Exception Thrown ListenForAllClients " + ex.Message);
            }
        }

        internal static WorldClient FindClientByCharId(UInt32 charId) => FindClientByCharId(Clients, charId);

        internal static WorldClient FindClientByCharId(Dictionary<string, WorldClient> clients, UInt32 charId)
        {
            if (charId == 0 || clients == null) return null;
            lock (clients)
            {
                foreach (WorldClient client in clients.Values)
                {
                    if (client != null && client.playerData != null && client.playerData.getCharID() == charId)
                    {
                        return client;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Send a Message to One Player (like private Message)
        /// </summary>
        /// <param name="charId">CharId to send the Packet</param>
        /// <param name="packet">Packet Stream</param>
        public void SendRPCToOnePlayer(UInt32 charId, byte[] packet)
        {
            WorldClient client = FindClientByCharId(charId);
            if (client != null)
            {
                client.messageQueue.addRpcMessage(packet);
                client.FlushQueue();
            }
        }

        /// <summary>
        /// Send a Message to One Player (like private Message by Handle)
        /// </summary>
        /// <param name="packet">Packet Stream</param>
        /// <param name="playerHandle">Player Handle to send the Packet</param>
        public void SendRPCToOnePlayerByHandle(byte[] packet, string playerHandle)
        {
            foreach (WorldClient client in ClientSnapshot())
            {
                if (client == null) continue;
                string charname = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
                Output.writeToLogForConsole("Charname |" + charname + "| PlayerHandle |" + playerHandle + "|");
                if (charname == playerHandle)
                {
                    client.messageQueue.addRpcMessage(packet);
                    client.FlushQueue();
                }
            }
        }


        /// <summary>
        /// Sends a RPC Message to all Players but not myself
        /// NEEDS TESTING
        /// </summary>
        /// <param name="myself"></param>
        /// <param name="data"></param>
        public void SendRPCToAllOtherPlayers(ClientData myself, byte[] data)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            foreach (WorldClient client in ClientSnapshot())
            {
                if (client != null && client.playerData.getCharID() != myself.getCharID())
                {
                    // create the RPC Message
                    client.messageQueue.addRpcMessage(data);
                    client.FlushQueue();
                }
            }
        }

        public void SendRPCToCrewMembers(UInt32 groupId, WorldClient myself, byte[] data, bool ShouldSendToMyself)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            foreach (WorldClient client in ClientSnapshot())
            {
                if (client == null) continue;
                if (NumericalUtils.ByteArrayToUint32(client.playerInstance.CrewID.getValue(), 1) == groupId)
                {
                    if (client.playerData.getCharID() != myself.playerData.getCharID() || ShouldSendToMyself)
                    {
                        // create the RPC Message
                        client.messageQueue.addRpcMessage(data);
                        client.FlushQueue();
                    }
                }
            }
        }
        
        public void SendRPCToFactionMembers(UInt32 groupId, WorldClient myself, byte[] data, bool ShouldSendToMyself)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            foreach (WorldClient client in ClientSnapshot())
            {
                if (client == null) continue;
                if (NumericalUtils.ByteArrayToUint32(client.playerInstance.FactionID.getValue(), 1) == groupId)
                {
                    if (client.playerData.getCharID() != myself.playerData.getCharID() || ShouldSendToMyself)
                    {
                        // create the RPC Message
                        client.messageQueue.addRpcMessage(data);
                        client.FlushQueue();
                    }
                }
            }
        }
        
        public void SendRPCToMissionTeamMembers(UInt32 groupId, WorldClient myself, byte[] data, bool ShouldSendToMyself)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            foreach (WorldClient client in ClientSnapshot())
            {
                if (client == null) continue;
                if (NumericalUtils.ByteArrayToUint32(client.playerInstance.MissionTeamID.getValue(), 1) == groupId)
                {
                    if (client.playerData.getCharID() != myself.playerData.getCharID() || ShouldSendToMyself)
                    {
                        // create the RPC Message
                        client.messageQueue.addRpcMessage(data);
                        client.FlushQueue();
                    }
                }
            }
        }

        public void SendRPCToPlayerList(ArrayList players, byte[] data)
        {
            List<string> handles = new List<string>();
            foreach (Hashtable friend in players)
            {
                handles.Add(friend["handle"].ToString());
            }

            foreach (WorldClient client in ClientSnapshot())
            {
                if (client == null) continue;
                string handleRecipient =
                    StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
                if (handles.Contains(handleRecipient))
                {
                    // create the RPC Message
                    client.messageQueue.addRpcMessage(data);
                    client.FlushQueue();
                }
            }
        }
        

        /// <summary>
        /// Sends a RPC Message to all connected players including the sender
        /// </summary>
        /// <param name="data"></param>
        public void sendRPCToAllPlayers(byte[] data)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            foreach (WorldClient client in ClientSnapshot())
            {
                if (client == null) continue;
                client.messageQueue.addRpcMessage(data);
                client.FlushQueue();
            }
        }

        /// <summary>
        /// The connected clients at this moment. Broadcasts iterate this copy instead of holding the Clients lock
        /// while they build, queue and flush per viewer: under 200 moving players the per-movement broadcast held it
        /// for the whole fan-out and the receive and visibility threads waited on it.
        /// </summary>
        internal static WorldClient[] ClientSnapshot()
        {
            lock (Clients)
            {
                WorldClient[] copy = new WorldClient[Clients.Count];
                Clients.Values.CopyTo(copy, 0);
                return copy;
            }
        }

        // Re-entrancy-safe thread-local broadcast snapshot scratch avoiding new WorldClient[N] array allocations
        // on movement, NPC, and static-object fan-outs.
        [ThreadStatic] private static List<WorldClient> broadcastScratch;

        internal static List<WorldClient> RentClientSnapshot() => RentClientSnapshot(Clients);

        internal static List<WorldClient> RentClientSnapshot(Dictionary<string, WorldClient> clients)
        {
            List<WorldClient> recipients = broadcastScratch;
            broadcastScratch = null;
            if (recipients == null)
            {
                recipients = new List<WorldClient>(256);
            }
            else
            {
                recipients.Clear();
            }

            if (clients != null)
            {
                lock (clients)
                {
                    if (recipients.Capacity < clients.Count) recipients.Capacity = clients.Count;
                    foreach (WorldClient c in clients.Values)
                    {
                        recipients.Add(c);
                    }
                }
            }

            return recipients;
        }

        internal static void ReturnClientSnapshot(List<WorldClient> recipients)
        {
            if (recipients == null) return;
            recipients.Clear();
            if (broadcastScratch == null)
            {
                broadcastScratch = recipients;
            }
        }

        /// <summary>
        /// This sends a View Create/Update/Delete Packet to all Players 
        /// </summary>
        /// <param name="data">Packet Stream without ViewID</param>
        /// <param name="charId">from charId</param>
        /// <param name="goId">from GoId</param>
        /// <summary>
        /// Forwards a player's own-view update (<c>01 &lt;flags&gt; &lt;fields&gt;</c>) to the players who already
        /// see that player. Players who don't are left to ViewVisibleThread, which spawns (and deletes) views
        /// by range and district.
        /// </summary>
        public void SendViewPacketToAllPlayers(byte[] data,UInt32 charId, UInt32 goId, UInt64 entityId, byte[] followingUpdate = null)
        {
            List<WorldClient> recipients = RentClientSnapshot();
            try
            {
                int count = recipients.Count;
                for (int i = 0; i < count; i++)
                {
                    WorldClient client = recipients[i];
                    if (client == null || client.playerData.getCharID() == charId)
                    {
                        continue;
                    }

                    // This used to create a view for every connected client (any district, any distance) and,
                    // when the view didn't exist yet, send the movement bytes as if they were a spawn; it also
                    // added stray 00 bytes after the update and after deletes, which made the client misread the
                    // rest of the packet.
                    ClientView view = client.viewMan.FindViewForEntity(entityId);
                    if (view == null || !view.viewCreated)
                    {
                        continue;
                    }

                    byte[] update = followingUpdate != null
                        ? BuildCombinedViewUpdate(view.ViewID, data, followingUpdate)
                        : BuildViewUpdate(view.ViewID, data);
                    client.messageQueue.addObjectMessage(update, false);
                    client.FlushQueue();
                }
            }
            finally
            {
                ReturnClientSnapshot(recipients);
            }
        }

        /// <summary>
        /// Forwards a player's own-view movement update (<c>01 &lt;flags&gt; &lt;fields&gt;</c>) to every viewer that
        /// currently sees them, coalescing unsent updates per viewer/entity in place and deferring UDP send to the
        /// viewer's next flush tick so mover threads do not contend on other clients' flush/fence locks.
        /// </summary>
        internal void SendMovementUpdateToAllPlayers(SelfViewUpdate state, byte[] forwardedData, UInt32 charId, UInt64 entityId)
        {
            SendMovementUpdateToAllPlayers(state, charId, entityId);
        }

        internal void SendMovementUpdateToAllPlayers(SelfViewUpdate state, UInt32 charId, UInt64 entityId)
        {
            List<WorldClient> recipients = RentClientSnapshot();
            try
            {
                int count = recipients.Count;
                for (int i = 0; i < count; i++)
                {
                    WorldClient client = recipients[i];
                    if (client == null || client.IsBot || client.playerData.getCharID() == charId)
                    {
                        continue;
                    }

                    ClientView view = client.viewMan.FindViewForEntity(entityId);
                    if (view == null || !view.viewCreated)
                    {
                        continue;
                    }

                    client.messageQueue.AddOrCoalesceMovement(view.ViewID, state);
                    client.MarkFlushPending();
                }
            }
            finally
            {
                ReturnClientSnapshot(recipients);
            }
        }

        /// <summary>One view-list entry: the view id followed directly by the update.</summary>
        public static byte[] BuildViewUpdate(UInt16 viewId, byte[] data)
        {
            return MessageQueue.BuildViewUpdateFast(viewId, data);
        }

        /// <summary>Two back-to-back view-list entries for the same view in a single allocation.</summary>
        internal static byte[] BuildCombinedViewUpdate(UInt16 viewId, byte[] first, byte[] second)
        {
            int firstLen = first != null ? first.Length : 0;
            int secondLen = second != null ? second.Length : 0;
            byte[] result = new byte[4 + firstLen + secondLen];
            result[0] = (byte)(viewId & 0xFF);
            result[1] = (byte)(viewId >> 8);
            if (firstLen > 0)
            {
                Buffer.BlockCopy(first, 0, result, 2, firstLen);
            }
            int secondOffset = 2 + firstLen;
            result[secondOffset] = (byte)(viewId & 0xFF);
            result[secondOffset + 1] = (byte)(viewId >> 8);
            if (secondLen > 0)
            {
                Buffer.BlockCopy(second, 0, result, secondOffset + 2, secondLen);
            }
            return result;
        }

        public void SendSelfViewUpdate(PacketContent pak, UInt16 viewId, WorldClient client, bool isTimed)
        {
            ServerPackets serverPackets = new ServerPackets();
            serverPackets.SendUpdateViewStatePacket(client, viewId, pak.ReturnFinalPacket());
        }

        public void SendSelfViewUpdateToTarget(PacketContent pak, UInt16 targetViewId, WorldClient currentClient)
        {

            ClientView theView = currentClient.viewMan.getViewById(targetViewId);
            if (theView == null)
            {
                return;
            }
            
            if (theView.GoID != 12)
            {
                // it is no player - so we do nothing
                return;
            }

            foreach (WorldClient target in ClientSnapshot())
            {
                if (target.playerData.getEntityId() == theView.entityId)
                {
                    Output.WriteDebugLog("Update SelfViewState from Ability FX on OtherState Views from " + StringUtils.charBytesToString(currentClient.playerInstance.CharacterName.getValue()) + " to  " + StringUtils.charBytesToString(target.playerInstance.CharacterName.getValue()) + " HEX: " + StringUtils.bytesToString_NS(pak.ReturnFinalPacket()));
                    target.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
                    target.FlushQueue();
                    return;
                }
            }
        }

        public void SendViewUpdateToClientWhoHasStaticObjectSpawned(PacketContent packet, StaticWorldObject worldObject, UInt64 entityStaticId)
        {
            byte[] updateData = packet.ReturnFinalPacket();
            ServerPackets pak = null;
            List<WorldClient> recipients = RentClientSnapshot();
            try
            {
                int count = recipients.Count;
                for (int i = 0; i < count; i++)
                {
                    WorldClient client = recipients[i];
                    if (client == null) continue;
                    // Find, not GetViewForEntityAndGo: that would add an empty view to every player for every car
                    ClientView objectView = client.viewMan.FindViewForEntity(entityStaticId);
                    if (objectView != null && objectView.viewCreated && worldObject.metrId == client.playerData.getDistrictId() && client.playerData.getOnWorld())
                    {
                        pak ??= new ServerPackets();
                        pak.SendUpdateViewStatePacket(client, objectView.ViewID, updateData);
                    }
                }
            }
            finally
            {
                ReturnClientSnapshot(recipients);
            }
        }

        /// <summary>
        /// Deletes an NPC's view from every client that has it. For NPCs taken out of the mob list (mission NPCs
        /// when the mission ends): the visibility thread only deletes views of NPCs it still finds in that list.
        /// </summary>
        public void DeleteMobViews(Mob mob)
        {
            ulong mobEntityId = mob.getEntityId();
            ServerPackets pak = null;
            List<WorldClient> recipients = RentClientSnapshot();
            try
            {
                int count = recipients.Count;
                for (int i = 0; i < count; i++)
                {
                    WorldClient client = recipients[i];
                    ClientView mobView = client?.viewMan.FindViewForEntity(mobEntityId);
                    if (mobView == null)
                    {
                        continue;
                    }

                    client.viewMan.removeViewByViewId(mobView.ViewID);
                    if (mobView.viewCreated)
                    {
                        pak ??= new ServerPackets();
                        pak.sendDeleteViewPacket(client, mobView.ViewID);
                    }
                }
            }
            finally
            {
                ReturnClientSnapshot(recipients);
            }
        }

        public void SendViewUpdateToClientsWhoHasMobSpawned(PacketContent packet, Mob mob, byte[] followingUpdate = null, bool forceFlush = false, bool deferFlush = false)
        {
            byte[] updateData = packet.ReturnFinalPacket();
            ulong mobEntityId = mob.getEntityId();
            ushort mobDistrict = mob.getDistrict();
            List<WorldClient> recipients = RentClientSnapshot();
            try
            {
                int count = recipients.Count;
                for (int i = 0; i < count; i++)
                {
                    WorldClient client = recipients[i];
                    if (client == null || client.IsBot) continue;
                    // Look up only: GetViewForEntityAndGo would hand every client without the NPC a view id it never
                    // spawns, and the client's view ids would then run one behind the server's.
                    ClientView mobView = client.viewMan.FindViewForEntity(mobEntityId);
                    if (mobView != null && mobView.viewCreated && mobDistrict == client.playerData.getDistrictId() && client.playerData.getOnWorld())
                    {
                        ServerPackets.SendNpcUpdateDataFast(mobView.ViewID, client, updateData, followingUpdate, forceFlush, deferFlush);
                    }
                }
            }
            finally
            {
                ReturnClientSnapshot(recipients);
            }
        }


    }
}
