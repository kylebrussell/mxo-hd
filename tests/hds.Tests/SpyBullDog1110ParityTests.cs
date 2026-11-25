using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyBullDog1110ParityTests
{
    private const ushort BullDog = StealthShieldAbilities.BullDog; // 1110

    private static WorldClient Player(uint charId, byte org, int x, int y, int z, byte yaw = 64, byte level = 20)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData();
        c.playerData.setCharID(charId);
        c.playerData.setEntityId(charId);
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerData.pvpFlag = true;
        c.playerData.isBot = true;
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.Level.setValue(level);
        c.playerInstance.OrganizationID.setValue(org);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
        c.playerInstance.YawInterval.setValue(yaw);
        c.playerInstance.Health.setValue((ushort)5000);
        c.playerInstance.MaxHealth.setValue((ushort)5000);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        c.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(c, long.MaxValue);
        return c;
    }

    private static void Track(WorldClient observer, WorldClient subject, ushort viewId, ushort spawnId)
    {
        observer.viewMan.views.Add(new ClientView(12, viewId, subject.playerData.getEntityId())
        {
            spawnId = spawnId,
            viewCreated = true
        });
    }

    [Fact]
    public void SpyBullDog1110EnforcesRearStealth4mSightAndCombatAdmission()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-bull-dog-1110").Plan;
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(20));

        var caster = Player(110, 1, 21550, 1095, 16150, yaw: 64);
        var enemy = Player(6114, 2, 21750, 1095, 16150, yaw: 64);
        var friend = Player(6111, 1, 21650, 1095, 16250, yaw: 192);
        var frontCaster = Player(110, 1, 21850, 1095, 16200, yaw: 173);
        var wallCaster = Player(110, 1, 21950, 1095, 16150, yaw: 192);
        var farCaster = Player(110, 1, 21100, 1095, 16150, yaw: 64); // 6.5 m > 4 m

        foreach (var c in new[] { caster, frontCaster, wallCaster, farCaster })
        {
            Track(c, enemy, 4, 10);
            Track(c, friend, 5, 11);
        }

        var prevWorld = Store.world;
        var prevWorldCfg = Store.worldConfig;
        WorldServer.Clients ??= new Dictionary<string, WorldClient>();
        var saved = new Dictionary<string, WorldClient>(WorldServer.Clients);
        try
        {
            Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            var cfg = (WorldConfig)RuntimeHelpers.GetUninitializedObject(typeof(WorldConfig));
            cfg.IsPvpServer = true;
            cfg.PvpMaxSafeLevel = 15;
            Store.worldConfig = cfg;
            WorldServer.Clients.Clear();
            WorldServer.Clients["b-c"] = caster;
            WorldServer.Clients["b-e"] = enemy;
            WorldServer.Clients["b-f"] = friend;

            // Empty and ally -> InvalidTarget (24).
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, BullDog, 0, null, out uint emptyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, BullDog, 5, 11, out uint allyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, allyReason);

            // Live opponent without Stealth -> CasterStateRequired (23).
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, BullDog, 4, 10, out uint stealthReason));
            Assert.Equal(AbilityRefusals.CasterStateRequired, stealthReason);

            foreach (var c in new[] { caster, frontCaster, wallCaster, farCaster })
                c.playerData.conditionStateFlags = Stealth.StealthedFlag;

            // Stale spawn, frontal 50-deg cone, solid wall, >4 m range.
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, BullDog, 4, 99, out uint staleReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, staleReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(frontCaster, BullDog, 4, 10, out uint frontReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, frontReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(wallCaster, BullDog, 4, 10, out uint wallReason));
            Assert.Equal(AbilityRefusals.TargetNotInSight, wallReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(farCaster, BullDog, 4, 10, out uint farReason));
            Assert.Equal(AbilityRefusals.TargetOutOfRange, farReason);

            // Rear 2 m stand succeeds.
            Assert.False(SpyAbilityRegistry.TryRefuse(caster, BullDog, 4, 10, out _));

            // Close combat -> MustBeOutsideCombat (21).
            var combat = CombatManager.GetHandler(caster);
            combat.isCombatRunning = true;
            try
            {
                Assert.True(SpyAbilityRegistry.TryRefuse(caster, BullDog, 4, 10, out uint combatReason));
                Assert.Equal(AbilityRefusals.MustBeOutsideCombat, combatReason);
            }
            finally
            {
                combat.isCombatRunning = false;
            }

            Assert.Equal((ushort)10, AbilityCosts.Cost(BullDog));
            Assert.True(AbilityCooldowns.TryGet(BullDog, out var cd));
            Assert.Equal(10f, cd.Seconds);
            Assert.Equal((ushort)691, cd.Group);
            Assert.Equal(15f, cd.GroupSeconds);
        }
        finally
        {
            WorldServer.Clients.Clear();
            foreach (var kv in saved) WorldServer.Clients[kv.Key] = kv.Value;
            Store.world = prevWorld;
            Store.worldConfig = prevWorldCfg;
        }
    }
}
