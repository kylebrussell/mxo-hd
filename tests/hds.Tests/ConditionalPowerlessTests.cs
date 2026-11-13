using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class ConditionalPowerlessTests
{
    private sealed class Subject:TimedEffects.Subject
    {
        public bool Living=true; public uint Flags; public List<TimedEffects.Active> Off=new();
        internal override WorldClient Context=>null!;
        internal override bool Alive=>Living;
        internal override uint ConditionFlags=>Flags;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active> a,TimedEffects.Active i,List<TimedEffects.Active> r){Off.AddRange(r);}
        internal override bool Ticked(TimedEffects.Active m,int damage)=>true;
        internal override void Ended(List<TimedEffects.Active> ended)=>Off.AddRange(ended);
    }
    private sealed class NoProc:Random { public override double NextDouble()=>.99; }
    private sealed class AllProc:Random { public override double NextDouble()=>0; }
    private static TempMods.Effect Throw(){Assert.True(TempMods.TryGet(340,out var e));return e;}
    private static void Stagger(Subject s,double start,float seconds=30)=>s.Effects.Apply(883,new[]{new TimedEffects.Active{Kind=TempMods.KindState,Target=201,Proc=TempMods.Proc.State,Seconds=seconds}},start);
    [Fact] public void StoredCallbackAndPowerlessStateHaveNoImplicitTimerProducer()
    {
        var m=Assert.Single(AbilityCatalog.Find(340).Modifiers,m=>m.Text("StatModInitFunc")=="ApplyPowerlessIfStaggered");
        Assert.Equal(6,m.Number("MinValue"));Assert.Equal(6,m.Number("MaxValue"));Assert.Equal(1,m.Number("DurationType"));Assert.Equal(2,m.Number("WhenEnabled"));Assert.Equal(0,m.State);
        var state=Assert.Single(AbilityCatalog.Find(96).Modifiers);Assert.Equal("PowerlessInit",state.Text("StatModInitFunc"));Assert.Equal("PowerlessTerm",state.Text("StatModTermFunc"));
    }
    [Theory][InlineData(false,false,false)][InlineData(true,true,false)][InlineData(true,false,true)]
    public void AbsentExpiredOrDeadAtInitializationNeverAddsPowerless(bool stagger,bool expired,bool dead)
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var s=new Subject{Living=!dead};if(stagger)Stagger(s,expired?now-31:now);
        TimedEffects.Apply(s,Throw(),now,rng:new NoProc());Assert.False(s.Effects.HasAt(96,now));TimedEffects.Clear(s);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void CurrentHolderOrTacticConditionAddsSixSecondsWithoutNewTimer(bool flags)
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var s=new Subject();if(flags)s.Flags=CombatInterlock.ConditionBit(CombatTactic.Power);else Stagger(s,now);
        var added=TimedEffects.Apply(s,Throw(),now,rng:new NoProc());var state=Assert.Single(added,m=>m.Target==96);
        Assert.Equal(6,state.Seconds);Assert.Equal(now+6,state.Expires);Assert.Equal((ushort)340,state.Parent);Assert.False(s.Effects.HasParent(734));
        TimedEffects.RunDue(now+6.01);Assert.False(s.Effects.HasAt(96,now+6.01));Assert.Contains(state,s.Off);TimedEffects.Clear(s);
    }
    [Fact] public void ConditionArrivingLaterDoesNotRunAnExpiryCallback()
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var s=new Subject();TimedEffects.Apply(s,Throw(),now,rng:new AllProc());Stagger(s,now+1);
        TimedEffects.RunDue(now+10);Assert.False(s.Effects.HasAt(96,now+10));TimedEffects.Clear(s);
    }
    [Fact] public void RefreshCleanseDeathAndWorldClearDoNotDuplicateOrRecreateState()
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var s=new Subject();Stagger(s,now);
        TimedEffects.Apply(s,Throw(),now,rng:new AllProc());Assert.Equal(Throw().Mods.Length+1,s.Effects.Snapshot().Count(m=>m.Parent==340));
        TimedEffects.Apply(s,Throw(),now+1,rng:new AllProc());Assert.Single(s.Effects.Snapshot(),m=>m.Parent==340&&m.Target==96);
        Assert.True(s.Effects.HasAt(200,now+1));Assert.True(s.Effects.HasParent(883));
        TimedEffects.RemoveParent(s,340);Assert.False(s.Effects.HasAt(96,now+1));Assert.True(s.Effects.HasParent(883));
        TimedEffects.Apply(s,Throw(),now+2,rng:new NoProc());TimedEffects.Clear(s);TimedEffects.RunDue(now+40);Assert.True(s.Effects.IsEmpty);
    }
    [Fact] public void ExistingImmunityPolicyRefusesPowerlessButKeepsSiblingsAndOriginalTimer()
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var s=new Subject();Stagger(s,now);
        var timer=new TimedEffects.Active{Target=681,Proc=TempMods.Proc.State,Seconds=30};s.Effects.Apply(734,new[]{timer},now);
        TimedEffects.Apply(s,Throw(),now,rng:new AllProc());Assert.False(s.Effects.HasAt(96,now));Assert.True(s.Effects.HasAt(200,now));Assert.Same(timer,Assert.Single(s.Effects.Snapshot(),m=>m.Parent==734));TimedEffects.Clear(s);
    }
    [Fact] public void ActualNpcHolderUsesItsCurrentTacticFlag()
    {
        var mob=new Mob();typeof(Mob).GetField("conditionFlags",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(mob,CombatInterlock.ConditionBit(CombatTactic.Power));
        var subject=TimedEffects.ForMob(mob);double now=TimeUtils.GetSimTimeSeconds()+100;TimedEffects.Apply(subject,Throw(),now,rng:new NoProc());Assert.True(subject.Effects.HasAt(96,now));TimedEffects.Clear(subject);
    }
    [Fact] public void StaggerPreparationRequiresItsModeFlagAndExistingStrictIdentityScope()
    {
        string? old=Environment.GetEnvironmentVariable("MXO_CONDITIONAL_POWERLESS_QA");
        try { Environment.SetEnvironmentVariable("MXO_CONDITIONAL_POWERLESS_QA",null);Assert.False(CombatDepthStagger.Allowed("1","mxotest_fixture",110,true,false,"CSR"));
            Environment.SetEnvironmentVariable("MXO_CONDITIONAL_POWERLESS_QA","1");Assert.True(CombatDepthStagger.Allowed("1","mxotest_fixture",110,true,false,"CSR"));
            foreach(var args in new[]{("0","mxotest_fixture",110u,true,false,"CSR"),("1","reality_hd",110u,true,false,"CSR"),("1","mxotest_",110u,true,false,"CSR"),("1","mxotest_bad-name",110u,true,false,"CSR"),("1","mxotest_fixture",0u,true,false,"CSR"),("1","mxotest_fixture",2u,true,false,"CSR"),("1","mxotest_fixture",110u,false,false,"CSR"),("1","mxotest_fixture",110u,true,true,"CSR"),("1","mxotest_fixture",110u,true,false,"TheNeo")})
                Assert.False(CombatDepthStagger.Allowed(args.Item1,args.Item2,args.Item3,args.Item4,args.Item5,args.Item6));
            Assert.True(CombatDepthStagger.Parse(new[]{"stagger"},out float seconds));Assert.Equal(30,seconds);
            foreach(string invalid in new[]{"0","61","NaN","Infinity","-1","1e2"})Assert.False(CombatDepthStagger.Parse(new[]{"stagger",invalid},out _));
            var source=CombatDepthStagger.Effect(2);Assert.Equal((ushort)883,source.Ability);Assert.Equal((ushort)201,Assert.Single(source.Mods).Target);Assert.Equal(2,source.Mods[0].Seconds);
        } finally {Environment.SetEnvironmentVariable("MXO_CONDITIONAL_POWERLESS_QA",old);}
    }
    [Fact] public void DiagnosticModeGrantsPlayer340ButNeverNpc883OrGlobalRandomControls()
    {
        var mode=NativeQa.NativeQaModes.Find("conditional-powerless");Assert.Equal("conditional-powerless",mode.Name);
        Assert.Equal(new ushort[]{2,17,340,1112},mode.Plan.Abilities);Assert.DoesNotContain((ushort)883,mode.Plan.Abilities);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor(mode.Name);Assert.Equal("1",env["MXO_CONDITIONAL_POWERLESS_QA"]);Assert.Equal("1",env["MXO_DEV_COMMANDS"]);
        Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);Assert.Equal("",env["MXO_GM_CHARACTER_IDS"]);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")["MXO_CONDITIONAL_POWERLESS_QA"]);
    }
    [Fact] public void ActualPlayerRecordsAndActionGateShareTheSameCleanup()
    {
        var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        c.playerData.conditionStateFlags=CombatInterlock.ConditionBit(CombatTactic.Power);var subject=TimedEffects.ForPlayer(c);double now=TimeUtils.GetSimTimeSeconds();
        try {
            TimedEffects.Apply(subject,Throw(),now,rng:new NoProc());var state=Assert.Single(subject.Effects.Snapshot(),m=>m.Target==96);
            Assert.False(ControlActions.CanUseAbilities(c));Assert.True(ControlActions.CanUseWeapons(subject.Effects));
            Assert.Contains(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),m=>m.content.AsSpan(1).SequenceEqual(PlayerEffects.Record(state,false)));
            Assert.False(subject.Effects.HasParent(734));TimedEffects.RemoveParent(subject,340);Assert.True(ControlActions.CanUseAbilities(c));
            Assert.Contains(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),m=>m.content.AsSpan(1).SequenceEqual(PlayerEffects.Record(state,true)));
            TimedEffects.Apply(subject,Throw(),now,rng:new NoProc());c.playerData.setOnWorld(false);TimedEffects.Clear(subject);Assert.True(subject.Effects.IsEmpty);Assert.True(ControlActions.CanUseAbilities(c));
        } finally {TimedEffects.Clear(subject);}
    }
}
