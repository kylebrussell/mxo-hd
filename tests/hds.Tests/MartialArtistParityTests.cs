using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MartialArtistParityTests
{
    private static WorldClient Actor()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.Health.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)200);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        return client;
    }

    private static AbilityItem Ability(ushort id)
    {
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        return ability;
    }

    private static ushort MaximumHealth(WorldClient actor) =>
        NumericalUtils.ByteArrayToUint16(actor.playerInstance.MaxHealth.getValue(), 1);

    [Theory]
    [InlineData(269, 2, 126, 575)]
    [InlineData(264, 3, 126, 525)]
    public void RankedPassiveFeedsAccuracyDamageAndMaximumHealth(ushort id, int accuracy, int damage, int health)
    {
        var actor = Actor();
        int attack = AbilityStats.Attack(actor, false);
        AbilityPassives.SetLoaded(actor, new[] { (id, (ushort)50) });
        Assert.Equal(attack + accuracy, AbilityStats.Attack(actor, false));
        Assert.Equal(damage, AbilityStats.ScaleAbilityDamage(actor, 267, 123));
        Assert.Equal(health, MaximumHealth(actor));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(attack, AbilityStats.Attack(actor, false));
        Assert.Equal(123, AbilityStats.ScaleAbilityDamage(actor, 267, 123));
        Assert.Equal(500, MaximumHealth(actor));
    }

    [Theory]
    [InlineData(405, 124, 2, 0, 6, 4.4f, 4)]
    [InlineData(531, 130, 0, 11, 12, 4, 4.4f)]
    [InlineData(410, 123, 2, 11, 0, 4, 4)]
    public void FocusPassivesFeedTypedContestDamageAndLinkedControls(ushort id, int damage, int defense,
        int resistance, int viralBonus, float powerless, float stun)
    {
        var actor = Actor();
        var virus = Ability(330);
        int meleeDefense = AbilityStats.Defense(actor, false);
        int viralDefense = AbilityStats.SpecialDefense(actor, virus);
        AbilityPassives.SetLoaded(actor, new[] { (id, (ushort)50) });
        Assert.Equal(damage, AbilityStats.ScaleAbilityDamage(actor, 267, 123));
        Assert.Equal(meleeDefense + defense, AbilityStats.Defense(actor, false));
        Assert.Equal(resistance, AbilityStats.Toughness(actor, false));
        Assert.Equal(viralDefense + viralBonus / 2, AbilityStats.SpecialDefense(actor, virus));
        Assert.Equal(powerless, AbilityControlDurations.Scale(actor, TempMods.PowerlessAbility, 4), 4);
        Assert.Equal(stun, AbilityControlDurations.Scale(actor, TempMods.StunAbility, 4), 4);
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(123, AbilityStats.ScaleAbilityDamage(actor, 267, 123));
        Assert.Equal(meleeDefense, AbilityStats.Defense(actor, false));
    }

    [Theory]
    [InlineData(410, 1069, 1)]
    [InlineData(270, 1067, 2)]
    [InlineData(533, 1066, 1)]
    [InlineData(263, 1065, 1)]
    [InlineData(426, 1064, 1)]
    public void LoadedDisciplineRegistersAndRemovesGrantAndFeedsDesperation(ushort parent, ushort child, int bonus)
    {
        var actor = Actor();
        actor.playerData.setOnWorld(true);
        actor.playerInstance.Health.setValue((ushort)51);
        Assert.True(AbilityBuffs.Apply(actor, 1149));
        Assert.Equal(89, AbilityStats.Player(actor, 1044));
        Assert.True(AbilityBuffs.Remove(actor, 1149));
        AbilityPassives.SetLoaded(actor, new[] { (parent, (ushort)50) });
        AbilityGrantRegistration.LoadingComplete(actor, new byte[] { 1 });
        Assert.True(AbilityBuffs.Apply(actor, 1149));
        Assert.Equal(89 + bonus, AbilityStats.Player(actor, 1044));
        byte[] add = AbilityGrantRegistration.Record(new(parent, child, 0), false);
        Assert.Contains(actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
            message => message.content.AsSpan(1).SequenceEqual(add));

        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityBuffs.IsActive(actor, 1149));
        byte[] remove = AbilityGrantRegistration.Record(new(parent, child, 0), true);
        Assert.Contains(actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
            message => message.content.AsSpan(1).SequenceEqual(remove));
    }

    [Theory]
    [InlineData(1065, 5, 127, 5, 90)]
    [InlineData(1066, 7, 130, 6, 110)]
    [InlineData(1067, 4, 125, 7, 148)]
    [InlineData(1069, 5, 126, 8, 150)]
    public void GrantedUpgradeSelfTargetsAndChangesFourCombatInputs(ushort id, int accuracy, int damage, int defense, int health)
    {
        var actor = Actor();
        Assert.True(AbilitySupport.Handles(id));
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, id, 99)));
        int attack = AbilityStats.Attack(actor, false);
        int defend = AbilityStats.Defense(actor, false);
        Assert.True(AbilityBuffs.Apply(actor, id));
        Assert.Equal(attack + accuracy, AbilityStats.Attack(actor, false));
        Assert.Equal(damage, AbilityStats.ScaleAbilityDamage(actor, 267, 123));
        Assert.Equal(defend + defense, AbilityStats.Defense(actor, false));
        Assert.Equal(500 + health, MaximumHealth(actor));
        Assert.True(AbilityBuffs.Remove(actor, id));
        Assert.Equal(attack, AbilityStats.Attack(actor, false));
        Assert.Equal(defend, AbilityStats.Defense(actor, false));
        Assert.Equal(500, MaximumHealth(actor));
    }

    [Fact]
    public void IronBodyAndSereneCalmReachDistinctConsumersAndClear()
    {
        var actor = Actor();
        Assert.True(AbilityBuffs.Apply(actor, 271));
        Assert.Equal(44, AbilityStats.Toughness(actor, false));
        Assert.Equal(48, AbilityStats.Toughness(actor, true));
        Assert.True(AbilityBuffs.Remove(actor, 271));
        Assert.Equal(0, AbilityStats.Toughness(actor, false));
        Assert.Equal(0, AbilityStats.Toughness(actor, true));

        Assert.True(AbilityBuffs.Apply(actor, 272));
        Assert.Equal(57, AbilityStats.SpecialDefense(actor, Ability(267)));
        Assert.Equal(51, AbilityStats.SpecialDefense(actor, Ability(105)));
        Assert.Equal(54, AbilityStats.SpecialDefense(actor, Ability(330)));
        Assert.Equal(49, AbilityStats.SpecialDefense(actor, Ability(134)));
        Assert.True(AbilityBuffs.Remove(actor, 272));
        Assert.Equal(0, AbilityStats.SpecialDefense(actor, Ability(330)));
    }

    [Theory]
    [InlineData(411, 40, 0, 0)]
    [InlineData(535, 50, 50, 11)]
    public void CasterModifiersApplyOnlyThroughLandedHostileMove(ushort id, int melee, int ballistic, int damage)
    {
        var actor = Actor();
        Assert.False(AbilitySupport.Handles(id));
        Assert.Equal(0, AbilityStats.Toughness(actor, false));
        if (id == 411) Assert.Null(CombatSpecials.Damage(id, null));
        CombatSpecials.Landed(actor, id);
        Assert.Equal(melee, AbilityStats.Toughness(actor, false));
        Assert.Equal(ballistic, AbilityStats.Toughness(actor, true));
        Assert.Equal(100 + damage, AbilityStats.ScaleAbilityDamage(actor, 267, 100));
        Assert.True(AbilityBuffs.Remove(actor, id));
        Assert.Equal(0, AbilityStats.Toughness(actor, false));
    }

    private sealed class ProcDraw(params double[] draws) : Random
    {
        private int cursor;
        public override double NextDouble() => draws[Math.Min(cursor++, draws.Length - 1)];
    }

    private sealed class Recipient : TimedEffects.Subject
    {
        internal override WorldClient Context => null!;
        internal override void Applied(TempMods.Effect effect, List<TimedEffects.Active> added,
            TimedEffects.Active immunity, List<TimedEffects.Active> replaced) { }
        internal override bool Ticked(TimedEffects.Active mod, int damage) => true;
        internal override void Ended(List<TimedEffects.Active> ended) { }
    }

    [Theory]
    [InlineData(406, 93, 15, .19, .21)]
    [InlineData(407, 96, 4, .99, -1)]
    [InlineData(411, 93, 15, .39, .41)]
    [InlineData(412, 93, 6, .99, -1)]
    [InlineData(273, 91, 4, .99, -1)]
    [InlineData(296, 91, 8, .99, -1)]
    [InlineData(536, 96, 6, .49, .51)]
    [InlineData(266, 93, 8, .19, .21)]
    public void LandedStateUsesItsOwnDurationAndProcBoundary(ushort id, ushort state, float seconds, double hit, double miss)
    {
        var target = new Recipient();
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.True(TempMods.TryGet(id, out var effect));
        try
        {
            if (miss >= 0)
                Assert.DoesNotContain(TimedEffects.Apply(target, effect, now, rng: new ProcDraw(miss)), mod => mod.Target == state);
            var result = Assert.Single(TimedEffects.Apply(target, effect, now, rng: new ProcDraw(hit)), mod => mod.Target == state);
            Assert.Equal(seconds, result.Seconds);
            Assert.True(target.Effects.HasAt(state, now + seconds - 0.1));
            Assert.False(target.Effects.HasAt(state, now + seconds + 0.1));
        }
        finally { TimedEffects.Clear(target); }
    }

    [Theory]
    [InlineData(265, .29, .31)]
    [InlineData(532, .99, -1)]
    [InlineData(534, .39, .41)]
    public void TerminalStunAddsIndependentImmunityAndRefusesStacking(ushort id, double hit, double miss)
    {
        var target = new Recipient();
        double now = TimeUtils.GetSimTimeSeconds() + 10;
        Assert.True(TempMods.TryGet(id, out var effect));
        try
        {
            if (miss >= 0)
                Assert.Empty(TimedEffects.Apply(target, effect, now, rng: new ProcDraw(miss)));
            TimedEffects.Apply(target, effect, now, rng: new ProcDraw(hit));
            Assert.False(target.Effects.HasAt(TempMods.StunAbility, now));
            TimedEffects.RunDue(now + .02);
            var stun = Assert.Single(target.Effects.Snapshot(), mod => mod.Target == TempMods.StunAbility);
            var timer = Assert.Single(target.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility);
            Assert.Equal(id, stun.Parent);
            Assert.Equal(4, stun.Seconds);
            Assert.Equal(4, timer.Seconds);
            TimedEffects.Apply(target, effect, now + 1, rng: new ProcDraw(hit));
            TimedEffects.RunDue(now + 1.02);
            Assert.Same(timer, Assert.Single(target.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility));
            TimedEffects.RunDue(now + 4.03);
            Assert.False(target.Effects.HasAt(TempMods.StunAbility, now + 4.03));
            Assert.False(target.Effects.HasControlImmunity(now + 4.03));
        }
        finally { TimedEffects.Clear(target); }
    }

    [Theory]
    [InlineData(274, 522, -60, 16, .29, .31)]
    [InlineData(412, 180, -140, 12, .39, .41)]
    [InlineData(412, 181, -100, 12, .39, .41)]
    [InlineData(267, 180, -64, 12, .99, -1)]
    [InlineData(267, 181, -62, 12, .99, -1)]
    [InlineData(268, 522, -60, 12, .99, -1)]
    [InlineData(268, 524, -60, 12, .99, -1)]
    public void LandedStatFeedsOrdinaryRoundInputAndClears(ushort id, ushort stat, int delta, int duration, double hit, double miss)
    {
        var mob = new Mob();
        var recipient = TimedEffects.ForMob(mob);
        double now = TimeUtils.GetSimTimeSeconds();
        int Consumer() => stat switch
        {
            180 => AbilityStats.Toughness(mob, false),
            181 => AbilityStats.Toughness(mob, true),
            522 => AbilityStats.Attack(mob, false),
            _ => AbilityStats.Attack(mob, true)
        };
        Assert.True(TempMods.TryGet(id, out var effect));
        try
        {
            if (miss >= 0)
                Assert.DoesNotContain(TimedEffects.Apply(recipient, effect, now, rng: new ProcDraw(miss)), mod => mod.Target == stat);
            Assert.Equal(0, Consumer());
            var applied = Assert.Single(TimedEffects.Apply(recipient, effect, now, rng: new ProcDraw(hit)), mod => mod.Target == stat);
            Assert.Equal(duration, applied.Seconds);
            Assert.Equal(delta, Consumer());
            TimedEffects.RunDue(now + duration + .1);
            Assert.Equal(0, Consumer());
        }
        finally { TimedEffects.Clear(recipient); }
    }

    [Theory]
    [InlineData(.39, .41, 180)]
    [InlineData(.41, .39, 181)]
    public void TomoNageRollsEachResistanceProcSeparately(double first, double second, ushort expected)
    {
        var target = new Recipient();
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.True(TempMods.TryGet(412, out var effect));
        try
        {
            var added = TimedEffects.Apply(target, effect, now, rng: new ProcDraw(first, second));
            Assert.Equal(expected, Assert.Single(added, mod => mod.Proc == TempMods.Proc.Stat).Target);
            Assert.Contains(added, mod => mod.Target == TempMods.EnrageAbility);
        }
        finally { TimedEffects.Clear(target); }
    }
}
