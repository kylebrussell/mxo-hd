using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class ConcentrationDamageQaTests : IDisposable
{
    private readonly Dictionary<string,WorldClient>? oldClients=WorldServer.Clients;
    private readonly WorldClient oldCurrent=Store.currentClient;
    private readonly ServerConfig oldConfig=Store.config;
    private readonly string[] keys={"MXO_DEV_COMMANDS","MXO_CONCENTRATION_QA_DAMAGE","MXO_CAST_INTERRUPT_QA_ROLL","MXO_DB_NAME"};
    private readonly string?[] oldEnvironment;
    public ConcentrationDamageQaTests()
    {
        oldEnvironment=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        Environment.SetEnvironmentVariable(keys[0],"1");Environment.SetEnvironmentVariable(keys[1],"1");
        Environment.SetEnvironmentVariable(keys[2],"85");Environment.SetEnvironmentVariable(keys[3],"mxotest_interrupt");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!
            .SetValue(Store.config,new DbParams{DatabaseName="mxotest_interrupt"});WorldServer.Clients=new();
    }
    public void Dispose()
    {
        WorldServer.Clients=oldClients!;Store.currentClient=oldCurrent;Store.config=oldConfig;
        for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],oldEnvironment[i]);
    }
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerData.setCharID(110);c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);c.playerData.setUDPSessionEstablished(true);
        c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        c.playerInstance.Level.setValue((byte)50);var name=new byte[32];Encoding.ASCII.GetBytes("QaInterrupt").CopyTo(name,0);c.playerInstance.CharacterName.setValue(name);
        c.Alive=true;typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        WorldServer.Clients["fixture"]=c;Store.currentClient=c;return c;
    }
    private static AbilityCastInterrupts.Cast Cast(WorldClient c,ushort id=360)
    {
        AbilityCooldowns.StartCast(c.playerData,TimeUtils.GetSimTimeSeconds(),4);
        return AbilityCastInterrupts.Begin(c,id,4);
    }
    private static void Fire()=>DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds()+1.3);
    private static string Arm(WorldClient c,string amount="100")=>CombatDepthDev.Run(c,new[]{"hurtme",amount,"next"});
    [Theory]
    [InlineData("1","mxotest_native","1",110u,true,false,"CSR",true)]
    [InlineData("true","mxotest_native","1",110u,true,false,"CSR",false)]
    [InlineData("1","reality_hd","1",110u,true,false,"CSR",false)]
    [InlineData("1","mxotest_","1",110u,true,false,"CSR",false)]
    [InlineData("1","mxotest_bad-name","1",110u,true,false,"CSR",false)]
    [InlineData("1","mxotest_native","0",110u,true,false,"CSR",false)]
    [InlineData("1","mxotest_native","1",0u,true,false,"CSR",false)]
    [InlineData("1","mxotest_native","1",2u,true,false,"CSR",false)]
    [InlineData("1","mxotest_native","1",110u,true,false,"TheNeo",false)]
    [InlineData("1","mxotest_native","1",110u,false,false,"CSR",false)]
    [InlineData("1","mxotest_native","1",110u,true,true,"CSR",false)]
    public void StrictDisposableModeAndProtectedIdentityGuards(string dev,string db,string enabled,uint id,bool world,bool dead,string name,bool expected)=>
        Assert.Equal(expected,ConcentrationDamageQa.Allowed(dev,db,enabled,id,world,dead,name));
    [Theory]
    [InlineData("0")] [InlineData("-1")] [InlineData("501")] [InlineData("1000")] [InlineData("1.5")] [InlineData("bad")]
    public void DamageInputIsBoundedIntegerAndNeverLethal(string amount)
    {
        var c=Client();Assert.DoesNotContain("armed:",Arm(c,amount));Cast(c);Fire();Assert.Equal(1000,PvpCombat.HealthOf(c));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ArmWaitsForCastThenRealDamageFiresOnceThroughNormalInterruptPath(bool loaded)
    {
        var c=Client();if(loaded)AbilityPassives.SetLoaded(c,new[]{((ushort)628,(ushort)50)});
        Assert.Contains("armed:",Arm(c));Fire();Assert.Equal(1000,PvpCombat.HealthOf(c));
        var cast=Cast(c);Fire();Assert.Equal(900,PvpCombat.HealthOf(c));Fire();Assert.Equal(900,PvpCombat.HealthOf(c));
        Assert.Equal(loaded,AbilityCastInterrupts.Complete(cast,()=>{}));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void BoundDamageRefusesLostAuthWorldLifeOwnershipOrNonlethalBudget(int mutation)
    {
        var c=Client();Assert.Contains("armed:",Arm(c));var cast=Cast(c);
        if(mutation==0)c.playerData.worldGeneration++;
        if(mutation==1)c.playerData.lifeGeneration++;
        if(mutation==2)c.playerData.setUDPSessionEstablished(false);
        if(mutation==3)c.playerData.worldChangePending=true;
        if(mutation==4)c.playerData.setDistrictId((int)MxOLocations.LA);
        if(mutation==5)WorldServer.Clients.Clear();
        if(mutation==6)Environment.SetEnvironmentVariable("MXO_CONCENTRATION_QA_DAMAGE","0");
        if(mutation==7)c.playerInstance.Health.setValue((ushort)100);
        if(mutation==8)c.playerData.setDistrictId(2);
        if(mutation==9){c.playerData=new ClientData();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerData.setUDPSessionEstablished(true);}
        Fire();Assert.Equal(mutation==7?100:1000,PvpCombat.HealthOf(c));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ArmCannotFollowChangedContextIntoANewEligibleCast(int mutation)
    {
        var c=Client();Assert.Contains("armed:",Arm(c));
        if(mutation==0)c.playerData.worldGeneration++;
        if(mutation==1)c.playerData.lifeGeneration++;
        if(mutation==2)c.playerData.setCharID(111);
        Cast(c);Fire();Assert.Equal(1000,PvpCombat.HealthOf(c));
    }
    [Fact]
    public void CancelledOrReplacedCastCannotDamageTheReplacement()
    {
        var c=Client();Assert.Contains("armed:",Arm(c));Cast(c);
        PvpCombat.Damage(c,10);var replacement=Cast(c);Fire();Assert.Equal(990,PvpCombat.HealthOf(c));
        Assert.True(AbilityCastInterrupts.Complete(replacement,()=>{}));
        c.playerData.castEndsAt=-1;Assert.Contains("armed:",Arm(c));Cast(c);var newer=Cast(c);Fire();
        Assert.Equal(990,PvpCombat.HealthOf(c));Assert.True(AbilityCastInterrupts.Complete(newer,()=>{}));
    }
    [Fact]
    public void RequestRequiresCurrentAuthenticatedLiveRegisteredActorAndCannotTargetAnotherCharacter()
    {
        var c=Client();Store.currentClient=null!;Assert.DoesNotContain("armed:",Arm(c));Store.currentClient=c;
        c.playerData.setUDPSessionEstablished(false);Assert.DoesNotContain("armed:",Arm(c));c.playerData.setUDPSessionEstablished(true);
        c.Alive=false;Assert.DoesNotContain("armed:",Arm(c));c.Alive=true;
        Assert.DoesNotContain("armed:",CombatDepthDev.Run(c,new[]{"hurtme","100","next","6114"}));
        Assert.DoesNotContain("armed:",CombatDepthDev.Run(c,new[]{"hurtme","100"}));
        c.playerInstance.Health.setValue((ushort)100);Assert.DoesNotContain("armed:",Arm(c));
    }
    [Fact]
    public void CompletedCastCannotReuseConsumedArmAndUnrelatedCastDoesNotConsumeItEarly()
    {
        var c=Client();AbilityPassives.SetLoaded(c,new[]{((ushort)628,(ushort)50)});
        Assert.Contains("armed:",Arm(c));var unrelated=Cast(c,77);Fire();Assert.Equal(1000,PvpCombat.HealthOf(c));
        Assert.True(AbilityCastInterrupts.Complete(unrelated,()=>{}));var eligible=Cast(c);Fire();Assert.Equal(900,PvpCombat.HealthOf(c));
        Assert.True(AbilityCastInterrupts.Complete(eligible,()=>{}));Cast(c);Fire();Assert.Equal(900,PvpCombat.HealthOf(c));
    }

    [Fact]
    public void EnvironmentCannotSpoofActualForeignDatabaseAndModeGuardIsRechecked()
    {
        var c=Client();Store.config.dbParams.DatabaseName="reality_hd";
        Assert.DoesNotContain("armed:",Arm(c));Store.config.dbParams.DatabaseName="mxotest_interrupt";
        Environment.SetEnvironmentVariable("MXO_CONCENTRATION_QA_DAMAGE","");Assert.DoesNotContain("armed:",Arm(c));
        Environment.SetEnvironmentVariable("MXO_CONCENTRATION_QA_DAMAGE","1");Assert.Contains("armed:",Arm(c));
        Cast(c);Store.config.dbParams.DatabaseName="reality_hd";Fire();Assert.Equal(1000,PvpCombat.HealthOf(c));
    }

}
