using System.Runtime.CompilerServices;
using System.Reflection;
using hds;
using HeadlessClient;
namespace hds.Tests;
[Collection("DelayedActions")]
public class DeflectCodeTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.MaxHealth.setValue((ushort)1000);c.playerInstance.Health.setValue((ushort)1000);
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }
    [Theory][InlineData(85)][InlineData(90)][InlineData(91)][InlineData(93)][InlineData(96)][InlineData(99)][InlineData(102)][InlineData(199)][InlineData(200)][InlineData(201)][InlineData(231)]
    public void OnlyExplicitKnownControlsConsumeThePositiveBonus(ushort state)
    {Assert.True(DeflectCode.KnownControl(state));Assert.Equal(6.3f,DeflectCode.Duration(7,50,0,10,state),4);}
    [Theory][InlineData(0)][InlineData(108)][InlineData(1157)][InlineData(681)][InlineData(9999)]
    public void OtherTargetsRetainExactExistingRawMitigation(ushort state)
    {Assert.False(DeflectCode.KnownControl(state));Assert.Equal(AbilityControlDurations.Mitigate(7,50,275),DeflectCode.Duration(7,50,275,130,state));}
    [Fact]public void RawThresholdCasterLevelAndTotalFloorRemainIndependent()
    {
        Assert.Equal(7,AbilityControlDurations.Mitigate(7,50,10));
        Assert.Equal(4.221f,DeflectCode.Duration(7,50,275,10,102),4);
        Assert.Equal(2.31f,DeflectCode.Duration(7,50,300,10,102),4);
        Assert.Equal(2.31f,DeflectCode.Duration(7,50,0,500,102),4);
        Assert.Equal(7,DeflectCode.Duration(7,50,0,-10,102));Assert.Equal(0,DeflectCode.Duration(0,50,0,10,102));
        Assert.Equal(-1,DeflectCode.Duration(-1,50,0,10,102));Assert.Equal(130,DeflectCode.CappedBonus(500));
    }
    [Fact]public void RealLoaded23ShortensNewStunButNotImmunityOrExistingSnapshots()
    {
        var caster=Client();var target=Client();AbilityPassives.SetLoaded(target,new(ushort,ushort)[]{(23,50)});
        Assert.Equal(10,AbilityStats.Player(target,1047));Assert.Equal(0,AbilityControlDurations.Determination(target));
        Assert.True(TempMods.TryGet(315,out var effect));double now=TimeUtils.GetSimTimeSeconds();
        var subject=TimedEffects.ForPlayer(target);var state=Assert.Single(TimedEffects.Apply(subject,effect,now,caster),m=>m.Target==102);
        Assert.Equal(6.3f,state.Seconds,4);var timer=Assert.Single(subject.Effects.Snapshot(),m=>m.Parent==734);Assert.Equal(14,timer.Seconds);
        AbilityPassives.SetLoaded(target,Array.Empty<(ushort,ushort)>());Assert.Equal(0,AbilityStats.Player(target,1047));Assert.Equal(6.3f,state.Seconds,4);
        Assert.True(subject.Effects.HasAt(102,now+6.2));Assert.False(subject.Effects.HasAt(102,now+6.4));Assert.True(subject.Effects.HasAt(681,now+13.9));
        Assert.DoesNotContain(TimedEffects.Apply(subject,effect,now+8,caster),m=>m.Target==102); //real immunity is not bypassed
        TimedEffects.Clear(subject);
        Assert.Equal(7,Assert.Single(TimedEffects.Apply(subject,effect,now+15,caster),m=>m.Target==102).Seconds);
        TimedEffects.Clear(subject);
    }

    [Fact]
    public void Rank19ClearHeadChangesNewHostileStunsAndHealingAcrossArchiveStates()
    {
        const ushort clearHead = 233, advancedDamageResistance = 376, rank = 19;
        var caster = Client();
        var target = Client();
        caster.playerInstance.Level.setValue((byte)rank);
        target.playerInstance.Level.setValue((byte)rank);
        var loaded = new NativeQa.Modes.StarterHackerDefensivePassivesMode().Plan.Abilities
            .Where(id => id != advancedDamageResistance).Select(id => (Ability: id, Level: rank)).ToArray();
        var archived = loaded.Where(entry => entry.Ability != clearHead).ToArray();
        Assert.Contains((clearHead, rank), loaded);
        Assert.True(TempMods.TryGet(315, out var effect));
        var subject = TimedEffects.ForPlayer(target);
        double now = TimeUtils.GetSimTimeSeconds();

        try
        {
            var initial = Sample(loaded, 3, 24, 6.79f);
            AbilityPassives.SetLoaded(target, archived);
            Assert.Equal(0, AbilityStats.Player(target, 1047));
            Assert.Equal(6.79f, initial.Seconds, 4);
            Assert.True(subject.Effects.HasAt(TempMods.StunAbility, now + 6.78));
            Assert.False(subject.Effects.HasAt(TempMods.StunAbility, now + 6.80));
            Assert.True(subject.Effects.HasControlImmunity(now + 6.80));
            Assert.Empty(TimedEffects.Apply(subject, effect, now + 8, caster));

            TimedEffects.Clear(subject);
            Sample(archived, 0, 23, 7);
            TimedEffects.Clear(subject);
            Sample(loaded, 3, 24, 6.79f);
        }
        finally
        {
            TimedEffects.Clear(subject);
            AbilityPassives.SetLoaded(target, Array.Empty<(ushort, ushort)>());
        }

        TimedEffects.Active Sample((ushort Ability, ushort Level)[] entries, int determination, ushort heal, float duration)
        {
            AbilityPassives.SetLoaded(target, entries);
            CombatStyles.Select(target, CombatStyles.HackerStyle);
            Assert.Equal(0, AbilityControlDurations.Determination(target));
            Assert.Equal(determination, AbilityStats.Player(target, 1047));
            Assert.Equal(heal, AbilityHealing.Amount(target, target, 55));
            var result = TimedEffects.Apply(subject, effect, now, caster);
            var stun = Assert.Single(result, mod => mod.Target == TempMods.StunAbility);
            Assert.Equal(duration, stun.Seconds, 4);
            var immunity = Assert.Single(subject.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility);
            Assert.Equal(14, immunity.Seconds);
            Assert.True(subject.Effects.HasControlImmunity(now + 13.99));
            Assert.False(subject.Effects.HasControlImmunity(now + 14.01));
            return stun;
        }
    }

    [Fact]public void FriendlySelfStatAndDotDurationsDoNotConsume1047()
    {
        var caster=Client();var target=Client();AbilityPassives.SetLoaded(target,new(ushort,ushort)[]{(23,50)});
        List<TimedEffects.Active> Apply(ushort ability,TempMods.Proc proc,ushort state,bool self=false)=>TimedEffects.Apply(TimedEffects.ForPlayer(target),
            new TempMods.Effect{Ability=ability,Mods=new[]{new TempMods.Mod{Kind=TempMods.KindState,Proc=proc,Target=state,Seconds=7}}},TimeUtils.GetSimTimeSeconds(),self?target:caster);
        Assert.Equal(7,Assert.Single(Apply(967,TempMods.Proc.State,99)).Seconds);TimedEffects.Clear(TimedEffects.ForPlayer(target));
        Assert.Equal(7,Assert.Single(Apply(40,TempMods.Proc.State,99,true)).Seconds);TimedEffects.Clear(TimedEffects.ForPlayer(target));
        Assert.Equal(7,Assert.Single(Apply(315,TempMods.Proc.Stat,102)).Seconds);TimedEffects.Clear(TimedEffects.ForPlayer(target));
        Assert.Equal(7,Assert.Single(Apply(474,TempMods.Proc.Dot,1157)).Seconds);TimedEffects.Clear(TimedEffects.ForPlayer(target));
    }
    [Fact]public void DelayedSeverChildrenReadBonusAtActualChildCreation()
    {
        var caster=Client();var target=Client();var subject=TimedEffects.ForPlayer(target);
        var marker=new TimedEffects.Active{Parent=341,Caster=caster,Value=10};
        Assert.Equal(10,AbilitySeverArteryStates.ChildSeconds(subject,marker,199));
        AbilityPassives.SetLoaded(target,new(ushort,ushort)[]{(23,50)});Assert.Equal(9,AbilitySeverArteryStates.ChildSeconds(subject,marker,199));
        Assert.Equal(9,AbilitySeverArteryStates.ChildSeconds(subject,marker,200));
        AbilityPassives.SetLoaded(target,Array.Empty<(ushort,ushort)>());Assert.Equal(10,AbilitySeverArteryStates.ChildSeconds(subject,marker,199));
    }
    [Fact]public void CompleteStateRecordKeepsDurationAndRejectsWrongIdentityOrTruncation()
    {
        var b=Convert.FromHexString("80bc11000101003b010000660000000000009a99c940");
        Assert.True(DeflectCodeFixture.State(b,315,102,false,out float duration));Assert.Equal(6.3f,duration);
        for(int i=0;i<b.Length;i++)Assert.False(DeflectCodeFixture.State(b[..i],315,102,false,out _));
        Assert.False(DeflectCodeFixture.State(b,315,102,true,out _));Assert.False(DeflectCodeFixture.State(b,315,99,false,out _));
        Assert.False(DeflectCodeFixture.State(b,40,102,false,out _));Assert.False(DeflectCodeFixture.State(b.Concat(new byte[]{0}).ToArray(),315,102,false,out _));
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,0,-1}){var copy=b.ToArray();BitConverter.GetBytes(bad).CopyTo(copy,18);Assert.False(DeflectCodeFixture.State(copy,315,102,false,out _));}
    }
    [Fact]public void ModeAndDefenderUseTheSameFixedPrerequisitesAndStrictControls()
    {
        foreach(var item in AbilityPrerequisites.Current.Closure(23))Assert.Contains(item.Key,DeflectCodeFixture.DefenderAbilities);
        var mode=NativeQa.NativeQaModes.Find("deflect-code-duration");Assert.Equal("deflect-code-duration",mode.Name);
        Assert.Equal(DeflectCodeFixture.DefenderAbilities,mode.Plan.Abilities);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Contains("OURS",mode.Plan.ReadyMessages.Single());Assert.DoesNotContain("MXO_CALLBACK_QA_ROLL100",mode.Plan.EnvironmentOverrides.Keys);
        Assert.True(DeflectCodeFixture.DefenderAbilities.Sum(id=>AbilityCatalog.Find(id)!.Number("Ability/InventoryMemCost"))<=AbilityMemory.ForLevel(50));
    }
    [Fact]public void FreshCastWindowRetainsEarlierImmunityWithoutAdoptingOldRecords()
    {
        var immunity=Convert.FromHexString("80bc0100130100de020000a902000000000000006041");
        var stun=Convert.FromHexString("80bc11001201003b010000660000000000000000e040");
        var stale=immunity.ToArray();BitConverter.GetBytes(8f).CopyTo(stale,18);
        var records=new[]{stale,immunity,stun};
        Assert.True(DeflectCodeFixture.State(immunity,734,681,false,out float duration));Assert.Equal(14,duration);
        Assert.Same(immunity,DeflectCodeFixture.FreshState(records,1,734,681));
        Assert.Same(stun,DeflectCodeFixture.FreshState(records,1,315,102));
        Assert.Null(DeflectCodeFixture.FreshState(records,3,734,681));
        Assert.Null(DeflectCodeFixture.FreshState(records,1,734,102));
        Assert.Throws<ArgumentOutOfRangeException>(()=>DeflectCodeFixture.FreshState(records,-1,734,681));
        var off=immunity.ToArray();off[17]=1;Assert.Null(DeflectCodeFixture.FreshState(new[]{off},0,734,681));
        Assert.Null(DeflectCodeFixture.FreshState(new[]{immunity[..^1]},0,734,681));
        var actual=AbilityBuffs.BuildModifier(1,0x113,734,681,0,false,14);
        Assert.Equal(immunity,actual);actual[17]=1;
        Assert.Equal(AbilityBuffs.BuildModifier(1,0x113,734,681,0,true,14),actual);
    }
}
