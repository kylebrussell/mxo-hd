using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;

[Collection("DelayedActions")]
public class CombatInsightEngagementTests
{
    [Theory]
    [InlineData(0,0,0,0,0,100,110,false)]
    [InlineData(0,0,10,0,0,110,110,true)]
    [InlineData(0,0,10,0,25,110,135,false)]
    [InlineData(100,0,10,0,25,220,135,true)]
    [InlineData(-400,0,100,0,0,0,110,false)]
    [InlineData(-20,0,10,0,0,88,110,false)]
    [InlineData(0,10,10,0,0,121,110,true)]
    [InlineData(0,0,-10,0,0,100,110,false)]
    public void ExplicitOursScoresConsumeThePercentageOnce(int tacticForce,int force,int insight,int tacticEvade,int evade,long expectedForce,long expectedEvade,bool allowed)
    {
        var r=CombatInsightEngagement.Calculate(tacticForce,force,insight,tacticEvade,evade);
        Assert.Equal(expectedForce,r.Force);Assert.Equal(expectedEvade,r.Evade);Assert.Equal(allowed,r.Allows);
    }

    [Fact]public void ExtremeInputsDoNotOverflowOrProduceNegativeScores()
    {
        var high=CombatInsightEngagement.Calculate(int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue);
        Assert.True(high.Force>high.Evade);Assert.True(high.Evade>0);
        var low=CombatInsightEngagement.Calculate(int.MinValue,int.MinValue,int.MinValue,int.MinValue,int.MinValue);
        Assert.Equal(0,low.Force);Assert.Equal(10,low.Evade);
    }

