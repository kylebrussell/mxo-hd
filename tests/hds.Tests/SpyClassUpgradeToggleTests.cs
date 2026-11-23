using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyClassUpgradeToggleTests
{
    private static WorldClient Client(byte level = 19)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(1113, 1009, 3)]
    [InlineData(1115, 639, 3)]
    [InlineData(1117, 644, 3)]
    [InlineData(1120, 1032, 15)]
    public void SpyFamilyClassUpgradesSupportToggleOffWithoutRestartingReuse(ushort id, ushort stat, int expected)
    {
        var actor = Client(19);
        Assert.True(AbilityBuffs.IsStartingClassUpgrade(id));
        Assert.True(AbilityBuffs.TryGet(id, out var buff));
        Assert.True(buff.Toggle);
        Assert.True(AbilityBuffs.CooldownStartsOnUse(id));
        Assert.Equal((ushort)20, AbilityCosts.Cost(id));
        Assert.True(AbilityCooldowns.TryGet(id, out var cd));
        Assert.Equal(30f, cd.Seconds);

        // Activation starts 30 s reuse and applies modifiers.
        AbilityCooldowns.Start(actor, id, 100);
        Assert.True(AbilityBuffs.Apply(actor, id));
        Assert.True(AbilityBuffs.IsToggledOn(actor, id));
        Assert.Equal(expected, AbilityStats.Player(actor, stat));
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, id, 104));

        // Toggling off at +4 s removes modifiers and does NOT restart the 30 s reuse timer.
        actor.messageQueue.RPCMessagesQueue.Clear();
        Assert.True(AbilityBuffs.Remove(actor, id));
        Assert.False(AbilityBuffs.IsToggledOn(actor, id));
        Assert.Equal(0, AbilityStats.Player(actor, stat));
        byte[] restart = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, 30, true);
        var sent = actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m => m.content.AsSpan(1).ToArray()).ToArray();
        Assert.DoesNotContain(sent, p => p.SequenceEqual(restart));
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, id, 106));
        Assert.False(AbilityCooldowns.IsCoolingDown(actor.playerData, id, 131));
    }

    [Fact]
    public void SpyAndInfiltratorUpgradesMutuallyReplaceEachOtherInClassBuffFamily()
    {
        var actor = Client(19);
        Assert.True(AbilityBuffs.Apply(actor, 1113));
        Assert.Equal(8, AbilityStats.Player(actor, 209));
        Assert.Equal(0, AbilityStats.Player(actor, 639));

        Assert.True(AbilityBuffs.Apply(actor, 1115));
        Assert.False(AbilityBuffs.IsToggledOn(actor, 1113));
        Assert.True(AbilityBuffs.IsToggledOn(actor, 1115));
        Assert.Equal(0, AbilityStats.Player(actor, 209));
        Assert.Equal(3, AbilityStats.Player(actor, 639));
        Assert.Equal(10, AbilityStats.Player(actor, 877));
        Assert.True(AbilityBuffs.Remove(actor, 1115));
    }

    [Fact]
    public void InfiltratorUpgradeAppliesDirectSneakMovementOnlyAndClearsItWithoutBreakingSneak()
    {
        var actor = Client(50);
        Assert.True(AbilityBuffs.Apply(actor, 1115));
        Assert.Equal(0f, Scale());

        Assert.NotNull(Stealth.Begin(actor, Stealth.Defs[1116], 50));
        Assert.Equal(0f, Scale());
        Stealth.Cleared(actor);

        var sneak = Stealth.Begin(actor, Stealth.Defs[1111], 50);
        Assert.NotNull(sneak);
        Assert.Equal(.10f, Scale());
        Assert.True(AbilityBuffs.Remove(actor, 1115));
        Assert.Equal(0f, Scale());
        Assert.Same(sneak, Stealth.Current(actor));
        Stealth.Cleared(actor);

        float Scale() => BitConverter.ToSingle(actor.playerInstance.MovementScale.getValue());
    }

    [Fact]
    public void InfiltratorUpgradeFeedsDisguiseLeaseAndBothCombatInputs()
    {
        var actor = Client(50);
        var sweep = new AbilityItem();
        sweep.setAbilityID(134);
        int defence = AbilityStats.Defense(actor, false);
        int rangedDefence = AbilityStats.Defense(actor, true);
        int throwing = AbilityStats.SpecialAttack(actor, sweep);
        var mask = Stealth.Begin(actor, Stealth.Defs[1114], 50, 1308623188);
        Assert.Equal(56, mask.Seconds);
        Stealth.Cleared(actor);

        Assert.True(AbilityBuffs.Apply(actor, 1115));
        Assert.Equal(defence + 3, AbilityStats.Defense(actor, false));
        Assert.Equal(rangedDefence, AbilityStats.Defense(actor, true));
        Assert.Equal(throwing + 3, AbilityStats.SpecialAttack(actor, sweep));
        mask = Stealth.Begin(actor, Stealth.Defs[1114], 50, 1308623188);
        Assert.Equal(59, mask.Seconds);
        Assert.True(AbilityBuffs.Remove(actor, 1115));
        Assert.Same(mask, Stealth.Current(actor));
        Assert.Equal(59, mask.Seconds);
        Assert.Equal(defence, AbilityStats.Defense(actor, false));
        Assert.Equal(throwing, AbilityStats.SpecialAttack(actor, sweep));
        Stealth.Cleared(actor);
    }

    [Theory]
    [InlineData(155, 1113)]
    [InlineData(131, 1115)]
    [InlineData(283, 1120)]
    public void SpyFamilyClassDisciplinesRegisterTheirGrantedUpgradesForActions(ushort parent, ushort child)
    {
        Assert.True(AbilityGrantRegistration.Handles(parent, child));
        var actor = Client(19);
        actor.playerData.setOnWorld(true);
        actor.playerData.setDistrictId(1);
        actor.playerData.worldGeneration = 1;
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(actor, new WorldSendFence());
        actor.playerData.passives.Loaded[parent] = 15;

        AbilityGrantRegistration.LoadingComplete(actor, new byte[] { 1 });
        byte[] expectedRecord = AbilityGrantRegistration.Record(new(parent, child, 0), false);
        var sent = actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m => m.content.AsSpan(1).ToArray()).ToArray();
        Assert.Contains(sent, p => p.SequenceEqual(expectedRecord));
    }
}
