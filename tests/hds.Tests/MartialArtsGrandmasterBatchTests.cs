using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MartialArtsGrandmasterBatchTests
{
    private const ushort AikidoRedirection = 405, AikidoGrandmaster = 410;
    private const ushort KarateFocus = 531, KarateGrandmaster = 533, BerserkerAttack = 535;
    private const ushort KarateGrandmasterUpgrade = 1066, AikidoGrandmasterUpgrade = 1069;

    private static WorldClient ReadyClient(byte level = 50)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)1000);
        client.playerInstance.MaxHealth.setValue((ushort)1000);
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
    public void BerserkerAttackRefusesOutsideCloseCombatAndAppliesShippedCasterBuff()
    {
        Assert.Equal((ushort)40, AbilityCosts.Cost(BerserkerAttack));
        Assert.True(AbilityCooldowns.TryGet(BerserkerAttack, out var cd));
        Assert.Equal(60f, cd.Seconds);
        Assert.Equal((ushort)687, cd.Group);
        Assert.Equal(5f, cd.GroupSeconds);
        Assert.Equal(CombatStyles.Karate, CombatStyles.RequiredStyle(BerserkerAttack));

        var client = ReadyClient(50);
        Assert.True(SpyAbilityRegistry.TryRefuse(client, BerserkerAttack, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);

        Assert.True(AbilityBuffs.Apply(client, BerserkerAttack));
        Assert.Equal(11, AbilityStats.Player(client, 159));
        Assert.Equal(50, AbilityStats.Player(client, 180));
        Assert.Equal(50, AbilityStats.Player(client, 181));
        Assert.True(AbilityBuffs.Remove(client, BerserkerAttack));
        Assert.Equal(0, AbilityStats.Player(client, 159));
        Assert.Equal(0, AbilityStats.Player(client, 180));
        Assert.Equal(0, AbilityStats.Player(client, 181));
    }

    [Fact]
    public void GrandmasterAndFocusPassivesFeedAllExpectedRank50Stats()
    {
        var c405 = ReadyClient(50);
        AbilityPassives.SetLoaded(c405, new (ushort, ushort)[] { (AikidoRedirection, 50) });
        Assert.Equal(5, AbilityPassives.Total(c405, 1068));
        Assert.Equal(1, AbilityPassives.Total(c405, 1142));
        Assert.Equal(5, AbilityPassives.Total(c405, 1143));
        Assert.Equal(10, AbilityPassives.Total(c405, 1042));
        Assert.Equal(6, AbilityPassives.Total(c405, 1030));
        Assert.Equal(12, AbilityPassives.Total(c405, 1148));

        var c410 = ReadyClient(50);
        AbilityPassives.SetLoaded(c410, new (ushort, ushort)[] { (AikidoGrandmaster, 50) });
        Assert.Equal(2, AbilityPassives.Total(c410, 1068));
        Assert.Equal(1, AbilityPassives.Total(c410, 1046));
        Assert.Equal(5, AbilityPassives.Total(c410, 1144));
        Assert.Equal(5, AbilityPassives.Total(c410, 1143));
        Assert.True(AbilityGrantRegistration.Handles(AikidoGrandmaster, AikidoGrandmasterUpgrade));

        var c531 = ReadyClient(50);
        AbilityPassives.SetLoaded(c531, new (ushort, ushort)[] { (KarateFocus, 50) });
        Assert.Equal(12, AbilityPassives.Total(c531, 1030));
        Assert.Equal(6, AbilityPassives.Total(c531, 1142));
        Assert.Equal(5, AbilityPassives.Total(c531, 1144));
        Assert.Equal(5, AbilityPassives.Total(c531, 1068));
        Assert.Equal(10, AbilityPassives.Total(c531, 995));

        var c533 = ReadyClient(50);
        AbilityPassives.SetLoaded(c533, new (ushort, ushort)[] { (KarateGrandmaster, 50) });
        Assert.Equal(2, AbilityPassives.Total(c533, 995));
        Assert.Equal(4, AbilityPassives.Total(c533, 1142));
        Assert.Equal(1, AbilityPassives.Total(c533, 1046));
        Assert.True(AbilityGrantRegistration.Handles(KarateGrandmaster, KarateGrandmasterUpgrade));
    }

    [Fact]
    public void GrandmasterBatchFitsMemoryAndReloadsInPrerequisiteOrder()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-ma-grandmaster-karate-aikido").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(50),
            $"MA grandmaster batch memory {mem} must fit level-50 capacity {AbilityMemory.ForLevel(50)}");

        ushort[] reloadOrder = { KarateFocus, KarateGrandmaster, AikidoRedirection, AikidoGrandmaster };
        var active = plan.Abilities.Except(reloadOrder).ToDictionary(id => id, _ => (ushort)50);
        foreach (ushort id in reloadOrder)
        {
            foreach (var prereq in AbilityPrerequisites.Current.Closure(id))
            {
                Assert.True(active.TryGetValue(prereq.Key, out ushort loadedRank) && loadedRank >= prereq.Value,
                    $"Reloading {id} requires {prereq.Key} at rank {prereq.Value} to already be loaded");
            }
            active[id] = 50;
        }
    }
}
