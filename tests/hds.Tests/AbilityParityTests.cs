using hds;
using hds.shared;
using System.Runtime.CompilerServices;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityParityTests
{
    private static WorldClient Client()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.Health.setValue((ushort)100);
        client.playerInstance.InnerStrengthMax.setValue((ushort)500);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)500);
        client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 0));
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static ushort Value(hds.Attribute attribute) => NumericalUtils.ByteArrayToUint16(attribute.getValue(), 1);
    private static TimedEffects.Active State(ushort target, float seconds = 30) => new()
    { Kind = TempMods.KindState, Target = target, Proc = TempMods.Proc.State, Seconds = seconds };

    [Fact]
    public void ShippedTemplatesResolveAliasesAndPreserveCapturedOverrides()
    {
        Assert.Equal(1404, AbilityCatalog.All.Count);
        Assert.Same(AbilityCatalog.Find(359), AbilityCatalog.Find(359 + 4096));
        Assert.Same(AbilityCatalog.Find(359), AbilityCatalog.Find(359 + 8192));
        Assert.Equal(359, AbilityCatalog.IdOf(0x80000000u | (359u << 10)));
        Assert.Equal(10, AbilityCosts.Cost(77));
        Assert.Equal(AbilityCosts.Retail[329], AbilityCosts.Cost(329));
        Assert.True(AbilityCooldowns.TryGet(315, out var sourced));
        Assert.Equal(8f, sourced.Seconds);
        Assert.True(AbilityCooldowns.TryGet(359, out var captured));
        Assert.Equal(AbilityCooldowns.Retail[359].Seconds, captured.Seconds);
        Assert.True(AbilityCatalog.Find(77).Healing);
        Assert.False(AbilityCatalog.Find(77).DirectDamage);
        Assert.True(AbilityCatalog.Find(359).DirectDamage);
        Assert.False(AbilityCatalog.Find(967).DirectDamage);
        Assert.Contains((ushort)17, AbilityProgression.GrantsOf(2));
    }

    [Theory]
    [InlineData(315, 7, 14)]
    [InlineData(333, 5, 10)]
    public void StunDurationAndImmunityComeFromTheShippedTemplate(ushort id, float seconds, float immunity)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        var state = Assert.Single(effect.Mods, m => m.Target == TempMods.StunAbility);
        Assert.Equal(seconds, state.Seconds);
        Assert.Equal(immunity, effect.ImmunitySeconds);
        Assert.Equal(immunity, TimedEffects.ImmunityFor(effect, TimedEffects.Instantiate(effect, new Random(1))
            .Single(m => m.Target == TempMods.StunAbility)).Seconds);
    }

    [Theory]
    [InlineData(99, false, true, true, true)]
    [InlineData(96, true, false, true, true)]
    [InlineData(231, true, true, false, true)]
    [InlineData(93, true, true, true, false)]
    [InlineData(85, false, false, false, false)]
    [InlineData(102, false, false, false, false)]
    public void ControlStatesGateTheirOwnActionsAndExpire(ushort state, bool move, bool ability, bool weapon, bool withdraw)
    {
        var holder = new TimedEffects.Holder();
        double now = TimeUtils.GetSimTimeSeconds();
        holder.Apply(3000, new[] { State(state) }, now);
        Assert.Equal(move, ControlActions.CanMove(holder));
        Assert.Equal(ability, ControlActions.CanUseAbilities(holder));
        Assert.Equal(weapon, ControlActions.CanUseWeapons(holder));
        Assert.Equal(withdraw, ControlActions.CanWithdraw(holder));
        holder.Apply(3000, new[] { State(state) }, now - 31);
        Assert.True(ControlActions.CanMove(holder)); // no timer-tick gap after expiry
        Assert.True(ControlActions.CanUseAbilities(holder));
        Assert.True(ControlActions.CanUseWeapons(holder));
    }

    [Fact]
    public void DamageBreaksWholePacifyParentAndRetainsItsImmunity()
    {
        var holder = new TimedEffects.Holder();
        var mez = State(TempMods.MezAbility); mez.Break = TempMods.BreakRule.Damage;
        holder.Apply(800, new[] { mez, new TimedEffects.Active { Proc = TempMods.Proc.Stat, Target = 115, Value = -50, Seconds = 30 } }, 0);
        holder.Apply(734, new[] { State(681) }, 0);
        Assert.Empty(holder.HealthChanged(-10, true, new Random(1))); // healing doesn't break damage-only pacify
        Assert.Empty(holder.HealthChanged(0, true, new Random(1)));
        Assert.Equal(2, holder.HealthChanged(1, true, new Random(1)).Count);
        Assert.False(holder.HasParent(800));
        Assert.True(holder.HasParent(734));
    }

    [Fact]
    public void ChanceRootProtectsItsFirstDamageThenBreaksAtTheScriptThreshold()
    {
        var effect = AbilityCatalog.Controls(1135);
        var root = Assert.Single(effect.Mods, m => m.Target == TempMods.RootedAbility);
        Assert.Equal(TempMods.BreakRule.Chance, root.Break);
        Assert.True(root.ProtectFirstHealthChange);
        Assert.Equal(100, root.BreakChance);
        var holder = new TimedEffects.Holder();
        holder.Apply(1135, TimedEffects.Instantiate(effect, new Random(1)), 0);
        Assert.Empty(holder.HealthChanged(2, true, new Random(1)));
        Assert.True(holder.Has(TempMods.RootedAbility));
        Assert.NotEmpty(holder.HealthChanged(1, true, new Random(1)));
        Assert.False(holder.HasParent(1135));
        holder.Apply(1135, TimedEffects.Instantiate(effect, new Random(1)), 0);
        Assert.Empty(holder.HealthChanged(-1, true, new Random(1))); // healing flips the signed proc value, too
        Assert.NotEmpty(holder.HealthChanged(1, true, new Random(1)));
    }

    [Fact]
    public void SelfHealIgnoresHostileSelectionAndCapsAtMaximum()
    {
        var client = Client();
        Assert.Same(client, Assert.Single(AbilitySupport.Targets(client, 967, 100)));
        AbilitySupport.Land(client, 967, 100);
        Assert.Equal(150, Value(client.playerInstance.Health));
        client.playerInstance.Health.setValue((ushort)490);
        AbilitySupport.Land(client, 967, 100);
        Assert.Equal(500, Value(client.playerInstance.Health));
        client.playerData.isDead = true;
        client.playerInstance.Health.setValue((ushort)0);
        Assert.Empty(AbilitySupport.Targets(client, 967, 2));
        Assert.Equal(0, AbilityHandler.HealSelf(client, 100));
    }

    [Fact]
    public void HotTicksAtTheScriptIntervalAndDoesNotTickAtExpiry()
    {
        var client = Client();
        AbilitySupport.Land(client, 169, 2);
        var hot = Assert.Single(TimedEffects.ForPlayer(client).Effects.Snapshot());
        Assert.Equal(TempMods.Proc.Hot, hot.Proc);
        Assert.Equal(100, Value(client.playerInstance.Health)); // no immediate quarter-max heal
        Assert.Equal(15f, hot.Seconds);
        Assert.Equal(2.5, hot.Interval);
        TimedEffects.RunDue(hot.Start + 2.51);
        Assert.Equal(121, Value(client.playerInstance.Health));
        TimedEffects.RunDue(hot.Start + 15.1);
        Assert.Equal(205, Value(client.playerInstance.Health)); // five rounded 21-point ticks
        Assert.True(TimedEffects.ForPlayer(client).Effects.IsEmpty);
    }

    [Fact]
    public void ClassBuffReplacementAppliesVitalDeltaAtomicallyAndRemovalClamps()
    {
        var client = Client();
        AbilityBuffs.Apply(client, 1064); // captured +137 HP
        Assert.Equal(637, Value(client.playerInstance.MaxHealth));
        client.playerInstance.Health.setValue((ushort)600);
        AbilityBuffs.Apply(client, 1067); // captured +148, same class family
        Assert.Equal(648, Value(client.playerInstance.MaxHealth));
        Assert.Equal(600, Value(client.playerInstance.Health));
        AbilityBuffs.Apply(client, 1064);
        Assert.Equal(637, Value(client.playerInstance.MaxHealth));
        Assert.Equal(600, Value(client.playerInstance.Health));
        Assert.False(AbilityBuffs.IsActive(client, 1067));
        Assert.Equal(137, AbilityBuffs.VitalTotal(client, 106));
        AbilityBuffs.Remove(client, 1064);
        Assert.Equal(500, Value(client.playerInstance.MaxHealth));
        Assert.Equal(500, Value(client.playerInstance.Health));
    }

    [Fact]
    public void DuelDefeatBlocksMovementAbilitiesAndHealingUntilReconstruction()
    {
        var client = Client();
        client.playerData.duelDeath = true;
        Assert.False(ControlActions.CanMove(client));
        Assert.False(ControlActions.CanUseAbilities(client));
        Assert.Empty(AbilitySupport.Targets(client, 967, 2));
        Assert.Equal(100, AbilityHandler.HealSelf(client, 100));
    }

    [Fact]
    public void NaturalRegenerationRunsHealthChangeCallbacks()
    {
        var client = Client();
        client.playerData.setOnWorld(true);
        var root = State(TempMods.RootedAbility); root.Break = TempMods.BreakRule.HealthChange;
        TimedEffects.ForPlayer(client).Effects.Apply(3000, new[] { root }, TimeUtils.GetSimTimeSeconds());
        HealthRegen.Tick(client, 1);
        Assert.Equal(105, Value(client.playerInstance.Health));
        Assert.False(TimedEffects.ForPlayer(client).Effects.HasParent(3000));
    }

    [Fact]
    public void NegativeVitalBuffRestoresTheUnbuffedMaximumAfterClampingAndLevelGain()
    {
        var client = Client();
        client.playerInstance.InnerStrengthMax.setValue((ushort)200);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        AbilityBuffs.Apply(client, 383); // captured Force Enhancer: -317 maximum IS
        Assert.Equal(1, Value(client.playerInstance.InnerStrengthMax));
        Assert.Equal(200, AbilityBuffs.UnbuffedMaximum(client, 109));
        AbilityBuffs.Apply(client, 383); // refresh must not lose the baseline
        Assert.Equal(200, AbilityBuffs.UnbuffedMaximum(client, 109));
        client.playerInstance.InnerStrengthMax.setValue((ushort)11); // +10 from a level gain
        AbilityBuffs.RefreshVitals(client);
        Assert.Equal(1, Value(client.playerInstance.InnerStrengthMax));
        Assert.Equal(210, AbilityBuffs.UnbuffedMaximum(client, 109));
        AbilityBuffs.Remove(client, 383);
        Assert.Equal(210, Value(client.playerInstance.InnerStrengthMax));
    }

    [Fact]
    public void CombatStatsChangeActualRollsAndDamageForTheSameRandomSequence()
    {
        CombatInterlock.RollHit(new Random(10), CombatTactic.Power, 50, CombatTactic.Power, 50, out var attack, out var defense);
        CombatInterlock.RollHit(new Random(10), CombatTactic.Power, 50, CombatTactic.Power, 50, out var boostedAttack, out var boostedDefense,
            CombatTacticType.Normal, 100, 100);
        Assert.True(boostedAttack > attack);
        Assert.True(boostedDefense > defense);
        var baseDamage = CombatCalculator.CloseCombatDamage(new Random(10), 50, 50, CombatTactic.Power, out _);
        var bonusDamage = CombatCalculator.CloseCombatDamage(new Random(10), 50, 50, CombatTactic.Power, out _, CombatTacticType.Normal, 100);
        var resisted = CombatCalculator.CloseCombatDamage(new Random(10), 50, 50, CombatTactic.Power, out _, CombatTacticType.Normal, 0, 100);
        Assert.True(bonusDamage > baseDamage);
        Assert.True(resisted < baseDamage);
    }

    [Theory]
    [InlineData(100, 495, 0, true)]
    [InlineData(401, 495, 0, false)]
    [InlineData(0, 895, 0, false)]
    public void AreaEffectsRespectRadiusAndFloor(double x, double y, double z, bool included) =>
        Assert.Equal(included, AbilityTargets.WithinArea(0, 495, 0, x, y, z, 4));
}
