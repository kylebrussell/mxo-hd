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
public class NeuroDartParityTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort NeuroDart = MasterKnifeThrowerBatch.NeuroDart;
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static WorldClient Client(uint charId, double x, double y, double z, int alignment = 1, bool pvpFlag = true,
        ushort[]? loaded = null, ushort style = 0)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setEntityId(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.pvpFlag = pvpFlag;
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(FixtureLevel);
        client.playerInstance.OrganizationID.setValue((byte)alignment);
        client.playerInstance.Position.setValue(Pos(x, y, z));
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)900);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        if (loaded != null)
            AbilityPassives.SetLoaded(client, loaded.Select(id => (id, (ushort)FixtureRank)).ToArray());
        if (style != 0)
            CombatStyles.UseStyle(client, style);
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

    private static WorldServer PrepareWorld(params WorldClient[] clients)
    {
        string root = FindRepoRoot();
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        WorldServer.Clients ??= new Dictionary<string, WorldClient>();
        WorldServer.Clients.Clear();
        for (int i = 0; i < clients.Length; i++)
            WorldServer.Clients[$"neuro-dart-{i}"] = clients[i];
        Store.world = world;
        var config = (WorldConfig)RuntimeHelpers.GetUninitializedObject(typeof(WorldConfig));
        config.IsPvpServer = true;
        config.PvpMaxSafeLevel = 15;
        Store.worldConfig = config;
        return world;
    }

    private static ClientView TrackPlayer(WorldClient observer, WorldClient target, ushort viewId, ushort spawnId)
    {
        var view = new ClientView(12, viewId, target.playerData.getEntityId())
        {
            spawnId = spawnId,
            viewCreated = true
        };
        observer.viewMan.views.Add(view);
        return view;
    }

    [Fact]
    public void NeuroDartUsesGuardedCastAndEnforcesHostileSightRangeCombatAndStaleSpawn()
    {
        Assert.True(MasterKnifeThrowerBatch.UsesGuardedCast(NeuroDart));

        var caster = Client(110, 17000, 495, 6400, alignment: 1);
        var enemy = Client(6114, 16800, 495, 6400, alignment: 2);
        var friend = Client(6111, 17150, 495, 6400, alignment: 1);
        var farEnemy = Client(6115, 17000, 495, 8401, alignment: 2);
        var wallCaster = Client(111, 28650, 895, 12700, alignment: 1);
        var wallEnemy = Client(6113, 29150, 895, 12700, alignment: 2);

        try
        {
            PrepareWorld(caster, enemy, friend, farEnemy, wallCaster, wallEnemy);
            TrackPlayer(caster, enemy, 4, 10);
            TrackPlayer(caster, friend, 5, 11);
            TrackPlayer(caster, farEnemy, 6, 12);
            TrackPlayer(wallCaster, wallEnemy, 7, 13);

            // Cleared selection, friendly target, and stale spawn all refuse with InvalidTarget (24).
            Assert.True(MasterKnifeThrowerBatch.TryRefuse(caster, NeuroDart, 0, null, out uint emptyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);

            Assert.True(MasterKnifeThrowerBatch.TryRefuse(caster, NeuroDart, 5, 11, out uint friendReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, friendReason);

            Assert.True(MasterKnifeThrowerBatch.TryRefuse(caster, NeuroDart, 4, 99, out uint staleReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, staleReason);

            // Beyond 16 m refuses with TargetOutOfRange (34); across solid wall refuses with TargetNotInSight (9).
            Assert.True(MasterKnifeThrowerBatch.TryRefuse(caster, NeuroDart, 6, 12, out uint farReason));
            Assert.Equal(AbilityRefusals.TargetOutOfRange, farReason);

            Assert.True(MasterKnifeThrowerBatch.TryRefuse(wallCaster, NeuroDart, 7, 13, out uint wallReason));
            Assert.Equal(AbilityRefusals.TargetNotInSight, wallReason);

            // Valid clear hostile within 16 m is admitted out of combat.
            Assert.False(MasterKnifeThrowerBatch.TryRefuse(caster, NeuroDart, 4, 10, out _));

            // Moving enemy beyond 16 m before cast completion causes recheck to refuse (fizzle path).
            enemy.playerInstance.Position.setValue(Pos(17000, 495, 8401));
            Assert.True(MasterKnifeThrowerBatch.TryRefuse(caster, NeuroDart, 4, 10, out uint movedReason));
            Assert.Equal(AbilityRefusals.TargetOutOfRange, movedReason);

            // In combat refuses with MustBeOutsideCombat (21).
            enemy.playerInstance.Position.setValue(Pos(16800, 495, 6400));
            var handler = CombatManager.GetHandler(caster);
            handler.isCombatRunning = true;
            Assert.True(MasterKnifeThrowerBatch.TryRefuse(caster, NeuroDart, 4, 10, out uint combatReason));
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, combatReason);
            handler.isCombatRunning = false;
        }
        finally
        {
            WorldServer.Clients.Clear();
        }
    }

    [Fact]
    public void NeuroDartDebuffsAreConsumedAndDotScalesWithThrownInfluenceAndResistance()
    {
        var plan = NativeQaModes.RequireKnown("ability-parity-neuro-dart-parity").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var caster = Client(110, 17000, 495, 6400, loaded: plan.Abilities.ToArray(), style: SpyStyle);
        var victim = Client(6114, 16800, 495, 6400, alignment: 2);

        Assert.Equal(39, AbilityStats.Influence(caster, 1034));
        Assert.Equal((ushort)366, AbilityStats.ScaleAbilityDamage(caster, NeuroDart, 264));
        Assert.Equal(61, TempMods.DotTick(366, 15f, 2.5f));
        Assert.Equal((ushort)50, TypedAbilityResistance.Amount(61, FixtureLevel, FixtureLevel, 75));

        int meleeAtkBefore = AbilityStats.Attack(victim, ranged: false);
        int rangedAtkBefore = AbilityStats.Attack(victim, ranged: true);
        int meleeDefBefore = AbilityStats.Defense(victim, ranged: false);
        int rangedDefBefore = AbilityStats.Defense(victim, ranged: true);
        int forceBefore = AbilityStats.Player(victim, 519);

        var subject = TimedEffects.ForPlayer(victim);
        Assert.True(TempMods.TryGet(NeuroDart, out var effect));
        TimedEffects.Apply(subject, effect, 100.0, caster, "CSR_Tester");

        var active = subject.Effects.Snapshot();
        Assert.Equal(9, active.Count);
        Assert.Contains(active, m => m.Target == 115 && m.Value == -57 && m.Seconds == 15f);
        Assert.Contains(active, m => m.Target == 877 && m.Value == -85 && m.Seconds == 15f);
        Assert.Contains(active, m => m.Target == 878 && m.Value == -72 && m.Seconds == 15f);
        Assert.Contains(active, m => m.Target == 519 && m.Value == -20 && m.Seconds == 15f);
        Assert.Contains(active, m => m.Target == 522 && m.Value == -15 && m.Seconds == 15f);
        Assert.Contains(active, m => m.Target == 524 && m.Value == -15 && m.Seconds == 15f);
        Assert.Contains(active, m => m.Target == 1009 && m.Value == -13 && m.Seconds == 15f);
        Assert.Contains(active, m => m.Target == 1010 && m.Value == -21 && m.Seconds == 15f);
        var dot = Assert.Single(active, m => m.Proc == TempMods.Proc.Dot);
        Assert.Equal(366, dot.Value);
        Assert.Equal(61, dot.TickDamage);
        Assert.Equal(2.5, dot.Interval);

        // Runtime stat consumers reflect all combat debuffs while active.
        Assert.Equal(meleeAtkBefore - 15, AbilityStats.Attack(victim, ranged: false));
        Assert.Equal(rangedAtkBefore - 15, AbilityStats.Attack(victim, ranged: true));
        Assert.Equal(meleeDefBefore - 13, AbilityStats.Defense(victim, ranged: false));
        Assert.Equal(rangedDefBefore - 21, AbilityStats.Defense(victim, ranged: true));
        Assert.Equal(forceBefore - 20, AbilityStats.Player(victim, 519));

        // Clean removal restores all stats.
        TimedEffects.RemoveParent(subject, NeuroDart);
        Assert.Empty(subject.Effects.Snapshot());
        Assert.Equal(meleeAtkBefore, AbilityStats.Attack(victim, ranged: false));
        Assert.Equal(rangedAtkBefore, AbilityStats.Attack(victim, ranged: true));
        Assert.Equal(meleeDefBefore, AbilityStats.Defense(victim, ranged: false));
        Assert.Equal(rangedDefBefore, AbilityStats.Defense(victim, ranged: true));
        Assert.Equal(forceBefore, AbilityStats.Player(victim, 519));
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "sector_properties.csv")))
            dir = Directory.GetParent(dir)!.FullName;
        return dir;
    }
}
