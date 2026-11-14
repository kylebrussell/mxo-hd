using System.Runtime.CompilerServices;
using System.Reflection;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class SeverArteryStateTests
{
    private sealed class Subject:TimedEffects.Subject
    {
        public bool Living=true;public int AppliedCount;public int DeterminationValue;
        internal override int Determination=>DeterminationValue;
        internal override WorldClient Context=>null!;
        internal override bool Alive=>Living;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active> a,TimedEffects.Active i,List<TimedEffects.Active> r)=>AppliedCount++;
        internal override bool Ticked(TimedEffects.Active a,int damage)=>true;
        internal override void Ended(List<TimedEffects.Active> ended){}
    }
    private static (Subject,TimedEffects.Active) Marker(double now)
    {
        var s=new Subject();Assert.True(TempMods.TryGet(341,out var effect));
        var mods=TimedEffects.Apply(s,effect,now,rng:new Random(1));var marker=Assert.Single(mods,m=>m.Proc==TempMods.Proc.SeverState);
        marker.CallbackCurrent=()=>true;marker.CallbackStateCurrent=()=>true;return(s,marker);
    }
    [Theory][InlineData(0,0,199)][InlineData(9,1,200)][InlineData(0,2,201)][InlineData(10,2,0)][InlineData(99,0,0)]
    public void OriginalChanceAndUniformSelectorHaveSeparateDraws(int roll,int selector,int expected)
    {var ranges=new List<int>();Assert.Equal(expected,AbilitySeverArteryStates.Choose(n=>{ranges.Add(n);return n==100?roll:selector;}));Assert.Equal(expected==0?new[]{100}:new[]{100,3},ranges);}
    [Theory][InlineData("1","mxotest_sever","1","0",100,0)][InlineData("1","mxotest_sever","1","99",100,99)]
    [InlineData("1","mxotest_sever","1","2",3,2)][InlineData("0","mxotest_sever","1","0",100,null)]
    [InlineData("1","mxotest_sever","0","0",100,null)][InlineData("1","reality_hd","1","0",100,null)]
    [InlineData("1","mxotest_","1","0",100,null)][InlineData("1","mxotest_a;DROP","1","0",100,null)]
    [InlineData("1","mxotest_a","1","1",100,null)][InlineData("1","mxotest_a","1","3",3,null)]
    public void DevDrawUsesActualStrictDisposableNameAndFeatureAndOnlyApprovedValues(string dev,string db,string marker,string raw,int max,int? expected)
        =>Assert.Equal(expected,AbilitySeverArteryStates.QaDraw(dev,db,marker,raw,max));
    [Fact] public void StateAppendsWithoutReplacingDotSlowCallbackAndRefreshesOnlySameState()
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var(s,m)=Marker(now);var originals=s.Effects.Snapshot().ToArray();
        Assert.True(AbilitySeverArteryStates.Execute(s,m,now,n=>0));var first=Assert.Single(s.Effects.Snapshot(),a=>a.Target==199);
        Assert.Equal((ushort)341,first.Parent);Assert.Equal(10,first.Seconds);Assert.Equal(now+10,first.Expires);Assert.All(originals,a=>Assert.Contains(a,s.Effects.Snapshot()));
        Assert.True(AbilitySeverArteryStates.Execute(s,m,now+1,n=>n==100?0:1));Assert.Contains(first,s.Effects.Snapshot());
        Assert.True(AbilitySeverArteryStates.Execute(s,m,now+2,n=>0));Assert.DoesNotContain(first,s.Effects.Snapshot());Assert.Single(s.Effects.Snapshot(),a=>a.Target==199);
        Assert.Single(s.Effects.Snapshot(),a=>a.Target==200);Assert.Equal(68,Assert.Single(originals,a=>a.Proc==TempMods.Proc.Dot).Value);
        Assert.Contains(originals,a=>a.Target==115&&a.Value==-50);TimedEffects.Clear(s);
    }
    [Theory][InlineData("removed")][InlineData("death")][InlineData("expired")][InlineData("stale")][InlineData("pending-during-draw")]
    public void RemovedDeadExpiredOrInvalidatedDuringDrawCannotAddState(string change)
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var(s,m)=Marker(now);
        if(change=="removed")TimedEffects.RemoveParent(s,341);if(change=="death")s.Living=false;if(change=="expired")now+=30;if(change=="stale")m.CallbackCurrent=()=>false;
        Assert.False(AbilitySeverArteryStates.Execute(s,m,now,n=>{if(change=="pending-during-draw")m.CallbackCurrent=()=>false;return 0;}));
        Assert.DoesNotContain(s.Effects.Snapshot(),a=>a.Proc==TempMods.Proc.State&&a.Parent==341);TimedEffects.Clear(s);
    }
    [Fact] public void Only341HasOneThirtySecondCallbackAndSchedulerDoesNotDuplicateTicksOrTickAtExpiry()
    {
        var e=AbilityHostile.Catalog(341);var m=Assert.Single(e.Mods,a=>a.Proc==TempMods.Proc.SeverState);Assert.Equal(30,m.Seconds);Assert.Equal(2.5,m.Interval);
        foreach(ushort id in new ushort[]{146,281,340})Assert.DoesNotContain(TempMods.TryGet(id,out var other)?other.Mods:Array.Empty<TempMods.Mod>(),a=>a.Proc==TempMods.Proc.SeverState);
        var holder=new TimedEffects.Holder();var active=TimedEffects.Instantiate(e,new Random(1)).Single(a=>a.Proc==TempMods.Proc.SeverState);holder.Apply(341,new[]{active},100);
        var ticks=new List<TimedEffects.Active>();var expired=new List<TimedEffects.Active>();holder.Advance(102.5,ticks,expired);Assert.Single(ticks);ticks.Clear();holder.Advance(102.5,ticks,expired);Assert.Empty(ticks);
        holder.Advance(130,ticks,expired);Assert.Equal(10,ticks.Count);Assert.Single(expired);Assert.Equal(11,active.Ticks);
    }
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.playerData.setOnWorld(true);c.playerInstance=new PlayerCharacter();c.playerInstance.Level.setValue((byte)50);
        void Set(string field,object value)=>typeof(WorldClient).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,value);
        Set("alive",true);Set("<IsBot>k__BackingField",true);Set("<TransportFence>k__BackingField",new WorldSendFence());return c;
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)]
    public void CapturedSourceLifetimeRejectsResetLifePendingDeathDisconnectAndOwnerReplacement(int change)
    {
        var c=Client();var current=AbilitySeverArteryStates.Lifetime(c);Assert.True(current());
        switch(change){case 0:c.playerData.worldGeneration++;break;case 1:c.playerData.lifeGeneration++;break;case 2:c.playerData.worldChangePending=true;break;case 3:c.playerData.isDead=true;break;case 4:typeof(WorldClient).GetField("alive",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,false);break;case 5:c.playerData=new();break;case 6:c.playerData.waitForRPCShutDown=true;break;case 7:c.TransportFence.PublishIncoming(true,()=>{});break;}
        Assert.False(current());
    }
    [Fact] public void RecipientWorldChangeCannotPublishCapturedCallbackAndNativeModeHasNoGlobalRngOverride()
    {
        var source=Client();var target=Client();var subject=TimedEffects.ForPlayer(target);var marker=new TimedEffects.Active{Parent=341,Target=710,Value=10,Seconds=30,Proc=TempMods.Proc.SeverState,Caster=source};
        subject.Effects.Apply(341,new[]{marker},100);AbilitySeverArteryStates.Bind(subject,marker);target.playerData.lifeGeneration++;
        Assert.False(AbilitySeverArteryStates.Execute(subject,marker,101,_=>throw new Exception("Stale target must not draw")));Assert.Single(subject.Effects.Snapshot());subject.Effects.RemoveAll();
        var mode=new NativeQa.Modes.SeverArteryStatesMode().Plan;Assert.Single(mode.EnvironmentOverrides);Assert.Equal("1",mode.EnvironmentOverrides[AbilitySeverArteryStates.Feature]);
        Assert.False(mode.FixedCallbackRolls);Assert.False(mode.GmCharacter110);Assert.Contains((ushort)341,mode.Abilities);Assert.Contains(HeadlessClient.Scenarios.All,s=>s.Name=="sever-artery-states");
    }
    [Theory][InlineData(0,199)][InlineData(1,200)][InlineData(2,201)]
    public void ScriptTenSecondArgumentRetainsNormalClassFourDurationAndTargetMitigation(int selector,ushort state)
    {
        // Actual state199/200/201 have no AttributeLink. Do not invent a duration-bonus stat for them.
        Assert.Equal((ushort)0,AbilityControlDurations.BonusStat(state));
        double now=TimeUtils.GetSimTimeSeconds()+100;var(s,m)=Marker(now);m.Caster=Client();s.DeterminationValue=255;
        Assert.True(AbilitySeverArteryStates.Execute(s,m,now,n=>n==100?0:selector));
        var child=Assert.Single(s.Effects.Snapshot(),a=>a.Target==state);Assert.Equal(9.34f,child.Seconds,4);
        Assert.Equal(10,m.Value);Assert.Equal(now+child.Seconds,child.Expires);TimedEffects.Clear(s);
    }
    [Fact] public void NpcDeathGenerationPreventsOldMarkerRevivalAfterSameObjectRespawnOrIdentityReuse()
    {
        var mob=new Mob();mob.setHealthM(500);mob.setHealthC(500);mob.setEntityId(1);var live=AbilitySeverArteryStates.MobLifetime(mob);Assert.True(live());
        mob.setIsDead(true);Assert.False(live());mob.ResetForRespawn();Assert.False(live()); // No need to rely on a timer sweeping the corpse before respawn.
        var respawn=AbilitySeverArteryStates.MobLifetime(mob);Assert.True(respawn());mob.setEntityId(2);Assert.False(respawn());
        var current=AbilitySeverArteryStates.MobLifetime(mob);mob.setIsSpawned(false);Assert.False(current());
    }
}
