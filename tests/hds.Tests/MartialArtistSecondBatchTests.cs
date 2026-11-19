using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MartialArtistSecondBatchTests
{
    private static WorldClient Actor()
    {
        var actor = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        actor.playerData = new ClientData();
        actor.playerData.setDistrictId(1);
        actor.playerInstance = new PlayerCharacter();
        actor.playerInstance.Level.setValue((byte)50);
        actor.playerInstance.MaxHealth.setValue((ushort)500);
        actor.playerInstance.Health.setValue((ushort)100);
        actor.playerInstance.InnerStrengthMax.setValue((ushort)200);
        actor.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        actor.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(actor, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(actor, new WorldSendFence());
        return actor;
    }

    private static AbilityItem Item(ushort id)
    {
        var item = new AbilityItem();
        item.setAbilityID(id);
        return item;
    }

    private static ushort MaximumHealth(WorldClient actor) =>
        NumericalUtils.ByteArrayToUint16(actor.playerInstance.MaxHealth.getValue(), 1);

    [Theory]
    [InlineData(101, 64, 154, 57, 74)]
    [InlineData(132, 64, 158, 45, 72)]
    [InlineData(133, 74, 156, 49, 70)]
    public void SelectedStyleFeedsTypedContestsOrdinaryWithdrawalAndDamage(ushort style, int accuracy, int hit, int defense, int withdraw)
    {
        var actor = Actor();
        AbilityPassives.SetLoaded(actor, new (ushort, ushort)[] { (101, 50), (132, 50), (133, 50) });
        CombatStyles.Select(actor, style);
        Assert.Equal(style, CombatStyles.Active(actor));
        Assert.Equal(accuracy, AbilityStats.Attack(actor, false));
        Assert.Equal(hit, AbilityStats.ScaleAbilityDamage(actor, 427, 123));
        Assert.Equal(defense, AbilityStats.Defense(actor, false));
        Assert.Equal(defense, AbilityStats.Defense(actor, true));
        Assert.Equal(defense, AbilityStats.SpecialDefense(actor, Item(330)));
        Assert.Equal(defense, AbilityStats.SpecialDefense(actor, Item(134)));
        Assert.Equal(withdraw, WithdrawalInput.Attack(actor, null, ordinary: true));
        Assert.Equal(50 + withdraw, WithdrawalInput.AttackBase(50, withdraw, CombatTactic.Speed));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(actor));
        Assert.NotEqual(accuracy, AbilityStats.Attack(actor, false));
    }

    [Theory]
    [InlineData(258, 3, 126, 525, 0, 0, 4)]
    [InlineData(136, 2, 129, 500, 0, 0, 4)]
    [InlineData(431, 0, 124, 500, 5, 3, 4.4f)]
    [InlineData(860, 0, 123, 500, 0, 100, 4)]
    public void LoadedRankedPassivesReachCombatAndVitalConsumers(ushort id, int accuracy, int hit,
        int health, int defense, int viral, float stun)
    {
        var actor = Actor();
        AbilityPassives.SetLoaded(actor, new[] { (id, (ushort)50) });
        Assert.Equal(accuracy, AbilityStats.Attack(actor, false));
        Assert.Equal(hit, AbilityStats.ScaleAbilityDamage(actor, 427, 123));
        Assert.Equal(health, MaximumHealth(actor));
        Assert.Equal(defense, AbilityStats.Defense(actor, false));
        Assert.Equal(viral, AbilityStats.SpecialDefense(actor, Item(330)));
        Assert.Equal(stun, AbilityControlDurations.Scale(actor, TempMods.StunAbility, 4), 4);
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Attack(actor, false));
        Assert.Equal(123, AbilityStats.ScaleAbilityDamage(actor, 427, 123));
        Assert.Equal(500, MaximumHealth(actor));
        Assert.Equal(0, AbilityStats.SpecialDefense(actor, Item(330)));
    }

    [Theory]
    [InlineData(137, 1062, 91, 123)]
    [InlineData(257, 1063, 90, 124)]
    public void DisciplineMembershipGrantsUpgradeAndFeedsDesperation(ushort parent, ushort child, int initiative, int hit)
    {
        var actor = Actor();
        actor.playerData.setOnWorld(true);
        actor.playerInstance.Health.setValue((ushort)51);
        AbilityPassives.SetLoaded(actor, new[] { (parent, (ushort)50) });
        AbilityGrantRegistration.LoadingComplete(actor, new byte[] { 1 });
        Assert.True(AbilityBuffs.Apply(actor, 1149));
        Assert.Equal(initiative, AbilityStats.Player(actor, 1044));
        Assert.Equal(hit, AbilityStats.ScaleAbilityDamage(actor, 427, 123));
        byte[] on = AbilityGrantRegistration.Record(new(parent, child, 0), false);
        Assert.Contains(actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(), message => message.content.AsSpan(1).SequenceEqual(on));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        byte[] off = AbilityGrantRegistration.Record(new(parent, child, 0), true);
        Assert.Contains(actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(), message => message.content.AsSpan(1).SequenceEqual(off));
        Assert.False(AbilityBuffs.IsActive(actor, 1149));
        Assert.Equal(123, AbilityStats.ScaleAbilityDamage(actor, 427, 123));
    }

    [Theory]
    [InlineData(1062, 4, 125, 4, 570)]
    [InlineData(1063, 6, 125, 4, 590)]
    [InlineData(1064, 13, 130, 8, 637)]
    public void GrantedUpgradeRoutesToSelfAndChangesFourLiveInputs(ushort id, int accuracy, int hit, int defense, int health)
    {
        var actor = Actor();
        Assert.True(AbilitySupport.Handles(id));
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, id, 7)));
        Assert.True(AbilityBuffs.Apply(actor, id));
        Assert.Equal(accuracy, AbilityStats.Attack(actor, false));
        Assert.Equal(hit, AbilityStats.ScaleAbilityDamage(actor, 427, 123));
        Assert.Equal(defense, AbilityStats.Defense(actor, false));
        Assert.Equal(health, MaximumHealth(actor));
        Assert.True(AbilityBuffs.Remove(actor, id));
        Assert.Equal(500, MaximumHealth(actor));
        Assert.Equal(0, AbilityStats.Defense(actor, false));
    }

    [Fact]
    public void IronGuardUsesLandedCasterDefenseAndKeepsOpponentDamage()
    {
        var actor = Actor();
        Assert.False(AbilitySupport.Handles(139));
        Assert.Null(CombatSpecials.Damage(139, null));
        Assert.Equal(0, AbilityStats.Defense(actor, false));
        CombatSpecials.Landed(actor, 139);
        Assert.Equal(10, AbilityStats.Defense(actor, false));
        Assert.Equal(10, AbilityStats.Defense(actor, true));
        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + 12.1);
        Assert.Equal(0, AbilityStats.Defense(actor, false));
        Assert.Equal(0, AbilityStats.Defense(actor, true));
    }

    private sealed class Draw(double fraction = 0, int percentage = 0) : Random
    {
        public override double NextDouble() => fraction;
        public override int Next(int maxValue) => percentage;
    }

    [Theory]
    [InlineData(427, 96, 6f, .99, -1)]
    [InlineData(428, 91, 4f, .29, .31)]
    [InlineData(430, 96, 6f, .99, -1)]
    [InlineData(259, 96, 6f, .99, -1)]
    [InlineData(260, 90, 4f, .99, -1)]
    [InlineData(262, 102, 4f, .39, .41)]
    [InlineData(869, 231, 20f, .24, .26)]
    [InlineData(870, 96, 24.6f, .99, -1)]
    public void LandedControlsUsePerMoveDurationAndProcBoundary(ushort id, ushort state, float seconds, double hit, double miss)
    {
        var recipient = TimedEffects.ForPlayer(Actor());
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.True(TempMods.TryGet(id, out var effect));
        try
        {
            if (miss >= 0)
                Assert.DoesNotContain(TimedEffects.Apply(recipient, effect, now, rng: new Draw(miss)), mod => mod.Target == state);
            Assert.Equal(seconds, Assert.Single(TimedEffects.Apply(recipient, effect, now, rng: new Draw(hit)),
                mod => mod.Target == state).Seconds);
            Assert.True(recipient.Effects.HasAt(state, now + seconds - .1));
            Assert.False(recipient.Effects.HasAt(state, now + seconds + .1));
        }
        finally { TimedEffects.Clear(recipient); }
    }

    [Fact]
    public void ExtremeFallingKickTermHonorsTenPercentProcAndSeparateImmunity()
    {
        var actor = Actor();
        var recipient = TimedEffects.ForPlayer(actor);
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.True(TempMods.TryGet(429, out var effect));
        try
        {
            Assert.Empty(TimedEffects.Apply(recipient, effect, now, rng: new Draw(.11)));
            TimedEffects.Apply(recipient, effect, now, rng: new Draw(.09));
            TimedEffects.RunDue(now + .05);
            Assert.False(recipient.Effects.HasAt(TempMods.StunAbility, now + .05));
            TimedEffects.RunDue(now + .11);
            var stun = Assert.Single(recipient.Effects.Snapshot(), mod => mod.Target == TempMods.StunAbility);
            var timer = Assert.Single(recipient.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility);
            Assert.Equal(4, stun.Seconds);
            Assert.Equal(4, timer.Seconds);
            Assert.Equal(100, AbilityHandler.CurrentInnerStrength(actor));
            TimedEffects.Apply(recipient, effect, now + 1, rng: new Draw(.09));
            TimedEffects.RunDue(now + 1.11);
            Assert.Same(timer, Assert.Single(recipient.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility));
            TimedEffects.RunDue(now + 4.12);
            Assert.True(recipient.Effects.IsEmpty);
        }
        finally { TimedEffects.Clear(recipient); }
    }

    [Theory]
    [InlineData(138, 1009, 15, .99, -1)]
    [InlineData(870, 163, 15, .24, .26)]
    public void TimedDebuffsReachNpcContestInputAndExpire(ushort id, ushort stat, int seconds, double hit, double miss)
    {
        var mob = new Mob();
        var recipient = TimedEffects.ForMob(mob);
        double now = TimeUtils.GetSimTimeSeconds();
        int ContestInput() => stat == 163 ? AbilityStats.Attack(mob, false) : AbilityStats.Defense(mob, false);
        Assert.True(TempMods.TryGet(id, out var effect));
        try
        {
            if (miss >= 0)
                Assert.DoesNotContain(TimedEffects.Apply(recipient, effect, now, rng: new Draw(miss)), mod => mod.Target == stat);
            Assert.Equal(0, ContestInput());
            Assert.Equal(seconds, Assert.Single(TimedEffects.Apply(recipient, effect, now, rng: new Draw(hit)),
                mod => mod.Target == stat).Seconds);
            Assert.Equal(-10, ContestInput());
            TimedEffects.RunDue(now + seconds + .1);
            Assert.Equal(0, ContestInput());
        }
        finally { TimedEffects.Clear(recipient); }
    }

    [Theory]
    [InlineData(0.19, true)]
    [InlineData(0.20, false)]
    public void MachinegunFistDispelKeepsHostileDebuffAndExpiresOneBuff(double draw, bool dispels)
    {
        var source = Actor();
        var target = Actor();
        Assert.True(AbilityBuffs.Apply(target, 327));
        var recipient = TimedEffects.ForPlayer(target);
        try
        {
            Assert.True(TempMods.TryGet(138, out var effect));
            TimedEffects.Apply(recipient, effect, TimeUtils.GetSimTimeSeconds());
            AbilityHostile.LandedOnPlayer(source, target, 261, new Draw(draw));
            Assert.Equal(!dispels, AbilityBuffs.IsActive(target, 327));
            Assert.True(recipient.Effects.HasParent(138));
        }
        finally
        {
            TimedEffects.Clear(recipient);
            AbilityBuffs.RemoveAll(target);
        }
    }

    [Fact]
    public void BalanceHealsItsOwnerOnDamageAndExternalHealingThenExpires()
    {
        var owner = Actor();
        var other = Actor();
        var original = AbilityHealthCallbacks.Random15;
        try
        {
            AbilityHealthCallbacks.Random15 = () => 32767;
            Assert.True(AbilityBuffs.Apply(owner, 861));
            Assert.True(AbilityBuffs.Apply(owner, 170));
            PvpCombat.Damage(owner, 1);
            Assert.Equal(198, PvpCombat.HealthOf(owner));
            Assert.Equal(298, AbilityHandler.HealSelf(owner, 1));
            PvpCombat.Damage(other, 1);
            Assert.Equal(99, PvpCombat.HealthOf(other));
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + 30.1);
            PvpCombat.Damage(owner, 1);
            Assert.Equal(297, PvpCombat.HealthOf(owner));
        }
        finally
        {
            AbilityHealthCallbacks.Random15 = original;
            AbilityBuffs.RemoveAll(owner);
        }
    }

    [Theory]
    [InlineData(24, true)]
    [InlineData(25, false)]
    public void PurityUsesPerDebuffTwentyFivePercentWithoutKeepingABuff(int roll, bool cleansed)
    {
        var actor = Actor();
        var subject = TimedEffects.ForPlayer(actor);
        var original = AbilityHostile.PulseRandom;
        try
        {
            Assert.True(TempMods.TryGet(138, out var effect));
            TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds());
            Assert.True(AbilityBuffs.Apply(actor, 327));
            AbilityHostile.PulseRandom = new Draw(percentage: roll);
            AbilitySupport.Land(actor, 868, 2);
            Assert.Equal(!cleansed, subject.Effects.HasParent(138));
            Assert.True(AbilityBuffs.IsActive(actor, 327));
            Assert.False(AbilityBuffs.IsActive(actor, 868));
        }
        finally
        {
            AbilityHostile.PulseRandom = original;
            TimedEffects.Clear(subject);
            AbilityBuffs.RemoveAll(actor);
        }
    }

    [Fact]
    public void ZenMasterListenerIsOwnerOnlyAndChildCannotBeInvokedDirectly()
    {
        var owner = Actor();
        var childOnly = Actor();
        var original = AbilityHealthCallbacks.Random100;
        try
        {
            AbilityPassives.SetLoaded(owner, new (ushort, ushort)[] { (871, 50) });
            AbilityPassives.SetLoaded(childOnly, new (ushort, ushort)[] { (872, 50) });
            AbilityHealthCallbacks.Random100 = () => 15;
            PvpCombat.Damage(owner, 1);
            Assert.Equal(100, AbilityHandler.CurrentInnerStrength(owner));
            AbilityHealthCallbacks.Random100 = () => 14;
            AbilityHealthCallbacks.Reapply(owner, 872);
            PvpCombat.Damage(childOnly, 1);
            Assert.Equal(100, AbilityHandler.CurrentInnerStrength(childOnly));
            Assert.Equal(100, AbilityHandler.CurrentInnerStrength(owner));
            owner.playerInstance.InnerStrengthAvailable.setValue((ushort)197);
            PvpCombat.Damage(owner, 1);
            Assert.Equal(200, AbilityHandler.CurrentInnerStrength(owner));
            Assert.False(AbilityProgression.MeetsCharacterLevel(871, 50));
        }
        finally { AbilityHealthCallbacks.Random100 = original; }
    }
}
