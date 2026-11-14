using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using NativeQa;

namespace hds.Tests;

public class MobiusCodeTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        typeof(WorldClient).GetProperty("IsBot")!.SetValue(c,true);
        c.playerData=new ClientData();c.playerData.setOnWorld(true);
        c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("alive",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,true);
        return c;
    }
    [Theory]
    [InlineData(-1,false)] [InlineData(0,true)] [InlineData(1,false)] [InlineData(99,false)] [InlineData(100,false)]
    public void PrimaryChanceIsExactlyOnePercent(int roll,bool expected)=>Assert.Equal(expected,MobiusCode.ProcSucceeds(roll));

    [Fact]
    public void CapabilityHasStrictExpiryAndDoesNotCrossOwners()
    {
        var a=Client();var b=Client();MobiusCode.Apply(a,100);
        Assert.True(MobiusCode.PreventsFreeAttack(a,109.999));
        Assert.False(MobiusCode.PreventsFreeAttack(b,105));
        Assert.False(MobiusCode.PreventsFreeAttack(a,110));
        Assert.False(MobiusCode.PreventsFreeAttack(a,111));
    }
    [Theory]
    [InlineData("world")] [InlineData("life")] [InlineData("death")] [InlineData("duel")] [InlineData("owner")] [InlineData("clear")] [InlineData("parent")]
    public void InvalidatedCapabilityCannotRevive(string why)
    {
        var c=Client();MobiusCode.Apply(c,100);var data=c.playerData;
        switch(why)
        {
            case "world":data.worldGeneration++;break;
            case "life":data.lifeGeneration++;break;
            case "death":data.isDead=true;break;
            case "duel":data.duelDeath=true;break;
            case "owner":c.playerData=new ClientData();c.playerData.setOnWorld(true);break;
            case "clear":TimedEffects.ForPlayer(c).Effects.RemoveAll();break;
            case "parent":TimedEffects.ForPlayer(c).Effects.RemoveParent(967);break;
        }
        Assert.False(MobiusCode.PreventsFreeAttack(c,101));
        c.playerData=data;data.worldGeneration=0;data.lifeGeneration=0;data.isDead=false;data.duelDeath=false;
        Assert.False(MobiusCode.PreventsFreeAttack(c,102));
    }
    [Fact]
    public void OtherParentRemovalDoesNotRemoveMobius()
    {
        var c=Client();MobiusCode.Apply(c,100);TimedEffects.ForPlayer(c).Effects.RemoveParent(77);
        Assert.True(MobiusCode.PreventsFreeAttack(c,101));
    }
    [Fact]
    public void OldCallbackCannotAdoptNewLifeOrOverwriteItsNewProc()
    {
        var c=Client();var beforeChance=new MobiusCode.Invocation(c);
        c.playerData.lifeGeneration++;
        MobiusCode.Apply(beforeChance,100);
        Assert.False(MobiusCode.PreventsFreeAttack(c,101));
        MobiusCode.Apply(c,102);
        MobiusCode.Apply(beforeChance,103);
        Assert.True(MobiusCode.PreventsFreeAttack(c,104));
        Assert.False(MobiusCode.PreventsFreeAttack(c,112));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void CallbackCapturedBeforeRemovalCannotReinstallProtection(bool all)
    {
        var c=Client();var beforeChance=new MobiusCode.Invocation(c);
        if(all)TimedEffects.ForPlayer(c).Effects.RemoveAll();else TimedEffects.ForPlayer(c).Effects.RemoveParent(967);
        Assert.False(MobiusCode.Apply(beforeChance,100));
        Assert.False(MobiusCode.PreventsFreeAttack(c,101));
        Assert.True(MobiusCode.Apply(c,102));
        Assert.False(MobiusCode.Apply(beforeChance,103));
        Assert.True(MobiusCode.PreventsFreeAttack(c,104));
    }
    [Theory]
    [InlineData("world")] [InlineData("life")] [InlineData("owner")] [InlineData("clear")] [InlineData("parent")]
    public void ImmediateHealBoundaryRejectsLifecycleChangedByHealthCallbackBeforeAnyDraw(string change)
    {
        var c=Client();int heals=0,draws=0,clocks=0;
        var invocation=AbilitySupport.HealImmediate(c,c,967,()=>
        {
            heals++; // Simulates synchronous TimedEffects.HealthChanged inside the ordinary HealSelf callback.
            switch(change)
            {
                case "world":c.playerData.worldGeneration++;break;
                case "life":c.playerData.lifeGeneration++;break;
                case "owner":c.playerData=new ClientData();c.playerData.setOnWorld(true);break;
                case "clear":TimedEffects.ForPlayer(c).Effects.RemoveAll();break;
                case "parent":TimedEffects.ForPlayer(c).Effects.RemoveParent(967);break;
            }
        });
        Assert.Equal(1,heals);
        Assert.False(MobiusCode.Resolve(invocation,()=>{draws++;return 0;},()=>{clocks++;return 100;}));
        Assert.Equal(0,draws);Assert.Equal(0,clocks);
        Assert.False(MobiusCode.PreventsFreeAttack(c,101));
    }
    [Theory]
    [InlineData(967,true,true)] [InlineData(77,true,false)] [InlineData(967,false,false)]
    public void ImmediateHealBoundaryKeepsOrdinaryHealingAndOnlySelf967Producer(ushort ability,bool self,bool proc)
    {
        var caster=Client();var target=self?caster:Client();int heals=0,draws=0;
        var invocation=AbilitySupport.HealImmediate(caster,target,ability,()=>heals++);
        Assert.Equal(1,heals);
        Assert.Equal(proc,MobiusCode.Resolve(invocation,()=>{draws++;Assert.Equal(1,heals);return 0;},()=>100));
        Assert.Equal(proc?1:0,draws);Assert.Equal(proc,MobiusCode.PreventsFreeAttack(target,101));
    }
    private sealed class CountingRandom:Random
    {
        public int Draws;
        public CountingRandom(int seed):base(seed){}
        public override int Next(int maxValue){Draws++;return base.Next(maxValue);}
        public override int Next(int min,int max){Draws++;return base.Next(min,max);}
        public override double NextDouble(){Draws++;return base.NextDouble();}
    }
    [Fact]
    public void GateKeepsContestDrawsAndSkipsDamageWithoutCancelingSubsequentShots()
    {
        var normalRng=new CountingRandom(5);var blockedRng=new CountingRandom(5);
        for(int n=0;n<20;n++)
        {
            // Compare against the contest alone: every protected shot consumes exactly those draws.
            var expectedRng=new CountingRandom(5+n);
            var actualRng=new CountingRandom(5+n);
            CombatInterlock.RollHit(expectedRng,CombatTactic.Speed,50,CombatTactic.Speed,50,out var attack,out var defense);
            int gates=0;
            var blocked=FreeAttacks.Fire(actualRng,50,CombatTactic.Speed,CombatTacticType.Normal,50,CombatTactic.Speed,0,canBeFreeAttacked:()=>
            { gates++;Assert.Equal(expectedRng.Draws,actualRng.Draws);return false; });
            Assert.Equal(1,gates);
            Assert.Equal(attack,blocked.AttackRoll);Assert.Equal(defense,blocked.DefenseRoll);
            Assert.Equal(expectedRng.Draws,actualRng.Draws);Assert.Equal(expectedRng.NextDouble(),actualRng.NextDouble());
            Assert.False(blocked.Hit);Assert.Equal(0,blocked.Damage);Assert.Equal(0,blocked.Absorbed);
        }
        Assert.Contains(Enumerable.Range(0,50).Select(_=>FreeAttacks.Fire(normalRng,50,CombatTactic.Speed,CombatTacticType.Normal,50,CombatTactic.Speed,0)),s=>s.Hit&&s.Damage>0);
        Assert.Contains(Enumerable.Range(0,50).Select(_=>FreeAttacks.Fire(blockedRng,50,CombatTactic.Speed,CombatTacticType.Normal,50,CombatTactic.Speed,0,canBeFreeAttacked:()=>false)),s=>s.AttackRoll>s.DefenseRoll&&!s.Hit&&s.Damage==0);
    }
    [Fact]
    public void UnarmedCallbackControlUsesExactlyOneNaturalChanceDraw()
    {
        var c=Client();int draws=0;
        Assert.Equal(42,MobiusCodeQa.Roll(c,()=>{draws++;return 42;}));Assert.Equal(1,draws);
        Assert.False(MobiusCode.PreventsFreeAttack(c,0));
    }
    [Theory]
    [InlineData("mobius-free-attack-positive","positive")]
    [InlineData("mobius-free-attack-negative","negative")]
    public void ModesAreDiscoveredAndDoNotFixCombatRng(string mode,string branch)
    {
        Assert.Contains(mode,NativeQaModes.Names);var p=NativeQaModes.Find(mode).Plan;
        Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Contains((ushort)967,p.Abilities);Assert.Equal("1",p.EnvironmentOverrides["MXO_MOBIUS_QA"]);
        Assert.Equal(branch,p.ExtraQaMetadata["MobiusCallbackBranch"].GetString());
        Assert.Empty(NativeQaFixturePlan.EnvironmentFor("actions")["MXO_MOBIUS_QA"]);
    }
    [Theory]
    [InlineData("1","mxotest_mobius","1",110,true,false,"CSR",true)]
    [InlineData("0","mxotest_mobius","1",110,true,false,"CSR",false)]
    [InlineData("1","reality_hd","1",110,true,false,"CSR",false)]
    [InlineData("1","mxotest_mobius","0",110,true,false,"CSR",false)]
    [InlineData("1","mxotest_mobius","1",111,true,false,"CSR",false)]
    [InlineData("1","mxotest_mobius","1",110,true,false,"TheNeo",false)]
    [InlineData("1","mxotest_mobius","1",110,false,false,"CSR",false)]
    public void CallbackControlIsStrictlyDisposable(string dev,string db,string feature,uint id,bool world,bool dead,string name,bool allowed)=>
        Assert.Equal(allowed,MobiusCodeQa.Allowed(dev,db,feature,id,world,dead,name));
}
