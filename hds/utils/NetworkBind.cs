using System;
using System.Net;

namespace hds
{
    /// <summary>
    /// The address the auth, margin, world and console servers listen on. MXO_BIND_ADDRESS sets it (e.g. 127.0.0.1
    /// for local testing with the client on the same machine: a loopback-only server doesn't need a Windows
    /// Firewall exception, so every new build path doesn't raise the firewall prompt). Default: all interfaces.
    /// </summary>
    public static class NetworkBind
    {
        public static IPAddress Address => Parse(Environment.GetEnvironmentVariable("MXO_BIND_ADDRESS"));

        // The real client's ports are fixed, so the defaults must stay as they are. The overrides exist so several
        // servers can run side by side for headless testing. Margin (TCP) and world (UDP) share 10000 by default.
        public const int DefaultAuthPort = 11000;
        public const int DefaultMarginPort = 10000;
        public const int DefaultWorldPort = 10000;
        public const int DefaultConsolePort = 55557;

        public static int AuthPort => ParsePort(Environment.GetEnvironmentVariable("MXO_AUTH_PORT"), DefaultAuthPort);
        public static int MarginPort => ParsePort(Environment.GetEnvironmentVariable("MXO_MARGIN_PORT"), DefaultMarginPort);
        public static int WorldPort => ParsePort(Environment.GetEnvironmentVariable("MXO_WORLD_PORT"), DefaultWorldPort);
        public static int ConsolePort => ParsePort(Environment.GetEnvironmentVariable("MXO_CONSOLE_PORT"), DefaultConsolePort);

        /// <summary>A port number from an environment variable; anything that isn't a valid port means the default.</summary>
        public static int ParsePort(string value, int defaultPort)
        {
            return int.TryParse(value?.Trim(), out int port) && port > 0 && port <= 65535 ? port : defaultPort;
        }

        public static IPAddress Parse(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value.Trim(), out IPAddress address)
                ? address
                : IPAddress.Any;
        }
    }
}
