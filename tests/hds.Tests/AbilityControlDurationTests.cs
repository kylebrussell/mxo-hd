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
