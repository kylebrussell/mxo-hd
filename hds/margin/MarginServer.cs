/// <summary>
/// Hardline Dreams Team - 2010
/// Created by Neo
/// Modified by Morpheus
/// </summary>

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Collections;


namespace hds
{
    public class MarginServer
    {
        private readonly object listLock = new object();
        List<MarginClient> clientList;
        List<Thread> threadList;
        private Func<UInt32, bool> activeWorldCharacterCheck;

        private TcpListener tcpListener;
        private Thread listenThread;
        private bool mainThreadWorking;

        public MarginServer()
        {
            // Threads storage
            mainThreadWorking = true;
            clientList = new List<MarginClient>();
            threadList = new List<Thread>();
            activeWorldCharacterCheck = WorldServer.HasActiveCharacter;
            tcpListener = new TcpListener(NetworkBind.Address, NetworkBind.MarginPort);
            Output.WriteLine("Margin server set and ready at port " + NetworkBind.MarginPort);
            listenThread = new Thread(new ThreadStart(ListenForClients));
        }

        internal void SetActiveWorldCharacterCheckForTesting(Func<UInt32, bool> check)
        {
            activeWorldCharacterCheck = check ?? WorldServer.HasActiveCharacter;
        }

        public void StartServer()
        {
            listenThread.Start();
        }

        public void StopServer()
        {
            mainThreadWorking = false;
            MarginClient[] clientsSnapshot;
            Thread[] threadsSnapshot;
            lock (listLock)
            {
                clientsSnapshot = clientList.ToArray();
                threadsSnapshot = threadList.ToArray();
            }

            for (int i = 0; i < clientsSnapshot.Length; i++)
            {
                MarginClient temp = clientsSnapshot[i];
                try
                {
                    temp?.forceClose();
                }
                catch
                {
                }
            }

            tcpListener.Stop();
            for (int i = 0; i < threadsSnapshot.Length; i++)
            {
                Thread tempThread = threadsSnapshot[i];
                if (tempThread != null && tempThread.IsAlive)
                {
                    Output.WriteLine("Margin thread " + i + " is alive, closing it");
                    tempThread.Interrupt();
                }
            }
        }


        private void ListenForClients()
        {
            this.tcpListener.Start(hds.auth.AuthServer.DefaultAcceptorBacklog);

            while (mainThreadWorking)
            {
                // Create a new object when a client arrives
                TcpClient client = null;
                try
                {
                    client = this.tcpListener.AcceptTcpClient();
                }
                catch (SocketException ex)
                {
                    if (!mainThreadWorking)
                    {
                        break;
                    }

                    Output.WriteLine("Margin listener socket error: " + ex.Message);
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    if (!mainThreadWorking)
                    {
                        break;
                    }

                    Output.WriteLine("Margin listener stopped unexpectedly.");
                    break;
                }

                if (!mainThreadWorking)
                {
                    client?.Close();
                    break;
                }

                try
                {
                    client.NoDelay = true;
                    client.ReceiveTimeout = MarginClient.PreAuthReceiveTimeoutMs;
                    client.SendTimeout = MarginClient.SendTimeoutMs;
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 15);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 4);
                }
                catch
                {
                }

                MarginClient marClient = new MarginClient(client.GetHashCode());

                // Define a new thread with the handling method as main loop and start it
                Thread clientThread = new Thread(new ParameterizedThreadStart(marClient.HandleClientComm))
                {
                    IsBackground = true
                };
                marClient.marginThreadId = clientThread.ManagedThreadId;

