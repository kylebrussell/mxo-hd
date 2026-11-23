using hds;
using hds.shared;
using System.Runtime.CompilerServices;

namespace hds.Tests;

[CollectionDefinition("AbilityParityWorld", DisableParallelization = true)]
public class AbilityParityWorldCollection { }

[Collection("AbilityParityWorld")]
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

    [Theory]
    [InlineData(50, 6, 11)]
    [InlineData(434, 7, 32)]
    [InlineData(435, 9, 30)]
    [InlineData(437, 9, 123)]
    public void HackerGroupHealsLandShippedAmountOnLivingTeammatesInRange(ushort id, int radius, int amount)
    {
        var caster = Client();
        var near = Client();
        var far = Client();
        var outsider = Client();
        var dead = Client();
        var roster = new[] { caster, near, far, outsider, dead };
        for (int i = 0; i < roster.Length; i++)
        {
            uint character = (uint)(77600 + i);
            roster[i].playerData.setCharID(character);
            roster[i].playerData.setEntityId(character);
            roster[i].playerData.setOnWorld(true);
            roster[i].viewMan = new();
        }
        near.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(radius * 100 - 1, 495, 0));
        far.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(radius * 100 + 1, 495, 0));
        dead.playerData.isDead = true;
        dead.playerInstance.Health.setValue((ushort)0);
        var groups = hds.world.Structures.SocialGroups.Instance;
        var team = groups.CreateTeam(77600, "Medic");
        var original = WorldServer.Clients;
        var previousWorld = Store.world;
        try
        {
            Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            var nearView = outsider.viewMan.addView(near.playerData.getEntityId(), 1);
            nearView.viewCreated = true;
            groups.AddTeamMember(team.Id, 77601, "Near");
            groups.AddTeamMember(team.Id, 77602, "Far");
            groups.AddTeamMember(team.Id, 77604, "Dead");
            WorldServer.Clients = roster.ToDictionary(c => c.playerData.getCharID().ToString());
            Assert.Equal(radius, AbilityRadius.Metres(caster, id));
            AbilitySupport.Land(caster, id, 2);
            Assert.Equal(100 + amount, PvpCombat.HealthOf(caster));
            Assert.Equal(100 + amount, PvpCombat.HealthOf(near));
            Assert.Equal(100, PvpCombat.HealthOf(far));
            Assert.Equal(100, PvpCombat.HealthOf(outsider));
            Assert.Equal(0, PvpCombat.HealthOf(dead));
            var expected = WorldServer.BuildViewUpdate(nearView.ViewID,
                PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16((ushort)(100 + amount)))));
            Assert.Contains(outsider.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                update => update.content.SequenceEqual(expected));
        }
        finally
        {
            Store.world = previousWorld;
            WorldServer.Clients = original;
            foreach (var client in roster)
                groups.RemoveTeamMember(team.Id, client.playerData.getCharID());
        }
    }

    [Theory]
    [InlineData(83, 1082, new[] { 183, 172, 342 }, new[] { 2, 2, 4 })]
    [InlineData(390, 1079, new[] { 114, 564, 342 }, new[] { 10, 7, 6 })]
    [InlineData(92, 1089, new[] { 174, 564, 520, 127 }, new[] { 3, 7, 10, 7 })] // Capture override: GOB radius is +5.
    [InlineData(439, 1093, new[] { 127, 564, 174 }, new[] { 10, 7, 10 })]
    public void HackerDisciplineGrantsExposeTheirShippedUpgradeStatsUntilRemoval(
        ushort parent, ushort upgrade, int[] stats, int[] values)
    {
        var caster = Client();
        caster.playerInstance.Level.setValue((byte)50);
        AbilityPassives.SetLoaded(caster, new[] { (parent, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(caster, upgrade));
        Assert.Same(caster, Assert.Single(AbilitySupport.Targets(caster, upgrade, 123)));
        AbilitySupport.Land(caster, upgrade, 123);
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(values[i], AbilityStats.Player(caster, (ushort)stats[i]));
        Assert.True(AbilityBuffs.Remove(caster, upgrade));
        foreach (int stat in stats)
            Assert.Equal(0, AbilityStats.Player(caster, (ushort)stat));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(caster, upgrade));
    }

    [Fact]
    public void TraumaSurgeonChangesTheSharedHealAndRadiusAndAddsSevenConcentration()
    {
        var caster = Client();
        var near = Client();
        var edge = Client();
        var outside = Client();
        var roster = new[] { caster, near, edge, outside };
        for (int i = 0; i < roster.Length; i++)
        {
            uint character = (uint)(77620 + i);
            roster[i].playerData.setCharID(character);
            roster[i].playerData.setEntityId(character);
            roster[i].playerData.setOnWorld(true);
            roster[i].viewMan = new();
        }
        near.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(200, 495, 0));
        edge.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 900));
        outside.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 950));
        var selected = caster.viewMan.addView(outside.playerData.getEntityId(), 1);
        selected.viewCreated = true;
        caster.playerInstance.Level.setValue((byte)50);
        caster.playerData.combatStyle = CombatStyles.HackerStyle;
        var profile = new NativeQa.Modes.TraumaSurgeonSupportMode().Plan.Abilities.Select(id => (id, (ushort)50));
        AbilityPassives.SetLoaded(caster, profile);
        Assert.True(AbilityPassives.IsLoaded(caster, 1093));
        var groups = hds.world.Structures.SocialGroups.Instance;
        var team = groups.CreateTeam(77620, "Trauma");
        var original = WorldServer.Clients;
        var previousWorld = Store.world;
        try
        {
            Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            foreach (var member in roster.Skip(1))
                groups.AddTeamMember(team.Id, member.playerData.getCharID(), "Support");
            WorldServer.Clients = roster.ToDictionary(client => client.playerData.getCharID().ToString());

            Assert.Equal(20, AbilityStats.Influence(caster, 564));
            Assert.Equal(8.68, AbilityRadius.Metres(caster, 371), 5);
            AbilitySupport.Land(caster, 371, selected.ViewID);
            Assert.Equal(new[] { 128, 128, 100, 100 }, roster.Select(client => (int)PvpCombat.HealthOf(client)));

            // Invoke the production support executor directly. The fixture collects the actual 1093 stock request.
            AbilitySupport.Land(caster, 1093, selected.ViewID);
            Assert.Equal(27, AbilityStats.Influence(caster, 564));
            Assert.Equal(9.38, AbilityRadius.Metres(caster, 371), 5);
            Assert.All(roster.Skip(1), member => Assert.False(member.playerData.activeBuffs.ContainsKey(1093)));
            AbilitySupport.Land(caster, 371, selected.ViewID);
            Assert.Equal(new[] { 158, 158, 130, 100 }, roster.Select(client => (int)PvpCombat.HealthOf(client)));

            Assert.True(AbilityBuffs.Remove(caster, 1093));
            Assert.Equal(20, AbilityStats.Influence(caster, 564));
            Assert.Equal(8.68, AbilityRadius.Metres(caster, 371), 5);
            AbilitySupport.Land(caster, 371, selected.ViewID);
            Assert.Equal(new[] { 186, 186, 130, 100 }, roster.Select(client => (int)PvpCombat.HealthOf(client)));
        }
        finally
        {
            Store.world = previousWorld;
            WorldServer.Clients = original;
            foreach (var member in roster)
                groups.RemoveTeamMember(team.Id, member.playerData.getCharID());
        }
    }

    [Fact]
    public void UpgradeMasterChangesTheLinkedConcentrationInputInTheLegalSupportProfile()
    {
        var caster = Client();
        caster.playerInstance.Level.setValue((byte)50);
        var profile = new NativeQa.Modes.TraumaSurgeonSupportMode().Plan.Abilities;
        CheckInput(profile, 20, 85, grant: true);
        CheckInput(profile.Where(id => id != 425), 15, 82, grant: false);
        CheckInput(profile, 20, 85, grant: true);

        void CheckInput(IEnumerable<ushort> loaded, short effectiveConcentration, int through, bool grant)
        {
            AbilityPassives.SetLoaded(caster, loaded.Select(id => (id, (ushort)50)));
            Assert.Equal(grant, AbilityPassives.IsLoaded(caster, 1096));
            Assert.Equal(effectiveConcentration, AbilityStats.Influence(caster, 564));
            Assert.Equal(through, AbilityCastInterrupts.CastThroughPercent(100, 500, effectiveConcentration));
        }
    }

    [Theory]
    [InlineData(76, new[] { 1049, 1045, 1030 }, new[] { 7, 11, 11 })]
    [InlineData(83, new[] { 1045, 1049, 1030 }, new[] { 1, 1, 1 })]
    [InlineData(390, new[] { 1027, 1143 }, new[] { 2, 2 })]
    [InlineData(391, new[] { 1027, 1039, 1143 }, new[] { 12, 15, 12 })]
    [InlineData(74, new[] { 127, 1052 }, new[] { 24, 5 })]
    [InlineData(433, new[] { 1059, 1141 }, new[] { 10, 5 })]
    [InlineData(439, new[] { 1054 }, new[] { 5 })]
    public void LoadedHackerPassivesApplyAndRemoveShippedStatTotals(ushort id, int[] stats, int[] values)
    {
        var caster = Client();
        caster.playerInstance.Level.setValue((byte)50);
        AbilityPassives.SetLoaded(caster, new[] { (id, (ushort)50) });
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(values[i], AbilityStats.Player(caster, (ushort)stats[i]));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        foreach (int stat in stats)
            Assert.Equal(0, AbilityStats.Player(caster, (ushort)stat));
    }

    [Theory]
    [InlineData(390, 1, 6)]
    [InlineData(391, 6, 6.9f)]
    public void SkriptKiddiePassivesReachWeaponDefenseAndPacifyDuration(ushort id, int defense, float pacify)
    {
        var caster = Client();
        caster.playerInstance.Level.setValue((byte)50);
        AbilityPassives.SetLoaded(caster, new[] { (id, (ushort)50) });
        Assert.Equal(defense, AbilityStats.Defense(caster, false));
        Assert.Equal(defense, AbilityStats.Defense(caster, true));
        Assert.Equal(pacify, AbilityControlDurations.Scale(caster, TempMods.MezAbility, 6), 3);
    }

    [Fact]
    public void TotalDedicationIncreasesHealAndRegenerationThroughLinkedStats()
    {
        var caster = Client();
        caster.playerInstance.Level.setValue((byte)50);
        caster.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        AbilityPassives.SetLoaded(caster, new[] { ((ushort)433, (ushort)50) });
        Assert.Equal(33, AbilityHealing.Amount(caster, caster, 434));
        Assert.Equal((ushort?)108, AbilityHandler.RegenerateInnerStrength(caster));
        Assert.Equal(.8, caster.playerData.innerStrengthRegenCarry, 5);
    }

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

    [Theory]
    [InlineData(595, TempMods.StunAbility, 6f, 12f)]
    [InlineData(597, TempMods.EnrageAbility, 10f, 0f)]
    [InlineData(588, TempMods.EnrageAbility, 12f, 0f)]
    [InlineData(618, TempMods.StunAbility, 4f, 0f)]
    [InlineData(618, TempMods.EnrageAbility, 8f, 0f)]
    public void AdvancedCoderControlTemplatesDriveExpiry(ushort ability, ushort state, float seconds, float immunity)
    {
        Assert.True(TempMods.TryGet(ability, out var effect));
        var modifier = Assert.Single(effect.Mods, mod => mod.Proc == TempMods.Proc.State && mod.Target == state);
        Assert.Equal(seconds, modifier.Seconds);
        Assert.Equal(immunity, effect.ImmunitySeconds);

        var holder = new TimedEffects.Holder();
        holder.Apply(ability, TimedEffects.Instantiate(effect, new Random(1)), 100);
        Assert.True(holder.HasAt(state, 100 + seconds - .01));
        Assert.False(holder.HasAt(state, 100 + seconds));
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
        Assert.Equal(TempMods.DOTPlaceHolderAbility, hot.Target);
        Assert.Equal(100, Value(client.playerInstance.Health)); // no immediate heal; the stored 14 is the HOT's total
        Assert.Equal(15f, hot.Seconds);
        Assert.Equal(2.5, hot.Interval);
        TimedEffects.RunDue(hot.Start + 2.51);
        Assert.Equal(102, Value(client.playerInstance.Health));
        TimedEffects.RunDue(hot.Start + 15.1);
        Assert.Equal(110, Value(client.playerInstance.Health)); // five script-rounded two-point ticks before expiry
        Assert.True(TimedEffects.ForPlayer(client).Effects.IsEmpty);
    }

    [Fact]
    public void EmergencyRepairsClampsTheRuntimeTickAndLeavesFullHealthUntouched()
    {
        var client = Client();
        client.playerInstance.Health.setValue((ushort)499);
        AbilitySupport.Land(client, 169, 2);
        var hot = Assert.Single(TimedEffects.ForPlayer(client).Effects.Snapshot());
        Assert.Equal(499, PvpCombat.HealthOf(client));
        Assert.Equal(2, hot.TickDamage);
        TimedEffects.RunDue(hot.Start + 2.51);
        Assert.Equal(500, PvpCombat.HealthOf(client));
        TimedEffects.RunDue(hot.Expires + .01);
        Assert.Equal(500, PvpCombat.HealthOf(client));
        Assert.True(TimedEffects.ForPlayer(client).Effects.IsEmpty);
    }

    [Fact]
    public void TypedHealingInfluenceChangesStoredHotTotalButNotUntypedInhaler()
    {
        var caster = Client();
        caster.playerInstance.Level.setValue((byte)50);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (71, 50), (233, 50), (409, 50) });
        Assert.Equal(15, AbilityStats.Influence(caster, 174));
        Assert.Equal(42, AbilityHealing.Amount(caster, caster, 54));
        Assert.Equal(16, AbilityHealing.Amount(caster, caster, 169));
        Assert.Equal(15, AbilityHealing.Amount(caster, caster, 716));
        AbilitySupport.Land(caster, 169, 2);
        var effect = Assert.Single(TimedEffects.ForPlayer(caster).Effects.Snapshot());
        Assert.Equal(3, effect.TickDamage);
        TimedEffects.RemoveParent(TimedEffects.ForPlayer(caster), 169);
    }

    [Theory]
    [InlineData(69, 2, 5.0, 5)]
    [InlineData(141, 7, 2.5, 5)]
    [InlineData(253, -1, 2.5, 3)]
    [InlineData(281, 7, 5.0, 9)]
    [InlineData(466, 2, 2.5, 5)]
    public void DotUsesTheModifierTypeAndStoredCadenceUntilExpiry(ushort ability, int type, double interval, int count)
    {
        Assert.True(TempMods.TryGet(ability, out var effect));
        var dot = Assert.Single(TimedEffects.Instantiate(effect, new Random(7)), mod => mod.Proc == TempMods.Proc.Dot);
        Assert.Equal(type, dot.DamageType);
        Assert.Equal(interval, dot.Interval);
        var holder = new TimedEffects.Holder();
        holder.Apply(ability, new[] { dot }, 10);
        var ticks = new List<TimedEffects.Active>();
        var expired = new List<TimedEffects.Active>();
        holder.Advance(10 + interval - .01, ticks, expired);
        Assert.Empty(ticks);
        holder.Advance(10 + interval, ticks, expired);
        Assert.Single(ticks);
        holder.Advance(10 + dot.Seconds, ticks, expired);
        Assert.Equal(count, ticks.Count);
        Assert.Same(dot, Assert.Single(expired));
    }

    [Fact]
    public void EmergencyRepairsTwoUsesTheSelectedTargetStoredTotalAndExpiresBeforeTwentiethTick()
    {
        var caster = Client();
        AbilitySupport.Land(caster, 332, 2);
        var target = TimedEffects.ForPlayer(caster);
        var effect = Assert.Single(target.Effects.Snapshot());
        Assert.Equal(76, effect.Value);
        Assert.Equal(50, effect.Seconds);
        Assert.Equal(2.5, effect.Interval);
        Assert.Equal(4, effect.TickDamage);
        Assert.Equal((ushort)100, Value(caster.playerInstance.Health));
        TimedEffects.RunDue(effect.Expires - 47.5);
        Assert.Equal((ushort)104, Value(caster.playerInstance.Health));
        TimedEffects.RunDue(effect.Expires);
        Assert.Equal((ushort)176, Value(caster.playerInstance.Health));
        Assert.Empty(target.Effects.Snapshot());
        TimedEffects.RunDue(effect.Expires + 2.5);
        Assert.Equal((ushort)176, Value(caster.playerInstance.Health));
    }

    [Fact]
    public void EmergencyRepairsTwoCapsThePulseAndWaitsThroughFullHealthWithoutEnding()
    {
        var client = Client();
        client.playerInstance.Health.setValue((ushort)499);
        client.playerInstance.Level.setValue((byte)50);
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (309, 50), (CombatStyles.HackerStyle, 50) });
        client.playerData.combatStyle = CombatStyles.HackerStyle;
        AbilityPassives.Refresh(client, false);
        Assert.Equal((ushort)24, AbilityCosts.Effective(client, 332));
        AbilitySupport.Land(client, 332, 2);
        var subject = TimedEffects.ForPlayer(client);
        var effect = Assert.Single(subject.Effects.Snapshot());
        Assert.Equal(96, effect.Value);
        Assert.Equal(5, effect.TickDamage);
        Assert.Equal((ushort)499, PvpCombat.HealthOf(client));

        TimedEffects.RunDue(effect.Expires - 47.5);
        Assert.Equal((ushort)500, PvpCombat.HealthOf(client));
        Assert.Contains(client.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
            frame => frame.content.SequenceEqual(ServerPackets.BuildSelfHealthUpdate(500)));
        int fullHealthFrames = client.messageQueue.ObjectMessagesQueue.Count;

        TimedEffects.RunDue(effect.Expires - 45);
        Assert.Equal(fullHealthFrames, client.messageQueue.ObjectMessagesQueue.Count);
        Assert.Same(effect, Assert.Single(subject.Effects.Snapshot()));

        client.playerInstance.Health.setValue((ushort)450);
        TimedEffects.RunDue(effect.Expires - 42.5);
        Assert.Equal((ushort)455, PvpCombat.HealthOf(client));
        TimedEffects.RunDue(effect.Expires);
        Assert.Equal((ushort)500, PvpCombat.HealthOf(client));
        Assert.Empty(subject.Effects.Snapshot());
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

    [Theory]
    [InlineData(150, 50, true)]
    [InlineData(150, -50, true)]
    [InlineData(150, 80, false)]
    [InlineData(-150, 0, false)]
    [InlineData(0, 0, false)]
    public void StoredGunConeUsesFullHorizontalArc(double x, double z, bool included)
    {
        var entry = AbilityCatalog.Find(493);
        Assert.Equal(13d, entry.Number("Ability/CastDirectObjects"));
        Assert.Equal(8d, entry.Number("Ability/RadiusOfEffect"));
        Assert.Equal(included, AbilityTargets.InCone(1, 0, x, z, entry.Number("Ability/AngleOfEffect")));
    }
}
