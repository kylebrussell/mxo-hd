using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityHealthCallbackTests
{
    private static WorldClient Client(ushort health=100)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)20);c.playerInstance.MaxHealth.setValue((ushort)500);c.playerInstance.Health.setValue(health);
        c.playerInstance.InnerStrengthMax.setValue((ushort)200);c.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    private sealed class ZeroRandom:Random { public override int Next(int maxValue)=>0; }

    private static ushort LastOwnHealth(WorldClient c)
    {
        var bytes=c.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>().Last().content;
        Assert.Equal(new byte[]{2,0,2,128,128,128},bytes.Take(6));
        return BitConverter.ToUInt16(bytes,7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ZenDropsQueuedListenerIfWorldLifeOrLoadGenerationChanges(int change)
    {
        var c=Client();var random=AbilityHealthCallbacks.Random100;
        try
        {
            AbilityPassives.SetLoaded(c,new[]{((ushort)871,(ushort)30)});
            c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
            AbilityHealthCallbacks.Random100=()=>
            {
                if(change==0)c.playerData.worldGeneration++;
                else if(change==1)c.playerData.lifeGeneration++;
                else AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
                return 0;
            };
            AbilityHealthCallbacks.HealthChanged(c,1,true);
            Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));
        }
        finally {AbilityHealthCallbacks.Random100=random;}
    }

    [Fact]
    public void ZenParentRunsOnceAcrossNestedHealingAndStopsAfterWorldReload()
    {
        var c=Client();var random=AbilityHealthCallbacks.Random100;var sample=AbilityHealthCallbacks.Random15;int calls=0;
        try
        {
            AbilityHealthCallbacks.Random100=()=>{calls++;return 0;};AbilityHealthCallbacks.Random15=()=>0;
            c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
            AbilityPassives.SetLoaded(c,new[]{((ushort)871,(ushort)30)});
            AbilityBuffs.Apply(c,196);PvpCombat.Damage(c,1);
            Assert.Equal(1,calls);Assert.Equal(105,AbilityHandler.CurrentInnerStrength(c));
            c.playerData.worldGeneration++;
            AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>(),newWorld:true);
            PvpCombat.Damage(c,1);Assert.Equal(1,calls);Assert.Equal(105,AbilityHandler.CurrentInnerStrength(c));
            AbilityPassives.SetLoaded(c,new[]{((ushort)872,(ushort)30)});
            PvpCombat.Damage(c,1);Assert.Equal(1,calls); // owning child alone is not ownership of parent
        }
        finally {AbilityHealthCallbacks.Random100=random;AbilityHealthCallbacks.Random15=sample;AbilityBuffs.RemoveAll(c);}
    }

    [Fact]
    public void ZenMasterUsesLoadedParentHealthEventsChanceAndVitalCap()
    {
        var c=Client();var original=AbilityHealthCallbacks.Random100;
        try
        {
            c.playerInstance.InnerStrengthAvailable.setValue((ushort)180);
            AbilityHealthCallbacks.Random100=()=>0;
            PvpCombat.Damage(c,1);Assert.Equal(180,AbilityHandler.CurrentInnerStrength(c));
            AbilityPassives.SetLoaded(c,new[]{((ushort)871,(ushort)30)});
            AbilityHealthCallbacks.Reapply(c,872);Assert.Equal(180,AbilityHandler.CurrentInnerStrength(c));
            AbilityHealthCallbacks.Random100=()=>15;
            PvpCombat.Damage(c,1);Assert.Equal(180,AbilityHandler.CurrentInnerStrength(c));
            AbilityHealthCallbacks.Random100=()=>14;
            PvpCombat.Damage(c,1);Assert.Equal(185,AbilityHandler.CurrentInnerStrength(c));
            AbilityHandler.HealSelf(c,1);Assert.Equal(190,AbilityHandler.CurrentInnerStrength(c));
            AbilityHealthCallbacks.HealthChanged(c,0,true);Assert.Equal(190,AbilityHandler.CurrentInnerStrength(c));
            c.playerInstance.InnerStrengthAvailable.setValue((ushort)198);
            PvpCombat.Damage(c,1);Assert.Equal(200,AbilityHandler.CurrentInnerStrength(c));
            AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
            c.playerInstance.InnerStrengthAvailable.setValue((ushort)180);
            AbilityHealthCallbacks.Reapply(c,872);PvpCombat.Damage(c,1);
            Assert.Equal(180,AbilityHandler.CurrentInnerStrength(c));
            AbilityPassives.SetLoaded(c,new[]{((ushort)871,(ushort)30)});
            c.playerData.isDead=true;AbilityHealthCallbacks.HealthChanged(c,1,true);
            Assert.Equal(180,AbilityHandler.CurrentInnerStrength(c));
            c.playerData.isDead=false;c.playerData.duelDeath=true;
            AbilityHealthCallbacks.HealthChanged(c,1,true);Assert.Equal(180,AbilityHandler.CurrentInnerStrength(c));
            Assert.True(AbilityHealthCallbacks.InternalEffect(872));
            Assert.Equal(99,AbilityCatalog.Find(871).Number("CharStatsPrerequisite/ReqCharLevel"));
            Assert.Empty(AbilityProgression.GrantsOf(871));
        }
        finally {AbilityHealthCallbacks.Random100=original;AbilityBuffs.RemoveAll(c);}
    }

    [Fact]
    public void HealthWritersPresentAndReturnTheCompletedCallbackResult()
    {
        var c=Client();var original=AbilityHealthCallbacks.Random15;
        try
        {
            AbilityHealthCallbacks.Random15=()=>0;AbilityBuffs.Apply(c,196);
            Assert.Equal(120,AbilityHandler.HealSelf(c,10));
            Assert.Equal(PvpCombat.HealthOf(c),LastOwnHealth(c));
            c.playerData.setOnWorld(true);HealthRegen.Tick(c,1);
            Assert.Equal(135,PvpCombat.HealthOf(c)); //5 regen +10 proc
            Assert.Equal(PvpCombat.HealthOf(c),LastOwnHealth(c));
            var mob=new Mob();Assert.False(mob.HitCombatTarget(20,target:c));
            Assert.Equal(125,PvpCombat.HealthOf(c));Assert.Equal(PvpCombat.HealthOf(c),LastOwnHealth(c));
            Assert.True(TimedEffects.ForPlayer(c).Ticked(new TimedEffects.Active {Parent=97},20));
            Assert.Equal(115,PvpCombat.HealthOf(c));Assert.Equal(PvpCombat.HealthOf(c),LastOwnHealth(c));
            Assert.True(mob.HitCombatTarget(200,target:c));
            Assert.Equal(0,PvpCombat.HealthOf(c));Assert.Equal(PvpCombat.HealthOf(c),LastOwnHealth(c));
        }
        finally {AbilityHealthCallbacks.Random15=original;AbilityBuffs.RemoveAll(c);}
    }

    [Fact]
    public void NativeFunctionZeroUsesFifteenBitMultiplyAndExcludesUpperEndpoint()
    {
        var m=AbilityCatalog.Find(196).Modifiers.Single(m=>m.Text("StatModHealthChangedFunc")=="OnHealthReduceHealProc");
        Assert.False(AbilityValues.TryValue(m,50,50,out _));
        Assert.True(AbilityValues.TryRandomValue(m,0,out int low));Assert.Equal(10,low);
        Assert.True(AbilityValues.TryRandomValue(m,32767,out int high));Assert.Equal(19,high);
        Assert.False(AbilityValues.TryRandomValue(m,32768,out _));
        for(int roll=0;roll<32768;roll++)
        {
            Assert.True(AbilityValues.TryRandomValue(m,roll,out int value));
            Assert.Equal(10+roll*10/32768,value);
        }
        Assert.Equal(50,AbilityHealthCallbacks.Sample(862,0));Assert.Equal(99,AbilityHealthCallbacks.Sample(862,32767));
        Assert.Equal(149,AbilityHealthCallbacks.Sample(942,32767));Assert.Equal(109,AbilityHealthCallbacks.Sample(944,32767));
    }

    [Fact]
    public void IgnorePainSnapshotsOneRollAndHandlesExternalHealingWithoutRecursiveFullHeal()
    {
        var c=Client(); var original=AbilityHealthCallbacks.Random15;
        try
        {
            AbilityHealthCallbacks.Random15=()=>32767;Assert.True(AbilityBuffs.Apply(c,196));
            AbilityHealthCallbacks.Random15=()=>0;
            Assert.False(PvpCombat.Damage(c,10));Assert.Equal(109,PvpCombat.HealthOf(c));
            AbilityHandler.HealSelf(c,10);Assert.Equal(138,PvpCombat.HealthOf(c)); // external heal plus one stored19
            AbilityBuffs.Remove(c,196);PvpCombat.Damage(c,10);Assert.Equal(128,PvpCombat.HealthOf(c));
        }
        finally { AbilityHealthCallbacks.Random15=original;AbilityBuffs.RemoveAll(c); }
    }

    [Fact]
    public void ExpiryAndOldGenerationCannotFireOrRemoveARefreshedProc()
    {
        var c=Client();var original=AbilityHealthCallbacks.Random15;
        try
        {
            AbilityHealthCallbacks.Random15=()=>32767;AbilityBuffs.Apply(c,196);int old=c.playerData.activeBuffs[196].Generation;
            AbilityHealthCallbacks.Random15=()=>0;AbilityBuffs.Apply(c,196);
            Assert.False(AbilityBuffs.Remove(c,196,generation:old));
            PvpCombat.Damage(c,5);Assert.Equal(105,PvpCombat.HealthOf(c));
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds()+11);
            Assert.False(AbilityBuffs.IsActive(c,196));
            PvpCombat.Damage(c,5);Assert.Equal(100,PvpCombat.HealthOf(c));
        }
        finally { AbilityHealthCallbacks.Random15=original;AbilityBuffs.RemoveAll(c); }
    }

    [Fact]
    public void NeutralBalanceHealIgnoresHealingInfluenceAndNestedParentsAreBounded()
    {
        var c=Client(200);var original=AbilityHealthCallbacks.Random15;
        try
        {
            AbilityHealthCallbacks.Random15=()=>0;
            AbilityBuffs.Apply(c,170);AbilityBuffs.Apply(c,861);
            PvpCombat.Damage(c,50);Assert.Equal(200,PvpCombat.HealthOf(c)); // neutral heal50, not influence55
            AbilityBuffs.Apply(c,196);
            PvpCombat.Damage(c,1);Assert.Equal(259,PvpCombat.HealthOf(c)); // each parent exactly once, not an endless heal chain
        }
        finally { AbilityHealthCallbacks.Random15=original;AbilityBuffs.RemoveAll(c); }
    }

    [Fact]
    public void FatalAndZeroDeltaEventsDoNotInventSelfResurrection()
    {
        var c=Client(5);AbilityBuffs.Apply(c,196);AbilityBuffs.Apply(c,861);
        Assert.True(PvpCombat.Damage(c,10));Assert.Equal(0,PvpCombat.HealthOf(c));
        AbilityHealthCallbacks.HealthChanged(c,0,true);Assert.Equal(0,PvpCombat.HealthOf(c));
        c.playerData.isDead=true;AbilityHealthCallbacks.HealthChanged(c,-1,true);Assert.Equal(0,PvpCombat.HealthOf(c));
        AbilityBuffs.RemoveAll(c);
    }

    [Fact]
    public void DeflectVirusChildCarriesScaledDefencesAndRunsItsFourPercentTermCleanseOnce()
    {
        var c=Client();var previous=AbilityHostile.PulseRandom;
        try
        {
            Assert.True(AbilityAuras.Handles(378));Assert.True(AbilityAuras.Handles(380));
            Assert.Equal(7,AbilityAuras.DrainOf(378));Assert.Equal(693,AbilityAuras.EffectOf(378));
            Assert.True(TempMods.TryGet(481,out var overflow));
            TimedEffects.Apply(TimedEffects.ForPlayer(c),overflow,TimeUtils.GetSimTimeSeconds(),c,"Source");
            AbilityBuffs.Apply(c,693);Assert.Equal(19,AbilityBuffs.Total(c,183));Assert.Equal(9,AbilityBuffs.Total(c,1009));
            AbilityHostile.PulseRandom=new ZeroRandom();
            Assert.True(AbilityBuffs.Remove(c,693));Assert.Equal(0,AbilityBuffBuffer.Reserved(c));
            Assert.False(AbilityBuffs.Remove(c,693));
        }
        finally { AbilityHostile.PulseRandom=previous;AbilityBuffs.RemoveAll(c); }
    }

    [Fact]
    public void LineUpUsesTwoDurationsPreservesConcealmentAndOnlyDamageBreaksTheParent()
    {
        var c=Client();
        try
        {
            Assert.True(AbilityHealthCallbacks.LineUp(c,false));Assert.True(Stealth.IsOn(c,1137));
            Assert.Equal(25,AbilityBuffs.Total(c,524));Assert.Equal(10,AbilityBuffs.Total(c,171));
            Assert.Equal(36,AbilityBuffBuffer.Used(c)); // parent20 once, concealment16
            AbilityHandler.HealSelf(c,1);Assert.True(TimedEffects.ForPlayer(c).Effects.HasParent(506));
            PvpCombat.Damage(c,1);Assert.False(TimedEffects.ForPlayer(c).Effects.HasParent(506));Assert.False(Stealth.IsOn(c,1137));
            Assert.True(AbilityHealthCallbacks.LineUp(c,false));
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds()+13);
            Assert.Equal(0,AbilityBuffs.Total(c,171));Assert.Equal(25,AbilityBuffs.Total(c,524));
            PvpCombat.Damage(c,1);Assert.True(TimedEffects.ForPlayer(c).Effects.HasParent(506)); //12s callback is gone
            int generation=Stealth.Current(c)?.Generation??0;
            Assert.True(AbilityHealthCallbacks.LineUp(c,true));Assert.True((Stealth.Current(c)?.Generation??0)>=generation+2);
            Assert.True(AbilityBuffs.Apply(c,173));Assert.False(TimedEffects.ForPlayer(c).Effects.HasParent(506));
        }
        finally { AbilityBuffs.RemoveAll(c);Stealth.End(c,"test",false); }
    }
}
