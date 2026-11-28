using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using hds;
using Xunit;

namespace hds.Tests;

public class WorldIngressAndEntityIdTests
{
    private static void SetField(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static WorldClient MakeTestClient(uint charId = 0, bool blockWorker = true)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.Alive = true;
        SetField(client, "receivedPackets", new Queue<byte[]>());
        SetField(client, "packetWorkerRunning", blockWorker);
        SetField(client, "processLock", new object());
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        return client;
    }

    [Fact]
    public async Task NextEntityId_IsThreadSafeAndStrictlyUnique()
    {
        const int workers = 16;
        const int perWorker = 1000;
        var bag = new ConcurrentBag<ulong>();

        Task[] tasks = Enumerable.Range(0, workers)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < perWorker; i++)
                {
                    bag.Add(WorldServer.NextEntityId());
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        ulong[] ids = bag.ToArray();
        Assert.Equal(workers * perWorker, ids.Length);
        Assert.Equal(ids.Length, new HashSet<ulong>(ids).Count);
        Assert.All(ids, id => Assert.True(id > 0));
    }

    [Fact]
    public void QueuePooledPacket_EnforcesMaxQueuedPacketsCap()
    {
        var client = MakeTestClient(charId: 42, blockWorker: true);
        const int extra = 35;
        int total = WorldClient.MaxQueuedPackets + extra;

        for (int i = 0; i < total; i++)
        {
            byte[] buf = ArrayPool<byte>.Shared.Rent(64);
            client.QueuePooledPacket(buf, 16);
        }

        Assert.Equal(WorldClient.MaxQueuedPackets, client.PendingPacketCount);
        Assert.Equal(extra, client.DroppedIngressPackets);

        client.CleanupOnDisconnect();
        Assert.Equal(0, client.PendingPacketCount);
    }

    [Fact]
    public void QueuePacket_EnforcesMaxQueuedPacketsCap()
    {
        var client = MakeTestClient(charId: 43, blockWorker: true);
        const int extra = 12;
        int total = WorldClient.MaxQueuedPackets + extra;

        for (int i = 0; i < total; i++)
        {
            client.QueuePacket(new byte[] { 1, 2, 3, 4 });
        }

        Assert.Equal(WorldClient.MaxQueuedPackets, client.PendingPacketCount);
        Assert.Equal(extra, client.DroppedIngressPackets);
    }

    [Fact]
    public void CanAdmitNewEndpointLocked_EnforcesTotalAndUnauthenticatedCaps()
    {
        Assert.False(WorldServer.CanAdmitNewEndpointLocked(null!, maxClients: 10, maxUnauthenticated: 3));

        var registry = new Dictionary<string, WorldClient>();

        // Fill up to the unauthenticated limit (3)
        for (int i = 0; i < 3; i++)
        {
            Assert.True(WorldServer.CanAdmitNewEndpointLocked(registry, maxClients: 10, maxUnauthenticated: 3));
            registry["unauth-" + i] = MakeTestClient(charId: 0);
        }

        // 4th unknown endpoint must be rejected while 3 unauthenticated slots are occupied
        Assert.False(WorldServer.CanAdmitNewEndpointLocked(registry, maxClients: 10, maxUnauthenticated: 3));

        // Once one client authenticates (charId != 0), unauthenticated count drops to 2 and admission resumes
        registry["unauth-0"].playerData.setCharID(1001);
        Assert.True(WorldServer.CanAdmitNewEndpointLocked(registry, maxClients: 10, maxUnauthenticated: 3));

        // Authenticate all existing clients and fill up to total maxClients (10)
        registry["unauth-1"].playerData.setCharID(1002);
        registry["unauth-2"].playerData.setCharID(1003);
        for (int i = 3; i < 10; i++)
        {
            Assert.True(WorldServer.CanAdmitNewEndpointLocked(registry, maxClients: 10, maxUnauthenticated: 3));
            registry["auth-" + i] = MakeTestClient(charId: (uint)(2000 + i));
        }

        // At total maxClients, even with 0 unauthenticated clients, admission is rejected
        Assert.False(WorldServer.CanAdmitNewEndpointLocked(registry, maxClients: 10, maxUnauthenticated: 3));
    }

