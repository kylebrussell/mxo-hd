using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

/// <summary>
/// Row-level coverage for the Knife Thrower and Master Knife Thrower rows 279, 280, 287, 295, 341, 348 and 386:
/// every listed shipped modifier reaches the runtime effect, Force Combat reaches the live entry contest, the
/// ISDrain Target carriers (stat 710) never spend the recipient's Inner Strength, and the level-50 damage totals
/// match the stock measurements (PR1099/1140/1149/1150/1152/1158/1170).
/// </summary>
[Collection("DelayedActions")]
public sealed class KnifeThrowerRowClosureTests
{
    private const ushort DeadlyThrow = DeadlyThrowDamage.Id, CripplingThrow = SpyAbilityDamage.CripplingThrow,
        Suplex = SuplexDamage.Suplex, CannyStrike = CannyStrikeDamage.CannyStrike, SeverArtery = CombatSpecials.SeverArtery,
        StaggeringThrow = SpyMasterSneakOpeners.StaggeringThrow, FreedomToJoints = SpyMasterSneakOpeners.FreedomToJoints;

    [Fact]
    public void DeadlyThrowRuntimeEffectCarriesEveryShippedRecord()
    {
        var mods = Effect(DeadlyThrow);
        // Movement Speed / Sneak Movement / Invisibility Movement and Force Combat for 15 s (captured retail values).
        AssertStat(mods, 115, -57, 15);
        AssertStat(mods, 877, -85, 15);
        AssertStat(mods, 878, -72, 15);
        AssertStat(mods, 519, -15, 15);
        // Independent 20% Dazed and Staggered procs for 8 s.
        AssertState(mods, TempMods.DazeAbility, 8, .2f);
        AssertState(mods, TempMods.StaggeredAbility, 8, .2f);
        Assert.Equal(6, mods.Length);

        Assert.True(DeadlyThrowDamage.TryResolve(AbilityCatalog.Find(DeadlyThrow), 50, out ushort base50));
        Assert.Equal(44, base50);
        Assert.True(DeadlyThrowDamage.TryResolve(AbilityCatalog.Find(DeadlyThrow), 20, out ushort base20));
        Assert.Equal(30, base20);
        // DealDamageIfOffBalance initializer: a separate stored function-3 component.
        Assert.Equal(45, ConditionalThrownDamage.Value(DeadlyThrow, 50));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(DeadlyThrow));
        Assert.Equal(18, AbilityCosts.Cost(DeadlyThrow));
    }

    [Fact]
    public void CripplingThrowRuntimeEffectCarriesEveryShippedRecord()
    {
        var mods = Effect(CripplingThrow);
        var root = AssertState(mods, TempMods.RootedAbility, 15, 1f);
        // The stored -100 ISDrain Target sibling is the OnHealthReduceDeactivateChance payload: it protects the first
        // health change and arms the later 100% break; it is not a separate stat record.
        Assert.True(root.ProtectFirstHealthChange);
        Assert.Equal(TempMods.BreakRule.Chance, root.Break);
        Assert.Equal(100, root.BreakChance);
        AssertStat(mods, 519, -40, 15);
        Assert.DoesNotContain(mods, m => m.Target == 710 || m.Target == TempMods.ISDrainTargetAbility);
        Assert.Equal(2, mods.Length);

        var item = new AbilityItem();
        item.setAbilityID(CripplingThrow);
        Assert.Equal(1, AbilityHandler.AbilityDamage(item, new Random(0), characterLevel: 50));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(CripplingThrow));
        Assert.Equal(20, AbilityCosts.Cost(CripplingThrow));
    }

    [Fact]
    public void SuplexRuntimeEffectCarriesEveryShippedRecord()
    {
        var mods = Effect(Suplex);
        AssertState(mods, TempMods.DazeAbility, 15, 1f);
        AssertState(mods, TempMods.OffBalanceAbility, 15, 1f);
        AssertState(mods, TempMods.StaggeredAbility, 15, 1f);
        Assert.Equal(3, mods.Length);
        Assert.True(SuplexDamage.TryResolve(AbilityCatalog.Find(Suplex), 50, out ushort base50));
        Assert.Equal(120, base50);
        Assert.Equal(300, SneakAttacks.Bonus(Suplex, true));
        Assert.Equal(0, SneakAttacks.Bonus(Suplex, false));
        Assert.Equal(50, AbilityCosts.Cost(Suplex));
    }

    [Fact]
    public void CannyStrikeHasOnlyItsStoredLevelScaledThrownHit()
    {
        Assert.False(TempMods.TryGet(CannyStrike, out var effect) && effect.Mods.Length > 0);
        var entry = AbilityCatalog.Find(CannyStrike);
        var seen = new HashSet<ushort>();
        for (int seed = 0; seed < 200; seed++)
        {
            Assert.True(CannyStrikeDamage.TryResolve(entry, 50, new Random(seed), out ushort damage));
            Assert.InRange(damage, (ushort)8, (ushort)10);
            seen.Add(damage);
        }
        Assert.Contains((ushort)9, seen);
        Assert.Equal(7, TypedAbilityResistance.StoredType(CannyStrike));
        Assert.Equal(70, AbilityCosts.Cost(CannyStrike));
    }

    [Fact]
    public void SeverArteryRuntimeEffectCarriesEveryShippedRecord()
    {
        var mods = Effect(SeverArtery);
        var dot = Assert.Single(mods, m => m.Proc == TempMods.Proc.Dot);
        Assert.Equal((68, 30f, 2.5), (dot.MinValue, dot.Seconds, dot.Interval));
        AssertStat(mods, 115, -50, 30);
        AssertStat(mods, 877, -50, 30);
        AssertStat(mods, 878, -50, 30);
        AssertStat(mods, 519, -10, 30);
        // RandomCombatStateChance on the stored ISDrain Target 710 carrier: value 10, 2.5 s, 30 s.
        var marker = Assert.Single(mods, m => m.Proc == TempMods.Proc.SeverState);
        Assert.Equal((710, 10, 30f, 2.5), (marker.Target, marker.MinValue, marker.Seconds, marker.Interval));
        Assert.Equal(6, mods.Length);
        Assert.Equal(35, AbilityCosts.Cost(SeverArtery));
    }

    [Fact]
    public void StaggeringThrowAndFreedomToJointsRuntimeEffectsCarryEveryShippedRecord()
    {
        var staggering = Effect(StaggeringThrow);
        AssertState(staggering, TempMods.StaggeredAbility, 12, .5f);
        Assert.Single(staggering);
        Assert.True(SpyMasterSneakOpeners.TryResolve(AbilityCatalog.Find(StaggeringThrow), 50, out ushort staggeringBase));
        Assert.Equal(190, staggeringBase);
        Assert.Equal(45, AbilityCosts.Cost(StaggeringThrow));

        var freedom = Effect(FreedomToJoints);
        AssertState(freedom, TempMods.PowerlessAbility, 8, 1f);
        Assert.Single(freedom);
        Assert.True(SpyMasterSneakOpeners.TryResolve(AbilityCatalog.Find(FreedomToJoints), 50, out ushort freedomBase));
        Assert.Equal(92, freedomBase);
        Assert.Equal(900, SneakAttacks.Bonus(FreedomToJoints, true));
        Assert.Equal(60, AbilityCosts.Cost(FreedomToJoints));
    }

    [Theory]
    [InlineData(DeadlyThrow, -15, 15, 93)]
    [InlineData(CripplingThrow, -40, 15, 66)]
    [InlineData(SeverArtery, -10, 30, 99)]
    public void ForceCombatDebuffLowersTheAffectedPlayersEntryScoreUntilItsOwnExpiry(ushort ability, int forcePenalty,
        int seconds, long reducedScore)
    {
        using var fixture = new EntryFixture();
        AbilityPassives.SetLoaded(fixture.Source, new (ushort, ushort)[] { (2, 50), (113, 50) });
        Assert.True(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var baseline));
        Assert.Equal((110L, 110L), (baseline.Force, baseline.Evade));
        Assert.True(TempMods.TryGet(ability, out var effect));

        var affected = TimedEffects.ForPlayer(fixture.Source);
        double start = TimeUtils.GetSimTimeSeconds();
        TimedEffects.Apply(affected, effect, start, fixture.Target, "Knife thrower");

        var applied = Assert.Single(affected.Effects.Snapshot(), mod => mod.Parent == ability && mod.Target == 519);
        Assert.Equal((forcePenalty, (float)seconds), (applied.Value, applied.Seconds));
        Assert.Equal(forcePenalty, AbilityStats.Player(fixture.Source, 519));
        Assert.Equal(0, AbilityStats.Player(fixture.Target, 519));
        Assert.False(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var withDebuff));
        Assert.Equal((reducedScore, baseline.Evade), (withDebuff.Force, withDebuff.Evade));

        Assert.True(TimedEffects.RemoveParent(affected, ability));
        Assert.Equal(0, AbilityStats.Player(fixture.Source, 519));
        Assert.True(CombatInsightEngagement.Allows(fixture.Source, fixture.Target, fixture.Handle, out var restored));
        Assert.Equal((baseline.Force, baseline.Evade), (restored.Force, restored.Evade));
    }

    [Fact]
    public void CripplingThrowAndSeverArteryIsDrainCarriersNeverSpendTheRecipientsInnerStrength()
    {
        var target = Player(50941, 50);
        var subject = TimedEffects.ForPlayer(target);
        try
        {
            Assert.True(TempMods.TryGet(CripplingThrow, out var crippling));
            TimedEffects.Apply(subject, crippling, TimeUtils.GetSimTimeSeconds());
            Assert.Equal(1000, InnerStrength(target));
            TimedEffects.HealthChanged(subject, 1, belowMaximum: true, damageEvent: true);
            Assert.True(subject.Effects.Has(TempMods.RootedAbility));
            TimedEffects.HealthChanged(subject, 10, belowMaximum: true, damageEvent: true);
            Assert.False(subject.Effects.Has(TempMods.RootedAbility));
            Assert.Equal(1000, InnerStrength(target));
            Assert.DoesNotContain(subject.Effects.Snapshot(), m => m.Parent == CripplingThrow);

            Assert.True(TempMods.TryGet(SeverArtery, out var sever));
            TimedEffects.Apply(subject, sever, TimeUtils.GetSimTimeSeconds());
            var marker = Assert.Single(subject.Effects.Snapshot(), m => m.Parent == SeverArtery && m.Proc == TempMods.Proc.SeverState);
            Assert.Equal((710, 10), (marker.Target, marker.Value));
            Assert.Equal(0, AbilityStats.Player(target, 710));
            Assert.Equal(1000, InnerStrength(target));
            Assert.True(TimedEffects.RemoveParent(subject, SeverArtery));
            Assert.Equal(1000, InnerStrength(target));
        }
        finally
        {
            TimedEffects.Clear(subject);
            AbilityBuffs.RemoveAll(target);
        }
    }

    [Fact]
    public void Level50DamageTotalsMatchTheStockNeutralAndResistedMeasurements()
    {
        // Sever Artery: ThrownDOT 68 x +54% Thrown = 104 total, nine per 2.5 s tick (PR1140 neutral), seven resisted.
        var sever = Caster(NativeQa.NativeQaModes.Find("ability-parity-sever-artery-thrown-resistance").Plan.Abilities);
        Assert.Equal(54, AbilityStats.Influence(sever, 1034));
        int dotTotal = AbilityStats.ScaleTypedAbilityDamage(sever, 7, 68);
        Assert.Equal(104, dotTotal);
        int tick = TempMods.DotTick(dotTotal, 30, 2.5);
        Assert.Equal(9, tick);
        Assert.Equal(7, Assert.Single(Effect(SeverArtery), m => m.Proc == TempMods.Proc.Dot).DamageType);
        Assert.Equal(9, TypedAbilityResistance.Npc(sever, Dummy(false), SeverArtery, tick, 7));
        Assert.Equal(7, TypedAbilityResistance.Npc(sever, Dummy(true), SeverArtery, tick, 7));

        // Staggering Throw 190 -> 292 neutral, 240 resisted (PR1149/1170); Freedom to Joints 92 + 900 -> 1527 / 1256 (PR1099);
        // Suplex 120 + 300 -> 646 neutral (PR1158), 531 resisted.
        var master = Caster(NativeQa.NativeQaModes.Find("ability-parity-spy-master-sneak-openers").Plan.Abilities);
        Assert.Equal(54, AbilityStats.Influence(master, 1034));
        AssertTotals(master, StaggeringThrow, 190, 292, 240);
        AssertTotals(master, FreedomToJoints, SneakAttacks.Add(92, FreedomToJoints, true), 1527, 1256);
        AssertTotals(master, Suplex, SneakAttacks.Add(120, Suplex, true), 646, 531);
    }

    [Fact]
    public void ClosureModePlanFitsMemoryAndItsPrintedExpectationsAreTheServerTotals()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-knife-thrower-closure").Plan;
        Assert.Equal(NativeQa.Modes.KnifeThrowerClosureMode.Loadout, plan.Abilities.ToArray());
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));
        Assert.Equal(new ushort[] { CombatStyles.SpyStyle, DeadlyThrow, CripplingThrow, Stealth.SneakGrantAbility, Suplex },
            plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);

        var caster = Caster(plan.Abilities);
        Assert.True(DeadlyThrowDamage.TryResolve(AbilityCatalog.Find(DeadlyThrow), 50, out ushort deadly));
        Assert.Equal(NativeQa.Modes.KnifeThrowerClosureMode.DeadlyNeutral, AbilityStats.ScaleAbilityDamage(caster, DeadlyThrow, deadly));
        AssertTotals(caster, Suplex, SneakAttacks.Add(120, Suplex, true),
            NativeQa.Modes.KnifeThrowerClosureMode.SuplexNeutral, NativeQa.Modes.KnifeThrowerClosureMode.SuplexResisted);

        // The cast arrives in Self Defense: the loaded Spy Style is switched to before the special move is admitted.
        var fresh = Player(112, 50);
        AbilityPassives.SetLoaded(fresh, plan.Abilities.Select(id => (id, (ushort)50)).ToArray());
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(fresh));
        foreach (ushort id in new[] { DeadlyThrow, CripplingThrow, Suplex })
        {
            Assert.True(CombatStyles.IsSpecialMove(id));
            Assert.False(CombatStyles.CanUse(fresh, id));
        }
        Assert.True(CombatStyles.EnsureStyleFor(fresh, CripplingThrow));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.Active(fresh));
        Assert.True(CombatStyles.CanUse(fresh, DeadlyThrow));
    }

    private static void AssertTotals(WorldClient caster, ushort ability, ushort raw, ushort neutral, ushort resisted)
    {
        ushort scaled = AbilityStats.ScaleAbilityDamage(caster, ability, raw);
        Assert.Equal(neutral, scaled);
        Assert.Equal(neutral, TypedAbilityResistance.Npc(caster, Dummy(false), ability, scaled));
        Assert.Equal(resisted, TypedAbilityResistance.Npc(caster, Dummy(true), ability, scaled));
    }

    private static TempMods.Mod[] Effect(ushort ability)
    {
        Assert.True(TempMods.TryGet(ability, out var effect));
        return effect.Mods;
    }

    private static TempMods.Mod AssertStat(TempMods.Mod[] mods, ushort stat, int value, float seconds)
    {
        var mod = Assert.Single(mods, m => m.Proc == TempMods.Proc.Stat && m.Target == stat);
        Assert.Equal((value, value, seconds), (mod.MinValue, mod.MaxValue, mod.Seconds));
        return mod;
    }

    private static TempMods.Mod AssertState(TempMods.Mod[] mods, ushort state, float seconds, float chance)
    {
        var mod = Assert.Single(mods, m => m.Proc == TempMods.Proc.State && m.Target == state);
        Assert.Equal(seconds, mod.Seconds);
        Assert.Equal(chance, mod.Chance, 4);
        return mod;
    }

    private static int InnerStrength(WorldClient client) =>
        NumericalUtils.ByteArrayToUint16(client.playerInstance.InnerStrengthAvailable.getValue(), 1);

    private static WorldClient Caster(IEnumerable<ushort> loadout)
    {
        var client = Player(110, 50);
        AbilityPassives.SetLoaded(client, loadout.Select(id => (id, (ushort)50)).ToArray());
        CombatStyles.Select(client, CombatStyles.SpyStyle);
        return client;
    }

    private static Mob Dummy(bool resisted)
    {
        var mob = new Mob();
        mob.setName("Melee Dummy");
        mob.setLevel(50);
        mob.setHealthM(30000);
        mob.setHealthC(30000);
        if (resisted)
            TimedEffects.Apply(TimedEffects.ForMob(mob), new TempMods.Effect
            {
                Ability = 720,
                Mods = new[]
                {
                    new TempMods.Mod
                    {
                        Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = 1033,
                        MinValue = CombatFixtureWeapons.DummyPhysicalResistance,
                        MaxValue = CombatFixtureWeapons.DummyPhysicalResistance, Seconds = 300
                    }
                }
            }, TimeUtils.GetSimTimeSeconds());
        return mob;
    }

    private static WorldClient Player(uint id, byte level)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(id);
        client.playerData.setEntityId(id);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, long.MaxValue);
        typeof(WorldClient).GetField("lastUsedTime", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        client.LivenessClock = () => 0;
        client.Alive = true;
        return client;
    }

    /// <summary>The live PvP entry contest between a Power-tactic source and a shielded Speed-tactic target.</summary>
    private sealed class EntryFixture : IDisposable
    {
        internal readonly WorldClient Source = EntryClient(110), Target = EntryClient(111);
        internal readonly ClientView View;
        internal uint Handle => (uint)(View.ViewID | (View.spawnId << 16));

        internal EntryFixture()
        {
            Source.playerData.combatTactic = CombatTactic.Power;
            Target.playerData.combatTactic = CombatTactic.Speed;
            View = Source.viewMan.addView(Target.playerData.getEntityId(), 12);
            View.viewCreated = true;
            View.spawnId = 7;
            AbilityPassives.SetLoaded(Source, new (ushort, ushort)[] { (2, 50) });
            AbilityPassives.SetLoaded(Target, new (ushort, ushort)[] { (2, 50), (217, 50), (49, 50) });
            Assert.True(AbilityBuffs.Apply(Target, 217));
        }

        private static WorldClient EntryClient(uint id)
        {
            var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            c.playerData = new ClientData(); c.playerInstance = new PlayerCharacter(); c.viewMan = new ViewManager(); c.messageQueue = new MessageQueue();
            typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, new WorldSendFence());
            typeof(WorldClient).GetField("lastUsedTime", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, TimeUtils.getUnixTimeUint32());
            typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, long.MaxValue);
            c.Alive = true; c.playerData.setCharID(id); c.playerData.setEntityId(id); c.playerData.setOnWorld(true); c.playerData.setDistrictId(1);
            c.playerInstance.Level.setValue((byte)50); c.playerInstance.Health.setValue((ushort)5000); c.playerInstance.MaxHealth.setValue((ushort)5000);
            c.playerInstance.InnerStrengthAvailable.setValue((ushort)1000); c.playerInstance.InnerStrengthMax.setValue((ushort)1000);
            return c;
        }

        public void Dispose()
        {
            AbilityBuffs.Remove(Target, 217);
            AbilityBuffs.Remove(Target, 49);
            TimedEffects.Clear(TimedEffects.ForPlayer(Target));
            TimedEffects.Clear(TimedEffects.ForPlayer(Source));
        }
    }
}
