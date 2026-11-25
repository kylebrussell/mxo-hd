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
public class SpyInstantSneakOpenersTests
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort SpinningBacksweep = 152, Suplex = 287, BullDog = 387;

    private static WorldClient Player(uint charId, byte alignment, int x, int y, int z, byte yaw = 64, byte level = 50)
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
        c.playerInstance.OrganizationID.setValue(alignment);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
        c.playerInstance.YawInterval.setValue(yaw);
        c.playerInstance.Health.setValue((ushort)5000);
        c.playerInstance.MaxHealth.setValue((ushort)5000);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        c.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(c, long.MaxValue);
        return c;
    }

    private static void RegisterView(WorldClient observer, WorldClient subject, ushort viewId, ushort spawnId)
    {
        observer.viewMan.views.Add(new ClientView(12, viewId, subject.playerData.getEntityId())
        {
            spawnId = spawnId,
            viewCreated = true
        });
    }

    [Theory]
    [InlineData(SpinningBacksweep, 28, 75, 158, 130, 20, 30f)]
    [InlineData(Suplex, 120, 300, 646, 531, 50, 38f)]
    [InlineData(BullDog, 75, 950, 1578, 1298, 37, 60f)]
    [InlineData(SpyMasterSneakOpeners.DivingFrankensteiner, 164, 725, 1369, 1126, 80, 60f)]
    public void InstantSneakOpenersEnforceRearAdmissionAndResolveExactLevel50Totals(
        ushort abilityId, ushort expectedBase, int expectedStealthBonus, ushort expectedNeutral, ushort expectedResisted, ushort expectedIs, float expectedOwn)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        Assert.True(SpyInstantSneakOpenerAdmission.Handles(abilityId));
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-instant-sneak-openers").Plan;
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));

        var caster = Player(110, 1, 21550, 1095, 16150, yaw: 64);
        var enemy = Player(6114, 2, 21750, 1095, 16150, yaw: 64);
        var friend = Player(6111, 1, 21650, 1095, 16250, yaw: 192);
        var frontCaster = Player(110, 1, 21850, 1095, 16200, yaw: 173);
        var wallCaster = Player(110, 1, 21950, 1095, 16150, yaw: 192);
        var farCaster = Player(110, 1, 18650, 1095, 16150, yaw: 64);

        foreach (var c in new[] { caster, frontCaster, wallCaster, farCaster })
        {
            RegisterView(c, enemy, 4, 10);
            RegisterView(c, friend, 5, 11);
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
            WorldServer.Clients["s-c"] = caster;
            WorldServer.Clients["s-e"] = enemy;
            WorldServer.Clients["s-f"] = friend;

            // Missing Spy Style -> WrongStyle (13).
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, abilityId, 4, 10, out uint styleReason));
            Assert.Equal(AbilityRefusals.WrongStyle, styleReason);

            foreach (var c in new[] { caster, frontCaster, wallCaster, farCaster })
            {
                AbilityPassives.SetLoaded(c, plan.Abilities.Select(a => (a, (ushort)50)));
                CombatStyles.Select(c, Spy);
            }

            // Missing Stealth -> CasterStateRequired (23).
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, abilityId, 4, 10, out uint stealthReason));
            Assert.Equal(AbilityRefusals.CasterStateRequired, stealthReason);

            foreach (var c in new[] { caster, frontCaster, wallCaster, farCaster })
                c.playerData.conditionStateFlags = Stealth.StealthedFlag;

            // Empty, ally, stale, frontal (50-deg cone), solid wall, >6 m range.
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, abilityId, 0, null, out uint emptyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, abilityId, 5, 11, out uint allyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, allyReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, abilityId, 4, 99, out uint staleReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, staleReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(frontCaster, abilityId, 4, 10, out uint frontReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, frontReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(wallCaster, abilityId, 4, 10, out uint wallReason));
            Assert.Equal(AbilityRefusals.TargetNotInSight, wallReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(farCaster, abilityId, 4, 10, out uint farReason));
            Assert.Equal(AbilityRefusals.TargetOutOfRange, farReason);

            // Rear stand succeeds.
            Assert.False(SpyAbilityRegistry.TryRefuse(caster, abilityId, 4, 10, out _));

            // Combat refusal.
            var combat = CombatManager.GetHandler(caster);
            combat.isCombatRunning = true;
            try
            {
                Assert.True(SpyAbilityRegistry.TryRefuse(caster, abilityId, 4, 10, out uint combatReason));
                Assert.Equal(AbilityRefusals.MustBeOutsideCombat, combatReason);
            }
            finally
            {
                combat.isCombatRunning = false;
            }

            // Exact Level-50 damage, stealth bonus, influence, resistance, cost, and timers.
            var item = new AbilityItem();
            item.setAbilityID(abilityId);
            ushort resolvedBase = AbilityHandler.AbilityDamage(item, new Random(1), characterLevel: 50);
            Assert.Equal(expectedBase, resolvedBase);
            Assert.Equal(expectedStealthBonus, SneakAttacks.Bonus(abilityId, true));
            Assert.Equal(54, AbilityStats.Influence(caster, 1034));
            ushort neutral = AbilityStats.ScaleAbilityDamage(caster, abilityId, SneakAttacks.Add(resolvedBase, abilityId, true));
            Assert.Equal(expectedNeutral, neutral);
            Assert.Equal(expectedResisted, TypedAbilityResistance.Amount(neutral, 50, 50, 75));
            Assert.Equal(expectedIs, AbilityCosts.Cost(abilityId));
            Assert.True(AbilityCooldowns.TryGet(abilityId, out var cd));
            Assert.Equal(expectedOwn, cd.Seconds);
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
