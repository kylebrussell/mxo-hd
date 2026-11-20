using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class OperativeBatchThreeTests
{
    private static WorldClient Actor(byte level = 50)
    {
        var actor = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        actor.playerData = new ClientData();
        actor.playerData.setDistrictId(1);
        actor.playerInstance = new PlayerCharacter();
        actor.playerInstance.Level.setValue(level);
        actor.playerInstance.Health.setValue((ushort)500);
        actor.playerInstance.MaxHealth.setValue((ushort)1000);
        actor.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(actor, long.MaxValue);
        return actor;
    }

    [Fact]
    public void OperativeIncreasesOnlySittingOutOfCombatRegenAndGrantsItsUpgrade()
    {
        var actor = Actor();
        actor.playerData.setOnWorld(true);
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)143, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(actor, 1060));
        HealthRegen.Tick(actor, 1);
        Assert.Equal(510, PvpCombat.HealthOf(actor));
        actor.playerData.stance = AnimationPackets.StanceSitting;
        Assert.Equal(0, HealthRegen.RateBonus(actor, inCombat: true));
        HealthRegen.Tick(actor, 1);
        Assert.Equal(525, PvpCombat.HealthOf(actor));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(actor, 1060));
        HealthRegen.Tick(actor, 1);
        Assert.Equal(535, PvpCombat.HealthOf(actor));
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)143, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(actor, 1060));
        HealthRegen.Tick(actor, 1);
        Assert.Equal(550, PvpCombat.HealthOf(actor));
    }

    [Theory]
    [InlineData(116, 1140, 7)]
    [InlineData(116, 1144, 7)]
    [InlineData(154, 1046, 2)]
    [InlineData(175, 1051, 25)]
    [InlineData(151, 1027, 5)]
    [InlineData(151, 1143, 5)]
    public void LoadedPassivesHaveScopedEffectsAndUnloadCleanly(ushort id, ushort stat, int value)
    {
        var actor = Actor();
        AbilityPassives.SetLoaded(actor, new[] { (id, (ushort)50) });
        Assert.Equal(value, AbilityStats.Player(actor, stat));
        if (id == 116)
        {
            Assert.Equal(15, AbilityStats.Toughness(actor, ranged: false));
            Assert.Equal(15, AbilityStats.Toughness(actor, ranged: true));
        }
        if (id == 154)
        {
            Assert.True(AbilityPassives.IsLoaded(actor, 1061));
            Assert.True(AbilityDesperation.TryGet(1149, 250, 500, AbilityStats.Player(actor, stat), out var buff));
            Assert.Equal(51, Assert.Single(buff.Modifiers).Value);
        }
        if (id == 175) Assert.True(AbilityPassives.IsLoaded(actor, 1116));
        if (id == 151)
        {
            Assert.Equal(2, AbilityStats.Defense(actor, ranged: false));
            Assert.Equal(2, AbilityStats.Defense(actor, ranged: true));
            Assert.True(AbilityPassives.IsLoaded(actor, 1111));
        }
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Player(actor, stat));
    }

    [Fact]
    public void ShadowLoadedGrantCanBeRemovedAndRestored()
    {
        var actor = Actor();
        var loaded = new[] { ((ushort)290, (ushort)50) };
        AbilityPassives.SetLoaded(actor, loaded, newWorld: true);
        Assert.True(AbilityPassives.IsLoaded(actor, 1117));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(actor, 1117));
        AbilityPassives.SetLoaded(actor, loaded);
        Assert.True(AbilityPassives.IsLoaded(actor, 1117));
    }

    [Fact]
    public void ShadowUpgradeIsSelfOnlyAndSuppliesItsExistingDurationAndCombatInputs()
    {
        var actor = Actor();
        Assert.True(AbilitySupport.Handles(1117));
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, 1117, 99)));
        Assert.True(AbilityBuffs.Apply(actor, 1117));
        Assert.Equal(3, AbilityStats.Player(actor, 644));
        Assert.Equal(6, AbilityStats.Player(actor, 1005));
        Assert.Equal(5, AbilityStats.ThrownAccuracy(actor));
        Assert.True(AbilityBuffs.Remove(actor, 1117));
        Assert.Equal(0, AbilityStats.Player(actor, 644));
        Assert.Equal(0, AbilityStats.Player(actor, 1005));
        Assert.Equal(0, AbilityStats.ThrownAccuracy(actor));
    }

    [Theory]
    [InlineData(185, 267, 1009, 15)]
    [InlineData(185, 105, 1009, 15)]
    [InlineData(123, 146, 1250, 20)]
    [InlineData(123, 1128, 1250, 20)]
    [InlineData(124, 130, 1250, 20)]
    [InlineData(135, 145, 1399, 20)]
    public void SelfDamageBuffsReachTheirTypedConsumersAndExpire(ushort id, ushort attack, int applied, float seconds)
    {
        var actor = Actor();
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, id, 99)));
        Assert.True(AbilityBuffs.TryGet(id, out var buff));
        Assert.Equal(seconds, buff.Seconds);
        Assert.Equal(1000, AbilityStats.ScaleAbilityDamage(actor, attack, 1000));
        Assert.True(AbilityBuffs.Apply(actor, id));
        Assert.Equal(applied, AbilityStats.ScaleAbilityDamage(actor, attack, 1000));
        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + seconds + 1);
        Assert.Equal(1000, AbilityStats.ScaleAbilityDamage(actor, attack, 1000));
    }

    [Theory]
    [InlineData(124, 135, 145, 1399)]
    [InlineData(135, 124, 130, 1250)]
    public void SoldierDamageSpecializationsReplaceEachOther(ushort first, ushort second, ushort attack, int damage)
    {
        var actor = Actor();
        Assert.True(AbilityBuffs.Apply(actor, 185));
        Assert.True(AbilityBuffs.Apply(actor, first));
        Assert.False(AbilityBuffs.IsActive(actor, 185));
        Assert.True(AbilityBuffs.Apply(actor, second));
        Assert.False(AbilityBuffs.IsActive(actor, first));
        Assert.True(AbilityBuffs.IsActive(actor, second));
        Assert.Equal(damage, AbilityStats.ScaleAbilityDamage(actor, attack, 1000));
        Assert.Equal(1000, AbilityStats.ScaleAbilityDamage(actor, second == 135 ? (ushort)130 : (ushort)145, 1000));
    }

    [Theory]
    [InlineData(23, 6)]
    [InlineData(50, 15)]
    public void StealthAwarenessUsesCasterLevelAndFeedsTheDetectionThreshold(byte level, int bonus)
    {
        var actor = Actor(level);
        actor.playerInstance.StealthAwareness.setValue((byte)1);
        Assert.False(Stealth.SneakTest(1, 30, 0, 300, 0));
        Assert.True(AbilityBuffs.Apply(actor, 156));
        Assert.Equal(1 + bonus, Stealth.AwarenessOf(actor));
        Assert.True(Stealth.SneakTest(Stealth.AwarenessOf(actor), 30, 0, 300, 0));
        Assert.True(AbilityBuffs.Remove(actor, 156));
        Assert.Equal(1, Stealth.AwarenessOf(actor));
    }

    [Theory]
    [InlineData(150, 519, 20, 300)]
    [InlineData(110, 520, 20, 300)]
    [InlineData(291, 115, 40, 15)]
    [InlineData(291, 877, 20, 15)]
    [InlineData(291, 878, 20, 15)]
    public void SelfBuffsIgnoreHostileSelectionAndExpire(ushort id, ushort stat, int value, float seconds)
    {
        var actor = Actor();
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, id, 99)));
        Assert.True(AbilityBuffs.TryGet(id, out var buff));
        Assert.Equal(seconds, buff.Seconds);
        Assert.True(AbilityBuffs.Apply(actor, id));
        Assert.Equal(value, AbilityStats.Player(actor, stat));
        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + seconds + 1);
        Assert.Equal(0, AbilityStats.Player(actor, stat));
    }

    [Fact]
    public void SideRollEvasionOnlyAppliesOnLandingAndExpires()
    {
        var actor = Actor();
        Assert.False(AbilitySupport.Handles(121));
        Assert.Equal(0, AbilityStats.Player(actor, 520));
        CombatSpecials.Landed(actor, 121);
        Assert.Equal(20, AbilityStats.Player(actor, 520));
        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + 31);
        Assert.Equal(0, AbilityStats.Player(actor, 520));
    }

    [Theory]
    [InlineData(281, 61, 50, 5, 9, 6)]
    [InlineData(146, 17, 25, 2.5, 9, 2)]
    public void ShippedThrownDotUsesTypedRoundedTicksAndNoTerminalTick(
        ushort id, int value, float seconds, double interval, int count, int tick)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        var dot = Assert.Single(TimedEffects.Instantiate(effect, new Random(1)), m => m.Proc == TempMods.Proc.Dot);
        Assert.Equal(value, dot.Value);
        Assert.Equal(7, dot.DamageType);
        Assert.Equal(seconds, dot.Seconds);
        Assert.Equal(interval, dot.Interval);
        var holder = new TimedEffects.Holder();
        holder.Apply(id, new[] { dot }, 100);
        var ticks = new List<TimedEffects.Active>();
        var expired = new List<TimedEffects.Active>();
        for (int n = 1; n <= count; n++)
        {
            holder.Advance(100 + n * interval - .01, ticks, expired);
            Assert.Empty(ticks);
            holder.Advance(100 + n * interval, ticks, expired);
            Assert.Equal(tick, Assert.Single(ticks).TickDamage);
            ticks.Clear();
        }
        holder.Advance(100 + seconds, ticks, expired);
        Assert.Empty(ticks);
        Assert.Same(dot, Assert.Single(expired));
    }

    private sealed class Draw(double roll) : Random
    {
        public override double NextDouble() => roll;
    }

    [Fact]
    public void OverhandSmashUsesItsChanceAndIndependentAccuracyAndDefensePenalties()
    {
        Assert.True(TempMods.TryGet(145, out var effect));
        var npc = new Mob();
        var holder = TimedEffects.ForMob(npc).Effects;
        Assert.DoesNotContain(TimedEffects.Instantiate(effect, new Draw(.101)), m => m.Target == TempMods.EnrageAbility);
        var landed = TimedEffects.Instantiate(effect, new Draw(.099));
        Assert.Equal(10, Assert.Single(landed, m => m.Target == TempMods.EnrageAbility).Seconds);
        holder.Apply(145, landed, 100);
        Assert.Equal(-10, AbilityStats.Attack(npc, false));
        Assert.Equal(-10, AbilityStats.Defense(npc, false));
        Assert.False(holder.HasAt(TempMods.EnrageAbility, 110));
        holder.Advance(110, new(), new());
        Assert.Equal(0, AbilityStats.Attack(npc, false));
        Assert.Equal(0, AbilityStats.Defense(npc, false));
    }

    [Theory]
    [InlineData(105, 180, -5, 10)]
    [InlineData(105, 181, -5, 10)]
    [InlineData(130, 115, -50, 8)]
    [InlineData(130, 877, -50, 8)]
    [InlineData(130, 878, -50, 8)]
    [InlineData(130, 519, -10, 8)]
    public void PhysicalDebuffsReachTheLiveTimedHolder(ushort id, ushort stat, int value, float seconds)
    {
        var npc = new Mob();
        var holder = TimedEffects.ForMob(npc).Effects;
        Assert.True(TempMods.TryGet(id, out var effect));
        var landed = TimedEffects.Instantiate(effect, new Draw(0));
        Assert.Equal(seconds, Assert.Single(landed, m => m.Target == stat).Seconds);
        holder.Apply(id, landed, 100);
        Assert.Equal(value, id == 105 ? AbilityStats.Toughness(npc, stat == 181) : AbilityStats.Npc(npc, stat));
        holder.Advance(100 + seconds, new(), new());
        Assert.Equal(0, AbilityStats.Npc(npc, stat));
    }

    [Fact]
    public void BullDogOpeningBonusUsesTheExistingCapturedStealthFlag()
    {
        Assert.Equal(75, SneakAttacks.Add(75, 387, fromStealth: false));
        Assert.Equal(1025, SneakAttacks.Add(75, 387, fromStealth: true));
    }

    [Fact]
    public void LoadingAndRemovingMultiFightingReversesThePenaltyInput()
    {
        var actor = Actor();
        int Penalty() => MultifightingInterlock.Penalty(3, MultifightingInterlock.Loaded(actor.playerData));
        Assert.Equal(-15, Penalty());
        var loaded = new[] { ((ushort)140, (ushort)50) };
        AbilityPassives.SetLoaded(actor, loaded);
        Assert.Equal(-5, Penalty());
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(-15, Penalty());
        AbilityPassives.SetLoaded(actor, loaded);
        Assert.Equal(-5, Penalty());
    }
}
