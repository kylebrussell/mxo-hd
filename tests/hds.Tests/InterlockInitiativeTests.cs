using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;

[Collection("DelayedActions")]
public class InterlockInitiativeTests
{
    private static WorldClient Actor(ushort health=250)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)20);c.playerInstance.MaxHealth.setValue((ushort)500);c.playerInstance.Health.setValue(health);
        c.playerInstance.InnerStrengthMax.setValue((ushort)200);c.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    [Fact] public void RealHalfHealthSnapshotChangesInterlockRollsAndExpiryRestoresThem()
    {
        var c=Actor();int ordinary=AbilityStats.Attack(c,false),freeFire=AbilityStats.Attack(c,true),baseline=InterlockInitiative.Attack(c,null);
        try
        {
            Assert.True(AbilityBuffs.Apply(c,1149));
            Assert.Equal(50,InterlockInitiative.Bonus(c));Assert.Equal(baseline+50,InterlockInitiative.Attack(c,null));
            Assert.Equal(ordinary,AbilityStats.Attack(c,false));Assert.Equal(freeFire,AbilityStats.Attack(c,true));
            c.playerInstance.Health.setValue((ushort)400);
            Assert.Equal(baseline+50,InterlockInitiative.Attack(c,null));
            int before=Hits(baseline),during=Hits(InterlockInitiative.Attack(c,null));
            Assert.True(during>before,"Identical isolated rolls must benefit from the real buff");
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds()+21);
            Assert.Equal(baseline,InterlockInitiative.Attack(c,null));Assert.Equal(before,Hits(InterlockInitiative.Attack(c,null)));
        }
        finally {AbilityBuffs.RemoveAll(c);}
    }
    [Fact] public void RealProducerCapUnloadAndSpecialAccuracyRemainSeparate()
    {
        var c=Actor(1);c.playerInstance.Level.setValue((byte)1);
        try
        {
            AbilityPassives.SetLoaded(c,new[]{((ushort)1149,(ushort)1)});
            Assert.True(AbilityBuffs.Apply(c,1149));Assert.Equal(12,InterlockInitiative.Bonus(c));
            var move=new AbilityItem();move.setAbilityID(105);
            Assert.Equal(AbilityStats.SpecialAttack(c,move)+12,InterlockInitiative.Attack(c,move));
            AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
            Assert.Equal(0,InterlockInitiative.Bonus(c));Assert.Equal(AbilityStats.SpecialAttack(c,move),InterlockInitiative.Attack(c,move));
        }
        finally {AbilityBuffs.RemoveAll(c);}
    }
    [Fact] public void EscapeAndDeadBuffCleanupUseTheirExistingPolicies()
    {
        var c=Actor();
        try
        {
            Assert.True(AbilityBuffs.Apply(c,1149));
            var escape=new AbilityItem();escape.setAbilityID(CombatSpecials.SideRollEscape);
            Assert.Equal(6,AbilityCatalog.Find(escape.getAbilityID()).Number("Ability/TestFunction"));
            Assert.Equal(AbilityStats.SpecialAttack(c,escape),InterlockInitiative.Attack(c,escape));
            AbilityBuffs.RemoveAll(c);Assert.Equal(0,InterlockInitiative.Bonus(c));
        }
        finally {AbilityBuffs.RemoveAll(c);}
    }
    [Fact] public void BonusAdditionCannotOverflowOrTurnNegativeInitiativeIntoABenefit()
    {
        Assert.Equal(int.MaxValue,InterlockInitiative.Add(int.MaxValue-1,600));
        Assert.Equal(-30,InterlockInitiative.Add(-30,-100));
        Assert.Equal(20,InterlockInitiative.Add(-30,50));
    }
    static int Hits(int bonus)
    {
        var rng=new Random(731);int hits=0;
        for(int i=0;i<1000;i++)if(CombatInterlock.RollHit(rng,CombatTactic.Speed,50,CombatTactic.Speed,50,out _,out _,CombatTacticType.Normal,bonus,0))hits++;
        return hits;
    }
}
