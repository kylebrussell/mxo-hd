using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[CollectionDefinition("SniperParityWorld", DisableParallelization = true)]
public class SniperParityWorldCollection { }

[Collection("SniperParityWorld")]
public class SniperParityTests
{
    private sealed class FixedRoll(double roll) : Random
    {
        public override double NextDouble() => roll;
    }

    private static WorldClient Client()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.Health.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)200);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(504, .30f, .10f)]
    [InlineData(505, .50f, .01f)]
    public void ConcealedAndSniperShotsUseTheirOwnDelayedStunProbabilities(ushort id, float chance, float delay)
    {
        var missed = TimedEffects.ForPlayer(Client());
        var hit = TimedEffects.ForPlayer(Client());
        Assert.True(TempMods.TryGet(id, out var effect));
        var marker = Assert.Single(effect.Mods);
        Assert.Equal(delay, marker.Seconds);
        Assert.Equal(chance, marker.Chance);
        Assert.Equal(4, marker.MinValue);
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.Empty(TimedEffects.Apply(missed, effect, now, rng: new FixedRoll(chance)));
        Assert.Single(TimedEffects.Apply(hit, effect, now, rng: new FixedRoll(chance - .01)));
        TimedEffects.RunDue(now + delay - .001);
        Assert.False(hit.Effects.Has(TempMods.StunAbility));
        TimedEffects.RunDue(now + delay + .001);
        Assert.False(missed.Effects.Has(TempMods.StunAbility));
        Assert.Equal(4, Assert.Single(hit.Effects.Snapshot(), active => active.Target == TempMods.StunAbility).Seconds);
        Assert.Equal(4, Assert.Single(hit.Effects.Snapshot(), active => active.Parent == TempMods.ControlImmunityTimerAbility).Seconds);
        TimedEffects.RunDue(now + delay + 4.01);
        Assert.True(hit.Effects.IsEmpty);
    }

    [Theory]
    [InlineData(495, 1072, new[] { 106, 524, 171, 1010 }, new[] { 110, 6, 6, 7 })]
    [InlineData(508, 1136, new[] { 106, 524, 171, 1010 }, new[] { 60, 10, 4, 1 })]
    [InlineData(143, 1061, new[] { 106, 180, 181 }, new[] { 50, 20, 20 })]
    public void GunmanClassUpgradesApplyTheirOwnStatsAndRemoveThemOnReplacement(
        ushort parent, ushort upgrade, int[] stats, int[] values)
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new[] { (parent, (ushort)50) }, true);
        Assert.True(AbilityPassives.IsLoaded(client, upgrade));
        Assert.Same(client, Assert.Single(AbilitySupport.Targets(client, upgrade, 123)));
        AbilitySupport.Land(client, upgrade, 123);
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(values[i], AbilityBuffs.Total(client, (ushort)stats[i]));
        Assert.Equal(500 + values[0], NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1));
        if (upgrade == 1061)
        {
            Assert.Equal(20, AbilityStats.Toughness(client, false));
            Assert.Equal(20, AbilityStats.Toughness(client, true));
        }
        else
        {
            Assert.True(AbilityStats.Attack(client, true) >= values[1]);
            Assert.True(AbilityStats.Influence(client, 171) >= values[2]);
            Assert.Equal(values[3], AbilityStats.Defense(client, true));
        }

        Assert.True(AbilityBuffs.Apply(client, 1060));
        Assert.False(AbilityBuffs.IsActive(client, upgrade));
        Assert.Equal(500, NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1));
        Assert.True(AbilityBuffs.Remove(client, 1060));
    }
}
