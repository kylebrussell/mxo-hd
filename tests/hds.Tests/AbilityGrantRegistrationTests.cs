using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
public class AbilityGrantRegistrationTests
{
    static byte[] Hex(string s)=>Convert.FromHexString(s);
    static readonly AbilityGrantRegistration.Relation Grant=new(356,1087,0);
    static readonly AbilityGrantRegistration.Stamp Initial=new(1,0);
    [Fact] public void CapturedHowitzerLoadGrantAndRemovalAreLiteral()
    {
        Assert.Equal(Hex("80b23f0400000802"),ServerPackets.BuildAbilityLoad(1087,0,520));
        Assert.Equal(Hex("80bc45033f0400640100003f04000000000000000000"),AbilityGrantRegistration.Record(Grant,false));
        Assert.Equal(Hex("80bc45033f0400640100003f04000000000100000000"),AbilityGrantRegistration.Record(Grant,true));
    }
    [Fact] public void OnlyReviewedRelationChangesAndLegacyLevelsRemainExact()
    {
        Assert.True(AbilityGrantRegistration.Handles(356,1087));
        foreach(ushort parent in new ushort[]{2,37,277})foreach(ushort child in AbilityProgression.GrantsOf(parent))Assert.False(AbilityGrantRegistration.Handles(parent,child));
        Assert.Equal(Hex("80b2110032000802"),ServerPackets.BuildAbilityLoad(17,AbilityProgression.GrantLevel(2,50),520));
        Assert.Equal(Hex("80b2ca0300000802"),ServerPackets.BuildAbilityLoad(970,AbilityProgression.GrantLevel(37,50),520));
        Assert.Equal(Hex("80b2c90300000802"),ServerPackets.BuildAbilityLoad(969,AbilityProgression.GrantLevel(277,50),520));
    }
    [Fact] public void ReadinessDuplicatesUpgradeAndNewWorldAreIdempotent()
    {
        var state=new AbilityGrantRegistration.Presentation();
        Assert.Empty(state.Reconcile(Initial,false,new[]{Grant}));
        Assert.Equal(2,state.Reconcile(Initial,true,new[]{Grant}).Count);
        Assert.Empty(state.Reconcile(Initial,true,new[]{Grant}));
        Assert.Empty(state.Reconcile(Initial,false,new[]{Grant})); //356 upgrade leaves captured grantlevel0.
        var next=new AbilityGrantRegistration.Stamp(2,1);
        Assert.Empty(state.Reconcile(next,false,new[]{Grant}));
        Assert.Equal(2,state.Reconcile(next,true,new[]{Grant}).Count);
        var removed=state.Reconcile(next,false,Array.Empty<AbilityGrantRegistration.Relation>());
        Assert.Equal(2,removed.Count);Assert.Equal(Hex("80b33f04"),removed[0]);Assert.Equal(AbilityGrantRegistration.Record(Grant,true),removed[1]);
        Assert.Empty(state.Reconcile(next,true,Array.Empty<AbilityGrantRegistration.Relation>()));
        Assert.Equal(2,state.Reconcile(next,false,new[]{Grant}).Count);
    }
    [Fact] public void SharedSourceRetainsChildAndSourceSwapRemovesBeforeAdding()
    {
        //Pure ownership planner only; source999 is NOT admitted by production Handles.
        var second=new AbilityGrantRegistration.Relation(999,1087,0);var state=new AbilityGrantRegistration.Presentation();
        state.Reconcile(Initial,true,new[]{Grant});
        Assert.Empty(state.Reconcile(Initial,false,new[]{Grant,second}));
        Assert.Empty(state.Reconcile(Initial,false,new[]{Grant}));
        var swap=state.Reconcile(Initial,false,new[]{second});
        Assert.Equal(3,swap.Count);Assert.Equal(AbilityGrantRegistration.Record(Grant,true),swap[0]);
        Assert.Equal(ServerPackets.BuildAbilityLoad(1087,0,520),swap[1]);Assert.Equal(AbilityGrantRegistration.Record(second,false),swap[2]);
        Assert.DoesNotContain(swap,b=>b.SequenceEqual(Hex("80b33f04")));
    }
    static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();
        c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerData.passives.Loaded[356]=50;c.messageQueue=new();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    [Fact] public void ActualReadyHandlerRejectsMalformedPendingLaAndStaleGenerations()
    {
        var c=Client();AbilityGrantRegistration.LoadedChanged(c);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        foreach(var body in new[]{Array.Empty<byte>(),new byte[]{0},new byte[]{1,0}})AbilityGrantRegistration.LoadingComplete(c,body);
        Assert.Empty(c.messageQueue.RPCMessagesQueue);
        var stale=new AbilityGrantRegistration.Stamp(c.playerData.worldGeneration,c.TransportFence.Capture());
        c.playerData.setOnWorld(false);c.playerData.setOnWorld(true);
        AbilityGrantRegistration.Apply(c,true,stale);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        var current=new AbilityGrantRegistration.Stamp(c.playerData.worldGeneration,c.TransportFence.Capture());
        c.TransportFence.PublishIncoming(true,()=>{});AbilityGrantRegistration.Apply(c,true,current);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        c.TransportFence.Resume(c.TransportFence.Capture());
        c.playerData.worldChangePending=true;AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Empty(c.messageQueue.RPCMessagesQueue);c.playerData.worldChangePending=false;
        c.playerData.setDistrictId((uint)MxOLocations.LA);AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Empty(c.messageQueue.RPCMessagesQueue);c.playerData.setDistrictId(1);
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(2,c.messageQueue.RPCMessagesQueue.Count);
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(2,c.messageQueue.RPCMessagesQueue.Count);
        c.playerData.passives.Loaded.Remove(356);AbilityGrantRegistration.LoadedChanged(c);Assert.Equal(4,c.messageQueue.RPCMessagesQueue.Count);
    }
}
