using hds;
using System.Runtime.CompilerServices;

namespace hds.Tests;

[Collection("DelayedActions")]
public class ClientCombatActionTests
{
    [Fact]
    public void SelectionMatchesTheWorldEntryAndSlotProducers()
    {
        var initial = CombatSelection.Parse(new byte[] { 0x6b, 0, 0x10, 0, 0, 0, 0 });
        Assert.NotNull(initial);
        Assert.Null(CombatSelection.Value(initial.Value));
        var kungfu = CombatSelection.Parse(new byte[] { 0x6b, 0x85, 0, 0, 0, 0, 0 });
        Assert.Equal(133u, CombatSelection.Value(kungfu!.Value));
        Assert.True(CombatSelection.IsStyle(133));
        Assert.True(CombatSelection.IsStyle(17));
        Assert.False(CombatSelection.IsStyle(315));
        var weapon = CombatSelection.Parse(new byte[] { 0x69, 0, 0x10, 0, 0, 0, 1 });
        Assert.Equal(0x80000069u, CombatSelection.Value(weapon!.Value));
        Assert.True(weapon.Value.IgnoreStyleUpdate);
        Assert.Null(CombatSelection.Parse(new byte[6]));
        Assert.Null(CombatSelection.Parse(new byte[] { 0x6b, 0, 0x10, 0, 0, 2, 0 }));
        Assert.Null(CombatSelection.Value(new(0x6b, 0xffffffff, false, false)));
    }

    [Fact]
    public void SelectionRequiresALoadedAbilityOrItsHiddenGrant()
    {
        var awakened = new MarginAbilityItem { AbilityID = unchecked((int)(0x80000000u | 2u << 10)) };
        var kungfu = new MarginAbilityItem { AbilityID = unchecked((int)(0x80000000u | 133u << 10)) };
        Assert.True(CombatSelection.IsLoaded(17, new[] { awakened }));
        Assert.False(CombatSelection.IsLoaded(133, new[] { awakened, kungfu }));
        kungfu.isLoaded = true;
        Assert.True(CombatSelection.IsLoaded(133, new[] { kungfu }));
    }

    [Fact]
    public void DismissChecksTheSameBuffCategoryAndProhibitionAsTheClient()
    {
        Assert.Equal((ushort)1064, AbilityCancellation.Parse(new byte[] { 0x28, 4 }));
        Assert.Null(AbilityCancellation.Parse(new byte[] { 0x28 }));
        Assert.Null(AbilityCancellation.Parse(new byte[] { 0x28, 0x14 })); // no alias trick
        Assert.True(AbilityCancellation.CanDismiss(10)); // Hyper Dodge, captured 80 bb 0a 00
        Assert.True(AbilityCancellation.CanDismiss(1064)); // Kung Fu Master
        Assert.False(AbilityCancellation.CanDismiss(17)); // CanNOTRemoveBuff
        Assert.False(AbilityCancellation.CanDismiss(133));
        Assert.False(AbilityCancellation.CanDismiss(315)); // hostile stun
        Assert.False(AbilityCancellation.CanDismiss(359)); // damage
        Assert.False(AbilityCancellation.CanDismiss(4095));
    }

    [Fact]
    public void DismissTakesOffOnlyTheRequestedOwnBuff()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        AbilityBuffs.Apply(client, 10);
        AbilityBuffs.Apply(client, 1064);
        TimedEffects.ForPlayer(client).Effects.Apply(315, new[] { new TimedEffects.Active
            { Target = TempMods.StunAbility, Proc = TempMods.Proc.State, Seconds = 30 } }, 0);
        Assert.True(AbilityCancellation.Dismiss(client, 10));
        Assert.False(client.playerData.activeBuffs.ContainsKey(10));
        Assert.True(client.playerData.activeBuffs.ContainsKey(1064));
        Assert.False(AbilityCancellation.Dismiss(client, 315));
        Assert.True(TimedEffects.ForPlayer(client).Effects.HasParent(315));
        Assert.False(AbilityCancellation.Dismiss(client, 10));
    }
}
