using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;

[Collection("DelayedActions")]
public class TypedResistanceBonusTests
{
    [Theory][InlineData(2,1148)][InlineData(7,1146)][InlineData(0,0)][InlineData(1,0)][InlineData(3,0)][InlineData(-1,0)]
    public void OnlyTheTwoStoredTypedBonusFamiliesAreConsumed(int type,int expected)=>Assert.Equal(expected,TypedResistanceBonuses.Stat(type));
    [Theory][InlineData(1,10,2)][InlineData(25,10,12)][InlineData(50,10,22)][InlineData(50,130,286)]
    [InlineData(50,-10,-22)][InlineData(1,-1,0)][InlineData(50,0,0)]
    public void OursPointsUseTheExistingResistanceScaleAndSignedTruncation(int level,int percent,int expected)=>Assert.Equal(expected,TypedResistanceBonuses.Points(level,percent));

    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }

    [Fact]public void Real593ProducerLinksAndRemovalFeedTheConsumerOnce()
    {
        var source=Client();var target=Client();
        Assert.Equal(1148,AbilityCatalog.IdOf((uint)AbilityCatalog.Find(157)!.Number("AbilityAttributeLink/AttributeLink")));
        Assert.Equal(1146,AbilityCatalog.IdOf((uint)AbilityCatalog.Find(1033)!.Number("AbilityAttributeLink/AttributeLink")));
        AbilityPassives.SetLoaded(target,new (ushort,ushort)[]{(593,50)});
        Assert.Equal(10,AbilityStats.Player(target,1146));Assert.Equal(10,AbilityStats.Player(target,1148));
        Assert.Equal(94,TypedAbilityResistance.Player(source,target,330,100));
        Assert.Equal(94,TypedAbilityResistance.Player(source,target,1135,100));
        Assert.Equal(401,TypedAbilityResistance.Player(source,target,330,424));
        Assert.Equal(78,TypedAbilityResistance.Player(source,target,1135,83));
        Assert.Equal(100,TypedAbilityResistance.Player(source,target,371,100));
        Assert.Equal(100,TypedAbilityResistance.Player(target,target,330,100));
        AbilityPassives.SetLoaded(target,Array.Empty<(ushort,ushort)>());
        Assert.Equal(100,TypedAbilityResistance.Player(source,target,330,100));Assert.Equal(100,TypedAbilityResistance.Player(source,target,1135,100));
    }

    [Fact]
    public void Rank19AdvancedDamageResistanceRoundTripsFourTypedConsumers()
    {
        const ushort resistance = 376, dedication = 370, rank = 19;
        var opponent = Client();
        var player = Client();
        opponent.playerInstance.Level.setValue((byte)rank);
        player.playerInstance.Level.setValue((byte)rank);
        var loaded = new NativeQa.Modes.StarterHackerDefensivePassivesMode().Plan.Abilities
            .Where(id => id != dedication).Select(id => (Ability: id, Level: rank)).ToArray();
        var archived = loaded.Where(entry => entry.Ability != resistance).ToArray();
        Assert.Contains((resistance, rank), loaded);
        Assert.True(loaded.Sum(entry => AbilityMemory.Cost(entry.Ability)) <= AbilityMemory.ForLevel(rank));
        Assert.Equal(0, TypedAbilityResistance.StoredType(198));
        Assert.Equal(1, TypedAbilityResistance.StoredType(105));
        Assert.Equal(2, TypedAbilityResistance.StoredType(58));
        Assert.Equal(7, TypedAbilityResistance.StoredType(1135));

        try
        {
            Verify(archived, 0, 0, 200, 1000);
            Verify(loaded, 1, 4, 201, 982);
            Verify(archived, 0, 0, 200, 1000);
            Verify(loaded, 1, 4, 201, 982);
        }
        finally
        {
            AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        }

        void Verify((ushort Ability, ushort Level)[] profile, int damagePercent, int resistancePercent, ushort damage, ushort resisted)
        {
            AbilityPassives.SetLoaded(player, profile);
            CombatStyles.Select(player, CombatStyles.HackerStyle);
            Assert.Equal(damagePercent, AbilityStats.Player(player, 1142));
            Assert.Equal(damagePercent, AbilityStats.Player(player, 1139));
            Assert.Equal(resistancePercent, AbilityStats.Player(player, 1148));
            Assert.Equal(resistancePercent, AbilityStats.Player(player, 1146));
            Assert.Equal(damage, AbilityStats.ScaleAbilityDamage(player, 198, 200));
            Assert.Equal(damage, AbilityStats.ScaleAbilityDamage(player, 105, 200));
            Assert.Equal(resisted, TypedAbilityResistance.Player(opponent, player, 58, 1000));
            Assert.Equal(resisted, TypedAbilityResistance.Player(opponent, player, 1135, 1000));
            Assert.Equal(1000, TypedAbilityResistance.Player(opponent, player, 105, 1000));
            Assert.Equal(1000, TypedAbilityResistance.Player(opponent, player, 198, 1000));
        }
    }

    [Fact]public void ExistingRawResistanceAndBothExistingCapsRemainAuthoritative()
    {
        var source=Client();var target=Client();AbilityPassives.SetLoaded(target,new (ushort,ushort)[]{(593,50)});
        Assert.True(AbilityBuffs.Apply(target,5));Assert.Equal(75,AbilityStats.Player(target,1033));
        Assert.Equal(77,TypedAbilityResistance.Player(source,target,1135,100)); //75raw +22points, not a second raw percentage
        TimedEffects.ForPlayer(target).Effects.Apply(2000,new[]{new TimedEffects.Active{Target=1148,Kind=AbilityBuffs.KindStat,Value=500,Seconds=30}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(130,AbilityStats.Player(target,1148));Assert.Equal(52,TypedAbilityResistance.Player(source,target,330,100));
        Assert.Equal(52,TypedAbilityResistance.Amount(100,50,50,TypedResistanceBonuses.Total(240,50,130)));
        Assert.Equal(240,TypedResistanceBonuses.Total(240,50,0));
        AbilityBuffs.RemoveAll(target);
    }

    [Fact]public void NpcTargetsUseTheSameBonusAndTypeIsolationWithoutANewProducer()
    {
        var source=Client();var npc=new Mob();npc.setLevel(50);npc.setHealthC(1000);npc.setHealthM(1000);
        var holder=TimedEffects.ForMob(npc).Effects;
        holder.Apply(593,new[]{new TimedEffects.Active{Target=1146,Kind=AbilityBuffs.KindStat,Value=10,Seconds=30}},TimeUtils.GetSimTimeSeconds());
        Assert.Equal(94,TypedAbilityResistance.Npc(source,npc,1135,100));Assert.Equal(100,TypedAbilityResistance.Npc(source,npc,330,100));
        holder.RemoveParent(593);Assert.Equal(100,TypedAbilityResistance.Npc(source,npc,1135,100));
    }

    [Fact]public void BonusArithmeticSaturatesInsteadOfWrappingAndZeroPreservesTheOldTotal()
    {
        Assert.Equal(int.MaxValue,TypedResistanceBonuses.Points(int.MaxValue,int.MaxValue));
        Assert.Equal(int.MaxValue,TypedResistanceBonuses.Total(int.MaxValue,50,130));
        Assert.Equal(int.MinValue,TypedResistanceBonuses.Total(int.MinValue,50,-130));
        Assert.Equal(-75,TypedResistanceBonuses.Total(-75,50,0));
    }
}
