using hds;
using hds.shared;
namespace hds.Tests;

[Collection("DelayedActions")]
public class ConditionalControlTests
{
    private sealed class Subject : TimedEffects.Subject
    {
        public bool Living = true;
        public uint Flags;
        public int Applications;
        internal override WorldClient Context => null!;
        internal override bool Alive => Living;
        internal override uint ConditionFlags => Flags;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active> a,TimedEffects.Active i,List<TimedEffects.Active> r) => Applications++;
        internal override bool Ticked(TimedEffects.Active m,int damage) => true;
        internal override void Ended(List<TimedEffects.Active> ended) { }
    }
    private static void Daze(Subject s,double now,float seconds=12) => TimedEffects.Apply(s,new TempMods.Effect {
        Ability=482,Mods=new[]{new TempMods.Mod {Kind=TempMods.KindState,Target=TempMods.DazeAbility,
            Proc=TempMods.Proc.State,Seconds=seconds}}},now);

    [Theory]
    [InlineData(347,4)] [InlineData(1135,8)]
    public void MarkerChecksDazedAtTermThenAddsExactScriptStunAndIndependentTimer(int id,int seconds)
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;
        var s=new Subject();Assert.True(TempMods.TryGet((ushort)id,out var effect));
        var marker=Assert.Single(effect.Mods,m=>m.Proc==TempMods.Proc.Terminal);
        Assert.Equal(.01f,marker.Seconds);Assert.Equal(seconds,marker.MinValue);
        TimedEffects.Apply(s,effect,now,rng:new Random(4));
        Assert.False(s.Effects.Has(TempMods.StunAbility));
        Daze(s,now+.005); // condition arriving after the hit still counts at term
        TimedEffects.RunDue(now+.02);
        var stun=Assert.Single(s.Effects.Snapshot(),m=>m.Target==TempMods.StunAbility);
        Assert.Equal(seconds,stun.Seconds);Assert.Equal((ushort)id,stun.Parent);
        Assert.Equal(TempMods.BreakRule.None,stun.Break);
        var timer=Assert.Single(s.Effects.Snapshot(),m=>m.Parent==TempMods.ControlImmunityTimerAbility);
        Assert.Equal(seconds,timer.Seconds);Assert.Equal(now+.02+seconds,timer.Expires,4);
        if(id==1135) Assert.True(s.Effects.Has(TempMods.RootedAbility)); // append, never replace root/debuff siblings
        TimedEffects.RemoveParent(s,(ushort)id);
        Assert.True(s.Effects.Has(TempMods.ISDrainTargetAbility));
        TimedEffects.RunDue(now+seconds+1);
        Assert.False(s.Effects.Has(TempMods.ISDrainTargetAbility));
        TimedEffects.Clear(s);
    }

    [Theory]
    [InlineData(false,false,false)] [InlineData(true,true,false)] [InlineData(true,false,true)]
    public void NoDazeExistingImmunityOrDeathPreventsStun(bool daze,bool immunity,bool dead)
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var s=new Subject();
        if(daze) Daze(s,now);
        if(immunity) s.Effects.Apply(734,new[]{new TimedEffects.Active {Target=681,Proc=TempMods.Proc.State,Seconds=30}},now);
        Assert.True(TempMods.TryGet(347,out var effect));TimedEffects.Apply(s,effect,now,rng:new Random(4));s.Living=!dead;
        TimedEffects.RunDue(now+.02);Assert.False(s.Effects.Has(TempMods.StunAbility));TimedEffects.Clear(s);
    }

    [Fact]
    public void ExpiredDazeDoesNotCountAndTacticFlagDoesCount()
    {
        double now=TimeUtils.GetSimTimeSeconds()+100;var s=new Subject();Daze(s,now,.005f);
        TempMods.TryGet(347,out var effect);TimedEffects.Apply(s,effect,now,rng:new Random(4));
        TimedEffects.RunDue(now+.02);Assert.False(s.Effects.Has(TempMods.StunAbility));
        s.Flags=CombatInterlock.ConditionBit(CombatTactic.Speed);
        TimedEffects.Apply(s,effect,now+1,rng:new Random(4));TimedEffects.RunDue(now+1.02);
        Assert.True(s.Effects.Has(TempMods.StunAbility));TimedEffects.Clear(s);
    }

    [Fact]
    public void ActualNpcTacticFlagsFeedTheSharedStunAndAttackGate()
    {
        var mob=new Mob();
        typeof(Mob).GetField("conditionFlags",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!
            .SetValue(mob,CombatInterlock.ConditionBit(CombatTactic.Speed));
        var s=TimedEffects.ForMob(mob);double now=TimeUtils.GetSimTimeSeconds()+100;
        TempMods.TryGet(1135,out var effect);TimedEffects.Apply(s,effect,now);
        Assert.False(mob.IsStunned(now));TimedEffects.RunDue(now+.02);
        Assert.True(mob.IsStunned(now+1));Assert.False(mob.IsStunned(now+9));TimedEffects.Clear(s);
    }

    [Fact]
    public void ExplicitTermIgnoresExpiredUnsweptTimerAndSendsItsOffBeforeFreshOn()
    {
        var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData=new ClientData();client.playerInstance=new PlayerCharacter();client.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!
            .SetValue(client,long.MaxValue);
        client.playerData.conditionStateFlags=CombatInterlock.ConditionBit(CombatTactic.Speed);
        var subject=TimedEffects.ForPlayer(client);double now=TimeUtils.GetSimTimeSeconds();
        var stale=new TimedEffects.Active {Kind=TempMods.KindPlain,Target=681,Proc=TempMods.Proc.State,Seconds=1,RecordId=500};
        subject.Effects.Apply(734,new[]{stale},now-2); // expired, deliberately not swept by RunDue
        Assert.True(TempMods.TryGet(1135,out var effect));
        TimedEffects.Apply(subject,effect,now);client.messageQueue.RPCMessagesQueue.Clear();
        // Explicit removal runs its term immediately while the stale734 is still in the holder.
        TimedEffects.RemoveParent(subject,1135);
        var stun=Assert.Single(subject.Effects.Snapshot(),m=>m.Target==102);Assert.Equal(8,stun.Seconds);
        var fresh=Assert.Single(subject.Effects.Snapshot(),m=>m.Parent==734);Assert.NotSame(stale,fresh);
        var packets=client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content).ToList();
        int oldOff=packets.FindIndex(p=>p.AsSpan(1).SequenceEqual(PlayerEffects.Record(stale,true)));
        int newOn=packets.FindIndex(p=>p.AsSpan(1).SequenceEqual(PlayerEffects.Record(fresh,false)));
        Assert.True(oldOff>=0);Assert.True(newOn>oldOff);
        TimedEffects.Clear(subject);
    }

    [Fact]
    public void ExplicitMarkerRemovalRunsTermButDeathClearCancelsIt()
    {
        double now=TimeUtils.GetSimTimeSeconds();var s=new Subject();Daze(s,now);
        TempMods.TryGet(347,out var effect);TimedEffects.Apply(s,effect,now,rng:new Random(4));
        TimedEffects.RemoveParent(s,347);Assert.True(s.Effects.Has(TempMods.StunAbility));
        TimedEffects.Clear(s);Daze(s,now);TimedEffects.Apply(s,effect,now,rng:new Random(4));
        TimedEffects.Clear(s);TimedEffects.RunDue(now+1);Assert.True(s.Effects.IsEmpty);
    }
}
