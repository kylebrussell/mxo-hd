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

    [Theory]
    [InlineData(124,20,171,10)] [InlineData(170,50,174,25)] [InlineData(417,20,106,200)]
    [InlineData(173,20,522,25)] [InlineData(173,50,522,26)] [InlineData(421,50,1009,47)]
    public void TimedTemplatesUseCharacterLevelNativePrecisionAndPositiveCaps(ushort ability,int level,ushort stat,int value)
    {
        Assert.True(AbilityScaledBuffs.TryGet(ability,level,out var buff));
        Assert.Contains(buff.Modifiers,m=>m.Stat==stat&&m.Value==value);
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
        Assert.False(AbilityAuras.Handles(378)); // sweep callback not silently dropped
    }

    [Fact]
    public void TacticalTogglesReachCombatAndRejectTheirScriptConflict()
    {
        var c=Client(20);
        Assert.True(AbilityBuffs.Apply(c,15));
        Assert.Equal(CombatTacticType.Precise,CombatTactics.Type(c));
        Assert.False(AbilityBuffs.Apply(c,9));
        Assert.True(AbilityBuffs.Remove(c,15));
        Assert.Equal(CombatTacticType.Normal,CombatTactics.Type(c));
        Assert.True(AbilityBuffs.Apply(c,9));
        Assert.Equal(CombatTacticType.Energized,CombatTactics.Type(c));
        Assert.False(AbilityBuffs.Apply(c,15));
        Assert.True(AbilityBuffs.Remove(c,9));
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
}
