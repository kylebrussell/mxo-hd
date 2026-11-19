using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public sealed class AbilityEvidenceTimedBuffTests
{
    private static WorldClient Level50Client()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.messageQueue = new MessageQueue();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.Health.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        typeof(WorldClient).GetField("lastFlushTick",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(124, 25)]
    [InlineData(321, 3)]
    [InlineData(373, 64)]
    [InlineData(374, 64)]
    [InlineData(421, 47)]
    public void NumericBuffsChangeTheirGameplayConsumerAndRestoreItOnDismissal(ushort ability, int delta)
    {
        var actor = Level50Client();
        var caster = Level50Client();
        Func<int> consumer = ability switch
        {
            124 => () => AbilityStats.ScaleAbilityDamage(actor, 126, 100),
            321 => () => -AbilityCosts.Effective(actor, 124),
            373 => () => AbilityStats.Attack(actor, false),
            374 => () => AbilityStats.Attack(actor, true),
            421 => () => AbilityStats.Defense(actor, false),
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
        int before = consumer();
        int ballisticDefense = AbilityStats.Defense(actor, true);
        Assert.True(AbilityBuffs.Apply(actor, ability, caster));
        Assert.Equal(before + delta, consumer());
        if (ability == 321)
            Assert.Equal((ushort)9, AbilityCosts.Effective(actor, 77));
        if (ability == 421)
            Assert.Equal(ballisticDefense + delta, AbilityStats.Defense(actor, true));
        Assert.True(AbilityCancellation.Dismiss(actor, ability));
        Assert.Equal(before, consumer());
        Assert.Equal(ballisticDefense, AbilityStats.Defense(actor, true));
    }
}
