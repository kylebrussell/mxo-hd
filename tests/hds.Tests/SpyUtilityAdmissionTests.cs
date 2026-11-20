using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

public class SpyUtilityAdmissionTests
{
    private static WorldClient Client(uint charId, byte level = 14)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
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
    [InlineData(SixthSense.Ability)]
    [InlineData(StealthShieldAbilities.BullDog)]
    [InlineData(StealthShieldAbilities.DisableEvasion)]
    public void SpyUtilitiesRefuseInCloseCombatWithMustBeOutsideCombat(ushort abilityId)
    {
        var client = Client(14901);
        CombatManager.GetHandler(client).isCombatRunning = true;
        try
        {
            Assert.True(SpyAbilityRegistry.TryRefuse(client, abilityId, 4, 1, out uint reason));
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, reason);
        }
        finally
        {
            CombatManager.GetHandler(client).isCombatRunning = false;
        }
    }

    [Fact]
    public void EmptySelectionAllowedForSixthSenseAndRefusedForStealthThrows()
    {
        var client = Client(14902);
        Assert.False(SpyAbilityRegistry.TryRefuse(client, SixthSense.Ability, 0, null, out _));

        foreach (ushort id in new[] { StealthShieldAbilities.BullDog, StealthShieldAbilities.DisableEvasion })
        {
            Assert.True(SpyAbilityRegistry.TryRefuse(client, id, 0, null, out uint reason));
            Assert.Equal(AbilityRefusals.InvalidTarget, reason);
        }
    }

    [Fact]
    public void SixthSenseRank14ClosureMemoryAndGuaranteedDetectionMatchLivePipeline()
    {
        const byte level = 14, rank = 14;
        ushort[] roots = { 2, 17, SixthSense.Ability };
        ushort[] loadout = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().ToArray();
        Assert.True(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));

        var caster = Client(14903, level);
        AbilityPassives.SetLoaded(caster, loadout.Select(id => (id, (ushort)rank)).ToArray());

        // Sixth Sense passive: 4 * rank -> +56 Detection (stat 646) at rank 14, guaranteeing roll(0..99) + 56 > 50.
        int detection = AbilityStats.Player(caster, SixthSense.Detection);
        Assert.Equal(56, detection);
        Assert.True(SixthSense.Detects(0, detection, 0));
        Assert.Equal((ushort)10, AbilityCosts.Cost(SixthSense.Ability));
    }
}
