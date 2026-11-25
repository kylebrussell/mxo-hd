using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public sealed class DropKickInfuriatingClosureTests
{
    [Fact]
    public void ModeLoadoutFitsLevel50MemoryAndResolvesExact153And1130Hits()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-drop-kick-infuriating-closure").Plan;
        Assert.Contains(SpySneakOpeners.DropKick, plan.Abilities);
        Assert.Contains((ushort)1130, plan.Abilities);
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.DisengageMode]);

        var caster = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        caster.playerData = new ClientData();
        caster.playerData.setCharID(110);
        caster.playerInstance = new PlayerCharacter();
        caster.playerInstance.Level.setValue((byte)50);
        caster.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(caster, long.MaxValue);
        AbilityPassives.SetLoaded(caster, plan.Abilities.Select(id => (id, (ushort)50)));
        CombatStyles.Select(caster, CombatStyles.SpyStyle);

        var dropItem = new AbilityItem();
        dropItem.setAbilityID(SpySneakOpeners.DropKick);
        ushort dropRaw = AbilityStats.ScaleAbilityDamage(caster, SpySneakOpeners.DropKick,
            SneakAttacks.Add(AbilityHandler.AbilityDamage(dropItem, new Random(1), characterLevel: 50), SpySneakOpeners.DropKick, true));
        Assert.Equal((ushort)280, dropRaw);
        Assert.Equal((ushort)230, TypedAbilityResistance.Amount(dropRaw, 50, 50, 75));

        var infItem = new AbilityItem();
        infItem.setAbilityID(1130);
        ushort infRaw = AbilityStats.ScaleAbilityDamage(caster, 1130,
            AbilityHandler.AbilityDamage(infItem, new Random(1), characterLevel: 50));
        Assert.Equal((ushort)182, infRaw);
        Assert.Equal((ushort)149, TypedAbilityResistance.Amount(infRaw, 50, 50, 75));
    }
}
