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
}
