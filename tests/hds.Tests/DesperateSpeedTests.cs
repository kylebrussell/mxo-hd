using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

[CollectionDefinition("DesperateSpeedExpiry", DisableParallelization = true)]
public class DesperateSpeedExpiryCollection { }

[Collection("DesperateSpeedExpiry")]
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

            // Registration happens during Apply, after Active.Started is sampled.
            double registeredBy = TimeUtils.GetSimTimeSeconds();
            var subject = TimedEffects.ForPlayer(caster);
            var active = caster.playerData.activeBuffs[DesperateSpeed];
            subject.Due(active.Started + def.Seconds - 0.01);
            Assert.True(AbilityBuffs.IsActive(caster, DesperateSpeed));

            subject.Due(registeredBy + def.Seconds + 0.01);
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

    [Theory]
    [InlineData(86)]
    [InlineData(130)]
    public void TimedMovementDebuffComposesWithExistingSpeedAndRestoresIt(ushort ability)
    {
        var player = Client();
        var subject = TimedEffects.ForPlayer(player);
        Assert.True(TempMods.TryGet(ability, out var effect));
        try
        {
            Assert.True(AbilityBuffs.Apply(player, HyperSpeed));
            TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds());
            Assert.Equal(-.05f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.Contains(player.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                message => message.content.AsSpan().IndexOf(Convert.FromHexString("0200028008CDCC4CBD")) >= 0);
            Assert.StartsWith("02000288058C1ECDCC4CBD", Convert.ToHexString(ServerPackets.BuildHyperSpeedUpdate(60, 1,
                scale: AbilityBuffs.WithTimedMovementDebuffs(player, .45f))));
            Assert.True(TimedEffects.RemoveParent(subject, ability));
            Assert.Equal(.45f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
        }
        finally { AbilityBuffs.RemoveAll(player); }
    }

    [Theory]
    [InlineData(12, .45f, .12f, 600)]
    [InlineData(323, .25f, .05f, 720)]
    [InlineData(423, .45f, .10f, 960)]
    public void MovementBuffProjectsTheSelectedStanceWithoutStackingChannels(ushort ability, float run, float stealth, int duration)
    {
        var player = Client(level: 50);
        try
        {
            Assert.True(AbilityBuffs.Apply(player, ability));
            Assert.Equal(run, Scale());
            Assert.True(AbilityBuffs.TryGet(ability, out var buff));
            Assert.Equal(duration, buff.Seconds);
            Assert.Equal((int)(stealth * 100 + .5), AbilityStats.Player(player, 877));
            Assert.Equal((int)(stealth * 100 + .5), AbilityStats.Player(player, 878));
            Assert.NotNull(Stealth.Begin(player, Stealth.Defs[Stealth.SneakGrantAbility], 25));
            Assert.Equal(stealth, Scale());
            Assert.True(Stealth.End(player, "test stance", false));
            Assert.Equal(run, Scale());
            Assert.NotNull(Stealth.Begin(player, Stealth.Defs[Stealth.InvisibilityGrantAbility], 25));
            Assert.Equal(stealth, Scale());
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + duration + .01);
            Assert.False(AbilityBuffs.IsActive(player, ability));
            Assert.Equal(0, Scale());
        }
        finally
        {
            Stealth.End(player, "test cleanup", false);
            AbilityBuffs.RemoveAll(player);
        }

        float Scale() => BitConverter.ToSingle(player.playerInstance.MovementScale.getValue());
    }

    [Fact]
    public void SprintStartAndRemovalPreserveTheActiveSlowContribution()
    {
        var player = Client();
        var subject = TimedEffects.ForPlayer(player);
        Assert.True(TempMods.TryGet(86, out var effect));
        try
        {
            TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds());
            Assert.True(AbilityBuffs.Apply(player, 184));
            Assert.Equal(-.35f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(AbilityBuffs.Remove(player, 184));
            Assert.Equal(-.50f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(TimedEffects.RemoveParent(subject, 86));
            Assert.Equal(0f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
        }
        finally { AbilityBuffs.RemoveAll(player); }
    }

    [Fact]
    public void RemovingHinderingShotPreservesActiveSlow()
    {
        var player = Client();
        var subject = TimedEffects.ForPlayer(player);
        Assert.True(TempMods.TryGet(130, out var hindering));
        Assert.True(TempMods.TryGet(86, out var slow));
        try
        {
            double now = TimeUtils.GetSimTimeSeconds();
            TimedEffects.Apply(subject, hindering, now);
            TimedEffects.Apply(subject, slow, now);
            Assert.Equal(-1f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(TimedEffects.RemoveParent(subject, 130));
            Assert.Equal(-.50f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(TimedEffects.RemoveParent(subject, 86));
            Assert.Equal(0f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
        }
        finally { AbilityBuffs.RemoveAll(player); }
    }

    [Theory]
    [InlineData(176)]
    [InlineData(254)]
    public void ChanceRootOverridesBothSpeedAndSlowAndRestoresTheEffectsStillActive(ushort ability)
    {
        var player = Client();
        var subject = TimedEffects.ForPlayer(player);
        Assert.True(TempMods.TryGet(ability, out var root));
        Assert.True(TempMods.TryGet(86, out var slow));
        try
        {
            Assert.True(AbilityBuffs.Apply(player, HyperSpeed));
            double now = TimeUtils.GetSimTimeSeconds();
            TimedEffects.Apply(subject, root, now, rng: new RootRoll());
            Assert.False(ControlActions.CanMove(subject.Effects));
            Assert.Equal(-1f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.Contains(player.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                message => message.content.AsSpan().IndexOf(Convert.FromHexString("0200028008000080BF")) >= 0);

            TimedEffects.Apply(subject, slow, now);
            Assert.True(AbilityBuffs.Remove(player, HyperSpeed));
            Assert.True(AbilityBuffs.Apply(player, 184));
            Assert.Equal(-1f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            TimedEffects.RunDue(now + 8.01);
            Assert.True(ControlActions.CanMove(subject.Effects));
            Assert.Equal(-.35f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(AbilityBuffs.Remove(player, 184));
            Assert.Equal(-.50f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(TimedEffects.RemoveParent(subject, 86));
            Assert.Equal(0f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
        }
        finally { AbilityBuffs.RemoveAll(player); }
    }

    [Fact]
    public void RemovingOneChanceRootKeepsTheOtherRootAndARefusedProcCannotRefreshIt()
    {
        var player = Client();
        var subject = TimedEffects.ForPlayer(player);
        Assert.True(TempMods.TryGet(176, out var covering));
        Assert.True(TempMods.TryGet(254, out var immobilizing));
        Assert.True(TempMods.TryGet(40, out var freeze));
        try
        {
            Assert.True(AbilityBuffs.Apply(player, HyperSpeed));
            double now = TimeUtils.GetSimTimeSeconds();
            Assert.Empty(TimedEffects.Apply(subject, covering, now, rng: new RootRoll(.2)));
            Assert.Equal(.45f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            TimedEffects.Apply(subject, freeze, now);
            Assert.False(ControlActions.CanMove(subject.Effects));
            Assert.Equal(.45f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(TimedEffects.RemoveParent(subject, 40));

            var first = Assert.Single(TimedEffects.Apply(subject, covering, now, rng: new RootRoll()));
            Assert.Empty(TimedEffects.Apply(subject, covering, now + 2, rng: new RootRoll(.2)));
            Assert.Same(first, Assert.Single(subject.Effects.Snapshot()));
            TimedEffects.Apply(subject, immobilizing, now, rng: new RootRoll());
            Assert.True(TimedEffects.RemoveParent(subject, 176));
            Assert.False(ControlActions.CanMove(subject.Effects));
            Assert.Equal(-1f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
            Assert.True(TimedEffects.RemoveParent(subject, 254));
            Assert.True(ControlActions.CanMove(subject.Effects));
            Assert.Equal(.45f, BitConverter.ToSingle(player.playerInstance.MovementScale.getValue()));
        }
        finally { AbilityBuffs.RemoveAll(player); }
    }

    private sealed class RootRoll(double value = 0) : Random
    {
        public override double NextDouble() => value;
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
