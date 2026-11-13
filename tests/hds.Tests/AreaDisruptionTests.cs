using hds;
using hds.shared;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class AreaDisruptionTests : IDisposable
{
    readonly Dictionary<string,WorldClient> oldClients=WorldServer.Clients;
    readonly ServerConfig oldConfig=Store.config;
    readonly WorldServer oldWorld=Store.world;
    readonly WorldClient oldCurrent=Store.currentClient;
    readonly System.Collections.ArrayList oldMobs=WorldServer.mobs;
    readonly string[] keys={"MXO_DEV_COMMANDS","MXO_FREECAST_VIRAL_QA","MXO_FREECAST_VIRAL_QA_ATTACK15","MXO_FREECAST_VIRAL_QA_DEFENSE15","MXO_DB_NAME"};
    readonly string?[] oldEnv;
    public AreaDisruptionTests()
    {
        oldEnv=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach(var pair in keys.Zip(new[]{"1","1","0","32766","reality_hd"}))Environment.SetEnvironmentVariable(pair.First,pair.Second);
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_viral"});
        WorldServer.Clients=new();WorldServer.mobs=new();Store.world=(WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
    }
    public void Dispose()
    {
        foreach(var c in WorldServer.Clients.Values)AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        WorldServer.Clients=oldClients;Store.config=oldConfig;Store.currentClient=oldCurrent;WorldServer.mobs=oldMobs;Store.world=oldWorld;
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
    static void At(WorldClient c,double x,double y=495)=>c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x,y,0));
    WorldClient Actor()
    {
        var c=Client(110);Store.currentClient=c;At(c,0);c.playerData.pvpFlag=true;c.playerInstance.OrganizationID.setValue((byte)2);
        AbilityPassives.SetLoaded(c,new[]{((ushort)38,(ushort)50)});return c;
    }
    static AbilityItem Ability(ushort id=38){var a=new AbilityItem();a.setAbilityID(id);return a;}
    static bool Affected(WorldClient c)=>TimedEffects.ForPlayer(c).Effects.Snapshot().Any(m=>m.Parent==38);
    WorldClient Target(WorldClient actor,uint id,double x,double y=495)
    {
        var c=Client(id);At(c,x,y);c.playerData.pvpFlag=true;c.playerInstance.OrganizationID.setValue((byte)1);
        actor.viewMan.addView(id,1).viewCreated=true;return c;
    }
    [Fact] public void SourceControlValuesAndStrictApplicabilityRemainExact()
    {
        Assert.True(CasterHostileArea.Handles(38));Assert.True(CasterHostileArea.Handles(97));
        foreach(ushort id in new ushort[]{47,694,1025,1109}){Assert.False(CasterHostileArea.Handles(id));Assert.False(FreeCastViralContest.Applies(id,AbilityCastOrigin.NormalFreeCast));}
        Assert.True(TempMods.TryGet(38,out var effect));var mod=Assert.Single(effect.Mods);Assert.Equal((ushort)102,mod.Target);Assert.Equal(3f,mod.Seconds);Assert.Equal(8f,effect.ImmunitySeconds);
        var row=AbilityCatalog.Find(38);Assert.Equal(14,row.Number("Ability/AbilityInnerStrengthCost"));Assert.Equal(43,row.Number("Ability/RecastTime"));Assert.Equal(2,row.Number("Ability/CastingTime"));
        Assert.False(AbilityCastInterrupts.KnownInterruptible(38));Assert.False(AbilityTargets.Retail.ContainsKey(38));
    }
    [Theory][InlineData(0)][InlineData(7)][InlineData(500)]
    public void CasterEightMetreBoundaryIgnoresSelectionAndKeepsZeroDamage(ushort selected)
    {
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15","32766");Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15","0");
        var c=Actor();c.playerData.currentSelectedTargetViewId=selected;var near=Target(c,111,799);var edge=Target(c,112,800);var far=Target(c,113,801);var floor=Target(c,114,100,645);
        try{CasterHostileArea.Land(c,Ability());Assert.True(Affected(near));Assert.True(Affected(edge));Assert.False(Affected(far));Assert.False(Affected(floor));Assert.False(Affected(c));Assert.Equal(1000,PvpCombat.HealthOf(near));Assert.Single(TimedEffects.ForPlayer(near).Effects.Snapshot().Where(m=>m.Parent==38));}
        finally{foreach(var p in new[]{near,edge,far,floor})TimedEffects.Clear(TimedEffects.ForPlayer(p));}
    }
    [Fact]public void DeflectNeverAppliesControlOrDamage()
    {var c=Actor();var t=Target(c,111,100);CasterHostileArea.Land(c,Ability());Assert.False(Affected(t));Assert.Equal(1000,PvpCombat.HealthOf(t));Assert.Single(t.messageQueue.RPCMessagesQueue);}
    [Theory][InlineData(false)][InlineData(true)]
    public void OrdinaryNpcLandingUsesOneContestAndSourceControl(bool hit)
    {
        var c=Actor();Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15",hit?"32766":"0");Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15",hit?"0":"32766");
        var m=new Mob();m.setEntityId(1000);m.setMobId(1);m.setDistrict(1);m.setName("AreaNpc");m.setHealthC(1000);m.setHealthM(1000);m.setLevel(50);m.setXPos(750);m.setYPos(495);WorldServer.mobs.Add(m);c.viewMan.addView(1000,1).viewCreated=true;
        try{CasterHostileArea.Land(c,Ability());Assert.Equal(hit,TimedEffects.ForMob(m).Effects.Snapshot().Any(e=>e.Parent==38));Assert.Equal((ushort)1000,m.getHealthC());}
        finally{TimedEffects.Clear(TimedEffects.ForMob(m));}
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ActualEnumerationBarrierRejectsAnInvalidatedCasterSnapshot(int mutation)
    {
        var c=Actor();var t=Target(c,111,100);Exception? error=null;
        var worker=new Thread(()=>{try{CasterHostileArea.Land(c,Ability());}catch(Exception ex){error=ex;}});
        lock(WorldServer.mobs.SyncRoot)
        {
            worker.Start();
            Assert.True(SpinWait.SpinUntil(()=>worker.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin),TimeSpan.FromSeconds(3)),"Actual MobsInArea waits on the held registry after capturing the centre");
            if(mutation==0)c.playerData.worldGeneration++;
            if(mutation==1)c.playerData.lifeGeneration++;
            if(mutation==2)c.Alive=false;
            if(mutation==3)c.playerData.worldChangePending=true;
        }
        Assert.True(worker.Join(TimeSpan.FromSeconds(3)));Assert.Null(error);
        Assert.False(Affected(t));Assert.Empty(c.messageQueue.RPCMessagesQueue);Assert.Empty(t.messageQueue.RPCMessagesQueue);
    }
}
