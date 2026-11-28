using System;
using System.Collections.Generic;
using System.Net.Sockets;
using hds.auth;
using HDS.Network;
using NetCoreServer;

namespace hds
{
    public class AuthClientSession : TcpSession
    {
        private const int MaxPendingFramesPerSession = 8;

        private readonly AuthServer _authServer;
        private readonly AuthHandler _handler;
        private readonly AuthFrameBuffer _frameBuffer = new AuthFrameBuffer();
        private readonly Queue<byte[]> _pendingFrames = new Queue<byte[]>();
        private readonly List<byte[]> _extractedScratch = new List<byte[]>(1);
        private readonly object _sessionLock = new object();
        private long _lastActivityTick;
        private bool _processingFrame;
        private volatile bool _disconnected;

        public AuthClientSession(TcpServer server) : base(server)
        {
            _authServer = server as AuthServer;
            _handler = new AuthHandler(this);
            _lastActivityTick = Environment.TickCount64;
        }

        internal bool IsIdleExpired(long nowTick, int idleTimeoutMs)
        {
            if (_disconnected || IsDisposed || !IsConnected) return false;
            lock (_sessionLock)
            {
                if (_disconnected || _processingFrame || _pendingFrames.Count > 0)
                {
                    return false;
                }
                return nowTick - _lastActivityTick >= idleTimeoutMs;
            }
        }

        protected override void OnConnected()
        {
            // Don't make a new handler here: the session can already have received its first packet when this runs (it
            // happens under load), and replacing the handler then lost the challenge, so the client's answer crashed the
            // server (found by tools/HeadlessClient). The constructor's handler is the only one.
            lock (_sessionLock)
            {
                _lastActivityTick = Environment.TickCount64;
            }
            Console.WriteLine($"AuthClient with Id {Id} connected!");
        }

        protected override void OnDisconnected()
        {
            _disconnected = true;
            lock (_sessionLock)
            {
                _pendingFrames.Clear();
                _extractedScratch.Clear();
            }

            Console.WriteLine($"AuthClient with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] packet, long offset, long size)
        {
            if (size <= 0 || _disconnected) return;

            byte[] firstToDispatch = null;
            bool dropSession = false;
            string dropReason = null;

            lock (_sessionLock)
            {
                if (_disconnected) return;
                _lastActivityTick = Environment.TickCount64;
                _extractedScratch.Clear();

                try
                {
                    _frameBuffer.Append(packet, (int)offset, (int)size, _extractedScratch);
                }
                catch (Exception ex)
                {
                    dropSession = true;
                    dropReason = ex.Message;
                }

                if (!dropSession && _extractedScratch.Count > 0)
                {
                    if (_pendingFrames.Count + _extractedScratch.Count > MaxPendingFramesPerSession)
                    {
                        dropSession = true;
                        dropReason = "too many pipelined auth frames";
                    }
                    else
                    {
                        for (int i = 0; i < _extractedScratch.Count; i++)
                        {
                            _pendingFrames.Enqueue(_extractedScratch[i]);
                        }

                        if (!_processingFrame && _pendingFrames.Count > 0)
                        {
                            _processingFrame = true;
                            firstToDispatch = _pendingFrames.Dequeue();
                        }
                    }
                }
                _extractedScratch.Clear();
            }

            if (dropSession)
            {
                Output.WriteLine($"AuthClient with Id {Id} dropped: {dropReason}");
                Disconnect();
                return;
            }

            if (firstToDispatch != null)
            {
                DispatchLoop(firstToDispatch);
            }
        }

        private void DispatchLoop(byte[] frame)
        {
            while (frame != null)
            {
                if (_disconnected || IsDisposed || !IsConnected)
                {
                    lock (_sessionLock)
                    {
                        _processingFrame = false;
                        _pendingFrames.Clear();
                    }
                    return;
                }

                if (_authServer == null)
                {
                    ExecuteFrame(frame);
                    frame = DequeueNextOrFinish();
                    continue;
                }

                int opcode = AuthHandler.PeekOpcode(frame);
                AuthWorkQueue targetQueue = (opcode == (byte)AuthOpcode.AS_GetPublicKeyRequest ||
                                             opcode == (byte)AuthOpcode.AS_AuthRequest)
                    ? _authServer.FastQueue
                    : _authServer.LoginQueue;

                byte[] captured = frame;
                if (targetQueue.TryEnqueue(() => ProcessOnWorker(captured)))
                {
                    return;
                }

                // Queue is full: send a client-visible "server unavailable / busy" reply instead of dropping the socket.
                Output.WriteLine($"AuthServer busy (queue limit {targetQueue.MaxQueueDepth} reached); replying LTAS_DATABASECONNDOWN to {Id}");
                _handler.SendAuthError((uint)AuthErrorCode.LTAS_DATABASECONNDOWN);
                frame = DequeueNextOrFinish();
            }
        }

        private void ProcessOnWorker(byte[] frame)
        {
            try
            {
                if (!_disconnected && !IsDisposed && IsConnected)
                {
                    ExecuteFrame(frame);
                }
            }
            finally
            {
                byte[] next = DequeueNextOrFinish();
                if (next != null)
                {
                    DispatchLoop(next);
                }
            }
        }

        private byte[] DequeueNextOrFinish()
        {
            lock (_sessionLock)
            {
                _lastActivityTick = Environment.TickCount64;
                if (_disconnected || _pendingFrames.Count == 0)
                {
                    _processingFrame = false;
                    return null;
                }

                return _pendingFrames.Dequeue();
            }
        }

        private void ExecuteFrame(byte[] frame)
        {
            try
            {
                PacketReader reader = new PacketReader(frame);
                _handler.ProcessPacket(reader, frame.Length);
            }
            catch (Exception ex)
            {
                // Catch any protocol/handler exception so it never crashes the worker thread or process.
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
