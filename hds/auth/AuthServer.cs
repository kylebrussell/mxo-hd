using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;

namespace hds.auth
{
    public class AuthServer : TcpServer
    {
        public const int DefaultAcceptorBacklog = 4096;
        public const int DefaultMaxQueueDepth = 1024;

        private readonly AuthWorkQueue _fastQueue;
        private readonly AuthWorkQueue _loginQueue;

        internal AuthWorkQueue FastQueue => _fastQueue;
        internal AuthWorkQueue LoginQueue => _loginQueue;

        public int LoginWorkerCount => _loginQueue.WorkerCount;
        public int MaxLoginQueueDepth => _loginQueue.MaxQueueDepth;

        protected override TcpSession CreateSession()
        {
            return new AuthClientSession(this);
        }

        public AuthServer() : this(NetworkBind.Address, NetworkBind.AuthPort, null, null)
        {
        }

        public AuthServer(IPAddress address, int port, int? workerCount = null, int? maxQueueDepth = null)
            : base(address, port)
        {
            OptionAcceptorBacklog = DefaultAcceptorBacklog;
            OptionNoDelay = true;

            int resolvedWorkers = workerCount ?? ReadIntEnv("MXO_AUTH_WORKERS", Math.Max(4, Environment.ProcessorCount), 1, 64);
            int resolvedQueue = maxQueueDepth ?? ReadIntEnv("MXO_AUTH_MAX_QUEUE", DefaultMaxQueueDepth, 1, 10000);
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
            Stop();
            _fastQueue.Dispose();
            _loginQueue.Dispose();
        }

        protected override void Dispose(bool disposingManagedResources)
        {
            if (disposingManagedResources)
            {
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
