using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using MxoHd.PacketResearch;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyMasterDisciplinesTests
{
    private const ushort WeakPoints = 164, WickedAim = 278, Shadow = 290, StealthCountermeasures = 293;
    private const ushort MasterKnifeThrower = 338, MasterAssassin = 389;
    private const ushort ShadowUpgrade = 1117, MasterKnifeThrowerUpgrade = 1121, MasterAssassinUpgrade = 1123;

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
    [InlineData(MasterKnifeThrower, MasterKnifeThrowerUpgrade)]
    [InlineData(MasterAssassin, MasterAssassinUpgrade)]
    public void SpyMasterDisciplinesRegisterZeroGrantsAtReadinessAndReconcileOnCycle(ushort parent, ushort child)
    {
        Assert.True(AbilityGrantRegistration.ZeroGrant(AbilityCatalog.Find(parent), child));
        Assert.True(AbilityGrantRegistration.Handles(parent, child));
        Assert.DoesNotContain((parent, child), AbilityGrantRegistration.ObservedPairs);

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
    public void ShadowReadyPresentationReconcilesAndMasterPassivesFeedStats()
    {
        // Shadow (290 -> 1117) stays in ReadyPresentationPairs and reconciles cleanly at world readiness.
        Assert.True(AbilityGrantRegistration.ZeroGrant(AbilityCatalog.Find(Shadow), ShadowUpgrade));
        Assert.True(AbilityGrantRegistration.Handles(Shadow, ShadowUpgrade));
        Assert.Contains((Shadow, ShadowUpgrade), AbilityGrantRegistration.ReadyPresentationPairs);

        var cShadow = ReadyClient(50);
        cShadow.playerData.passives.Loaded[Shadow] = 50;
        AbilityGrantRegistration.LoadingComplete(cShadow, new byte[] { 1 });
        byte[] shadowAdd = AbilityGrantRegistration.Record(new(Shadow, ShadowUpgrade, 0), off: false);
        Assert.Contains(cShadow.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
            m => m.content.AsSpan(1).SequenceEqual(shadowAdd));

        // Master Assassin (389): Thrown Damage Bonus +5 (1028)
        var c389 = ReadyClient(50);
        AbilityPassives.SetLoaded(c389, new (ushort, ushort)[] { (MasterAssassin, 50) });
        Assert.Equal(5, AbilityPassives.Total(c389, 1028));
        Assert.Equal(104, AbilityStats.ScaleTypedAbilityDamage(c389, 7, 100)); // binary32 1.05f * 100

        // Master Knife Thrower (338): Thrown Accuracy Bonus +5 (1145)
        var c338 = ReadyClient(50);
        AbilityPassives.SetLoaded(c338, new (ushort, ushort)[] { (MasterKnifeThrower, 50) });
        Assert.Equal(5, AbilityPassives.Total(c338, 1145));

        // Weak Points (164): Thrown Dmg +10 (1028), Thrown Acc +10 (1145), Ballistic Def +10 (1027)
        var c164 = ReadyClient(50);
        AbilityPassives.SetLoaded(c164, new (ushort, ushort)[] { (WeakPoints, 50) });
        Assert.Equal(10, AbilityPassives.Total(c164, 1028));
        Assert.Equal(10, AbilityPassives.Total(c164, 1145));
        Assert.Equal(10, AbilityPassives.Total(c164, 1027));

        // Wicked Aim (278): Thrown Dmg +15 (1028), Thrown Acc +15 (1145), Root Dur +10 (1040), Pacify Dur +11 (1039)
        var c278 = ReadyClient(50);
        AbilityPassives.SetLoaded(c278, new (ushort, ushort)[] { (WickedAim, 50) });
        Assert.Equal(15, AbilityPassives.Total(c278, 1028));
        Assert.Equal(15, AbilityPassives.Total(c278, 1145));
        Assert.Equal(10, AbilityPassives.Total(c278, 1040));
        Assert.Equal(11, AbilityPassives.Total(c278, 1039));
    }

    [Fact]
    public void StealthCountermeasuresAndMasterUpgradesApplyExpectedStatsAndCosts()
    {
        Assert.Equal((ushort)140, AbilityCosts.Cost(StealthCountermeasures));
        Assert.True(AbilityCooldowns.TryGet(StealthCountermeasures, out var scCd));
        Assert.Equal(15f, scCd.Seconds);

        var client = ReadyClient(50);
        Assert.True(AbilityBuffs.Apply(client, StealthCountermeasures));
        Assert.Equal(80, AbilityStats.Player(client, 204));
        Assert.True(AbilityBuffs.Remove(client, StealthCountermeasures));
        Assert.Equal(0, AbilityStats.Player(client, 204));

        Assert.True(AbilityBuffs.Apply(client, MasterKnifeThrowerUpgrade));
        Assert.Equal(6, AbilityStats.Player(client, 1034));
        Assert.Equal(18, AbilityStats.Player(client, 1032));
        Assert.Equal(10, AbilityStats.Player(client, 520));

        Assert.True(AbilityBuffs.Apply(client, MasterAssassinUpgrade));
        Assert.False(AbilityBuffs.IsToggledOn(client, MasterKnifeThrowerUpgrade));
        Assert.Equal(10, AbilityStats.Player(client, 1034));
        Assert.Equal(7, AbilityStats.Player(client, 1032));
        Assert.Equal(8, AbilityStats.Player(client, 209));
        Assert.True(AbilityBuffs.Remove(client, MasterAssassinUpgrade));
        Assert.Equal(0, AbilityStats.Player(client, 1034));
    }

    [Fact]
    public void SpyMasterDisciplinesBatchFitsMemoryAndReloadsInPrerequisiteOrder()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-master-disciplines").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(50),
            $"Spy master batch memory {mem} must fit level-50 capacity {AbilityMemory.ForLevel(50)}");

        ushort[] reloadOrder = { WeakPoints, WickedAim, MasterKnifeThrower, MasterAssassin };
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
