using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

public class CannyStrikeDamageTests
{
    private const ushort CannyStrike = 295, SpyStyle = 1112;

    private static WorldClient Client(byte level = 22)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(22)]
    [InlineData(35)]
    [InlineData(50)]
    public void CannyStrikeResolvesShippedValueFunction4ThrownBase(int level)
    {
        var source = AbilityCatalog.Find(CannyStrike);
        Assert.NotNull(source);

        var item = new AbilityItem();
        item.setAbilityID(CannyStrike);
        item.setValueFrom(0);
        item.setValueTo(0);

        foreach (int seed in new[] { 1, 42, 987 })
        {
            Assert.True(CannyStrikeDamage.TryResolve(source!, level, new Random(seed), out ushort damage));
            // 625180c5: min=8, max=10, sigma=(10-8)/6=0, center=9 across levels 22..50 -> deterministic 9.
            Assert.Equal((ushort)9, damage);
            Assert.Equal((ushort)9, AbilityHandler.AbilityDamage(item, new Random(seed), characterLevel: level));
        }
    }

    [Fact]
    public void CannyStrikeRefusesOutsideCloseCombatWithNativeInvalidTarget()
    {
        var client = Client(22);
        client.playerData.setCharID(29501);

        Assert.True(CannyStrikeDamage.TryRefuse(client, CannyStrike, 0, null, out uint emptyReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);

        Assert.True(CannyStrikeDamage.TryRefuse(client, CannyStrike, 4, 1, out uint outOfCombatReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, outOfCombatReason);

        Assert.False(CannyStrikeDamage.TryRefuse(client, 287, 0, null, out _));
    }

    [Fact]
    public void CannyStrikeFullRank22ClosureDamageAndTimersMatchLivePipeline()
    {
        const byte level = 22, rank = 22;
        ushort[] roots = { 2, 17, SpyStyle, CannyStrike };
        ushort[] loadout = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().ToArray();
        Assert.True(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));

        var caster = Client(level);
        caster.playerData.setCharID(29502);
        AbilityPassives.SetLoaded(caster, loadout.Select(id => (id, (ushort)rank)).ToArray());
        CombatStyles.Select(caster, SpyStyle);

        var item = new AbilityItem();
        item.setAbilityID(CannyStrike);
        item.setValueFrom(0);
        item.setValueTo(0);

        ushort baseDamage = AbilityHandler.AbilityDamage(item, new Random(1), characterLevel: level);
        Assert.Equal((ushort)9, baseDamage);

        // Rank 22 closure: Spy Style 1112 (+15%) + Weak Points 164 (+4%) + Wicked Aim 278 (+6%) = +25%.
        Assert.Equal(25, AbilityStats.Influence(caster, 1034));
        ushort landed = AbilityStats.ScaleAbilityDamage(caster, CannyStrike, baseDamage);
        Assert.Equal((ushort)11, landed);

        Assert.Equal((ushort)70, AbilityCosts.Cost(CannyStrike));
        Assert.Equal(0f, AbilityCatalog.Find(CannyStrike)!.CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(CannyStrike, out var cd));
        Assert.Equal(30f, cd.Seconds);
        Assert.Equal((ushort)0, cd.Group);
    }
}
