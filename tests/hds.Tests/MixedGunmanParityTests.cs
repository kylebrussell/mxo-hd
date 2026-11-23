using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[CollectionDefinition("MixedGunmanParity", DisableParallelization = true)]
public class MixedGunmanParityCollection { }

[Collection("MixedGunmanParity")]
public class MixedGunmanParityTests
{
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
    [InlineData(245, new[] { 1046 }, new[] { 1 }, 1071)]
    [InlineData(246, new[] { 1139, 1144 }, new[] { 6, 6 }, 0)]
    [InlineData(251, new[] { 1140 }, new[] { 1 }, 1073)]
    [InlineData(252, new[] { 996, 1139 }, new[] { 5, 6 }, 0)]
    [InlineData(408, new[] { 163, 183 }, new[] { 200, 200 }, 0)]
    [InlineData(491, new[] { 1143, 1139, 1068, 1047 }, new[] { 5, 5, 5, 10 }, 0)]
    [InlineData(495, new[] { 1046, 995, 1139 }, new[] { 1, 2, 4 }, 1072)]
    [InlineData(497, new[] { 1046, 1027, 1042 }, new[] { 2, 5, 2 }, 1076)]
    public void LearnedGunmanAndLegacyPassivesPublishAndRemoveTheirStoredTotals(
        ushort id, int[] stats, int[] values, ushort grant)
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new[] { (id, (ushort)50) }, true);
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(values[i], AbilityPassives.Total(client, (ushort)stats[i]));
        if (grant > 0)
            Assert.True(AbilityPassives.IsLoaded(client, grant));
        if (id is 246 or 252)
            Assert.Equal(105, AbilityStats.ScaleTypedAbilityDamage(client, 1, 100));
        if (id == 246)
            Assert.Equal(13, AbilityStats.Toughness(client, false));
        if (id == 251)
            Assert.Equal(2, AbilityStats.Toughness(client, true));
        if (id == 252)
            Assert.Equal(2, AbilityStats.Attack(client, true));
        if (id == 408)
        {
            Assert.True(AbilityStats.Attack(client, false) > 0);
            var incoming = new AbilityItem();
            incoming.setAbilityID(330);
            Assert.True(AbilityStats.SpecialDefense(client, incoming) > 0);
        }

        AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>(), true);
        foreach (int stat in stats)
            Assert.Equal(0, AbilityPassives.Total(client, (ushort)stat));
        if (grant > 0)
            Assert.False(AbilityPassives.IsLoaded(client, grant));
    }

    [Theory]
    [InlineData(false, false, 0, 99)]
    [InlineData(true, false, 1, 100)]
    [InlineData(false, true, 2, 101)]
    [InlineData(true, true, 3, 102)]
    public void SpecialistAndDuelistFeedTheirTypedConsumersAndDesperation(bool specialist, bool duelist,
        int initiative, int snapshot)
    {
        var client = Client();
        client.playerInstance.Health.setValue((ushort)1);
        var selected = new[] { ((ushort)495, specialist), ((ushort)497, duelist) };
        AbilityPassives.SetLoaded(client, selected.Where(entry => entry.Item2).Select(entry => (entry.Item1, (ushort)50)), true);

        Assert.True(AbilityGrantRegistration.Handles(495, 1072));
        Assert.True(AbilityGrantRegistration.Handles(497, 1076));
        Assert.Equal(specialist, AbilityPassives.IsLoaded(client, 1072));
        Assert.Equal(duelist, AbilityPassives.IsLoaded(client, 1076));
        Assert.Equal(initiative, AbilityStats.Player(client, 1046));
        Assert.True(AbilityScaledBuffs.Resolve(client, 1149, out var desperation));
        var modifier = Assert.Single(desperation.Modifiers);
        Assert.Equal(1044, modifier.Stat);
        Assert.Equal(snapshot, modifier.Value);
        Assert.Equal(20f, desperation.Seconds);

        Assert.Equal(specialist ? 111 : 107, AbilityStats.ScaleTypedAbilityDamage(client, 1, 107));
        Assert.Equal(107, AbilityStats.ScaleTypedAbilityDamage(client, 0, 107));
        Assert.Equal(duelist ? 2 : 0, AbilityStats.Defense(client, true));
        Assert.Equal(0, AbilityStats.Defense(client, false));
        Assert.Equal(specialist ? 4.08f : 4f, AbilityControlDurations.Scale(client, TempMods.StunAbility, 4), 3);
        Assert.Equal(duelist ? 4.08f : 4f, AbilityControlDurations.Scale(client, TempMods.PowerlessAbility, 4), 3);
        Assert.Equal(4f, AbilityControlDurations.Scale(client, TempMods.RootedAbility, 4));
    }

    [Theory]
    [InlineData(245, 1071, 6, 4, 4)]
    [InlineData(251, 1073, 7, 2, 3)]
    public void ClassGunmanGrantsApplyOnlyToTheCasterUntilRemoved(
        ushort discipline, ushort upgrade, int accuracy, int damage, int defense)
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new[] { (discipline, (ushort)50) }, true);
        Assert.True(AbilityPassives.IsLoaded(client, upgrade));
        Assert.Same(client, Assert.Single(AbilitySupport.Targets(client, upgrade, 123)));
        AbilitySupport.Land(client, upgrade, 123);
        Assert.Equal(590, NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(accuracy, AbilityStats.Attack(client, true));
        Assert.Equal(defense, AbilityStats.Defense(client, true));
        Assert.Equal(damage, AbilityStats.Influence(client, 171));
        Assert.Equal(0, AbilityStats.Influence(client, 159));
        Assert.True(AbilityBuffs.Remove(client, upgrade));
        Assert.Equal(500, NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(0, AbilityStats.Attack(client, true));
        Assert.Equal(0, AbilityStats.Defense(client, true));
        Assert.Equal(0, AbilityStats.Influence(client, 171));
    }

    [Theory]
    [InlineData(207, 464, 100)]
    [InlineData(208, 463, 100)]
    [InlineData(547, 432, 100)]
    [InlineData(1022, 464, 750)]
    [InlineData(1023, 432, 750)]
    [InlineData(1024, 463, 750)]
    public void ExileScriptDamageOnlyMultipliesForTheMatchingTargetGrant(ushort id, ushort weakness, int damage)
    {
        var target = new Mob();
        foreach (ushort candidate in new ushort[] { 0, 432, 463, 464 })
        {
            CombatSpecials.SetWeakness(target, candidate);
            Assert.Equal((ushort?)(candidate == weakness ? damage : 10), CombatSpecials.Damage(id, target));
        }
    }

    [Theory]
    [InlineData(207, 0)]
    [InlineData(208, 1)]
    [InlineData(547, 0)]
    [InlineData(1022, 0)]
    [InlineData(1023, 0)]
    [InlineData(1024, 1)]
    public void ExileDirectObjectDamageUsesTheScriptedMeleeOrRangedType(ushort id, int type)
    {
        var attacker = Client();
        var victim = Client();
        Assert.Equal(type, TypedAbilityResistance.StoredType(id));
        Assert.Equal(-1, TypedAbilityResistance.StoredType(id, dot: true));
        AbilityPassives.SetLoaded(attacker, new[] { ((ushort)252, (ushort)50) }, true);
        Assert.Equal(type == 1 ? 105 : 100, AbilityStats.ScaleAbilityDamage(attacker, id, 100));
        Assert.True(AbilityBuffs.Apply(victim, 1));
        int applied = TypedAbilityResistance.Player(attacker, victim, id, 100);
        if (type == 1) Assert.InRange(applied, 1, 99);
        else Assert.Equal(100, applied);
        Assert.True(AbilityBuffs.Remove(victim, 1));
    }

    [Fact]
    public void FullAutoReduxReducesBothAccuracyTypesForEightSeconds()
    {
        var target = Client();
        Assert.True(TempMods.TryGet(496, out var effect));
        var effects = TimedEffects.ForPlayer(target);
        var mods = TimedEffects.Apply(effects, effect, TimeUtils.GetSimTimeSeconds());
        Assert.Equal(-15, AbilityStats.Attack(target, true));
        Assert.Equal(-15, AbilityStats.Attack(target, false));
        Assert.All(mods, modifier => Assert.Equal(8, modifier.Seconds));
        TimedEffects.RunDue(mods.Max(modifier => modifier.Expires) + .01);
        Assert.Equal(0, AbilityStats.Attack(target, true));
        Assert.Equal(0, AbilityStats.Attack(target, false));
    }
}
