using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

public class DesperateSpeedTests
{
    private const ushort DesperateSpeed = 291, HyperSpeed = 12;

    private static WorldClient Client(uint charId = 29101, byte level = 25)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void DesperateSpeedPublishesMovementScale040AndClearsOnExpiry()
    {
        var caster = Client(29101, 25);
        byte[] speedOn = AbilityBuffs.BuildMovementUpdate(0.40f, 0);
        byte[] speedOff = AbilityBuffs.BuildMovementUpdate(0f, 0);

        try
        {
            Assert.True(AbilityBuffs.TryGet(DesperateSpeed, out var def));
            Assert.Equal(15f, def.Seconds);
            Assert.Equal(0.40f, def.MovementScale);

            Assert.True(AbilityBuffs.Apply(caster, DesperateSpeed));
            Assert.Equal(40, AbilityStats.Player(caster, 115));
            Assert.Equal(20, AbilityStats.Player(caster, 877));
            Assert.Equal(20, AbilityStats.Player(caster, 878));
            Assert.Contains(caster.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                m => m.content.AsSpan().IndexOf(speedOn) >= 0);

            var active = caster.playerData.activeBuffs[DesperateSpeed];
            TimedEffects.RunDue(active.Started + 14.99);
            Assert.True(AbilityBuffs.IsActive(caster, DesperateSpeed));

            TimedEffects.RunDue(active.Started + 15.01);
            Assert.False(AbilityBuffs.IsActive(caster, DesperateSpeed));
            Assert.Equal(0, AbilityStats.Player(caster, 115));
            Assert.Equal(0, AbilityStats.Player(caster, 877));
            Assert.Equal(0, AbilityStats.Player(caster, 878));
            Assert.Contains(caster.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                m => m.content.AsSpan().IndexOf(speedOff) >= 0);
        }
        finally
        {
            AbilityBuffs.RemoveAll(caster);
        }
    }

    [Fact]
    public void DesperateSpeedReplacesStackingType27HyperSpeedWithoutZeroDrop()
    {
        var caster = Client(29102, 25);
        byte[] speed040 = AbilityBuffs.BuildMovementUpdate(0.40f, 0);
        byte[] speedOff = AbilityBuffs.BuildMovementUpdate(0f, 0);

        try
        {
            Assert.True(AbilityBuffs.Apply(caster, HyperSpeed));
            Assert.True(AbilityBuffs.IsActive(caster, HyperSpeed));
            caster.messageQueue.ObjectMessagesQueue.Clear();

            Assert.True(AbilityBuffs.Apply(caster, DesperateSpeed));
            Assert.False(AbilityBuffs.IsActive(caster, HyperSpeed));
            Assert.True(AbilityBuffs.IsActive(caster, DesperateSpeed));

            var objects = caster.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>().ToList();
            Assert.Contains(objects, m => m.content.AsSpan().IndexOf(speed040) >= 0);
            Assert.DoesNotContain(objects, m => m.content.AsSpan().IndexOf(speedOff) >= 0);
        }
        finally
        {
            AbilityBuffs.RemoveAll(caster);
        }
    }

    [Fact]
    public void DesperateSpeedCostReuseAndClosureMemoryFitLevel25()
    {
        const byte level = 25;
        ushort[] roots = { 2, 17, DesperateSpeed };
        ushort[] loadout = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().ToArray();
        Assert.True(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));

        Assert.Equal((ushort)50, AbilityCosts.Cost(DesperateSpeed));
        Assert.True(AbilityCooldowns.TryGet(DesperateSpeed, out var cd));
        Assert.Equal(298f, cd.Seconds);
    }
}
