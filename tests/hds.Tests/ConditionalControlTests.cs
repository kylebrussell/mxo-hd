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
        public List<int> DamageTicks { get; } = new();
        internal override WorldClient Context => null!;
        internal override bool Alive => Living;
        internal override uint ConditionFlags => Flags;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active> a,TimedEffects.Active i,List<TimedEffects.Active> r) => Applications++;
        internal override bool Ticked(TimedEffects.Active m,int damage)
        {
            DamageTicks.Add(damage);
            return true;
        }
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

    private sealed class FixedProcRoll(double roll) : Random
    {
        public int Calls { get; private set; }
        public override double NextDouble()
        {
            Calls++;
            return roll;
        }
    }

    [Theory]
    [InlineData(253, .30f, true)]
    [InlineData(256, .40f, false)]
    public void RifleTermStunsRollOnceAndHonorIndependentImmunity(ushort id, float chance, bool dot) =>
        AssertTermStun(id, chance, dot);

    [Fact]
    public void PistolAerialUsesItsOwnStoredStunRollAndFourSecondImmunity() =>
        AssertTermStun(AbilityConditionalControls.PistolAerial, .40f, false);

    [Fact]
    public void PistolAerialTermMarkerUsesTheArchivedTargetlessAddAndRemoveFrames()
    {
        const ushort pistolAerial = AbilityConditionalControls.PistolAerial;
        Assert.True(TempMods.TryGet(pistolAerial, out var effect));
        var subject = new Subject();
        var marker = Assert.Single(TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds() + 100, rng: new FixedProcRoll(0)));
        try
        {
            marker.RecordId = 0x0502;
            Assert.Equal(pistolAerial, marker.Target);
            Assert.Equal(Convert.FromHexString("80BC1D00020500F3000000000004000000000AD7233C"), PlayerEffects.Record(marker, false));
            Assert.Equal(Convert.FromHexString("80BC1D00020500F3000000000004000000010AD7233C"), PlayerEffects.Record(marker, true));
        }
        finally { TimedEffects.Clear(subject); }
    }

    [Fact]
    public void PistolAerialRepeatDuringImmunityKeepsTheExistingStackUntilExpiry()
    {
        const ushort ability = AbilityConditionalControls.PistolAerial;
        double now = TimeUtils.GetSimTimeSeconds();
        var subject = new Subject();
        Assert.True(TempMods.TryGet(ability, out var effect));
        try
        {
            TimedEffects.Apply(subject, effect, now, rng: new FixedProcRoll(0));
            TimedEffects.RunDue(now + .02);
            var stun = Assert.Single(subject.Effects.Snapshot(), mod => mod.Target == TempMods.StunAbility);
            var penalty = Assert.Single(subject.Effects.Snapshot(), AbilityConditionalControls.IsLinkedStunTactics);
            Assert.Equal(stun.Expires, penalty.Expires);
            TimedEffects.Apply(subject, effect, now + 1, rng: new FixedProcRoll(0));
            TimedEffects.RunDue(now + 1.02);
            Assert.Same(stun, Assert.Single(subject.Effects.Snapshot(), mod => mod.Target == TempMods.StunAbility));
            Assert.Same(penalty, Assert.Single(subject.Effects.Snapshot(), AbilityConditionalControls.IsLinkedStunTactics));
            Assert.Equal(-20, subject.Effects.Total(TempMods.CombatTacticsAbility));
            TimedEffects.RunDue(now + 4.03);
            Assert.True(subject.Effects.IsEmpty);
        }
        finally { TimedEffects.Clear(subject); }
    }

    [Fact]
    public void ThreeRoundBurstStunLowersBothAttackInputsAndRemovesTheSameLinkedRecord()
    {
        const ushort ability = CombatSpecials.ThreeRoundBurst;
        var client = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        var subject = TimedEffects.ForPlayer(client);
        double now = TimeUtils.GetSimTimeSeconds();
        int melee = AbilityStats.Attack(client, ranged: false);
        int ballistic = AbilityStats.Attack(client, ranged: true);
        try
        {
            Assert.True(TempMods.TryGet(ability, out var effect));
            var marker = Assert.Single(TimedEffects.Apply(subject, effect, now, rng: new FixedProcRoll(0)));
            Assert.Equal(ability, marker.Target);
            Assert.True(MxoHd.PacketResearch.ManageBonusRecord.TryDecode(PlayerEffects.Record(marker, false), out var markerRecord));
            Assert.Equal(ability, markerRecord!.TargetId);

            TimedEffects.RunDue(now + .02);
            var stun = Assert.Single(subject.Effects.Snapshot(), mod => mod.Target == TempMods.StunAbility);
            var penalty = Assert.Single(subject.Effects.Snapshot(), AbilityConditionalControls.IsLinkedStunTactics);
            var immunity = Assert.Single(subject.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility);
            Assert.Equal(stun.Expires, penalty.Expires);
            Assert.Equal(4, immunity.Seconds);
            var add = PlayerEffects.Record(penalty, false);
            Assert.True(MxoHd.PacketResearch.ManageBonusRecord.TryDecode(add, out var record));
            Assert.Equal((uint)TempMods.StunAbility, record!.SourceId);
            Assert.Equal(TempMods.CombatTacticsAbility, record.TargetId);
            Assert.Equal(-20, record.SignedValue);
            Assert.Equal(0, record.Duration);
            Assert.Contains(client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
                message => message.content.AsSpan(1).SequenceEqual(add));
            Assert.Equal(melee - 20, AbilityStats.Attack(client, ranged: false));
            Assert.Equal(ballistic - 20, AbilityStats.Attack(client, ranged: true));

            TimedEffects.RunDue(now + 4.03);
            var remove = PlayerEffects.Record(penalty, true);
            Assert.Contains(client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
                message => message.content.AsSpan(1).SequenceEqual(remove));
            Assert.True(subject.Effects.IsEmpty);
            Assert.Equal(melee, AbilityStats.Attack(client, ranged: false));
            Assert.Equal(ballistic, AbilityStats.Attack(client, ranged: true));
        }
        finally { TimedEffects.Clear(subject); }
    }

    private static void AssertTermStun(ushort id, float chance, bool dot)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        var marker = Assert.Single(effect.Mods, mod => mod.Proc == TempMods.Proc.Terminal);
        Assert.Equal(chance, marker.Chance);
        Assert.Equal(.01f, marker.Seconds);
        Assert.Equal(4, marker.MinValue);

        double now = TimeUtils.GetSimTimeSeconds();
        var miss = new Subject();
        var hit = new Subject();
        var immune = new Subject();
        immune.Effects.Apply(734, new[] { new TimedEffects.Active { Target = 681, Proc = TempMods.Proc.State, Seconds = 10 } }, now);
        var missRoll = new FixedProcRoll(chance);
        var hitRoll = new FixedProcRoll(chance - .001);
        var immuneRoll = new FixedProcRoll(0);
        TimedEffects.Apply(miss, effect, now, rng: missRoll);
        TimedEffects.Apply(hit, effect, now, rng: hitRoll);
        TimedEffects.Apply(immune, effect, now, rng: immuneRoll);
        Assert.False(hit.Effects.Has(TempMods.StunAbility));
        TimedEffects.RunDue(now + .02);
        Assert.All(new[] { missRoll, hitRoll, immuneRoll }, roll => Assert.Equal(1, roll.Calls));
        Assert.False(miss.Effects.Has(TempMods.StunAbility));
        Assert.False(immune.Effects.Has(TempMods.StunAbility));
        Assert.Equal(4, Assert.Single(hit.Effects.Snapshot(), mod => mod.Target == TempMods.StunAbility).Seconds);
        Assert.Equal(4, Assert.Single(hit.Effects.Snapshot(), mod => mod.Parent == 734).Seconds);
        Assert.Equal(dot, hit.Effects.Snapshot().Any(mod => mod.Proc == TempMods.Proc.Dot));
        TimedEffects.RunDue(now + 4.03);
        Assert.False(hit.Effects.Has(TempMods.StunAbility));
        Assert.False(hit.Effects.Has(TempMods.ISDrainTargetAbility));
        foreach (var subject in new[] { miss, hit, immune })
            TimedEffects.Clear(subject);
    }

    [Fact]
    public void WoundingShotKeepsItsCapturedDotAndRunsThreeRoundedUpdates()
    {
        double now = TimeUtils.GetSimTimeSeconds();
        var subject = new Subject();
        Assert.True(TempMods.TryGet(253, out var effect));
        var dot = Assert.Single(TimedEffects.Apply(subject, effect, now, rng: new FixedProcRoll(1)));
        Assert.Equal(TempMods.Proc.Dot, dot.Proc);
        Assert.Equal(14, dot.Value); // Capture override; the raw GOB amount is 7.
        Assert.Equal(2.5, dot.Interval);
        TimedEffects.RunDue(now + 2.49);
        Assert.Empty(subject.DamageTicks);
        TimedEffects.RunDue(now + 10.01);
        Assert.Equal(new[] { 4, 4, 4 }, subject.DamageTicks);
        Assert.False(subject.Effects.HasParent(253));
    }

    [Fact]
    public void ImmobilizingShotProtectsTheLandingHitThenUsesItsFiftyPercentBreak()
    {
        var subject = new Subject();
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.True(TempMods.TryGet(254, out var effect));
        var root = Assert.Single(TimedEffects.Apply(subject, effect, now), mod => mod.Target == TempMods.RootedAbility);
        Assert.Equal(8, root.Seconds);
        Assert.Equal(50, root.BreakChance);
        Assert.True(root.ProtectFirstHealthChange);
        Assert.Empty(subject.Effects.HealthChanged(1, true, new Random(1))); // Protected landing hit.
        Assert.Empty(subject.Effects.HealthChanged(1, true, new Random(0))); // Next(100) = 72.
        Assert.False(ControlActions.CanMove(subject.Effects));
        Assert.NotEmpty(subject.Effects.HealthChanged(1, true, new Random(1))); // Next(100) = 24.
        Assert.True(ControlActions.CanMove(subject.Effects));
        TimedEffects.Clear(subject);
    }

    [Theory]
    [InlineData(248, false, 12f)]
    [InlineData(248, true, 21.12f)]
    [InlineData(492, false, 14.08f)]
    public void ConeRootsUseTheirCasterSpecificDurationsAndThenReleaseMovement(ushort id, bool npcCaster, float seconds)
    {
        var subject = new Subject();
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.True(TempMods.TryGet(id, out var effect, npcCaster));
        var root = Assert.Single(TimedEffects.Apply(subject, effect, now));
        Assert.Equal(TempMods.RootedAbility, root.Target);
        Assert.Equal(seconds, root.Seconds);
        Assert.False(ControlActions.CanMove(subject.Effects));
        TimedEffects.RunDue(root.Expires + .01);
        Assert.True(ControlActions.CanMove(subject.Effects));
    }

    [Theory]
    [InlineData(241, 96, 8, .30f)]
    [InlineData(244, 96, 8, .40f)]
    [InlineData(247, 93, 8, .15f)]
    [InlineData(249, 93, 10, .20f)]
    [InlineData(250, 93, 8, .40f)]
    [InlineData(255, 96, 4, .50f)]
    [InlineData(493, 93, 10, .30f)]
    [InlineData(494, 93, 8, .40f)]
    public void GunControlsRespectTheirStoredProbabilityAtLanding(ushort id, ushort state, int seconds, float chance)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        var subject = new Subject();
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.DoesNotContain(TimedEffects.Apply(subject, effect, now, rng: new FixedProcRoll(chance)), mod => mod.Target == state);
        Assert.Equal(seconds, Assert.Single(TimedEffects.Apply(subject, effect, now,
            rng: new FixedProcRoll(chance - .001)), mod => mod.Target == state).Seconds);
        if (id == CombatSpecials.PistolBarrage)
        {
            var immunity = Assert.Single(subject.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility);
            Assert.Equal(seconds, immunity.Seconds);
            Assert.True(subject.Effects.HasControlImmunity(now + 1));
        }
        TimedEffects.RunDue(now + seconds + .01);
        Assert.False(subject.Effects.Has(state));
        if (id == CombatSpecials.PistolBarrage) Assert.False(subject.Effects.HasControlImmunity(now + seconds + .01));
        TimedEffects.Clear(subject);
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
