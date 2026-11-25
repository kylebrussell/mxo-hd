using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using NativeQa;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class SneakFreeHarryingDazzlingTests
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort Harrying = HarryingAttackDamage.Id;
    private const ushort Dazzling = DazzlingThrowDamage.Id;

    private static WorldClient Client(uint charId, double x, double y, double z, byte yaw = 64, byte org = 1, bool stealthed = false, ushort style = 0)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setEntityId(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.pvpFlag = true;
        client.playerData.isBot = true;
        if (stealthed) client.playerData.conditionStateFlags = Stealth.StealthedFlag;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)23);
        client.playerInstance.OrganizationID.setValue(org);
        client.playerInstance.Position.setValue(Pos(x, y, z));
        client.playerInstance.YawInterval.setValue(yaw);
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        if (style != 0) CombatStyles.UseStyle(client, style);
        return client;
    }

    private static byte[] Pos(double x, double y, double z)
    {
        byte[] bytes = new byte[24];
        BitConverter.GetBytes(x).CopyTo(bytes, 0);
        BitConverter.GetBytes(y).CopyTo(bytes, 8);
        BitConverter.GetBytes(z).CopyTo(bytes, 16);
        return bytes;
    }

    private static void Track(WorldClient observer, WorldClient target, ushort viewId, ushort spawnId)
    {
        observer.viewMan.views.Add(new ClientView(12, viewId, target.playerData.getEntityId())
        {
            spawnId = spawnId,
            viewCreated = true
        });
    }

    [Theory]
    [InlineData(Harrying)]
    [InlineData(Dazzling)]
    [InlineData(SpySneakOpeners.WeakeningBlow)]
    [InlineData(SpyMasterSneakOpeners.WardingBlow)]
    public void SneakFreeThrowAdmissionEnforcesStyleStealthOpponentFacingSightRangeAndCombat(ushort ability)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "sector_properties.csv")))
            dir = Directory.GetParent(dir)!.FullName;
        SectorProperties.Load(Path.Combine(dir, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(dir, "data", "navmesh"));

        var caster = Client(110, 21550, 1095, 16150, yaw: 64, org: 1, stealthed: false, style: 0);
        var enemy = Client(6114, 21750, 1095, 16150, yaw: 64, org: 2);
        var friend = Client(6111, 21650, 1095, 16250, yaw: 192, org: 1);

        var world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        WorldServer.Clients ??= new Dictionary<string, WorldClient>();
        WorldServer.Clients.Clear();
        WorldServer.Clients["c"] = caster;
        WorldServer.Clients["e"] = enemy;
        WorldServer.Clients["f"] = friend;
        Store.world = world;
        var config = (WorldConfig)RuntimeHelpers.GetUninitializedObject(typeof(WorldConfig));
        config.IsPvpServer = true;
        config.PvpMaxSafeLevel = 15;
        Store.worldConfig = config;

        try
        {
            Track(caster, enemy, 4, 10);
            Track(caster, friend, 5, 11);

            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 10, out uint wrongStyle));
            Assert.Equal(AbilityRefusals.WrongStyle, wrongStyle);

            AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (Spy, 23) });
            CombatStyles.Select(caster, Spy);
            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 10, out uint noStealth));
            Assert.Equal(AbilityRefusals.CasterStateRequired, noStealth);

            caster.playerData.conditionStateFlags = Stealth.StealthedFlag;
            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 5, 11, out uint ally));
            Assert.Equal(AbilityRefusals.InvalidTarget, ally);

            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 99, out uint stale));
            Assert.Equal(AbilityRefusals.InvalidTarget, stale);

            // Rear stand (2 m behind QAEnemy facing east) is admitted.
            Assert.False(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 10, out _));

            // Frontal stand inside QAEnemy's 50-deg cone is refused.
            caster.playerInstance.Position.setValue(Pos(21850, 1095, 16200));
            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 10, out uint frontal));
            Assert.Equal(AbilityRefusals.InvalidTarget, frontal);

            // Solid wall stand refuses with TargetNotInSight (9).
            caster.playerInstance.Position.setValue(Pos(21950, 1095, 16150));
            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 10, out uint blocked));
            Assert.Equal(AbilityRefusals.TargetNotInSight, blocked);

            // Out of range (> 6 m) refuses with TargetOutOfRange (34).
            caster.playerInstance.Position.setValue(Pos(20800, 1095, 16150));
            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 10, out uint far));
            Assert.Equal(AbilityRefusals.TargetOutOfRange, far);

            // Active combat refuses with MustBeOutsideCombat (21).
            caster.playerInstance.Position.setValue(Pos(21550, 1095, 16150));
            var handler = CombatManager.GetHandler(caster);
            handler.isCombatRunning = true;
            Assert.True(SneakFreeThrowAdmission.TryRefuse(caster, ability, 4, 10, out uint inCombat));
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, inCombat);
            handler.isCombatRunning = false;
        }
        finally
        {
            WorldServer.Clients.Clear();
        }
    }

    [Fact]
    public void QaGateAndBatchedModePlanAreValid()
    {
        Assert.True(SneakFreeControlsQa.Allowed("1", "1", "mxotest_sneak", 110, true, false));
        Assert.False(SneakFreeControlsQa.Allowed("0", "1", "mxotest_sneak", 110, true, false));
        Assert.False(SneakFreeControlsQa.Allowed("1", "1", "mxo", 110, true, false));
        Assert.False(SneakFreeControlsQa.Allowed("1", "1", "mxotest_sneak", 2, true, false));

        var plan = NativeQaModes.RequireKnown("ability-parity-harrying-dazzling-throws").Plan;
        Assert.Equal(new ushort[] { Spy, Stealth.SneakGrantAbility, Harrying, Dazzling }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[SneakFreeControlsQa.Mode]);
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(23));
    }
}
