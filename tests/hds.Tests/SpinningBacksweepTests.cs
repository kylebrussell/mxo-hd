using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

public class SpinningBacksweepTests
{
    private const ushort SpinningBacksweep = 152, SpyStyle = 1112, SneakPassive = 151, SneakGrant = 1111;

    private static WorldClient Client(byte level = 4)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)1000);
        client.playerInstance.MaxHealth.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void SpinningBacksweepRefusesCloseCombatAndMissingTargetViaRegistry()
    {
        var client = Client(4);
        client.playerData.setCharID(15201);
        Assert.True(SpyAbilityRegistry.TryRefuse(client, SpinningBacksweep, 0, null, out uint styleReason));
        Assert.Equal(AbilityRefusals.WrongStyle, styleReason);

        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (SpyStyle, 4) });
        CombatStyles.Select(client, SpyStyle);
        Assert.True(SpyAbilityRegistry.TryRefuse(client, SpinningBacksweep, 0, null, out uint stealthReason));
        Assert.Equal(AbilityRefusals.CasterStateRequired, stealthReason);

        client.playerData.conditionStateFlags = Stealth.StealthedFlag;
        Assert.True(SpyAbilityRegistry.TryRefuse(client, SpinningBacksweep, 0, null, out uint emptyReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);

        CombatManager.GetHandler(client).isCombatRunning = true;
        try
        {
            Assert.True(SpyAbilityRegistry.TryRefuse(client, SpinningBacksweep, 4, 1, out uint combatReason));
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, combatReason);
        }
        finally
        {
            CombatManager.GetHandler(client).isCombatRunning = false;
        }
    }

    [Fact]
    public void SpinningBacksweepFullRank4ClosureDamageAndTimersMatchLivePipeline()
    {
        const byte level = 4, rank = 4;
        ushort[] roots = { 2, 17, SpyStyle, SneakPassive, SneakGrant, SpinningBacksweep };
        ushort[] loadout = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().ToArray();
        Assert.True(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));

        var caster = Client(level);
        caster.playerData.setCharID(15202);
        AbilityPassives.SetLoaded(caster, loadout.Select(id => (id, (ushort)rank)).ToArray());
        CombatStyles.Select(caster, SpyStyle);

        var item = new AbilityItem();
        item.setAbilityID(SpinningBacksweep);
        item.setValueFrom(28);
        item.setValueTo(0);

        ushort baseDamage = AbilityHandler.AbilityDamage(item, new Random(1), characterLevel: level);
        Assert.Equal((ushort)28, baseDamage);

        ushort withOpener = SneakAttacks.Add(baseDamage, SpinningBacksweep, fromStealth: true);
        Assert.Equal((ushort)103, withOpener);

        // Rank 4 closure has only Spy Style 1112 (+6% thrown damage): trunc(103 * 1.06) = 109.
        Assert.Equal(6, AbilityStats.Influence(caster, 1034));
        ushort landed = AbilityStats.ScaleAbilityDamage(caster, SpinningBacksweep, withOpener);
        Assert.Equal((ushort)109, landed);

        Assert.Equal((ushort)20, AbilityCosts.Cost(SpinningBacksweep));
        Assert.Equal(0f, AbilityCatalog.Find(SpinningBacksweep)!.CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(SpinningBacksweep, out var cd));
        Assert.Equal(30f, cd.Seconds);
        Assert.Equal((ushort)691, cd.Group);
        Assert.Equal(15f, cd.GroupSeconds);
    }
}
