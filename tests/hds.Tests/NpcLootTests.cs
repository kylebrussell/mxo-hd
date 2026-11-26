using System.Net;
using System.Net.Sockets;
using hds;
using hds.shared;

namespace hds.Tests;

public class NpcLootTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void LootWindowMatchesRetailLayout()
    {
        // Retail: 81 19 00 00 00 00 10 00 f4 3d 00 00 00 00 00 00 03 00
        //         00 00 00 00 a7 1c 00 00 | 00 00 00 00 51 b8 00 00 | 00 00 00 00 c9 09 00 00
        Assert.Equal("811900000000100" + "0f43d0000" + "00000000" + "0300" +
                     "00000000a71c0000" + "0000000051b80000" + "00000000c9090000",
            Hex(NpcLoot.BuildLootWindow(0x3df4, new uint[] { 0x1ca7, 0xb851, 0x09c9 }, new byte[] { 0, 0, 0 })));

        // No items: 81 19 00 00 00 00 10 00 0c 03 00 00 00 00 00 00 00 00
        Assert.Equal("81190000000010000c03000000000000" + "0000",
            Hex(NpcLoot.BuildLootWindow(0x030c, new uint[0], new byte[0])));
    }

    [Fact]
    public void LootReplyMatchesRetailClaimedIdentityCapture()
    {
        // Retail claimed identity list. Client621e2bb0 removes these exact pairs; it is not a remaining-items list.
        Assert.Equal("81200c000000000000000000020000000028d61e000000000000a41c0000",
            Hex(ServerPackets.BuildLootAccepted(new[]
            {
                (new byte[] { 0, 0, 0, 0x28 }, 7894u),
                (new byte[4], 7332u)
            })));
        Assert.Equal("81200c0000000000000000000000",
            Hex(ServerPackets.BuildLootAccepted(Array.Empty<(byte[], uint)>())));
    }

    [Fact]
    public void PartialClaimListsTransferredIdentitiesAndRetainsOnlyFailuresForRetry()
    {
        var items = new List<uint> { 7332, 2399, 7332 };
        int capacity = 1;
        var claimed = NpcLoot.ClaimItems(items, _ => capacity-- > 0);
        Assert.Equal(7332u, Assert.Single(claimed).Item);
        Assert.Equal(new byte[4], claimed[0].Data);
        Assert.Equal(new uint[] { 2399, 7332 }, items);
        Assert.Empty(NpcLoot.ClaimItems(items, _ => false));
        var retry = NpcLoot.ClaimItems(items, _ => true);
        Assert.Equal(new uint[] { 2399, 7332 }, retry.Select(c => c.Item));
        Assert.Empty(items);
        Assert.Empty(NpcLoot.ClaimItems(items, _ => throw new Exception("Duplicate grant")));
    }

    [Fact]
    public void TakingOneItemClaimsOnlyThatCopyAndRetainsItWhenItDoesNotFit()
    {
        var items = new List<uint> { 7332, 2399, 7332 };
        Assert.Empty(NpcLoot.ClaimOne(items, 7332, _ => false));          // full bag: nothing claimed or removed
        Assert.Equal(new uint[] { 7332, 2399, 7332 }, items);
        Assert.Empty(NpcLoot.ClaimOne(items, 1111, _ => throw new Exception("Not in the corpse")));
        var one = Assert.Single(NpcLoot.ClaimOne(items, 7332, _ => true));
        Assert.Equal(7332u, one.Item);
        Assert.Equal(new byte[4], one.Data);
        Assert.Equal(new uint[] { 2399, 7332 }, items);                     // the other copy stays for a later take
        Assert.Equal("81200c000000000000000000010000000000a41c0000",
            Hex(ServerPackets.BuildLootAccepted(new[] { one })));
    }

    [Fact]
    public void AnotherCorpseRecreatesTheClientsLocallyClosedCachedWindow()
    {
        var client=new HeadlessClient.CorpseLootWindowState();
        client.Receive(NpcLoot.BuildLootWindow(1,new uint[]{6791},new byte[]{8}));
        Assert.True(client.Visible);client.CloseLocally();
        client.Receive(NpcLoot.BuildLootWindow(2,new uint[]{6799},new byte[]{8}));
        Assert.False(client.Visible); // The old reply alone reproduces client6202088b's cached return.
        foreach(var reply in NpcLoot.BuildLootPresentation(2,new uint[]{6799},new byte[]{8}))client.Receive(reply);
        Assert.True(client.Visible);
    }

    [Fact]
    public void CodeBitTierFollowsLevel()
    {
        Assert.Equal(7332u, NpcLoot.CodeBitFor(5));   // Code Bit 1
        Assert.Equal(7333u, NpcLoot.CodeBitFor(15));  // Code Bit 2
        Assert.Equal(7339u, NpcLoot.CodeBitFor(99));  // Code Bit 8 at most
    }

    [Fact]
    public void DropsAreAtMostThreeKnownItems()
    {
        var random = new System.Random(7);
        for (int i = 0; i < 200; i++)
        {
            var items = NpcLoot.Roll(25, random);
            Assert.InRange(items.Count, 0, 3);
            Assert.All(items, g => Assert.True(g == NpcLoot.HealthPill1 || (g >= 7332 && g <= 7339)));
        }
    }

    private static (WorldClient Client, string Key, Socket Sender, Socket Receiver) MakeTestClient(out Mob deadMob)
    {
        Store.world = new WorldServer();
        var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        receiver.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        receiver.ReceiveTimeout = 500;
        var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        sender.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var remote = (IPEndPoint)receiver.LocalEndPoint!;
        string key = remote.ToString();
        Store.world.objMan.PushClient(key);
        var client = new WorldClient(remote, sender, key);
        client.playerData.setOnWorld(true);
        client.playerData.setDistrictId(1);
        client.playerData.setCharID(42);
        client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(19403, -705, 60401));
        client.playerInstance.YawInterval.setValue(new byte[] { 0 });
        WorldServer.Clients[key] = client;

        deadMob = new Mob();
        deadMob.setEntityId(990001);
        deadMob.setDistrict(1);
        deadMob.setName("Chopper Runner");
        deadMob.setIsSpawned(true);
        deadMob.setIsDead(true);
        deadMob.setIsLootable(true);
        deadMob.setXPos(19403);
        deadMob.setYPos(-705);
        deadMob.setZPos(60701);
        ClientView view = client.viewMan.GetViewForEntityAndGo(deadMob.getEntityId(), 599);
        view.viewCreated = true;
        view.spawnId = 1;
        return (client, key, sender, receiver);
    }

    private static int DrainDatagrams(Socket receiver)
    {
        int count = 0;
        byte[] buf = new byte[4096];
        while (receiver.Poll(20_000, SelectMode.SelectRead))
        {
            receiver.Receive(buf);
            count++;
        }
        return count;
    }

    [Fact]
    public void DeferredDropRegistersImmediatelyAndAutoOpenFiresOnceOrSkipsAfterManualOpen()
    {
        var (client, key, sender, receiver) = MakeTestClient(out var mob);
        try
        {
            long epoch = client.TransportFence.Capture();
            int worldGen = client.playerData.worldGeneration;
            int combatGen = client.playerData.combatGeneration;

            var corpse = NpcLoot.DropAndRegister(client, mob, 125, 0, new uint[] { NpcLoot.CodeBit1 }, deferPresentation: true);
            Assert.NotNull(corpse);
            Assert.Empty(client.messageQueue.RPCMessagesQueue);
            Assert.Same(corpse, client.playerData.loot.Current);

            Assert.True(NpcLoot.TryPresentAutoOpen(client, corpse, epoch, worldGen, combatGen));
            Assert.Equal(2, client.messageQueue.RPCMessagesQueue.Count); // 81 1a then 81 19
            var rpcs = client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m => Hex(m.content)).ToArray();
            Assert.EndsWith("811a", rpcs[0]);
            Assert.Contains("8119", rpcs[1]);

            // Second fire is a clean no-op.
            Assert.False(NpcLoot.TryPresentAutoOpen(client, corpse, epoch, worldGen, combatGen));
            Assert.Equal(2, client.messageQueue.RPCMessagesQueue.Count);

            // Manual open before auto-open fires suppresses the duplicate auto-open.
            var corpse2 = NpcLoot.DropAndRegister(client, mob, 250, 0, Array.Empty<uint>(), deferPresentation: true);
            NpcLoot.Open(client, mob.getEntityId());
            Assert.False(NpcLoot.TryPresentAutoOpen(client, corpse2, epoch, worldGen, combatGen));
        }
        finally
        {
            WorldServer.Clients.Remove(key);
            sender.Dispose();
            receiver.Dispose();
        }
    }

    [Fact]
    public void AutoOpenIsSuppressedWhenCombatWorldOrFenceChanges()
    {
        var (client, key, sender, receiver) = MakeTestClient(out var mob);
        try
        {
            long epoch = client.TransportFence.Capture();
            int worldGen = client.playerData.worldGeneration;
            int combatGen = client.playerData.combatGeneration;

            var corpse = NpcLoot.DropAndRegister(client, mob, 100, 0, Array.Empty<uint>(), deferPresentation: true);

            // New combat started before delay elapsed: suppress auto-open.
            client.playerData.combatGeneration++;
            Assert.False(NpcLoot.TryPresentAutoOpen(client, corpse, epoch, worldGen, combatGen));
            Assert.Empty(client.messageQueue.RPCMessagesQueue);
            client.playerData.combatGeneration = combatGen;

            // World generation changed: suppress auto-open.
            client.playerData.worldGeneration++;
            Assert.False(NpcLoot.TryPresentAutoOpen(client, corpse, epoch, worldGen, combatGen));
            client.playerData.worldGeneration = worldGen;

            // Transport reset: suppress auto-open.
            client.TransportFence.PublishIncoming(true, () => { });
            Assert.False(NpcLoot.TryPresentAutoOpen(client, corpse, epoch, worldGen, combatGen));
            Assert.Empty(client.messageQueue.RPCMessagesQueue);
        }
        finally
        {
            WorldServer.Clients.Remove(key);
            sender.Dispose();
            receiver.Dispose();
        }
    }

    [Fact]
    public void DeathAndCombatEndForceFlushInSeparatePacketsPastThrottleBeforeDeferredLoot()
    {
        var (client, key, sender, receiver) = MakeTestClient(out var mob);
        try
        {
            var packets = new ServerPackets();
            // Simulate a killing hit flushing immediately before HandleCombatEnd (arms the 50 ms throttle).
            client.messageQueue.addObjectMessage(new byte[] { 0x02, 0x00, 0x02, 0x80, 0x80, 0x80, 0x40, 0xfa, 0x00 }, false);
            client.FlushQueue(false, true);
            Assert.Equal(1, DrainDatagrams(receiver));

            // 1) SendNpcDies must bypass the 50 ms throttle and emit its own datagram immediately.
            packets.SendNpcDies(3, client, mob);
            Assert.True(DrainDatagrams(receiver) >= 1, "SendNpcDies bypassed the 50 ms flush throttle");

            // 2) SendCombatEnd must also bypass the 50 ms throttle and emit its own datagram immediately.
            packets.SendCombatEnd(client, 9, 0x00010003u, client.playerInstance.Position.getValue(), 1);
            Assert.True(DrainDatagrams(receiver) >= 1, "SendCombatEnd bypassed the 50 ms flush throttle");

            // 3) Deferred drop must NOT piggyback 81 1a / 81 19 onto SendCombatEnd.
            var corpse = NpcLoot.DropAndRegister(client, mob, 90, 0, new uint[] { NpcLoot.CodeBit1 }, deferPresentation: true);
            Assert.Equal(0, DrainDatagrams(receiver));
            Assert.DoesNotContain(client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
                m => Hex(m.content).Contains("8119") || Hex(m.content).EndsWith("811a"));

            // 4) Deferred auto-open emits its own packet afterwards.
            Assert.True(NpcLoot.TryPresentAutoOpen(client, corpse, client.TransportFence.Capture(),
                client.playerData.worldGeneration, client.playerData.combatGeneration));
            Assert.True(DrainDatagrams(receiver) >= 1, "Deferred auto-open emitted its own datagram");
        }
        finally
        {
            WorldServer.Clients.Remove(key);
            sender.Dispose();
            receiver.Dispose();
        }
    }
}
