using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;

namespace hds.auth
{
    public class AuthServer : TcpServer
    {
        public const int DefaultAcceptorBacklog = 4096;
        public const int DefaultMaxQueueDepth = 1024;
        public const int DefaultIdleTimeoutMs = 30_000;

        private readonly AuthWorkQueue _fastQueue;
        private readonly AuthWorkQueue _loginQueue;
        private readonly int _idleTimeoutMs;
        private readonly object _reaperGate = new object();
        private Timer _idleReaper;
        private int _reapInProgress;

        internal AuthWorkQueue FastQueue => _fastQueue;
        internal AuthWorkQueue LoginQueue => _loginQueue;
        internal int IdleTimeoutMs => _idleTimeoutMs;

        public int LoginWorkerCount => _loginQueue.WorkerCount;
        public int MaxLoginQueueDepth => _loginQueue.MaxQueueDepth;

        protected override TcpSession CreateSession()
        {
            return new AuthClientSession(this);
        }

        public AuthServer() : this(NetworkBind.Address, NetworkBind.AuthPort, null, null, null)
        {
        }

        public AuthServer(IPAddress address, int port, int? workerCount = null, int? maxQueueDepth = null)
            : this(address, port, workerCount, maxQueueDepth, null)
        {
        }

        public AuthServer(IPAddress address, int port, int? workerCount, int? maxQueueDepth, int? idleTimeoutMs)
            : base(address, port)
        {
            OptionAcceptorBacklog = DefaultAcceptorBacklog;
            OptionNoDelay = true;
            OptionKeepAlive = true;

            int resolvedWorkers = workerCount ?? ReadIntEnv("MXO_AUTH_WORKERS", Math.Max(4, Environment.ProcessorCount), 1, 64);
            int resolvedQueue = maxQueueDepth ?? ReadIntEnv("MXO_AUTH_MAX_QUEUE", DefaultMaxQueueDepth, 1, 10000);
            _idleTimeoutMs = idleTimeoutMs ?? ReadIntEnv("MXO_AUTH_IDLE_TIMEOUT_MS", DefaultIdleTimeoutMs, 100, 300_000);
            int fastWorkers = Math.Min(8, Math.Max(2, Environment.ProcessorCount / 2));
            int fastQueueDepth = Math.Max(2048, resolvedQueue * 2);

            _fastQueue = new AuthWorkQueue(fastWorkers, fastQueueDepth, "AuthFast");
            _loginQueue = new AuthWorkQueue(resolvedWorkers, resolvedQueue, "AuthLogin");

            Output.WriteLine("Auth server set and ready at port " + port +
                             " (workers=" + resolvedWorkers + ", maxQueue=" + resolvedQueue + ", backlog=" + OptionAcceptorBacklog + ")");
            if (AuthHandler.DevAcceptAnyPassword)
            {
                Output.WriteLine("WARNING: MXO_DEV_ANY_PASSWORD is set - any password is accepted for existing accounts. Local testing only.");
            }
        }

        protected override void OnStarted()
        {
            StartIdleReaper();
        }

        protected override void OnStopped()
        {
            StopIdleReaper();
        }

        private void StartIdleReaper()
        {
            int intervalMs = Math.Clamp(_idleTimeoutMs / 4, 50, 5_000);
            lock (_reaperGate)
            {
                _idleReaper?.Dispose();
                _idleReaper = new Timer(_ => ReapIdleSessionsTick(), null, intervalMs, intervalMs);
            }
        }

        private void StopIdleReaper()
        {
            Timer reaper;
            lock (_reaperGate)
            {
                reaper = _idleReaper;
                _idleReaper = null;
            }
            reaper?.Dispose();
        }

        private void ReapIdleSessionsTick()
        {
            if (Interlocked.CompareExchange(ref _reapInProgress, 1, 0) != 0)
            {
                return;
            }
            try
            {
                ReapIdleSessions(Environment.TickCount64);
            }
            catch (Exception ex)
            {
                Output.WriteDebugLog("AuthServer idle reaper error: " + ex.Message);
            }
            finally
            {
                Volatile.Write(ref _reapInProgress, 0);
            }
        }

        internal int ReapIdleSessions(long nowTick)
        {
            if (IsDisposed || !IsStarted) return 0;
            int disconnected = 0;
            foreach (var entry in Sessions)
            {
                if (entry.Value is AuthClientSession session && session.IsIdleExpired(nowTick, _idleTimeoutMs))
                {
                    Output.OptWriteLine($"AuthClient with Id {session.Id} timed out after {_idleTimeoutMs}ms idle");
                    session.Disconnect();
                    disconnected++;
                }
            }
            return disconnected;
        }

        protected override void OnError(SocketError error)
        {
            Output.WriteLine("AuthServer caught a socket error: " + error);
        }

        public void startServer()
        {
            Start();
        }

        public void stopServer()
        {
            StopIdleReaper();
            Stop();
            _fastQueue.Dispose();
            _loginQueue.Dispose();
        }

        protected override void Dispose(bool disposingManagedResources)
        {
            if (disposingManagedResources)
            {
                StopIdleReaper();
                _fastQueue.Dispose();
                _loginQueue.Dispose();
            }

            base.Dispose(disposingManagedResources);
        }

        private static int ReadIntEnv(string name, int fallback, int min, int max)
        {
            string raw = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(raw) &&
                int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                if (parsed < min) return min;
                if (parsed > max) return max;
                return parsed;
            }

            return fallback;
        }
    }
}
