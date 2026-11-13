using hds;
using hds.shared;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class FreeCastViralLandingTests : IDisposable
{
    readonly Dictionary<string,WorldClient> oldClients=WorldServer.Clients;
    readonly ServerConfig oldConfig=Store.config;
    readonly WorldClient oldCurrent=Store.currentClient;
    readonly System.Collections.ArrayList oldMobs=WorldServer.mobs;
    readonly string[] keys={"MXO_DEV_COMMANDS","MXO_FREECAST_VIRAL_QA","MXO_FREECAST_VIRAL_QA_ATTACK15","MXO_FREECAST_VIRAL_QA_DEFENSE15","MXO_DB_NAME"};
    readonly string?[] oldEnv;
    public FreeCastViralLandingTests()
    {
        oldEnv=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach(var pair in keys.Zip(new[]{"1","1","0","32766","reality_hd"}))Environment.SetEnvironmentVariable(pair.First,pair.Second);
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_viral"});
        WorldServer.Clients=new();WorldServer.mobs=new();
    }
    public void Dispose()
    {
        foreach(var c in WorldServer.Clients.Values)AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        WorldServer.Clients=oldClients;Store.config=oldConfig;Store.currentClient=oldCurrent;WorldServer.mobs=oldMobs;
        for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],oldEnv[i]);
    }
    static WorldClient Client(uint id,int district=1)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new();c.playerData.setCharID(id);c.playerData.setEntityId(id);c.playerData.setDistrictId((uint)district);c.playerData.setOnWorld(true);c.playerData.setUDPSessionEstablished(true);
        c.playerInstance=new();c.messageQueue=new();c.viewMan=new();c.Alive=true;
        c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);c.playerInstance.Level.setValue((byte)50);
        var name=new byte[32];Encoding.ASCII.GetBytes("Viral"+id).CopyTo(name,0);c.playerInstance.CharacterName.setValue(name);
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        WorldServer.Clients[id.ToString()]=c;return c;
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void ActualQaDrawRejectsSpoofedDbAndLostFixtureOwnership(int mutation)
    {
        var c=Client(110);AbilityPassives.SetLoaded(c,new[]{((ushort)33,(ushort)50)});
        Assert.True(FreeCastViralContest.TryQaDraw(c,33,out int a,out int d));Assert.Equal(0,a);Assert.Equal(32766,d);
        if(mutation==0)typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="reality_hd"});
        if(mutation==1)AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        if(mutation==2)c.playerData.setUDPSessionEstablished(false);
        if(mutation==3)c.playerData.worldChangePending=true;
        if(mutation==4)c.playerData.setDistrictId((uint)MxOLocations.LA);
        if(mutation==5)c.Alive=false;
        if(mutation==6){var n=new byte[32];Encoding.ASCII.GetBytes("TheNeo").CopyTo(n,0);c.playerInstance.CharacterName.setValue(n);}
        Environment.SetEnvironmentVariable("MXO_DB_NAME","mxotest_spoof");
        Assert.False(FreeCastViralContest.TryQaDraw(c,33,out _,out _));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void MissingUncreatedOrDeletingPlayerViewProducesNoContestOrEffects(int state)
    {
        var c=Client(110);var victim=Client(111);Store.currentClient=c;
        c.playerData.pvpFlag=victim.playerData.pvpFlag=true;c.playerInstance.OrganizationID.setValue((byte)1);victim.playerInstance.OrganizationID.setValue((byte)2);
        AbilityPassives.SetLoaded(c,new[]{((ushort)33,(ushort)50)});
        if(state!=0){var view=c.viewMan.addView(111,1);view.viewCreated=state==2;view.viewNeedsToBeDeleted=state==2;}
        foreach(var client in WorldServer.Clients.Values){client.messageQueue.RPCMessagesQueue.Clear();client.messageQueue.ObjectMessagesQueue.Clear();}
        Assert.Equal(PvpRules.Verdict.Allowed,PvpCombat.Check(c,victim));
        var ability=new AbilityItem();ability.setAbilityID(33);
        Assert.False(FreeCastViralContest.Player(c,ability,victim));
        foreach(var client in WorldServer.Clients.Values){Assert.Empty(client.messageQueue.RPCMessagesQueue);Assert.Empty(client.messageQueue.ObjectMessagesQueue);}
        Assert.Equal(1000,PvpCombat.HealthOf(victim));
    }
    [Fact] public void ActualNpcLandingDeflectsBeforeDamageDebuffAndProcAndOnlyRoutesToVisibleSameWorld()
    {
        var caster=Client(110);Store.currentClient=caster;AbilityPassives.SetLoaded(caster,new[]{((ushort)33,(ushort)50)});
        var visible=Client(111);var hidden=Client(112);var foreign=Client(113,2);var deleting=Client(114);
        var mob=new Mob();mob.setEntityId(1000);mob.setMobId(1);mob.setDistrict(1);mob.setName("Target");mob.setHealthC(1000);mob.setHealthM(1000);mob.setLevel(50);WorldServer.mobs.Add(mob);
        foreach(var c in new[]{caster,visible,foreign,deleting}){var v=c.viewMan.addView(1000,1);v.viewCreated=true;if(c==deleting)v.viewNeedsToBeDeleted=true;}
        foreach(var c in WorldServer.Clients.Values){c.messageQueue.RPCMessagesQueue.Clear();c.messageQueue.ObjectMessagesQueue.Clear();}
        int state=mob.currentState;var ability=new AbilityItem();ability.setAbilityID(33);
        Assert.True(AbilityTargets.Land(caster,ability,caster.viewMan.FindViewForEntity(1000).ViewID,origin:AbilityCastOrigin.NormalFreeCast));
        Assert.Equal((ushort)1000,mob.getHealthC());Assert.Equal(state,mob.currentState);
        Assert.Equal(0,AbilityStats.Npc(mob,342));Assert.Equal(0,AbilityStats.Npc(mob,183));
        Assert.Single(caster.messageQueue.RPCMessagesQueue);Assert.Single(visible.messageQueue.RPCMessagesQueue);
        Assert.Single(caster.messageQueue.ObjectMessagesQueue);Assert.Single(visible.messageQueue.ObjectMessagesQueue);
        foreach(var c in new[]{hidden,foreign,deleting}){Assert.Empty(c.messageQueue.RPCMessagesQueue);Assert.Empty(c.messageQueue.ObjectMessagesQueue);}
    }
}
