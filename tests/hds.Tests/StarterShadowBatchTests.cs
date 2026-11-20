using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class StarterShadowBatchTests
{
    private const ushort InvisibilityPassive = 175, ShadowDiscipline = 290, DesperateSpeed = 291;
    private const ushort InvisibilityGrant = 1116, ShadowUpgrade = 1117;
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static WorldClient ReadyClient(byte level = FixtureLevel)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, new WorldSendFence());
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void ShadowStarterPassivesGrantsBuffsAndDurationsMatchShippedCatalog()
    {
        Assert.True(AbilityGrantRegistration.Handles(InvisibilityPassive, InvisibilityGrant));
        Assert.True(AbilityGrantRegistration.Handles(ShadowDiscipline, ShadowUpgrade));

        var c175 = ReadyClient();
        AbilityPassives.SetLoaded(c175, new (ushort, ushort)[] { (InvisibilityPassive, FixtureRank) });
        Assert.Equal(25, AbilityPassives.Total(c175, 1051));

        // 1116 baseline vs 1117 Shadow Upgrade (+3 s duration on stat 644)
        Assert.Equal(130.0, Stealth.InvisibilitySeconds(FixtureRank, 0));
        var client = ReadyClient();
        Assert.Equal((ushort)20, AbilityCosts.Cost(ShadowUpgrade));
        Assert.True(AbilityCooldowns.TryGet(ShadowUpgrade, out var cd1117));
        Assert.Equal(30f, cd1117.Seconds);

        Assert.True(AbilityBuffs.Apply(client, ShadowUpgrade));
        Assert.Equal(6, AbilityStats.Player(client, 1005));
        Assert.Equal(5, AbilityStats.Player(client, 1032));
        Assert.Equal(3, AbilityStats.Player(client, 644));
        Assert.Equal(10, AbilityStats.Player(client, 878));
        Assert.Equal(133.0, Stealth.InvisibilitySeconds(FixtureRank, AbilityStats.Player(client, 644)));
        Assert.True(AbilityBuffs.Remove(client, ShadowUpgrade));
        Assert.Equal(0, AbilityStats.Player(client, 644));

        // 1116 costs & reuse
        Assert.Equal((ushort)50, AbilityCosts.Cost(InvisibilityGrant));
        Assert.True(AbilityCooldowns.TryGet(InvisibilityGrant, out var cd1116));
        Assert.Equal(30f, cd1116.Seconds);
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(InvisibilityGrant));

        // 291 Desperate Speed: 50 IS, 298 s reuse, +40 Move (0.40 MovementScale), +20 Sneak/Invis Move for 15 s
        Assert.Equal((ushort)50, AbilityCosts.Cost(DesperateSpeed));
        Assert.True(AbilityCooldowns.TryGet(DesperateSpeed, out var cd291));
        Assert.Equal(298f, cd291.Seconds);
        Assert.True(AbilityBuffs.TryGet(DesperateSpeed, out var b291));
        Assert.Equal(15f, b291.Seconds);
        Assert.Equal(0.40f, b291.MovementScale, 3);

        Assert.True(AbilityBuffs.Apply(client, DesperateSpeed));
        Assert.Equal(40, AbilityStats.Player(client, 115));
        Assert.Equal(20, AbilityStats.Player(client, 877));
        Assert.Equal(20, AbilityStats.Player(client, 878));
        Assert.True(AbilityBuffs.Remove(client, DesperateSpeed));
        Assert.Equal(0, AbilityStats.Player(client, 115));
    }

    [Fact]
    public void ShadowStarterBatchFitsMemoryAndReloadsInPrerequisiteOrder()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-starter-shadow").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Shadow starter batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");

        ushort[] reloadOrder = { InvisibilityPassive, DesperateSpeed, 1128, ShadowDiscipline };
        ushort[] unloadOrder = reloadOrder.Reverse().ToArray();

        var remaining = plan.Abilities.ToHashSet();
        foreach (ushort id in unloadOrder)
        {
            Assert.True(remaining.Remove(id), $"Unload id {id} must be in loadout");
            foreach (ushort stillLoaded in remaining)
            {
                Assert.False(AbilityPrerequisites.Current.Closure(stillLoaded).ContainsKey(id),
                    $"Unloading {id} is blocked because {stillLoaded} still depends on it");
            }
        }

        var active = plan.Abilities.Except(reloadOrder).ToDictionary(id => id, _ => (ushort)FixtureRank);
        foreach (ushort id in reloadOrder)
        {
            foreach (var prereq in AbilityPrerequisites.Current.Closure(id))
            {
                Assert.True(active.TryGetValue(prereq.Key, out ushort loadedRank) && loadedRank >= prereq.Value,
                    $"Reloading {id} requires {prereq.Key} at rank {prereq.Value} to already be loaded");
            }
            active[id] = FixtureRank;
        }
    }
}
