using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class ViewIdWraparoundAndBroadcastTests
{
    private static void SetField(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static WorldClient MakeClient(uint charId, ulong entityId, string handle, uint districtId)
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
        c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.CharacterName.setValue(handle);
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        return c;
    }

    [Fact]
    public void ViewManager_WrapsPastUInt16MaxWithoutAssigningReservedIdsOrColliding()
    {
        var vm = new ViewManager();

        ClientView v4 = vm.addView(100, 12);
        ClientView v5 = vm.addView(101, 12);
        ClientView v6 = vm.addView(102, 12);
        Assert.Equal((ushort)4, v4.ViewID);
        Assert.Equal((ushort)5, v5.ViewID);
        Assert.Equal((ushort)6, v6.ViewID);

        // Remove 6 while keeping 4 and 5 live
        vm.removeViewByViewId(6);
        Assert.Null(vm.getViewById(6));
        Assert.Same(v4, vm.getViewById(4));
        Assert.Same(v5, vm.getViewById(5));

        // Advance near UInt16.MaxValue and wrap around
        vm.SetCurrentViewIdForTesting(UInt16.MaxValue - 1);
        ClientView vMax = vm.addView(200, 12);
        Assert.Equal(UInt16.MaxValue, vMax.ViewID);

        // Next allocation must wrap past 65535, skip reserved 0..3, skip live 4 & 5, and land on 6
        ClientView wrapped = vm.addView(201, 12);
        Assert.Equal((ushort)6, wrapped.ViewID);
        Assert.Same(wrapped, vm.getViewById(6));

        ClientView afterWrap = vm.addView(202, 12);
        Assert.Equal((ushort)7, afterWrap.ViewID);

        // After Clear(), wrapping from MaxValue allocates FirstDynamicViewId (4)
        vm.Clear();
        vm.SetCurrentViewIdForTesting(UInt16.MaxValue);
        ClientView afterClear = vm.addView(300, 12);
        Assert.Equal(ViewManager.FirstDynamicViewId, afterClear.ViewID);
    }

    [Fact]
    public void ViewList_DirectByViewIdAndDuplicateViewIdPreservesFirstMatchOrder()
    {
        var list = new ViewList();
        var a = new ClientView(12, 10, 1000);
        var b = new ClientView(12, 10, 1001); // duplicate ViewID exercises lazy multiByViewId
        var c = new ClientView(12, 11, 1002);

        list.Add(a);
        Assert.Same(a, list.FirstWithViewId(10));

        list.Add(b);
        list.Add(c);
        Assert.Same(a, list.FirstWithViewId(10));
        Assert.Same(c, list.FirstWithViewId(11));

        // Removing first promotes second
        list.Remove(a);
        Assert.Same(b, list.FirstWithViewId(10));

        // Inserting at index 0 triggers Rebuild() and preserves order
        var d = new ClientView(12, 10, 1003);
        list.Insert(0, d);
        Assert.Same(d, list.FirstWithViewId(10));

        list.Remove(d);
        Assert.Same(b, list.FirstWithViewId(10));

        list.Remove(b);
        Assert.Null(list.FirstWithViewId(10));
        Assert.Same(c, list.FirstWithViewId(11));
    }

    [Fact]
    public void SendMovementUpdateToAllPlayers_SkipsOutOfDistrictViewers()
    {
        var oldClients = WorldServer.Clients;
        var oldCurrent = Store.currentClient;
        try
        {
            var mover = MakeClient(1, 5000, "Mover", districtId: 1);
            var sameDistrictViewer = MakeClient(2, 5001, "NearViewer", districtId: 1);
            var otherDistrictViewer = MakeClient(3, 5002, "FarViewer", districtId: 2);

            ClientView vNear = sameDistrictViewer.viewMan.GetViewForEntityAndGo(5000, 12);
            vNear.viewCreated = true;
            ClientView vFar = otherDistrictViewer.viewMan.GetViewForEntityAndGo(5000, 12);
            vFar.viewCreated = true;

            WorldServer.Clients = new Dictionary<string, WorldClient>
            {
                ["m"] = mover,
                ["n"] = sameDistrictViewer,
                ["f"] = otherDistrictViewer
            };
            Store.currentClient = mover;

            // Valid SelfViewUpdate with yaw=42 (flags=0x04)
            byte[] raw = { SelfViewUpdate.FlagYaw, 42 };
            Assert.True(SelfViewUpdate.TryParse(raw, 0, out SelfViewUpdate state));

            var world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            world.SendMovementUpdateToAllPlayers(state, charId: 1, entityId: 5000);

            Assert.Equal(1, sameDistrictViewer.messageQueue.ObjectMessagesQueue.Count);
            Assert.Equal(0, otherDistrictViewer.messageQueue.ObjectMessagesQueue.Count);
        }
        finally
        {
            Store.currentClient = oldCurrent;
            WorldServer.Clients = oldClients;
        }
    }

    [Fact]
    public void SendRPCToPlayerList_UsesHashSetSafelyAndHandlesEmptyOrNullEntries()
    {
        var oldClients = WorldServer.Clients;
        try
        {
            var alice = MakeClient(10, 6001, "Alice", districtId: 1);
            var bob = MakeClient(11, 6002, "Bob", districtId: 1);

            WorldServer.Clients = new Dictionary<string, WorldClient>
            {
                ["a"] = alice,
                ["b"] = bob
            };

            var world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            byte[] rpc = { 0x81, 0x10, 0x01 };

            world.SendRPCToPlayerList(null!, rpc);
            world.SendRPCToPlayerList(new ArrayList(), rpc);

            var list = new ArrayList
            {
                new Hashtable { ["handle"] = null },
                new Hashtable { ["handle"] = "Alice" }
            };
            world.SendRPCToPlayerList(list, rpc);

            Assert.Equal(1, alice.messageQueue.RPCMessagesQueue.Count);
            Assert.Equal(0, bob.messageQueue.RPCMessagesQueue.Count);
        }
        finally
        {
            WorldServer.Clients = oldClients;
        }
    }
}
