using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class TypedSpecialAttackTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)5000);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    private static AbilityItem Move(ushort id){var a=new AbilityItem();a.setAbilityID(id);return a;}
    private static void Stat(WorldClient c,ushort stat,int value)=>TimedEffects.ForPlayer(c).Effects.Apply(stat,
        new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=stat,Value=value,Seconds=60}},TimeUtils.GetSimTimeSeconds());
    [Theory]
    [InlineData(0,0)][InlineData(1,524)][InlineData(2,342)][InlineData(3,522)][InlineData(4,1032)]
    [InlineData(5,0)][InlineData(6,0)][InlineData(7,0)][InlineData(-1,0)]
    public void ProvenUiSelectorIsBoundedAndEscapeRemainsFallback(int test,int stat)=>Assert.Equal(stat,AbilityStats.SpecialAttackStat(test));
    [Fact]
    public void GunAndMeleeMatchExistingCalculationsAndUnknownEscapePreserveOldBehavior()
    {
        var c=Client();Stat(c,163,3);Stat(c,522,7);Stat(c,524,31);Stat(c,996,12);Stat(c,997,18);
        Assert.Equal(AbilityStats.Attack(c,true),AbilityStats.SpecialAttack(c,Move(105)));
        Assert.Equal(AbilityStats.Attack(c,false),AbilityStats.SpecialAttack(c,Move(197)));
        foreach(var move in new AbilityItem?[]{null,Move(7),Move(121)}) Assert.Equal(AbilityStats.Attack(c,false),AbilityStats.SpecialAttack(c,move!));
    }
    [Fact]
    public void CapturedThrownDebuffChangesProductionContestWhileMeleeDoesNot()
    {
        var c=Client();Stat(c,522,40);
        int Hits(){var rng=new Random(731);int hits=0;for(int i=0;i<1000;i++)if(CombatInterlock.RollHit(rng,CombatTactic.Speed,50,CombatTactic.Speed,50,out _,out _,CombatTacticType.Normal,AbilityStats.SpecialAttack(c,Move(134)),0))hits++;return hits;}
        int ordinary=AbilityStats.Attack(c,false),before=Hits();
        Assert.True(TempMods.TryGet(97,out var captured));
        var thrown=captured.Mods.Single(m=>m.Target==1032);Assert.Equal(-10,thrown.MinValue);
        Stat(c,1032,-10);Assert.Equal(-10,AbilityStats.SpecialAttack(c,Move(134)));Assert.True(Hits()<before);
        Assert.Equal(ordinary,AbilityStats.Attack(c,false));
    }
    [Fact]
    public void DirectCapturedViralBuffWorksWithoutInventingRunProgramLinkMath()
    {
        var c=Client();Assert.True(AbilityBuffs.Apply(c,314));
        Assert.Equal(108,AbilityStats.SpecialAttack(c,Move(33)));
        Stat(c,1147,100);Assert.Equal(108,AbilityStats.SpecialAttack(c,Move(33)));
        Stat(c,342,10000);Assert.Equal(259,AbilityStats.SpecialAttack(c,Move(33)));
        Assert.Equal(0,AbilityStats.Attack(c,false));
        Assert.True(AbilityCatalog.Find(33)!.CastSeconds>0); // admission deliberately remains non-queued
    }
    [Theory]
    [InlineData("1","mxotest_owned",110,true,false,"CSR110",true)]
    [InlineData("true","mxotest_owned",110,true,false,"CSR110",false)]
    [InlineData("1","mxo",110,true,false,"CSR110",false)]
    [InlineData("1","mxotest_owned",2,true,false,"CSR110",false)]
    [InlineData("1","mxotest_owned",110,true,false,"TheNeo",false)]
    [InlineData("1","mxotest_owned",110,false,false,"CSR110",false)]
    [InlineData("1","mxotest_owned",110,true,true,"CSR110",false)]
    public void DiagnosticAuthorityIsStrict(string dev,string db,uint id,bool world,bool dead,string name,bool expected)=>
        Assert.Equal(expected,TypedAttackDiagnostic.Allowed(dev,db,id,world,dead,name));
}