    [Fact]
    public void RpcReceiveWindow_ReplacedSequenceIsNotPrematurelyEvicted()
    {
        var window = new RpcReceiveWindow();
        byte[] payloadA = { 0x01, 0x02, 0x03 };
        byte[] payloadB = { 0x09, 0x08, 0x07 };

        // Insert sequence 1 first, then 100 more unique sequences
        Assert.False(window.IsResend(1, 0x100, payloadA));
        for (ushort seq = 2; seq <= 101; seq++)
        {
            Assert.False(window.IsResend(seq, 0x100, payloadA));
        }

        // Replace sequence 1 with a new payload (fresh ticket at the tail of the FIFO)
        Assert.False(window.IsResend(1, 0x100, payloadB));
        Assert.True(window.IsResend(1, 0x100, payloadB));

        // Insert enough new sequences to push past the stale ticket of sequence 1,
        // while keeping fewer than Capacity entries after the replacement.
        for (ushort seq = 102; seq <= RpcReceiveWindow.Capacity + 10; seq++)
        {
            Assert.False(window.IsResend(seq, 0x100, payloadA));
        }

        // Sequence 1 with payloadB MUST still be tracked as a resend
        Assert.True(window.IsResend(1, 0x100, payloadB));

        // Push another 100 unique entries so the replacement ticket ages out past Capacity
        for (ushort seq = (ushort)(RpcReceiveWindow.Capacity + 11); seq <= RpcReceiveWindow.Capacity + 110; seq++)
        {
            Assert.False(window.IsResend(seq, 0x100, payloadA));
        }

        Assert.False(window.IsResend(1, 0x100, payloadB));
    }

    [Fact]
    public void RpcReceiveWindow_EmptyAndNullPayloadsAndReset()
    {
        var window = new RpcReceiveWindow();

        Assert.False(window.IsResend(7, 0x200, Array.Empty<byte>()));
        Assert.True(window.IsResend(7, 0x200, Array.Empty<byte>()));
        Assert.True(window.IsResend(7, 0x200, null!));

        // Different header with same empty payload is treated as a new RPC
        Assert.False(window.IsResend(7, 0x201, Array.Empty<byte>()));
        Assert.True(window.IsResend(7, 0x201, Array.Empty<byte>()));

        window.Reset();
        Assert.False(window.IsResend(7, 0x201, Array.Empty<byte>()));
    }

    [Fact]
    public void HasPendingMessages_DetectsAllQueueChannelsAndHandlesNullQueue()
    {
        var uninitialized = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        Assert.False(uninitialized.HasPendingMessages());

        var client = MakeTestClient(charId: 99);
        client.messageQueue = new MessageQueue();
        Assert.False(client.HasPendingMessages());

        client.messageQueue.ackOnlyCount = 1;
        Assert.True(client.HasPendingMessages());
        client.messageQueue.ackOnlyCount = 0;
        Assert.False(client.HasPendingMessages());

        client.messageQueue.addRawMessage(new byte[] { 0x01 });
        Assert.True(client.HasPendingMessages());
        client.messageQueue.rawMessages.Clear();
        Assert.False(client.HasPendingMessages());

        client.messageQueue.addObjectMessage(new byte[] { 0x02 }, false);
        Assert.True(client.HasPendingMessages());
        client.messageQueue.ObjectMessagesQueue.Clear();
        Assert.False(client.HasPendingMessages());

        client.messageQueue.addRpcMessage(new byte[] { 0x03 });
        Assert.True(client.HasPendingMessages());
        client.messageQueue.RPCMessagesQueue.Clear();
        Assert.False(client.HasPendingMessages());
    }
}
