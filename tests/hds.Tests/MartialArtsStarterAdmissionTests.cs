using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

public class MartialArtsStarterAdmissionTests
{
    private const ushort Aikido = 101, KungFu = 133, GuardBreaker = 138, IronGuard = 139;

    private static WorldClient Client(byte level = 12)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)2000);
        client.playerInstance.MaxHealth.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(GuardBreaker)]
    [InlineData(IronGuard)]
    public void MartialArtsStarterSpecialsRefuseOutsideInterlockViaRegistry(ushort abilityId)
    {
        var client = Client(12);
        client.playerData.setCharID(13801);

        Assert.True(SpyAbilityRegistry.TryRefuse(client, abilityId, 0, null, out uint emptyReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);

        Assert.True(SpyAbilityRegistry.TryRefuse(client, abilityId, 4, 1, out uint outOfCombatReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, outOfCombatReason);
    }

    [Fact]
    public void MartialArtsStarterFullRank12ClosureDamageAndTimersMatchLivePipeline()
    {
        const byte level = 12, rank = 12;
        ushort[] roots = { 2, 17, Aikido, KungFu, GuardBreaker, IronGuard };
        ushort[] loadout = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().ToArray();
        Assert.True(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));

        var caster = Client(level);
        caster.playerData.setCharID(13802);
        AbilityPassives.SetLoaded(caster, loadout.Select(id => (id, (ushort)rank)).ToArray());

        // Iron Guard (139) in Aikido (101) at rank 12: Aikido MeleeDamage (+9%) + MA Expertise (+1%) = +10% -> trunc(12 * 1.10) = 13.
        CombatStyles.Select(caster, Aikido);
        var ironGuardItem = new AbilityItem();
        ironGuardItem.setAbilityID(IronGuard);
        ushort ironBase = AbilityHandler.AbilityDamage(ironGuardItem, new Random(1), characterLevel: level);
        Assert.Equal((ushort)12, ironBase);
        Assert.Equal((ushort)13, AbilityStats.ScaleAbilityDamage(caster, IronGuard, ironBase));
        Assert.Equal((ushort)25, AbilityCosts.Cost(IronGuard));
        Assert.True(AbilityCooldowns.TryGet(IronGuard, out var ironCd));
        Assert.Equal(10f, ironCd.Seconds);

        // Guard Breaker (138) in Kung-Fu (133) at rank 12: Kung-Fu MeleeDamage (+9%) + MA Expertise (+1%) = +10% -> trunc(36 * 1.10) = 39.
        CombatStyles.Select(caster, KungFu);
        var guardBreakerItem = new AbilityItem();
        guardBreakerItem.setAbilityID(GuardBreaker);
        ushort gbBase = AbilityHandler.AbilityDamage(guardBreakerItem, new Random(1), characterLevel: level);
        Assert.Equal((ushort)36, gbBase);
        Assert.Equal((ushort)39, AbilityStats.ScaleAbilityDamage(caster, GuardBreaker, gbBase));
        Assert.Equal((ushort)20, AbilityCosts.Cost(GuardBreaker));
        Assert.True(AbilityCooldowns.TryGet(GuardBreaker, out var gbCd));
        Assert.Equal(10f, gbCd.Seconds);
    }
}
