using System;
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

        public static Dictionary<string, WorldClient> Clients { get; set; }

        // Collection to store the spawned npcs 
        public static ArrayList mobs = ArrayList.Synchronized(new ArrayList());
        public static ArrayList subways = ArrayList.Synchronized(new ArrayList());
        public static UInt64 entityIdCounter = 1;
        public static ArrayList gameServerEntities = ArrayList.Synchronized(new ArrayList()); // should hold all GameObject Entities where a view can be created (static, dynamic etc.)
        private byte[] buffer;
        
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
        }
        
        
        private void finalReceiveFrom(IAsyncResult iar)
        {
            
            Socket recvSocket = (Socket)iar.AsyncState;
            EndPoint Remote = new IPEndPoint(IPAddress.Any, 0);
            
            int msgLen = 0;
            WorldClient currentClient = null;

            try
            {
                msgLen = recvSocket.EndReceiveFrom(iar, ref Remote);
                byte[] finalMessage = new byte[msgLen];
                ArrayUtils.fastCopy(buffer, finalMessage, msgLen);

                // TODO CLIENT CHECK AND HANDLING
                string remoteKey = Remote.ToString();
                lock (Clients)
                {
                    if (Clients.TryGetValue(remoteKey, out currentClient))
                    {
                    }
                    else
                    {

                        objMan.PushClient(remoteKey); // Push first, then create it
                        currentClient = new WorldClient(Remote, socket, remoteKey);
                        gameServerEntities.Add(objMan.GetAssignedObject(remoteKey));
                        currentClient.playerData.setEntityId(entityIdCounter++);

                        Clients.Add(remoteKey, currentClient);
                    }
                }

                if (currentClient != null)
                {
                    currentClient.QueuePacket(finalMessage);
                }

            }
            catch (SocketException ex)
            {
                Output.WriteLine("Exception thrown by socket " + ex.SocketErrorCode + " " + ex.Message);
                if (ex.ErrorCode == 10054)
                {
                    // Client got removed by timeout
                    if (currentClient != null)
                    {
                        currentClient.Alive = false;
                    }
                }
            }
            finally
            {
                // Queue this datagram before receiving another. Concurrent callbacks racing for processLock can reorder RPCs.
                EndPoint nextClient = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref nextClient,
                        finalReceiveFrom, socket);
                }
                catch (SocketException ex)
                {
                    Output.WriteLine("[WORLD] Receive failed: " + ex.SocketErrorCode + " " + ex.Message);
                }
                catch (ObjectDisposedException)
                {
                    // The socket closed while this receive callback was finishing.
                }
            }

        }

        
        private void ListenForAllClients()
        {
            // SIO_UDP_CONNRESET stops Windows from surfacing ICMP "port unreachable" as receive
            // errors. It is a Windows-only ioctl: on macOS/Linux it throws and the unhandled
            // exception kills the whole server process (auth included), so only apply it there.
            if (OperatingSystem.IsWindows())
            {
                byte[] byteTrue = new byte[4];
                byteTrue[byteTrue.Length - 1] = 1;
                socket.IOControl(SIO_UDP_CONNRESET, byteTrue, null);
            }

            serverport = NetworkBind.WorldPort;
            udplistener = new IPEndPoint(NetworkBind.Address, serverport);
            buffer = new byte[4096];
            socket.Bind(udplistener);
    
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint Remote = sender;
            try
            {
                socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref Remote, new AsyncCallback(finalReceiveFrom), socket);
            }catch(Exception ex)
            {
                Output.WriteDebugLog("Exception Thrown ListenForAllClients " + ex.Message);
            }
        }
        

        /// <summary>
        /// Send a Message to One Player (like private Message)
        /// </summary>
        /// <param name="charId">CharId to send the Packet</param>
        /// <param name="packet">Packet Stream</param>
        public void SendRPCToOnePlayer(UInt32 charId, byte[] packet)
        {
            lock (Clients)
            {
                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient client = Clients[clientKey] as WorldClient;
                    if (client.playerData.getCharID() == charId)
                    {
                        client.messageQueue.addRpcMessage(packet);
                        client.FlushQueue();
                    }
                }
            }
        }

        /// <summary>
        /// Send a Message to One Player (like private Message by Handle)
        /// </summary>
        /// <param name="packet">Packet Stream</param>
        /// <param name="playerHandle">Player Handle to send the Packet</param>
        public void SendRPCToOnePlayerByHandle(byte[] packet, string playerHandle)
        {
            lock (Clients)
            {

                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient client = Clients[clientKey] as WorldClient;
                    string charname = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
                    Output.writeToLogForConsole("Charname |" + charname + "| PlayerHandle |" + playerHandle + "|");
                    int lenCharname = charname.Length;
                    int playerHandleLen = playerHandle.Length;
                    if (charname == playerHandle)
                    {
                        client.messageQueue.addRpcMessage(packet);
                        client.FlushQueue();
                    }
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
            lock(Clients){
                foreach (string clientKey in Clients.Keys)
                {
                    // Populate a message to all players 
                    // WE TEST THIS HERE!
                    WorldClient client = Clients[clientKey] as WorldClient;
                    if (client.playerData.getCharID() != myself.getCharID())
                    {

                        // create the RPC Message
                        client.messageQueue.addRpcMessage(data);
                        client.FlushQueue();
                    }

                }
            }
        }

        public void SendRPCToCrewMembers(UInt32 groupId, WorldClient myself, byte[] data, bool ShouldSendToMyself)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            lock(Clients){
                foreach (string clientKey in Clients.Keys)
                {
                    // Populate a message to all players to my crew
                    WorldClient client = Clients[clientKey] as WorldClient;
                    if (NumericalUtils.ByteArrayToUint32(client.playerInstance.CrewID.getValue(),1) == groupId)
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
        }
        
        public void SendRPCToFactionMembers(UInt32 groupId, WorldClient myself, byte[] data, bool ShouldSendToMyself)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            lock (Clients)
            {
                var clients = from clientCollection in Clients
                    where NumericalUtils.ByteArrayToUint32(clientCollection.Value.playerInstance.FactionID.getValue(),1) == groupId
                    select clientCollection;

                foreach (KeyValuePair<string, WorldClient> theClient in clients)
                {
                    WorldClient client = theClient.Value;

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
            lock (Clients)
            {
                var clients = from clientCollection in Clients
                    where NumericalUtils.ByteArrayToUint32(
                        clientCollection.Value.playerInstance.MissionTeamID.getValue(), 1) == groupId
                    select clientCollection;

                foreach (KeyValuePair<string, WorldClient> theClient in clients)
                {
                    WorldClient client = theClient.Value;
                    // Populate a message to all players to my crew

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

            lock (Clients)
            {
                foreach (string clientKey in Clients.Keys)
                {
                    // Populate a message to all players to my crew
                    WorldClient client = Clients[clientKey] as WorldClient;

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
        }
        

        /// <summary>
        /// Sends a RPC Message to all connected players including the sender
        /// </summary>
        /// <param name="data"></param>
        public void sendRPCToAllPlayers(byte[] data)
        {
            // Send a global message to all connected Players (like shut down Server announce or something)
            lock (Clients)
            {
                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient client = Clients[clientKey];
                    client.messageQueue.addRpcMessage(data);
                    client.FlushQueue();
                }
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
        public void SendViewPacketToAllPlayers(byte[] data,UInt32 charId, UInt32 goId, UInt64 entityId)
        {
            lock (Clients)
            {
                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient client = Clients[clientKey] as WorldClient;
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

                    client.messageQueue.addObjectMessage(BuildViewUpdate(view.ViewID, data), false);
                    client.FlushQueue();
                }
            }
        }

        /// <summary>One view-list entry: the view id followed directly by the update.</summary>
        public static byte[] BuildViewUpdate(UInt16 viewId, byte[] data)
        {
            PacketContent viewPacket = new PacketContent();
            viewPacket.AddUint16(viewId, 1);
            viewPacket.AddByteArray(data);
            return viewPacket.ReturnFinalPacket();
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

            lock (Clients)
            {
                
                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient target = Clients[clientKey];
                    if (target.playerData.getEntityId() == theView.entityId)
                    {
                        Output.WriteDebugLog("Update SelfViewState from Ability FX on OtherState Views from " + StringUtils.charBytesToString(currentClient.playerInstance.CharacterName.getValue()) + " to  " + StringUtils.charBytesToString(target.playerInstance.CharacterName.getValue()) + " HEX: " + StringUtils.bytesToString_NS(pak.ReturnFinalPacket()));
                        target.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
                        target.FlushQueue();
                        return;
                    }
                }
            }
        }

        public void SendViewUpdateToClientWhoHasStaticObjectSpawned(PacketContent packet, StaticWorldObject worldObject, UInt64 entityStaticId)
        {
            lock (Clients)
            {
                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient client = Clients[clientKey] as WorldClient;
                    // Find, not GetViewForEntityAndGo: that would add an empty view to every player for every car
                    ClientView objectView = client.viewMan.FindViewForEntity(entityStaticId);
                    if (objectView != null && objectView.viewCreated && worldObject.metrId == client.playerData.getDistrictId() && client.playerData.getOnWorld())
                    {
                        ServerPackets pak = new ServerPackets();
                        pak.SendUpdateViewStatePacket(client, objectView.ViewID, packet.ReturnFinalPacket());
                    }
                }
            }
        }

        /// <summary>
        /// Deletes an NPC's view from every client that has it. For NPCs taken out of the mob list (mission NPCs
        /// when the mission ends): the visibility thread only deletes views of NPCs it still finds in that list.
        /// </summary>
        public void DeleteMobViews(Mob mob)
        {
            lock (Clients)
            {
                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient client = Clients[clientKey] as WorldClient;
                    ClientView mobView = client?.viewMan.FindViewForEntity(mob.getEntityId());
                    if (mobView == null)
                    {
                        continue;
                    }

                    client.viewMan.removeViewByViewId(mobView.ViewID);
                    if (mobView.viewCreated)
                    {
                        new ServerPackets().sendDeleteViewPacket(client, mobView.ViewID);
                    }
                }
            }
        }

        public void SendViewUpdateToClientsWhoHasMobSpawned(PacketContent packet, Mob mob)
        {
            lock (Clients)
            {
                foreach (string clientKey in Clients.Keys)
                {
                    WorldClient client = Clients[clientKey] as WorldClient;
                    // Look up only: GetViewForEntityAndGo would hand every client without the NPC a view id it never
                    // spawns, and the client's view ids would then run one behind the server's.
                    ClientView mobView = client.viewMan.FindViewForEntity(mob.getEntityId());
                    if (mobView != null && mobView.viewCreated && mob.getDistrict() == client.playerData.getDistrictId() && client.playerData.getOnWorld())
                    {
                        ServerPackets pak = new ServerPackets();
                        pak.SendNpcUpdateData(mobView.ViewID, client, packet.ReturnFinalPacket());
                    }
                }
            }
        }


    }
}
