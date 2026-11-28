using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using hds;
using hds.auth;
using hds.shared;
using HDS.Network;

namespace hds.Tests;

public class AuthIdleAndDelayedActionsTests
{
    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task AuthServerDisconnectsSilentIdleClientAndStopsCleanly()
    {
        int port = GetFreeTcpPort();
        using var server = new AuthServer(IPAddress.Loopback, port, workerCount: 2, maxQueueDepth: 32, idleTimeoutMs: 250);
        server.startServer();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            // Stay completely silent; AuthServer's idle reaper must disconnect us within a couple seconds.
            byte[] buf = new byte[16];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            int read = await stream.ReadAsync(buf, cts.Token);
            Assert.Equal(0, read);
        }
        finally
        {
            server.stopServer();
        }
    }

    [Fact]
    public async Task AuthServerServesGetPublicKeyBeforeIdleTimeout()
    {
        int port = GetFreeTcpPort();
        using var server = new AuthServer(IPAddress.Loopback, port, workerCount: 2, maxQueueDepth: 32, idleTimeoutMs: 2000);
        server.startServer();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            // AS_GetPublicKeyRequest: length 0x05, opcode 0x06, 4 zero bytes.
            byte[] request = { 0x05, (byte)AuthOpcode.AS_GetPublicKeyRequest, 0x00, 0x00, 0x00, 0x00 };
            await stream.WriteAsync(request);
            await stream.FlushAsync();

            byte[] reply = new byte[512];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            int n = await stream.ReadAsync(reply, cts.Token);
            Assert.True(n > 10);
            int opcode = AuthHandler.PeekOpcode(reply);
            Assert.Equal((byte)AuthOpcode.AS_GetPublicKeyReply, opcode);
        }
        finally
        {
            server.stopServer();
        }
    }

    [Fact]
    public void DelayedActionsPreservesFifoOrderIsolatesExceptionsAndRetainsFutureItems()
    {
        // Use a high private virtual time window far beyond normal sim time so other tests never collide.
        var executed = new List<int>();
        double futureNow = TimeUtils.GetSimTimeSeconds() + 10_000_000.0;

        DelayedActions.After(10_000_001.0, () => executed.Add(1));
        DelayedActions.After(10_000_002.0, () => throw new InvalidOperationException("boom"));
        DelayedActions.After(10_000_003.0, () => executed.Add(3));
        DelayedActions.After(10_000_050.0, () => executed.Add(4));

        // Nothing due yet at futureNow: must execute nothing and retain all 4.
        DelayedActions.RunDue(futureNow);
        Assert.Empty(executed);

        // At futureNow + 10, items 1, 2 (throws), 3 are due in FIFO order; item 4 remains pending.
        DelayedActions.RunDue(futureNow + 10.0);
        Assert.Equal(new[] { 1, 3 }, executed);

        // At futureNow + 100, item 4 fires.
        DelayedActions.RunDue(futureNow + 100.0);
        Assert.Equal(new[] { 1, 3, 4 }, executed);
    }

    [Fact]
    public void WorldServerBroadcastSnapshotIsReentrantAndFindsClientByCharId()
    {
        var c1 = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c1.playerData = new ClientData();
        c1.playerData.setCharID(7001);

        var c2 = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c2.playerData = new ClientData();
        c2.playerData.setCharID(7002);

        var clients = new Dictionary<string, WorldClient>
        {
            ["127.0.0.1:1001"] = c1,
            ["127.0.0.1:1002"] = c2,
        };

        Assert.Same(c2, WorldServer.FindClientByCharId(clients, 7002));
        Assert.Null(WorldServer.FindClientByCharId(clients, 9999));

        var outer = WorldServer.RentClientSnapshot(clients);
        try
        {
            Assert.Equal(2, outer.Count);
            // Re-entrant nested rent on the same thread must return a separate list without corrupting outer.
            var inner = WorldServer.RentClientSnapshot(clients);
            try
            {
                Assert.NotSame(outer, inner);
                Assert.Equal(2, inner.Count);
            }
            finally
            {
                WorldServer.ReturnClientSnapshot(inner);
            }

            Assert.Equal(2, outer.Count);
        }
        finally
        {
            WorldServer.ReturnClientSnapshot(outer);
        }

        // Returned list is cleared and reused on next rent.
        Assert.Empty(outer);
        var reused = WorldServer.RentClientSnapshot(clients);
        try
        {
            Assert.Equal(2, reused.Count);
        }
        finally
        {
            WorldServer.ReturnClientSnapshot(reused);
        }
    }
}
