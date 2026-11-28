using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class BroadcastFilterAndDisconnectCleanupTests
{
    private static void SetField(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static WorldClient MakeClient(uint charId, ulong entityId, string handle, uint districtId, bool onWorld = true, bool withViewMan = true)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.Alive = true;
        SetField(c, "flushLock", new object());
        SetField(c, "lastFlushTick", long.MaxValue);
        SetField(c, "<TransportFence>k__BackingField", new WorldSendFence());
        c.playerData = new ClientData();
        c.playerData.setCharID(charId);
        c.playerData.setEntityId(entityId);
        c.playerData.setDistrictId(districtId);
        c.playerData.setOnWorld(onWorld);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.CharacterName.setValue(handle);
        c.messageQueue = new MessageQueue();
        c.viewMan = withViewMan ? new ViewManager() : null!;
        return c;
    }

    [Fact]
    public void SendViewUpdateToClientsWhoHasMobSpawned_PreFiltersDistrictAndOnWorldBeforeViewLookup()
    {
        var server = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        var sameDistrict = MakeClient(101, 5001, "SameMobViewer", districtId: 2, onWorld: true, withViewMan: true);
        var otherDistrict = MakeClient(102, 5002, "OtherDistrictViewer", districtId: 3, onWorld: true, withViewMan: false);
        var offWorld = MakeClient(103, 5003, "OffWorldViewer", districtId: 2, onWorld: false, withViewMan: false);

        var mob = new Mob();
        mob.setEntityId(99001);
        mob.setDistrict(2);

        ClientView view = sameDistrict.viewMan.addView(mob.getEntityId(), 12);
        view.viewCreated = true;

        var previousClients = WorldServer.Clients;
        try
        {
            WorldServer.Clients = new Dictionary<string, WorldClient>
            {
                ["same"] = sameDistrict,
                ["other"] = otherDistrict,
                ["off"] = offWorld
            };

            var pak = new PacketContent();
            pak.AddHexBytes("020e0100");
            // otherDistrict and offWorld have null viewMan: pre-filtering must skip them without throwing NRE.
            server.SendViewUpdateToClientsWhoHasMobSpawned(pak, mob, deferFlush: true);

            Assert.Single(sameDistrict.messageQueue.ObjectMessagesQueue);
            Assert.Empty(otherDistrict.messageQueue.ObjectMessagesQueue);
            Assert.Empty(offWorld.messageQueue.ObjectMessagesQueue);
        }
        finally
        {
            WorldServer.Clients = previousClients;
        }
    }

    [Fact]
    public void SendViewUpdateToClientWhoHasStaticObjectSpawned_PreFiltersBotsOffWorldAndOtherDistricts()
    {
        var server = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        var sameDistrict = MakeClient(201, 6001, "SameStaticViewer", districtId: 1, onWorld: true, withViewMan: true);
        var otherDistrict = MakeClient(202, 6002, "OtherStaticViewer", districtId: 2, onWorld: true, withViewMan: false);
        var offWorld = MakeClient(203, 6003, "OffWorldStaticViewer", districtId: 1, onWorld: false, withViewMan: false);
        var bot = MakeClient(204, 6004, "BotStaticViewer", districtId: 1, onWorld: true, withViewMan: false);
        SetField(bot, "<IsBot>k__BackingField", true);
        bot.playerData.isBot = true;

        ulong staticEntityId = 88001;
        var staticObj = new StaticWorldObject { metrId = 1 };
        ClientView view = sameDistrict.viewMan.addView(staticEntityId, 6568);
        view.viewCreated = true;

        var previousClients = WorldServer.Clients;
        try
        {
            WorldServer.Clients = new Dictionary<string, WorldClient>
            {
                ["same"] = sameDistrict,
                ["other"] = otherDistrict,
                ["off"] = offWorld,
                ["bot"] = bot
            };

            var pak = new PacketContent();
            pak.AddHexBytes("01020304");
            // otherDistrict, offWorld, and bot have null viewMan: pre-filtering must skip them without NRE.
            server.SendViewUpdateToClientWhoHasStaticObjectSpawned(pak, staticObj, staticEntityId);

            Assert.Single(sameDistrict.messageQueue.ObjectMessagesQueue);
            Assert.Empty(otherDistrict.messageQueue.ObjectMessagesQueue);
            Assert.Empty(offWorld.messageQueue.ObjectMessagesQueue);
            Assert.Empty(bot.messageQueue.ObjectMessagesQueue);
        }
        finally
        {
            WorldServer.Clients = previousClients;
        }
    }

    [Fact]
    public void SocialHandler_FindOnlineAndOnlineWhere_DoNotHoldWorldServerClientsLockDuringPredicate()
    {
        var alice = MakeClient(301, 7001, "AliceOnline", districtId: 1, onWorld: true);
        var bob = MakeClient(302, 7002, "BobOffline", districtId: 1, onWorld: false);
        var carol = MakeClient(303, 7003, "CarolOnline", districtId: 2, onWorld: true);

        var previousClients = WorldServer.Clients;
        try
        {
            WorldServer.Clients = new Dictionary<string, WorldClient>
            {
                ["a"] = alice,
                ["b"] = bob,
                ["c"] = carol
            };

            bool lockHeldInFind = false;
            WorldClient? found = SocialHandler.FindOnline(c =>
            {
                if (Monitor.IsEntered(WorldServer.Clients)) lockHeldInFind = true;
                return c.playerData.getCharID() == 303;
            });
            Assert.False(lockHeldInFind);
            Assert.Same(carol, found);

            bool lockHeldInWhere = false;
            List<WorldClient> online = SocialHandler.OnlineWhere(c =>
            {
                if (Monitor.IsEntered(WorldServer.Clients)) lockHeldInWhere = true;
                return true;
            });
            Assert.False(lockHeldInWhere);
            Assert.Equal(2, online.Count);
            Assert.Contains(alice, online);
            Assert.Contains(carol, online);
            Assert.DoesNotContain(bob, online);
        }
        finally
        {
            WorldServer.Clients = previousClients;
        }
    }

    [Fact]
    public void CoderPets_ClearSimulacraCounter_EvictsOnlyDisconnectedCharacter()
    {
        uint charA = 900101;
        uint charB = 900102;
        try
        {
            CoderPets.ClearSimulacraCounter(charA);
            CoderPets.ClearSimulacraCounter(charB);
            int baseline = CoderPets.SimulacraCounterCount;

            CoderPets.SetSimulacraCounterForTesting(charA, 7);
            CoderPets.SetSimulacraCounterForTesting(charB, 9);
            Assert.Equal(baseline + 2, CoderPets.SimulacraCounterCount);

            CoderPets.ClearSimulacraCounter(0);
            Assert.Equal(baseline + 2, CoderPets.SimulacraCounterCount);

            CoderPets.ClearSimulacraCounter(charA);
            Assert.Equal(baseline + 1, CoderPets.SimulacraCounterCount);
        }
        finally
        {
            CoderPets.ClearSimulacraCounter(charA);
            CoderPets.ClearSimulacraCounter(charB);
        }
    }

    [Fact]
    public void SendSelfViewUpdateToTarget_DeliversToMatchingEntityWithoutThrowing()
    {
        var server = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        var sender = MakeClient(401, 8001, "FxSender", districtId: 1, onWorld: true);
        var target = MakeClient(402, 8002, "FxTarget", districtId: 1, onWorld: true);

        ClientView view = sender.viewMan.addView(target.playerData.getEntityId(), 12);
        view.viewCreated = true;

        var previousClients = WorldServer.Clients;
        try
        {
            WorldServer.Clients = new Dictionary<string, WorldClient>
            {
                ["sender"] = sender,
                ["target"] = target
            };

            var pak = new PacketContent();
            pak.AddHexBytes("02000102");
            server.SendSelfViewUpdateToTarget(pak, view.ViewID, sender);

            Assert.Single(target.messageQueue.ObjectMessagesQueue);
            Assert.Empty(sender.messageQueue.ObjectMessagesQueue);
        }
        finally
        {
            WorldServer.Clients = previousClients;
        }
    }
}
