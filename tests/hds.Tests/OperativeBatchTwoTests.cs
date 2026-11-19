using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class OperativeBatchTwoTests
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

    [Theory]
    [InlineData(338, 1121, 0, 2)]
    [InlineData(367, 1118, 0, 0)]
    [InlineData(389, 1123, 49, 0)]
    public void GrantsReconcileOnLoadUnloadAndRetry(ushort parent, ushort child, int damageDelta, int accuracyDelta)
    {
        var actor = Actor();
        var rows = new[] { (parent, (ushort)50) };
        Assert.False(AbilityPassives.IsLoaded(actor, child));
        AbilityPassives.SetLoaded(actor, rows, newWorld: true);
        Assert.True(AbilityPassives.IsLoaded(actor, child));
        Assert.Equal(1000 + damageDelta, AbilityStats.ScaleAbilityDamage(actor, 1132, 1000));
        Assert.Equal(accuracyDelta, AbilityStats.ThrownAccuracy(actor));
        if (parent == 367) Assert.Equal(5, AbilityStats.Player(actor, 1045));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(actor, child));
        Assert.Equal(1000, AbilityStats.ScaleAbilityDamage(actor, 1132, 1000));
        Assert.Equal(0, AbilityStats.ThrownAccuracy(actor));
        AbilityPassives.SetLoaded(actor, rows);
        Assert.True(AbilityPassives.IsLoaded(actor, child));
    }

    [Fact]
    public void ImprovedInvisibilitySuppliesRankedDefenseAndDetermination()
    {
        var actor = Actor();
        var rows = new[] { ((ushort)363, (ushort)50) };
        int baseDetermination = AbilityControlDurations.Determination(actor);
        AbilityPassives.SetLoaded(actor, rows);
        Assert.Equal(5, AbilityStats.Defense(actor, false));
        Assert.Equal(5, AbilityStats.Defense(actor, true));
        Assert.Equal(baseDetermination + 10, AbilityControlDurations.Determination(actor));
        AbilityPassives.SetLoaded(actor, rows); // Refreshing the same loadout does not accumulate.
        Assert.Equal(5, AbilityStats.Defense(actor, false));
        Assert.Equal(baseDetermination + 10, AbilityControlDurations.Determination(actor));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Defense(actor, false));
        Assert.Equal(0, AbilityStats.Defense(actor, true));
        Assert.Equal(baseDetermination, AbilityControlDurations.Determination(actor));
    }

    [Fact]
    public void LinkedBlindAndStunDurationsUseTheirLoadedBonuses()
    {
        var actor = Actor();
        AbilityPassives.SetLoaded(actor, new[] { ((ushort)339, (ushort)50), ((ushort)384, (ushort)50) });
        Assert.Equal(10f, AbilityControlDurations.Scale(actor, TempMods.BlindAbility, 8), 4);
        Assert.Equal(9.2f, AbilityControlDurations.Scale(actor, TempMods.StunAbility, 8), 4);
        // Enraged has no stored class-4 duration link. Its proposed bonus consumer remains open.
        Assert.Equal(8f, AbilityControlDurations.Scale(actor, TempMods.EnrageAbility, 8));
        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(8f, AbilityControlDurations.Scale(actor, TempMods.StunAbility, 8));
        Assert.Equal(8f, AbilityControlDurations.Scale(actor, TempMods.BlindAbility, 8));
    }

    private sealed class Draw(params double[] rolls) : Random
    {
        private int index;
        public override double NextDouble() => rolls[Math.Min(index++, rolls.Length - 1)];
    }

    [Theory]
    [InlineData(1132, 199, 12, .499, .501)]
    [InlineData(340, 199, 8, .199, .201)]
    [InlineData(340, 200, 8, .199, .201)]
    [InlineData(347, 200, 8, .199, .201)]
    [InlineData(347, 201, 8, .199, .201)]
    [InlineData(348, 201, 12, .499, .501)]
    [InlineData(287, 199, 15, .99, -1)]
    [InlineData(287, 200, 15, .99, -1)]
    [InlineData(287, 201, 15, .99, -1)]
    [InlineData(386, 96, 8, .99, -1)]
    [InlineData(364, 90, 20, .99, -1)]
    [InlineData(1130, 93, 4, .99, -1)]
    public void ControlUsesSourcedDurationAndChance(ushort ability, ushort state, int seconds, double hit, double miss)
    {
        Assert.True(TempMods.TryGet(ability, out var effect));
        if (miss >= 0)
            Assert.DoesNotContain(TimedEffects.Instantiate(effect, new Draw(miss)), m => m.Target == state);
        var added = Assert.Single(TimedEffects.Instantiate(effect, new Draw(hit)), m => m.Target == state);
        Assert.Equal(seconds, added.Seconds);
        var holder = new TimedEffects.Holder();
        holder.Apply(ability, new[] { added }, 100);
        Assert.True(holder.HasAt(state, 100 + seconds - .01));
        Assert.False(holder.HasAt(state, 100 + seconds));
    }

    [Theory]
    [InlineData(.199, .201, 200)]
    [InlineData(.201, .199, 199)]
    public void ThroatCuttingThrowDrawsSeparatelyForOffBalanceAndDazed(double first, double second, ushort expected)
    {
        Assert.True(TempMods.TryGet(340, out var effect));
        Assert.Equal(expected, Assert.Single(TimedEffects.Instantiate(effect, new Draw(first, second))).Target);
    }

    private sealed class Recipient : TimedEffects.Subject
    {
        internal override WorldClient Context => null!;
        internal override bool Alive => true;
        internal override void Applied(TempMods.Effect effect, List<TimedEffects.Active> added,
            TimedEffects.Active immunity, List<TimedEffects.Active> replaced) { }
        internal override bool Ticked(TimedEffects.Active mod, int damage) => true;
        internal override void Ended(List<TimedEffects.Active> ended) { }
    }

    [Theory]
    [InlineData(347, 4)]
    [InlineData(1135, 8)]
    public void ConditionalStunCanRetryAfterClearingAStaleCast(ushort id, int stunSeconds)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        var target = new Recipient();
        double now = TimeUtils.GetSimTimeSeconds() + 90;
        try
        {
            TimedEffects.Apply(target, effect, now, rng: new Draw(.99));
            TimedEffects.RunDue(now + .02);
            Assert.False(target.Effects.HasAt(TempMods.StunAbility, now + .02));
            TimedEffects.RemoveParent(target, id);
            target.Effects.Apply(885, new[] { new TimedEffects.Active
            {
                Target = TempMods.DazeAbility, Proc = TempMods.Proc.State, Seconds = 1
            } }, now + 1);
            TimedEffects.Apply(target, effect, now + 1, rng: new Draw(.99));
            TimedEffects.RemoveParent(target, id); // A canceled throw may leave no terminal work.
            TimedEffects.RunDue(now + 1.02);
            Assert.False(target.Effects.HasAt(TempMods.StunAbility, now + 1.02));
            TimedEffects.Apply(target, effect, now + 1.1, rng: new Draw(.99));
            TimedEffects.RunDue(now + 1.12);
            Assert.True(target.Effects.HasAt(TempMods.StunAbility, now + 1.12));
            Assert.Equal(stunSeconds, Assert.Single(target.Effects.Snapshot(), m => m.Target == TempMods.StunAbility).Seconds);
            Assert.True(target.Effects.HasControlImmunity(now + 1.12));
            if (id == 1135) Assert.True(target.Effects.HasAt(TempMods.RootedAbility, now + 1.12));
        }
        finally { TimedEffects.Clear(target); }
    }

    [Fact]
    public void WardingBlowPenalizesBothAttackInputsUntilExpiry()
    {
        var npc = new Mob();
        var target = TimedEffects.ForMob(npc);
        Assert.True(TempMods.TryGet(1134, out var effect));
        var mods = TimedEffects.Instantiate(effect, new Draw(0));
        target.Effects.Apply(1134, mods, 100);
        Assert.Equal(-30, AbilityStats.Attack(npc, false));
        Assert.Equal(-30, AbilityStats.Attack(npc, true));
        target.Effects.Advance(108, new(), new());
        Assert.Equal(0, AbilityStats.Attack(npc, false));
        Assert.Equal(0, AbilityStats.Attack(npc, true));
    }

    [Fact]
    public void SeverArterySchedulesElevenTicksWithoutASecondTerminalTick()
    {
        Assert.True(TempMods.TryGet(341, out var effect));
        var dot = Assert.Single(TimedEffects.Instantiate(effect, new Draw(0)), m => m.Proc == TempMods.Proc.Dot);
        Assert.Equal(68, dot.Value);
        Assert.Equal(2.5, dot.Interval);
        Assert.Equal(30f, dot.Seconds);
        var holder = new TimedEffects.Holder();
        holder.Apply(341, new[] { dot }, 100);
        var ticks = new List<TimedEffects.Active>();
        var expired = new List<TimedEffects.Active>();
        holder.Advance(130, ticks, expired);
        Assert.Equal(11, ticks.Count);
        Assert.Equal(66, ticks.Sum(tick => tick.TickDamage));
        Assert.Same(dot, Assert.Single(expired));
        holder.Advance(131, ticks, expired);
        Assert.Equal(11, ticks.Count);
    }

    private static WorldClient StealthActor()
    {
        var client = Actor();
        typeof(WorldClient).GetProperty("IsBot")!.SetValue(client, true);
        typeof(WorldClient).GetField("alive", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        client.playerData.isBot = true;
        client.playerData.setOnWorld(true);
        client.playerInstance.InnerStrengthMax.setValue((ushort)100);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)45);
        return client;
    }

    [Fact]
    public void ShadowDrainsThenStopsAndStartsOnlyTheStoredTermRecast()
    {
        var actor = StealthActor();
        Assert.False(AbilityBuffs.CooldownStartsOnUse(1118));
        Assert.True(AbilityCooldowns.TryGet(1118, out var cooldown));
        Assert.Equal(30f, cooldown.Seconds);
        Stealth.Land(actor, 1118, 0);
        var state = Stealth.Current(actor);
        Assert.NotNull(state);
        Assert.Equal(180, state.Seconds);
        Assert.False(AbilityCooldowns.IsCoolingDown(actor.playerData, 1118, state.Started));
        DelayedActions.RunDue(state.Started + 5.1);
        Assert.Equal(20, AbilityHandler.CurrentInnerStrength(actor));
        DelayedActions.RunDue(state.Started + 10.1);
        Assert.Null(Stealth.Current(actor));
        Assert.Equal(20, AbilityHandler.CurrentInnerStrength(actor));
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, 1118, TimeUtils.GetSimTimeSeconds() + 29));
        Assert.False(AbilityCooldowns.IsCoolingDown(actor.playerData, 1118, TimeUtils.GetSimTimeSeconds() + 31));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VanishAftermathDoesNotCrossWorldOrLifeAndNewCastCanRetry(bool newLife)
    {
        var actor = StealthActor();
        Stealth.Land(actor, 368, 0);
        if (newLife) actor.playerData.lifeGeneration++;
        else actor.playerData.worldGeneration++;
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds() + 2.1);
        Assert.Null(Stealth.Current(actor));
        Stealth.Land(actor, 368, 0);
        Assert.Null(Stealth.Current(actor));
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds() + 2.1);
        Assert.Equal((ushort?)1116, Stealth.Current(actor)?.Def.Ability);
        Stealth.Cleared(actor);
    }

    [Fact]
    public void ImpartInvisibilityRefusesSelfAndLeavesExistingStateAlone()
    {
        var actor = StealthActor();
        Assert.Equal("You cannot impart invisibility on yourself.", Stealth.Refusal(actor, 366, 2));
        Stealth.Land(actor, 366, 2);
        Assert.Null(Stealth.Current(actor));
    }

    [Theory]
    [InlineData(287, 300)]
    [InlineData(289, 600)]
    [InlineData(385, 725)]
    [InlineData(386, 900)]
    public void ExistingOpenerDamageRequiresTheStealthFlag(ushort id, int bonus)
    {
        Assert.Equal(30, SneakAttacks.Add(30, id, fromStealth: false));
        Assert.Equal(30 + bonus, SneakAttacks.Add(30, id, fromStealth: true));
    }

    [Fact]
    public void CannyStrikeStillUsesTheThrownDamageConsumerWithAnIndependentBase()
    {
        var actor = Actor();
        Assert.Equal(1000, AbilityStats.ScaleAbilityDamage(actor, 295, 1000));
        Assert.True(AbilityBuffs.Apply(actor, 1120));
        Assert.Equal(1039, AbilityStats.ScaleAbilityDamage(actor, 295, 1000));
        Assert.True(AbilityBuffs.Remove(actor, 1120));
        Assert.Equal(1000, AbilityStats.ScaleAbilityDamage(actor, 295, 1000));
    }
}
