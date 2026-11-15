using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class TypedSpecialDefenseTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData(); c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.MaxHealth.setValue((ushort)5000);c.playerInstance.Health.setValue((ushort)5000);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    private static AbilityItem Move(ushort id) {var a=new AbilityItem();a.setAbilityID(id);return a;}
    [Theory]
    [InlineData(0,0)] [InlineData(1,1010)] [InlineData(2,183)] [InlineData(3,1009)]
    [InlineData(4,1011)] [InlineData(5,0)] [InlineData(6,1011)] [InlineData(7,0)] [InlineData(-1,0)]
    public void NativeJumpTableSelectsExactDefense(int test,int stat)=>Assert.Equal(stat,AbilityStats.SpecialDefenseStat(test));
    [Fact]
    public void HyperSenseChangesThrownContestsAndAuthoritativeHpButNotMeleeOrOrdinaryRounds()
    {
        var c=Client();AbilityPassives.SetLoaded(c,new[]{((ushort)1101,(ushort)50)},true);
        int Count(AbilityItem ability)
        {
            var rng=new Random(731);int hits=0;int defence=AbilityStats.SpecialDefense(c,ability);
            for(int i=0;i<1000;i++)if(CombatInterlock.RollHit(rng,CombatTactic.Speed,50,CombatTactic.Speed,50,out _,out _,CombatTacticType.Normal,0,defence))hits++;
            return hits;
        }
        int before=Count(Move(134)), melee=Count(Move(197)), ordinary=Count(null!);
        Assert.True(AbilityBuffs.Apply(c,1101));Assert.Equal(47,AbilityStats.SpecialDefense(c,Move(134)));
        int after=Count(Move(134));Assert.True(after<before);Assert.Equal(melee,Count(Move(197)));Assert.Equal(ordinary,Count(null!));
        PvpCombat.Damage(c,after);Assert.Equal(5000-after,PvpCombat.HealthOf(c));
        AbilityBuffs.Remove(c,1101);Assert.Equal(before,Count(Move(134)));
    }
    [Fact]
    public void NpcReadsItsOwnSelectedStatAndCapsPlayerStatsBeforeTheContest()
    {
        var mob=new Mob();TimedEffects.ForMob(mob).Effects.Apply(1101,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=1011,Value=47,Seconds=60}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(47,AbilityStats.SpecialDefense(mob,Move(134)));Assert.Equal(0,AbilityStats.SpecialDefense(mob,Move(197)));
        var c=Client();TimedEffects.ForPlayer(c).Effects.Apply(1101,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=1011,Value=10000,Seconds=60}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(191,AbilityStats.SpecialDefense(c,Move(134)));Assert.Equal(AbilityStats.Defense(c,false),AbilityStats.SpecialDefense(c,null!));
        Assert.Equal(0,AbilityStats.SpecialDefense(c,Move(7)));
        Assert.Equal(AbilityStats.Defense(c,false),AbilityStats.SpecialDefense(c,Move(121)));
        Assert.Equal(AbilityStats.Defense(mob,false),AbilityStats.SpecialDefense(mob,Move(121)));
    }
    [Fact]
    public void FixtureChecksExactDevFlagBeforeDereferencingCaller()
    {
        string? old=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS");
        try {Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","true");Assert.Contains("requires MXO_DEV_COMMANDS=1",CombatDepthDev.Run(null!,new[]{"specialdefense"}));}
        finally{Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",old);}
    }
}
