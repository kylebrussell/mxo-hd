using System.Net;
using System.Net.Sockets;
using hds;
using hds.shared;

namespace hds.Tests;

public class WorldClientTests
{
    /// <summary>
    /// A client is listed before its first packet is processed. If it counted as silent until then, the visibility
    /// thread could remove it at once, and its session's replies never went out.
    /// </summary>
    [Fact]
    public void ANewClientIsAliveBeforeItsFirstPacketIsProcessed()
    {
        Store.world ??= new WorldServer();
        const string key = "127.0.0.1:1";
        Store.world.objMan.PushClient(key);
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        WorldClient client = new WorldClient(new IPEndPoint(IPAddress.Loopback, 1), socket, key);

        Assert.True(client.Alive);
    }
}
