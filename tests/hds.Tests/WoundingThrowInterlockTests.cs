using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

[Collection("DelayedActions")]
public class WoundingThrowInterlockTests
{
    private const ushort Spy = CombatStyles.SpyStyle, WoundingThrow = CombatSpecials.WoundingThrow;

    private static WorldClient Client(byte level = 50)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData();
        c.playerData.setCharID(28101);
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.Level.setValue(level);
        c.playerInstance.Health.setValue((ushort)5000);
        c.playerInstance.MaxHealth.setValue((ushort)5000);
        c.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(c, long.MaxValue);
        return c;
    }

    [Fact]
    public void WoundingThrowMapsToKnifeThrowSrAndQueuesUnderSpyStyle()
    {
        var move = CombatAnimations.Special(WoundingThrow, landed: true, CombatTactic.Speed);
        Assert.NotNull(move);
        Assert.Equal(290u, move!.Id);
        Assert.Equal("Knife Throw SR", move.Name);
        Assert.Equal((ushort)3800, move.DurationMs);

        var caster = Client();
        Assert.False(SpyTimedThrows.Handles(WoundingThrow));
        Assert.False(SpyTimedThrows.CanQueue(caster, WoundingThrow));

        var plan = NativeQa.NativeQaModes.Find("ability-parity-wounding-throw-interlock").Plan;
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));
        AbilityPassives.SetLoaded(caster, plan.Abilities.Select(a => (a, (ushort)50)));
        CombatStyles.Select(caster, Spy);

        Assert.True(SpyTimedThrows.CanQueue(caster, WoundingThrow));
        var item = new AbilityItem();
        item.setAbilityID(WoundingThrow);
        item.setCastingTime(2f);
        Assert.True(CombatAnimations.CanResolveQueuedAbility(caster, item));

        // Level-50 Spy Style (+29% Thrown): 61 * 1.29 = 78 total / 10 ticks -> 8 neutral, 6 at +75 Thrown resistance.
        Assert.Equal(29, AbilityStats.Influence(caster, 1034));
        ushort scaledTotal = AbilityStats.ScaleAbilityDamage(caster, WoundingThrow, 61);
        Assert.Equal((ushort)78, scaledTotal);
        int tick = TempMods.DotTick(scaledTotal, 50f, 5f);
        Assert.Equal(8, tick);
        Assert.Equal((ushort)6, TypedAbilityResistance.Amount((ushort)tick, 50, 50, 75));
    }
}
