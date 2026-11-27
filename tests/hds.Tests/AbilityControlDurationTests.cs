using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityControlDurationTests
{
    private static WorldClient Client()
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
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(85, 1039)]
    [InlineData(90, 1041)]
    [InlineData(91, 1043)]
    [InlineData(96, 1042)]
    [InlineData(99, 1040)]
    [InlineData(102, 995)]
    public void StateDurationLinksComeFromTheShippedTemplate(ushort state, ushort stat) =>
        Assert.Equal(stat, AbilityControlDurations.BonusStat(state));

    [Fact]
    public void AbsentStateLinksAndNonstatesAreNotInferredFromTheirNames()
    {
        Assert.Equal(0, AbilityControlDurations.BonusStat(93)); // no stored Enrage link
        Assert.Equal(0, AbilityControlDurations.BonusStat(174));
        Assert.Equal(0, AbilityControlDurations.BonusStat(9999));
    }

    [Fact]
    public void StunRootAndPacifyUseTheirOwnBonusAndKeepFractionalSeconds()
    {
        var caster = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (384, 50), (278, 50) });
        Assert.Equal(8.05f, AbilityControlDurations.Scale(caster, 102, 7), 4);
        Assert.Equal(27.5f, AbilityControlDurations.Scale(caster, 99, 25), 4);
        Assert.Equal(6.66f, AbilityControlDurations.Scale(caster, 85, 6), 4);
        Assert.Equal(10, AbilityControlDurations.Scale(caster, 93, 10));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(7, AbilityControlDurations.Scale(caster, 102, 7));
    }

    [Fact]
    public void AppliedDurationControlsExpiryAndDoesNotScaleAnExplicitImmunityTimer()
    {
        var caster = Client(); var victim = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (384, 50) });
        Assert.True(TempMods.TryGet(315, out var effect));
        double now = TimeUtils.GetSimTimeSeconds();
        var applied = TimedEffects.Apply(TimedEffects.ForPlayer(victim), effect, now, caster, "Caster");
        var stun = Assert.Single(applied, m => m.Target == TempMods.StunAbility);
        Assert.Equal(8.05f, stun.Seconds, 4);
        Assert.True(TimedEffects.ForPlayer(victim).Effects.HasAt(TempMods.StunAbility, now + 7.5));
        Assert.False(TimedEffects.ForPlayer(victim).Effects.HasAt(TempMods.StunAbility, now + 8.1));
        Assert.Equal(14, TimedEffects.ImmunityFor(effect, stun).Seconds);
        Assert.Equal(7, effect.Mods.Single(m => m.Target == TempMods.StunAbility).Seconds);
        Assert.Equal(8.05f, BitConverter.ToSingle(PlayerEffects.Record(stun, false), 18), 4);
    }

    [Fact]
    public void AScaledRootRetainsItsDamageBreakRule()
    {
        var caster = Client(); var victim = Client();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (278, 50) });
        Assert.True(TempMods.TryGet(40, out var effect));
        var applied = TimedEffects.Apply(TimedEffects.ForPlayer(victim), effect, TimeUtils.GetSimTimeSeconds(), caster, "Caster");
        var root = Assert.Single(applied, m => m.Target == TempMods.RootedAbility);
        Assert.Equal(27.5f, root.Seconds, 4);
        Assert.Equal(TempMods.BreakRule.Damage, root.Break);
        Assert.Empty(TimedEffects.ForPlayer(victim).Effects.HealthChanged(-1, true, new Random(1)));
        Assert.Contains(root, TimedEffects.ForPlayer(victim).Effects.HealthChanged(1, true, new Random(1)));
    }

    [Theory]
    [InlineData(65, 0x411e6666u)]
    [InlineData(69, 0x41223d71u)]
    public void ArchivedZoneOfPowerlessnessDurationsRequireTheCasterSideBonusInTheCurrentModel(int bonus, uint durationBits)
    {
        var caster = Client();
        var recipient = Client();
        var subject = TimedEffects.ForPlayer(recipient);
        Assert.True(TempMods.TryGet(399, out var effect));
        Assert.Equal(6f, Assert.Single(effect.Mods).Seconds);

        try
        {
            // These inputs discriminate the runtime paths. The capture does not supply either caster's total.
            recipient.playerData.passives.Totals[1042] = bonus;
            Assert.Equal(6f, Apply().Seconds);
            TimedEffects.Clear(subject);

            recipient.playerData.passives.Totals[1042] = 0;
            caster.playerData.passives.Totals[1042] = bonus;
            Assert.Equal(durationBits, BitConverter.ToUInt32(PlayerEffects.Record(Apply(), false), 18));
            TimedEffects.Clear(subject);

            recipient.playerData.passives.Totals[575] = 300;
            Assert.Equal(AbilityControlDurations.Mitigate(BitConverter.UInt32BitsToSingle(durationBits), 50, 300),
                Apply().Seconds, 4);
        }
        finally
        {
            TimedEffects.Clear(subject);
        }

        TimedEffects.Active Apply() => Assert.Single(TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds(), caster),
            mod => mod.Parent == 399 && mod.Target == TempMods.PowerlessAbility);
    }

    [Fact]
    public void Enrage588AppliesTheUnscaledWithdrawRestrictionUntilExpiry()
    {
        const ushort enrage = 588;
        var entry = AbilityCatalog.Find(enrage);
        Assert.Equal(0, entry.Number("Ability/CastDirectObjects"));
        Assert.Equal(3, entry.Number("Ability/CastValidTarget"));
        Assert.Equal(16, entry.Number("Ability/SelectionRange"));
        Assert.Equal(27, entry.Number("Ability/AbilityInnerStrengthCost"));
        Assert.Equal(28, entry.Number("Ability/RecastTime"));
        Assert.Equal(2, entry.CastSeconds);

        var caster = Client();
        var affected = TimedEffects.ForPlayer(Client());
        var other = TimedEffects.ForPlayer(Client());
        Assert.True(TempMods.TryGet(enrage, out var effect));
        var state = Assert.Single(TimedEffects.Apply(affected, effect, TimeUtils.GetSimTimeSeconds(), caster));
        Assert.Equal(TempMods.EnrageAbility, state.Target);
        Assert.Equal(12, state.Seconds);
        Assert.False(ControlActions.CanWithdraw(affected.Effects));
        Assert.True(ControlActions.CanWithdraw(other.Effects));
        Assert.Empty(affected.Effects.HealthChanged(10, true, new Random(1)));
        Assert.False(ControlActions.CanWithdraw(affected.Effects));
        var expired = new List<TimedEffects.Active>();
        affected.Effects.Advance(TimeUtils.GetSimTimeSeconds() + 13, new List<TimedEffects.Active>(), expired);
        Assert.Contains(state, expired);
        Assert.True(ControlActions.CanWithdraw(affected.Effects));
    }

    [Fact]
    public void EnrageSuppressesTheBlockRoundAwardUntilExpiryWithoutLockingTheTactic()
    {
        var blocked = Client();
        var unaffected = Client();
        blocked.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        unaffected.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        blocked.playerData.combatTactic = CombatTactic.Block;
        unaffected.playerData.combatTactic = CombatTactic.Block;
        CombatHandler.ApplyRoundInnerStrength(blocked);
        Assert.Equal((ushort)110, AbilityHandler.CurrentInnerStrength(blocked));

        double now = TimeUtils.GetSimTimeSeconds();
        var subject = TimedEffects.ForPlayer(blocked);
        Assert.True(TempMods.TryGet(588, out var effect));
        var enrage = Assert.Single(TimedEffects.Apply(subject, effect, now, Client()));
        Assert.Equal(TempMods.EnrageAbility, enrage.Target);
        Assert.True(ControlActions.CanChangeTactic(subject.Effects));
        CombatHandler.ApplyRoundInnerStrength(blocked);
        CombatHandler.ApplyRoundInnerStrength(unaffected);
        Assert.Equal((ushort)110, AbilityHandler.CurrentInnerStrength(blocked));
        Assert.Equal((ushort)110, AbilityHandler.CurrentInnerStrength(unaffected));
        Assert.Equal(CombatTactic.Block, blocked.playerData.combatTactic);

        blocked.playerData.combatTactic = CombatTactic.Power;
        CombatHandler.ApplyRoundInnerStrength(blocked);
        Assert.Equal((ushort)110, AbilityHandler.CurrentInnerStrength(blocked));
        blocked.playerData.combatTactic = CombatTactic.Block;
        var expired = new List<TimedEffects.Active>();
        subject.Effects.Advance(now + enrage.Seconds + 1, new List<TimedEffects.Active>(), expired);
        Assert.Contains(enrage, expired);
        CombatHandler.ApplyRoundInnerStrength(blocked);
        Assert.Equal((ushort)120, AbilityHandler.CurrentInnerStrength(blocked));
    }

    [Fact]
    public void ACompletelyReducedTimedStateDoesNotBecomeAnIndefiniteState()
    {
        var caster = Client(); var victim = Client();
        TimedEffects.ForPlayer(caster).Effects.Apply(3000, new[] { new TimedEffects.Active
            { Proc = TempMods.Proc.Stat, Target = 995, Value = -100, Seconds = 30 } }, TimeUtils.GetSimTimeSeconds());
        var effect = new TempMods.Effect { Ability = 315, Mods = new[] { new TempMods.Mod
            { Kind = TempMods.KindState, Target = TempMods.StunAbility, Proc = TempMods.Proc.State, Seconds = 7 } } };
        Assert.Empty(TimedEffects.Apply(TimedEffects.ForPlayer(victim), effect, TimeUtils.GetSimTimeSeconds(), caster));
        Assert.False(TimedEffects.ForPlayer(victim).Effects.Has(TempMods.StunAbility));
    }
}
