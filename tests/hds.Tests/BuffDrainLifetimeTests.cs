using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
internal sealed class BuffDrainTestOwner : IDisposable
{
    private readonly Dictionary<string,WorldClient> previous;
    internal const string Key="drain-test-owner";
    internal readonly WorldClient Client;
    internal BuffDrainTestOwner(WorldClient client)
    {
        Client=client;previous=WorldServer.Clients;
        WorldServer.Clients=new Dictionary<string,WorldClient>{{Key,client}};
        Set(client,"key",Key);Set(client,"lastUsedTime",TimeUtils.getUnixTimeUint32());
        Set(client,"<TransportFence>k__BackingField",new WorldSendFence());
        client.Alive=true;client.playerData.setOnWorld(true);client.playerData.setCharID(110);client.playerData.setDistrictId(1);
    }
    internal static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(target,value);
    public void Dispose(){AbilityBuffs.RemoveAll(Client);Client.Alive=false;WorldServer.Clients=previous;}
}
[Collection("DelayedActions")]
public class BuffDrainLifetimeTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.MaxHealth.setValue((ushort)1000);c.playerInstance.Health.setValue((ushort)1000);
        c.playerInstance.InnerStrengthMax.setValue((ushort)100);c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        BuffDrainTestOwner.Set(c,"lastFlushTick",long.MaxValue);return c;
    }
    private static AbilityBuffs.Active On(WorldClient c)
    {AbilityPassives.SetLoaded(c,new[]{((ushort)16,(ushort)50)},true);Assert.True(AbilityBuffs.Apply(c,16));return c.playerData.activeBuffs[16];}
    [Fact]public void CurrentActivationDrainsOnceAndRemovedActivationCannotRestartAfterRollback()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var a=On(c);
        AbilityBuffs.TickActiveDrain(c,a);Assert.Equal(99,AbilityHandler.CurrentInnerStrength(c));
        AbilityBuffs.Remove(c,16);c.playerData.activeBuffs[16]=a;
        AbilityBuffs.TickActiveDrain(c,a);Assert.Equal(99,AbilityHandler.CurrentInnerStrength(c));Assert.True(a.Drain.Cancelled);
        c.playerData.activeBuffs.Remove(16);
    }
    [Fact]public void OldActivationCannotChargeOrRemoveNewActivationEvenWithSameNumericGeneration()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var old=On(c);AbilityBuffs.Remove(c,16);var fresh=On(c);
        fresh.Generation=old.Generation;c.playerInstance.InnerStrengthAvailable.setValue((ushort)0);
        AbilityBuffs.TickActiveDrain(c,old);Assert.Same(fresh,c.playerData.activeBuffs[16]);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);AbilityBuffs.TickActiveDrain(c,old);Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));
        AbilityBuffs.TickActiveDrain(c,fresh);Assert.Equal(99,AbilityHandler.CurrentInnerStrength(c));
        AbilityBuffs.Remove(c,16);AbilityBuffs.TickActiveDrain(c,fresh);Assert.Equal(99,AbilityHandler.CurrentInnerStrength(c));
    }
    [Fact]public void ConfirmedQuitCancelsTheStillMappedLiveEndpointsScheduledDrain()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var active=On(c);
        Assert.True(c.Alive);Assert.True(c.playerData.getOnWorld());Assert.True(c.IsCurrentEndpointOwner);
        AbilityBuffs.CancelDrains(c);
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds()+1.01);
        Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));Assert.True(active.Drain.Cancelled);
        // Restoring an old activation snapshot cannot undo terminal cancellation.
        c.playerData.activeBuffs[16]=active;AbilityBuffs.TickActiveDrain(c,active);Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));
    }
    [Theory][InlineData("world")][InlineData("life")][InlineData("owner")][InlineData("character")][InlineData("player")]
    [InlineData("endpoint")][InlineData("registry")][InlineData("transport")][InlineData("drop")][InlineData("logout")]
    public void InvalidatedLifetimeNeverChargesSendsOrReschedules(string change)
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var a=On(c);
        var data=c.playerData;var player=c.playerInstance;
        switch(change)
        {
            case "world":data.worldGeneration++;break;case "life":data.lifeGeneration++;break;
            case "owner":c.playerData=new ClientData();break;case "character":data.setCharID(111);break;
            case "player":c.playerInstance=new PlayerCharacter();break;
            case "endpoint":WorldServer.Clients[BuffDrainTestOwner.Key]=Client();break;
            case "registry":WorldServer.Clients=new Dictionary<string,WorldClient>();break;
            case "transport":c.TransportFence.PublishIncoming(true,()=>{});break;
            case "drop":data.setOnWorld(false);break;case "logout":c.Alive=false;break;
        }
        int count=c.messageQueue.ObjectMessagesQueue.Count;
        AbilityBuffs.TickActiveDrain(c,a);Assert.Equal(100,NumericalUtils.ByteArrayToUint16(player.InnerStrengthAvailable.getValue(),1));
        Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);Assert.True(a.Drain.Cancelled);
        c.playerData=data;c.playerInstance=player;
        data.setOnWorld(true);AbilityBuffs.TickActiveDrain(c,a);Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));
    }
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public void ResetBetweenValidationClaimAndPublicationNeverChargesOrPublishesIntoNewInstance(bool afterClaim,bool shortfall)
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var old=On(c);var oldPlayer=c.playerInstance;
        if(shortfall)oldPlayer.InnerStrengthAvailable.setValue((ushort)0);
        AbilityBuffs.Active? fresh=null;int queued=0;
        void ResetOnAnotherThread()
        {
            var task=Task.Run(()=>
            {
                c.TransportFence.PublishIncoming(true,()=>{});long epoch=c.TransportFence.Capture();
                Assert.True(c.TransportFence.ApplyReset(epoch,()=>
                {
                    c.playerData.setOnWorld(false);c.playerData=new ClientData();c.playerData.setOnWorld(true);c.playerData.setCharID(110);c.playerData.setDistrictId(1);
                    c.playerInstance=new PlayerCharacter();c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
                    c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);c.playerInstance.InnerStrengthMax.setValue((ushort)100);
                    c.messageQueue.ForgetWorldObjects();c.messageQueue.ForgetSentRpcs();
                }));
                c.TransportFence.Resume(epoch);fresh=On(c);queued=c.messageQueue.ObjectMessagesQueue.Count;
            });
            Assert.True(task.Wait(TimeSpan.FromSeconds(3)),"Reset acquires transport without a buff/upgrade lock inversion");task.GetAwaiter().GetResult();
        }
        AbilityBuffs.TickActiveDrain(c,old,beforeClaim:afterClaim?null:ResetOnAnotherThread,beforePublication:afterClaim?ResetOnAnotherThread:null);
        Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));Assert.Same(fresh,c.playerData.activeBuffs[16]);
        Assert.Equal(queued,c.messageQueue.ObjectMessagesQueue.Count);Assert.True(old.Drain.Cancelled);
        Assert.Equal(shortfall?0:afterClaim?99:100,NumericalUtils.ByteArrayToUint16(oldPlayer.InnerStrengthAvailable.getValue(),1));
        AbilityBuffs.TickActiveDrain(c,old);Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));
    }
    [Fact]public void EveryCurrentlyProducedDrainHasTheCheckedPureTerminationContract()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var found=new List<ushort>();
        foreach(var id in AbilityCatalog.All.Keys)
            if(AbilityScaledBuffs.Resolve(c,id,out var buff) && buff.DrainAmount>0)
            {found.Add(id);Assert.True(AbilityBuffs.PureDrainTermination(buff),"Unhandled draining definition "+id);}
        Assert.Equal(new ushort[]{13,16,442,941,943},found.OrderBy(x=>x));
    }
    [Theory][InlineData(941,7)][InlineData(943,6)]
    public void HealthChangeListenerDrainsStillChargeAndTerminateWithAllExistingOffRecords(ushort id,int cost)
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);Assert.True(AbilityBuffs.Apply(c,id));
        var active=c.playerData.activeBuffs[id];Assert.False(string.IsNullOrEmpty(active.Definition.HealthChangedProc));
        Assert.Equal(cost,active.Definition.DrainAmount);
        AbilityBuffs.TickActiveDrain(c,active);Assert.Equal(100-cost,AbilityHandler.CurrentInnerStrength(c));
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)0);c.messageQueue.RPCMessagesQueue.Clear();
        AbilityBuffs.TickActiveDrain(c,active);Assert.False(AbilityBuffs.IsActive(c,id));
        foreach(var record in AbilityBuffs.BuildRecords(id,active.Definition,active.RecordIds,true))
            Assert.Contains(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),m=>m.content.AsSpan(1).SequenceEqual(record));
        Assert.DoesNotContain(c.playerData.activeBuffs.Values,a=>ReferenceEquals(a,active));
    }
    [Fact]public void CallbackBearingDrainCannotSilentlyEnterThePureTerminationFamily()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var active=On(c);
        active.Definition.TermHeal=1;
        AbilityBuffs.TickActiveDrain(c,active);Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));Assert.True(active.Drain.Cancelled);
        active.Definition.TermHeal=0;
    }
    [Fact]public void ShortfallPreservesTheExisting442FifteenSecondTimerAndRecords()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);
        AbilityPassives.SetLoaded(c,new[]{((ushort)442,(ushort)50)},true);Assert.True(AbilityBuffs.Apply(c,442));
        var active=c.playerData.activeBuffs[442];c.messageQueue.RPCMessagesQueue.Clear();c.playerInstance.InnerStrengthAvailable.setValue((ushort)0);
        double now=TimeUtils.GetSimTimeSeconds();AbilityBuffs.TickActiveDrain(c,active);
        Assert.False(AbilityBuffs.IsActive(c,442));Assert.InRange(c.playerData.cooldownEndsAt[442],now+15,TimeUtils.GetSimTimeSeconds()+15);
        bool Has(byte[] payload)=>c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Any(m=>m.content.AsSpan(1).SequenceEqual(payload));
        Assert.True(Has(AbilityCooldowns.BuildTimer(442,AbilityCooldowns.OwnTimer,15,true)));
        Assert.True(Has(AbilityBuffs.BuildExclusive(442,false)));
        foreach(var record in AbilityBuffs.BuildRecords(442,active.Definition,active.RecordIds,true))Assert.True(Has(record));
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds()+15.1);Assert.True(Has(AbilityCooldowns.BuildTimer(442,AbilityCooldowns.OwnTimer,15,false)));
    }
    [Fact]public void CapturedCooldownExpiryCannotPublishIntoResetOwner()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var data=c.playerData;var player=c.playerInstance;long epoch=c.TransportFence.Capture();
        Assert.True(c.TransportFence.TryRun(epoch,()=>AbilityCooldowns.StartCaptured(c,442,TimeUtils.GetSimTimeSeconds(),data,player,data.worldGeneration,data.lifeGeneration,epoch)));
        c.TransportFence.PublishIncoming(true,()=>{});long next=c.TransportFence.Capture();
        c.TransportFence.ApplyReset(next,()=>{c.messageQueue.ForgetSentRpcs();c.messageQueue.ForgetWorldObjects();c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();});c.TransportFence.Resume(next);
        int before=c.messageQueue.RPCMessagesQueue.Count;DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds()+15.1);Assert.Equal(before,c.messageQueue.RPCMessagesQueue.Count);
        c.playerData=data;c.playerInstance=player;
    }
    [Fact]public void ScheduledOldCallbackAfterRelogAndNewOnOffDoesNotDrainTheNewLife()
    {
        var c=Client();using var owner=new BuffDrainTestOwner(c);var old=On(c);
        c.playerData.setOnWorld(false);var data=new ClientData();data.setOnWorld(true);data.setCharID(110);data.setDistrictId(1);c.playerData=data;
        var fresh=On(c);Assert.Equal(old.Generation,fresh.Generation);AbilityBuffs.Remove(c,16);
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds()+1.01);
        Assert.Equal(100,AbilityHandler.CurrentInnerStrength(c));Assert.False(AbilityBuffs.IsActive(c,16));Assert.True(old.Drain.Cancelled);Assert.True(fresh.Drain.Cancelled);
    }
}

