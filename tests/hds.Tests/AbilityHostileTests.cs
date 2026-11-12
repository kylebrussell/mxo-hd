using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityHostileTests
{
    private static WorldClient Client(ushort health = 1000, ushort innerStrength = 200)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.MaxHealth.setValue(health);
        client.playerInstance.Health.setValue(health);
        client.playerInstance.InnerStrengthMax.setValue(innerStrength);
        client.playerInstance.InnerStrengthAvailable.setValue(innerStrength);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static List<TimedEffects.Active> Land(WorldClient victim, ushort ability, WorldClient? caster = null)
    {
        Assert.True(TempMods.TryGet(ability, out var effect));
        return TimedEffects.Apply(TimedEffects.ForPlayer(victim), effect, TimeUtils.GetSimTimeSeconds(), caster, "Caster");
    }

    [Fact]
    public void DebuffFamilyFollowsTheClientStatusCategories()
    {
        Assert.True(AbilityHostile.IsDebuff(343));   // Miasma 2.0, UDB0
        Assert.True(AbilityHostile.IsDebuff(315));   // Stun
        Assert.False(AbilityHostile.IsDebuff(61));   // Miasma 1.0 aura is UBF0 on its caster
        Assert.True(AbilityHostile.IsBuff(320));     // Negative Condition Sweep
        Assert.True(AbilityHostile.Exempt(734));     // ControlImmunityTimer (dispelExemptAbilities)
        Assert.True(AbilityHostile.Exempt(17));      // Self Defense
        Assert.False(AbilityHostile.Cleansable(734));
    }

    [Theory]
    [InlineData(343, 1009, -21, 45f)]   // Miasma 2.0
    [InlineData(701, 1010, -14, 5f)]    // Miasma 1.0's pulse effect
    [InlineData(392, 342, -39, 18f)]    // Code Rot 1.0
    [InlineData(360, 157, -100, 24f)]   // Insidious Code
    [InlineData(401, 117, -20, 30f)]    // Inefficiency Field 1.0
    [InlineData(467, 114, -80, 30f)]    // Inefficiency Field 2.0
    [InlineData(402, 115, -125, 18f)]   // Bottleneck Field
    [InlineData(403, 183, -50, 23f)]    // Disruption Field
    [InlineData(361, 1032, -27, 35f)]   // Fumble Field
    [InlineData(65, 180, -24, 33f)]     // Overheat 1.0
    [InlineData(394, 181, -52, 28f)]    // Overheat 2.0
    [InlineData(66, 1009, -10, 33f)]    // Overload 1.0
    [InlineData(465, 1011, -18, 17f)]   // UI Lag 2.0
    [InlineData(396, 520, -50, 28f)]    // Crash
    [InlineData(354, 109, -58, 30f)]    // Destroy Resolve 1.0
    [InlineData(476, 106, -520, 40f)]   // Destroy Health
    [InlineData(480, 522, -61, 25f)]    // Combat Shutdown
    [InlineData(86, 115, -50, 28f)]     // Slow
    public void StoredFixedDebuffsBecomeStatTempmods(ushort ability, ushort stat, int value, float seconds)
    {
        Assert.True(TempMods.TryGet(ability, out var effect));
        var mod = Assert.Single(effect.Mods, m => m.Target == stat);
        Assert.Equal(TempMods.Proc.Stat, mod.Proc);
        Assert.Equal(TempMods.KindStat, mod.Kind);
        Assert.Equal(value, mod.MinValue);
        Assert.Equal(value, mod.MaxValue);
        Assert.Equal(seconds, mod.Seconds);
    }

    [Fact]
    public void ScriptedOrNativeDurationModifiersAreNotGuessed()
    {
        Assert.True(TempMods.TryGet(396, out var crash));
        Assert.DoesNotContain(crash.Mods, m => m.Target == 519); // Force Combat has no stored DurationType
        Assert.True(TempMods.TryGet(354, out var resolve));
        Assert.DoesNotContain(resolve.Mods, m => m.Target == 111); // instant current IS is applied on landing
        Assert.False(TempMods.Has(61));
        Assert.False(TempMods.Has(324)); // Delete Upgrade is an instant script, not a tempmod
        Assert.True(AbilityHostile.Lands(324));
        Assert.True(AbilityHostile.Lands(354));
    }

    [Theory]
    [InlineData(350, 100, 30f, 2.5, true)]  // Code Infection 1.0
    [InlineData(351, 129, 30f, 2.5, true)]  // Code Infection 2.0
    [InlineData(400, 59, 15f, 2.5, true)]   // Plague Zone 1.0
    [InlineData(69, 30, 30f, 5.0, false)]   // Processor Lag 1.0 updates every 5 s and stores no activation strings
    public void ViralDotsUseTheirStoredTotalAndInterval(ushort ability, int total, float seconds, double interval, bool virus)
    {
        Assert.True(TempMods.TryGet(ability, out var effect));
        var dot = Assert.Single(effect.Mods);
        Assert.Equal(TempMods.Proc.Dot, dot.Proc);
        Assert.Equal(TempMods.DOTPlaceHolderAbility, dot.Target);
        Assert.Equal(total, dot.MinValue);
        Assert.Equal(seconds, dot.Seconds);
        Assert.Equal(interval, dot.Interval);
        Assert.Equal(virus, effect.Virus);
    }

    [Fact]
    public void DotTicksFollowTheScriptFormulaAndStopBeforeExpiry()
    {
        var holder = new TimedEffects.Holder();
        Assert.True(TempMods.TryGet(350, out var effect));
        var mods = TimedEffects.Instantiate(effect, new Random(1));
        holder.Apply(350, mods, 0);
        var ticks = new List<TimedEffects.Active>(); var expired = new List<TimedEffects.Active>();
        holder.Advance(30, ticks, expired);
        Assert.Equal(11, ticks.Count); // 2.5 .. 27.5; no tick at expiry
        Assert.Equal(8, ticks[0].TickDamage); // int(100 / 30 * 2.5 + .5)
        Assert.Single(expired);

        holder = new TimedEffects.Holder();
        Assert.True(TempMods.TryGet(69, out var lag));
        holder.Apply(69, TimedEffects.Instantiate(lag, new Random(1)), 0);
        ticks.Clear(); expired.Clear();
        holder.Advance(30, ticks, expired);
        Assert.Equal(5, ticks.Count);
        Assert.Equal(5, ticks[0].TickDamage); // int(30 / 30 * 5 + .5)
    }

    [Fact]
    public void ActivationStringsChooseVirusOrOwnLines()
    {
        Assert.True(TempMods.TryGet(343, out var miasma));
        Assert.False(miasma.Virus);
        Assert.Equal(0xc4000036u, miasma.CasterLine); // "%3s is caught in your Miasma field!"
        Assert.Equal(0xc4000041u, miasma.TargetLine); // "%2s hits you with %1z!"
        Assert.True(TempMods.TryGet(401, out var field));
        Assert.True(field.Virus); // 0xc600001a "infects you", like captured UI Lag 1.0
    }

    [Fact]
    public void InefficiencyFieldRaisesInnerStrengthCostsUntilItExpires()
    {
        var victim = Client();
        Assert.Equal(25, AbilityCosts.Effective(victim, 359));
        var applied = Land(victim, 467);
        Assert.Equal(-30, AbilityStats.Player(victim, 117));
        Assert.Equal(32, AbilityCosts.Effective(victim, 359)); // 25 - trunc(25 * -30 / 100)
        Assert.Equal(-80, AbilityStats.Player(victim, 114));
        TimedEffects.RunDue(applied[0].Expires + 0.01);
        Assert.Equal(25, AbilityCosts.Effective(victim, 359));
    }

    [Fact]
    public void DestroyHealthLowersMaximumAndRestoresItAfterwards()
    {
        var victim = Client(1000);
        var applied = Land(victim, 476);
        Assert.Equal(480, PvpCombat.HealthOf(victim));
        Assert.Equal(480, NumericalUtils.ByteArrayToUint16(victim.playerInstance.MaxHealth.getValue(), 1));
        TimedEffects.RunDue(applied[0].Expires + 0.01);
        Assert.Equal(1000, NumericalUtils.ByteArrayToUint16(victim.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(480, PvpCombat.HealthOf(victim));
    }

    [Fact]
    public void DestroyResolveTakesCurrentInnerStrengthAtOnce()
    {
        var caster = Client(); var victim = Client(innerStrength: 200);
        Land(victim, 354, caster);
        AbilityHostile.LandedOnPlayer(caster, victim, 354);
        // Stored component order: Max IS -58 clamps 200 to 142, then the instant Current IS -58
        Assert.Equal(84, AbilityHandler.CurrentInnerStrength(victim));
        Assert.Equal(142, AbilityHandler.MaxInnerStrength(victim));
    }

    [Fact]
    public void SweepPulseRemovesDebuffFamiliesOnlyAndKeepsImmunityAndBuffs()
    {
        var victim = Client();
        Land(victim, 343);
        Land(victim, 315);
        Assert.True(AbilityBuffs.Apply(victim, 327));
        var subject = TimedEffects.ForPlayer(victim);
        Assert.True(subject.Effects.HasParent(734));
        Assert.True(AbilityHostile.ApplySweep(victim, victim, 424));
        var pulse = subject.Effects.Snapshot().Single(a => a.Parent == 424);
        Assert.Equal(TempMods.Proc.Pulse, pulse.Proc);
        Assert.Equal(35, pulse.Value);
        Assert.Equal(25f, pulse.Seconds);
        pulse.Value = 100; // force every roll
        TimedEffects.RunDue(pulse.Start + 5.01);
        Assert.False(subject.Effects.HasParent(343));
        Assert.False(subject.Effects.HasParent(315));
        Assert.True(subject.Effects.HasParent(734));
        Assert.True(subject.Effects.HasParent(424));
        Assert.True(AbilityBuffs.IsActive(victim, 327));
    }

    private sealed class ZeroRoll : Random
    {
        public override int Next(int maxValue) => 0;
    }

    [Theory]
    [InlineData(674, 5)]
    [InlineData(675, 10)]
    [InlineData(676, 15)]
    [InlineData(677, 20)]
    [InlineData(678, 25)]
    public void AntibioticsExecuteTheInitialCureAndKeepTheirPeriodicModifier(ushort ability, int chance)
    {
        var victim = Client();
        Land(victim, 315);
        var subject = TimedEffects.ForPlayer(victim);
        var previous = AbilityHostile.PulseRandom;
        try
        {
            AbilityHostile.PulseRandom = new ZeroRoll();
            Assert.True(AbilityHostile.ApplySweep(victim, victim, ability));
            Assert.False(subject.Effects.HasParent(315));
            Assert.True(subject.Effects.HasParent(734));
            var pulse = Assert.Single(subject.Effects.Snapshot(), a => a.Parent == ability);
            Assert.Equal(chance, pulse.Value);
            Assert.Equal(90, pulse.Seconds);
            Assert.Equal(5, pulse.Interval);
            Land(victim, 343);
            TimedEffects.RunDue(pulse.Start + 5.01);
            Assert.False(subject.Effects.HasParent(343));
            Assert.True(subject.Effects.HasParent(ability));
            TimedEffects.RunDue(pulse.Start + 90.01);
            Assert.False(subject.Effects.HasParent(ability));
        }
        finally { AbilityHostile.PulseRandom = previous; }
    }

    [Fact]
    public void ConditionSweepPillWaitsForItsFirstUpdateAndReplacesItsSweepFamily()
    {
        var victim = Client();
        Land(victim, 343);
        AbilityHostile.ApplySweep(victim, victim, 320);
        Assert.True(AbilityHostile.ApplySweep(victim, victim, 659));
        var subject = TimedEffects.ForPlayer(victim);
        Assert.True(subject.Effects.HasParent(343)); // no init callback in the stored template
        Assert.False(subject.Effects.HasParent(320));
        var pulse = Assert.Single(subject.Effects.Snapshot(), a => a.Parent == 659);
        Assert.Equal(25, pulse.Value);
        Assert.Equal(20, pulse.Seconds);
        Assert.Equal(5, pulse.Interval);
    }

    [Fact]
    public void SweepsOfOneStackingTypeReplaceEachOther()
    {
        var victim = Client();
        AbilityHostile.ApplySweep(victim, victim, 320);
        AbilityHostile.ApplySweep(victim, victim, 379);
        var subject = TimedEffects.ForPlayer(victim);
        Assert.False(subject.Effects.HasParent(320));
        Assert.True(subject.Effects.HasParent(379));
    }

    [Fact]
    public void DeleteUpgradeRemovesBuffsNotDebuffsAndNeverPicksTheLastOfSeveral()
    {
        var caster = Client(); var victim = Client();
        Land(victim, 343);
        Assert.True(AbilityBuffs.Apply(victim, 327));
        Assert.True(AbilityBuffs.Apply(victim, 344));
        var candidates = AbilityHostile.RemovableBuffs(victim);
        Assert.Equal(2, candidates.Count);
        ushort removed = AbilityHostile.RemoveRandomBuff(caster, victim, 324, new Random(7));
        Assert.Equal(candidates[0], removed); // randrange(0, 1) can only be 0
        Assert.False(AbilityBuffs.IsActive(victim, removed));
        Assert.True(TimedEffects.ForPlayer(victim).Effects.HasParent(343));
        AbilityHostile.RemoveRandomBuff(caster, victim, 324, new Random(7));
        Assert.Empty(AbilityHostile.RemovableBuffs(victim));
        Assert.Equal(0, AbilityHostile.RemoveRandomBuff(caster, victim, 324, new Random(7)));
    }

    [Fact]
    public void DeleteUpgradeCanRemoveAnActiveSweep()
    {
        var caster = Client(); var victim = Client();
        AbilityHostile.ApplySweep(victim, victim, 320);
        Assert.Equal(320, AbilityHostile.RemoveRandomBuff(caster, victim, 377, new Random(1)));
        Assert.False(TimedEffects.ForPlayer(victim).Effects.HasParent(320));
    }

    [Fact]
    public void AurasReadTheirDrainAndEffectFromTheTemplate()
    {
        Assert.True(AbilityAuras.Handles(61));
        Assert.Equal(701, AbilityAuras.EffectOf(61));
        Assert.Equal(3, AbilityAuras.DrainOf(61));
        Assert.Equal(5, AbilityAuras.IntervalOf(61));
        Assert.True(AbilityAuras.Handles(89));
        Assert.Equal(694, AbilityAuras.EffectOf(89));
        Assert.Equal(12, AbilityAuras.DrainOf(89));
        Assert.False(AbilityAuras.Handles(343));
        Assert.True(AbilityAuras.Handles(42));  // Combat Aura pulses a friendly UBF0 effect
        Assert.False(AbilityAuras.Handles(609)); // Daemon Boost belongs to CoderPets
        Assert.Equal(1, AbilityHostile.InstantDamage(Client(), 694)); // stored raw viral 1, no influence
    }

    [Fact]
    public void BufferOverflowKeepsARemovableHostileRecord()
    {
        Assert.True(TempMods.TryGet(481, out var overflow));
        var mod = Assert.Single(overflow.Mods);
        Assert.Equal(TempMods.KindFx, mod.Kind);
        Assert.Equal(481, mod.Target);
        Assert.Equal(150, mod.MinValue);
        Assert.Equal(60f, mod.Seconds);
        Assert.True(AbilityHostile.Cleansable(481));
    }
}
