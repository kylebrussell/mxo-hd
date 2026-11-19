using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class OperativeParityTests
{
    private static WorldClient Actor()
    {
        var actor = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        actor.playerData = new ClientData();
        actor.playerData.setDistrictId(1);
        actor.playerInstance = new PlayerCharacter();
        actor.playerInstance.Level.setValue((byte)50);
        actor.playerInstance.Health.setValue((ushort)500);
        actor.playerInstance.MaxHealth.setValue((ushort)500);
        actor.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(actor, long.MaxValue);
        return actor;
    }

    [Fact]
    public void WorldEntryPresentsLatestPassiveOnceAfterReadyWithoutResettingServerTotals()
    {
        var actor = Actor();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(actor, new WorldSendFence());
        Assert.True(AbilityPassives.BeginWorldPresentation(actor));
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)284, (ushort)30) }, newWorld: true);
        actor.playerData.setOnWorld(true);
        AbilityPassives.Refresh(actor);
        Assert.Equal(15, AbilityPassives.Total(actor, 1042));
        Assert.Empty(PassiveAdds());
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)284, (ushort)50) });
        Assert.Equal(25, AbilityPassives.Total(actor, 1042));
        AbilityPassives.LoadingComplete(actor, new byte[] { 0 });
        Assert.Empty(PassiveAdds());
        AbilityPassives.LoadingComplete(actor, new byte[] { 1 });
        Assert.Equal(3, PassiveAdds().Length);
        Assert.False(AbilityPassives.BeginWorldPresentation(actor)); // duplicate spawn input for this owner
        AbilityPassives.LoadingComplete(actor, new byte[] { 1 });
        Assert.Equal(3, PassiveAdds().Length);

        actor.playerData.setOnWorld(false);
        actor.playerData.setOnWorld(true);
        AbilityPassives.LoadingComplete(actor, new byte[] { 1 });
        Assert.Equal(3, PassiveAdds().Length); // the old presentation belongs to a different world
        Assert.True(AbilityPassives.BeginWorldPresentation(actor));
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)284, (ushort)50) }, newWorld: true);
        AbilityPassives.LoadingComplete(actor, new byte[] { 1 });
        Assert.Equal(6, PassiveAdds().Length);

        byte[][] PassiveAdds() => actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Where(message => message.content.AsSpan(1).StartsWith(new byte[] { 0x80, 0xbc, 0x45 }))
            .Select(message => message.content).ToArray();
    }

    [Theory]
    [InlineData(285, 1122, 0)]
    [InlineData(118, 1119, 0)]
    [InlineData(131, 1115, 0)]
    [InlineData(283, 1120, 0)]
    [InlineData(120, 1114, 25)]
    public void LoadedParentControlsChildAvailability(ushort parent, ushort child, ushort level)
    {
        var actor = Actor();
        var loaded = new[] { (parent, (ushort)25) };
        Assert.False(AbilityPassives.IsLoaded(actor, child));
        AbilityPassives.SetLoaded(actor, loaded, newWorld: true);
        Assert.True(AbilityPassives.IsLoaded(actor, child));
        Assert.Equal(level, AbilityPassives.Level(actor, child));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(actor, child));
        AbilityPassives.SetLoaded(actor, loaded);
        Assert.True(AbilityPassives.IsLoaded(actor, child));
        Assert.Equal(level, AbilityPassives.Level(actor, child));
    }

    [Theory]
    [InlineData(284, 200, 0, 0, 5f, 4.48f, 4f, 5f)]
    [InlineData(164, 220, 5, 5, 4f, 4f, 4f, 4f)]
    [InlineData(278, 229, 7, 0, 4f, 4.44f, 4.4f, 4f)]
    public void RankedPassivesFeedDamageContestAndDurationInputs(
        ushort id, int scaledDamage, int accuracy, int ballisticDefense,
        float confuse, float pacify, float root, float powerless)
    {
        var actor = Actor();
        AbilityPassives.SetLoaded(actor, new[] { (id, (ushort)50) });
        Assert.Equal(scaledDamage, AbilityStats.ScaleAbilityDamage(actor, 134, 200));
        Assert.Equal(accuracy, AbilityStats.ThrownAccuracy(actor));
        Assert.Equal(ballisticDefense, AbilityStats.Defense(actor, true));
        Assert.Equal(confuse, AbilityControlDurations.Scale(actor, TempMods.ConfuseAbility, 4), 4);
        Assert.Equal(pacify, AbilityControlDurations.Scale(actor, TempMods.MezAbility, 4), 4);
        Assert.Equal(root, AbilityControlDurations.Scale(actor, TempMods.RootedAbility, 4), 4);
        Assert.Equal(powerless, AbilityControlDurations.Scale(actor, TempMods.PowerlessAbility, 4), 4);
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(200, AbilityStats.ScaleAbilityDamage(actor, 134, 200));
        Assert.Equal(0, AbilityStats.ThrownAccuracy(actor));
        Assert.Equal(0, AbilityStats.Defense(actor, true));
        Assert.Equal(4f, AbilityControlDurations.Scale(actor, TempMods.MezAbility, 4));
    }

    [Theory]
    [InlineData(1122, 211, 5, 5, 0, 0, 0, 0)]
    [InlineData(1119, 205, 5, 0, 10, 0, 0, 0)]
    [InlineData(1115, 200, 3, 0, 0, 3, 3, 10)]
    public void UpgradeIsSelfOnlyAndFeedsItsShippedModifiers(
        ushort id, int scaledDamage, int accuracy, int stealth, int evasion, int defense, int disguiseDuration, int sneakMovement)
    {
        var actor = Actor();
        Assert.True(AbilitySupport.Handles(id));
        Assert.Same(actor, Assert.Single(AbilitySupport.Targets(actor, id, 99)));
        Assert.True(AbilityBuffs.Apply(actor, id));
        Assert.Equal(scaledDamage, AbilityStats.ScaleAbilityDamage(actor, 134, 200));
        Assert.Equal(accuracy, AbilityStats.ThrownAccuracy(actor));
        Assert.Equal(stealth, Stealth.StealthLevelOf(actor));
        Assert.Equal(evasion, AbilityStats.Player(actor, 520));
        Assert.Equal(defense, AbilityStats.Defense(actor, false));
        Assert.Equal(disguiseDuration, AbilityStats.Player(actor, 639));
        Assert.Equal(sneakMovement, AbilityStats.Player(actor, 877));
        Assert.True(AbilityBuffs.Remove(actor, id));
        Assert.Equal(0, AbilityStats.ThrownAccuracy(actor));
        Assert.Equal(0, Stealth.StealthLevelOf(actor));
    }

    [Fact]
    public void StealthCountermeasuresChangesAwarenessContestAndThenClears()
    {
        var actor = Actor();
        actor.playerInstance.StealthAwareness.setValue((byte)1);
        Assert.False(Stealth.SneakTest(Stealth.AwarenessOf(actor), 50, 0, 300, 25));
        Assert.True(AbilityBuffs.TryGet(293, out var buff));
        Assert.Equal(30f, buff.Seconds);
        Assert.Equal(1, Stealth.AwarenessOf(actor));
        Assert.True(AbilityBuffs.Apply(actor, 293));
        Assert.Equal(81, Stealth.AwarenessOf(actor));
        Assert.True(Stealth.SneakTest(Stealth.AwarenessOf(actor), 50, 0, 300, 25));
        Assert.True(AbilityBuffs.Remove(actor, 293));
        Assert.Equal(1, Stealth.AwarenessOf(actor));
    }

    private sealed class ProcDraw(double value) : Random
    {
        public override double NextDouble() => value;
    }

    [Theory]
    [InlineData(107, 199, 12, .149, .151)]
    [InlineData(107, 200, 12, .149, .151)]
    [InlineData(107, 201, 12, .149, .151)]
    [InlineData(1131, 200, 12, .499, .501)]
    [InlineData(1127, 199, 12, .299, .301)]
    [InlineData(1127, 200, 12, .299, .301)]
    [InlineData(1127, 201, 12, .299, .301)]
    [InlineData(279, 199, 8, .199, .201)]
    [InlineData(279, 201, 8, .199, .201)]
    [InlineData(107, 90, 12, .99, -1)]
    [InlineData(122, 93, 10, .99, -1)]
    [InlineData(282, 90, 10, .99, -1)]
    [InlineData(288, 91, 10, .99, -1)]
    [InlineData(292, 91, 4, .99, -1)]
    public void ShippedControlProbabilityAndLifetimeReachTheEffectHolder(
        ushort id, ushort state, float seconds, double success, double failure)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        if (failure >= 0)
            Assert.DoesNotContain(TimedEffects.Instantiate(effect, new ProcDraw(failure)), m => m.Target == state);
        var mod = Assert.Single(TimedEffects.Instantiate(effect, new ProcDraw(success)), m => m.Target == state);
        Assert.Equal(seconds, mod.Seconds);
        var holder = new TimedEffects.Holder();
        holder.Apply(id, new[] { mod }, 100);
        Assert.True(holder.HasAt(state, 100 + seconds - .01));
        Assert.False(holder.HasAt(state, 100 + seconds));
    }

    [Fact]
    public void CartwheelAppliesItsCombatEvasionOnlyToTheCasterWhenLanded()
    {
        var caster = Actor();
        Assert.False(AbilitySupport.Handles(292));
        Assert.True(CombatSpecials.TryCasterBuff(292, out var buff));
        Assert.Equal(30f, buff.Seconds);
        Assert.Equal(0, AbilityStats.Player(caster, 520));
        CombatSpecials.Landed(caster, 292);
        Assert.Equal(25, AbilityStats.Player(caster, 520));
        Assert.True(AbilityBuffs.Remove(caster, 292));
        Assert.Equal(0, AbilityStats.Player(caster, 520));
    }

    [Fact]
    public void CripplingThrowFirstHealthChangeArmsItsRootBreak()
    {
        var holder = new TimedEffects.Holder();
        Assert.True(TempMods.TryGet(280, out var effect));
        var active = TimedEffects.Instantiate(effect, new ProcDraw(0));
        var root = Assert.Single(active, m => m.Target == TempMods.RootedAbility);
        Assert.Equal(15f, root.Seconds);
        Assert.Equal(-40, Assert.Single(active, m => m.Target == 519).Value);
        holder.Apply(280, active, 100);
        Assert.Empty(holder.HealthChanged(1, true, new ProcDraw(0)));
        Assert.True(holder.HasAt(TempMods.RootedAbility, 101));
        Assert.NotEmpty(holder.HealthChanged(1, true, new ProcDraw(0)));
        Assert.False(holder.HasParent(280));
    }

    [Theory]
    [InlineData(134, 1009, -15, 10)]
    [InlineData(1133, 180, -45, 8)]
    [InlineData(1133, 181, -45, 8)]
    public void LandedDebuffChangesTheTargetCombatInputs(ushort id, ushort stat, int value, float seconds)
    {
        var mob = (Mob)RuntimeHelpers.GetUninitializedObject(typeof(Mob));
        var subject = TimedEffects.ForMob(mob);
        Assert.True(TempMods.TryGet(id, out var effect));
        var active = TimedEffects.Instantiate(effect, new ProcDraw(0));
        Assert.Equal(seconds, Assert.Single(active, m => m.Target == stat).Seconds);
        subject.Effects.Apply(id, active, 100);
        Assert.Equal(value, stat == 1009 ? AbilityStats.Defense(mob, false) : AbilityStats.Toughness(mob, stat == 181));
        subject.Effects.Advance(100 + seconds, new List<TimedEffects.Active>(), new List<TimedEffects.Active>());
        Assert.Equal(0, AbilityStats.Npc(mob, stat));
    }

    [Theory]
    [InlineData(153, 150)]
    [InlineData(286, 300)]
    [InlineData(288, 775)]
    public void ExistingSneakOpenerConsumerRequiresCapturedStealthFlag(ushort id, int bonus)
    {
        Assert.Equal(23, SneakAttacks.Add(23, id, fromStealth: false));
        Assert.Equal(23 + bonus, SneakAttacks.Add(23, id, fromStealth: true));
    }

    [Theory]
    [InlineData(8, 35)]
    [InlineData(25, 43)]
    [InlineData(50, 56)]
    public void DisguiseUsesGrantedRankInsteadOfCharacterLevel(ushort learned, int seconds)
    {
        var actor = Actor();
        AbilityPassives.SetLoaded(actor, new[] { (AbilityDisguise.Parent, learned) });
        int rank = Stealth.AbilityLevel(actor, AbilityDisguise.Ability);
        Assert.Equal(learned, rank);
        Assert.Equal(seconds, AbilityDisguise.Seconds(rank, AbilityStats.Player(actor, 639)));
        Assert.True(AbilityDisguise.TryModel(615, out uint model));
        Assert.Equal(1073743155u, model);
        Assert.False(AbilityDisguise.TryModel(0, out _));
    }

    [Fact]
    public void LoadedSixthSenseFeedsItsContestAndUnloadingClearsIt()
    {
        var actor = Actor();
        Assert.False(SixthSense.Detects(0, AbilityStats.Player(actor, SixthSense.Detection), 0));
        AbilityPassives.SetLoaded(actor, new[] { (SixthSense.Ability, (ushort)13) });
        Assert.Equal(52, AbilityStats.Player(actor, SixthSense.Detection));
        Assert.True(SixthSense.Detects(0, AbilityStats.Player(actor, SixthSense.Detection), 0));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.False(SixthSense.Detects(0, AbilityStats.Player(actor, SixthSense.Detection), 0));
    }

    [Theory]
    [InlineData(282, 24, 4)]
    [InlineData(141, 264, 44)]
    public void ThrownDotSchedulesFiveRoundedTicksAtTheStoredInterval(ushort id, int raw, int damage)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        var dot = Assert.Single(TimedEffects.Instantiate(effect, new ProcDraw(0)), m => m.Proc == TempMods.Proc.Dot);
        Assert.Equal(raw, dot.Value);
        Assert.Equal(7, dot.DamageType);
        Assert.Equal(15f, dot.Seconds);
        Assert.Equal(2.5, dot.Interval);
        var holder = new TimedEffects.Holder();
        holder.Apply(id, new[] { dot }, 100);
        var expired = new List<TimedEffects.Active>();
        for (int tick = 1; tick <= 5; tick++)
        {
            var ticks = new List<TimedEffects.Active>();
            double due = 100 + tick * 2.5;
            holder.Advance(due - .01, ticks, expired);
            Assert.Empty(ticks);
            holder.Advance(due, ticks, expired);
            Assert.Equal(damage, Assert.Single(ticks).TickDamage);
        }
        var terminalTicks = new List<TimedEffects.Active>();
        holder.Advance(115, terminalTicks, expired);
        Assert.Empty(terminalTicks);
        Assert.Equal(dot, Assert.Single(expired));
    }
}
