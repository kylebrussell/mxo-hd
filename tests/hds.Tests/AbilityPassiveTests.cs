using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityPassiveTests
{
    private static WorldClient Client()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.Health.setValue((ushort)100);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)500);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }
    private static ushort Value(hds.Attribute attribute) => NumericalUtils.ByteArrayToUint16(attribute.getValue(), 1);
    private static AbilityCatalog.Modifier Modifier(ushort id, ushort stat) => AbilityPassives.Modifiers(id).Single(m => m.Target == stat);

    [Fact]
    public void DirectDebuffRadiusUsesCaster177WhileUnproven75LinkStaysExcluded()
    {
        var caster=Client();var target=Client();
        Assert.Equal(4,AbilityRadius.TargetMetres(caster,333));
        Assert.True(AbilityBuffs.Apply(caster,345));
        Assert.Equal(21,AbilityStats.Player(caster,177));
        Assert.Equal(4,AbilityRadius.Metres(caster,333)); // existing friendly callers do not opt in
        Assert.Equal(4*(1+(double)(float).21),AbilityRadius.TargetMetres(caster,333));
        Assert.Equal(AbilityRadius.TargetMetres(caster,333),AbilityRadius.TargetMetres(caster,4429));
        Assert.Equal(4,AbilityRadius.TargetMetres(target,333));
        Assert.True(AbilityTargets.WithinArea(1000,495,0,1420,495,0,AbilityRadius.TargetMetres(caster,333)));
        Assert.False(AbilityTargets.WithinArea(1000,495,0,1500,495,0,AbilityRadius.TargetMetres(caster,333)));
        Assert.False(AbilityTargets.WithinArea(1000,495,0,1000,645,0,AbilityRadius.TargetMetres(caster,333)));
        Assert.Equal(14,AbilityRadius.Metres(caster,72)); // UBF0 remains separate
        Assert.True(AbilityBuffs.Remove(caster,345));Assert.Equal(4,AbilityRadius.TargetMetres(caster,333));
        AbilityPassives.SetLoaded(caster,new (ushort,ushort)[]{(75,50)});
        Assert.Equal(25,AbilityStats.Player(caster,1055));Assert.Equal(0,AbilityStats.Player(caster,177));
        Assert.Equal(4,AbilityRadius.TargetMetres(caster,333)); // do not invent linked stat aggregation
        // Effects.Total uses the real clock, so use a source-current timestamp for an active signed modifier.
        TimedEffects.ForPlayer(caster).Effects.Apply(123,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=177,Value=-25,Seconds=10}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(3,AbilityRadius.TargetMetres(caster,333));
        TimedEffects.ForPlayer(caster).Effects.Apply(123,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=177,Value=150,Seconds=10}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(100,AbilityStats.Player(caster,177));Assert.Equal(8,AbilityRadius.TargetMetres(caster,333));
    }

    [Fact]
    public void ResistDamageRadiusUsesLoadedCasterPercentAndExactFriendlyStatusCategory()
    {
        var caster=Client();
        Assert.Equal(14,AbilityRadius.Metres(caster,72));
        double hostile=AbilityRadius.Metres(caster,942);
        AbilityPassives.SetLoaded(caster,new (ushort,ushort)[]{(74,50)});
        Assert.Equal(24,AbilityStats.Player(caster,127));
        Assert.Equal(17.36,AbilityRadius.Metres(caster,72),6);
        Assert.Equal(AbilityRadius.Metres(caster,72),AbilityRadius.Metres(caster,4168));
        Assert.False(AbilityTargets.WithinArea(0,495,0,1600,495,0,14));
        Assert.True(AbilityTargets.WithinArea(0,495,0,1600,495,0,AbilityRadius.Metres(caster,72)));
        Assert.False(AbilityTargets.WithinArea(0,495,0,1800,495,0,AbilityRadius.Metres(caster,72)));
        Assert.Equal(hostile,AbilityRadius.Metres(caster,942));
        Assert.Equal(AbilityCatalog.Find(944).Number("Ability/RadiusOfEffect")*1.24,AbilityRadius.Metres(caster,944),6);
        AbilityPassives.SetLoaded(caster,Array.Empty<(ushort,ushort)>());
        Assert.Equal(14,AbilityRadius.Metres(caster,72));
    }

    [Fact]
    public void NativeLevelFunctionsTruncateScaleThenApplyTheCap()
    {
        Assert.Equal((ushort)0x45, AbilityPassives.Kind);
        Assert.True(AbilityValues.TryValue(Modifier(2, 204), 17, 50, out int awareness));
        Assert.Equal(4, awareness); // 17 * .25; native truncation, not rounding
        Assert.True(AbilityValues.TryValue(Modifier(37, 649), 50, 50, out int writing));
        Assert.Equal(15, writing); // ValueFunction 2's positive MaxValue cap
        Assert.True(AbilityValues.TryValue(Modifier(269, 1057), 1, 50, out int health));
        Assert.Equal(15, health); // ValueFunction 3 uses character level instead
        Assert.True(AbilityValues.TryValue(Modifier(76, 1030), 50, 50, out int defense));
        Assert.Equal(11, defense); // x87 retains the binary32 .24 scale's error until truncation
    }

    [Fact]
    public void LoadedPassivesReachStatsAndDoNotStackAliasesOrDuplicateRows()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (52, 50), (4148, 20), (37, 50), (106, 50) });
        Assert.Equal(200, AbilityStats.Player(client, 643));
        Assert.Equal(15, AbilityStats.Player(client, 649));
        Assert.Equal(0, AbilityStats.Player(client, 106)); // a class-2 stat cannot be loaded as a discipline
        AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Player(client, 643));
        Assert.Equal(0, AbilityStats.Player(client, 649));
    }

    [Fact]
    public void PassivesComposeWithClassBuffsAndUnloadingRestoresTheBaseline()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (269, 50), (45, 50) });
        Assert.Equal(575, Value(client.playerInstance.MaxHealth));
        Assert.Equal(550, Value(client.playerInstance.InnerStrengthMax));
        Assert.Equal(500, AbilityBuffs.UnbuffedMaximum(client, 106));
        Assert.Equal(2, AbilityStats.Attack(client, false)); // 5% of the existing level-50 combat base
        AbilityBuffs.Apply(client, 1064);
        Assert.Equal(712, Value(client.playerInstance.MaxHealth));
        AbilityPassives.Refresh(client);
        Assert.Equal(712, Value(client.playerInstance.MaxHealth)); // no compounding on refresh
        client.playerInstance.Health.setValue((ushort)700);
        AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>());
        Assert.Equal(637, Value(client.playerInstance.MaxHealth));
        Assert.Equal(637, Value(client.playerInstance.Health));
        AbilityBuffs.Remove(client, 1064);
        Assert.Equal(500, Value(client.playerInstance.MaxHealth));
        Assert.Equal(500, Value(client.playerInstance.InnerStrengthMax));
    }

    [Fact]
    public void SupremeDamageResistanceFeedsItsCombatAndVitalConsumersWhileLoaded()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)484, (ushort)50) });
        Assert.Equal(550, Value(player.playerInstance.InnerStrengthMax));
        Assert.Equal(11, AbilityStats.Toughness(player, ranged: false));
        Assert.Equal(11, AbilityStats.Toughness(player, ranged: true));
        Assert.Equal(110, AbilityStats.ScaleTypedAbilityDamage(player, 2, 100));
        Assert.Equal(110, AbilityStats.ScaleTypedAbilityDamage(player, 7, 100));
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        Assert.Equal(500, Value(player.playerInstance.InnerStrengthMax));
        Assert.Equal(0, AbilityStats.Toughness(player, ranged: false));
        Assert.Equal(0, AbilityStats.Toughness(player, ranged: true));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(player, 2, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(player, 7, 100));
    }

    [Fact]
    public void DeathEffectReducesLearnedLevelPassivesAndRecoveryRestoresThem()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (52, 50), (45, 50), (269, 1) });
        client.playerData.deathPenalty = .85f;
        client.playerData.deathPenaltyAt = DeathPenalty.Now;
        AbilityPassives.Refresh(client);
        Assert.Equal(168, AbilityStats.Player(client, 643)); // exact initial15% loss, independent of delivery timing
        client.playerData.deathPenalty = .5f;
        client.playerData.deathPenaltyAt = DeathPenalty.Now;
        AbilityPassives.Refresh(client);
        Assert.Equal(100, AbilityStats.Player(client, 643));
        Assert.Equal(525, Value(client.playerInstance.InnerStrengthMax));
        Assert.Equal(575, Value(client.playerInstance.MaxHealth)); // f3 character-level bonus is unchanged
        client.playerData.deathPenalty = 1;
        AbilityPassives.Refresh(client);
        Assert.Equal(200, AbilityStats.Player(client, 643));
        Assert.Equal(550, Value(client.playerInstance.InnerStrengthMax));
    }

    [Theory]
    [InlineData(77, 9)]
    [InlineData(54, 37)]
    [InlineData(1098, 180)]
    [InlineData(169, 14)]
    [InlineData(513, 13)]
    public void HealsUseTheirOwnTemplateRatherThanTheTargetsMaximum(ushort ability, ushort expected)
    {
        var caster = Client();
        var target = Client();
        target.playerInstance.MaxHealth.setValue((ushort)4000);
        Assert.Equal(expected, AbilityHealing.Amount(caster, target, ability));
        target.playerInstance.MaxHealth.setValue((ushort)200);
        Assert.Equal(expected, AbilityHealing.Amount(caster, target, ability));
    }

    [Fact]
    public void PetRepairsCannotBeCastAsPlayerHeals()
    {
        var client = Client();
        Assert.Empty(AbilitySupport.Targets(client, 30, 2));
        Assert.Equal(0, AbilityHealing.Amount(client, client, 30));
    }

    [Theory]
    [InlineData(30, 20, 22)]
    [InlineData(31, 34, 39)]
    public void SimulacrumRepairUsesStoredHealthAndOnlyCasterInfluence(ushort ability, int baseline, int influenced)
    {
        var caster = Client();
        Assert.Equal(0, AbilityHealing.Amount(caster, Client(), ability));
        Assert.Equal(baseline, CoderPets.FixedHeal(caster, AbilityCatalog.Find(ability)));
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (71, 50), (233, 50), (370, 50) });
        Assert.Equal(15, AbilityStats.Influence(caster, 174));
        Assert.Equal(influenced, CoderPets.FixedHeal(caster, AbilityCatalog.Find(ability)));
    }

    [Fact]
    public void HealingInfluenceBelongsToTheCasterAndUnloadRemovesIt()
    {
        var caster = Client(); var target = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (71, 50), (233, 50), (370, 50) });
        Assert.Equal(15, AbilityStats.Influence(caster, 174));
        Assert.Equal(206, AbilityHealing.Amount(caster, target, 1098)); // native binary32 1.15 then x87 truncation
        Assert.Equal(180, AbilityHealing.Amount(target, caster, 1098));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(180, AbilityHealing.Amount(caster, target, 1098));
    }

    [Fact]
    public void RegenerationBonusesChangeRealTicksAndRetainFractionalStrength()
    {
        Assert.Equal((ushort)110, HealthRegen.Next(100, 500, false, false, 1, 100));
        double carry = 0;
        Assert.Equal(12, AbilityHandler.RegenAmount(false, ref carry, 50));
        Assert.Equal(2, AbilityHandler.RegenAmount(true, ref carry, 50));
        Assert.Equal(.4, carry, 5);
    }

    [Fact]
    public void CodeHardeningAffectsOnlyMeleeAndBallisticDamageWhileLoaded()
    {
        var caster = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (608, 50) });
        Assert.Equal(5, AbilityStats.Influence(caster, 159));
        Assert.Equal(5, AbilityStats.Influence(caster, 171));
        Assert.Equal(104, AbilityStats.ScaleTypedAbilityDamage(caster, 0, 100));
        Assert.Equal(104, AbilityStats.ScaleTypedAbilityDamage(caster, 1, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(caster, 2, 100));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(caster, 0, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(caster, 1, 100));
    }

    [Fact]
    public void UtilityMasterParentGrantsTheActionAndAffectsOnlyMeleeAccuracy()
    {
        var caster = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (620, 50) });
        Assert.True(AbilityPassives.IsLoaded(caster, 1108));
        Assert.Equal(5, AbilityStats.Player(caster, 997));
        Assert.Equal(2, AbilityStats.Attack(caster, false));
        Assert.Equal(0, AbilityStats.Attack(caster, true));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(caster, 1108));
        Assert.Equal(0, AbilityStats.Attack(caster, false));
    }

    [Fact]
    public void UtilityMasterReplacesTechnicianAndFeedsAllFourSelfBuffStats()
    {
        var caster = Client();
        Assert.Same(caster, Assert.Single(AbilitySupport.Targets(caster, 604, 100)));
        Assert.Same(caster, Assert.Single(AbilitySupport.Targets(caster, 613, 100)));
        Assert.Same(caster, Assert.Single(AbilitySupport.Targets(caster, 1108, 100)));
        AbilitySupport.Land(caster, 604, 100);
        Assert.Equal(550, Value(caster.playerInstance.MaxHealth));
        Assert.Equal(20, AbilityStats.Toughness(caster, false));
        Assert.Equal(20, AbilityStats.Toughness(caster, true));
        AbilitySupport.Land(caster, 613, 100);
        Assert.False(AbilityBuffs.IsActive(caster, 604));
        Assert.Equal(600, Value(caster.playerInstance.MaxHealth));
        Assert.Equal(3, AbilityStats.Defense(caster, false));
        Assert.Equal(20, AbilityStats.Toughness(caster, false));
        Assert.Equal(20, AbilityStats.Toughness(caster, true));
        AbilitySupport.Land(caster, 1108, 100);
        Assert.False(AbilityBuffs.IsActive(caster, 613));
        Assert.True(AbilityBuffs.IsActive(caster, 1108));
        Assert.Equal(650, Value(caster.playerInstance.MaxHealth));
        Assert.Equal(100, Value(caster.playerInstance.Health));
        Assert.Equal(6, AbilityStats.Defense(caster, false));
        Assert.Equal(20, AbilityStats.Toughness(caster, false));
        Assert.Equal(20, AbilityStats.Toughness(caster, true));
        Assert.True(AbilityBuffs.Remove(caster, 1108));
        Assert.Equal(500, Value(caster.playerInstance.MaxHealth));
        Assert.Equal(0, AbilityStats.Defense(caster, false));
        Assert.Equal(0, AbilityStats.Toughness(caster, false));
        Assert.Equal(0, AbilityStats.Toughness(caster, true));
    }

    [Fact]
    public void WeaponMakerFeedsTheWeaponCategoryCompileContest()
    {
        var caster = Client();
        ushort weapon = CraftingHandler.Specialization(CraftingRules.Find(2565).Category);
        ushort unrelated = CraftingHandler.Specialization(CraftingRules.Find(1271).Category);
        Assert.Equal(655, weapon);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (517, 50) });
        Assert.Equal(50, CraftingHandler.Skill(caster, weapon));
        Assert.Equal(0, CraftingHandler.Skill(caster, unrelated));
        Assert.True(CraftingRules.Succeeds(1, 6, 51, CraftingHandler.Skill(caster, weapon)));
        Assert.False(CraftingRules.Succeeds(1, 6, 51, CraftingHandler.Skill(caster, unrelated)));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, CraftingHandler.Skill(caster, weapon));
    }

    [Fact]
    public void ToolAndUpgradeMakerFeedOnlyTheirMatchingCompileCategories()
    {
        var caster = Client();
        ushort tool = CraftingHandler.Specialization(CraftingRules.Find(2485).Category);
        ushort consumable = CraftingHandler.Specialization(CraftingRules.Find(39413).Category);
        ushort handgun = CraftingHandler.Specialization(CraftingRules.Find(700).Category);
        Assert.Equal(654, tool);
        Assert.Equal(656, consumable);
        foreach (var (ability, matching, unrelated) in new[] { ((ushort)518, tool, consumable), ((ushort)521, consumable, tool) })
        {
            AbilityPassives.SetLoaded(caster, new[] { (ability, (ushort)50) });
            Assert.Equal(50, CraftingHandler.Skill(caster, matching));
            Assert.Equal(0, CraftingHandler.Skill(caster, unrelated));
            Assert.Equal(0, CraftingHandler.Skill(caster, handgun));
            Assert.True(OrdinaryItemCode.Succeeds(10, 10, 50, CraftingHandler.Skill(caster, matching)));
            Assert.False(OrdinaryItemCode.Succeeds(10, 10, 50, CraftingHandler.Skill(caster, unrelated)));
        }
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, CraftingHandler.Skill(caster, tool));
        Assert.Equal(0, CraftingHandler.Skill(caster, consumable));
    }

    [Fact]
    public void HackerDamageUsesPassiveAndActiveInfluenceAndUnloadingRestoresTheBase()
    {
        var caster = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (95, 50) });
        Assert.Equal(15, AbilityStats.Influence(caster, 172));
        Assert.Equal((ushort)229, AbilityStats.ScaleAbilityDamage(caster, 359, 200));
        Assert.Equal((ushort)995, AbilityStats.ScaleAbilityDamage(caster, 474, 866));
        AbilityBuffs.Apply(caster, 327); // Overclock +43 viral influence
        Assert.Equal((ushort)316, AbilityStats.ScaleAbilityDamage(caster, 359, 200));
        AbilityBuffs.Remove(caster, 327);
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal((ushort)200, AbilityStats.ScaleAbilityDamage(caster, 359, 200));
        Assert.Equal((ushort)0, AbilityStats.ScaleAbilityDamage(caster, 359, 0));
    }

    [Theory]
    [InlineData(587, 117, 134)]
    [InlineData(596, 174, 200)]
    [InlineData(615, 83, 95)]
    public void AdvancedSimulacraRepairsUseTheirOwnTemplateAndCasterInfluence(ushort ability, int baseline, int influenced)
    {
        var caster = Client();
        var template = AbilityCatalog.Find(ability);
        Assert.Equal(baseline, CoderPets.FixedHeal(caster, template));
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (71, 50), (233, 50), (370, 50) });
        Assert.Equal(15, AbilityStats.Influence(caster, 174));
        Assert.Equal(influenced, CoderPets.FixedHeal(caster, template));
        Assert.Equal(0, AbilityHealing.Amount(caster, Client(), ability));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(baseline, CoderPets.FixedHeal(caster, template));
    }

    [Theory]
    [InlineData(25, 5, 1, 4)]
    [InlineData(50, 10, 2, 11)]
    public void AdvancedCoderPassivesReachOnlyTheirTypedCombatConsumers(ushort rank, int viralDamage, int accuracyAndDefense, int ballisticResistance)
    {
        var caster = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (585, rank), (600, rank), (614, rank) });
        Assert.Equal(viralDamage, AbilityStats.Influence(caster, 172));
        Assert.True(AbilityStats.ScaleAbilityDamage(caster, 359, 100) > 100);
        Assert.Equal((ushort)100, AbilityStats.ScaleTypedAbilityDamage(caster, 0, 100));
        Assert.Equal(accuracyAndDefense, AbilityStats.Attack(caster, false));
        Assert.Equal(accuracyAndDefense, AbilityStats.Attack(caster, true));
        Assert.Equal(accuracyAndDefense, AbilityStats.Defense(caster, false));
        Assert.Equal(0, AbilityStats.Defense(caster, true));
        Assert.Equal(0, AbilityStats.Toughness(caster, false));
        Assert.Equal(ballisticResistance, AbilityStats.Toughness(caster, true));

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal((ushort)100, AbilityStats.ScaleAbilityDamage(caster, 359, 100));
        Assert.Equal(0, AbilityStats.Attack(caster, false));
        Assert.Equal(0, AbilityStats.Attack(caster, true));
        Assert.Equal(0, AbilityStats.Defense(caster, false));
        Assert.Equal(0, AbilityStats.Toughness(caster, true));
    }

    [Fact]
    public void UtilityCoderFeedsMaximumHealthAndBothPhysicalResistances()
    {
        var caster = Client();
        Assert.Same(caster, Assert.Single(AbilitySupport.Targets(caster, 604, 100)));
        AbilitySupport.Land(caster, 604, 100);
        Assert.True(AbilityBuffs.IsActive(caster, 604));
        Assert.Equal(550, Value(caster.playerInstance.MaxHealth));
        Assert.Equal(100, Value(caster.playerInstance.Health));
        Assert.Equal(20, AbilityStats.Toughness(caster, false));
        Assert.Equal(20, AbilityStats.Toughness(caster, true));
        Assert.True(AbilityBuffs.Remove(caster, 604));
        Assert.Equal(500, Value(caster.playerInstance.MaxHealth));
        Assert.Equal(0, AbilityStats.Toughness(caster, false));
        Assert.Equal(0, AbilityStats.Toughness(caster, true));
    }

    [Theory]
    [InlineData(518, 1271u, 2399u, 654)]
    [InlineData(521, 2399u, 1271u, 656)]
    public void CraftSpecializationUsesTheLoadedSkillForTheShippedCategory(ushort ability, uint item, uint other, ushort stat)
    {
        var caster = Client();
        ushort specialization = CraftingHandler.Specialization(CraftingRules.Find(item).Category);
        ushort unrelated = CraftingHandler.Specialization(CraftingRules.Find(other).Category);
        Assert.Equal(stat, specialization);
        AbilityPassives.SetLoaded(caster, new[] { (ability, (ushort)50) });
        Assert.Equal(50, CraftingHandler.Skill(caster, specialization));
        Assert.Equal(0, CraftingHandler.Skill(caster, unrelated));
        Assert.False(CraftingRules.Succeeds(1, 6, 50, CraftingHandler.Skill(caster, specialization)));
        Assert.True(CraftingRules.Succeeds(1, 6, 51, CraftingHandler.Skill(caster, specialization)));
        Assert.False(CraftingRules.Succeeds(1, 6, 51, CraftingHandler.Skill(caster, unrelated)));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, CraftingHandler.Skill(caster, specialization));
    }
}
