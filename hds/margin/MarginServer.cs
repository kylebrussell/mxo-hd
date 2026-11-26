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

        private TcpListener tcpListener;
        private Thread listenThread;
        private bool mainThreadWorking;

        public MarginServer()
        {
            // Threads storage
            mainThreadWorking = true;
            clientList = new List<MarginClient>();
            threadList = new List<Thread>();
            tcpListener = new TcpListener(NetworkBind.Address, NetworkBind.MarginPort);
            Output.WriteLine("Margin server set and ready at port " + NetworkBind.MarginPort);
            listenThread = new Thread(new ThreadStart(ListenForClients));
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

        internal void RegisterClient(MarginClient marClient, Thread clientThread = null)
        {
            lock (listLock)
            {
                if (clientThread != null)
                {
                    threadList.RemoveAll(t => t == null || !t.IsAlive);
                    threadList.Add(clientThread);
                }

                clientList.Add(marClient);
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
                foreach (MarginClient resultClient in clientList)
                {
                    if (resultClient != null && resultClient.getCharID() == charID)
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
            MarginClient[] snapshot;
            lock (listLock)
            {
                snapshot = clientList.ToArray();
            }

            foreach (MarginClient resultClient in snapshot)
            {
                if (resultClient != null && resultClient.HoldsVerifiedCharacter(charId)) return resultClient.SessionKey;
            }
            return null;
        }

        public bool IsAnotherClientActive(UInt32 charId)
        {
            lock (listLock)
            {
                foreach (MarginClient resultClient in clientList)
                {
                    if (resultClient != null && resultClient.getCharID() == charId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void RemoveClientsByCharId(UInt32 charId)
        {
            int threadIdToInterrupt = 0;
            lock (listLock)
            {
                MarginClient removeClient = null;
                foreach (MarginClient resultClient in clientList)
                {
                    if (resultClient != null && resultClient.getCharID() == charId)
                    {
                        threadIdToInterrupt = resultClient.marginThreadId;
                        removeClient = resultClient;
                        break;
                    }
                }

                if (removeClient != null)
                {
                    clientList.Remove(removeClient);
                }
            }

            if (threadIdToInterrupt != 0)
            {
                RemoveThreadFromThreadList(threadIdToInterrupt);
            }
        }

        private void RemoveThreadFromThreadList(int ThreadId)
        {
            Thread toInterrupt = null;
            lock (listLock)
            {
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
