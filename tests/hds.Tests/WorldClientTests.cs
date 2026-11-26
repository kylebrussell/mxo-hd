using System.Net;
using System.Net.Sockets;
using hds;
using hds.shared;

namespace hds.Tests;

[CollectionDefinition(nameof(WorldClientGlobals), DisableParallelization = true)]
public sealed class WorldClientGlobals { }

[Collection(nameof(WorldClientGlobals))]
public class WorldClientTests
{
    /// <summary>
    /// A client is listed before its first packet is processed. If it counted as silent until then, the visibility
    /// thread could remove it at once, and its session's replies never went out.
    /// </summary>
    [Fact]
    public void ANewClientIsAliveBeforeItsFirstPacketIsProcessed()
    {
        var previousWorld = Store.world;
        var previousClients = WorldServer.Clients;
        try
        {
            Store.world = new WorldServer();
            const string key = "127.0.0.1:1";
            Store.world.objMan.PushClient(key);
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            WorldClient client = new WorldClient(new IPEndPoint(IPAddress.Loopback, 1), socket, key);
            Assert.True(client.Alive);
        }
        finally
        {
            Store.world = previousWorld;
            WorldServer.Clients = previousClients;
        }
    }
}
