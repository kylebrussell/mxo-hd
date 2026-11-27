using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using hds.databases;
using hds.databases.interfaces;
using hds.resources.gameobjects;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class DisconnectCleanupAndTimersTests : IDisposable
{
    private readonly Dictionary<string, WorldClient> oldClients = WorldServer.Clients;
    private readonly DatabaseManager oldDatabase = Store.dbManager;
    private readonly WorldServer oldWorld = Store.world;
    private readonly MarginServer oldMargin = Store.margin;
    private readonly ArrayList oldMobsSnapshot;

    public DisconnectCleanupAndTimersTests()
    {
        WorldServer.Clients = new Dictionary<string, WorldClient>();
        lock (WorldServer.mobs.SyncRoot)
        {
            oldMobsSnapshot = new ArrayList(WorldServer.mobs);
        }
    }

    public void Dispose()
    {
        WorldServer.Clients = oldClients;
        Store.dbManager = oldDatabase;
        Store.world = oldWorld;
        Store.margin = oldMargin;
        lock (WorldServer.mobs.SyncRoot)
        {
            WorldServer.mobs.Clear();
            foreach (object? item in oldMobsSnapshot) WorldServer.mobs.Add(item);
        }
    }

    public class NoOpWorldDbProxy : DispatchProxy
    {
        public uint LastOfflineCharId;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IWorldDBHandler.SetOnlineStatus) && args?.Length >= 1 && args[0] is uint cid)
            {
                LastOfflineCharId = cid;
            }
            if (targetMethod?.ReturnType == typeof(ArrayList)) return new ArrayList();
            if (targetMethod?.ReturnType.IsValueType == true && targetMethod.ReturnType != typeof(void))
            {
                return Activator.CreateInstance(targetMethod.ReturnType);
            }
            return null;
        }
    }

    private static void SetField(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static WorldClient MakeClient(uint charId, ulong entityId, string name, bool alive = true)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.Alive = alive;
        SetField(c, "lastUsedTime", TimeUtils.getUnixTimeUint32());
        SetField(c, "processLock", new object());
        SetField(c, "receivedPackets", new Queue<byte[]>());
        SetField(c, "packetWorkerRunning", true); // prevent ThreadPool worker from racing during unit test
        SetField(c, "flushLock", new object());
        SetField(c, "lastFlushTick", long.MaxValue);
        SetField(c, "<TransportFence>k__BackingField", new WorldSendFence());
        c.playerData = new ClientData();
        c.playerData.setCharID(charId);
        c.playerData.setEntityId(entityId);
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.CharacterName.setValue(StringUtils.stringToBytes(name));
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        return c;
    }

    [Fact]
    public void CleanDeadPlayersDoesNotDesyncOutOfRangeViewsAndCleansUpLeaks()
    {
        var world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        world.objMan = new ObjectManager();
        Store.world = world;

        var margin = (MarginServer)RuntimeHelpers.GetUninitializedObject(typeof(MarginServer));
        SetField(margin, "listLock", new object());
        SetField(margin, "clientList", new List<MarginClient>());
        Store.margin = margin;

        var dbProxy = DispatchProxy.Create<IWorldDBHandler, NoOpWorldDbProxy>();
        Store.dbManager = new DatabaseManager { WorldDbHandler = dbProxy };

        const string deadKey = "127.0.0.1:41001";
        world.objMan.PushClient(deadKey);
        var deadClient = MakeClient(7001, 880001UL, "Leaver", alive: true);
        deadClient.playerInstance = world.objMan.GetAssignedObject(deadKey);
        deadClient.playerInstance.CharacterName.setValue(StringUtils.stringToBytes("Leaver"));
        lock (WorldServer.gameServerEntities.SyncRoot)
        {
            WorldServer.gameServerEntities.Add(deadClient.playerInstance);
        }

        // Queue a pooled ingress datagram before stopping so CleanupOnDisconnect must drain it.
        byte[] rented = ArrayPool<byte>.Shared.Rent(64);
        deadClient.QueuePooledPacket(rented, 32);
        Assert.Equal(1, deadClient.PendingPacketCount);
        deadClient.Alive = false;

        var inRangeViewer = MakeClient(7002, 880002UL, "Nearby");
        var deadView = inRangeViewer.viewMan.addView(deadClient.playerData.getEntityId(), 12);
        deadView.viewCreated = true;

        var outOfRangePlayer = MakeClient(7003, 880003UL, "FarAway");
        Assert.Equal(0, outOfRangePlayer.viewMan.Count);

        WorldServer.Clients[deadKey] = deadClient;
        WorldServer.Clients["127.0.0.1:41002"] = inRangeViewer;
        WorldServer.Clients["127.0.0.1:41003"] = outOfRangePlayer;

        var deadList = new List<WorldClient> { deadClient };
        var allList = new List<WorldClient> { deadClient, inRangeViewer, outOfRangePlayer };

        typeof(WorldThreads).GetMethod("CleanDeadPlayers", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { deadList, allList });

        // Out-of-range player must NOT get a bogus ClientView or ViewID bump or delete packet.
        Assert.Equal(0, outOfRangePlayer.viewMan.Count);
        Assert.Empty(outOfRangePlayer.messageQueue.ObjectMessagesQueue);
        ClientView nextAllocated = outOfRangePlayer.viewMan.addView(999999UL, 12);
        Assert.Equal((ushort)4, nextAllocated.ViewID);

        // In-range viewer must receive exactly one delete packet and have the dead view removed.
        Assert.Null(inRangeViewer.viewMan.FindViewForEntity(deadClient.playerData.getEntityId()));
        Assert.Single(inRangeViewer.messageQueue.ObjectMessagesQueue);

        // Disconnect registries and pooled buffers must be cleaned up.
        Assert.False(WorldServer.Clients.ContainsKey(deadKey));
        Assert.Null(world.objMan.GetAssignedObject(deadKey));
        lock (WorldServer.gameServerEntities.SyncRoot)
        {
            Assert.False(WorldServer.gameServerEntities.Contains(deadClient.playerInstance));
        }
        Assert.Equal(0, deadClient.PendingPacketCount);
        Assert.Equal(7001u, ((NoOpWorldDbProxy)(object)dbProxy).LastOfflineCharId);
    }

    [Fact]
    public void WorldServerPruneStaleEndpointCacheEvictsDeadAndClaimedEntries()
    {
        var world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        var cache = new Dictionary<EndPoint, WorldClient>();
        SetField(world, "clientsByEndpoint", cache);
        SetField(world, "staleEndpointScratch", new List<EndPoint>());

        var active = MakeClient(1, 1001UL, "Active", alive: true);
        var stopped = MakeClient(2, 1002UL, "Stopped", alive: false);
        var claimed = MakeClient(3, 1003UL, "Claimed", alive: true);
        SetField(claimed, "reapClaimed", true);

        cache[new IPEndPoint(IPAddress.Loopback, 50001)] = active;
        cache[new IPEndPoint(IPAddress.Loopback, 50002)] = stopped;
        cache[new IPEndPoint(IPAddress.Loopback, 50003)] = claimed;
        cache[new IPEndPoint(IPAddress.Loopback, 50004)] = null!;

        Assert.Equal(4, world.EndpointCacheCount);
        world.PruneStaleEndpointCache();
        Assert.Equal(1, world.EndpointCacheCount);
        Assert.True(cache.ContainsKey(new IPEndPoint(IPAddress.Loopback, 50001)));
    }

    [Fact]
    public void TimersThreadCheckAndResendIsolatesFaultsSkipsIdleAndStaggersInitialSave()
    {
        var broken = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient)); // messageQueue is null -> throws
        var idle = MakeClient(10, 2001UL, "Idle");
        var bot = MakeClient(11, 2002UL, "Bot");
        SetField(bot, "<IsBot>k__BackingField", true);
        bot.messageQueue.addObjectMessage(new byte[] { 0x01, 0x02 }, false);

        // Must not throw when a broken client is present, and must skip idle + bot cleanly.
        WorldThreads.CheckAndResendClients(new List<WorldClient> { null!, broken, idle, bot });
        Assert.Empty(idle.messageQueue.ObjectMessagesQueue);
        Assert.Single(bot.messageQueue.ObjectMessagesQueue);

        // Initial save time staggers deterministically across [now - 19, now] without underflow.
        var seen = new HashSet<uint>();
        const uint now = 1_700_000_000u;
        for (uint cid = 0; cid < 20; cid++)
        {
            uint initial = WorldThreads.ComputeInitialSaveTime(now, cid);
            Assert.InRange(initial, now - 19, now);
            seen.Add(initial);
        }
        Assert.Equal(20, seen.Count);
        Assert.Equal(1u, WorldThreads.ComputeInitialSaveTime(5u, 10u));
    }

    [Fact]
    public void MobVisibilityCacheDirtyTrackingRefreshesAllVisibilityMutations()
    {
        var mob1 = new Mob();
        mob1.setEntityId(500001UL);
        mob1.setDistrict(1);
        mob1.xBase = 1000; mob1.yBase = 500; mob1.zBase = 1000;
        mob1.setXPos(1000); mob1.setYPos(500); mob1.setZPos(1000);
        mob1.setIsSpawned(true);

        var mob2 = new Mob();
        mob2.setEntityId(500002UL);
        mob2.setDistrict(1);
        mob2.xBase = 2000; mob2.yBase = 500; mob2.zBase = 2000;
        mob2.setXPos(2000); mob2.setYPos(500); mob2.setZPos(2000);
        mob2.setIsSpawned(true);

        lock (WorldServer.mobs.SyncRoot)
        {
            WorldServer.mobs.Clear();
            WorldServer.mobs.Add(mob1);
            WorldServer.mobs.Add(mob2);
        }

        var cache = new WorldThreads.MobVisibilityCache();
        cache.UpdateFromWorld();
        Assert.Equal(2, cache.Mobs.Count);
        Assert.Equal(1000f, cache.Entries[0].X);
        Assert.True(cache.Entries[0].CanIntroduce);
        Assert.True(cache.Entries[0].Spawned);

        // No-op pass preserves exact versions.
        int v1Before = cache.Entries[0].VisibilityVersion;
        int v2Before = cache.Entries[1].VisibilityVersion;
        cache.UpdateFromWorld();
        Assert.Equal(v1Before, cache.Entries[0].VisibilityVersion);
        Assert.Equal(v2Before, cache.Entries[1].VisibilityVersion);

        // Move mob1 across a spatial grid cell boundary; mob2 stays untouched.
        mob1.setXPos(12000);
        mob1.setZPos(12000);
        cache.UpdateFromWorld();
        Assert.Equal(12000f, cache.Entries[0].X);
        Assert.Equal(12000f, cache.Entries[0].Z);
        Assert.Equal(v2Before, cache.Entries[1].VisibilityVersion);
        var cells = new List<List<int>>();
        cache.IndexGrids[1].CollectNeighborCells(12000f, 12000f, 5000f, cells);
        Assert.Contains(0, cells.SelectMany(c => c));

        // Death via TryClaimDeath flips CanIntroduce to false; ScriptedCorpse flips it back to true.
        Assert.True(mob1.TryClaimDeath());
        cache.UpdateFromWorld();
        Assert.False(cache.Entries[0].CanIntroduce);

        mob1.ScriptedCorpse = true;
        cache.UpdateFromWorld();
        Assert.True(cache.Entries[0].CanIntroduce);

        // Despawn & district change & ResetForRespawn all propagate immediately.
        mob1.setIsSpawned(false);
        cache.UpdateFromWorld();
        Assert.False(cache.Entries[0].Spawned);

        mob1.setDistrict(2);
        cache.UpdateFromWorld();
        Assert.Equal((ushort)2, cache.Entries[0].District);
        Assert.True(cache.IndexGrids.ContainsKey(2));

        mob1.ScriptedCorpse = false;
        mob1.ResetForRespawn();
        cache.UpdateFromWorld();
        Assert.Equal(1000f, cache.Entries[0].X);
        Assert.Equal(1000f, cache.Entries[0].Z);
        Assert.True(cache.Entries[0].CanIntroduce);
    }
}
