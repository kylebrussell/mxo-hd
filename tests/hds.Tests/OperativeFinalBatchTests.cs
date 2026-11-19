using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class OperativeFinalBatchTests
{
    private static WorldClient Actor()
    {
        var actor = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        typeof(WorldClient).GetProperty("IsBot")!.SetValue(actor, true);
        typeof(WorldClient).GetField("alive", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(actor, true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(actor, new WorldSendFence());
        actor.playerData = new ClientData { isBot = true };
        actor.playerData.setDistrictId(1);
        actor.playerData.setOnWorld(true);
        actor.playerInstance = new PlayerCharacter();
        actor.playerInstance.Level.setValue((byte)50);
        actor.playerInstance.MaxHealth.setValue((ushort)500);
        actor.playerInstance.Health.setValue((ushort)500);
        actor.playerInstance.InnerStrengthMax.setValue((ushort)500);
        actor.playerInstance.InnerStrengthAvailable.setValue((ushort)500);
        actor.messageQueue = new MessageQueue();
        return actor;
    }

    private static AbilityItem Ability(ushort id)
    {
        var item = new AbilityItem();
        item.setAbilityID(id);
        return item;
    }

    [Fact]
    public void SpinningBacksweepUsesItsFixedBaseWithSeparateThrownAndOpenerInputs()
    {
        var ability = Ability(152);
        ability.setValueFrom(28);
        ability.setValueTo(0); // The actual legacy row.
        foreach (int seed in new[] { 1, 2, 987 })
            Assert.Equal(28, AbilityHandler.AbilityDamage(ability, new Random(seed)));

        var actor = Actor();
        Assert.Equal(28, SneakAttacks.Add(28, 152, fromStealth: false));
        Assert.Equal(103, SneakAttacks.Add(28, 152, fromStealth: true));
        Assert.False(CombatStyles.EnsureStyleFor(actor, 152));
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)1112, (ushort)50) });
        Assert.True(CombatStyles.EnsureStyleFor(actor, 152));
        Assert.Equal(1112, CombatStyles.Active(actor));
        Assert.Equal(36, AbilityStats.ScaleAbilityDamage(actor, 152, 28));
        Assert.Equal(132, AbilityStats.ScaleAbilityDamage(actor, 152, 103));

        ability.setValueFrom(45);
        ability.setValueTo(45);
        Assert.Equal(45, AbilityHandler.AbilityDamage(ability, new Random(1)));
    }

    [Fact]
    public void SpyStyleSwitchPublishesAndWithdrawsAllNineInputs()
    {
        var actor = Actor();
        var expected = new (ushort Stat, int Value)[]
        {
            (522,59), (159,20), (1009,47), (1010,47), (183,47), (1011,47), (1032,66), (1034,29), (1005,72)
        };
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)1112, (ushort)50) });
        Assert.All(expected, item => Assert.Equal(0, AbilityStats.Player(actor, item.Stat)));
        Assert.True(CombatStyles.UseStyle(actor, 1112));
        Assert.All(expected, item => Assert.Equal(item.Value, AbilityStats.Player(actor, item.Stat)));
        Assert.Equal(59, AbilityStats.Attack(actor, false));
        Assert.Equal(47, AbilityStats.Defense(actor, false));
        Assert.Equal(47, AbilityStats.Defense(actor, true));
        Assert.Equal(47, AbilityStats.SpecialDefense(actor, Ability(330)));
        Assert.Equal(47, AbilityStats.SpecialDefense(actor, Ability(152)));
        Assert.Equal(66, AbilityStats.SpecialAttack(actor, Ability(152)));
        Assert.Equal(72, WithdrawalInput.Attack(actor, null!, ordinary: true));
        Assert.Equal(120, AbilityStats.ScaleAbilityDamage(actor, 267, 100));
        Assert.Equal(128, AbilityStats.ScaleAbilityDamage(actor, 152, 100));
        CombatStyles.Switch(actor, CombatStyles.SelfDefense);
        Assert.All(expected, item => Assert.Equal(0, AbilityStats.Player(actor, item.Stat)));
    }

    [Fact]
    public void SpyUpgradeAffectsOnlyCasterAndFeedsStealthAndTypedCombatInputs()
    {
        var actor = Actor();
        Assert.True(AbilitySupport.Handles(1113));
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, 1113, 99)));
        Assert.True(AbilityBuffs.Apply(actor, 1113));
        Assert.Equal(3, AbilityStats.Defense(actor, false));
        Assert.Equal(3, AbilityStats.ThrownAccuracy(actor));
        Assert.Equal(1019, AbilityStats.ScaleAbilityDamage(actor, 152, 1000));
        Assert.Equal(8, Stealth.StealthLevelOf(actor));
        Assert.True(AbilityBuffs.Remove(actor, 1113));
        Assert.Equal(0, Stealth.StealthLevelOf(actor));
        Assert.Equal(0, AbilityStats.ThrownAccuracy(actor));
    }

    [Fact]
    public void SpyGrantDoesNotPublishUntilReadyAndReloadsOnce()
    {
        var actor = Actor();
        var desired = new[] { ((ushort)155, (ushort)50) };
        byte[] add = Convert.FromHexString("80bc45035904009b0000005904000000000000000000");
        var off = add.ToArray();
        off[17] = 1;
        int Count(byte[] record) => actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Count(message => message.content.AsSpan(1).SequenceEqual(record));

        AbilityPassives.SetLoaded(actor, desired, newWorld: true);
        Assert.True(AbilityPassives.IsLoaded(actor, 1113));
        Assert.Equal(0, Count(add));
        AbilityGrantRegistration.LoadingComplete(actor, new byte[] { 1 });
        Assert.Equal(1, Count(add));
        Assert.Equal(1, Count(ServerPackets.BuildAbilityLoad(1113, 0, AbilityProgression.HiddenSlot)));
        AbilityGrantRegistration.LoadingComplete(actor, new byte[] { 1 });
        Assert.Equal(1, Count(add));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(actor, 1113));
        Assert.Equal(1, Count(off));
        AbilityPassives.SetLoaded(actor, desired);
        Assert.Equal(2, Count(add));
    }

    [Fact]
    public void SneakUsesGrantedRankAndDamageTurnsItOffBeforeRetry()
    {
        var actor = Actor();
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)151, (ushort)40) });
        Assert.Equal(40, AbilityPassives.Level(actor, 1111));
        Stealth.Land(actor, 1111, 0);
        var sneak = Assert.IsType<Stealth.State>(Stealth.Current(actor));
        Assert.True(Stealth.IsHidden(actor));
        Assert.Equal(40, Stealth.StealthLevelOf(actor));
        Assert.Contains(sneak.Mods, modifier => modifier.Stat == 115 && modifier.Value == -200);
        Stealth.HealthChanged(actor, 0, belowMaximum: true);
        Assert.Same(sneak, Stealth.Current(actor));
        Stealth.HealthChanged(actor, 1, belowMaximum: true);
        Assert.Null(Stealth.Current(actor));
        Stealth.Land(actor, 1111, 0);
        Assert.True(Stealth.IsHidden(actor));
        Stealth.Cleared(actor);
        Assert.False(Stealth.IsHidden(actor));
    }
}
