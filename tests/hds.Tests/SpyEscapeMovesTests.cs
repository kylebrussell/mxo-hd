using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyEscapeMovesTests
{
    private static WorldClient Client(byte level = 12)
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

    [Fact]
    public void SideRollEscapeStartsAndEnforcesIts12SecondFamilyCooldownWithoutZeroOwnTimer()
    {
        var actor = Client(12);
        Assert.True(AbilityCooldowns.TryGet(121, out var cd));
        Assert.Equal(0f, cd.Seconds);
        Assert.Equal((ushort)1200, cd.Group);
        Assert.Equal(12f, cd.GroupSeconds);

        AbilityCooldowns.Start(actor, 121, 100);
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, 121, 105));
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, 292, 105));
        Assert.False(AbilityCooldowns.IsCoolingDown(actor.playerData, 121, 112.1));

        byte[] groupStart = AbilityCooldowns.BuildTimer(1200, AbilityCooldowns.GroupTimer, 12, true);
        byte[] zeroOwn = AbilityCooldowns.BuildTimer(121, AbilityCooldowns.OwnTimer, 0, true);
        var sent = actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m => m.content.AsSpan(1).ToArray()).ToArray();
        Assert.Contains(sent, p => p.SequenceEqual(groupStart));
        Assert.DoesNotContain(sent, p => p.SequenceEqual(zeroOwn));
    }

    [Fact]
    public void CartwheelEscapeHas30sOwn12sSharedConfuse91AndCasterEvasionPlus25()
    {
        Assert.Equal((ushort)15, AbilityCosts.Cost(121));
        Assert.Equal((ushort)15, AbilityCosts.Cost(292));
        Assert.True(AbilityCooldowns.TryGet(292, out var cd));
        Assert.Equal(30f, cd.Seconds);
        Assert.Equal((ushort)1200, cd.Group);
        Assert.Equal(12f, cd.GroupSeconds);

        Assert.Equal((ushort)91, TempMods.ConfuseAbility);
        Assert.True(TempMods.TryGet(292, out var effect));
        var confuse = Assert.Single(effect.Mods, m => m.Target == TempMods.ConfuseAbility);
        Assert.Equal(4f, confuse.Seconds);

        var actor = Client(12);
        CombatSpecials.Landed(actor, 292);
        Assert.Equal(25, AbilityStats.Player(actor, 520));
        Assert.Equal((ushort)0, CombatSpecials.Damage(121, null));
        Assert.Equal((ushort)0, CombatSpecials.Damage(292, null));
        Assert.True(AbilityBuffs.Remove(actor, 292));
    }
}
