using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;

[CollectionDefinition("ConcentrationEnvironment",DisableParallelization=true)]
public class ConcentrationEnvironmentCollection {}
[Collection("ConcentrationEnvironment")]
public class AbilityCastInterruptTests : IDisposable
{
    private readonly Dictionary<string,WorldClient>? previous=WorldServer.Clients;
    public AbilityCastInterruptTests(){WorldServer.Clients=new();}
    public void Dispose(){WorldServer.Clients=previous!;}
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)500);c.playerInstance.InnerStrengthMax.setValue((ushort)500);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }
    private static AbilityCastInterrupts.Cast Begin(WorldClient c,ushort id=360)
    {
        AbilityCooldowns.StartCast(c.playerData,TimeUtils.GetSimTimeSeconds(),4);
        return AbilityCastInterrupts.Begin(c,id,4);
    }
    private sealed class Roll85:Random {public override int Next(int minimum,int maximum)=>85;}
    [Theory]
    [InlineData(1,1000,0,75)]
    [InlineData(999,1000,0,75)]
    [InlineData(1000,1000,0,33)]
    [InlineData(1,1000,30,90)]
    [InlineData(1,1000,-100,33)]
    public void LiteralPython2IntegerQuotientAndBounds(int damage,ushort maximum,short concentration,int expected) =>
        Assert.Equal(expected,AbilityCastInterrupts.CastThroughPercent(damage,maximum,concentration));
    [Fact]
    public void NativePacketIsSevenBytesWithCanonicalIdAndDamageReasonTwo()=>
        Assert.Equal(new byte[]{0x80,0xad,0x68,1,0,0,2},AbilityCastInterrupts.CancelPacket(360,2));
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadedConcentrateChangesActualDamageOutcomeAtSame85Roll(bool loaded)
    {
        var c=Client();if(loaded)AbilityPassives.SetLoaded(c,new[]{((ushort)628,(ushort)50)});
        Assert.Equal(loaded?30:0,AbilityStats.Influence(c,564));
        var cast=Begin(c);AbilityHandler.SpendInnerStrength(c,40);ushort paid=AbilityHandler.CurrentInnerStrength(c);
        AbilityCastInterrupts.OnDamage(c,80,new Roll85());
        int effects=0;bool completed=AbilityCastInterrupts.Complete(cast,()=>effects++);
        Assert.Equal(loaded,completed);Assert.Equal(loaded?1:0,effects);
        Assert.Equal(paid,AbilityHandler.CurrentInnerStrength(c));
        Assert.Equal(!loaded,c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Any(m=>m.content.Skip(1).SequenceEqual(AbilityCastInterrupts.CancelPacket(360,2))));
    }
    [Theory]
    [InlineData(77)]
    [InlineData(19)]
    public void AbsentOrExplicitFalseFlagDoesNotInventEligibility(ushort id)
    {
        var c=Client();var cast=Begin(c,id);AbilityCastInterrupts.OnDamage(c,80,new Roll85());Assert.True(AbilityCastInterrupts.Complete(cast,()=>{}));
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void ZeroOrHealingNeverInterrupts(int delta)
    {
        var c=Client();var cast=Begin(c);AbilityCastInterrupts.OnDamage(c,delta,new Roll85());Assert.True(AbilityCastInterrupts.Complete(cast,()=>{}));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void StaleOrIneligibleNormalCompletionHasNoEffectsOrCooldowns(int change)
    {
        var c=Client();var cast=Begin(c);int effects=0;
        if(change==0){c.playerData.worldGeneration++;}
        if(change==1){c.playerData.lifeGeneration++;}
        if(change==2){c.playerData.setOnWorld(false);c.playerData.setOnWorld(true);}
        if(change==3){c.playerData.worldChangePending=true;}
        if(change==4){c.playerData.isDead=true;}
        if(change==5){c.playerInstance.Health.setValue((ushort)0);}
        Assert.False(AbilityCastInterrupts.Complete(cast,()=>{effects++;AbilityCooldowns.Start(c,360,TimeUtils.GetSimTimeSeconds());}));
        Assert.Equal(0,effects);Assert.False(AbilityCooldowns.IsCoolingDown(c.playerData,360,TimeUtils.GetSimTimeSeconds()));
    }
    [Fact]
    public void DamageCancellationAndReplacementDoNotCommitOrReleaseAnotherReservation()
    {
        var c=Client();var old=Begin(c);AbilityCastInterrupts.OnDamage(c,80,new Roll85());
        double replacement=TimeUtils.GetSimTimeSeconds()+20;c.playerData.castEndsAt=replacement;
        int consumed=0;Assert.False(AbilityCastInterrupts.Complete(old,()=>consumed++));
        Assert.Equal(0,consumed);Assert.Equal(replacement,c.playerData.castEndsAt);
        var next=AbilityCastInterrupts.Begin(c,77,20);Assert.False(AbilityCastInterrupts.Complete(old,()=>consumed++));
        Assert.True(AbilityCastInterrupts.Complete(next,()=>consumed++));Assert.Equal(1,consumed);
    }
    [Fact]
    public void OneAtomicCompletionCannotBeCommittedTwice()
    {
        var c=Client();var cast=Begin(c);int effects=0;
        Assert.True(AbilityCastInterrupts.Complete(cast,()=>{effects++;AbilityCastInterrupts.OnDamage(c,1,new Roll85());effects++;}));
        Assert.False(AbilityCastInterrupts.Complete(cast,()=>effects++));Assert.Equal(2,effects);
    }
    [Fact]
    public void UnqualifiedPositiveHealthLossDoesNotBecomeDamage()
    {
        var c=Client();var cast=Begin(c);TimedEffects.HealthChanged(TimedEffects.ForPlayer(c),100,true);
        Assert.True(AbilityCastInterrupts.Complete(cast,()=>{}));
    }

    private static void EnvironmentCase(string? dev,string? database,string? roll,Action action)
    {
        string[] keys={"MXO_DEV_COMMANDS","MXO_DB_NAME","MXO_CAST_INTERRUPT_QA_ROLL"};var old=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        try {Environment.SetEnvironmentVariable(keys[0],dev);Environment.SetEnvironmentVariable(keys[1],database);Environment.SetEnvironmentVariable(keys[2],roll);action();}
        finally {for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],old[i]);}
    }
    [Theory]
    [InlineData("0","mxotest_cast","85")] [InlineData("true","mxotest_cast","85")]
    [InlineData("1","reality_hd","85")] [InlineData("1","MXOTEST_cast","85")]
    [InlineData("1","mxotest_cast","0")] [InlineData("1","mxotest_cast","-1")]
    [InlineData("1","mxotest_cast","100")] [InlineData("1","mxotest_cast","bad")]
    [InlineData("1","mxotest_cast",null)]
    public void QaRollRequiresExactDisposableGuardAndOneThroughNinetyNine(string? dev,string? database,string? roll)=>
        EnvironmentCase(dev,database,roll,()=>Assert.Null(AbilityCastInterrupts.QaInterruptRoll()));
    [Fact]
    public void QualifiedPvpDamageCancelsButHpBoostClampAndUnqualifiedWritesDoNot()
    {
        EnvironmentCase("1","mxotest_cast","85",()=>
        {
            var damage=Client();var cast=Begin(damage);PvpCombat.Damage(damage,80);
            Assert.False(AbilityCastInterrupts.Complete(cast,()=>throw new Exception("cancelled landing")));
            var clamp=Client();Assert.True(AbilityBuffs.Apply(clamp,1064));
            clamp.playerInstance.Health.setValue((ushort)1100);var pending=Begin(clamp);
            AbilityBuffs.Remove(clamp,1064);
            Assert.Equal(1000,PvpCombat.HealthOf(clamp));Assert.True(AbilityCastInterrupts.Complete(pending,()=>{}));
            var administrative=Client();var adminCast=Begin(administrative);
            administrative.playerInstance.Health.setValue((ushort)900);
            TimedEffects.HealthChanged(TimedEffects.ForPlayer(administrative),100,true);
            Assert.True(AbilityCastInterrupts.Complete(adminCast,()=>{}));
        });
    }
    [Fact]
    public void ReplacementItemStampAndReservationAreNotReleasedByAnOldNormalCast()
    {
        var c=Client();var old=Begin(c);double replacement=TimeUtils.GetSimTimeSeconds()+20;
        c.playerData.castEndsAt=replacement;c.playerData.cooldownEndsAt[9000]=replacement+300;
        AbilityCastInterrupts.OnDamage(c,80,new Roll85());
        Assert.Equal(replacement,c.playerData.castEndsAt);Assert.Equal(replacement+300,c.playerData.cooldownEndsAt[9000]);
        Assert.False(AbilityCastInterrupts.Complete(old,()=>throw new Exception("stale consumption")));
        Assert.Equal(replacement,c.playerData.castEndsAt);Assert.Equal(replacement+300,c.playerData.cooldownEndsAt[9000]);
    }
    [Fact]
    public void ActivePowerlessPreventsCompletionWithoutInventingADamageRoll()
    {
        var c=Client();var cast=Begin(c);
        TimedEffects.ForPlayer(c).Effects.Apply(999,new[]{new TimedEffects.Active{Proc=TempMods.Proc.State,Target=TempMods.PowerlessAbility,Seconds=30}},TimeUtils.GetSimTimeSeconds());
        Assert.False(AbilityCastInterrupts.Complete(cast,()=>throw new Exception("powerless landing")));
    }

    [Fact]
    public void RejectedSecondRequestCannotRedirectOrReplacePendingHeal()
    {
        var singleton=typeof(DataLoader).GetField("Instance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        var previousLoader=singleton.GetValue(null);var previousClient=Store.currentClient;
        var loader=(DataLoader)RuntimeHelpers.GetUninitializedObject(typeof(DataLoader));
        AbilityItem Item(ushort id){var a=new AbilityItem();a.setAbilityID(id);a.setAbilityName("fixture"+id);a.setCastAnimStart(new byte[4]);return a;}
        loader.AbilityDB=new(){Item(77),Item(360)};singleton.SetValue(null,loader);
        try
        {
            var c=Client();c.viewMan=new ViewManager();c.playerInstance.Health.setValue((ushort)100);Store.currentClient=c;
            var handler=new AbilityHandler();byte[] original={77,0,2,0};handler.ProcessAbility(ref original);
            double originalEnd=c.playerData.castEndsAt;ushort paid=AbilityHandler.CurrentInnerStrength(c);
            Assert.True(originalEnd>TimeUtils.GetSimTimeSeconds());
            byte[] rejected={0x68,1,0xe7,3};handler.ProcessAbility(ref rejected);
            Assert.Equal((ushort)360,handler.currentAbility.getAbilityID());
            Assert.Equal(originalEnd,c.playerData.castEndsAt);Assert.Equal(paid,AbilityHandler.CurrentInnerStrength(c));
            // Mutating the public legacy field also cannot affect the method-local target captured by the callback.
            handler.currentTargetViewId=999;
            DelayedActions.RunDue(originalEnd+1);
            Assert.True(PvpCombat.HealthOf(c)>100);
            Assert.True(AbilityCooldowns.IsCoolingDown(c.playerData,77,TimeUtils.GetSimTimeSeconds()));
            Assert.False(AbilityCooldowns.IsCoolingDown(c.playerData,360,TimeUtils.GetSimTimeSeconds()));
        }
        finally{singleton.SetValue(null,previousLoader);Store.currentClient=previousClient;}
    }

    [Theory]
    [InlineData("concentration",true)] [InlineData("concentration-control",false)]
    public async Task DiscoveredNativeFixtureReadOnlyPlanScopesOnlyInterruptRoll(string mode,bool loaded)
    {
        var plan=NativeQa.NativeQaModes.Find(mode).Plan;
        Assert.Equal(loaded,plan.Abilities.Contains((ushort)628));Assert.Contains((ushort)360,plan.Abilities);Assert.Contains((ushort)261,plan.Abilities);
        Assert.False(plan.FixedCallbackRolls);Assert.False(plan.GmCharacter110);
        var start=new System.Diagnostics.ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(string arg in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"),typeof(NativeQa.NativeQaFixturePlan).Assembly.Location,"--fixture-plan",mode})start.ArgumentList.Add(arg);
        using var process=System.Diagnostics.Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));await process.WaitForExitAsync(timeout.Token);Assert.True(process.ExitCode==0,await errors);
        using var json=System.Text.Json.JsonDocument.Parse(await output);var env=json.RootElement.GetProperty("Environment");
        Assert.Equal("85",env.GetProperty("MXO_CAST_INTERRUPT_QA_ROLL").GetString());
        Assert.Equal("",env.GetProperty("MXO_CALLBACK_QA_ROLL15").GetString());Assert.Equal("",env.GetProperty("MXO_CALLBACK_QA_ROLL100").GetString());
        Assert.Equal("",env.GetProperty("MXO_DESPERATION_QA_HOLD_HEALTH").GetString());
        Assert.Equal(loaded,json.RootElement.GetProperty("Plan").GetProperty("ExtraQaMetadata").GetProperty("ConcentrateLoaded").GetBoolean());
    }
}
