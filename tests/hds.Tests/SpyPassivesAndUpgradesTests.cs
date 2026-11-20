using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using MxoHd.PacketResearch;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyPassivesAndUpgradesTests
{
    private const ushort Infiltrator = 131, KnifeThrower = 283, Cutthroat = 118, Assassin = 285;
    private const ushort ImprovedSneakAttack = 284;
    private const ushort InfiltratorUpgrade = 1115, CutthroatUpgrade = 1119, KnifeThrowerUpgrade = 1120, AssassinUpgrade = 1122;

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
    [InlineData(Infiltrator, InfiltratorUpgrade)]
    [InlineData(KnifeThrower, KnifeThrowerUpgrade)]
    [InlineData(Cutthroat, CutthroatUpgrade)]
    [InlineData(Assassin, AssassinUpgrade)]
    public void SpyDisciplineZeroGrantsRegisterAtReadinessAndReconcileOnUnloadAndReload(ushort parent, ushort child)
    {
        Assert.True(AbilityGrantRegistration.ZeroGrant(AbilityCatalog.Find(parent), child));
        Assert.True(AbilityGrantRegistration.Handles(parent, child));
        Assert.DoesNotContain((parent, child), AbilityGrantRegistration.ObservedPairs);

        var client = ReadyClient();
        client.playerData.passives.Loaded[parent] = 50;
        byte[][] Packets() => client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(m => m.content.Skip(1).ToArray()).ToArray();

        // Nothing emitted before live-avatar readiness (0c 01).
        AbilityGrantRegistration.LoadedChanged(client);
        Assert.Empty(Packets());

        AbilityGrantRegistration.LoadingComplete(client, new byte[] { 1 });
        byte[] expectedLoad = ServerPackets.BuildAbilityLoad(child, 0, AbilityProgression.HiddenSlot);
        byte[] expectedAdd = AbilityGrantRegistration.Record(new(parent, child, 0), off: false);
        Assert.Equal(new[] { expectedLoad, expectedAdd }, Packets());

        // Idempotent duplicate readiness.
        AbilityGrantRegistration.LoadingComplete(client, new byte[] { 1 });
        Assert.Equal(2, Packets().Length);

        // Unload parent removes hidden grant + membership record.
        client.playerData.passives.Loaded.Remove(parent);
        AbilityGrantRegistration.LoadedChanged(client);
        byte[] expectedUnload = ServerPackets.BuildAbilityUnload(child);
        byte[] expectedRemove = AbilityGrantRegistration.Record(new(parent, child, 0), off: true);
        Assert.Equal(expectedUnload, Packets()[2]);
        Assert.Equal(expectedRemove, Packets()[3]);

        // Reload parent restores grant + membership record once.
        client.playerData.passives.Loaded[parent] = 50;
        AbilityGrantRegistration.LoadedChanged(client);
        Assert.Equal(expectedLoad, Packets()[4]);
        Assert.Equal(expectedAdd, Packets()[5]);
    }

    [Fact]
    public void ImprovedSneakAttackSuppliesAllThreeControlDurationBonusesAtRank50()
    {
        var client = ReadyClient(50);
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (ImprovedSneakAttack, 50) });

        Assert.Equal(25, AbilityPassives.Total(client, 1042)); // Confuse Duration Bonus
        Assert.Equal(25, AbilityPassives.Total(client, 1043)); // Powerless Duration Bonus
        Assert.Equal(12, AbilityPassives.Total(client, 1039)); // Pacify Duration Bonus

        // Verify live control-duration consumer applies all three bonuses via AttributeLink states.
        ushort confuseState = AbilityCatalog.All.First(e => AbilityControlDurations.BonusStat(e.Key) == 1042).Key;
        ushort powerlessState = AbilityCatalog.All.First(e => AbilityControlDurations.BonusStat(e.Key) == 1043).Key;
        ushort pacifyState = AbilityCatalog.All.First(e => AbilityControlDurations.BonusStat(e.Key) == 1039).Key;
        Assert.Equal(12.5f, AbilityControlDurations.Scale(client, confuseState, 10f), 3);
        Assert.Equal(12.5f, AbilityControlDurations.Scale(client, powerlessState, 10f), 3);
        Assert.Equal(11.2f, AbilityControlDurations.Scale(client, pacifyState, 10f), 3);
    }

    [Theory]
    [InlineData(CutthroatUpgrade, new ushort[] { 1034, 1032, 520 }, new[] { 3, 5, 10 })]
    [InlineData(KnifeThrowerUpgrade, new ushort[] { 1034, 1032, 520 }, new[] { 4, 15, 10 })]
    [InlineData(AssassinUpgrade, new ushort[] { 1034, 1032, 209 }, new[] { 6, 5, 5 })]
    public void SpyClassUpgradesApplyStoredModifiersAndToggleOffCleanly(ushort upgrade, ushort[] stats, int[] values)
    {
        var client = ReadyClient(50);
        Assert.True(AbilityBuffs.Apply(client, upgrade));
        Assert.True(AbilityBuffs.IsToggledOn(client, upgrade));
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(values[i], AbilityStats.Player(client, stats[i]));

        Assert.True(AbilityBuffs.Remove(client, upgrade));
        Assert.False(AbilityBuffs.IsToggledOn(client, upgrade));
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(0, AbilityStats.Player(client, stats[i]));
    }

    [Fact]
    public void SpyPassiveAndUpgradeBatchFitsMemoryAndReloadsInPrerequisiteOrder()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-passives-and-upgrades").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(50),
            $"Spy batch memory {mem} must fit level-50 capacity {AbilityMemory.ForLevel(50)}");

        ushort[] reloadOrder = { Infiltrator, Cutthroat, KnifeThrower, ImprovedSneakAttack, Assassin };
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
