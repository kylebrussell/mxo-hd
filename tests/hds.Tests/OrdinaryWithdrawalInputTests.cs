using System.Runtime.CompilerServices;
using hds;
using HeadlessClient;
namespace hds.Tests;
[Collection("DelayedActions")]
public class OrdinaryWithdrawalInputTests
{
    static WorldClient Actor(byte level=50)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue(level);c.playerInstance.Health.setValue((ushort)20000);c.playerInstance.MaxHealth.setValue((ushort)20000);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }
    static AbilityItem Move(ushort id){var m=new AbilityItem();m.setAbilityID(id);return m;}
    static void Stat(WorldClient c,ushort id,int value)=>TimedEffects.ForPlayer(c).Effects.Apply(id,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=id,Value=value,Seconds=60}},TimeUtils.GetSimTimeSeconds());
    [Fact] public void RealNormalStylesSupplyPrimary65Then72Then65While1045RemainsUnconsumed()
    {
        Assert.Equal((ushort)1045,AbilityCatalog.IdOf((uint)AbilityCatalog.Find(1005)!.Number("AbilityAttributeLink/AttributeLink")));
        Assert.False(AbilityCatalog.Find(1005)!.Has("AbilityAttributeLink/AttributeLevelBonusScale"));
        var c=Actor();AbilityPassives.SetLoaded(c,WithdrawalFixture.Abilities.Select(id=>(id,(ushort)50)));
        foreach(var row in new[]{(Style:(ushort)17,Raw:65),(Style:(ushort)1112,Raw:72),(Style:(ushort)17,Raw:65)})
        {c.playerData.combatStyle=row.Style;AbilityPassives.Refresh(c,false);Assert.Equal(row.Raw,AbilityStats.Player(c,1005));Assert.Equal(row.Raw,WithdrawalInput.Attack(c,null,true));}
        AbilityPassives.SetLoaded(c,WithdrawalFixture.Abilities.Concat(new ushort[]{120}).Select(id=>(id,(ushort)50)));
        Assert.Equal(25,AbilityStats.Player(c,1045));Assert.Equal(65,WithdrawalInput.Attack(c,null,true));
        AbilityPassives.SetLoaded(c,WithdrawalFixture.Abilities.Select(id=>(id,(ushort)50)));Assert.Equal(0,AbilityStats.Player(c,1045));Assert.Equal(65,WithdrawalInput.Attack(c,null,true));
        c.playerData=new ClientData();Assert.Equal(0,WithdrawalInput.Attack(c,null,true));
    }
    [Fact] public void DirectCapAppliesAndLinkedBonusIsNotGuessed()
    {
        var c=Actor();Stat(c,1005,10000);Stat(c,1045,1000);
        Assert.Equal(600,AbilityStats.Player(c,1005));Assert.Equal(130,AbilityStats.Player(c,1045));Assert.Equal(600,WithdrawalInput.Attack(c,null,true));
        var n=Actor();Stat(n,1005,20);Stat(n,1045,-11);Assert.Equal(20,WithdrawalInput.Attack(n,null,true));
        Stat(n,163,50);Stat(n,522,80);Stat(n,1044,60);Assert.Equal(20,WithdrawalInput.Attack(n,null,true)); // Ordinary-withdraw input is typed, not a duplicate melee/initiative addition.
    }
    [Theory] [InlineData(CombatTactic.Speed,1d)] [InlineData(CombatTactic.Power,1.2d)] [InlineData(CombatTactic.Grab,.9d)] [InlineData(CombatTactic.Block,1d)]
    public void PrimaryTacticFactorsFeedExistingTwoRollsWithoutNewDraws(CombatTactic tactic,double factor)
    {
        Assert.Equal(factor,WithdrawalInput.TacticFactor(tactic));Assert.Equal((int)(122*factor),WithdrawalInput.AttackBase(50,72,tactic));
        var actual=new Random(731);var oracle=new Random(731);
        CombatInterlock.RollHit(actual,tactic,50,CombatTactic.Speed,50,out ushort a,out ushort d,CombatTacticType.Normal,72,0,true);
        ushort expectedAttack=CombatInterlock.TacticRoll(oracle,(int)(122*factor),CombatInterlock.AttackRollRange);
        ushort expectedDefence=CombatInterlock.TacticRoll(oracle,50*(100+CombatTactics.Get(CombatTactic.Speed).DefensePercent)/100,CombatInterlock.DefenseRollRange);
        Assert.Equal(expectedAttack,a);Assert.Equal(expectedDefence,d);Assert.Equal(oracle.Next(),actual.Next());
    }
    [Fact] public void BlockWithdrawalHasPositiveScoreAndActualResultEvenThoughBlockDoesNotHit()
    {
        var rng=new Random(731);bool hits=CombatInterlock.RollHit(rng,CombatTactic.Block,50,CombatTactic.Block,50,out ushort score,out _,CombatTacticType.Normal,72,0,true);
        Assert.False(hits);Assert.True(score>1);Assert.Equal(122,WithdrawalInput.AttackBase(50,72,CombatTactic.Block));
        var outcome=CombatRules.Resolve(new(){Tactic=CombatTactic.Block,AttackRoll=score,Hits=hits,Health=1000},new(){Tactic=CombatTactic.Block,AttackRoll=1,Health=1000},false,false,true);
        Assert.True(outcome.Withdrew);Assert.Equal(CombatRules.Withdraw,outcome.Flags);
    }
    [Theory] [InlineData((ushort)121)] [InlineData((ushort)122)] [InlineData((ushort)292)] [InlineData((ushort)295)] [InlineData((ushort)368)] [InlineData((ushort)453)]
    public void Tf6NeverEntersOrdinaryBranchEvenIf44WasAlsoRequested(ushort ability)
    {
        Assert.False(WithdrawalInput.Ordinary(true,false,Move(ability),true));var c=Actor();Stat(c,1005,500);Stat(c,1045,100);
        Assert.Equal(InterlockInitiative.Attack(c,Move(ability)),WithdrawalInput.Attack(c,Move(ability),false));
    }
    [Fact] public void OnlyCurrentNonopeningOrdinaryRequestSelectsTypedInput()
    {
        Assert.False(WithdrawalInput.Ordinary(true,false,null,false)); // Internal TF6/teardown origin never adopts ordinary44 input.
        Assert.False(WithdrawalInput.Ordinary(false,false,null,true));Assert.False(WithdrawalInput.Ordinary(true,true,null,true));
        Assert.True(WithdrawalInput.Ordinary(true,false,null,true));Assert.True(WithdrawalInput.Ordinary(true,false,Move(134),true));
        var c=Actor();Stat(c,522,50);Stat(c,1044,20);Stat(c,1005,200);
        Assert.Equal(InterlockInitiative.Attack(c,null),WithdrawalInput.Attack(c,null,false));
        foreach(var tactic in Enum.GetValues<CombatTactic>())
        {
            var a=new Random(731);var b=new Random(731);
            CombatInterlock.RollHit(a,tactic,50,CombatTactic.Speed,50,out ushort ar,out ushort ad,CombatTacticType.Precise,70,20);
            // Literal pre-change RollHit base arithmetic, independently followed by the established two draws.
            int oldBase=(int)(Math.Max(0,50+70)*(100+CombatTactics.Get(tactic).TacticPercent)/100*CombatTactics.TypeAttackRollMultiplier(CombatTacticType.Precise));
            ushort br=CombatInterlock.TacticRoll(b,oldBase,CombatInterlock.AttackRollRange);
            ushort bd=CombatInterlock.TacticRoll(b,Math.Max(0,50+20)*(100+CombatTactics.Get(CombatTactic.Speed).DefensePercent)/100,CombatInterlock.DefenseRollRange);
            Assert.Equal(ar,br);Assert.Equal(ad,bd);Assert.Equal(a.Next(),b.Next());
        }
    }
    [Fact] public void ActualPvpRequestRetainsFirstOriginAndInternalEscapeDoesNotBecomeOrdinaryNextRound()
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var c=Actor();var fight=(PvpFight)RuntimeHelpers.GetUninitializedObject(typeof(PvpFight));
        typeof(PvpFight).GetField("sync",flags)!.SetValue(fight,new object());typeof(PvpFight).GetField("rng",flags)!.SetValue(fight,new Random(731));typeof(PvpFight).GetField("<IsActive>k__BackingField",flags)!.SetValue(fight,true);
        var side=new PvpFight.Side{Client=c,Move=new CombatInterlock.Move()};
        typeof(PvpFight).GetField("members",flags)!.SetValue(fight,new List<PvpFight.Side>{side});
        fight.RequestWithdraw(c); // Exact default used by SideRoll's null internal leave path.
        Assert.True(side.Withdrawing);Assert.False(side.OrdinaryWithdrawalRequested);
        Assert.False(WithdrawalInput.Ordinary(side.Withdrawing,false,null,side.OrdinaryWithdrawalRequested));
        fight.RequestWithdraw(c,true);Assert.False(side.OrdinaryWithdrawalRequested); // Existing pending request cannot change its origin.
        side.Withdrawing=false;fight.RequestWithdraw(c,true);Assert.True(side.OrdinaryWithdrawalRequested);
        Assert.True(WithdrawalInput.Ordinary(side.Withdrawing,false,null,side.OrdinaryWithdrawalRequested));
    }
    [Fact] public void FixtureHasSevenNormalParentsAndNoLoadoutOrStatOverrides()
    {
        var ids=WithdrawalFixture.Abilities;var rows=ids.Select((id,i)=>new AbilityMemory.Row((uint)i,(ushort)(400+i),0x80000000u|((uint)id<<10),50,id!=1112)).ToArray();
        Assert.Null(AbilityLoadQualification.Refusal(rows,new[]{Assert.Single(rows,x=>x.Ability==1112)},PlayerProgression.ExperienceForLevel(50),out bool low));Assert.False(low);
        Assert.True(rows.Sum(x=>AbilityMemory.Cost(x.Ability))<=AbilityMemory.ForLevel(50));Assert.Equal(7,ids.Length);
        var mode=NativeQa.NativeQaModes.RequireKnown(WithdrawalFixture.Name);Assert.False(mode.Plan.DevCommands);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Equal(ids,mode.Plan.Abilities);Assert.DoesNotContain(mode.Plan.EnvironmentOverrides.Keys,k=>k.Contains("ROLL")||k.Contains("ATTACK15"));
    }
    [Fact] public void ExactRoundOracleRejectsWrongOutcomeTruncationAndForeignFraming()
    {
        byte[] r=new byte[89];r[0]=0x45;BitConverter.GetBytes(0x200u).CopyTo(r,61);BitConverter.GetBytes(1).CopyTo(r,65);
        var produced=CombatInterlock.BuildRoundResult(1,true,new CombatInterlock.Move(),new CombatInterlock.Move(),123,CombatRules.Withdraw,0,0,0,0,0,combatResult:1);
        Assert.Equal(89,produced.Length);Assert.True(WithdrawalFixture.WithdrawalRound(produced));
        Assert.True(WithdrawalFixture.WithdrawalRound(r));Assert.False(WithdrawalFixture.WithdrawalRound(r[..88]));Assert.False(WithdrawalFixture.WithdrawalRound(new byte[]{99}.Concat(r).ToArray()));
        r[62]=0;Assert.False(WithdrawalFixture.WithdrawalRound(r));BitConverter.GetBytes(0x200u).CopyTo(r,61);BitConverter.GetBytes(0).CopyTo(r,65);Assert.False(WithdrawalFixture.WithdrawalRound(r));
    }
}
