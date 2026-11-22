using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using hds.world.Structures;

namespace hds.Tests;

[CollectionDefinition("AbilityHostileWorld", DisableParallelization = true)]
public class AbilityHostileWorldCollection { }

[Collection("AbilityHostileWorld")]
public class AbilityHostileTests
{
    [Fact]
    public void CodeConversionUsesTheThirtyPercentBoundaryOncePerHealthChange()
    {
        var caster = Client();
        var originalClients = WorldServer.Clients;
        var originalPercent = AbilityHealthCallbacks.Random100;
        var originalSample = AbilityHealthCallbacks.Random15;
        try
        {
            WorldServer.Clients = new() { ["caster"] = caster };
            caster.playerInstance.Health.setValue((ushort)400);
            AbilityHealthCallbacks.Random15 = () => 0;
            int roll = 30, rolls = 0;
            AbilityHealthCallbacks.Random100 = () =>
            {
                rolls++;
                return roll;
            };
            Assert.True(AbilityBuffs.Apply(caster, 943));

            AbilityHandler.HealSelf(caster, 10);
            Assert.Equal(410, PvpCombat.HealthOf(caster));
            Assert.Equal(1, rolls);

            roll = 29;
            AbilityHandler.HealSelf(caster, 10);
            Assert.Equal(480, PvpCombat.HealthOf(caster));
            Assert.Equal(2, rolls);

            AbilityBuffs.Remove(caster, 943);
            AbilityHandler.HealSelf(caster, 10);
            Assert.Equal(490, PvpCombat.HealthOf(caster));
            Assert.Equal(2, rolls);
        }
        finally
        {
            AbilityBuffs.RemoveAll(caster);
            WorldServer.Clients = originalClients;
            AbilityHealthCallbacks.Random100 = originalPercent;
            AbilityHealthCallbacks.Random15 = originalSample;
        }
    }

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

    [Theory]
    [InlineData(66, 33, new[] { -10, -10, -10, -10, 0, 0, 0 })]
    [InlineData(392, 18, new[] { 0, 0, 0, 0, -39, 0, 0 })]
    [InlineData(394, 28, new[] { 0, 0, 0, 0, 0, -52, -52 })]
    [InlineData(396, 42, new[] { 0, -27, 0, 0, -27, 0, 0 })]
    public void HackerDebuffsChangeTheirCombatConsumersUntilExpiry(ushort id, int seconds, int[] values)
    {
        var target = Client();
        int[] Consumers() => new[]
        {
            AbilityStats.Attack(target, false), AbilityStats.Attack(target, true),
            AbilityStats.Defense(target, false), AbilityStats.Defense(target, true),
            AbilityStats.ViralAccuracy(target), AbilityStats.Toughness(target, false),
            AbilityStats.Toughness(target, true)
        };
        var before = Consumers();
        var applied = Land(target, id);
        Assert.Equal(seconds, applied.Max(m => m.Seconds));
        Assert.Equal(values, Consumers().Zip(before, (active, baseline) => active - baseline).ToArray());
        if (id == 396)
        {
            foreach (ushort stat in new ushort[] { 115, 520, 877, 878 })
            {
                Assert.Equal(-50, AbilityStats.Player(target, stat));
                Assert.Equal(28, Assert.Single(applied, m => m.Target == stat).Seconds);
            }
        }
        TimedEffects.RunDue(applied.Max(m => m.Expires) + .01);
        Assert.Equal(before, Consumers());
    }

