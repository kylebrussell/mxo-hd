using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using MxoHd.PacketResearch;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MartialArtsMasterDisciplinesTests
{
    private const ushort KarateMaster = 263, AikidoMaster = 270, KungFuGrandmaster = 426, KarateGrandmaster = 533;
    private const ushort KungFuGrandmasterUpgrade = 1064, KarateMasterUpgrade = 1065, KarateGrandmasterUpgrade = 1066, AikidoMasterUpgrade = 1067;

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

    [Theory]
    [InlineData(KarateMaster, KarateMasterUpgrade)]
    [InlineData(AikidoMaster, AikidoMasterUpgrade)]
    [InlineData(KungFuGrandmaster, KungFuGrandmasterUpgrade)]
    [InlineData(KarateGrandmaster, KarateGrandmasterUpgrade)]
    public void MartialMasterDisciplinesRegisterZeroGrantsAtReadinessAndReconcileOnCycle(ushort parent, ushort child)
    {
        Assert.True(AbilityGrantRegistration.ZeroGrant(AbilityCatalog.Find(parent), child));
        Assert.True(AbilityGrantRegistration.Handles(parent, child));
        Assert.Contains((parent, child), AbilityGrantRegistration.ObservedPairs);

        var client = ReadyClient();
        client.playerData.passives.Loaded[parent] = 50;
        byte[][] Packets() => client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(m => m.content.Skip(1).ToArray()).ToArray();

        AbilityGrantRegistration.LoadedChanged(client);
        Assert.Empty(Packets());

        AbilityGrantRegistration.LoadingComplete(client, new byte[] { 1 });
        byte[] expectedLoad = ServerPackets.BuildAbilityLoad(child, 0, AbilityProgression.HiddenSlot);
        byte[] expectedAdd = AbilityGrantRegistration.Record(new(parent, child, 0), off: false);
        Assert.Equal(new[] { expectedLoad, expectedAdd }, Packets());

        client.playerData.passives.Loaded.Remove(parent);
        AbilityGrantRegistration.LoadedChanged(client);
        Assert.Equal(ServerPackets.BuildAbilityUnload(child), Packets()[2]);
        Assert.Equal(AbilityGrantRegistration.Record(new(parent, child, 0), off: true), Packets()[3]);

        client.playerData.passives.Loaded[parent] = 50;
        AbilityGrantRegistration.LoadedChanged(client);
        Assert.Equal(expectedLoad, Packets()[4]);
        Assert.Equal(expectedAdd, Packets()[5]);
    }

    [Fact]
    public void MartialMasterDisciplinePassivesFeedAllExpectedStatsAndConsumers()
    {
        // Karate Master (263): Initiative Bonus +1 (1046), Melee Accuracy Bonus +1 (997)
        var c263 = ReadyClient(50);
        AbilityPassives.SetLoaded(c263, new (ushort, ushort)[] { (KarateMaster, 50) });
        Assert.Equal(1, AbilityPassives.Total(c263, 1046));
        Assert.Equal(1, AbilityPassives.Total(c263, 997));
        Assert.Equal(1, AbilityStats.Player(c263, 1046));
        Assert.Equal(1, AbilityStats.Player(c263, 997));

        // Aikido Master (270): Initiative Bonus +2 (1046), Viral Defense Bonus +1 (1030)
        var c270 = ReadyClient(50);
        AbilityPassives.SetLoaded(c270, new (ushort, ushort)[] { (AikidoMaster, 50) });
        Assert.Equal(2, AbilityPassives.Total(c270, 1046));
        Assert.Equal(1, AbilityPassives.Total(c270, 1030));
        Assert.Equal(2, AbilityStats.Player(c270, 1046));
        Assert.Equal(1, AbilityStats.Player(c270, 1030));

        // Kung Fu Grandmaster (426): Initiative Bonus +1 (1046), Powerless Dur +2 (1042), Melee Acc Bonus +5 (997), Melee Dmg Bonus +1 (1142)
        var c426 = ReadyClient(50);
        AbilityPassives.SetLoaded(c426, new (ushort, ushort)[] { (KungFuGrandmaster, 50) });
        Assert.Equal(1, AbilityPassives.Total(c426, 1046));
        Assert.Equal(2, AbilityPassives.Total(c426, 1042));
        Assert.Equal(5, AbilityPassives.Total(c426, 997));
        Assert.Equal(1, AbilityPassives.Total(c426, 1142));
        ushort powerlessState = AbilityCatalog.All.First(e => AbilityControlDurations.BonusStat(e.Key) == 1042).Key;
        Assert.Equal(10.2f, AbilityControlDurations.Scale(c426, powerlessState, 10f), 3);
        Assert.Equal(1009, AbilityStats.ScaleTypedAbilityDamage(c426, 0, 1000)); // binary32 1.01f * 1000

        // Karate Grandmaster (533): Initiative Bonus +1 (1046), Stun Dur +2 (995), Melee Dmg Bonus +4 (1142)
        var c533 = ReadyClient(50);
        AbilityPassives.SetLoaded(c533, new (ushort, ushort)[] { (KarateGrandmaster, 50) });
        Assert.Equal(1, AbilityPassives.Total(c533, 1046));
        Assert.Equal(2, AbilityPassives.Total(c533, 995));
        Assert.Equal(4, AbilityPassives.Total(c533, 1142));
        ushort stunState = AbilityCatalog.All.First(e => AbilityControlDurations.BonusStat(e.Key) == 995).Key;
        Assert.Equal(10.2f, AbilityControlDurations.Scale(c533, stunState, 10f), 3);
        Assert.Equal(103, AbilityStats.ScaleTypedAbilityDamage(c533, 0, 100)); // binary32 1.04f * 100
    }

    [Theory]
    [InlineData(AikidoMasterUpgrade, 148, 4, 2, 7)]
    [InlineData(KungFuGrandmasterUpgrade, 137, 13, 6, 8)]
    [InlineData(KarateMasterUpgrade, 90, 5, 4, 5)]
    [InlineData(KarateGrandmasterUpgrade, 110, 7, 6, 6)]
    public void MartialMasterUpgradesApplyAllFourStatsAndCleanUpOnToggleOff(
        ushort upgrade, int expectedMaxHp, int expectedMeleeAcc, int expectedMeleeDmg, int expectedMeleeDef)
    {
        Assert.Equal((ushort)20, AbilityCosts.Cost(upgrade));
        Assert.True(AbilityCooldowns.TryGet(upgrade, out var cd));
        Assert.Equal(30f, cd.Seconds);

        var client = ReadyClient(50);
        ushort baseMaxHp = BitConverter.ToUInt16(client.playerInstance.MaxHealth.getValue(), 0);
        Assert.True(AbilityBuffs.Apply(client, upgrade));
        Assert.True(AbilityBuffs.IsToggledOn(client, upgrade));
        Assert.Equal(expectedMaxHp, AbilityStats.Player(client, 106));
        Assert.Equal((ushort)(baseMaxHp + expectedMaxHp), BitConverter.ToUInt16(client.playerInstance.MaxHealth.getValue(), 0));
        Assert.Equal(expectedMeleeAcc, AbilityStats.Attack(client, false));
        Assert.Equal(expectedMeleeDmg, AbilityStats.Player(client, 159));
        Assert.Equal(expectedMeleeDef, AbilityStats.Defense(client, false));

        Assert.True(AbilityBuffs.Remove(client, upgrade));
        Assert.False(AbilityBuffs.IsToggledOn(client, upgrade));
        Assert.Equal(baseMaxHp, BitConverter.ToUInt16(client.playerInstance.MaxHealth.getValue(), 0));
        Assert.Equal(0, AbilityStats.Attack(client, false));
        Assert.Equal(0, AbilityStats.Player(client, 159));
        Assert.Equal(0, AbilityStats.Defense(client, false));
    }

    [Fact]
    public void MartialMasterDisciplinesBatchFitsMemoryAndReloadsInPrerequisiteOrder()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-ma-master-disciplines").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(50),
            $"MA master batch memory {mem} must fit level-50 capacity {AbilityMemory.ForLevel(50)}");

        ushort[] reloadOrder = { KarateMaster, AikidoMaster, KungFuGrandmaster };
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
