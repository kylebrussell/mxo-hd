using System.Runtime.CompilerServices;
using hds;
using HeadlessClient;
namespace hds.Tests;
[Collection("DelayedActions")]
public class ThrownAccuracyConsumerTests
{
    static WorldClient Client(byte level=50)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue(level);c.playerInstance.Health.setValue((ushort)5000);c.playerInstance.MaxHealth.setValue((ushort)5000);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }
    static AbilityItem Move(ushort id){var m=new AbilityItem();m.setAbilityID(id);return m;}
    static void Stat(WorldClient c,ushort id,int value)=>TimedEffects.ForPlayer(c).Effects.Apply(id,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=id,Value=value,Seconds=60}},TimeUtils.GetSimTimeSeconds());
    [Fact]
    public void PrimaryRawLinkAndThreeExistingProducersAreExact()
    {
        Assert.Equal((ushort)1145,AbilityCatalog.IdOf((uint)AbilityCatalog.Find(1032)!.Number("AbilityAttributeLink/AttributeLink")));
        foreach(var pair in new[]{(Id:(ushort)164,Value:10),(Id:(ushort)278,Value:15),(Id:(ushort)338,Value:5)})
        {
            var c=Client();AbilityPassives.SetLoaded(c,new[]{(pair.Id,(ushort)50)});
            Assert.Equal(pair.Value,AbilityStats.Player(c,1145));
            Assert.Equal((int)(50*pair.Value/100f),AbilityStats.ThrownAccuracy(c));
            AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());Assert.Equal(0,AbilityStats.ThrownAccuracy(c));
        }
    }
    [Theory][InlineData(10,1)][InlineData(25,2)][InlineData(50,5)]
    public void Learned164AndCharacterLevelUseEstablishedPercentTruncation(byte level,int delta)
    {
        var c=Client(level);AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(164,50)});
        Assert.Equal(10,AbilityStats.Player(c,1145));Assert.Equal(delta,AbilityStats.SpecialAttack(c,Move(134)));
        Assert.Equal(0,AbilityStats.Attack(c,false));Assert.Equal(0,AbilityStats.Attack(c,true));Assert.Equal(0,AbilityStats.ViralAccuracy(c));
        foreach(ushort id in new ushort[]{105,197,33,121})Assert.Equal(0,AbilityStats.SpecialAttack(c,Move(id)));
        AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());Assert.Equal(0,AbilityStats.SpecialAttack(c,Move(134)));
    }
    [Fact]
    public void SeparateDirectAndBonusCapsApplyOnceAndNegativeValuesKeepTheirSign()
    {
        var c=Client();Stat(c,1032,10000);Stat(c,1145,1000);
        Assert.Equal(259,AbilityStats.Player(c,1032));Assert.Equal(130,AbilityStats.Player(c,1145));Assert.Equal(324,AbilityStats.ThrownAccuracy(c));
        var negative=Client();Stat(negative,1032,20);Stat(negative,1145,-11);Assert.Equal(15,AbilityStats.ThrownAccuracy(negative));
    }
    [Fact]
    public void GeneralBlindAndOtherProducerDamageRemainSeparate()
    {
        var c=Client();Stat(c,1032,20);Stat(c,163,3);AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(164,50)});
        Assert.Equal(28,AbilityStats.SpecialAttack(c,Move(134)));Assert.Equal(3,AbilityStats.Attack(c,false));
        Assert.True(TempMods.TryGet(TempMods.BlindAbility,out var blind));
        TimedEffects.ForPlayer(c).Effects.Apply(TempMods.BlindAbility,blind.Mods.Select(m=>new TimedEffects.Active{Proc=m.Proc,Target=m.Target,Value=m.MinValue,Seconds=m.Seconds}),TimeUtils.GetSimTimeSeconds());
        Assert.Equal(-2,AbilityStats.SpecialAttack(c,Move(134)));
        //164's existing1028 damage producer is not created or consumed again by the accuracy helper.
        Assert.Equal(10,AbilityStats.Player(c,1028));Assert.Equal((ushort)110,AbilityStats.ScaleAbilityDamage(c,279,100));
    }
    [Fact]
    public void ActualStyleTotalsAndPairedProductionDrawsChangeWithoutNewRngConsumption()
    {
        var c=Client();AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(17,50),(1112,50)});c.playerData.combatStyle=1112;AbilityPassives.Refresh(c,false);
        Assert.Equal(66,AbilityStats.Player(c,1032));int off=AbilityStats.SpecialAttack(c,Move(134));
        AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(17,50),(1112,50),(164,50)});Assert.Equal(71,AbilityStats.SpecialAttack(c,Move(134)));
        var a=new Random(731);var b=new Random(731);
        CombatInterlock.RollHit(a,CombatTactic.Speed,50,CombatTactic.Speed,50,out ushort offRoll,out ushort offDef,CombatTacticType.Normal,off,0);
        CombatInterlock.RollHit(b,CombatTactic.Speed,50,CombatTactic.Speed,50,out ushort onRoll,out ushort onDef,CombatTacticType.Normal,AbilityStats.SpecialAttack(c,Move(134)),0);
        Assert.True(onRoll>offRoll);Assert.Equal(offDef,onDef);Assert.Equal(a.Next(),b.Next());
        c.playerData=new ClientData();Assert.Equal(0,AbilityStats.SpecialAttack(c,Move(134))); //No cached1145 or previous owner's passive adoption.
    }
    [Fact]
    public void NormalFixtureHasOwnedPrerequisitesAndNoHitOverride()
    {
        var ids=ThrownAccuracyFixture.Abilities;var rows=ids.Select((id,i)=>new AbilityMemory.Row((uint)i,(ushort)(400+i),0x80000000u|((uint)id<<10),50,id!=164)).ToArray();
        Assert.Null(AbilityLoadQualification.Refusal(rows,new[]{Assert.Single(rows,r=>r.Ability==164)},PlayerProgression.ExperienceForLevel(50),out bool low));Assert.False(low);
        Assert.True(rows.Sum(r=>AbilityMemory.Cost(r.Ability))<=AbilityMemory.ForLevel(50));
        var mode=NativeQa.NativeQaModes.RequireKnown("thrown-accuracy-bonus");Assert.Equal(ids,mode.Plan.Abilities);Assert.False(mode.Plan.FixedCallbackRolls);Assert.True(mode.Plan.DevCommands);
        Assert.DoesNotContain(mode.Plan.EnvironmentOverrides.Keys,k=>k.Contains("ROLL")||k.Contains("ATTACK15"));
        Assert.Contains(HeadlessClient.Scenarios.All,s=>s.Name==mode.Name&&s.Environment!["MXO_PACKET_LOG"]=="1");
    }
    [Fact]
    public void ExactQueued47HasNoSuffixResynchronizationOrUnknownRecordAdmission()
    {
        byte[] bytes=Convert.FromHexString("47000000010100000000000000000000000000001A0006000000010000010001020000860000800000000000000000");
        Assert.Equal(47,bytes.Length);Assert.True(ThrownAccuracyFixture.QueuedLegSweep(bytes));
        for(int n=0;n<47;n++)Assert.False(ThrownAccuracyFixture.QueuedLegSweep(bytes.Take(n).ToArray()));
        Assert.False(ThrownAccuracyFixture.QueuedLegSweep(new byte[]{99}.Concat(bytes).ToArray()));
        foreach(int at in new[]{0,1,4,20,21,22,26,27,28,29,31,35}){var wrong=(byte[])bytes.Clone();wrong[at]^=1;Assert.False(ThrownAccuracyFixture.QueuedLegSweep(wrong));}
    }
}
