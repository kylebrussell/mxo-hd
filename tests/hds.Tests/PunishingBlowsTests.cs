using hds;
namespace hds.Tests;

[Collection("DelayedActions")]
public class PunishingBlowsTests
{
    private sealed class Target : TimedEffects.Subject
    {
        public bool Living=true;
        public uint Flags;
        internal override WorldClient Context=>null!;
        internal override bool Alive=>Living;
        internal override uint ConditionFlags=>Flags;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active> a,TimedEffects.Active i,List<TimedEffects.Active> r){}
        internal override bool Ticked(TimedEffects.Active m,int damage)=>true;
        internal override void Ended(List<TimedEffects.Active> m){}
    }

    [Theory]
    [InlineData(100,15,true,115)] [InlineData(100,15,false,100)]
    [InlineData(100,0,true,100)] [InlineData(100,-15,true,100)]
    [InlineData(1,15,true,1)] [InlineData(200,30,true,260)]
    [InlineData(65535,15,true,65535)] [InlineData(-1,15,true,0)]
    public void ActivePercentIsAppliedOnceWithoutAnInventedBase(int raw,int stat,bool vulnerable,int expected)
        =>Assert.Equal(expected,PunishingBlows.Amount(raw,stat,vulnerable));

    [Theory]
    [InlineData(0,false)] [InlineData(2,true)] [InlineData(4,true)]
    [InlineData(6,true)] [InlineData(8,false)] [InlineData(0x100000,false)]
    public void OnlyDazeAndOffBalanceCombatFlagsQualify(uint flags,bool expected)
    {
        var target=new Target{Flags=flags};Assert.Equal(expected,PunishingBlows.Vulnerable(target,10));
        target.Living=false;Assert.False(PunishingBlows.Vulnerable(target,10));
    }

    [Theory]
    [InlineData(102,true)] [InlineData(199,true)] [InlineData(200,true)]
    [InlineData(99,false)] [InlineData(201,false)]
    public void LiveControlStateExpiresAndRemovalImmediatelyStopsQualification(ushort state,bool expected)
    {
        var target=new Target();target.Effects.Apply(999,new[]{new TimedEffects.Active{Target=state,Proc=TempMods.Proc.State,Seconds=5}},10);
        Assert.Equal(expected,PunishingBlows.Vulnerable(target,14));
        Assert.False(PunishingBlows.Vulnerable(target,15));
        target.Effects.RemoveParent(999);Assert.False(PunishingBlows.Vulnerable(target,11));
    }

    [Fact]
    public void MissingOwnersAndDeadTargetsCannotGrantDamage()
    {
        Assert.Equal(100,PunishingBlows.Npc(null!,null!,100));
        Assert.Equal(100,PunishingBlows.Player(null!,null!,100));
        Assert.False(PunishingBlows.Vulnerable(null!,10));
    }

    private static WorldClient Client(uint district=1)
    {
        var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData=new ClientData();client.playerInstance=new PlayerCharacter();client.messageQueue=new MessageQueue();
        client.Alive=true;client.playerData.setOnWorld(true);client.playerData.setDistrictId(district);
        client.playerInstance.Level.setValue((byte)50);client.playerInstance.Health.setValue((ushort)1000);
        client.playerInstance.MaxHealth.setValue((ushort)1000);client.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        client.playerInstance.InnerStrengthMax.setValue((ushort)100);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(client,long.MaxValue);
        return client;
    }

    [Fact]
    public void Real16ToggleDrainShortfallAndRemovalControlTheExistingStatConsumer()
    {
        var source=Client();var target=Client();target.playerData.conditionStateFlags=4;
        AbilityPassives.SetLoaded(source,new[]{((ushort)16,(ushort)50)},true);
        Assert.Equal(100,PunishingBlows.Player(source,target,100));
        Assert.True(AbilityBuffs.Apply(source,16));
        Assert.Equal(15,AbilityStats.Player(source,212));
        Assert.Equal(115,PunishingBlows.Player(source,target,100));
        var active=source.playerData.activeBuffs[16];Assert.Equal(1,active.Definition.DrainAmount);Assert.Equal(1,active.Definition.DrainSeconds);
        source.playerInstance.InnerStrengthAvailable.setValue((ushort)1);
        AbilityBuffs.TickDrain(source,16,active.Generation);Assert.Equal(115,PunishingBlows.Player(source,target,100));
        AbilityBuffs.TickDrain(source,16,active.Generation);Assert.Equal(100,PunishingBlows.Player(source,target,100));
        TimedEffects.Clear(TimedEffects.ForPlayer(source));TimedEffects.Clear(TimedEffects.ForPlayer(target));
    }

    [Fact]
    public void CurrentWorldAndLifeStateAreCheckedOnEveryHitWithoutStickyVulnerability()
    {
        var source=Client();var target=Client();
        TimedEffects.ForPlayer(source).Effects.Apply(16,new[]{new TimedEffects.Active{Target=212,Kind=AbilityBuffs.KindStat,Value=15,Seconds=30}},TimeUtils.GetSimTimeSeconds());
        target.playerData.conditionStateFlags=2;Assert.Equal(115,PunishingBlows.Player(source,target,100));
        target.playerData.conditionStateFlags=0;Assert.Equal(100,PunishingBlows.Player(source,target,100));
        target.playerData.conditionStateFlags=4;target.playerData.worldChangePending=true;Assert.Equal(100,PunishingBlows.Player(source,target,100));
        target.playerData.worldChangePending=false;target.playerData.setDistrictId(2);Assert.Equal(100,PunishingBlows.Player(source,target,100));
        target.playerData.setDistrictId(1);target.playerData.isDead=true;Assert.Equal(100,PunishingBlows.Player(source,target,100));
        target.playerData.isDead=false;source.playerData.duelDeath=true;Assert.Equal(100,PunishingBlows.Player(source,target,100));
        source.playerData.duelDeath=false;TimedEffects.Clear(TimedEffects.ForPlayer(source));Assert.Equal(100,PunishingBlows.Player(source,target,100));
        TimedEffects.Clear(TimedEffects.ForPlayer(target));
    }

    [Fact]
    public void DedicatedModeUsesNormal16AndOnlyExistingScopedTargetStatePreparation()
    {
        var mode=NativeQa.NativeQaModes.Find("punishing-blows");Assert.Equal("punishing-blows",mode.Name);
        Assert.Contains((ushort)16,mode.Plan.Abilities);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Equal("1",mode.Plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Contains("OURS",mode.Plan.ReadyMessages.Single());
        Assert.DoesNotContain((ushort)885,mode.Plan.Abilities);
    }
}
