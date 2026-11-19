using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[CollectionDefinition("AbilityHostileWorld", DisableParallelization = true)]
public class AbilityHostileWorldCollection { }

[Collection("AbilityHostileWorld")]
public class AbilityHostileTests
{
    private static WorldClient Client(ushort health = 1000, ushort innerStrength = 200)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.LivenessClock = () => 0;
        typeof(WorldClient).GetField("alive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
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

    [Theory]
    [InlineData(315, 7, 14)]
    [InlineData(333, 5, 10)]
    public void HackerStunAppliesImmunitySeparatelyAndRetainsItAfterTheStateEnds(ushort ability, float duration, float immunity)
    {
        var victim = Client();
        double start = TimeUtils.GetSimTimeSeconds();
        Assert.True(TempMods.TryGet(ability, out var effect));
        var target = TimedEffects.ForPlayer(victim);
        TimedEffects.Apply(target, effect, start);
        Assert.True(target.Effects.HasAt(TempMods.StunAbility, start + duration - 0.01));
        Assert.Empty(target.Effects.HealthChanged(1, true, new Random(1)));
        Assert.True(target.Effects.HasAt(TempMods.StunAbility, start + duration - 0.01));
        Assert.False(target.Effects.HasAt(TempMods.StunAbility, start + duration + 0.01));
        Assert.True(target.Effects.HasControlImmunity(start + duration + 0.01));
        Assert.Empty(TimedEffects.Apply(target, effect, start + duration + 0.01));
        Assert.False(target.Effects.HasAt(TempMods.StunAbility, start + immunity - 0.01));
        Assert.False(target.Effects.HasControlImmunity(start + immunity + 0.01));
        Assert.Contains(TimedEffects.Apply(target, effect, start + immunity + 0.01),
            state => state.Target == TempMods.StunAbility);
        TimedEffects.Clear(target);
    }

    [Fact]
    public void MiasmaTwoChangesBothDefenseConsumersAndNaturallyClears()
    {
        var victim = Client();
        double start = TimeUtils.GetSimTimeSeconds();
        int melee = AbilityStats.Defense(victim, false);
        int ballistic = AbilityStats.Defense(victim, true);
        Assert.True(TempMods.TryGet(343, out var effect));
        TimedEffects.Apply(TimedEffects.ForPlayer(victim), effect, start);
        Assert.Equal(melee - 21, AbilityStats.Defense(victim, false));
        Assert.Equal(ballistic - 21, AbilityStats.Defense(victim, true));
        TimedEffects.RunDue(start + 45.01);
        Assert.Equal(melee, AbilityStats.Defense(victim, false));
        Assert.Equal(ballistic, AbilityStats.Defense(victim, true));
    }

    [Fact]
    public void CodeFreezeRootLivesForTwentyFiveSecondsUnlessDamageBreaksIt()
    {
        var victim = Client();
        var subject = TimedEffects.ForPlayer(victim);
        var root = Assert.Single(Land(victim, 40));
        Assert.Equal(TempMods.RootedAbility, root.Target);
        Assert.Equal(25, root.Seconds);
        Assert.False(ControlActions.CanMove(subject.Effects));
        Assert.True(ControlActions.CanUseAbilities(subject.Effects));
        TimedEffects.HealthChanged(subject, -1, true);
        Assert.False(ControlActions.CanMove(subject.Effects));
        TimedEffects.HealthChanged(subject, 1, true, damageEvent: true);
        Assert.True(ControlActions.CanMove(subject.Effects));
    }

    [Fact]
    public void SlowAppliesFourPenaltyRecordsAndLowersBallisticAttackUntilExpiry()
    {
        var victim = Client();
        var applied = Land(victim, 86);
        Assert.Equal(-19, AbilityStats.Attack(victim, true));
        foreach (ushort stat in new ushort[] { 115, 877, 878, 519 })
        {
            Assert.Equal(stat == 519 ? -15 : -50, AbilityStats.Player(victim, stat));
            Assert.Equal(28, Assert.Single(applied, m => m.Target == stat).Seconds);
        }
        TimedEffects.RunDue(applied.Max(m => m.Expires) + .01);
        Assert.Equal(0, AbilityStats.Attack(victim, true));
        foreach (ushort stat in new ushort[] { 115, 877, 878, 519 })
            Assert.Equal(0, AbilityStats.Player(victim, stat));
    }

    [Fact]
    public void VectorPreservesTheCapturedPowerlessAndViralDefenseValues()
    {
        var victim = Client();
        var subject = TimedEffects.ForPlayer(victim);
        var applied = Land(victim, 355);
        var powerless = Assert.Single(applied, m => m.Target == TempMods.PowerlessAbility);
        var defense = Assert.Single(applied, m => m.Target == 183);
        Assert.Equal(8.94f, powerless.Seconds);
        Assert.Equal(30, defense.Seconds);
        Assert.Equal(-60, AbilityStats.Player(victim, 183));
        var viralMove = new AbilityItem();
        viralMove.setAbilityID(330);
        Assert.Equal(-60, AbilityStats.SpecialDefense(victim, viralMove));
        Assert.False(ControlActions.CanUseAbilities(subject.Effects));
        TimedEffects.RunDue(powerless.Expires + .01);
        Assert.True(ControlActions.CanUseAbilities(subject.Effects));
        Assert.Equal(-60, AbilityStats.Player(victim, 183));
        TimedEffects.RunDue(defense.Expires + .01);
        Assert.Equal(0, AbilityStats.Player(victim, 183));
    }

    [Fact]
    public void TrojanHorseDrainsOnlyNearbyHostilePlayersEveryFourthSecond()
    {
        var caster = Client();
        var target = Client();
        var distant = Client();
        var ally = Client();
        var unseen = Client();
        var previousClients = WorldServer.Clients;
        var previousMobs = WorldServer.mobs;
        WorldServer.Clients = new();
        WorldServer.mobs = new();
        caster.viewMan = new();
        var players = new[] { caster, target, distant, ally, unseen };
        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            uint id = (uint)(77650 + i);
            player.playerData.setCharID(id);
            player.playerData.setEntityId(id);
            player.playerData.pvpFlag = true;
            player.playerInstance.OrganizationID.setValue((byte)(player == caster || player == ally ? 1 : 2));
            double x = player == caster ? 0 : player == distant ? 301 : 299;
            player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 0, 0));
            WorldServer.Clients.Add(id.ToString(), player);
            if (player != caster && player != unseen)
                caster.viewMan.addView(id, 1).viewCreated = true;
        }

        try
        {
            Assert.True(AbilityAuras.Handles(945));
            Assert.Equal(4, AbilityAuras.IntervalOf(945));
            Assert.Equal(946, AbilityAuras.EffectOf(945));
            Assert.Equal(PvpRules.Verdict.Allowed, PvpCombat.Check(caster, target));
            AbilityAuras.Start(caster, 945);
            Assert.True(AbilityAuras.IsOn(caster, 945));
            Assert.Equal(200, AbilityHandler.CurrentInnerStrength(target)); // no ApplyAuraEffect initializer

            AbilityAuras.Tick(caster, 945, 1);
            Assert.Equal(180, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(180, AbilityHandler.CurrentInnerStrength(target));
            Assert.Equal(1000, PvpCombat.HealthOf(target));
            foreach (var excluded in new[] { distant, ally, unseen })
                Assert.Equal(200, AbilityHandler.CurrentInnerStrength(excluded));

            target.playerInstance.InnerStrengthAvailable.setValue((ushort)8);
            AbilityAuras.Tick(caster, 945, 1);
            Assert.Equal(160, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(0, AbilityHandler.CurrentInnerStrength(target));

            caster.playerInstance.InnerStrengthAvailable.setValue((ushort)19);
            target.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
            AbilityAuras.Tick(caster, 945, 1);
            Assert.False(AbilityAuras.IsOn(caster, 945));
            AbilityAuras.Tick(caster, 945, 1);
            Assert.Equal(19, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(100, AbilityHandler.CurrentInnerStrength(target));
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
            WorldServer.Clients = previousClients;
            WorldServer.mobs = previousMobs;
        }
    }

    [Theory]
    [InlineData(350, 100, 8)]
    [InlineData(351, 129, 11)]
    [InlineData(353, 450, 38)] // Runtime capture override; GOB total is 94.
    public void VectorVirusDealsElevenRoundedViralTicksBeforeExpiry(ushort id, int total, int tickDamage)
    {
        var target = Client();
        var effect = Assert.Single(Land(target, id));
        Assert.Equal(total, effect.Value);
        Assert.Equal(2, effect.DamageType);
        Assert.Equal(2.5, effect.Interval);
        Assert.Equal(30, effect.Seconds);
        TimedEffects.RunDue(effect.Start + 2.49);
        Assert.Equal(1000, PvpCombat.HealthOf(target));
        TimedEffects.RunDue(effect.Start + 2.51);
        Assert.Equal(1000 - tickDamage, PvpCombat.HealthOf(target));
        TimedEffects.RunDue(effect.Expires + .01);
        Assert.Equal(1000 - tickDamage * 11, PvpCombat.HealthOf(target));
        Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(id));
    }

    [Theory]
    [InlineData(947, 99, .1f)]
    [InlineData(948, 102, .15f)]
    public void PacketLossUsesItsStoredProcThresholdOnTheLandedEffect(ushort id, ushort stateId, float threshold)
    {
        Assert.True(TempMods.TryGet(id, out var effect));
        var target = Client();
        var holder = TimedEffects.ForPlayer(target);
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.Empty(TimedEffects.Apply(holder, effect, now, rng: new ProcRoll(threshold)));
        var applied = Assert.Single(TimedEffects.Apply(holder, effect, now, rng: new ProcRoll(threshold - .00001)));
        Assert.Equal(stateId, applied.Target);
        Assert.Equal(10, applied.Seconds);
        TimedEffects.RunDue(applied.Expires + .01);
        Assert.False(holder.Effects.HasParent(id));
    }

    [Fact]
    public void FreezeSystemTwoPacifiesAndAddsTheShippedViralResistancePenalty()
    {
        var target = Client();
        var effects = TimedEffects.ForPlayer(target);
        var applied = Land(target, 352);
        Assert.Equal(6, Assert.Single(applied, m => m.Target == TempMods.MezAbility).Seconds);
        Assert.Equal(12, Assert.Single(applied, m => m.Target == 157).Seconds);
        Assert.Equal(-48, AbilityStats.Player(target, 157));
        Assert.False(ControlActions.CanUseAbilities(effects.Effects));
        var immunity = Assert.Single(effects.Effects.Snapshot(), m => m.Parent == TempMods.ControlImmunityTimerAbility);
        TimedEffects.HealthChanged(effects, 1, true, damageEvent: true);
        Assert.True(ControlActions.CanUseAbilities(effects.Effects));
        Assert.True(effects.Effects.HasParent(TempMods.ControlImmunityTimerAbility));
        TimedEffects.RunDue(immunity.Expires + .01);
        Assert.False(effects.Effects.HasParent(TempMods.ControlImmunityTimerAbility));
        Assert.Equal(0, AbilityStats.Player(target, 157));
    }

    [Fact]
    public void DeleteUpgradeTwoRunsBothDispelCallbacksWithoutSpendingTargetInnerStrength()
    {
        var caster = Client();
        var target = Client();
        Assert.True(AbilityBuffs.Apply(target, 327));
        Assert.True(AbilityBuffs.Apply(target, 344));
        Land(target, 392);
        var rng = new ZeroRoll();
        AbilityHostile.LandedOnPlayer(caster, target, 420, rng);
        Assert.False(AbilityBuffs.IsActive(target, 327));
        Assert.False(AbilityBuffs.IsActive(target, 344));
        Assert.True(TimedEffects.ForPlayer(target).Effects.HasParent(392));
        Assert.Equal(200, AbilityHandler.CurrentInnerStrength(target));
        TimedEffects.RemoveParent(TimedEffects.ForPlayer(target), 392);
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

    [Theory]
    [InlineData(401, -20, -35, 30, 5)]
    [InlineData(467, -30, -80, 32, 1)]
    public void InefficiencyFieldsRaiseCastCostAndSlowRegenUntilExpiry(ushort ability, int costStat,
        int regenStat, int castCost, int regeneration)
    {
        var victim = Client();
        double carry = 0;
        int NextRegen() => AbilityHandler.RegenAmount(false, ref carry, AbilityStats.Influence(victim, 114));
        Assert.Equal(25, AbilityCosts.Effective(victim, 359));
        Assert.Equal(8, NextRegen());
        var applied = Land(victim, ability);
        Assert.All(applied, effect => Assert.Equal(30f, effect.Seconds));
        Assert.Equal(costStat, AbilityStats.Player(victim, 117));
        Assert.Equal(castCost, AbilityCosts.Effective(victim, 359));
        Assert.Equal(regenStat, AbilityStats.Player(victim, 114));
        Assert.Equal(regeneration, NextRegen());
        TimedEffects.RunDue(applied[0].Expires + 0.01);
        Assert.Equal(25, AbilityCosts.Effective(victim, 359));
        Assert.Equal(8, NextRegen());
    }

    [Fact]
    public void DespoilerChangesMeleeAndBallisticAccuracyAndDamageForThirtySeconds()
    {
        var victim = Client();
        int melee = AbilityStats.Attack(victim, false);
        int ballistic = AbilityStats.Attack(victim, true);

        var applied = Land(victim, 404);
        Assert.Equal(4, applied.Count);
        Assert.All(applied, effect => Assert.Equal(30f, effect.Seconds));
        Assert.Equal(melee - 39, AbilityStats.Attack(victim, false));
        Assert.Equal(ballistic - 39, AbilityStats.Attack(victim, true));
        Assert.Equal(860, AbilityStats.ScaleTypedAbilityDamage(victim, 0, 1000));
        Assert.Equal(860, AbilityStats.ScaleTypedAbilityDamage(victim, 1, 1000));
        Assert.Equal(1000, AbilityStats.ScaleTypedAbilityDamage(victim, 2, 1000));

        TimedEffects.RunDue(applied[0].Expires + 0.01);
        Assert.Equal(melee, AbilityStats.Attack(victim, false));
        Assert.Equal(ballistic, AbilityStats.Attack(victim, true));
        Assert.Equal(1000, AbilityStats.ScaleTypedAbilityDamage(victim, 0, 1000));
        Assert.Equal(1000, AbilityStats.ScaleTypedAbilityDamage(victim, 1, 1000));
    }

    [Fact]
    public void DisruptionFieldLowersTheViralContestDefenseUntilExpiry()
    {
        var victim = Client();
        var viral = new AbilityItem();
        viral.setAbilityID(329);
        int before = AbilityStats.SpecialDefense(victim, viral);
        int physical = AbilityStats.Defense(victim, false);

        var applied = Assert.Single(Land(victim, 403));
        Assert.Equal(23f, applied.Seconds);
        Assert.Equal(before - 50, AbilityStats.SpecialDefense(victim, viral));
        Assert.Equal(physical, AbilityStats.Defense(victim, false));

        TimedEffects.RunDue(applied.Expires + 0.01);
        Assert.Equal(before, AbilityStats.SpecialDefense(victim, viral));
    }

    [Fact]
    public void UiLagRankTwoChangesBallisticAndThrownContestsUntilExpiry()
    {
        var victim = Client();
        var throwing = new AbilityItem();
        throwing.setAbilityID(134);
        int ballisticAttack = AbilityStats.Attack(victim, true);
        int ballisticDefense = AbilityStats.Defense(victim, true);
        int thrownAttack = AbilityStats.ThrownAccuracy(victim);
        int thrownDefense = AbilityStats.SpecialDefense(victim, throwing);

        var applied = Land(victim, 465);
        Assert.Equal(4, applied.Count);
        Assert.All(applied, effect => Assert.Equal(17f, effect.Seconds));
        Assert.Equal(ballisticAttack - 20, AbilityStats.Attack(victim, true));
        Assert.Equal(ballisticDefense - 18, AbilityStats.Defense(victim, true));
        Assert.Equal(thrownAttack - 20, AbilityStats.ThrownAccuracy(victim));
        Assert.Equal(thrownDefense - 18, AbilityStats.SpecialDefense(victim, throwing));

        TimedEffects.RunDue(applied[0].Expires + 0.01);
        Assert.Equal(ballisticAttack, AbilityStats.Attack(victim, true));
        Assert.Equal(ballisticDefense, AbilityStats.Defense(victim, true));
        Assert.Equal(thrownAttack, AbilityStats.ThrownAccuracy(victim));
        Assert.Equal(thrownDefense, AbilityStats.SpecialDefense(victim, throwing));
    }

    [Theory]
    [InlineData(0, 180)]
    [InlineData(1, 181)]
    [InlineData(2, 157)]
    [InlineData(7, 1033)]
    public void DestroyerIncreasesTakenDamageByReducingTheMatchingResistance(int damageType, ushort resistance)
    {
        var caster = Client();
        var victim = Client();
        int initial = AbilityStats.Player(victim, resistance);
        ushort previous = TypedAbilityResistance.Player(caster, victim, 0, 400, damageType);
        var applied = Land(victim, 1088, caster);
        Assert.Equal(4, applied.Count);
        Assert.Equal(initial - 120, AbilityStats.Player(victim, resistance));
        ushort reduced = TypedAbilityResistance.Player(caster, victim, 0, 400, damageType);
        Assert.True(reduced > previous);

        TimedEffects.RunDue(applied[0].Expires + 0.01);
        Assert.Equal(initial, AbilityStats.Player(victim, resistance));
        Assert.Equal(previous, TypedAbilityResistance.Player(caster, victim, 0, 400, damageType));
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
    public void DestroyDebuffsScaleNaturalRegenUntilExpiry()
    {
        var victim = Client(5000, 2000);
        double carry = 0;

        ushort? NextHealth() => HealthRegen.Next(1000,
            NumericalUtils.ByteArrayToUint16(victim.playerInstance.MaxHealth.getValue(), 1),
            false, false, 2, AbilityStats.Influence(victim, 112));
        int NextInnerStrength() => AbilityHandler.RegenAmount(false, ref carry, AbilityStats.Influence(victim, 114));

        Assert.Equal((ushort)1100, NextHealth());
        Assert.Equal(8, NextInnerStrength());

        var resolve = Land(victim, 354);
        var health = Land(victim, 476);
        Assert.Equal(-50, AbilityStats.Influence(victim, 114));
        Assert.Equal(-20, AbilityStats.Influence(victim, 112));
        Assert.Equal((ushort)1072, NextHealth()); // round(4480 * .01 * 2 * .8)
        Assert.Equal(4, NextInnerStrength());

        TimedEffects.RunDue(resolve[0].Expires + 0.01);
        Assert.Equal(8, NextInnerStrength());
        Assert.Equal((ushort)1072, NextHealth());
        TimedEffects.RunDue(health[0].Expires + 0.01);
        Assert.Equal((ushort)1100, NextHealth());
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

    private sealed class SweepRoll : Random
    {
        private readonly int value;
        private readonly Action? before;
        public SweepRoll(int value, Action? before=null) { this.value=value; this.before=before; }
        public override int Next(int maxValue) { before?.Invoke(); return value; }
    }

    [Fact]
    public void SweepLogsOneLinePerActuallyRemovedParentAndNoMissExpiryOrStaleSuccess()
    {
        var victim=Client(); victim.playerInstance.CharacterName.setValue("SweepProof");
        var subject=TimedEffects.ForPlayer(victim); var lines=new List<string>();
        var pulse=new TimedEffects.Active { Parent=678,Value=25 };
        var mods=Land(victim,97); Assert.Equal(4,mods.Count);
        AbilityHostile.Pulse(subject,pulse,new SweepRoll(99),lines.Add);
        Assert.True(subject.Effects.HasParent(97)); Assert.Empty(lines);
        AbilityHostile.Pulse(subject,pulse,new SweepRoll(0),lines.Add);
        Assert.False(subject.Effects.HasParent(97));
        Assert.Equal(new[]{"[SWEEP] 678 removed 97 from SweepProof"},lines);
        AbilityHostile.Pulse(subject,pulse,new SweepRoll(0),lines.Add);
        Assert.Single(lines); // no duplicate after all four sibling mods were removed

        Land(victim,97);
        AbilityHostile.Pulse(subject,pulse,new SweepRoll(0,()=>TimedEffects.RemoveParent(subject,97)),lines.Add);
        Assert.Single(lines); // another remover wins after the candidate snapshot
        var overdue=Land(victim,97); foreach(var mod in overdue) mod.Expires=TimeUtils.GetSimTimeSeconds()-1;
        AbilityHostile.Pulse(subject,pulse,new SweepRoll(0),lines.Add);
        Assert.False(subject.Effects.HasParent(97)); Assert.Single(lines); // pending natural cleanup is not a live sweep success
        var expiredMods=Land(victim,97); var expired=new List<TimedEffects.Active>();
        subject.Effects.Advance(expiredMods.Max(m=>m.Expires)+1,new List<TimedEffects.Active>(),expired);
        Assert.False(subject.Effects.HasParent(97)); Assert.Equal(4,expired.Count);
        AbilityHostile.Pulse(subject,pulse,new SweepRoll(0),lines.Add); Assert.Single(lines);

        Land(victim,97); AbilityHostile.Pulse(subject,pulse,new SweepRoll(0),lines.Add);
        Assert.Equal(2,lines.Count); // an actual reapplication can be removed and logged again
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

    private sealed class ProcRoll(double value) : Random
    {
        public int Rolls;
        public override double NextDouble() { Rolls++; return value; }
    }

    [Theory]
    [InlineData(0.199999, true)]
    [InlineData(0.2, false)]
    [InlineData(0.999, false)]
    public void MachinegunFistDispelUsesItsStoredTwentyPercentProc(double roll, bool removes)
    {
        var caster = Client(); var victim = Client();
        Assert.Equal(0.2, AbilityCatalog.Find(261)!.Modifiers.Single(m => m.Text("StatModInitFunc") == "RemoveRandomBuff").Number("ProcTriggerChance"));
        Assert.True(AbilityBuffs.Apply(victim, 327));
        Assert.True(AbilityBuffs.Apply(victim, 344));
        Land(victim, 343);
        var rng = new ProcRoll(roll);
        AbilityHostile.LandedOnPlayer(caster, victim, 261, rng);
        Assert.Equal(1, rng.Rolls);
        Assert.Equal(!removes, AbilityBuffs.IsActive(victim, 327));
        Assert.True(AbilityBuffs.IsActive(victim, 344));
        Assert.True(TimedEffects.ForPlayer(victim).Effects.HasParent(343));
    }

    [Theory]
    [InlineData(0.199999, true)]
    [InlineData(0.2, false)]
    public void MachinegunFistNpcFailureOutputAlsoRequiresTheProc(double roll, bool triggered)
    {
        var caster=Client(); var mob=new Mob(); mob.setName("ProcMob");
        var rng=new ProcRoll(roll);
        AbilityHostile.LandedOnMob(caster,mob,261,rng);
        Assert.Equal(1,rng.Rolls);
        Assert.Equal(triggered?1:0,caster.messageQueue.RPCMessagesQueue.Count);
    }

    [Fact]
    public void TriggeredDispelDeactivatesAnAuraAndAllowsLaterBuff()
    {
        var caster=Client(); var target=Client();
        var previousClients=WorldServer.Clients;
        WorldServer.Clients=new Dictionary<string,WorldClient>();
        try
        {
            AbilityAuras.Start(target,42);
            Assert.True(AbilityAuras.IsOn(target,42));
            // Remove any child pulses: leave the toggled parent as the only candidate.
            foreach(ushort id in AbilityHostile.RemovableBuffs(target).Where(id=>id!=42).ToList())
                AbilityHostile.RemoveBuff(target,id);
            Assert.Equal(new ushort[]{42},AbilityHostile.RemovableBuffs(target));
            AbilityHostile.LandedOnPlayer(caster,target,261,new ProcRoll(0));
            Assert.False(AbilityAuras.IsOn(target,42));
            Assert.Empty(AbilityHostile.RemovableBuffs(target));
            Assert.True(AbilityBuffs.Apply(target,327));
            Assert.True(AbilityBuffs.IsActive(target,327));
        }
        finally
        {
            AbilityAuras.Stop(target,42);
            WorldServer.Clients=previousClients;
        }
    }

    [Theory]
    [InlineData("life")]
    [InlineData("world")]
    [InlineData("transport")]
    public void StaleAuraCannotDrainAfterTheSourceChanges(string transition)
    {
        var caster = Client();
        var other = Client();
        var clients = WorldServer.Clients;
        WorldServer.Clients = new Dictionary<string, WorldClient>();
        try
        {
            AbilityAuras.Start(caster, 42);
            AbilityAuras.Start(other, 42);
            Assert.True(AbilityAuras.IsOn(caster, 42));
            switch (transition)
            {
                case "life": caster.playerData.lifeGeneration++; break;
                case "world": caster.playerData.worldGeneration++; break;
                case "transport":
                    caster.TransportFence.PublishIncoming(true, () => { });
                    caster.TransportFence.Resume(caster.TransportFence.Capture());
                    break;
            }
            Assert.False(AbilityAuras.IsOn(caster, 42));
            Assert.True(AbilityAuras.IsOn(other, 42));
            AbilityAuras.Tick(caster, 42, 1);
            Assert.Equal(200, AbilityHandler.CurrentInnerStrength(caster));
            AbilityAuras.Start(caster, 42);
            AbilityAuras.Tick(caster, 42, 1);
            Assert.True(AbilityAuras.IsOn(caster, 42));
            Assert.Equal(200, AbilityHandler.CurrentInnerStrength(caster));
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
            AbilityAuras.Clear(other, announce: false);
            WorldServer.Clients = clients;
        }
    }

    [Theory]
    [InlineData(324)]
    [InlineData(377)]
    public void DispelWithoutStoredChanceDoesNotConsumeAProcRoll(ushort ability)
    {
        var caster = Client(); var victim = Client();
        Assert.True(AbilityBuffs.Apply(victim, 327));
        var rng = new ProcRoll(0.999);
        AbilityHostile.LandedOnPlayer(caster, victim, ability, rng);
        Assert.False(AbilityBuffs.IsActive(victim, 327));
        Assert.Equal(0, rng.Rolls);
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
