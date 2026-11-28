using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.world.Structures;
using Xunit;

namespace hds.Tests;

public class UdpTransportAndPacketBoundsTests
{
    private static void SetField(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    [Theory]
    [InlineData("127.0.0.1", 54321)]
    [InlineData("192.168.10.42", 11000)]
    [InlineData("10.0.0.1", 65535)]
    public void TryReadIpv4EndpointKey_RoundTripsEndpointWithoutAllocating(string ipText, int port)
    {
        var ep = new IPEndPoint(IPAddress.Parse(ipText), port);
        SocketAddress sa = ep.Serialize();

        Assert.True(WorldServer.TryReadIpv4EndpointKey(sa, out ulong key, out uint ipv4Raw, out ushort decodedPort));
        Assert.Equal((ushort)port, decodedPort);
        var reconstructed = new IPEndPoint(new IPAddress((long)ipv4Raw), decodedPort);
        Assert.Equal(ep, reconstructed);
        Assert.Equal(((ulong)ipv4Raw << 16) | decodedPort, key);
    }

    [Fact]
    public void TryReadIpv4EndpointKey_ReturnsFalseForNullOrIpv6()
    {
        Assert.False(WorldServer.TryReadIpv4EndpointKey(null!, out _, out _, out _));

        var v6 = new IPEndPoint(IPAddress.IPv6Loopback, 12345).Serialize();
        Assert.False(WorldServer.TryReadIpv4EndpointKey(v6, out _, out _, out _));
    }

    [Fact]
    public void LoopbackUdpReceiveAndCachedSend_RoundTripsViaSocketAddress()
    {
        using var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        receiver.ReceiveTimeout = 3000;
        receiver.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        sender.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        var receiverEp = (IPEndPoint)receiver.LocalEndPoint!;
        var senderEp = (IPEndPoint)sender.LocalEndPoint!;

        // Build a minimal WorldClient pointing at receiverEp and send two datagrams using the cached SocketAddress path.
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        SetField(client, "Remote", receiverEp);
        SetField(client, "socket", sender);

        byte[] msg1 = { 0x01, 0x02, 0x03, 0x04, 0x05 };
        byte[] msg2 = { 0xAA, 0xBB, 0xCC };
        Assert.True(client.SendPacketSliceForTesting(msg1, msg1.Length));
        Assert.True(client.SendPacketSliceForTesting(msg2, msg2.Length));

        // Receive both via .NET 8 Socket.ReceiveFrom(Span, SocketAddress) and verify exact sender endpoint key.
        var recvSa = new SocketAddress(AddressFamily.InterNetwork);
        byte[] buf = new byte[64];

        int len1 = receiver.ReceiveFrom(buf.AsSpan(), SocketFlags.None, recvSa);
        Assert.Equal(msg1.Length, len1);
        Assert.Equal(msg1, buf[..len1]);
        Assert.True(WorldServer.TryReadIpv4EndpointKey(recvSa, out ulong key1, out uint ip1, out ushort port1));
        Assert.Equal(senderEp, new IPEndPoint(new IPAddress((long)ip1), port1));

        int len2 = receiver.ReceiveFrom(buf.AsSpan(), SocketFlags.None, recvSa);
        Assert.Equal(msg2.Length, len2);
        Assert.Equal(msg2, buf[..len2]);
        Assert.True(WorldServer.TryReadIpv4EndpointKey(recvSa, out ulong key2, out _, out _));
        Assert.Equal(key1, key2);
    }

    [Theory]
    [InlineData(SocketError.ConnectionReset, true)]
    [InlineData(SocketError.MessageSize, true)]
    [InlineData(SocketError.Interrupted, true)]
    [InlineData(SocketError.WouldBlock, true)]
    [InlineData(SocketError.TryAgain, true)]
    [InlineData(SocketError.NoBufferSpaceAvailable, true)]
    [InlineData(SocketError.OperationAborted, false)]
    [InlineData(SocketError.NotSocket, false)]
    [InlineData(SocketError.InvalidArgument, false)]
    public void IsTransientReceiveError_ClassifiesSocketErrors(SocketError error, bool expectedTransient)
    {
        Assert.Equal(expectedTransient, WorldServer.IsTransientReceiveError(error));
    }

    [Fact]
    public void PruneStaleEndpointCache_EvictsDeadAndClaimedFromIpv4Cache()
    {
        var world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        var ipv4Cache = new Dictionary<ulong, WorldClient>();
        var genericCache = new Dictionary<EndPoint, WorldClient>();
        SetField(world, "clientsByIpv4Endpoint", ipv4Cache);
        SetField(world, "staleIpv4EndpointScratch", new List<ulong>());
        SetField(world, "clientsByEndpoint", genericCache);
        SetField(world, "staleEndpointScratch", new List<EndPoint>());

        var aliveClient = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        aliveClient.Alive = true;
        SetField(aliveClient, "lastUsedTime", TimeUtils.getUnixTimeUint32());

        var deadClient = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        deadClient.Alive = false;

        var claimedClient = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        claimedClient.Alive = true;
        SetField(claimedClient, "reapClaimed", true);

        ipv4Cache[1] = aliveClient;
        ipv4Cache[2] = deadClient;
        ipv4Cache[3] = claimedClient;
        ipv4Cache[4] = null!;

        Assert.Equal(4, world.EndpointCacheCount);
        world.PruneStaleEndpointCache();
        Assert.Equal(1, world.EndpointCacheCount);
        Assert.True(ipv4Cache.ContainsKey(1));
    }

    [Fact]
    public void PacketReader_BoundsTruncatedSizedStringsAndReadBytes()
    {
        // Truncated packet declaring 0xFFFF-byte string with only 2 bytes remaining ("AB")
        byte[] truncated = { 0xFF, 0xFF, (byte)'A', (byte)'B' };
        var r1 = new PacketReader(truncated);
        Assert.Equal("AB", r1.ReadSizedString());
        Assert.Equal(truncated.Length, r1.GetOffset());

        var r2 = new PacketReader(truncated);
        Assert.Equal("AB", r2.ReadSizedZeroTerminatedString());
        Assert.Equal(truncated.Length, r2.GetOffset());

        // Normal zero-terminated string strips NUL and advances cleanly
        byte[] valid = { 0x04, 0x00, (byte)'N', (byte)'e', (byte)'o', 0x00, 0x77 };
        var r3 = new PacketReader(valid);
        Assert.Equal("Neo", r3.ReadSizedZeroTerminatedString());
        Assert.Equal(6, r3.GetOffset());
        Assert.Equal(0x77u, r3.ReadUint8());

        // ReadBytes guards negative and oversized requests before allocating
        var r4 = new PacketReader(new byte[] { 1, 2, 3 });
        Assert.Empty(r4.ReadBytes(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => r4.ReadBytes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => r4.ReadBytes(4));
        Assert.Equal(new byte[] { 1, 2 }, r4.ReadBytes(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => r4.ReadBytes(2));
    }

    [Fact]
    public void ChatRooms_EnforcesMaxRoomsAndPerRoomListCaps()
    {
        var rooms = new ChatRooms();
        for (int i = 0; i < ChatRooms.MaxRooms; i++)
        {
            Assert.Equal(ChatRooms.Status.Success, rooms.Create("Owner" + i, "Room" + i, "secret", "topic"));
        }

        // Next distinct room hits MaxRooms cap
        Assert.Equal(ChatRooms.Status.RoomFull, rooms.Create("OverflowOwner", "RoomOverflow", "", ""));

        // Per-room WaitingForEntry / Invited / Banned caps
        for (int i = 0; i < ChatRooms.MaxRoomListEntries; i++)
        {
            Assert.Equal(ChatRooms.Status.WaitingForEntry, rooms.Enter("Waiter" + i, "Room0", "wrong", out _));
            Assert.Equal(ChatRooms.Status.Success, rooms.Apply(ChatRooms.Change.Invite, "Owner0", "Room0", "Invitee" + i, true));
            Assert.Equal(ChatRooms.Status.Success, rooms.Apply(ChatRooms.Change.Ban, "Owner0", "Room0", "Banned" + i, true));
        }

        Assert.Equal(ChatRooms.Status.RoomFull, rooms.Enter("WaiterOverflow", "Room0", "wrong", out _));
        Assert.Equal(ChatRooms.Status.RoomFull, rooms.Apply(ChatRooms.Change.Invite, "Owner0", "Room0", "InviteeOverflow", true));
        Assert.Equal(ChatRooms.Status.RoomFull, rooms.Apply(ChatRooms.Change.Ban, "Owner0", "Room0", "BannedOverflow", true));
    }
}
