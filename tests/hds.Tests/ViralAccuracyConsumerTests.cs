using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class ViralAccuracyConsumerTests
{
    static WorldClient Client(byte level)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue(level);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    [Theory][InlineData(10,1)][InlineData(25,2)][InlineData(50,5)]
    public void LoadedRunProgramChangesActualViralTotalsAndUnloadingRemovesIt(byte level,int expected)
    {
        var c=Client(level);int before=AbilityStats.ViralAccuracy(c);
        AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(32,50)});
        Assert.Equal(10,AbilityStats.Player(c,1147));
        Assert.Equal(before+expected,AbilityStats.ViralAccuracy(c));
        Assert.Equal(0,AbilityStats.Attack(c,false));Assert.Equal(0,AbilityStats.Attack(c,true));
        var ability=new AbilityItem();ability.setAbilityID(33);
        Assert.Equal(expected,AbilityStats.SpecialAttack(c,ability));
        AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        Assert.Equal(before,AbilityStats.ViralAccuracy(c));
    }
    [Fact]
    public void BonusChangesFreeCastProductionContestWithoutChangingDraws()
    {
        var c=Client(50);
        var before=FreeCastViralContest.Evaluate(AbilityStats.ViralAccuracy(c),0,0,0,0,null,17000,17000);
        AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(32,50)});
        var after=FreeCastViralContest.Evaluate(AbilityStats.ViralAccuracy(c),0,0,0,0,null,17000,17000);
        Assert.True(after.Attack>before.Attack);Assert.Equal(before.Defense,after.Defense);
        Assert.Equal(before.Forced,after.Forced);
    }
}