                RegisterClient(marClient, clientThread);
                clientThread.Start(client);
            }
        }

        internal int ActiveClientCount
        {
            get
            {
                lock (listLock)
                {
                    return clientList.Count;
                }
            }
        }

        internal void RegisterClient(MarginClient marClient, Thread clientThread = null)
        {
            if (marClient == null) return;
            marClient.AttachOwner(this);
            lock (listLock)
            {
                if (clientThread != null)
                {
                    threadList.RemoveAll(t => t == null || !t.IsAlive);
                    threadList.Add(clientThread);
                }

                clientList.RemoveAll(c => c == null || !c.isWorking());
                clientList.Add(marClient);
            }
        }

        internal void UnregisterClient(MarginClient marClient)
        {
            if (marClient == null) return;
            int threadId = marClient.marginThreadId;
            lock (listLock)
            {
                clientList.Remove(marClient);
                if (threadList != null)
                {
                    for (int i = threadList.Count - 1; i >= 0; i--)
                    {
                        Thread t = threadList[i];
                        if (t == null || !t.IsAlive || (threadId != 0 && t.ManagedThreadId == threadId))
                        {
                            threadList.RemoveAt(i);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Atomically checks that no other live Margin connection or active World session holds
        /// <paramref name="charId"/> and claims it for <paramref name="claimant"/> under <see cref="listLock"/>,
        /// pruning dead Margin entries along the way.
        /// </summary>
        internal bool TryClaimCharacter(MarginClient claimant, UInt32 charId)
        {
            if (claimant == null || charId == 0 || !claimant.isWorking()) return false;
            lock (listLock)
            {
                if (!claimant.isWorking()) return false;
                for (int i = clientList.Count - 1; i >= 0; i--)
                {
                    MarginClient existing = clientList[i];
                    if (existing == null || !existing.isWorking())
                    {
                        clientList.RemoveAt(i);
                        continue;
                    }
                    if (!ReferenceEquals(existing, claimant) && existing.getCharID() == charId)
                    {
                        return false;
                    }
                }
                if (activeWorldCharacterCheck != null && activeWorldCharacterCheck(charId))
                {
                    return false;
                }
                claimant.SetClaimedCharId(charId);
                return true;
            }
        }

        // <summary>
        // An external Hole to establish the UDP Session.
        // Will be called by a seperate thread maybe (NOT FINISHED YET)
        // </summary>
        // <param name="charID">the CharID to find the correct thread
        public void SendUDPSessionReply(UInt32 charID)
        {
            MarginClient match = null;
            lock (listLock)
            {
                for (int i = clientList.Count - 1; i >= 0; i--)
                {
                    MarginClient resultClient = clientList[i];
                    if (resultClient == null || !resultClient.isWorking())
                    {
                        clientList.RemoveAt(i);
                        continue;
                    }
                    if (resultClient.getCharID() == charID)
                    {
                        match = resultClient;
                        break;
                    }
                }
            }

            if (match != null)
            {
                match.EstablishUDPSessionReplyExternal();
            }
        }

        /// <summary>
        /// True when a live margin connection with a verified ticket has this character loaded: the world only opens a
        /// UDP session for such a character, since the UDP init carries nothing but the character id.
        /// </summary>
        public bool HasVerifiedCharacter(UInt32 charId) => SessionKeyForVerifiedCharacter(charId) != null;

        /// <summary>
        /// The session key of the live, verified margin connection that has this character loaded, or null. The world
        /// encrypts that character's UDP session with it, so only the client that got it under its account key can talk.
        /// </summary>
        public byte[] SessionKeyForVerifiedCharacter(UInt32 charId)
        {
            lock (listLock)
            {
                for (int i = clientList.Count - 1; i >= 0; i--)
                {
                    MarginClient resultClient = clientList[i];
                    if (resultClient == null || !resultClient.isWorking())
                    {
                        clientList.RemoveAt(i);
                        continue;
                    }
                    if (resultClient.HoldsVerifiedCharacter(charId))
                    {
                        return resultClient.SessionKey;
                    }
                }
            }
            return null;
        }

        public bool IsAnotherClientActive(UInt32 charId)
        {
            lock (listLock)
            {
                for (int i = clientList.Count - 1; i >= 0; i--)
                {
                    MarginClient resultClient = clientList[i];
                    if (resultClient == null || !resultClient.isWorking())
                    {
                        clientList.RemoveAt(i);
                        continue;
                    }
                    if (resultClient.getCharID() == charId)
                    {
                        return true;
                    }
                }
                if (activeWorldCharacterCheck != null && activeWorldCharacterCheck(charId))
                {
                    return true;
                }
            }

            return false;
        }

        public void RemoveClientsByCharId(UInt32 charId)
        {
            List<MarginClient> removedClients = null;
            List<int> threadIds = null;
            lock (listLock)
            {
                for (int i = clientList.Count - 1; i >= 0; i--)
                {
                    MarginClient resultClient = clientList[i];
                    if (resultClient == null)
                    {
                        clientList.RemoveAt(i);
                        continue;
                    }
                    if (resultClient.getCharID() == charId)
                    {
                        clientList.RemoveAt(i);
                        if (removedClients == null) removedClients = new List<MarginClient>(2);
                        removedClients.Add(resultClient);
                        if (resultClient.marginThreadId != 0)
                        {
                            if (threadIds == null) threadIds = new List<int>(2);
                            threadIds.Add(resultClient.marginThreadId);
                        }
                    }
                }
            }

            if (removedClients != null)
            {
                for (int i = 0; i < removedClients.Count; i++)
                {
                    // Close the evicted socket so any blocked NetworkStream.Read unblocks immediately
                    // instead of leaking the TCP connection and OS thread.
                    removedClients[i].passiveClose();
                    removedClients[i].forceClose();
                }
            }

            if (threadIds != null)
            {
                for (int i = 0; i < threadIds.Count; i++)
                {
                    RemoveThreadFromThreadList(threadIds[i]);
                }
            }
        }

        private void RemoveThreadFromThreadList(int ThreadId)
        {
            Thread toInterrupt = null;
            lock (listLock)
            {
                if (threadList == null) return;
                for (int i = threadList.Count - 1; i >= 0; i--)
                {
                    Thread tempThread = threadList[i];
                    if (tempThread == null || !tempThread.IsAlive)
                    {
                        threadList.RemoveAt(i);
                        continue;
                    }

                    if (tempThread.ManagedThreadId == ThreadId)
                    {
                        toInterrupt = tempThread;
                        threadList.RemoveAt(i);
                        break;
                    }
                }
            }

            if (toInterrupt != null && toInterrupt.IsAlive)
            {
                toInterrupt.Interrupt();
            }
        }
    }
}
