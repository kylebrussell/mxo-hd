using hds;
using System.Runtime.CompilerServices;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityScaledBuffTests
{
    private static WorldClient Client(byte level)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData(); c.playerInstance=new PlayerCharacter(); c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue(level);c.playerInstance.MaxHealth.setValue((ushort)500);c.playerInstance.Health.setValue((ushort)500);
        c.playerInstance.InnerStrengthMax.setValue((ushort)200);c.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }

    [Fact]
    public void PistolEvasiveKeepsTheHitAndAppliesThreeDefensesOnlyToItsCaster()
    {
        var caster = Client(50);
        var target = Client(50);
        var attack = new AbilityItem();
        attack.setAbilityID(279);
        Assert.True(CombatSpecials.TargetsEnemy(242));
        Assert.Null(CombatSpecials.Damage(242, null));
        Assert.False(AbilitySupport.Handles(242));

        CombatSpecials.Landed(caster, 242);
        Assert.Equal(12f, caster.playerData.activeBuffs[242].Definition.Seconds);
        Assert.Equal(20, AbilityStats.Defense(caster, false));
        Assert.Equal(20, AbilityStats.Defense(caster, true));
        Assert.Equal(20, AbilityStats.SpecialDefense(caster, attack));
        Assert.Equal(0, AbilityStats.SpecialDefense(target, attack));
        TimedEffects.RunDue(caster.playerData.activeBuffs[242].Started + 12.1);
        Assert.Equal(0, AbilityStats.Defense(caster, false));
        Assert.Equal(0, AbilityStats.Defense(caster, true));
        Assert.Equal(0, AbilityStats.SpecialDefense(caster, attack));
    }

    [Theory]
    [InlineData(129, 55, 74)]
    [InlineData(147, 49, 70)]
    [InlineData(158, 51, 72)]
    public void WeaponStyleFeedsTypedDefenseAndWithdrawalOnlyWhileSelected(ushort style, int defense, int withdraw)
    {
        var actor = Client(50);
        AbilityPassives.SetLoaded(actor, new[] { (style, (ushort)50), ((ushort)2, (ushort)50) });
        CombatStyles.Select(actor, style);
        Assert.Equal(64, AbilityStats.Attack(actor, ranged: true));
        Assert.Equal(13, AbilityStats.Influence(actor, 171));
        Assert.Equal(defense, AbilityStats.Defense(actor, ranged: true));
        Assert.Equal(defense, AbilityStats.Defense(actor, ranged: false));
        foreach (ushort attack in new ushort[] { 330, 279 })
        {
            var item = new AbilityItem();
            item.setAbilityID(attack);
            Assert.Equal(defense, AbilityStats.SpecialDefense(actor, item));
        }
        Assert.Equal(withdraw, WithdrawalInput.Attack(actor, null, ordinary: true));
        CombatStyles.Select(actor, 17);
        Assert.Equal(50, AbilityStats.Attack(actor, ranged: true));
        Assert.Equal(12, AbilityStats.Influence(actor, 171));
        Assert.Equal(47, AbilityStats.Defense(actor, ranged: true));
    }

    [Theory]
    [InlineData(1076, 3, 110, 4, 10)]
    [InlineData(1074, 9, 120, 4, 4)]
    [InlineData(1070, 4, 70, 2, 4)]
    [InlineData(1075, 4, 100, 2, 6)]
    [InlineData(1060, 4, 0, 0, 0)]
    public void SelfUpgradesApplyTheirOwnCombatStatsAndRemoveWithTheNextUpgrade(
        ushort ability, int accuracy, int health, int damage, int defense)
    {
        var actor = Client(50);
        actor.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 0));
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, ability, 99)));
        AbilitySupport.Land(actor, ability, 99);
        Assert.True(AbilityBuffs.IsActive(actor, ability));
        Assert.Equal(accuracy, AbilityStats.Attack(actor, ranged: true));
        Assert.Equal(ability == 1060 ? accuracy : 0, AbilityStats.Attack(actor, ranged: false));
        Assert.Equal(500 + health, NumericalUtils.ByteArrayToUint16(actor.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(500, PvpCombat.HealthOf(actor));
        Assert.Equal(damage, AbilityStats.Influence(actor, 171));
        Assert.Equal(defense, AbilityStats.Defense(actor, ranged: true));

        ushort replacement = ability == 1076 ? (ushort)1074 : (ushort)1076;
        AbilitySupport.Land(actor, replacement, 99);
        Assert.False(AbilityBuffs.IsActive(actor, ability));
        Assert.True(AbilityBuffs.Remove(actor, replacement));
        Assert.Equal(0, AbilityStats.Attack(actor, ranged: true));
        Assert.Equal(0, AbilityStats.Attack(actor, ranged: false));
        Assert.Equal(500, NumericalUtils.ByteArrayToUint16(actor.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(0, AbilityStats.Influence(actor, 171));
        Assert.Equal(0, AbilityStats.Defense(actor, ranged: true));
    }

    [Theory]
    [InlineData(128, 1, 1070)]
    [InlineData(240, 2, 1075)]
    public void LoadedGunmanAndGunslingerRetainTheShippedInitiativeBonusAndGrant(
        ushort ability, int bonus, ushort grant)
    {
        var actor = Client(50);
        AbilityPassives.SetLoaded(actor, new[] { (ability, (ushort)50) }, true);
        Assert.Equal(bonus, AbilityPassives.Total(actor, 1046));
        Assert.True(AbilityPassives.IsLoaded(actor, grant));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>(), true);
        Assert.Equal(0, AbilityPassives.Total(actor, 1046));
        Assert.False(AbilityPassives.IsLoaded(actor, grant));
    }

    [Theory]
    [InlineData(125, 2, 5, 0, 0, 0, 0)]
    [InlineData(440, 2, 1, 2, 0, 0, 1074)]
    [InlineData(441, 0, 0, 15, 10, 20, 0)]
    [InlineData(239, 2, 2, 0, 0, 0, 0)]
    [InlineData(240, 0, 1, 0, 0, 0, 1075)]
    public void GunmanPassivesFeedTheirOwnCombatConsumersAndUnload(
        ushort ability, int accuracy, int damage, int rootBonus, int stunBonus, int toughness, ushort grant)
    {
        var actor = Client(50);
        AbilityPassives.SetLoaded(actor, new[] { (ability, (ushort)50) }, true);
        Assert.Equal(accuracy, AbilityStats.Attack(actor, ranged: true));
        Assert.Equal(0, AbilityStats.Attack(actor, ranged: false));
        Assert.Equal(damage, AbilityStats.Influence(actor, 171));
        Assert.Equal(10f * (1 + rootBonus / 100f), AbilityControlDurations.Scale(actor, TempMods.RootedAbility, 10), 3);
        Assert.Equal(10f * (1 + stunBonus / 100f), AbilityControlDurations.Scale(actor, TempMods.StunAbility, 10), 3);
        Assert.Equal(toughness, AbilityStats.Toughness(actor, ranged: true));
        Assert.Equal(ability == 239 ? 13 : 0, AbilityStats.Toughness(actor, ranged: false));
        if (grant != 0) Assert.True(AbilityPassives.IsLoaded(actor, grant));

        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>(), true);
        Assert.Equal(0, AbilityStats.Attack(actor, ranged: true));
        Assert.Equal(0, AbilityStats.Influence(actor, 171));
        Assert.Equal(10f, AbilityControlDurations.Scale(actor, TempMods.RootedAbility, 10));
        Assert.Equal(10f, AbilityControlDurations.Scale(actor, TempMods.StunAbility, 10));
        Assert.Equal(0, AbilityStats.Toughness(actor, ranged: true));
        Assert.Equal(0, AbilityStats.Toughness(actor, ranged: false));
        if (grant != 0) Assert.False(AbilityPassives.IsLoaded(actor, grant));
    }

    [Theory]
    [InlineData(1, 181, 30, 1, 254)]
    [InlineData(5, 1033, 30, 7, 279)]
    [InlineData(6, 180, 235, 0, 198)]
    [InlineData(8, 157, 30, 2, 330)]
    public void AwakenedResistanceChangesOnlyTheMatchingDamageAndExpires(
        ushort ability, ushort stat, int magnitude, int type, ushort attack)
    {
        var defender = Client(20);
        var attacker = Client(20);
        double start = TimeUtils.GetSimTimeSeconds();
        Assert.True(AbilityBuffs.Apply(defender, ability));
        Assert.Equal(magnitude, AbilityBuffs.Total(defender, stat));
        Assert.Equal(20f, defender.playerData.activeBuffs[ability].Definition.Seconds);
        int effective = AbilityStats.Player(defender, stat);
        Assert.True(effective > 0);
        if (type < 2)
        {
            Assert.Equal(effective, AbilityStats.Toughness(defender, ranged: type == 1));
            Assert.Equal(0, AbilityStats.Toughness(defender, ranged: type != 1));
        }
        Assert.InRange(TypedAbilityResistance.Player(attacker, defender, attack, 200, type), (ushort)1, (ushort)199);
        Assert.Equal(200, TypedAbilityResistance.Player(attacker, defender, attack, 200, (type + 1) % 3));
        TimedEffects.RunDue(start + 19);
        Assert.True(AbilityBuffs.IsActive(defender, ability));
        TimedEffects.RunDue(start + 21);
        Assert.False(AbilityBuffs.IsActive(defender, ability));
        Assert.Equal(200, TypedAbilityResistance.Player(attacker, defender, attack, 200, type));
    }

    [Theory]
    [InlineData(10, 1010, 51, 254)]
    [InlineData(1099, 1009, 52, 198)]
    [InlineData(1100, 183, 77, 330)]
    [InlineData(1101, 1011, 47, 279)]
    public void HyperDefensesFeedTheirOwnContestAndRemoveTheOldFamilyMember(
        ushort ability, ushort stat, int captured, ushort opposing)
    {
        var defender = Client(50);
        AbilityPassives.SetLoaded(defender, new[] { (ability, (ushort)50) }, true);
        var move = new AbilityItem();
        move.setAbilityID(opposing);
        Assert.Equal(0, AbilityStats.SpecialDefense(defender, move));
        Assert.True(AbilityBuffs.Apply(defender, ability));
        Assert.Equal(captured, AbilityBuffs.Total(defender, stat));
        Assert.Equal(AbilityStats.Player(defender, stat), AbilityStats.SpecialDefense(defender, move));
        Assert.True(AbilityStats.SpecialDefense(defender, move) > 0);

        ushort replacement = ability == 1099 ? (ushort)10 : (ushort)1099;
        Assert.True(AbilityBuffs.Apply(defender, replacement));
        Assert.False(AbilityBuffs.IsActive(defender, ability));
        Assert.Equal(0, AbilityStats.SpecialDefense(defender, move));
        AbilityBuffs.Remove(defender, replacement);
    }

    [Fact]
    public void DuelistLinksDefensePowerlessDurationAndUpgrade()
    {
        var fighter = Client(50);
        int baseline = InterlockInitiative.Attack(fighter, null);
        AbilityPassives.SetLoaded(fighter, new[] { ((ushort)497, (ushort)50) }, true);
        Assert.Equal(2, AbilityPassives.Total(fighter, 1046));
        Assert.Equal(2, AbilityStats.Defense(fighter, ranged: true));
        Assert.Equal(10.2f, AbilityControlDurations.Scale(fighter, TempMods.PowerlessAbility, 10f), 3);
        Assert.True(AbilityPassives.IsLoaded(fighter, 1076));

        AbilityPassives.SetLoaded(fighter, Array.Empty<(ushort, ushort)>(), true);
        Assert.Equal(baseline, InterlockInitiative.Attack(fighter, null));
        Assert.Equal(0, AbilityStats.Defense(fighter, ranged: true));
        Assert.Equal(10f, AbilityControlDurations.Scale(fighter, TempMods.PowerlessAbility, 10f));
        Assert.False(AbilityPassives.IsLoaded(fighter, 1076));
    }

    [Fact]
    public void HyperStrengthFeedsPhysicalInfluenceOnlyWhileEnabled()
    {
        var actor = Client(50);
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)13, (ushort)20) }, true);
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(actor, 0, 100));
        Assert.True(AbilityBuffs.Apply(actor, 13));
        Assert.Equal(40, AbilityBuffs.Total(actor, 159));
        Assert.Equal(139, AbilityStats.ScaleTypedAbilityDamage(actor, 0, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(actor, 1, 100));
        Assert.True(AbilityBuffs.Remove(actor, 13));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(actor, 0, 100));
    }

    [Fact]
    public void HeadButtLowersMeleeAccuracyUntilItsStoredExpiry()
    {
        var attacker = Client(50);
        var target = Client(50);
        Assert.True(TempMods.TryGet(197, out var effect));
        double start = TimeUtils.GetSimTimeSeconds();
        var active = Assert.Single(TimedEffects.Apply(TimedEffects.ForPlayer(target), effect, start, attacker, "Head Butt"));
        Assert.Equal(20f, active.Seconds);
        Assert.Equal(-10, AbilityStats.Attack(target, ranged: false));
        Assert.Equal(0, AbilityStats.Attack(target, ranged: true));
        TimedEffects.RunDue(active.Expires + 0.01);
        Assert.Equal(0, AbilityStats.Attack(target, ranged: false));
    }

    [Fact]
    public void AwakenedGrantAndDetectionFollowTheLoadedRankAndUnload()
    {
        var actor = Client(50);
        actor.playerInstance.StealthAwareness.setValue((byte)1);
        Assert.Equal(1, Stealth.AwarenessOf(actor));
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)2, (ushort)20) }, true);
        Assert.Equal(20, AbilityPassives.Level(actor, 17));
        Assert.Equal(6, Stealth.AwarenessOf(actor));
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)2, (ushort)50) }, true);
        Assert.Equal(50, AbilityPassives.Level(actor, 17));
        Assert.Equal(13, Stealth.AwarenessOf(actor));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>(), true);
        Assert.False(AbilityPassives.IsLoaded(actor, 17));
        Assert.Equal(1, Stealth.AwarenessOf(actor));
    }

    [Fact]
    public void VirologistGrantsTheSelfUpgradeAndFeedsItsTypedConsumers()
    {
        var target = Client(50);
        AbilityPassives.SetLoaded(target, new[] { ((ushort)98, (ushort)50) }, true);
        Assert.True(AbilityPassives.IsLoaded(target, 1077));
        Assert.Equal(1, AbilityStats.Player(target, 1030));
        Assert.Equal(1, AbilityStats.Player(target, 1045));
        Assert.Same(target, Assert.Single(AbilitySupport.Targets(target, 1077, 123)));
        AbilitySupport.Land(target, 1077, 123);
        Assert.Equal(5, AbilityStats.Player(target, 564));
        Assert.Equal(77, AbilityCastInterrupts.CastThroughPercent(1, 500, (short)AbilityStats.Influence(target, 564)));
        Assert.Equal(3, AbilityStats.ViralAccuracy(target));
        Assert.Equal(20, AbilityStats.Player(target, 157));
        Assert.True(TypedAbilityResistance.Player(Client(50), target, 330, 100, 2) < 100);
        Assert.True(AbilityBuffs.Remove(target, 1077));
        Assert.Equal(0, AbilityStats.Player(target, 564));
        Assert.Equal(0, AbilityStats.ViralAccuracy(target));
        Assert.Equal(100, TypedAbilityResistance.Player(Client(50), target, 330, 100, 2));
        AbilityPassives.SetLoaded(target, Array.Empty<(ushort, ushort)>(), true);
        Assert.False(AbilityPassives.IsLoaded(target, 1077));
    }

    [Fact]
    public void TransmitVirusAndOverclockApplyTheirOwnTypedDamageAndResistance()
    {
        var target = Client(50);
        var attacker = Client(50);
        AbilityPassives.SetLoaded(target, new[] { ((ushort)95, (ushort)50) }, true);
        Assert.Equal(15, AbilityStats.Player(target, 1148));
        Assert.Equal(15, AbilityStats.Player(target, 1031));
        Assert.Equal(114, AbilityStats.ScaleTypedAbilityDamage(target, 2, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(target, 1, 100));
        Assert.True(TypedAbilityResistance.Player(attacker, target, 330, 100, 2) < 100);
        AbilityPassives.SetLoaded(target, Array.Empty<(ushort, ushort)>(), true);

        Assert.True(AbilityBuffs.Apply(target, 327));
        var buff = target.playerData.activeBuffs[327];
        Assert.Equal(20, buff.Definition.Seconds);
        Assert.Equal(43, AbilityStats.Influence(target, 172));
        Assert.Equal(142, AbilityStats.ScaleTypedAbilityDamage(target, 2, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(target, 1, 100));
        TimedEffects.RunDue(buff.Started + 20.01);
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(target, 2, 100));
    }

    [Theory]
    [InlineData(323, 720, 25, 5)]
    [InlineData(423, 960, 45, 10)]
    public void MovementAcceleratorsPublishTimedSpeedAndStealthModifiers(ushort id, int seconds, int speed, int stealth)
    {
        var target = Client(50);
        Assert.True(AbilityBuffs.Apply(target, id));
        var buff = target.playerData.activeBuffs[id];
        Assert.Equal(seconds, buff.Definition.Seconds);
        Assert.Equal(speed / 100f, buff.Definition.MovementScale);
        Assert.Equal(speed, AbilityStats.Player(target, 115));
        Assert.Equal(stealth, AbilityStats.Player(target, 877));
        Assert.Equal(stealth, AbilityStats.Player(target, 878));
        TimedEffects.RunDue(buff.Started + seconds + .01);
        Assert.Equal(0, AbilityStats.Player(target, 115));
        Assert.Equal(0, AbilityStats.Player(target, 877));
        Assert.Equal(0, AbilityStats.Player(target, 878));
    }

    [Fact]
    public void UpgradeMasterAddsAndRemovesDamageAndAvailableBuffer()
    {
        var target = Client(50);
        int baseline = AbilityBuffBuffer.Maximum(target);
        AbilityPassives.SetLoaded(target, new[] { ((ushort)425, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(target, 1096));
        Assert.Equal(5, AbilityStats.Player(target, 1090));
        Assert.True(AbilityBuffs.Apply(target, 1096));
        var buff = target.playerData.activeBuffs[1096];
        Assert.Equal(300, buff.Definition.Seconds);
        Assert.Equal(baseline + 200, AbilityBuffBuffer.Maximum(target));
        foreach (int kind in new[] { 0, 1, 2, 7 })
            Assert.Equal(125, AbilityStats.ScaleTypedAbilityDamage(target, kind, 100));
        TimedEffects.RunDue(buff.Started + 300.01);
        Assert.Equal(baseline, AbilityBuffBuffer.Maximum(target));
        foreach (int kind in new[] { 0, 1, 2, 7 })
            Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(target, kind, 100));
        AbilityPassives.SetLoaded(target, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(target, 1096));
    }

    [Theory]
    [InlineData(317, new[] { 1144, 1140, 1090 }, new[] { 11, 11, 10 })]
    [InlineData(318, new[] { 1144, 1140 }, new[] { 1, 1 })]
    [InlineData(349, new[] { 1147, 1056 }, new[] { 5, 15 })]
    [InlineData(422, new[] { 1139, 1142, 1146, 1148 }, new[] { 10, 10, 5, 5 })]
    [InlineData(940, new[] { 520, 181 }, new[] { 50, 25 })]
    public void HackerPassivesInstallLevelFiftyTotalsAndRemoveThemOnUnload(ushort id, int[] stats, int[] expected)
    {
        var target = Client(50);
        AbilityPassives.SetLoaded(target, new[] { (id, (ushort)50) });
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(expected[i], AbilityStats.Player(target, (ushort)stats[i]));
        if (id == 318)
        {
            Assert.True(AbilityPassives.IsLoaded(target, 1095));
            Assert.Equal(2, AbilityStats.Toughness(target, false));
            Assert.Equal(2, AbilityStats.Toughness(target, true));
        }
        AbilityPassives.SetLoaded(target, Array.Empty<(ushort, ushort)>());
        foreach (int stat in stats)
            Assert.Equal(0, AbilityStats.Player(target, (ushort)stat));
        Assert.False(AbilityPassives.IsLoaded(target, 1095));
    }

    [Theory]
    [InlineData(321, 300, 15)]
    [InlineData(322, 600, 200)]
    [InlineData(419, 300, 500)]
    [InlineData(421, 300, 47)]
    public void ReviewedHackerBuffsReachTheirGameplayConsumers(ushort id, int seconds, int value)
    {
        var target = Client(50);
        var consumer = id switch
        {
            321 => new Func<int>(() => AbilityCosts.Effective(target, 359)),
            421 => () => AbilityStats.Defense(target, false),
            _ => () => NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(), 1)
        };
        int baseline = consumer();
        Assert.True(AbilityBuffs.Apply(target, id));
        var buff = target.playerData.activeBuffs[id];
        Assert.Equal(seconds, buff.Definition.Seconds);
        if (id == 321)
        {
            Assert.Equal(value, AbilityStats.Player(target, 117));
            Assert.Equal(baseline - baseline * value / 100, consumer());
        }
        else
        {
            Assert.Equal(baseline + value, consumer());
            if (id == 421) Assert.Equal(value, AbilityStats.Defense(target, true));
        }
        TimedEffects.RunDue(buff.Started + seconds + .01);
        Assert.Equal(baseline, consumer());
    }

    [Theory]
    [InlineData(124,20,171,10)] [InlineData(170,50,174,25)] [InlineData(417,20,106,200)]
    [InlineData(173,20,522,25)] [InlineData(173,50,522,26)] [InlineData(421,50,1009,47)]
    public void TimedTemplatesUseCharacterLevelNativePrecisionAndPositiveCaps(ushort ability,int level,ushort stat,int value)
    {
        Assert.True(AbilityScaledBuffs.TryGet(ability,level,out var buff));
        Assert.Contains(buff.Modifiers,m=>m.Stat==stat&&m.Value==value);
    }

    [Fact]
    public void RepelCombatChangesBothDefenseInputsAndPowerlessDurationWhileLoaded()
    {
        var caster = Client(50);
        int melee = AbilityStats.Defense(caster, false);
        int ballistic = AbilityStats.Defense(caster, true);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (398, 50) });

        Assert.Equal(melee + 2, AbilityStats.Defense(caster, false));
        Assert.Equal(ballistic + 2, AbilityStats.Defense(caster, true));
        Assert.Equal(6.9f, AbilityControlDurations.Scale(caster, TempMods.PowerlessAbility, 6f), 3);

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(melee, AbilityStats.Defense(caster, false));
        Assert.Equal(ballistic, AbilityStats.Defense(caster, true));
        Assert.Equal(6f, AbilityControlDurations.Scale(caster, TempMods.PowerlessAbility, 6f));
    }

    [Fact]
    public void InfectionEfficiencyChangesMeleeAttackBallisticDamageAndStunWhileLoaded()
    {
        var caster = Client(50);
        int melee = AbilityStats.Attack(caster, false);
        int ballistic = AbilityStats.Attack(caster, true);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (461, 50) });

        Assert.Equal(melee + 2, AbilityStats.Attack(caster, false));
        Assert.Equal(ballistic, AbilityStats.Attack(caster, true));
        Assert.Equal(1049, AbilityStats.ScaleTypedAbilityDamage(caster, 1, 1000));
        Assert.Equal(4.6f, AbilityControlDurations.Scale(caster, TempMods.StunAbility, 4f), 3);

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(melee, AbilityStats.Attack(caster, false));
        Assert.Equal(1000, AbilityStats.ScaleTypedAbilityDamage(caster, 1, 1000));
        Assert.Equal(4f, AbilityControlDurations.Scale(caster, TempMods.StunAbility, 4f));
    }

    [Fact]
    public void FastHealingRaisesNaturalHealthRegenAndReturnsItToBaseline()
    {
        var caster = Client(50);
        var target = Client(50);
        ushort? NextHealth(WorldClient actor) => HealthRegen.Next(100, 500, false, false, 2, AbilityStats.Influence(actor, 112));
        Assert.Equal((ushort)110, NextHealth(target));

        Assert.True(AbilityBuffs.Apply(target, 237, caster));
        Assert.Equal((ushort)116, NextHealth(target));
        Assert.Equal((ushort)110, NextHealth(caster));
        Assert.Equal(600f, target.playerData.activeBuffs[237].Definition.Seconds);

        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + 601);
        Assert.Equal((ushort)110, NextHealth(target));
    }

    [Fact]
    public void CapturedOverridesRemainAuthoritativeAndUnresolvedStylesAreExcluded()
    {
        Assert.True(AbilityScaledBuffs.Resolve(Client(20),327,out var buff));
        Assert.Equal(43,Assert.Single(buff.Modifiers).Value);
        Assert.False(AbilityScaledBuffs.TryGet(17,50,out _));
        Assert.False(AbilityScaledBuffs.TryGet(1006,50,out _));
        Assert.False(AbilityScaledBuffs.TryGet(474,50,out _));
    }

    [Fact]
    public void FriendlyBuffUsesCasterLevelAndKeepsTheAppliedValueThroughRemoval()
    {
        var caster=Client(20);var target=Client(50);
        Assert.True(AbilityBuffs.Apply(target,170,caster));
        Assert.Equal(10,AbilityBuffs.Total(target,174));
        var on=((SequencedMessage)target.messageQueue.RPCMessagesQueue[0]!).content[1..];
        caster.playerInstance.Level.setValue((byte)50);target.playerInstance.Level.setValue((byte)1);
        Assert.Equal(10,AbilityBuffs.Total(target,174));
        target.messageQueue.RPCMessagesQueue.Clear();
        Assert.True(AbilityBuffs.Remove(target,170));
        var off=((SequencedMessage)target.messageQueue.RPCMessagesQueue[0]!).content[1..];
        Assert.Equal(on[..17],off[..17]);Assert.Equal((byte)0,on[17]);Assert.Equal((byte)1,off[17]);
        Assert.Equal(0,AbilityBuffs.Total(target,174));
    }

    [Fact]
    public void ReceiverCapsTheDirectHealthBuffWithoutChangingItsEffectRecord()
    {
        var caster = Client(50);
        var target = Client(10);

        Assert.Equal(500, PvpCombat.HealthOf(target));
        Assert.True(AbilityBuffs.Apply(target, 417, caster));
        Assert.Equal(500, AbilityBuffs.Total(target, 106)); // Source magnitude stays on the modifier record.
        Assert.Equal(250, AbilityStats.Player(target, 106));
        Assert.Equal(750, NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(500, PvpCombat.HealthOf(target));

        Assert.True(AbilityBuffs.Remove(target, 417));
        Assert.Equal(500, NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(), 1));
    }

    [Fact]
    public void CharacterStatsReadTheSavedBaseWithTheExistingLegacyFallback()
    {
        var character = Client(50);
        Assert.Equal(24, AbilityStats.Player(character, CharacterAttributes.Focus));
        character.playerData.attributeBases = new Dictionary<byte, ushort>
        {
            [CharacterAttributes.Focus] = 11,
            [CharacterAttributes.Belief] = 5
        };
        Assert.Equal(11, AbilityStats.Player(character, CharacterAttributes.Focus));
        Assert.Equal(5, AbilityStats.Player(character, CharacterAttributes.Belief));
        Assert.Equal(26, AbilityStats.Player(character, CharacterAttributes.Vitality));
        Assert.Equal(0, AbilityStats.Player(character, 575));
    }

    [Fact]
    public void CapturedStatusBuffsFeedDefenseAccuracyAndDeterminationConsumers()
    {
        var target = Client(50);
        AbilityPassives.SetLoaded(target, new (ushort, ushort)[] { (1102, 50) });
        Assert.Equal(0, AbilityStats.Defense(target, false));
        Assert.Equal(0, AbilityStats.ViralAccuracy(target));
        Assert.Equal(30f, AbilityControlDurations.Mitigate(30f, 20, AbilityControlDurations.Determination(target)));

        Assert.True(AbilityBuffs.Apply(target, 1099, target));
        Assert.True(AbilityBuffs.Apply(target, 314, target));
        Assert.True(AbilityBuffs.Apply(target, 1102, target));
        Assert.Equal(52, AbilityStats.Defense(target, false));
        Assert.Equal(108, AbilityStats.ViralAccuracy(target));
        Assert.Equal(10.2f, AbilityControlDurations.Mitigate(30f, 20, AbilityControlDurations.Determination(target)), 3);

        Assert.True(AbilityBuffs.Remove(target, 1099));
        Assert.True(AbilityBuffs.Remove(target, 314));
        Assert.True(AbilityBuffs.Remove(target, 1102));
        Assert.Equal(0, AbilityStats.Defense(target, false));
        Assert.Equal(0, AbilityStats.ViralAccuracy(target));
        Assert.Equal(30f, AbilityControlDurations.Mitigate(30f, 20, AbilityControlDurations.Determination(target)));
    }

    [Theory]
    [InlineData(373, false)]
    [InlineData(374, true)]
    public void HackerAccuracyBuffUsesCasterLevelAndConsumedAttackStats(ushort id, bool ranged)
    {
        var caster = Client(20);
        var recipient = Client(50);
        int baseline = AbilityStats.Attack(recipient, ranged);
        Assert.Same(caster, Assert.Single(AbilitySupport.Targets(caster, id, 999)));
        int casterAttack = AbilityStats.Attack(caster, ranged);
        AbilitySupport.Land(caster, id, 999);
        Assert.Equal(casterAttack + 25, AbilityStats.Attack(caster, ranged));
        Assert.Equal(baseline, AbilityStats.Attack(recipient, ranged));
        Assert.True(AbilityBuffs.Remove(caster, id));
        Assert.Equal(casterAttack, AbilityStats.Attack(caster, ranged));

        caster.playerInstance.Level.setValue((byte)50);
        AbilitySupport.Land(caster, id, 0);
        Assert.Equal(casterAttack + 64, AbilityStats.Attack(caster, ranged));
        Assert.True(AbilityBuffs.Remove(caster, id));
        Assert.Equal(casterAttack, AbilityStats.Attack(caster, ranged));
    }

    [Theory]
    [InlineData(310, 1083, 1)]
    [InlineData(334, 1084, 5)]
    [InlineData(335, 1086, 0)]
    [InlineData(369, 1092, 0)]
    public void HackerParentsRegisterGrantsAndRemoveOnlyTheirContribution(ushort parent, ushort child, int viralBonus)
    {
        var caster = Client(50);
        caster.playerData.setDistrictId(1);
        caster.playerData.setOnWorld(true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(caster, new WorldSendFence());
        AbilityGrantRegistration.LoadingComplete(caster, new byte[] { 1 });
        byte[][] Packets() => caster.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(r => r.content[1..]).ToArray();
        int baseline = AbilityStats.ScaleTypedAbilityDamage(caster, 2, 1000);

        AbilityPassives.SetLoaded(caster, new[] { (parent, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(caster, child));
        byte[] add = AbilityGrantRegistration.Record(new(parent, child, 0), false);
        Assert.Single(Packets(), rpc => rpc.SequenceEqual(add));
        Assert.Equal(baseline + viralBonus * 10 - (viralBonus == 0 ? 0 : 1), AbilityStats.ScaleTypedAbilityDamage(caster, 2, 1000));
        AbilityPassives.SetLoaded(caster, new[] { (parent, (ushort)50) });
        Assert.Single(Packets(), rpc => rpc.SequenceEqual(add));

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(caster, child));
        byte[] remove = AbilityGrantRegistration.Record(new(parent, child, 0), true);
        Assert.Single(Packets(), rpc => rpc.SequenceEqual(remove));
        Assert.Equal(baseline, AbilityStats.ScaleTypedAbilityDamage(caster, 2, 1000));
    }

    [Theory]
    [InlineData(1083, 4, 6, 0)]
    [InlineData(1084, 10, 9, 0)]
    [InlineData(1086, 2, 6, 10)]
    public void HackerClassUpgradesChangeTheActualConsumersAndRemoveTheirEffects(
        ushort ability, int damage, int accuracy, int radius)
    {
        var caster = Client(50);
        AbilitySupport.Land(caster, ability, 999);
        Assert.Equal(damage, AbilityStats.Influence(caster, 172));
        Assert.Equal(accuracy, AbilityStats.ViralAccuracy(caster));
        Assert.Equal(78, AbilityCastInterrupts.CastThroughPercent(10, 500,
            (short)AbilityStats.Influence(caster, AbilityCastInterrupts.Concentration)));
        Assert.Equal(4 * (1 + (double)(float)(radius / 100.0)), AbilityRadius.TargetMetres(caster, 333));
        Assert.True(AbilityBuffs.Remove(caster, ability));
        Assert.Equal(0, AbilityStats.Influence(caster, 172));
        Assert.Equal(0, AbilityStats.ViralAccuracy(caster));
        Assert.Equal(4, AbilityRadius.TargetMetres(caster, 333));
    }

    [Theory]
    [InlineData(309, 3, 10)]
    [InlineData(346, 5, 0)]
    public void HackerLoadedPassivesReachViralDamageAndCastThroughConsumers(
        ushort ability, int bonusDamage, short concentration)
    {
        var caster = Client(50);
        AbilityPassives.SetLoaded(caster, new[] { (ability, (ushort)50) });
        Assert.Equal((int)(1000 * (double)(float)(1 + bonusDamage / 100.0)), AbilityStats.ScaleTypedAbilityDamage(caster, 2, 1000));
        Assert.Equal(concentration, AbilityStats.Influence(caster, AbilityCastInterrupts.Concentration));
        Assert.Equal(75 + concentration / 2,
            AbilityCastInterrupts.CastThroughPercent(10, 500, (short)AbilityStats.Influence(caster, AbilityCastInterrupts.Concentration)));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(1000, AbilityStats.ScaleTypedAbilityDamage(caster, 2, 1000));
        Assert.Equal(0, AbilityStats.Influence(caster, AbilityCastInterrupts.Concentration));
    }

    [Fact]
    public void CapturedCombatHackingChangesTwoDefenseConsumersAndThePaidCostUntilRemoval()
    {
        var caster = Client(50);
        AbilitySupport.Land(caster, 344, 999);
        Assert.Equal(22, AbilityStats.Defense(caster, false));
        Assert.Equal(22, AbilityStats.Defense(caster, true));
        Assert.Equal((ushort)16, AbilityCosts.Effective(caster, 315));
        Assert.True(AbilityBuffs.Remove(caster, 344));
        Assert.Equal(0, AbilityStats.Defense(caster, false));
        Assert.Equal(0, AbilityStats.Defense(caster, true));
        Assert.Equal((ushort)20, AbilityCosts.Effective(caster, 315));
    }

    [Theory]
    [InlineData(469, 1088)]
    [InlineData(236, 1097)]
    public void HackerParentsPublishTheGrantedActionOnlyWhileLoaded(ushort parent, ushort child)
    {
        var caster = Client(50);
        caster.playerData.setDistrictId(1);
        caster.playerData.setOnWorld(true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(caster, new WorldSendFence());
        AbilityGrantRegistration.LoadingComplete(caster, new byte[] { 1 });
        AbilityPassives.SetLoaded(caster, new[] { (parent, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(caster, child));
        byte[] add = AbilityGrantRegistration.Record(new(parent, child, 0), false);
        byte[][] Packets() => caster.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(message => message.content[1..]).ToArray();
        Assert.Single(Packets(), record => record.SequenceEqual(add));
        if (parent == 469)
            Assert.Equal(10.5f, AbilityControlDurations.Scale(caster, TempMods.StunAbility, 10), 3);
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(caster, child));
        byte[] remove = AbilityGrantRegistration.Record(new(parent, child, 0), true);
        Assert.Single(Packets(), record => record.SequenceEqual(remove));
        Assert.Equal(10, AbilityControlDurations.Scale(caster, TempMods.StunAbility, 10));
    }

    [Fact]
    public void CorpsmanUpgradeFeedsCapturedConcentrationHealingAndRadiusThenRestoresTheBaseline()
    {
        var caster = Client(50);
        AbilitySupport.Land(caster, 1092, 90);
        Assert.Equal(7, AbilityStats.Influence(caster, AbilityCastInterrupts.Concentration));
        Assert.Equal(6, AbilityStats.Influence(caster, 174));
        Assert.Equal(27, AbilityHealing.Amount(caster, caster, 381));
        Assert.Equal(6 * (1 + (double)0.1f), AbilityRadius.Metres(caster, 381));
        Assert.True(AbilityBuffs.Remove(caster, 1092));
        Assert.Equal(26, AbilityHealing.Amount(caster, caster, 381));
        Assert.Equal(6, AbilityRadius.Metres(caster, 381));
    }

    [Fact]
    public void DirectCostBuffAndDebuffAffectOrdinaryAbilitySpending()
    {
        var target = Client(50);
        var source = Client(50);
        const ushort probe = 314;

        Assert.Equal((ushort)35, AbilityCosts.Effective(target, probe));
        Assert.True(AbilityBuffs.Apply(target, 321, source));
        Assert.Equal((ushort)30, AbilityCosts.Effective(target, probe));
        Assert.True(AbilityBuffs.Remove(target, 321));
        Assert.True(TempMods.TryGet(401, out var hostile));
        var effects = TimedEffects.ForPlayer(target);
        TimedEffects.Apply(effects, hostile, TimeUtils.GetSimTimeSeconds(), source, "Source");
        Assert.Equal((ushort)42, AbilityCosts.Effective(target, probe));
        Assert.True(TimedEffects.RemoveParent(effects, 401));
        Assert.Equal((ushort)35, AbilityCosts.Effective(target, probe));
    }

    [Fact]
    public void ReplacingHealthBuffUsesTheNewCasterWithoutCompoundingAndRestoresTheBaseline()
    {
        var caster=Client(20);var target=Client(50);
        Assert.True(AbilityBuffs.Apply(target,417,caster));
        Assert.Equal(700,NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(),1));
        Assert.Equal(500,PvpCombat.HealthOf(target));
        caster.playerInstance.Level.setValue((byte)50);
        Assert.True(AbilityBuffs.Apply(target,417,caster));
        Assert.Equal(1000,NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(),1));
        Assert.Equal(500,AbilityBuffs.VitalTotal(target,106));
        caster.playerInstance.Level.setValue((byte)1);
        target.playerInstance.Health.setValue((ushort)900);
        AbilityBuffs.Remove(target,417);
        Assert.Equal(500,NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(),1));
        Assert.Equal(500,PvpCombat.HealthOf(target));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FriendlyHealthSnapshotAndHostileMaximumReductionSurviveEitherRemovalOrder(bool removeFriendlyFirst)
    {
        var caster=Client(20);var target=Client(50);
        target.playerInstance.MaxHealth.setValue((ushort)1000);
        target.playerInstance.Health.setValue((ushort)1000);
        Assert.True(AbilityBuffs.Apply(target,417,caster));
        Assert.True(TempMods.TryGet(476,out var hostile));
        TimedEffects.Apply(TimedEffects.ForPlayer(target),hostile,TimeUtils.GetSimTimeSeconds(),caster,"Caster");
        Assert.Equal(680,NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(),1));
        caster.playerInstance.Level.setValue((byte)50);
        if (removeFriendlyFirst)
        {
            Assert.True(AbilityBuffs.Remove(target,417));
            Assert.Equal(480,NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(),1));
            Assert.True(TimedEffects.RemoveParent(TimedEffects.ForPlayer(target),476));
        }
        else
        {
            Assert.True(TimedEffects.RemoveParent(TimedEffects.ForPlayer(target),476));
            Assert.Equal(1200,NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(),1));
            Assert.True(AbilityBuffs.Remove(target,417));
        }
        Assert.Equal(1000,NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(),1));
        Assert.Equal(0,AbilityBuffs.VitalTotal(target,106));
    }


    [Fact]
    public void ImprovedHackingChangesTheRecipientsViralDamageAndHealingUntilExpiry()
    {
        var caster = Client(20);
        var recipient = Client(50);
        var patient = Client(50);
        ushort Heal() => AbilityHealing.Amount(recipient, patient, 413);
        ushort Damage() => AbilityStats.ScaleAbilityDamage(recipient, 330, 100);
        Assert.Equal(70, Heal());
        Assert.Equal(100, Damage());
        double start = TimeUtils.GetSimTimeSeconds();
        Assert.True(AbilityBuffs.Apply(recipient, 489, caster));
        Assert.Equal(77, Heal());
        Assert.Equal(110, Damage());
        Assert.Equal(100, AbilityStats.ScaleAbilityDamage(caster, 330, 100));
        TimedEffects.RunDue(start + 299);
        Assert.Equal(77, Heal());
        TimedEffects.RunDue(start + 301);
        Assert.Equal(70, Heal());
        Assert.Equal(100, Damage());
    }

    [Fact]
    public void ForceEnhancerChangesBallisticCombatAndRestoresTheOriginalScores()
    {
        var player = Client(50);
        int attack = AbilityStats.Attack(player, true);
        int defense = AbilityStats.Defense(player, true);
        int meleeAttack = AbilityStats.Attack(player, false);
        double start = TimeUtils.GetSimTimeSeconds();
        Assert.True(AbilityBuffs.Apply(player, 383));
        Assert.Equal(attack + 46, AbilityStats.Attack(player, true));
        Assert.Equal(defense + 21, AbilityStats.Defense(player, true));
        Assert.Equal(meleeAttack, AbilityStats.Attack(player, false));
        Assert.Equal(105, AbilityStats.ScaleTypedAbilityDamage(player, 1, 100));
        AbilityCooldowns.Start(player, 383, start);
        Assert.True(AbilityCooldowns.IsCoolingDown(player.playerData, 383, start + 299));
        Assert.False(AbilityCooldowns.IsCoolingDown(player.playerData, 383, start + 301));
        TimedEffects.RunDue(start + 31);
        Assert.Equal(attack, AbilityStats.Attack(player, true));
        Assert.Equal(defense, AbilityStats.Defense(player, true));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(player, 1, 100));
    }

    [Theory]
    [InlineData(692, 254, 279, 19, 9, 80)]
    [InlineData(693, 330, 198, 19, 9, 75)]
    public void LinkedDeflectBuffsChangeTheMatchingDefenseContestsThenExpire(
        ushort child, ushort primaryAttack, ushort secondaryAttack, int primaryBonus, int secondaryBonus, int castThrough)
    {
        var caster = Client(20);
        var recipient = Client(50);
        int Defense(ushort attack)
        {
            var move = new AbilityItem();
            move.setAbilityID(attack);
            return AbilityStats.SpecialDefense(recipient, move);
        }
        int CastThrough() => AbilityCastInterrupts.CastThroughPercent(1, 1000,
            (short)AbilityStats.Influence(recipient, AbilityCastInterrupts.Concentration));

        int primary = Defense(primaryAttack);
        int secondary = Defense(secondaryAttack);
        double start = TimeUtils.GetSimTimeSeconds();
        Assert.True(AbilityBuffs.Apply(recipient, child, caster));
        Assert.Equal(primary + primaryBonus, Defense(primaryAttack));
        Assert.Equal(secondary + secondaryBonus, Defense(secondaryAttack));
        Assert.Equal(castThrough, CastThrough());
        TimedEffects.RunDue(start + 5.01);
        Assert.Equal(primary, Defense(primaryAttack));
        Assert.Equal(secondary, Defense(secondaryAttack));
        Assert.Equal(75, CastThrough());
    }

    [Fact]
    public void GroupNumericBuffsRequireBeneficialMetadataAndStillRejectCallbacks()
    {
        Assert.True(AbilityScaledBuffs.TryGet(489,20,out var buff));
        Assert.Equal(300,buff.Seconds);
        Assert.Contains(buff.Modifiers,m=>m.Stat==172 && m.Value==10);
        Assert.Contains(buff.Modifiers,m=>m.Stat==174 && m.Value==10);
        Assert.False(AbilityScaledBuffs.TryGet(63,20,out _)); // RefundIS callback
        Assert.False(AbilityScaledBuffs.TryGet(436,20,out _));
        Assert.False(AbilityScaledBuffs.TryGet(361,20,out _)); // hostile field
        Assert.True(AbilityAuras.Handles(42)); Assert.True(AbilityAuras.Handles(485));
        Assert.Equal(695,AbilityAuras.EffectOf(42)); Assert.Equal(12,AbilityAuras.DrainOf(42));
        Assert.Equal(696,AbilityAuras.EffectOf(485)); Assert.Equal(15,AbilityAuras.DrainOf(485));
        Assert.True(AbilityScaledBuffs.TryScriptedTimed(63,20,out _));
        Assert.True(AbilityAuras.Handles(378)); // explicit child term sweep executor
    }

    [Fact]
    public void TacticalTogglesReachCombatAndRejectTheirScriptConflict()
    {
        var c=Client(20);
        ushort RoundDamage() => CombatCalculator.CloseCombatDamage(
            new Random(731), 20, 20, CombatTactic.Speed, out _, CombatTactics.Type(c), bonus: 100);
        ushort normal = RoundDamage();
        Assert.True(AbilityBuffs.Apply(c,15));
        Assert.Equal(CombatTacticType.Precise,CombatTactics.Type(c));
        Assert.True(RoundDamage() < normal);
        Assert.False(AbilityBuffs.Apply(c,9));
        Assert.True(AbilityBuffs.Remove(c,15));
        Assert.Equal(CombatTacticType.Normal,CombatTactics.Type(c));
        Assert.True(AbilityBuffs.Apply(c,9));
        Assert.Equal(CombatTacticType.Energized,CombatTactics.Type(c));
        Assert.True(RoundDamage() > normal);
        Assert.False(AbilityBuffs.Apply(c,15));
        Assert.True(AbilityBuffs.Remove(c,9));
        Assert.Equal(normal, RoundDamage());
    }

    [Fact]
    public void HyperSenseResolvesLearnedLevelAndRetainsItsRemovalSnapshot()
    {
        var c=Client(50);
        AbilityPassives.SetLoaded(c,new[]{((ushort)1101,(ushort)20)},true);
        Assert.True(AbilityBuffs.Apply(c,1101));
        Assert.Equal(19,AbilityBuffs.Total(c,1011));
        AbilityPassives.SetLoaded(c,new[]{((ushort)1101,(ushort)50)},true);
        Assert.Equal(19,AbilityBuffs.Total(c,1011));
        Assert.True(AbilityBuffs.Remove(c,1101));
        Assert.Equal(0,AbilityBuffs.Total(c,1011));
    }

    [Fact]
    public void GaussianBlurAddsAndRemovesOnlyItsOwnPresentationBit()
    {
        var c=Client(20);
        Assert.True(AbilityBuffs.Apply(c,184));
        Assert.True(AbilityBuffs.Apply(c,49));
        Assert.Equal(0x440,NumericalUtils.ByteArrayToUint16(c.playerInstance.DissemblingType.getValue(),1));
        Assert.Equal(25,AbilityBuffs.Total(c,520));
        Assert.True(AbilityBuffs.Remove(c,49));
        Assert.Equal(0x400,NumericalUtils.ByteArrayToUint16(c.playerInstance.DissemblingType.getValue(),1));
        AbilityBuffs.Remove(c,184);
    }

    [Fact]
    public void FindWeaknessInfluencesThrownHitsWithoutBuffingBallisticOrViralDamage()
    {
        var c=Client(20);
        Assert.Equal(100,AbilityStats.ScaleAbilityDamage(c,279,100));
        Assert.True(AbilityBuffs.Apply(c,123));
        Assert.Equal(110,AbilityStats.ScaleAbilityDamage(c,279,100));
        Assert.Equal(100,AbilityStats.ScaleAbilityDamage(c,254,100));
        Assert.Equal(100,AbilityStats.ScaleAbilityDamage(c,350,100));
        AbilityBuffs.Remove(c,123);
        Assert.Equal(100,AbilityStats.ScaleAbilityDamage(c,279,100));
    }

    [Fact]
    public void DisguiseUsesOwnedToolModelsAndRestoresItsOriginalAppearance()
    {
        Assert.True(AbilityDisguise.TryModel(615,out uint model));
        Assert.Equal(0x40000533u,model);
        Assert.Equal(0u,AbilityDisguise.ModelFor(new[]{new Crafting.InventoryItem(1,615)},2));
        Assert.Equal(model,AbilityDisguise.ModelFor(new[]{new Crafting.InventoryItem(1,615)},1));
        Assert.Equal(0u,AbilityDisguise.ModelFor(new[]{new Crafting.InventoryItem(97,615)}));
        var c=Client(50);
        c.playerInstance.Description.setValue(123u);
        c.playerInstance.UseRSIDescription.setValue((byte)1);
        c.playerInstance.TitleAbility.setValue(42u);
        Assert.True(AbilityBuffs.Apply(c,49));
        var state=Stealth.Begin(c,Stealth.Defs[1114],20,model);
        Assert.Equal(41,state.Seconds);
        Assert.Equal(model,BitConverter.ToUInt32(c.playerInstance.Description.getValue()));
        Assert.Equal(0,c.playerInstance.UseRSIDescription.getValue()[0]);
        Assert.Equal(0u,BitConverter.ToUInt32(c.playerInstance.TitleAbility.getValue()));
        Assert.False(Stealth.IsHidden(c)); // disguise is a visible model, not invisibility
        Assert.Equal(0x48,NumericalUtils.ByteArrayToUint16(c.playerInstance.DissemblingType.getValue(),1));
        Assert.True(Stealth.End(c,"test",false));
        Assert.Equal(123u,BitConverter.ToUInt32(c.playerInstance.Description.getValue()));
        Assert.Equal(1,c.playerInstance.UseRSIDescription.getValue()[0]);
        Assert.Equal(42u,BitConverter.ToUInt32(c.playerInstance.TitleAbility.getValue()));
        Assert.Equal(0x40,NumericalUtils.ByteArrayToUint16(c.playerInstance.DissemblingType.getValue(),1));
        AbilityBuffs.Remove(c,49);
    }

    [Fact]
    public void OverflowReservesFreeCapacityAndKeepsExistingFamilyReplacements()
    {
        var c=Client(20);
        Assert.Equal(180,AbilityBuffBuffer.Maximum(c)); // chosen baseline, not recovered native getter
        Assert.True(AbilityBuffs.Apply(c,327));
        int used=AbilityBuffBuffer.Used(c);
        Assert.True(TempMods.TryGet(481,out var overflow));
        TimedEffects.Apply(TimedEffects.ForPlayer(c),overflow,TimeUtils.GetSimTimeSeconds(),c,"Source");
        Assert.Equal(Math.Min(150,180-used),AbilityBuffBuffer.Reserved(c));
        Assert.False(AbilityBuffs.Apply(c,489));
        Assert.True(AbilityBuffs.Apply(c,327)); // replacement keeps already-held buffer credit
        Assert.True(AbilityBuffs.Remove(c,327));
        Assert.Equal(150,AbilityBuffBuffer.Reserved(c));
        Assert.False(AbilityBuffs.Apply(c,489));
        Assert.True(TimedEffects.RemoveParent(TimedEffects.ForPlayer(c),481));
        Assert.Equal(0,AbilityBuffBuffer.Reserved(c));
        Assert.True(AbilityBuffs.Apply(c,489));
        AbilityBuffs.Remove(c,489);
    }

    [Theory]
    [InlineData(395,80)] [InlineData(481,150)] [InlineData(488,150)]
    public void AllBufferOverflowParentsReleaseTheirReservationOnExpiry(ushort id,int reserve)
    {
        var c=Client(50);
        Assert.True(TempMods.TryGet(id,out var overflow));
        var active=TimedEffects.Apply(TimedEffects.ForPlayer(c),overflow,TimeUtils.GetSimTimeSeconds(),c,"Source");
        Assert.Equal(reserve,AbilityBuffBuffer.Reserved(c));
        TimedEffects.RunDue(active.Max(a=>a.Expires)+0.01);
        Assert.Equal(0,AbilityBuffBuffer.Reserved(c));
    }

    [Theory]
    [InlineData(13,159,40,25,5)] [InlineData(16,212,15,1,1)] [InlineData(442,524,20,30,5)]
    public void SourcedTogglesDrainAndDeactivateOnShortfall(ushort id,ushort stat,int expected,int drain,int interval)
    {
        var c=Client(50);
        using var owner=new BuffDrainTestOwner(c);
        AbilityPassives.SetLoaded(c,new[]{(id,(ushort)20)},true);
        Assert.True(AbilityBuffs.Apply(c,id));
        Assert.Equal(expected,AbilityBuffs.Total(c,stat));
        var active=c.playerData.activeBuffs[id];
        Assert.Equal(drain,active.Definition.DrainAmount); Assert.Equal(interval,active.Definition.DrainSeconds);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)drain);
        AbilityBuffs.TickDrain(c,id,active.Generation);
        Assert.Equal(0,AbilityHandler.CurrentInnerStrength(c));
        Assert.True(AbilityBuffs.IsActive(c,id)); // equality pays; the next tick deactivates
        AbilityBuffs.TickDrain(c,id,active.Generation);
        Assert.False(AbilityBuffs.IsActive(c,id));
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        AbilityBuffs.TickDrain(c,id,active.Generation); // stale timer cannot spend after removal
        Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));
    }

    [Theory]
    [InlineData(63,10)] [InlineData(436,25)]
    public void FirewallAndShieldRefundRecipientInnerStrengthOnTerm(ushort id,int refund)
    {
        var c=Client(20); var source=Client(50);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        Assert.True(AbilityBuffs.Apply(c,id,source));
        var applied=c.playerData.activeBuffs[id].Definition;
        Assert.Equal(300,applied.Seconds);
        Assert.Equal(id==63?35:47,AbilityBuffs.Total(c,183));
        source.playerInstance.Level.setValue((byte)1);
        Assert.True(AbilityBuffs.Remove(c,id));
        Assert.Equal(100+refund,AbilityHandler.CurrentInnerStrength(c));
        Assert.Equal(0,AbilityBuffs.Total(c,183));
        Assert.False(AbilityBuffs.Remove(c,id));
        Assert.Equal(100+refund,AbilityHandler.CurrentInnerStrength(c));
    }

    [Theory]
    [InlineData(68,75)] [InlineData(319,250)]
    public void PersonalFirewallTermHealsAndPreservesOtherPresentationBits(ushort id,int heal)
    {
        var c=Client(50); c.playerInstance.Health.setValue((ushort)100);
        Assert.True(AbilityBuffs.Apply(c,49)); Assert.True(AbilityBuffs.Apply(c,id));
        Assert.Equal(0x140,NumericalUtils.ByteArrayToUint16(c.playerInstance.DissemblingType.getValue(),1));
        Assert.Equal(id == 319 ? 80 : 40, AbilityStats.Player(c, 157));
        foreach (ushort resistance in new ushort[] { 180, 181, 1033 })
            Assert.Equal(id == 319 ? 20 : 10, AbilityStats.Player(c, resistance));
        Assert.True(AbilityBuffs.Remove(c,id));
        Assert.Equal(100+heal,PvpCombat.HealthOf(c));
        Assert.Equal(0x40,NumericalUtils.ByteArrayToUint16(c.playerInstance.DissemblingType.getValue(),1));
        Assert.True(AbilityBuffs.Apply(c,id)); c.playerData.isDead=true;
        c.playerInstance.Health.setValue((ushort)0); AbilityBuffs.RemoveAll(c);
        Assert.Equal(0,PvpCombat.HealthOf(c)); // explicit death-order guard
    }

    [Fact]
    public void BufferMultipleReservationsStayBoundedAndDeathClearsThem()
    {
        var c=Client(20);
        foreach(ushort id in new ushort[]{395,481,488})
        {
            Assert.True(TempMods.TryGet(id,out var effect));
            TimedEffects.Apply(TimedEffects.ForPlayer(c),effect,TimeUtils.GetSimTimeSeconds(),c,"Source");
        }
        Assert.Equal(180,AbilityBuffBuffer.Reserved(c));
        Assert.False(AbilityBuffs.Apply(c,15));
        TimedEffects.RemoveParent(TimedEffects.ForPlayer(c),395);
        Assert.Equal(180,AbilityBuffBuffer.Reserved(c));
        c.playerData.isDead=true; AbilityBuffs.RemoveAll(c); TimedEffects.Clear(TimedEffects.ForPlayer(c));
        Assert.Equal(0,AbilityBuffBuffer.Reserved(c)); Assert.Equal(0,AbilityBuffBuffer.Used(c));
    }

    [Fact]
    public void DeterminationUsesLearnedLevelWithoutInventingAToggleFlag()
    {
        var c=Client(50); AbilityPassives.SetLoaded(c,new[]{((ushort)1102,(ushort)20)},true);
        Assert.True(AbilityBuffs.Apply(c,1102)); Assert.Equal(60,AbilityStats.Player(c,575));
        Assert.False(AbilityBuffs.IsToggledOn(c,1102)); // shipped template has no IsAToggleAbility
        Assert.True(AbilityBuffs.CooldownStartsOnUse(1102));
        AbilityBuffs.Remove(c,1102);
    }

    [Fact]
    public void PurityRunsItsInstantTwentyFivePercentSweepWithoutInventingATimer()
    {
        var c=Client(50);
        Assert.True(TempMods.TryGet(481,out var overflow));
        TimedEffects.Apply(TimedEffects.ForPlayer(c),overflow,TimeUtils.GetSimTimeSeconds(),c,"Source");
        var previous=AbilityHostile.PulseRandom;
        try
        {
            AbilityHostile.PulseRandom=new Random(1); // first Next(100)=24, inside the stored25% threshold
            Assert.True(AbilityHostile.ApplySweep(c,c,868));
            Assert.Equal(0,AbilityBuffBuffer.Reserved(c));
            Assert.False(TimedEffects.ForPlayer(c).Effects.HasParent(868));
        }
        finally { AbilityHostile.PulseRandom=previous; }
    }

    [Fact]
    public void FullBufferAlsoRefusesPersistentHotsButNotInstantHealingOrPurity()
    {
        var c=Client(20);
        foreach(ushort id in new ushort[]{395,481})
        {
            Assert.True(TempMods.TryGet(id,out var effect));
            TimedEffects.Apply(TimedEffects.ForPlayer(c),effect,TimeUtils.GetSimTimeSeconds(),c,"Source");
        }
        Assert.True(AbilitySupport.RequiresBuffer(169));
        Assert.False(AbilitySupport.BufferFits(c,169));
        Assert.True(AbilitySupport.BufferFits(c,54));
        Assert.True(AbilitySupport.BufferFits(c,868));
        TimedEffects.Clear(TimedEffects.ForPlayer(c));
        Assert.True(AbilitySupport.BufferFits(c,169));
    }
}
