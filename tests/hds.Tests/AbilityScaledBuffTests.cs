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
        Assert.True(AbilityScaledBuffs.TryScriptedTimed(63,20,out _));
        Assert.True(AbilityAuras.Handles(378)); // explicit child term sweep executor
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
