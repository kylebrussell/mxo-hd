using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HeadlessClient;
using hds;

namespace NativeQa;

public sealed record NativeQaEndpoint(IPAddress Address)
{
    public const string BindOption = "--bind-address";
    public bool IsPrimary => Address.Equals(IPAddress.Loopback);
    public string Account => IsPrimary ? "CSR_AbilityQA" : "Codex_SecondQA";
    public ServerAddress Server => new(Address.ToString(), NetworkBind.DefaultAuthPort, NetworkBind.DefaultMarginPort,
        NetworkBind.DefaultWorldPort);

    public static NativeQaEndpoint Parse(string text)
    {
        if (!IPAddress.TryParse(text, out var address) || address.AddressFamily != AddressFamily.InterNetwork ||
            !IPAddress.IsLoopback(address) || address.ToString() != text)
            throw new ArgumentException("NativeQa --bind-address requires a canonical IPv4 loopback address (for example 127.0.0.2).");
        return new NativeQaEndpoint(address);
    }

    public string ClientLock(string primaryLock) => IsPrimary ? primaryLock : Path.ChangeExtension(primaryLock, Address + ".lock");

    public static bool Conflicts(IPAddress address, int port, IEnumerable<IPEndPoint> listeners) =>
        listeners.Any(listener => listener.Port == port &&
            (listener.Address.Equals(address) || listener.Address.Equals(IPAddress.Any) ||
                listener.Address.Equals(IPAddress.IPv6Any)));

    public void RequireAvailable()
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var tcp = properties.GetActiveTcpListeners();
        var udp = properties.GetActiveUdpListeners();
        foreach (int port in new[] { Server.AuthPort, Server.MarginPort })
            if (Conflicts(Address, port, tcp)) throw new IOException($"NativeQa TCP endpoint {Address}:{port} is occupied.");
        if (Conflicts(Address, Server.WorldPort, udp))
            throw new IOException($"NativeQa UDP endpoint {Address}:{Server.WorldPort} is occupied.");
    }

    public void RequireReady(ServerInstance server)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var tcp = properties.GetActiveTcpListeners();
        var udp = properties.GetActiveUdpListeners();
        bool listening = new[] { Server.AuthPort, Server.MarginPort }
            .All(port => tcp.Contains(new IPEndPoint(Address, port))) && udp.Contains(new IPEndPoint(Address, Server.WorldPort));
        if (server.HasExited || server.ConfiguredAddress != Server || !listening)
            throw new IOException($"NativeQa did not start its stock listeners on {Address}.");
    }
}
