using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class PacketPruneEffectsAndVisibilityTests
{
    private static WorldClient MakeClient(uint charId = 0)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.viewMan = new ViewManager();
        client.messageQueue = new MessageQueue();
        return client;
    }

    private static SequencedMessage MakeModifierRpc(int? world, long transport, int rpcNumber = -1, SequencedMessage? timedView = null)
    {
        // Framed 80 bc modifier RPC (length prefix 0x04 + 80 bc 01 02)
        return new SequencedMessage(new byte[] { 0x04, 0x80, 0xbc, 0x01, 0x02 })
        {
            RpcWorldGeneration = world,
            RpcTransportEpoch = transport,
            rpcNumber = rpcNumber,
            TimedViewWithRpc = timedView
        };
    }

    [Fact]
    public void WorldPacket_PruneUnassignedWorldRpcs_CompactsUpdatesModifiersAndPairedViews()
    {
        var packet = new WorldPacket(new ClientData());
        // Empty fast path
        packet.PruneUnassignedWorldRpcs(world: 1, transport: 1);
        Assert.Empty(packet.RPCMessages);

        var pairedView = new SequencedMessage(new byte[] { 0x02, 0x00, 0x01, 0x02 });
        var staleWithView = MakeModifierRpc(world: 1, transport: 10, rpcNumber: -1, timedView: pairedView);
        var numberedOldWorld = MakeModifierRpc(world: 1, transport: 10, rpcNumber: 5);
        var currentWorld = MakeModifierRpc(world: 2, transport: 11, rpcNumber: -1);
        var nonWorldBound = new SequencedMessage(new byte[] { 0x02, 0x80, 0x01 });

        Assert.True(packet.addRPCContent(staleWithView));
        Assert.True(packet.addRPCContent(numberedOldWorld));
        Assert.True(packet.addRPCContent(currentWorld));
        Assert.True(packet.addRPCContent(nonWorldBound));
        Assert.Single(packet.ObjectMessages);

        packet.PruneUnassignedWorldRpcs(world: 2, transport: 11);

        Assert.Equal(3, packet.RPCMessages.Count);
        Assert.Same(numberedOldWorld, packet.RPCMessages[0]);
        Assert.Same(currentWorld, packet.RPCMessages[1]);
        Assert.Same(nonWorldBound, packet.RPCMessages[2]);
        Assert.Empty(packet.ObjectMessages);

        // Modifier count was decremented for the pruned modifier RPC, so 4 more modifiers fit (2 remaining + 4 = 6).
        for (int i = 0; i < 4; i++)
        {
            Assert.True(packet.addRPCContent(MakeModifierRpc(world: 2, transport: 11)));
        }
        Assert.False(packet.addRPCContent(MakeModifierRpc(world: 2, transport: 11)));
    }

    [Fact]
    public void TimedEffects_ReadOnlyQueriesDoNotAllocateAndActiveCountTracksMutations()
    {
        WorldClient untouchedPlayer = MakeClient(charId: 901);
        var untouchedMob = new Mob();
        untouchedMob.setEntityId(99001);

        Assert.False(TimedEffects.TryGetPlayerEffects(untouchedPlayer, out _));
        Assert.False(TimedEffects.TryGetMobEffects(untouchedMob, out _));
        Assert.True(ControlActions.CanMove(untouchedPlayer));
        Assert.True(ControlActions.CanUseAbilities(untouchedPlayer));
        Assert.True(ControlActions.CanPerformSpecialMove(untouchedPlayer, 100));
        Assert.True(ControlActions.CanMove(untouchedMob));
        Assert.False(ControlActions.Incapacitated(untouchedMob));
        Assert.True(ControlActions.CanUseWeapons(untouchedMob));
        Assert.False(untouchedMob.IsStunned(TimeUtils.GetSimTimeSeconds()));

        // Read-only checks must not have populated the ConditionalWeakTables.
        Assert.False(TimedEffects.TryGetPlayerEffects(untouchedPlayer, out _));
        Assert.False(TimedEffects.TryGetMobEffects(untouchedMob, out _));

        // Now apply a root state, verify control gates and activeCount transitions across Remove/Advance.
        double now = TimeUtils.GetSimTimeSeconds();
        var mobEffects = TimedEffects.ForMob(untouchedMob);
        Assert.True(TimedEffects.TryGetMobEffects(untouchedMob, out _));
        Assert.False(mobEffects.Effects.HasActiveEffects);
        Assert.True(mobEffects.Effects.IsEmpty);

        var root = new TimedEffects.Active
        {
            Target = TempMods.RootedAbility,
            Proc = TempMods.Proc.State,
            Seconds = 10f
        };
        mobEffects.Effects.Apply(parent: 500, new[] { root }, now);

        Assert.True(mobEffects.Effects.HasActiveEffects);
        Assert.False(mobEffects.Effects.IsEmpty);
        Assert.False(ControlActions.CanMove(untouchedMob));
        Assert.False(ControlActions.Incapacitated(untouchedMob));
        Assert.True(ControlActions.CanUseWeapons(untouchedMob));

        // Advancing past expiry clears activeCount back to 0.
        var ticks = new List<TimedEffects.Active>();
        var expired = new List<TimedEffects.Active>();
        mobEffects.Effects.Advance(now + 15.0, ticks, expired);

        Assert.Single(expired);
        Assert.False(mobEffects.Effects.HasActiveEffects);
        Assert.True(mobEffects.Effects.IsEmpty);
        Assert.True(ControlActions.CanMove(untouchedMob));

        // Stun blocks movement, weapons, and sets Incapacitated + Mob.IsStunned.
        var stun = new TimedEffects.Active
        {
            Target = TempMods.StunAbility,
            Proc = TempMods.Proc.State,
            Seconds = 10f
        };
        mobEffects.Effects.Apply(parent: 501, new[] { stun }, now);
        Assert.True(ControlActions.Incapacitated(untouchedMob));
        Assert.False(ControlActions.CanMove(untouchedMob));
        Assert.False(ControlActions.CanUseWeapons(untouchedMob));
        Assert.True(untouchedMob.IsStunned(now + 1.0));

        mobEffects.Effects.RemoveParent(501);
        Assert.False(mobEffects.Effects.HasActiveEffects);
        Assert.False(untouchedMob.IsStunned(now + 1.0));
    }

    [Fact]
    public void ViewManager_CollectVisibleMobs_SkipsStaticNonMobViewsAndKeepsMobs()
    {
        var viewMan = new ViewManager();

        var boothView = viewMan.addView(1001, HardlineBooths.PhoneBoothType);
        boothView.viewCreated = true;
        var subwayView = viewMan.addView(1002, 6568);
        subwayView.viewCreated = true;
        var signpostView = viewMan.addView(1003, 8400);
        signpostView.viewCreated = true;

        var mob = new Mob();
        mob.setEntityId(2001);
        mob.setDistrict(1);
        mob.setIsSpawned(true);
        var mobView = viewMan.addView(2001, 200);
        mobView.viewCreated = true;
        mobView.spawnId = 7;

        var mobs = new Dictionary<ulong, Mob> { [2001] = mob };
        var visible = new HashSet<Mob>();
        var selected = new HashSet<Mob>();

        viewMan.CollectVisibleMobs(mobs, districtId: 1, isDead: false, targetView: mobView.ViewID, targetSpawn: 7, visible, selected);

        Assert.Single(visible);
        Assert.Contains(mob, visible);
        Assert.Single(selected);
        Assert.Contains(mob, selected);
    }

    [Fact]
    public void TryClaimInactiveClient_ClaimsUniqueInactiveAndRejectsDuplicates()
    {
        Dictionary<string, WorldClient> previous = WorldServer.Clients;
        try
        {
            var unique = MakeClient(charId: 401);
            unique.Alive = false;
            var duplicate = MakeClient(charId: 402);
            duplicate.Alive = false;
            var live = MakeClient(charId: 403);
            live.LivenessClock = () => 0u;
            live.Alive = true;

            WorldServer.Clients = new Dictionary<string, WorldClient>
            {
                ["127.0.0.1:4001"] = unique,
                ["127.0.0.1:4002"] = duplicate,
                ["127.0.0.1:4003"] = duplicate,
                ["127.0.0.1:4004"] = live
            };

            Assert.True(WorldThreads.TryClaimInactiveClient(unique, out string claimedKey));
            Assert.Equal("127.0.0.1:4001", claimedKey);
            Assert.False(WorldThreads.TryClaimInactiveClient(unique, out _)); // already claimed
            Assert.False(WorldThreads.TryClaimInactiveClient(duplicate, out _)); // mapped twice
            Assert.False(WorldThreads.TryClaimInactiveClient(live, out _)); // still alive
        }
        finally
        {
            WorldServer.Clients = previous;
        }
    }
}
