using hds;
using System.Runtime.CompilerServices;
namespace hds.Tests;
[Collection("DelayedActions")]
public class ConditionalThrownDamageTests
{
    private sealed class Subject:TimedEffects.Subject
    {
        public bool Living=true;public uint Flags;
        internal override WorldClient Context=>null!;
        internal override bool Alive=>Living;
        internal override uint ConditionFlags=>Flags;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active>a,TimedEffects.Active i,List<TimedEffects.Active>r){}
        internal override bool Ticked(TimedEffects.Active m,int damage)=>true;
        internal override void Ended(List<TimedEffects.Active>m){}
    }
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("lastUsedTime",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        c.Alive=true;c.playerData.setOnWorld(true);c.playerData.setDistrictId(1);c.playerInstance.Level.setValue((byte)50);
        c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }
    [Theory][InlineData(279,1,22)][InlineData(279,17,29)][InlineData(279,50,45)][InlineData(282,1,24)][InlineData(282,50,24)][InlineData(281,50,0)]
    public void ActualStoredInitializerUsesRecoveredValueFunctions(int ability,int level,int expected)=>Assert.Equal(expected,ConditionalThrownDamage.Value((ushort)ability,level));
    [Fact]public void OnlyTheTwoStoredOnUseInitializersAreRecognized()
    {
        var rows=AbilityCatalog.All.Values.Where(a=>a.Modifiers.Any(m=>m.Text("StatModInitFunc")=="DealDamageIfOffBalance")).Select(a=>a.Id).Order();
        Assert.Equal(new ushort[]{279,282},rows);
        foreach(ushort id in rows){var m=Assert.Single(AbilityCatalog.Find(id).Modifiers,m=>m.Text("StatModInitFunc")=="DealDamageIfOffBalance");Assert.Equal(2,m.Number("WhenEnabled"));Assert.Equal(1,m.Number("DurationType"));}
    }
    [Theory][InlineData(0,0)][InlineData(2,0)][InlineData(8,0)][InlineData(4,45)][InlineData(6,45)]
    public void ScriptTestsOffBalanceRatherThanTheMisleadingDamageOnDazeComponentName(uint flags,int expected)
    {var s=new Subject{Flags=flags};Assert.Equal(expected,ConditionalThrownDamage.Resolve(279,50,s,10));s.Living=false;Assert.Equal(0,ConditionalThrownDamage.Resolve(279,50,s,10));}
    [Fact]public void ActualStateHolderExpiryAndRemovalStopTheCondition()
    {
        var s=new Subject();s.Effects.Apply(885,new[]{new TimedEffects.Active{Target=200,Proc=TempMods.Proc.State,Seconds=5}},10);
        Assert.Equal(24,ConditionalThrownDamage.Resolve(282,50,s,14));Assert.Equal(0,ConditionalThrownDamage.Resolve(282,50,s,15));
        s.Effects.RemoveParent(885);Assert.Equal(0,ConditionalThrownDamage.Resolve(282,50,s,11));
    }
    [Fact]public void SeparateComponentRetainsExistingInfluenceAndExplicitThrownResistancePolicy()
    {
        var caster=Client();var target=Client();
        TimedEffects.ForPlayer(caster).Effects.Apply(1112,new[]{new TimedEffects.Active{Target=1034,Value=29,Kind=AbilityBuffs.KindStat,Seconds=60}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(58,AbilityStats.ScaleAbilityDamage(caster,279,45));Assert.Equal(30,AbilityStats.ScaleAbilityDamage(caster,282,24));
        Assert.Equal(58,TypedAbilityResistance.Player(caster,target,279,58,7));
        TimedEffects.ForPlayer(target).Effects.Apply(5,new[]{new TimedEffects.Active{Target=1033,Value=75,Kind=AbilityBuffs.KindStat,Seconds=60}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(47,TypedAbilityResistance.Player(caster,target,279,58,7));Assert.Equal(24,TypedAbilityResistance.Player(caster,target,282,30,7));
        TimedEffects.Clear(TimedEffects.ForPlayer(caster));TimedEffects.Clear(TimedEffects.ForPlayer(target));
    }
    [Fact]public void HealthCallbacksCannotSendAConditionalHitIntoAnotherLifeOrWorld()
    {
        var source=Client();var target=Client();var fence=new ConditionalThrownDamage.Fence(source,target);Assert.True(fence.Current(source));Assert.True(fence.TargetCurrent(target));
        source.playerData.worldGeneration++;Assert.False(fence.Current(source));source.playerData.worldGeneration--;source.playerData.lifeGeneration++;Assert.False(fence.Current(source));
        target.playerData.lifeGeneration++;Assert.False(fence.TargetCurrent(target));target.playerData.lifeGeneration--;target.playerData.worldChangePending=true;Assert.False(fence.TargetCurrent(target));
        target.playerData.worldChangePending=false;target.playerData.setOnWorld(false);Assert.False(fence.TargetCurrent(target));
    }
    [Fact]public void IdenticalCountersCannotAuthorizeAReplacementCharacterOwner()
    {
        var source=Client();var target=Client();source.playerData.setCharID(110);target.playerData.setCharID(111);
        var fence=new ConditionalThrownDamage.Fence(source,target);
        var oldSource=source.playerData;var oldTarget=target.playerData;
        source.playerData=Client().playerData;source.playerData.setCharID(110);
        Assert.Equal(oldSource.worldGeneration,source.playerData.worldGeneration);Assert.Equal(oldSource.lifeGeneration,source.playerData.lifeGeneration);Assert.Equal(oldSource.getDistrictId(),source.playerData.getDistrictId());
        Assert.False(fence.Current(source));source.playerData=oldSource;Assert.True(fence.Current(source));
        target.playerData=Client().playerData;target.playerData.setCharID(111);
        Assert.Equal(oldTarget.worldGeneration,target.playerData.worldGeneration);Assert.Equal(oldTarget.lifeGeneration,target.playerData.lifeGeneration);Assert.Equal(oldTarget.getDistrictId(),target.playerData.getDistrictId());
        Assert.False(fence.TargetCurrent(target));target.playerData=oldTarget;Assert.True(fence.TargetCurrent(target));
        source.playerData.setCharID(112);Assert.False(fence.Current(source));target.playerData.setCharID(113);Assert.False(fence.TargetCurrent(target));
    }
    [Fact]public void BonusReusesCurrentPvpEligibilityBeforeAnySuccessOutputOrHealthWrite()
    {
        var source=Client();var target=Client();source.playerData.setCharID(110);target.playerData.setCharID(111);
        source.playerInstance.OrganizationID.setValue((byte)1);target.playerInstance.OrganizationID.setValue((byte)2);target.playerData.pvpFlag=true;
        Assert.Equal(PvpRules.Verdict.Allowed,PvpCombat.Check(source,target));
        var fence=new ConditionalThrownDamage.Fence(source,target);target.playerData.conditionStateFlags=4;
        target.playerInstance.OrganizationID.setValue((byte)1); // A synchronous base-hit callback changed the alliance.
        Assert.Equal(PvpRules.Verdict.NotOpponent,PvpCombat.Check(source,target));
        ConditionalThrownDamage.Player(source,target,279,fence);
        Assert.Equal(1000,PvpCombat.HealthOf(target));Assert.Empty(source.messageQueue.RPCMessagesQueue);Assert.Empty(target.messageQueue.RPCMessagesQueue);
    }
    [Fact]public void PreparationUsesReal885StateWithoutChangingImmunityOrGrantingAnAbility()
    {var e=CombatDepthOffBalance.Effect(30);Assert.NotNull(e);Assert.Equal((ushort)885,e.Ability);var m=Assert.Single(e.Mods);Assert.Equal((ushort)200,m.Target);Assert.Equal(30,m.Seconds);Assert.Null(CombatDepthOffBalance.Effect(61));}
    [Fact]public void NativeModeAndStrictPreparationExposeNoRandomOverridesOrNpcAbilityGrant()
    {
        var mode=NativeQa.NativeQaModes.Find("conditional-thrown-damage");Assert.Equal(HeadlessClient.ConditionalThrowFixture.Abilities,mode.Plan.Abilities);
        Assert.DoesNotContain((ushort)885,mode.Plan.Abilities);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor(mode.Name);Assert.Equal("1",env["MXO_CONDITIONAL_THROW_QA"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")["MXO_CONDITIONAL_THROW_QA"]);
        string? old=Environment.GetEnvironmentVariable("MXO_CONDITIONAL_THROW_QA");try
        {
            Environment.SetEnvironmentVariable("MXO_CONDITIONAL_THROW_QA","1");Assert.True(CombatDepthOffBalance.Allowed("1","mxotest_throw",110,true,false,"CSR_Tester110"));
            Assert.False(CombatDepthOffBalance.Allowed("0","mxotest_throw",110,true,false,"CSR_Tester110"));Assert.False(CombatDepthOffBalance.Allowed("1","mxotest_",110,true,false,"CSR_Tester110"));
            Assert.False(CombatDepthOffBalance.Allowed("1","mxotest_throw;bad",110,true,false,"CSR_Tester110"));Assert.False(CombatDepthOffBalance.Allowed("1","mxotest_throw",2,true,false,"TheNeo"));
            Assert.False(CombatDepthOffBalance.Allowed("1","mxotest_throw",110,true,false,"TheNeo"));Assert.False(CombatDepthOffBalance.Allowed("1","mxotest_throw",110,false,false,"CSR_Tester110"));
            Assert.False(CombatDepthOffBalance.Allowed("1","mxotest_throw",110,true,true,"CSR_Tester110"));
            Environment.SetEnvironmentVariable("MXO_CONDITIONAL_THROW_QA","");Assert.False(CombatDepthOffBalance.Allowed("1","mxotest_throw",110,true,false,"CSR_Tester110"));
        }finally{Environment.SetEnvironmentVariable("MXO_CONDITIONAL_THROW_QA",old);}
        foreach(string bad in new[]{"0","61","NaN","Infinity","-1"})Assert.False(CombatDepthOffBalance.Parse(new[]{"offbalance",bad},out _));
    }
}
