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

        public static IPAddress Parse(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value.Trim(), out IPAddress address)
                ? address
                : IPAddress.Any;
        }
    }
}
