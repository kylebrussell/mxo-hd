using hds;
using hds.shared;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class UiLagAreaTests : IDisposable
{
    readonly Dictionary<string,WorldClient> oldClients=WorldServer.Clients;
    readonly ServerConfig oldConfig=Store.config;
    readonly WorldServer oldWorld=Store.world;
    readonly WorldClient oldCurrent=Store.currentClient;
    readonly System.Collections.ArrayList oldMobs=WorldServer.mobs;
    readonly string[] keys={"MXO_DEV_COMMANDS","MXO_FREECAST_VIRAL_QA","MXO_FREECAST_VIRAL_QA_ATTACK15","MXO_FREECAST_VIRAL_QA_DEFENSE15","MXO_DB_NAME"};
    readonly string?[] oldEnv;
    public UiLagAreaTests()
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
        AbilityPassives.SetLoaded(c,new[]{((ushort)97,(ushort)50)});return c;
    }
    static AbilityItem Ability(ushort id=97){var a=new AbilityItem();a.setAbilityID(id);return a;}
    static bool Affected(WorldClient c)=>TimedEffects.ForPlayer(c).Effects.Snapshot().Any(m=>m.Parent==97);
    WorldClient Target(WorldClient actor,uint id,double x,double y=495)
    {
        var c=Client(id);At(c,x,y);c.playerData.pvpFlag=true;c.playerInstance.OrganizationID.setValue((byte)1);
        actor.viewMan.addView(id,1).viewCreated=true;return c;
    }
    [Fact] public void CapturedRecipientValuesStayDistinctAndTwentySevenSeconds()
    {
        Assert.True(TempMods.TryGet(97,out var effect));
        Assert.Equal(new ushort[]{1032,183,1011,342},effect.Mods.Select(m=>m.Target));
        Assert.Equal(new[]{-10,-17,-12,-14},effect.Mods.Select(m=>m.MinValue));
        Assert.All(effect.Mods,m=>Assert.Equal(27,m.Seconds));
    }
    [Theory] [InlineData(0)] [InlineData(7)] [InlineData(500)]
    public void SelectedViewNeverMovesCentreAndNearPlayersLandOnce(ushort selected)
    {
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15","32766");Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15","0");
        var c=Actor();c.playerData.currentSelectedTargetViewId=selected;
        var near=Target(c,111,299);var edge=Target(c,112,300);var far=Target(c,113,301);
        try
        {
            UiLagArea.Land(c,Ability());
            Assert.True(Affected(near));Assert.True(Affected(edge));Assert.False(Affected(far));Assert.False(Affected(c));
            Assert.Equal(4,TimedEffects.ForPlayer(near).Effects.Snapshot().Count(m=>m.Parent==97));
            Assert.Equal(1000,PvpCombat.HealthOf(near));Assert.Equal(0,AbilityStats.Player(far,342));
        }
        finally {foreach(var p in new[]{near,edge,far})TimedEffects.Clear(TimedEffects.ForPlayer(p));}
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void OrdinaryRecipientAuthorityStaysInForce(int mutation)
    {
        var c=Actor();var t=Target(c,111,100);
        if(mutation==0)t.playerData.setDistrictId(2);
        if(mutation==1)At(t,100,645);
        if(mutation==2)t.playerData.isDead=true;
        if(mutation==3)t.playerInstance.OrganizationID.setValue((byte)2);
        if(mutation==4)t.playerData.pvpFlag=false;
        if(mutation==5)c.viewMan=new();
        UiLagArea.Land(c,Ability());Assert.False(Affected(t));Assert.Empty(t.messageQueue.RPCMessagesQueue);
    }
    [Fact] public void DeflectionDoesNotApplyAnyTimedModifierOrDamage()
    {
        var c=Actor();var t=Target(c,111,100);UiLagArea.Land(c,Ability());
        Assert.False(Affected(t));Assert.Equal(1000,PvpCombat.HealthOf(t));Assert.Equal(0,AbilityStats.Player(t,342));
        Assert.Single(t.messageQueue.RPCMessagesQueue); // actual viral result, not a tempmod
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void NpcRecipientUsesSameContestBeforeExistingDebuffs(bool hit)
    {
        var c=Actor();Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15",hit?"32766":"0");Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15",hit?"0":"32766");
        var mob=new Mob();mob.setEntityId(1000);mob.setMobId(1);mob.setDistrict(1);mob.setName("Nearby");mob.setHealthC(1000);mob.setHealthM(1000);mob.setLevel(50);mob.setXPos(200);mob.setYPos(495);mob.setZPos(0);WorldServer.mobs.Add(mob);
        c.viewMan.addView(1000,1).viewCreated=true;
        try
        {
            UiLagArea.Land(c,Ability());
            Assert.Equal(hit,TimedEffects.ForMob(mob).Effects.Snapshot().Any(m=>m.Parent==97));
            Assert.Equal((ushort)1000,mob.getHealthC());
            Assert.Equal(hit?-14:0,AbilityStats.Npc(mob,342));
        }
        finally {TimedEffects.Clear(TimedEffects.ForMob(mob));}
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void NpcFriendlyDeadForeignHiddenPetAndOtherFloorStayExcluded(int mutation)
    {
        var c=Actor();var mob=new Mob();mob.setEntityId(1000);mob.setMobId(1);mob.setDistrict(1);mob.setName("Excluded");mob.setHealthC(1000);mob.setHealthM(1000);mob.setLevel(50);mob.setXPos(200);mob.setYPos(495);WorldServer.mobs.Add(mob);
        if(mutation!=3)c.viewMan.addView(1000,1).viewCreated=true;
        if(mutation==0)mob.isFriendly=true;
        if(mutation==1)mob.setIsDead(true);
        if(mutation==2)mob.setDistrict(2);
        if(mutation==4)mob.pet=(CoderPets.Pet)RuntimeHelpers.GetUninitializedObject(typeof(CoderPets.Pet));
        if(mutation==5)mob.setYPos(645);
        c.messageQueue.RPCMessagesQueue.Clear();UiLagArea.Land(c,Ability());
        Assert.Empty(c.messageQueue.RPCMessagesQueue);Assert.Empty(TimedEffects.ForMob(mob).Effects.Snapshot());
    }
    [Theory] [InlineData(38)] [InlineData(47)] [InlineData(694)] [InlineData(1025)] [InlineData(1109)]
    public void NoOtherSelectorFourFamilyIsEnabled(ushort id)
    {
        var c=Actor();var t=Target(c,111,100);UiLagArea.Land(c,Ability(id));
        Assert.False(Affected(t));Assert.False(FreeCastViralContest.Applies(id,AbilityCastOrigin.NormalFreeCast) && id!=97);
    }
}
