﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using hds;
 using hds.auth;
 using hds.databases.Entities;
 using NetCoreServer;

namespace hds
{
    public class AuthClientSession : TcpSession
    {
        private AuthHandler _handler;

        public AuthClientSession(TcpServer server) : base(server)
        {
            _handler = new AuthHandler(this);
        }
        
        protected override void OnConnected()
        {
            // Don't make a new handler here: the session can already have received its first packet when this runs (it
            // happens under load), and replacing the handler then lost the challenge, so the client's answer crashed the
            // server (found by tools/HeadlessClient). The constructor's handler is the only one.
            Console.WriteLine($"AuthClient with Id {Id} connected!");
            
        }

        protected override void OnDisconnected()
        {
            
            Console.WriteLine($"AuthClient with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] packet, long offset, long size)
        {
            byte[] packetContent = new byte[size];
            ArrayUtils.copy(packet,0, packetContent, 0, (int)size);
            PacketReader reader = new PacketReader(packetContent);
            try
            {
                _handler.ProcessPacket(reader, size);
            }
            catch (Exception ex)
            {
                // This runs on a socket callback: anything escaping here ends the process and drops every player, for
                // one bad login (a wrong MD5 and an unknown opcode both throw AuthException).
                Output.WriteLine($"AuthClient with Id {Id} dropped: {ex.Message}");
                Disconnect();
            }

        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AuthClient Session caught an error with code {error}");
        }
    }
}