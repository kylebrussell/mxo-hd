using hds;
using hds.shared;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace hds.Tests;
[Collection("DelayedActions")]
public class ReverseBulldogTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly WorldClient Source;
        internal readonly Mob Mob=new();
        internal readonly WorldClient? Target;
        private readonly Dictionary<string,WorldClient>? previousClients;
        internal readonly ClientView View;
        internal readonly TimedEffects.Subject Subject;
        internal readonly double Now=TimeUtils.GetSimTimeSeconds()+100;
        internal Fixture(bool player=false)
        {
            Source=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            Source.playerData=new ClientData();Source.playerInstance=new PlayerCharacter();Source.viewMan=new ViewManager();Source.messageQueue=new MessageQueue();
            typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Source,new WorldSendFence());
            typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Source,TimeUtils.getUnixTimeUint32());
            typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Source,long.MaxValue);
            Source.Alive=true;Source.playerData.setOnWorld(true);Source.playerData.setDistrictId(1);Source.playerData.setCharID(388);
            Source.playerInstance.Health.setValue((ushort)1000);Source.playerInstance.MaxHealth.setValue((ushort)1000);Source.playerInstance.Level.setValue((byte)50);
            Mob.setEntityId(3880001);Mob.setDistrict(1);Mob.setHealthC(10000);Mob.setHealthM(10000);Mob.setName("BulldogUnit");Mob.setGoId(new byte[]{0x57,2});
            lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Add(Mob);
            if(player)
            {
                Target=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));Target.playerData=new ClientData();Target.playerInstance=new PlayerCharacter();Target.messageQueue=new MessageQueue();
                typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Target,new WorldSendFence());
                typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Target,TimeUtils.getUnixTimeUint32());
                typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Target,long.MaxValue);
                Target.Alive=true;Target.playerData.setOnWorld(true);Target.playerData.setDistrictId(1);Target.playerData.setCharID(389);Target.playerData.setEntityId(3880002);
                Target.playerInstance.Health.setValue((ushort)10000);Target.playerInstance.MaxHealth.setValue((ushort)10000);Target.playerInstance.Level.setValue((byte)50);
                previousClients=WorldServer.Clients;WorldServer.Clients=new(){["bulldog-target"]=Target};
                View=Source.viewMan.addView(3880002,12);Subject=TimedEffects.ForPlayer(Target);
            }
            else {View=Source.viewMan.addView(Mob.getEntityId(),599);Subject=TimedEffects.ForMob(Mob);}
            View.viewCreated=true;View.spawnId=7;
        }
        internal AbilityReverseBulldog.Cast Cast(bool complete=true)
        {
            var normal=AbilityCastInterrupts.Begin(Source,388,0);var cast=AbilityReverseBulldog.Capture(Source,388,View.ViewID,normal);
            if(complete)Assert.True(AbilityCastInterrupts.Complete(normal,()=>{}));return cast;
        }
        internal List<TimedEffects.Active> Arm(AbilityReverseBulldog.Cast? cast=null)
        {
            Assert.True(TempMods.TryGet(388,out var effect));var mods=TimedEffects.Instantiate(effect,new Random(1));
            var configure=AbilityReverseBulldog.Bind(cast??Cast(),Subject,388);
            foreach(var m in mods)configure(m);
            Subject.Effects.Apply(388,mods,Now,preserveOtherTargets:true);return mods;
        }
        internal void Due(double at)
        {
            var ticks=new List<TimedEffects.Active>();var expired=new List<TimedEffects.Active>();Subject.Effects.Advance(at,ticks,expired);
            foreach(var m in expired)m.OnExpiredAt?.Invoke(Subject,m,at);
        }
        public void Dispose(){Source.Alive=false;TimedEffects.Clear(Subject);if(Target!=null)WorldServer.Clients=previousClients!;lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Remove(Mob);}
    }
    [Fact]public void ActualStoredMarkersHaveDistinctAuthoredTimesAndValues()
    {
        Assert.True(TempMods.TryGet(388,out var effect));Assert.Equal(2,effect.Mods.Length);
        var stun=Assert.Single(effect.Mods,m=>m.Target==388);Assert.Equal(.01f,stun.Seconds);Assert.Equal(8,stun.MinValue);Assert.Equal(TempMods.Proc.Terminal,stun.Proc);
        var timer=Assert.Single(effect.Mods,m=>m.Target==710);Assert.Equal(.1f,timer.Seconds);Assert.Equal(16,timer.MinValue);Assert.Equal(0,effect.ImmunitySeconds);
        Assert.False(AbilityReverseBulldog.Markers(AbilityCatalog.Find(347),out _));Assert.False(AbilityReverseBulldog.Markers(AbilityCatalog.Find(1135),out _));
    }
    [Fact]public void NaturalTermsAddUnconditionalStunThenIndependentLongerTimerOnce()
    {
        using var f=new Fixture();var markers=f.Arm();f.Due(f.Now+.02);
        var stun=Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Target==102);Assert.Equal(8,stun.Seconds);Assert.Equal((ushort)388,stun.Parent);Assert.Equal(TempMods.BreakRule.None,stun.Break);
        Assert.False(f.Subject.Effects.Has(681)); //No invented8second timer at the first callback.
        f.Due(f.Now+.11);var timer=Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Parent==734);Assert.Equal((ushort)681,timer.Target);Assert.Equal(16,timer.Seconds);
        foreach(var m in markers)m.OnExpiredAt?.Invoke(f.Subject,m,f.Now+1);
        Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Target==102);Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Parent==734);Assert.Equal((ushort)10000,f.Mob.getHealthC());
    }
    [Fact]public void PreexistingImmunityBlocksBothCallbacksWithoutReplacingIt()
    {
        using var f=new Fixture();var old=new TimedEffects.Active {Target=681,Proc=TempMods.Proc.State,Seconds=30,RecordId=91};f.Subject.Effects.Apply(734,new[]{old},f.Now);
        f.Arm();f.Due(f.Now+.2);Assert.False(f.Subject.Effects.Has(102));Assert.Same(old,Assert.Single(f.Subject.Effects.Snapshot()));
    }
    [Fact]public void PrematureAndUncompletedCallbacksCannotCreateState()
    {
        using var f=new Fixture();var markers=f.Arm(f.Cast(false));f.Due(f.Now+.2);Assert.False(f.Subject.Effects.Has(102));
        markers=f.Arm();foreach(var m in markers)m.OnExpiredAt?.Invoke(f.Subject,m,f.Now+.001);Assert.False(f.Subject.Effects.Has(102));
    }
    [Theory][InlineData("world")][InlineData("life")][InlineData("owner")][InlineData("pending")][InlineData("transport")][InlineData("dead")][InlineData("off")][InlineData("view")][InlineData("delete")][InlineData("spawn")][InlineData("npcdeath")][InlineData("npcreuse")][InlineData("friendly")][InlineData("removed")]
    public void CapturedSourceAndExactNpcLifetimeFenceEveryTerm(string change)
    {
        using var f=new Fixture();f.Arm();
        switch(change)
        {
            case "world":f.Source.playerData.worldGeneration++;break;case "life":f.Source.playerData.lifeGeneration++;break;
            case "owner":f.Source.playerData=new ClientData();break;case "pending":f.Source.playerData.worldChangePending=true;break;
            case "transport":f.Source.TransportFence.PublishIncoming(true,()=>{});f.Source.TransportFence.Resume(f.Source.TransportFence.Capture());break;
            case "dead":f.Source.playerData.isDead=true;break;case "off":f.Source.playerData.setOnWorld(false);break;
            case "view":f.Source.viewMan.removeViewByViewId(f.View.ViewID);break;case "delete":f.View.viewNeedsToBeDeleted=true;break;case "spawn":f.View.spawnId++;break;
            case "npcdeath":f.Mob.setIsDead(true);break;case "npcreuse":f.Mob.setIsDead(true);f.Mob.setIsDead(false);break;
            case "friendly":f.Mob.isFriendly=true;break;case "removed":lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Remove(f.Mob);break;
        }
        f.Due(f.Now+.2);Assert.False(f.Subject.Effects.Has(102));Assert.False(f.Subject.Effects.Has(681));
    }
    [Theory][InlineData(false)][InlineData(true)]public void ClearAndRemovalInvalidateAlreadyDequeuedCallbacks(bool remove)
    {
        using var f=new Fixture();var markers=f.Arm();var expired=new List<TimedEffects.Active>();f.Subject.Effects.Advance(f.Now+.2,new(),expired);
        if(remove)TimedEffects.RemoveParent(f.Subject,388);else TimedEffects.Clear(f.Subject);
        foreach(var m in expired)m.OnExpiredAt?.Invoke(f.Subject,m,f.Now+.2);
        Assert.True(f.Subject.Effects.IsEmpty);
    }
    [Fact]public void RefreshedReceiptRejectsOldDequeuedTerms()
    {
        using var f=new Fixture();var old=f.Arm();f.Arm();
        foreach(var m in old)m.OnExpiredAt?.Invoke(f.Subject,m,f.Now+.2);
        Assert.False(f.Subject.Effects.Has(102));f.Due(f.Now+.2);Assert.True(f.Subject.Effects.Has(102));Assert.True(f.Subject.Effects.Has(681));
    }
    [Theory][InlineData("world")][InlineData("life")][InlineData("owner")][InlineData("character")][InlineData("district")][InlineData("pending")][InlineData("transport")][InlineData("dead")][InlineData("off")][InlineData("removed")]
    public void PlayerRecipientLifetimeAndCurrentOnlineIdentityFenceCallbacks(string change)
    {
        using var f=new Fixture(true);f.Arm();var target=f.Target!;
        switch(change)
        {
            case "world":target.playerData.worldGeneration++;break;case "life":target.playerData.lifeGeneration++;break;case "owner":target.playerData=new ClientData();break;
            case "character":target.playerData.setCharID(390);break;case "district":target.playerData.setDistrictId(2);break;
            case "pending":target.playerData.worldChangePending=true;break;case "transport":target.TransportFence.PublishIncoming(true,()=>{});target.TransportFence.Resume(target.TransportFence.Capture());break;
            case "dead":target.playerData.isDead=true;break;case "off":target.playerData.setOnWorld(false);break;case "removed":WorldServer.Clients.Remove("bulldog-target");break;
        }
        f.Due(f.Now+.2);Assert.False(f.Subject.Effects.Has(102));Assert.False(f.Subject.Effects.Has(681));Assert.Empty(target.messageQueue.RPCMessagesQueue);
    }
    [Fact]public void PlayerGetsDistinctStateTimerRecordsBoundToCapturedLifetime()
    {
        using var f=new Fixture(true);f.Arm();f.Due(f.Now+.02);Assert.True(f.Subject.Effects.Has(102));Assert.False(f.Subject.Effects.Has(681));
        Assert.Equal(3,f.Target!.messageQueue.RPCMessagesQueue.Count);f.Due(f.Now+.11);Assert.True(f.Subject.Effects.Has(681));Assert.Equal(5,f.Target.messageQueue.RPCMessagesQueue.Count);
        var stun=Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Target==102);Assert.True(stun.RecordId>0);Assert.True(stun.LinkedRecordId>0);Assert.Equal(8,stun.Seconds);
    }
    [Fact]public void ImmuneReapplicationKeepsThePreviouslyAppliedStunAndTimer()
    {
        using var f=new Fixture();f.Arm();f.Due(f.Now+.2);
        var stun=Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Target==102);var timer=Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Parent==734);
        f.Arm();f.Due(f.Now+.3);Assert.Same(stun,Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Target==102));Assert.Same(timer,Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Parent==734));
    }
    [Fact]public void StunUsesExistingClass4BonusAndMitigationWhileExplicitTimerStays16()
    {
        using var f=new Fixture();AbilityPassives.SetLoaded(f.Source,new(ushort,ushort)[]{(384,50)});
        f.Subject.Effects.Apply(77,new[]{new TimedEffects.Active {Kind=TempMods.KindStat,Target=575,Value=300,Seconds=90,Proc=TempMods.Proc.Stat}},f.Now);
        f.Arm();f.Due(f.Now+.2);Assert.Equal(3.128f,Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Target==102).Seconds,4);
        Assert.Equal(16,Assert.Single(f.Subject.Effects.Snapshot(),m=>m.Parent==734).Seconds);AbilityPassives.SetLoaded(f.Source,Array.Empty<(ushort,ushort)>());
    }
    [Fact]public void SameCountersCannotAuthorizeAReplacementPlayerOwner()
    {
        using var f=new Fixture(true);f.Arm();var old=f.Target!.playerData;var replacement=new ClientData();
        replacement.setCharID(old.getCharID());replacement.setEntityId(old.getEntityId());replacement.setDistrictId(old.getDistrictId());replacement.setOnWorld(true);
        replacement.worldGeneration=old.worldGeneration;replacement.lifeGeneration=old.lifeGeneration;f.Target.playerData=replacement;
        f.Due(f.Now+.2);Assert.False(f.Subject.Effects.Has(102));Assert.Empty(f.Target.messageQueue.RPCMessagesQueue);
    }
    [Fact]public void FixtureProfilesAreExplicitNormalLevel50WithoutRandomOrStateControls()
    {
        var source=HeadlessClient.ReverseBulldogFixture.Seed("BulldogUnit");var target=HeadlessClient.ReverseBulldogFixture.Seed("TargetUnit",true);
        Assert.Equal((int)PlayerProgression.ExperienceForLevel(50),source.Experience);Assert.Equal(source.Experience,target.Experience);
        Assert.Equal(HeadlessClient.ReverseBulldogFixture.Abilities,source.OwnedLoadedAbilities!.Select(a=>a.Ability));Assert.Equal(new ushort[]{2,17},target.OwnedLoadedAbilities!.Select(a=>a.Ability));
        Assert.Equal(200,target.X-source.X);Assert.Equal(64,target.Rotation);Assert.Equal(2,target.Organization);Assert.Equal(10000,target.Health);
        var mode=NativeQa.NativeQaModes.Find("reverse-bulldog-controls");Assert.False(mode.Plan.DevCommands);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Contains(mode.Plan.AfterHelpers,sql=>sql.Contains("level=50,exp="+PlayerProgression.ExperienceForLevel(50)));
        Assert.Equal(8,mode.Plan.ExtraQaMetadata["RawStunSeconds"].GetInt32());Assert.Equal(16,mode.Plan.ExtraQaMetadata["ExplicitTimerSeconds"].GetInt32());
        HeadlessClient.ReverseBulldogFixture.RequirePin(new string('a',64),new string('A',64));Assert.Throws<InvalidDataException>(()=>HeadlessClient.ReverseBulldogFixture.RequirePin(null,new string('a',64)));
    }
    [Fact]public void GuardFailureRollsBackOnlyItsExactUnpublishedReplacement()
    {
        var holder=new TimedEffects.Holder();var old=new TimedEffects.Active {Target=102,Seconds=8,RecordId=77};holder.Apply(388,new[]{old},10);
        int checks=0;var candidate=new TimedEffects.Active {Target=102,Seconds=8};Assert.False(holder.AppendCallback(388,candidate,11,()=>++checks==1,out var replaced));
        Assert.Same(old,Assert.Single(holder.Snapshot()));Assert.Equal((ushort)77,old.RecordId);Assert.Same(old,Assert.Single(replaced));
    }
    [Theory][InlineData(false)][InlineData(true)]public void RollbackDoesNotReviveAfterClearOrOverwriteNewerRecord(bool newer)
    {
        var holder=new TimedEffects.Holder();var old=new TimedEffects.Active {Target=102,Seconds=8};holder.Apply(388,new[]{old},10);
        var next=new TimedEffects.Active {Target=102,Seconds=9};int checks=0;
        bool Current(){if(++checks==1)return true;holder.RemoveAll();if(newer)holder.Apply(388,new[]{next},11);return false;}
        Assert.False(holder.AppendCallback(388,new(){Target=102,Seconds=8},11,Current,out _));
        if(newer)Assert.Same(next,Assert.Single(holder.Snapshot()));else Assert.True(holder.IsEmpty);
    }
    [Fact]public void RollbackCannotReviveAnAlreadyExpiredPreviousRecord()
    {
        var holder=new TimedEffects.Holder();var old=new TimedEffects.Active {Target=102,Seconds=1,RecordId=77};holder.Apply(388,new[]{old},10);
        int checks=0;Assert.False(holder.AppendCallback(388,new(){Target=102,Seconds=8},12,()=>++checks==1,out _));Assert.True(holder.IsEmpty);
    }

}