    [Fact]
    public void ProcessorLagDealsFiveViralTicksAndPublishesTickEffect()
    {
        var target = Client();
        var tick = Assert.Single(Land(target, 69));
        Assert.Equal(2, tick.DamageType);
        Assert.Equal(30, tick.Seconds);
        Assert.Equal(5, tick.Interval);
        Assert.Equal(1000, PvpCombat.HealthOf(target));
        TimedEffects.RunDue(tick.Start + 4.99);
        Assert.Equal(1000, PvpCombat.HealthOf(target));
        TimedEffects.RunDue(tick.Start + 5.01);
        Assert.Equal(995, PvpCombat.HealthOf(target));
        var update = Assert.IsType<SequencedMessage>(Assert.Single(target.messageQueue.ObjectMessagesQueue));
        Assert.Contains(Convert.ToHexString(BitConverter.GetBytes(TempMods.VirusCrashFx)),
            Convert.ToHexString(update.content));
        TimedEffects.RunDue(tick.Expires + .01);
        Assert.Equal(975, PvpCombat.HealthOf(target));
        Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(69));
    }

    [Fact]
    public void CombatAuraPulsesShippedAccuracyAndChargesTwelveInnerStrength()
    {
        var caster = Client();
        var recipient = Client();
        caster.playerData.setCharID(77610);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 0, 0));
        recipient.playerData.setCharID(77611);
        recipient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(299, 0, 0));
        var groups = hds.world.Structures.SocialGroups.Instance;
        var team = groups.CreateTeam(77610, "Aura");
        groups.AddTeamMember(team.Id, 77611, "Teammate");
        var previous = WorldServer.Clients;
        WorldServer.Clients = new() { ["Aura"] = caster, ["Teammate"] = recipient };
        try
        {
            AbilityAuras.Start(caster, 42);
            Assert.True(AbilityAuras.IsOn(caster, 42));
            Assert.Equal(200, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(64, AbilityStats.Attack(recipient, false));
            Assert.Equal(64, AbilityStats.Attack(recipient, true));
            Assert.Equal(5, recipient.playerData.activeBuffs[695].Definition.Seconds);

            recipient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(301, 0, 0));
            AbilityAuras.Tick(caster, 42, 1);
            Assert.Equal(188, AbilityHandler.CurrentInnerStrength(caster));
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + 5.01);
            Assert.Equal(0, AbilityStats.Attack(recipient, false));
            Assert.Equal(0, AbilityStats.Attack(recipient, true));

            caster.playerInstance.InnerStrengthAvailable.setValue((ushort)11);
            AbilityAuras.Tick(caster, 42, 1);
            Assert.Equal(11, AbilityHandler.CurrentInnerStrength(caster));
            Assert.False(AbilityAuras.IsOn(caster, 42));
            Assert.InRange(caster.playerData.cooldownEndsAt[42] - TimeUtils.GetSimTimeSeconds(), 14.9, 15d);
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
            AbilityBuffs.RemoveAll(caster);
            AbilityBuffs.RemoveAll(recipient);
            WorldServer.Clients = previous;
            groups.RemoveTeamMember(team.Id, 77611);
            groups.RemoveTeamMember(team.Id, 77610);
        }
    }

    [Fact]
    public void CodeStopBreaksOnDamageButItsTwelveSecondImmunitySurvives()
    {
        var caster = Client();
        var target = Client();
        var state = Assert.Single(Land(target, 393, caster));
        var effects = TimedEffects.ForPlayer(target);
        Assert.Equal(6, state.Seconds);
        var immunity = Assert.Single(effects.Effects.Snapshot(), m => m.Parent == TempMods.ControlImmunityTimerAbility);
        Assert.Equal(12, immunity.Seconds);
        Assert.False(ControlActions.CanUseAbilities(effects.Effects));
        TimedEffects.HealthChanged(effects, -1, true);
        Assert.True(effects.Effects.HasParent(393));
        TimedEffects.HealthChanged(effects, 1, true, damageEvent: true);
        Assert.True(ControlActions.CanUseAbilities(effects.Effects));
        Assert.Empty(Land(target, 393, caster));
        TimedEffects.RunDue(immunity.Expires + .01);
        Assert.False(effects.Effects.HasParent(TempMods.ControlImmunityTimerAbility));
    }

    [Fact]
    public void ResistCombatAppliesTwentyEvasionForSixHundredSeconds()
    {
        var target = Client();
        var before = CombatInsightEngagement.Calculate(0, 0, 0, 0, AbilityStats.Player(target, 520));
        double start = TimeUtils.GetSimTimeSeconds();
        Assert.True(AbilityBuffs.Apply(target, 72));
        Assert.Equal(20, AbilityStats.Player(target, 520));
        Assert.Equal(600, target.playerData.activeBuffs[72].Definition.Seconds);
        var after = CombatInsightEngagement.Calculate(0, 0, 0, 0, AbilityStats.Player(target, 520));
        Assert.Equal(before.Evade + 20, after.Evade);
        TimedEffects.RunDue(start + 600.01);
        Assert.Equal(0, AbilityStats.Player(target, 520));
    }

    [Fact]
    public void CodeFreezeRootLivesForTwentyFiveSecondsUnlessDamageBreaksIt()
    {
        var victim = Client();
        var subject = TimedEffects.ForPlayer(victim);
        var applied = Land(victim, 40);
        var root = Assert.Single(applied, mod => mod.Target == TempMods.RootedAbility);
        var force = Assert.Single(applied, mod => mod.Target == 519);
        Assert.Equal(-20, force.Value);
        Assert.Equal(25, force.Seconds);
        Assert.Equal(TempMods.RootedAbility, root.Target);
        Assert.Equal(25, root.Seconds);
        Assert.False(ControlActions.CanMove(subject.Effects));
        Assert.True(ControlActions.CanUseAbilities(subject.Effects));
        TimedEffects.HealthChanged(subject, -1, true);
        Assert.False(ControlActions.CanMove(subject.Effects));
        TimedEffects.HealthChanged(subject, 1, true, damageEvent: true);
        Assert.True(ControlActions.CanMove(subject.Effects));
        Assert.Equal(0, AbilityStats.Player(victim, 519));
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

    [Theory]
    [InlineData(379, 20, 35)]
    [InlineData(424, 35, 25)]
    public void SweepPulseRemovesDebuffFamiliesOnlyAndKeepsImmunityAndBuffs(ushort ability, int chance, float seconds)
    {
        var victim = Client();
        Land(victim, 343);
        Land(victim, 315);
        Assert.True(AbilityBuffs.Apply(victim, 327));
        var subject = TimedEffects.ForPlayer(victim);
        Assert.True(subject.Effects.HasParent(734));
        Assert.True(AbilityHostile.ApplySweep(victim, victim, ability));
        var pulse = subject.Effects.Snapshot().Single(a => a.Parent == ability);
        Assert.Equal(TempMods.Proc.Pulse, pulse.Proc);
        Assert.Equal(chance, pulse.Value);
        Assert.Equal(seconds, pulse.Seconds);
        AbilityHostile.Pulse(subject, pulse, new ZeroRoll());
        Assert.False(subject.Effects.HasParent(343));
        Assert.False(subject.Effects.HasParent(315));
        Assert.True(subject.Effects.HasParent(734));
        Assert.True(subject.Effects.HasParent(ability));
        Assert.True(AbilityBuffs.IsActive(victim, 327));
    }

    [Theory]
    [InlineData((ushort)71, (ushort)320, 18)]
    [InlineData((ushort)370, (ushort)320, 18)]
    [InlineData((ushort)71, (ushort)379, 20)]
    [InlineData((ushort)370, (ushort)379, 20)]
    public void CasterSweepBonusSnapshotsAWholeChancePointWithoutChangingThePublishedValue(ushort passive, ushort ability, int baseChance)
    {
        var caster = Client();
        var recipient = Client();
        var subject = TimedEffects.ForPlayer(recipient);
        AbilityPassives.SetLoaded(recipient, new[] { (passive, (ushort)19) });
        Assert.Equal(1, AbilityStats.Player(recipient, 1052));
        Land(recipient, 86);

        Assert.True(AbilityHostile.ApplySweep(caster, recipient, ability));
        var unboosted = Assert.Single(subject.Effects.Snapshot(), effect => effect.Parent == ability);
        Assert.Equal(baseChance, unboosted.Value);
        Assert.Equal(baseChance, unboosted.SweepChance);
        AbilityHostile.Pulse(subject, unboosted, new SweepRoll(baseChance));
        Assert.True(subject.Effects.HasParent(86));

        AbilityPassives.SetLoaded(caster, new[] { (passive, (ushort)19) });
        Assert.True(AbilityHostile.ApplySweep(caster, recipient, ability));
        var boosted = Assert.Single(subject.Effects.Snapshot(), effect => effect.Parent == ability);
        Assert.Equal(baseChance, boosted.Value);
        Assert.Equal(baseChance + 1, boosted.SweepChance);

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        AbilityHostile.Pulse(subject, boosted, new SweepRoll(baseChance + 1));
        Assert.True(subject.Effects.HasParent(86));
        AbilityHostile.Pulse(subject, boosted, new SweepRoll(baseChance));
        Assert.False(subject.Effects.HasParent(86));

        Assert.True(AbilityHostile.ApplySweep(caster, recipient, ability));
        Assert.Equal(baseChance, Assert.Single(subject.Effects.Snapshot(), effect => effect.Parent == ability).SweepChance);
        Assert.True(AbilityHostile.ApplySweep(recipient, recipient, 659));
        Assert.Null(Assert.Single(subject.Effects.Snapshot(), effect => effect.Parent == 659).SweepChance);
    }

    [Theory]
    [InlineData((ushort)71, 0)]
    [InlineData((ushort)370, 2)]
    public void SweepPassiveRetainsItsOtherTypedConsumers(ushort passive, int expectedDefense)
    {
        var caster = Client();
        var recipient = Client();
        Assert.Equal(180, AbilityHealing.Amount(caster, recipient, 1098));
        AbilityPassives.SetLoaded(caster, new[] { (passive, (ushort)50) });
        Assert.Equal(5, AbilityStats.Player(caster, 1052));
        Assert.Equal(5, AbilityStats.Influence(caster, 174));
        Assert.Equal(188, AbilityHealing.Amount(caster, recipient, 1098));
        Assert.Equal(expectedDefense, AbilityStats.Defense(caster, ranged: false));
        Assert.Equal(expectedDefense, AbilityStats.Defense(caster, ranged: true));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(180, AbilityHealing.Amount(caster, recipient, 1098));
        Assert.Equal(0, AbilityStats.Defense(caster, ranged: false));
        Assert.Equal(0, AbilityStats.Defense(caster, ranged: true));
    }

    [Fact]
    public void SweepPartyDoesNotCleanseOnLandingButPulsesWithTheStoredChance()
    {
        var recipient = Client();
        Land(recipient, 315);
        Assert.True(AbilityBuffs.Apply(recipient, 327));
        var subject = TimedEffects.ForPlayer(recipient);
        var previous = AbilityHostile.PulseRandom;
        try
        {
            AbilityHostile.PulseRandom = new ZeroRoll();
            Assert.True(AbilityHostile.ApplySweep(recipient, recipient, 487));
            var sweep = Assert.Single(subject.Effects.Snapshot(), effect => effect.Parent == 487);
            Assert.True(subject.Effects.HasParent(315));
            AbilityHostile.PulseRandom = new SweepRoll(30);
            TimedEffects.RunDue(sweep.Start + 5.01);
            Assert.True(subject.Effects.HasParent(315));
            AbilityHostile.PulseRandom = new SweepRoll(29);
            TimedEffects.RunDue(sweep.Start + 10.01);
            Assert.False(subject.Effects.HasParent(315));
            Assert.True(subject.Effects.HasParent(734));
            Assert.True(AbilityBuffs.IsActive(recipient, 327));
            TimedEffects.RunDue(sweep.Start + 25.01);
            Assert.False(subject.Effects.HasParent(487));
        }
        finally { AbilityHostile.PulseRandom = previous; }
    }

    [Fact]
    public void CancelUpgradeTwoRemovesOneRemovableParentAndKeepsItsOverflowUntilTerm()
    {
        var caster = Client();
        var recipient = Client();
        Assert.True(AbilityBuffs.Apply(recipient, 327));
        var subject = TimedEffects.ForPlayer(recipient);
        Assert.True(TempMods.TryGet(488, out var effect));
        double start = TimeUtils.GetSimTimeSeconds();
        TimedEffects.Apply(subject, effect, start, caster, "Source");
        AbilityHostile.LandedOnPlayer(caster, recipient, 488, new ZeroRoll());
        Assert.False(AbilityBuffs.IsActive(recipient, 327));
        Assert.Equal(150, AbilityBuffBuffer.Reserved(recipient));
        TimedEffects.RunDue(start + 5.01);
        Assert.Equal(150, AbilityBuffBuffer.Reserved(recipient));
        TimedEffects.RunDue(start + 50.01);
        Assert.Equal(0, AbilityBuffBuffer.Reserved(recipient));
    }

    [Fact]
    public void CombatAuraTwoChargesFifteenEachTickAndStopsOnShortfall()
    {
        var caster = Client(innerStrength: 30);
        var previous = WorldServer.Clients;
        WorldServer.Clients = new Dictionary<string, WorldClient>();
        try
        {
            AbilityAuras.Start(caster, 485);
            Assert.True(AbilityAuras.IsOn(caster, 485));
            Assert.Equal(30, AbilityHandler.CurrentInnerStrength(caster));
            AbilityAuras.Tick(caster, 485, 1);
            Assert.Equal(15, AbilityHandler.CurrentInnerStrength(caster));
            AbilityAuras.Tick(caster, 485, 1);
            Assert.Equal(0, AbilityHandler.CurrentInnerStrength(caster));
            AbilityAuras.Tick(caster, 485, 1);
            Assert.False(AbilityAuras.IsOn(caster, 485));
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
            WorldServer.Clients = previous;
        }
    }

    [Theory]
    [InlineData(320, 18, 40)]
    [InlineData(379, 20, 35)]
    [InlineData(424, 35, 25)]
    public void TimedSweepsWaitForTheirFirstTickAndRollAtTheStoredThreshold(ushort id, int chance, float seconds)
    {
        var victim = Client();
        var subject = TimedEffects.ForPlayer(victim);
        var previous = AbilityHostile.PulseRandom;
        try
        {
            AbilityHostile.PulseRandom = new SweepRoll(chance);
            Land(victim, 343);
            Assert.True(AbilityHostile.ApplySweep(victim, victim, id));
            var pulse = Assert.Single(subject.Effects.Snapshot(), active => active.Parent == id);
            Assert.Equal(chance, pulse.Value);
            Assert.Equal(seconds, pulse.Seconds);
            Assert.Equal(5, pulse.Interval);
            Assert.True(subject.Effects.HasParent(343));

            TimedEffects.RunDue(pulse.Start + 5.01);
            Assert.True(subject.Effects.HasParent(343));
            Land(victim, 343);
            AbilityHostile.PulseRandom = new SweepRoll(chance - 1);
            TimedEffects.RunDue(pulse.Start + 10.01);
            Assert.False(subject.Effects.HasParent(343));
            TimedEffects.RunDue(pulse.Start + seconds + .01);
            Assert.False(subject.Effects.HasParent(id));
        }
        finally
        {
            AbilityHostile.PulseRandom = previous;
            TimedEffects.Clear(subject);
        }
    }

    [Theory]
    [InlineData(379, 9)]
    [InlineData(693, 6)]
    public void GroupSweepLandsOnlyOnLivingTeamMembersWithinStoredRadius(ushort id, int radius)
    {
        var caster = Client();
        var near = Client();
        var far = Client();
        var outsider = Client();
        var dead = Client();
        var players = new[] { caster, near, far, outsider, dead };
        double[] positions = { 0, radius * 100 - 1, radius * 100 + 1, 300, 500 };
        var previous = WorldServer.Clients;
        var previousWorld = Store.world;
        var team = SocialGroups.Instance.CreateTeam(77680, "SweepCaster");
        try
        {
            Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            WorldServer.Clients = new();
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                uint charId = (uint)(77680 + i);
                player.playerData.setCharID(charId);
                player.playerData.setEntityId(charId);
                player.viewMan = new ViewManager();
                player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(positions[i], 0, 0));
                WorldServer.Clients.Add(charId.ToString(), player);
                if (player != outsider)
                    SocialGroups.Instance.AddTeamMember(team.Id, charId, charId.ToString());
            }
            dead.playerData.isDead = true;
            AbilitySupport.Land(caster, id, 2);
            foreach (var player in players)
            {
                bool applied = id == 379 ? TimedEffects.ForPlayer(player).Effects.HasParent(id) : AbilityBuffs.IsActive(player, id);
                Assert.Equal(player == caster || player == near, applied);
            }
        }
        finally
        {
            foreach (var player in players)
            {
                SocialGroups.Instance.RemoveTeamMember(team.Id, player.playerData.getCharID());
                AbilityBuffs.RemoveAll(player);
                TimedEffects.Clear(TimedEffects.ForPlayer(player));
            }
            WorldServer.Clients = previous;
            Store.world = previousWorld;
        }
    }

    [Fact]
    public void UpgradeArtistKeepsItsSweepAndStatsUntilClassReplacement()
    {
        var player = Client();
        var subject = TimedEffects.ForPlayer(player);
        var previous = AbilityHostile.PulseRandom;
        try
        {
            Land(player, 343);
            AbilityHostile.PulseRandom = new SweepRoll(10);
            Assert.Same(player, Assert.Single(AbilitySupport.Targets(player, 1095, 999)));
            AbilitySupport.Land(player, 1095, 2);
            var pulse = Assert.Single(subject.Effects.Snapshot(), active => active.Parent == 1095);
            Assert.Equal(10, pulse.Value);
            Assert.Equal(5, pulse.Interval);
            Assert.Equal(float.PositiveInfinity, pulse.Seconds);
            Assert.Equal(1095, pulse.Target);
            Assert.Equal(5, AbilityStats.Player(player, 117));
            Assert.Equal(7, AbilityStats.Player(player, 564));
            Assert.Equal(20, AbilityStats.Player(player, 520));
            Assert.Equal(130, CombatInsightEngagement.Calculate(0, 0, 0, 0, AbilityStats.Player(player, 520)).Evade);
            Assert.Equal(34, AbilityCosts.Effective(player, 314));
            Assert.Equal(78, AbilityCastInterrupts.CastThroughPercent(0, 1000, (short)AbilityStats.Player(player, 564)));

            TimedEffects.RunDue(pulse.Start + 5.01);
            Assert.True(subject.Effects.HasParent(343));
            Land(player, 343);
            AbilityHostile.PulseRandom = new SweepRoll(9);
            TimedEffects.RunDue(pulse.Start + 10.01);
            Assert.False(subject.Effects.HasParent(343));
            Assert.True(AbilityBuffs.Apply(player, 1091));
            Assert.False(subject.Effects.HasParent(1095));
            Assert.True(subject.Effects.HasParent(1091));
            Assert.Equal(32, AbilityCosts.Effective(player, 314));
        }
        finally
        {
            AbilityHostile.PulseRandom = previous;
            AbilityBuffs.RemoveAll(player);
            TimedEffects.Clear(subject);
        }
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

    [Theory]
    [InlineData(324)]
    [InlineData(377)]
    public void DeleteUpgradeRemovesBuffsNotDebuffsAndNeverPicksTheLastOfSeveral(ushort ability)
    {
        var caster = Client(); var victim = Client();
        Land(victim, 343);
        Assert.True(AbilityBuffs.Apply(victim, 327));
        Assert.True(AbilityBuffs.Apply(victim, 344));
        var candidates = AbilityHostile.RemovableBuffs(victim);
        Assert.Equal(2, candidates.Count);
        ushort removed = AbilityHostile.RemoveRandomBuff(caster, victim, ability, new Random(7));
        Assert.Equal(candidates[0], removed); // randrange(0, 1) can only be 0
        Assert.False(AbilityBuffs.IsActive(victim, removed));
        Assert.True(TimedEffects.ForPlayer(victim).Effects.HasParent(343));
        AbilityHostile.RemoveRandomBuff(caster, victim, ability, new Random(7));
        Assert.Empty(AbilityHostile.RemovableBuffs(victim));
        Assert.Equal(0, AbilityHostile.RemoveRandomBuff(caster, victim, ability, new Random(7)));
    }

    private sealed class ProcRoll(double value) : Random
    {
        public int Rolls;
        public override double NextDouble() { Rolls++; return value; }
    }

    [Theory]
    [InlineData(0.499999, false, true)]
    [InlineData(0.5, false, false)]
    [InlineData(0.499999, true, false)]
    public void DevastationFieldRollsAtLandingAndStunsOnlyOnTermWithoutImmunity(
        double roll, bool immune, bool shouldStun)
    {
        var victim = Client();
        var subject = TimedEffects.ForPlayer(victim);
        double now = TimeUtils.GetSimTimeSeconds();
        if (immune)
            subject.Effects.Apply(TempMods.ControlImmunityTimerAbility, new[]
            {
                new TimedEffects.Active { Kind = TempMods.KindPlain, Target = TempMods.ISDrainTargetAbility,
                    Proc = TempMods.Proc.State, Seconds = 14 }
            }, now);
        try
        {
            Assert.True(TempMods.TryGet(468, out var effect));
            var rng = new ProcRoll(roll);
            var applied = TimedEffects.Apply(subject, effect, now, rng: rng);
            Assert.Equal(1, rng.Rolls);
            Assert.Equal(roll < 0.5, applied.Any(mod => mod.Proc == TempMods.Proc.Terminal));
            Assert.False(subject.Effects.HasAt(TempMods.StunAbility, now));
            TimedEffects.RunDue(now + 0.05);
            Assert.False(subject.Effects.HasAt(TempMods.StunAbility, now + 0.05));
            TimedEffects.RunDue(now + 0.11);
            Assert.Equal(shouldStun, subject.Effects.HasAt(TempMods.StunAbility, now + 0.11));
            if (shouldStun)
            {
                Assert.True(subject.Effects.HasControlImmunity(now + 6.10));
                Assert.Equal(6, Assert.Single(subject.Effects.Snapshot(), mod => mod.Target == TempMods.StunAbility).Seconds);
                TimedEffects.RunDue(now + 6.12);
                Assert.False(subject.Effects.HasAt(TempMods.StunAbility, now + 6.12));
                Assert.False(subject.Effects.HasControlImmunity(now + 6.12));
            }
            else
            {
                Assert.Equal(immune, subject.Effects.HasControlImmunity(now + 0.11));
            }
        }
        finally { TimedEffects.Clear(subject); }
    }

    [Theory]
    [InlineData(399, TempMods.PowerlessAbility, 10.14f, false, true)]
    [InlineData(462, TempMods.EnrageAbility, 6f, true, false)]
    public void HackerAreaControlsApplyOnlyTheirOwnActionGateUntilExpiry(
        ushort ability, ushort state, float seconds, bool canUseAbilities, bool canWithdraw)
    {
        var victim = Client();
        var applied = Assert.Single(Land(victim, ability), mod => mod.Target == state);
        var holder = TimedEffects.ForPlayer(victim).Effects;
        Assert.Equal(seconds, applied.Seconds);
        Assert.Empty(holder.HealthChanged(1, belowMaximum: true, new Random(1)));
        Assert.True(ControlActions.CanMove(holder));
        Assert.True(ControlActions.CanUseWeapons(holder));
        Assert.Equal(canUseAbilities, ControlActions.CanUseAbilities(holder));
        Assert.Equal(canWithdraw, ControlActions.CanWithdraw(holder));
        TimedEffects.RunDue(applied.Expires + 0.001);
        Assert.True(ControlActions.CanUseAbilities(holder));
        Assert.True(ControlActions.CanWithdraw(holder));
    }

    [Theory]
    [InlineData(400, 59, 10)]
    [InlineData(466, 424, 71)]
    public void PlagueZoneAppliesFiveActualHealthTicksAndThenStops(ushort ability, int capturedTotal, int tick)
    {
        var victim = Client();
        var subject = TimedEffects.ForPlayer(victim);
        Assert.True(TempMods.TryGet(ability, out var effect));
        double now = TimeUtils.GetSimTimeSeconds();
        var applied = Assert.Single(TimedEffects.Apply(subject, effect, now, rng: new MinimumDotRoll()));
        Assert.Equal(capturedTotal, applied.Value);
        Assert.Equal(1000, PvpCombat.HealthOf(victim));
        TimedEffects.RunDue(now + 2.51);
        Assert.Equal(1000 - tick, PvpCombat.HealthOf(victim));
        TimedEffects.RunDue(applied.Expires + 0.01);
        Assert.Equal(1000 - 5 * tick, PvpCombat.HealthOf(victim));
        TimedEffects.RunDue(applied.Expires + 2.5);
        Assert.Equal(1000 - 5 * tick, PvpCombat.HealthOf(victim));
    }

    private sealed class MinimumDotRoll : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
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

    [Theory]
    [InlineData(378, 693)]
    [InlineData(380, 692)]
    public void DeflectAuraChargesSevenAndDeactivatesBeforeAnUnaffordablePulse(ushort ability, ushort child)
    {
        var caster = Client(innerStrength: 13);
        var clients = WorldServer.Clients;
        WorldServer.Clients = new Dictionary<string, WorldClient> { ["deflect"] = caster };
        try
        {
            AbilityAuras.Start(caster, ability);
            Assert.True(AbilityAuras.IsOn(caster, ability));
            Assert.True(AbilityBuffs.IsActive(caster, child));
            AbilityAuras.Tick(caster, ability, 1);
            Assert.Equal(6, AbilityHandler.CurrentInnerStrength(caster));
            Assert.True(AbilityAuras.IsOn(caster, ability));
            AbilityAuras.Tick(caster, ability, 1);
            Assert.False(AbilityAuras.IsOn(caster, ability));
            Assert.Equal(6, AbilityHandler.CurrentInnerStrength(caster));
            AbilityAuras.Tick(caster, ability, 1);
            Assert.Equal(6, AbilityHandler.CurrentInnerStrength(caster));
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
            AbilityBuffs.RemoveAll(caster);
            WorldServer.Clients = clients;
        }
    }

    [Fact]
    public void DeflectBulletsKeepsOneChildAcrossTwoCastersAndRechecksRadiusOnPaidPulses()
    {
        var first = Participant(77650, 0, 22);
        var second = Participant(77651, 600, 30, 14);
        var teammate = Participant(77652, 300, 22);
        var outside = Participant(77653, -301, 22);
        var downed = Participant(77654, 150, 22);
        var outsider = Participant(77655, 150, 22);
        downed.playerData.isDead = true;

        var groups = SocialGroups.Instance;
        var team = groups.CreateTeam(77650, "First");
        groups.AddTeamMember(team.Id, 77651, "Second");
        groups.AddTeamMember(team.Id, 77652, "Teammate");
        groups.AddTeamMember(team.Id, 77653, "Outside");
        groups.AddTeamMember(team.Id, 77654, "Downed");

        var previous = WorldServer.Clients;
        var previousWorld = Store.world;
        WorldClient[] players = { first, second, teammate, outside, downed, outsider };
        WorldServer.Clients = players.ToDictionary(player => player.playerData.getCharID().ToString());
        Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        try
        {
            AbilityAuras.Start(first, 380);
            Assert.Equal(21, AbilityStats.Player(first, 1010));
            Assert.Equal(21, AbilityStats.Player(teammate, 1010));
            Assert.All(new[] { second, outside, downed, outsider }, player => Assert.False(AbilityBuffs.IsActive(player, 692)));

            AbilityAuras.Start(second, 380);
            Assert.Equal(14, AbilityHandler.CurrentInnerStrength(second));
            var last = teammate.playerData.activeBuffs[692];
            Assert.Equal(5, last.Definition.Seconds);
            Assert.Equal(28, AbilityStats.Player(teammate, 1010));
            Assert.Equal(21, AbilityStats.Player(first, 1010));

            Assert.True(AbilityAuras.Stop(first, 380));
            Assert.InRange(first.playerData.cooldownEndsAt[380] - TimeUtils.GetSimTimeSeconds(), 9.9, 10d);
            AbilityAuras.Tick(first, 380, 1);
            Assert.Same(last, teammate.playerData.activeBuffs[692]);
            AbilityAuras.Tick(second, 380, 1);
            Assert.Equal(7, AbilityHandler.CurrentInnerStrength(second));
            var refreshed = teammate.playerData.activeBuffs[692];
            Assert.NotSame(last, refreshed);
            Assert.Equal(28, AbilityStats.Player(teammate, 1010));

            teammate.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(299, 0, 0));
            AbilityAuras.Tick(second, 380, 1);
            Assert.Equal(0, AbilityHandler.CurrentInnerStrength(second));
            Assert.Same(refreshed, teammate.playerData.activeBuffs[692]);
            AbilityAuras.Tick(second, 380, 1);
            Assert.False(AbilityAuras.IsOn(second, 380));
            TimedEffects.RunDue(refreshed.Started + 5.01);
            Assert.Equal(0, AbilityStats.Player(teammate, 1010));
        }
        finally
        {
            AbilityAuras.Clear(first, announce: false);
            AbilityAuras.Clear(second, announce: false);
            foreach (var player in players) AbilityBuffs.RemoveAll(player);
            Store.world = previousWorld;
            WorldServer.Clients = previous;
            foreach (uint id in new uint[] { 77654, 77653, 77652, 77651, 77650 }) groups.RemoveTeamMember(team.Id, id);
        }

        static WorldClient Participant(uint id, double x, byte level, ushort strength = 200)
        {
            var player = Client(innerStrength: strength);
            player.playerData.setCharID(id);
            player.playerData.setEntityId(id);
            player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 0, 0));
            player.playerInstance.Level.setValue(level);
            player.viewMan = new ViewManager();
            return player;
        }
    }

    [Theory]
    [InlineData(61, 3)]
    [InlineData(89, 12)]
    public void PathogenistAuraDeactivatesBeforeAnUnaffordablePulse(ushort ability, ushort upkeep)
    {
        var caster = Client(innerStrength: (ushort)(2 * upkeep - 1));
        var previous = WorldServer.Clients;
        WorldServer.Clients = new() { ["caster"] = caster };
        try
        {
            AbilityAuras.Start(caster, ability);
            Assert.True(AbilityAuras.IsOn(caster, ability));
            Assert.Equal(2 * upkeep - 1, AbilityHandler.CurrentInnerStrength(caster));

            AbilityAuras.Tick(caster, ability, 1);
            Assert.Equal(upkeep - 1, AbilityHandler.CurrentInnerStrength(caster));
            Assert.True(AbilityAuras.IsOn(caster, ability));

            AbilityAuras.Tick(caster, ability, 1);
            Assert.False(AbilityAuras.IsOn(caster, ability));
            Assert.Equal(upkeep - 1, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Contains(caster.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(), packet =>
                packet.content.AsSpan(1).SequenceEqual(AbilityBuffs.BuildExclusive(ability, false)));

            AbilityAuras.Tick(caster, ability, 1);
            Assert.Equal(upkeep - 1, AbilityHandler.CurrentInnerStrength(caster));
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
            WorldServer.Clients = previous;
        }
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
        Assert.True(AbilityAuras.Handles(609));
        Assert.Equal(699, AbilityAuras.EffectOf(609));
        Assert.Equal(5, AbilityAuras.DrainOf(609));
        Assert.Equal(5, AbilityAuras.IntervalOf(609));
        Assert.False(AbilityAuras.Handles(619)); // Daemon Boost 2.0 retains its existing CoderPets scheduler.
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

    [Theory]
    [InlineData((ushort)70, (ushort)18, 3)]
    [InlineData((ushort)74, (ushort)19, 1)]
    public void StarterRecoveryPassivesFeedTheStrictSweepThresholdAtStockRanks(ushort ability, ushort rank, int bonus)
    {
        const ushort sweep = 320, slow = 86;
        var caster = Client();
        var recipient = Client();
        var target = TimedEffects.ForPlayer(recipient);
        AbilityPassives.SetLoaded(caster, new[] { (ability, rank) });
        Assert.Equal(bonus, AbilityStats.Player(caster, 1052));
        Land(recipient, slow);

        Assert.True(AbilityHostile.ApplySweep(caster, recipient, sweep));
        var loadedPulse = Assert.Single(target.Effects.Snapshot(), effect => effect.Parent == sweep);
        Assert.Equal(18, loadedPulse.Value);
        AbilityHostile.Pulse(target, loadedPulse, new SweepRoll(18 + bonus));
        Assert.True(target.Effects.HasParent(slow));
        AbilityHostile.Pulse(target, loadedPulse, new SweepRoll(17 + bonus));
        Assert.False(target.Effects.HasParent(slow));

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Player(caster, 1052));
        Land(recipient, slow);
        Assert.True(AbilityHostile.ApplySweep(caster, recipient, sweep));
        var unloadedPulse = Assert.Single(target.Effects.Snapshot(), effect => effect.Parent == sweep);
        Assert.Equal(18, unloadedPulse.Value);
        AbilityHostile.Pulse(target, unloadedPulse, new SweepRoll(18));
        Assert.True(target.Effects.HasParent(slow));
        TimedEffects.Clear(target);
    }

    [Fact]
    public void QuickRecoveryUsesOnlyLoadedHealthAndSweepModifiersDespiteItsMoveFlags()
    {
        const ushort recovery = 70;
        var entry = AbilityCatalog.Find(recovery);
        Assert.Equal(1, entry.Number("Ability/HasSuccessInterlockMove"));
        Assert.Equal(1, entry.Number("Ability/HasDeflectionInterlockMove"));
        foreach (string field in new[] { "CastWhenUsed", "CastDirectObjects", "TestFunction", "CombatSpecialMove" })
            Assert.False(entry.Has($"Ability/{field}"));
        Assert.Equal(new ushort[] { 1052, 1057 }, entry.Modifiers.Select(modifier => modifier.Target).Order().ToArray());
        Assert.All(entry.Modifiers, modifier =>
        {
            Assert.Equal(0, modifier.Number("WhenEnabled", -1));
            Assert.Equal(2, modifier.Number("DurationType", -1));
        });
        Assert.False(AbilitySupport.Handles(recovery));
        Assert.False(CombatSpecials.TargetsEnemy(recovery));

        var caster = Client(health: 500);
        var loaded = new[] { (recovery, (ushort)18) };
        AbilityPassives.SetLoaded(caster, loaded, newWorld: true);
        Assert.Equal(8, AbilityStats.Player(caster, 1057));
        Assert.Equal(540, NumericalUtils.ByteArrayToUint16(caster.playerInstance.MaxHealth.getValue(), 1));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(500, NumericalUtils.ByteArrayToUint16(caster.playerInstance.MaxHealth.getValue(), 1));
        AbilityPassives.SetLoaded(caster, loaded);
        Assert.Equal(540, NumericalUtils.ByteArrayToUint16(caster.playerInstance.MaxHealth.getValue(), 1));
    }
}