    private sealed class Fixture:IDisposable
    {
        internal readonly WorldClient Source=Client(110),Target=Client(111);
        internal readonly ClientView View;
        internal uint Handle=>(uint)(View.ViewID|(View.spawnId<<16));
        internal Fixture()
        {
            Source.playerData.combatTactic=CombatTactic.Power;Target.playerData.combatTactic=CombatTactic.Speed;
            View=Source.viewMan.addView(Target.playerData.getEntityId(),12);View.viewCreated=true;View.spawnId=7;
            AbilityPassives.SetLoaded(Source,new (ushort,ushort)[]{(2,50)});
            AbilityPassives.SetLoaded(Target,new (ushort,ushort)[]{(2,50),(217,50),(49,50)});
            Assert.True(AbilityBuffs.Apply(Target,217));
        }
        public void Dispose(){AbilityBuffs.Remove(Target,217);AbilityBuffs.Remove(Target,49);TimedEffects.Clear(TimedEffects.ForPlayer(Target));TimedEffects.Clear(TimedEffects.ForPlayer(Source));}
    }
    private static WorldClient Client(uint id)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.viewMan=new ViewManager();c.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,new WorldSendFence());
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        c.Alive=true;c.playerData.setCharID(id);c.playerData.setEntityId(id);c.playerData.setOnWorld(true);c.playerData.setDistrictId(1);
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)5000);c.playerInstance.MaxHealth.setValue((ushort)5000);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);c.playerInstance.InnerStrengthMax.setValue((ushort)1000);return c;
    }

    [Fact]
    public void CompelCloseCombatAndCombatEvasionFeedOppositeSidesOfTheLiveEntryGate()
    {
        using var f = new Fixture();
        Assert.False(CombatInsightEngagement.Allows(f.Source, f.Target, f.Handle, out var before));
        Assert.Equal(100, before.Force);
        Assert.Equal(110, before.Evade);
        try
        {
            Assert.True(AbilityBuffs.Apply(f.Source, 150));
            Assert.True(CombatInsightEngagement.Allows(f.Source, f.Target, f.Handle, out var compelled));
            Assert.Equal(120, compelled.Force);
            Assert.True(AbilityBuffs.Apply(f.Target, 110));
            Assert.False(CombatInsightEngagement.Allows(f.Source, f.Target, f.Handle, out var evasion));
            Assert.Equal(130, evasion.Evade);
        }
        finally
        {
            AbilityBuffs.Remove(f.Source, 150);
            AbilityBuffs.Remove(f.Target, 110);
        }
        Assert.False(CombatInsightEngagement.Allows(f.Source, f.Target, f.Handle, out var removed));
        Assert.Equal(before.Force, removed.Force);
        Assert.Equal(before.Evade, removed.Evade);
    }

    [Fact]public void RealLoaded113AndStored49ProduceTheFourBeforeAfterScores()
    {
        using var f=new Fixture();
        int unchangedAccuracy=AbilityStats.Player(f.Source,522);
        Assert.False(CombatInsightEngagement.Allows(f.Source,f.Target,f.Handle,out var before));Assert.Equal(100,before.Force);
        AbilityPassives.SetLoaded(f.Source,new (ushort,ushort)[]{(2,50),(113,50)});
        Assert.Equal(10,AbilityStats.Player(f.Source,1048));
        Assert.True(CombatInsightEngagement.Allows(f.Source,f.Target,f.Handle,out var loaded));Assert.Equal(110,loaded.Force);
        Assert.True(AbilityBuffs.Apply(f.Target,49));Assert.Equal(25,AbilityStats.Player(f.Target,520));
        Assert.False(CombatInsightEngagement.Allows(f.Source,f.Target,f.Handle,out var blurred));Assert.Equal(135,blurred.Evade);
        f.Source.playerData.combatTactic=CombatTactic.Grab;
        Assert.True(CombatInsightEngagement.Allows(f.Source,f.Target,f.Handle,out var grabbed));Assert.Equal(220,grabbed.Force);
        //The consumer never turns1048 into an attack/defence stat.
        Assert.Equal(0,AbilityStats.Player(f.Source,519));Assert.Equal(unchangedAccuracy,AbilityStats.Player(f.Source,522));
    }

    [Fact]
    public void BottleneckFieldReducesTheLiveForceScoreUntilExpiry()
    {
        using var fixture = new Fixture();
        fixture.Source.playerData.combatTactic = CombatTactic.Grab;
        Assert.True(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var baseline));
        Assert.True(TempMods.TryGet(402, out var bottleneck));
        var subject = TimedEffects.ForPlayer(fixture.Source);
        double start = TimeUtils.GetSimTimeSeconds();
        TimedEffects.Apply(subject, bottleneck, start);
        Assert.True(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var affected));
        Assert.Equal(baseline.Force - 25, affected.Force);
        Assert.Equal(baseline.Evade, affected.Evade);
        foreach (ushort movement in new ushort[] { 115, 877, 878 })
            Assert.Equal(-125, AbilityStats.Player(fixture.Source, movement));
        TimedEffects.RunDue(start + 18.01);
        Assert.True(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var restored));
        Assert.Equal(baseline.Force, restored.Force);
    }

    [Fact]public void Positive520NeverRecreatesRemovedOrSuppressed217()
    {
        using var f=new Fixture();Assert.True(AbilityBuffs.Apply(f.Target,49));
        TimedEffects.Apply(TimedEffects.ForPlayer(f.Target),new TempMods.Effect{Ability=1125,Mods=new[]{new TempMods.Mod{
            Kind=TempMods.KindStat,Proc=TempMods.Proc.Terminal,Target=710,Seconds=8}}},TimeUtils.GetSimTimeSeconds());
        Assert.False(EvasionShield.HasState(f.Target));Assert.Equal(25,AbilityStats.Player(f.Target,520));
        Assert.True(CombatInsightEngagement.Allows(f.Source,f.Target,f.Handle,out _));
        TimedEffects.RemoveParent(TimedEffects.ForPlayer(f.Target),1125);AbilityBuffs.Remove(f.Target,217);
        Assert.True(CombatInsightEngagement.Allows(f.Source,f.Target,f.Handle,out _));
    }

    [Fact]
    public void CutthroatUpgradeContributesTenCombatEvasionPointsToTheLiveEntryGate()
    {
        using var f = new Fixture();
        Assert.False(CombatInsightEngagement.Allows(f.Source, f.Target, f.Handle, out var baseline));
        Assert.Equal(110, baseline.Evade);
        Assert.True(AbilityBuffs.Apply(f.Target, 1119));
        Assert.False(CombatInsightEngagement.Allows(f.Source, f.Target, f.Handle, out var upgraded));
        Assert.Equal(120, upgraded.Evade);
        Assert.True(AbilityBuffs.Remove(f.Target, 1119));
        Assert.False(CombatInsightEngagement.Allows(f.Source, f.Target, f.Handle, out var removed));
        Assert.Equal(baseline.Evade, removed.Evade);
    }

    [Theory]
    [InlineData(294, 80)]
    [InlineData(1120, 10)]
    [InlineData(1121, 10)]
    public void OperativeEvasionAffectsTheLiveContestAndReturnsToBaseline(ushort ability, int bonus)
    {
        using var fixture = new Fixture();
        Assert.False(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var before));
        Assert.True(AbilityBuffs.Apply(fixture.Target, ability));
        Assert.False(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var active));
        Assert.Equal(before.Evade + bonus, active.Evade);
        Assert.Equal(before.Force, active.Force);

        Assert.True(AbilityBuffs.Remove(fixture.Target, ability));
        Assert.False(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var restored));
        Assert.Equal(before.Evade, restored.Evade);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void CapturedCurrentScopeRejectsOwnerWorldLifeResetAndViewReplacement(int change)
    {
        using var f=new Fixture();var stamp=new CombatInsightEngagement.Scope(f.Source,f.Target,f.Handle);Assert.True(stamp.Current);
        if(change==0)f.Source.playerData=Client(110).playerData;
        if(change==1)f.Target.playerData.worldGeneration++;
        if(change==2)f.Source.playerData.lifeGeneration++;
        if(change==3)f.Source.TransportFence.PublishIncoming(true,()=>{});
        if(change==4)f.View.spawnId++;
        if(change==5)f.View.viewNeedsToBeDeleted=true;
        if(change==6)f.Source.viewMan=new ViewManager();
        Assert.False(stamp.Current);
    }

    [Fact]public void AWrongCurrentSpawnCannotUseTheNewEvasionConsumer()
    {
        using var f=new Fixture();Assert.False(CombatInsightEngagement.Allows(f.Source,f.Target,f.Handle+0x10000,out var result));Assert.False(result.Computed);
    }
}
