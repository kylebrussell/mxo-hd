using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public sealed class HolyWaterCureTests
{
    [Fact]
    public void UninfectedPlayerTakesBaseTenDamageWithNoCureOrRsiRevert()
    {
        var caster = Client(51001, "Caster");
        var target = Client(51002, "Target");

        Assert.Equal((ushort)10, CombatSpecials.PlayerDamage(CombatSpecials.HolyWater, target));
        Assert.Equal((ushort)10, CombatSpecials.PlayerDamage(CombatSpecials.HolyWaterPlus, target));
        Assert.False(HolyWaterCure.IsInfected(target));
        Assert.False(HolyWaterCure.LandedOnPlayer(caster, target, CombatSpecials.HolyWater));
        Assert.False(HolyWaterCure.LandedOnPlayer(caster, target, CombatSpecials.HolyWaterPlus));
        Assert.Empty(target.messageQueue.RPCMessagesQueue);
        Assert.Empty(target.messageQueue.ObjectMessagesQueue);
    }

    [Fact]
    public void HolyWater547Deals100ToInfectedPlayerRemovesFiveGrantsDeactivatesAll24RsiCapsAndRevertsRsi()
    {
        var caster = Client(51003, "Caster");
        var target = Client(51004, "InfectedV2");

        HolyWaterCure.Grant(target, HolyWaterCure.ZombieVirus2Base);
        HolyWaterCure.Grant(target, HolyWaterCure.ZombieVirus2Bite);
        HolyWaterCure.Grant(target, HolyWaterCure.KilledOnlyByHolyWater, 1);
        foreach (ushort cap in HolyWaterCure.RsiCaptureAbilities)
            HolyWaterCure.ActivateRsiCapture(target, cap);
        target.messageQueue.RPCMessagesQueue.Clear();
        target.messageQueue.ObjectMessagesQueue.Clear();

        Assert.True(HolyWaterCure.IsInfected(target));
        Assert.Equal((ushort)100, CombatSpecials.PlayerDamage(CombatSpecials.HolyWater, target));
        Assert.Equal((byte)0, target.playerInstance.UseRSIDescription.getValue()[0]);

        Assert.True(HolyWaterCure.LandedOnPlayer(caster, target, CombatSpecials.HolyWater));

        Assert.False(HolyWaterCure.IsInfected(target));
        foreach (ushort grant in HolyWaterCure.ZombieVirusGrants)
            Assert.False(HolyWaterCure.HasGrant(target, grant));
        foreach (ushort cap in HolyWaterCure.RsiCaptureAbilities)
            Assert.False(HolyWaterCure.HasActiveRsiCapture(target, cap));

        // Reverts UseRSIDescription -> 1 and Description -> 0.
        Assert.Equal((byte)1, target.playerInstance.UseRSIDescription.getValue()[0]);
        Assert.Equal(0u, BitConverter.ToUInt32(target.playerInstance.Description.getValue(), 0));

        // Queued 80 b3 unloads for all three active grants (SequencedMessage prefixes the 1-byte RPC length).
        var rpcs = target.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m => m.content).ToList();
        foreach (ushort grant in new[] { HolyWaterCure.ZombieVirus2Base, HolyWaterCure.ZombieVirus2Bite, HolyWaterCure.KilledOnlyByHolyWater })
        {
            byte[] unload = ServerPackets.BuildAbilityUnload(grant);
            Assert.Contains(rpcs, bytes => bytes.Length > 1 && bytes.AsSpan(1).SequenceEqual(unload));
        }

        // Subsequent Holy Water hit drops back to base 10 damage.
        Assert.Equal((ushort)10, CombatSpecials.PlayerDamage(CombatSpecials.HolyWater, target));
        Assert.False(HolyWaterCure.LandedOnPlayer(caster, target, CombatSpecials.HolyWater));
    }

    [Fact]
    public void EnhancedHolyWater1023Deals750ToInfectedPlayerRemovesGrantsRevertsRsiAndLeavesRsiCaptureAlone()
    {
        var caster = Client(51005, "Caster");
        var target = Client(51006, "InfectedV3");

        HolyWaterCure.Grant(target, HolyWaterCure.ZombieVirus3Base);
        HolyWaterCure.Grant(target, HolyWaterCure.ZombieVirus3Bite);
        HolyWaterCure.Grant(target, HolyWaterCure.KilledOnlyByHolyWater, 1);
        HolyWaterCure.ActivateRsiCapture(target, HolyWaterCure.RsiCapture);
        target.messageQueue.RPCMessagesQueue.Clear();
        target.messageQueue.ObjectMessagesQueue.Clear();

        Assert.True(HolyWaterCure.IsInfected(target));
        Assert.Equal((ushort)750, CombatSpecials.PlayerDamage(CombatSpecials.HolyWaterPlus, target));

        Assert.True(HolyWaterCure.LandedOnPlayer(caster, target, CombatSpecials.HolyWaterPlus));

        Assert.False(HolyWaterCure.IsInfected(target));
        foreach (ushort grant in HolyWaterCure.ZombieVirusGrants)
            Assert.False(HolyWaterCure.HasGrant(target, grant));

        // Exact general.py:147-154 parity: 1023 removes grants and reverts RSI without deactivating RSICap* abilities.
        Assert.True(HolyWaterCure.HasActiveRsiCapture(target, HolyWaterCure.RsiCapture));
        Assert.Equal((byte)1, target.playerInstance.UseRSIDescription.getValue()[0]);
        Assert.Equal(0u, BitConverter.ToUInt32(target.playerInstance.Description.getValue(), 0));

        // Follow-up hit returns to base 10.
        Assert.Equal((ushort)10, CombatSpecials.PlayerDamage(CombatSpecials.HolyWaterPlus, target));
    }

    [Fact]
    public void CatalogHasAll24ShippedRsiCaptureAbilitiesAndFiveZombieVirusGrants()
    {
        Assert.Equal(5, HolyWaterCure.ZombieVirusGrants.Length);
        Assert.Equal(24, HolyWaterCure.RsiCaptureAbilities.Length);
        foreach (ushort id in HolyWaterCure.ZombieVirusGrants.Concat(HolyWaterCure.RsiCaptureAbilities))
            Assert.NotNull(AbilityCatalog.Find(id));
    }

    private static WorldClient Client(uint charId, string name)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.CharacterName.setValue(name);
        client.playerInstance.Level.setValue((byte)19);
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        client.LivenessClock = () => 0;
        client.Alive = true;
        return client;
    }
}
