using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

public class SuplexDamageTests
{
    private const ushort Suplex = 287, SpyStyle = 1112, SneakPassive = 151, SneakGrant = 1111;

    private static WorldClient Client(byte level = 27)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(27, 89)]
    [InlineData(30, 97)]
    [InlineData(45, 120)]
    [InlineData(50, 120)]
    public void SuplexResolvesShippedValueFunction3ThrownBase(int level, ushort expected)
    {
        var source = AbilityCatalog.Find(Suplex);
        Assert.NotNull(source);
        Assert.True(SuplexDamage.TryResolve(source!, level, out ushort damage));
        Assert.Equal(expected, damage);

        var item = new AbilityItem();
        item.setAbilityID(Suplex);
        item.setValueFrom(17);
        item.setValueTo(0);
        Assert.Equal(expected, AbilityHandler.AbilityDamage(item, new Random(1), characterLevel: level));
    }

    [Fact]
    public void SuplexRefusesCloseCombatAndMissingTargetWithNativeCodes()
    {
        var client = Client(27);
        client.playerData.setCharID(28701);
        Assert.True(SuplexDamage.TryRefuse(client, Suplex, 0, null, out uint styleReason));
        Assert.Equal(AbilityRefusals.WrongStyle, styleReason);

        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (SpyStyle, 27) });
        CombatStyles.Select(client, SpyStyle);
        Assert.True(SuplexDamage.TryRefuse(client, Suplex, 0, null, out uint stealthReason));
        Assert.Equal(AbilityRefusals.CasterStateRequired, stealthReason);

        client.playerData.conditionStateFlags = Stealth.StealthedFlag;
        Assert.True(SuplexDamage.TryRefuse(client, Suplex, 0, null, out uint emptyReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);

        CombatManager.GetHandler(client).isCombatRunning = true;
        try
        {
            Assert.True(SuplexDamage.TryRefuse(client, Suplex, 4, 1, out uint combatReason));
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, combatReason);
        }
        finally
        {
            CombatManager.GetHandler(client).isCombatRunning = false;
        }

        Assert.False(SuplexDamage.TryRefuse(client, 152, 0, null, out _));
    }

    [Fact]
    public void SuplexFullRank27ClosureDamageAndControlsMatchLivePipeline()
    {
        const byte level = 27, rank = 27;
        ushort[] roots = { 2, 17, SpyStyle, SneakPassive, SneakGrant, Suplex };
        ushort[] loadout = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().ToArray();
        Assert.True(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));

        var caster = Client(level);
        caster.playerData.setCharID(28702);
        AbilityPassives.SetLoaded(caster, loadout.Select(id => (id, (ushort)rank)).ToArray());
        CombatStyles.Select(caster, SpyStyle);

        var item = new AbilityItem();
        item.setAbilityID(Suplex);
        item.setValueFrom(17);
        item.setValueTo(0);

        ushort baseDamage = AbilityHandler.AbilityDamage(item, new Random(1), characterLevel: level);
        Assert.Equal((ushort)89, baseDamage);

        ushort withOpener = SneakAttacks.Add(baseDamage, Suplex, fromStealth: true);
        Assert.Equal((ushort)389, withOpener);

        // Rank 27 closure: Spy Style 1112 (+17%) + Weak Points 164 (+5%) + Wicked Aim 278 (+8%) = +30%.
        Assert.Equal(30, AbilityStats.Influence(caster, 1034));
        ushort landed = AbilityStats.ScaleAbilityDamage(caster, Suplex, withOpener);
        Assert.Equal((ushort)505, landed);

        Assert.Equal((ushort)50, AbilityCosts.Cost(Suplex));
        Assert.Equal(0f, AbilityCatalog.Find(Suplex)!.CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(Suplex, out var cd));
        Assert.Equal(38f, cd.Seconds);
        Assert.Equal((ushort)691, cd.Group);
        Assert.Equal(15f, cd.GroupSeconds);

        Assert.True(TempMods.TryGet(Suplex, out var effect));
        foreach (ushort state in new[] { TempMods.OffBalanceAbility, TempMods.StaggeredAbility, TempMods.DazeAbility })
        {
            var mod = Assert.Single(effect.Mods, m => m.Target == state);
            Assert.Equal(15f, mod.Seconds);
            Assert.Equal(15f, AbilityControlDurations.Scale(caster, state, mod.Seconds), 4);
        }
    }
}
