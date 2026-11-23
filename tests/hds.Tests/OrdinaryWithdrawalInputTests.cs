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
    [Fact] public void RealNormalStylesSupplyDirectPointsAndLinkedPercentageIsAppliedOnlyWhileLoaded()
    {
        Assert.Equal((ushort)1045,AbilityCatalog.IdOf((uint)AbilityCatalog.Find(1005)!.Number("AbilityAttributeLink/AttributeLink")));
        Assert.False(AbilityCatalog.Find(1005)!.Has("AbilityAttributeLink/AttributeLevelBonusScale"));
        var c=Actor();AbilityPassives.SetLoaded(c,WithdrawalFixture.Abilities.Select(id=>(id,(ushort)50)));
        foreach(var row in new[]{(Style:(ushort)17,Raw:65),(Style:(ushort)1112,Raw:72),(Style:(ushort)17,Raw:65)})
        {c.playerData.combatStyle=row.Style;AbilityPassives.Refresh(c,false);Assert.Equal(row.Raw,AbilityStats.Player(c,1005));Assert.Equal(row.Raw,WithdrawalInput.Attack(c,null,true));}
        AbilityPassives.SetLoaded(c,WithdrawalFixture.Abilities.Concat(new ushort[]{120}).Select(id=>(id,(ushort)50)));
        Assert.Equal(25,AbilityStats.Player(c,1045));Assert.Equal(81,WithdrawalInput.Attack(c,null,true));
        AbilityPassives.SetLoaded(c,WithdrawalFixture.Abilities.Select(id=>(id,(ushort)50)));Assert.Equal(0,AbilityStats.Player(c,1045));Assert.Equal(65,WithdrawalInput.Attack(c,null,true));
        c.playerData=new ClientData();Assert.Equal(0,WithdrawalInput.Attack(c,null,true));
    }
    [Fact] public void DirectAndPercentageUseTheirOwnCapsAndRoundBeforeTactic()
    {
        var c=Actor();Stat(c,1005,10000);Stat(c,1045,1000);
        Assert.Equal(600, AbilityStats.Player(c, 1005));
        Assert.Equal(130, AbilityStats.Player(c, 1045));
        Assert.Equal(1380, WithdrawalInput.Attack(c, null, ordinary: true));

        var penalized = Actor();
        Stat(penalized, 1005, 20);
        Stat(penalized, 1045, -11);
        Assert.Equal(17, WithdrawalInput.Attack(penalized, null, ordinary: true));
        Assert.Equal(80, WithdrawalInput.AttackBase(50, 17, CombatTactic.Power));

        Stat(penalized, 163, 50);
        Stat(penalized, 522, 80);
        Stat(penalized, 1044, 60);
        Assert.Equal(17, WithdrawalInput.Attack(penalized, null, ordinary: true));

        var noDirect = Actor();
        Stat(noDirect, 1045, 25);
        Assert.Equal(0, WithdrawalInput.Attack(noDirect, null, ordinary: true));
    }

    [Fact] public void VirologistLoadoutSuppliesBothTypedInputsAndItsUpgrade()
    {
        var actor = Actor();
        var viralMove = Move(330);
        Stat(actor, 1005, 65);
        Stat(actor, 1030, 1);
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)503, (ushort)40) });

        Assert.Equal(67, WithdrawalInput.Attack(actor, null, ordinary: true));
        Assert.Equal(0, AbilityStats.SpecialDefense(actor, viralMove));
        Assert.False(AbilityPassives.IsLoaded(actor, 1077));

        AbilityPassives.SetLoaded(actor, new[] { ((ushort)503, (ushort)40), ((ushort)98, (ushort)50) });
        Assert.Equal(68, WithdrawalInput.Attack(actor, null, ordinary: true));
        Assert.Equal(1, AbilityStats.SpecialDefense(actor, viralMove));
        Assert.True(AbilityPassives.IsLoaded(actor, 1077));

        AbilityPassives.SetLoaded(actor, new[] { ((ushort)503, (ushort)40) });
        Assert.Equal(67, WithdrawalInput.Attack(actor, null, ordinary: true));
        Assert.Equal(0, AbilityStats.SpecialDefense(actor, viralMove));
        Assert.False(AbilityPassives.IsLoaded(actor, 1077));
    }
    [Theory]
    [InlineData(true, true, 70, 50, 113, false)]
    [InlineData(true, false, 69, 49, 112, true)]
    [InlineData(false, true, 67, 48, 111, true)]
    [InlineData(false, false, 66, 47, 110, true)]
    public void PhagePassivesFeedTheirOwnWithdrawViralDefenseAndShieldInputs(bool resist, bool phage,
        int withdrawal, int viralDefense, int evade, bool entryAllowed)
    {
        var actor = Actor();
        actor.playerData.combatStyle = 17;
        var loadout = new List<(ushort, ushort)> { (2, 50), (17, 50), (98, 50), (236, 50) };
        if (resist) loadout.Add((76, 19));
        if (phage) loadout.Add((83, 50));
        AbilityPassives.SetLoaded(actor, loadout);

        Assert.Equal(65, AbilityStats.Player(actor, 1005));
        Assert.Equal(withdrawal, WithdrawalInput.Attack(actor, null, ordinary: true));
        Assert.Equal(viralDefense, AbilityStats.SpecialDefense(actor, Move(330)));
        Assert.Equal(47, AbilityStats.SpecialDefense(actor, Move(254)));
        var entry = CombatInsightEngagement.Calculate(0, 12, 0, 0, 0, AbilityStats.Player(actor, 1049));
        Assert.Equal(112, entry.Force);
        Assert.Equal(evade, entry.Evade);
        Assert.Equal(entryAllowed, entry.Allows);
        Assert.Equal(phage, AbilityPassives.IsLoaded(actor, 1082));
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
        var move = Move(ability);
        int shippedVanishBonus = ability == 368 ? 30 : 0;
        Assert.Equal(InterlockInitiative.Attack(c, move) + shippedVanishBonus,
            WithdrawalInput.Attack(c, move, ordinary: false));
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
    [Fact] public void BlockHookUsesCapturedWholeModifierAndPrimaryWriterNotGuessedDescriptorPrefix()
    {
        byte[] raw=Convert.FromHexString("80BC1500150100F5030000F403030000000000000000");Assert.Equal(22,raw.Length);Assert.True(WithdrawalFixture.BlockRecord(raw));
        Assert.True(WithdrawalFixture.BlockRecord(AbilityBuffs.BuildModifier(0x15,0x115,1013,1012,3,false,0)));
        for(int n=0;n<22;n++)Assert.False(WithdrawalFixture.BlockRecord(raw[..n]));
        foreach(int at in new[]{0,1,2,3,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21})
        {var wrong=(byte[])raw.Clone();wrong[at]^=1;Assert.False(WithdrawalFixture.BlockRecord(wrong));}
        Assert.False(WithdrawalFixture.BlockRecord(new byte[]{99}.Concat(raw).ToArray()));Assert.False(WithdrawalFixture.BlockRecord(raw.Concat(new byte[]{0}).ToArray()));
    }

}
