using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class StaticObjectIndexAndLeakTests
{
    private static StaticWorldObject MakeStatic(ushort district, uint id, double x = 0)
    {
        return new StaticWorldObject
        {
            metrId = district,
            mxoStaticId = id,
            staticId = id & 0xFFFFFu,
            sectorID = (ushort)(id >> 20),
            pos_x = x
        };
    }

    [Fact]
    public void StaticObjectIndex_contiguous_districts_uses_zero_overrides_and_preserves_first_entry()
    {
        var index = new StaticObjectIndex();
        index.EnsureCapacity(5000);
        Assert.True(index.TableCapacity >= 6667);

        // Load district 1, then district 2, then district 3 contiguously (matching real CSV boot order).
        var firstD1Id42 = MakeStatic(1, 42, 100);
        var dupD1Id42 = MakeStatic(1, 42, 999);
        var d2Id42 = MakeStatic(2, 42, 200);
        var d2Id99 = MakeStatic(2, 99, 299);
        var d3Id99 = MakeStatic(3, 99, 399);

        index.Add(firstD1Id42);
        index.Add(dupD1Id42);
        for (uint i = 1000; i < 2500; i++) index.Add(MakeStatic(1, i, i));

        index.Add(d2Id42);
        index.Add(d2Id99);
        for (uint i = 1000; i < 2500; i++) index.Add(MakeStatic(2, i, i + 10000));

        index.Add(d3Id99);
        for (uint i = 2000; i < 3000; i++) index.Add(MakeStatic(3, i, i + 20000));

        Assert.Equal(0, index.OutOfOrderOverrideCount);
        Assert.Same(firstD1Id42, index.Find(42, 1));
        Assert.Same(d2Id42, index.Find(42, 2));
        Assert.Null(index.Find(42, 3));
        Assert.Same(firstD1Id42, index.Find(42, null));

        Assert.Same(d2Id99, index.Find(99, 2));
        Assert.Same(d3Id99, index.Find(99, 3));
        Assert.Same(d2Id99, index.Find(99, null));

        Assert.Null(index.Find(999999, null));
        Assert.Null(index.Find(999999, 1));
    }

    [Fact]
    public void StaticObjectIndex_interleaved_districts_matches_reference_dictionary_oracle()
    {
        var index = new StaticObjectIndex();
        var refObjects = new Dictionary<ulong, StaticWorldObject>();
        var refFirst = new Dictionary<uint, ushort>();

        void AddBoth(StaticWorldObject obj)
        {
            index.Add(obj);
            ulong key = ((ulong)obj.metrId << 32) | obj.mxoStaticId;
            refObjects.TryAdd(key, obj);
            refFirst.TryAdd(obj.mxoStaticId, obj.metrId);
        }

        // Targeted tricky permutation: d1 seen first, then d2, then d3 inserts id=5, then d2 inserts id=5, then d1 inserts id=5.
        AddBoth(MakeStatic(1, 1, 1));
        AddBoth(MakeStatic(2, 2, 2));
        AddBoth(MakeStatic(3, 5, 35));
        AddBoth(MakeStatic(2, 5, 25));
        AddBoth(MakeStatic(1, 5, 15));

        // Randomized stress across 6 interleaved districts and 400 IDs (forcing collisions, duplicates, resizes).
        var rng = new Random(1337);
        for (int step = 0; step < 2500; step++)
        {
            ushort district = (ushort)(1 + rng.Next(6));
            uint id = (uint)(1 + rng.Next(400));
            AddBoth(MakeStatic(district, id, step));
        }

        Assert.Equal(refObjects.Count, index.Count);
        for (uint id = 1; id <= 410; id++)
        {
            StaticWorldObject expectedDefault = refFirst.TryGetValue(id, out ushort firstDist)
                ? refObjects[((ulong)firstDist << 32) | id]
                : null;
            Assert.Same(expectedDefault, index.Find(id, null));

            for (ushort d = 1; d <= 7; d++)
            {
                refObjects.TryGetValue(((ulong)d << 32) | id, out StaticWorldObject expected);
                Assert.Same(expected, index.Find(id, d));
            }
        }
    }

    private static WorldClient MakeTestClient()
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(WorldClient).GetField("flushLock", flags)!.SetValue(c, new object());
        typeof(WorldClient).GetField("lastFlushTick", flags)!.SetValue(c, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", flags)!.SetValue(c, new WorldSendFence());
        c.Alive = true;
        c.playerData = new ClientData();
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(23828, 495, 13984));
        c.messageQueue = new MessageQueue();
        return c;
    }

    [Fact]
    public void SubwayHandler_OnDisconnect_removes_open_ride_and_dead_client_stops_countdown()
    {
        WorldClient client = MakeTestClient();

        int before = SubwayHandler.ActiveRideCount;
        Assert.True(SubwayHandler.OpenMap(client, anyDistance: true));
        Assert.Equal(before + 1, SubwayHandler.ActiveRideCount);

        SubwayHandler.OnDisconnect(client);
        Assert.Equal(before, SubwayHandler.ActiveRideCount);

        // Open again, mark client dead, and confirm OnMapOpen cleans up instead of scheduling ticks.
        Assert.True(SubwayHandler.OpenMap(client, anyDistance: true));
        Assert.Equal(before + 1, SubwayHandler.ActiveRideCount);
        client.Alive = false;
        SubwayHandler.OnMapOpen(client);
        Assert.Equal(before, SubwayHandler.ActiveRideCount);
    }

    [Fact]
    public void ClientActionsHandler_prunes_expired_stuck_cooldowns()
    {
        ClientActionsHandler.ClearStuckCooldownsForTesting();
        try
        {
            ClientActionsHandler.RecordStuckCooldownForTesting(1001, 100.0);
            ClientActionsHandler.RecordStuckCooldownForTesting(1002, 350.0);
            Assert.Equal(2, ClientActionsHandler.StuckCooldownCount);

            // At t = 450, char 1001 (age 350s >= 300s) expires; char 1002 (age 100s < 300s) remains.
            ClientActionsHandler.PruneExpiredStuckCooldowns(450.0);
            Assert.Equal(1, ClientActionsHandler.StuckCooldownCount);

            ClientActionsHandler.PruneExpiredStuckCooldowns(700.0);
            Assert.Equal(0, ClientActionsHandler.StuckCooldownCount);
        }
        finally
        {
            ClientActionsHandler.ClearStuckCooldownsForTesting();
        }
    }

    [Fact]
    public void DuelHandler_prunes_expired_declined_cooldowns()
    {
        double baseTime = 10000.0;
        DuelHandler.RecordDeclineForTesting(99001, 99002, baseTime);
        DuelHandler.RecordDeclineForTesting(99003, 99004, baseTime + 50.0);
        int withBoth = DuelHandler.DeclinedCooldownCount;
        Assert.True(withBoth >= 2);

        // At baseTime + RechallengeSeconds + 1, the first decline expires while the second (+50s) stays active.
        DuelHandler.PruneExpiredDeclinesForTesting(baseTime + DuelHandler.RechallengeSeconds + 1.0);
        Assert.True(DuelHandler.DeclinedCooldownCount <= withBoth - 1);

        // At baseTime + 50 + RechallengeSeconds + 1, the second also expires.
        DuelHandler.PruneExpiredDeclinesForTesting(baseTime + 50.0 + DuelHandler.RechallengeSeconds + 1.0);
        Assert.True(DuelHandler.DeclinedCooldownCount <= withBoth - 2);
    }
}
