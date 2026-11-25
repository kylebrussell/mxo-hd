using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[CollectionDefinition("SniperParityWorld", DisableParallelization = true)]
public class SniperParityWorldCollection { }

[Collection("SniperParityWorld")]
public class SniperParityTests
{
    private sealed class FixedRoll(double roll) : Random
    {
        public override double NextDouble() => roll;
    }

    private static WorldClient Client()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.Health.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)200);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void ConcealedShotUsesItsFixedBallisticBaseForIncompleteLegacyEndpoints()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith("504;", StringComparison.Ordinal)).Split(';');
        var item = new AbilityItem();
        item.setAbilityID(504);
        item.setValueFrom(short.Parse(row[9]));
        item.setValueTo(ushort.Parse(row[10]));

        Assert.Equal(4, item.getValueFrom());
        Assert.Equal(0, item.getValueTo());
        Assert.Equal(1, TypedAbilityResistance.StoredType(504));
        Assert.Null(CombatSpecials.Damage(504, null));
        Assert.False(AbilityTargets.Retail.ContainsKey(504));
        foreach (int seed in new[] { 1, 2, 987 })
            Assert.Equal(22, AbilityHandler.AbilityDamage(item, new Random(seed)));

        item.setValueFrom(29);
        item.setValueTo(29);
        Assert.Equal(29, AbilityHandler.AbilityDamage(item, new Random(1)));
    }

    [Theory]
    [InlineData(505, 47, 60, 45f, 0, 0f, 30, 6f)]
    [InlineData(454, 61, 45, 10f, 689, 5f, 20, 2f)]
    public void SniperAndDeadlyShotsUseTheirFixedBallisticBasesAndStoredCastTerms(ushort id, int damage, int cost,
        float own, int group, float groupSeconds, int range, float cast)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith(id + ";", StringComparison.Ordinal)).Split(';');
        var item = new AbilityItem();
        item.setAbilityID(id);
        item.setValueFrom(short.Parse(row[9]));
        item.setValueTo(ushort.Parse(row[10]));

        // The legacy CSV endpoints (4..0) are incomplete, so the single authored instant Ballistic health modifier wins.
        Assert.True(item.getValueTo() < item.getValueFrom());
        Assert.Equal(1, TypedAbilityResistance.StoredType(id));
        Assert.Null(CombatSpecials.Damage(id, null));
        foreach (int seed in new[] { 1, 2, 987 })
            Assert.Equal(damage, AbilityHandler.AbilityDamage(item, new Random(seed)));

        var entry = AbilityCatalog.Find(id)!;
        Assert.Equal(cost, AbilityCosts.Cost(id));
        Assert.True(AbilityCooldowns.TryGet(id, out var cd));
        Assert.Equal(own, cd.Seconds);
        Assert.Equal(group, cd.Group);
        Assert.Equal(groupSeconds, cd.GroupSeconds);
        Assert.Equal(range, entry.Number("Ability/SelectionRange"));
        Assert.Equal(cast, entry.Number("Ability/CastingTime"));
        Assert.Equal(1, entry.Number("Ability/CastWhenUsed"));
        Assert.Equal(1, entry.Number("Ability/TestFunction"));
        Assert.Equal(CombatStyles.Rifles, CombatStyles.RequiredStyle(id));
        Assert.True(AbilityConditionalControls.HasCapturedStunStack(id));
    }

    [Theory]
    [InlineData(504, .30f, .10f)]
    [InlineData(505, .50f, .01f)]
    public void ConcealedAndSniperShotsUseTheirOwnDelayedStunProbabilities(ushort id, float chance, float delay)
    {
        var missed = TimedEffects.ForPlayer(Client());
        var hit = TimedEffects.ForPlayer(Client());
        var immune = TimedEffects.ForPlayer(Client());
        Assert.True(TempMods.TryGet(id, out var effect));
        var marker = Assert.Single(effect.Mods);
        Assert.Equal(delay, marker.Seconds);
        Assert.Equal(chance, marker.Chance);
        Assert.Equal(4, marker.MinValue);
        double now = TimeUtils.GetSimTimeSeconds();
        immune.Effects.Apply(734, new[] { new TimedEffects.Active { Target = 681, Proc = TempMods.Proc.State, Seconds = 10 } }, now);
        TimedEffects.Apply(immune, effect, now, rng: new FixedRoll(0));
        Assert.Empty(TimedEffects.Apply(missed, effect, now, rng: new FixedRoll(chance)));
        Assert.Single(TimedEffects.Apply(hit, effect, now, rng: new FixedRoll(chance - .01)));
        TimedEffects.RunDue(now + delay - .001);
        Assert.False(hit.Effects.Has(TempMods.StunAbility));
        TimedEffects.RunDue(now + delay + .001);
        Assert.False(missed.Effects.Has(TempMods.StunAbility));
        Assert.False(immune.Effects.Has(TempMods.StunAbility));
        Assert.Equal(4, Assert.Single(hit.Effects.Snapshot(), active => active.Target == TempMods.StunAbility).Seconds);
        Assert.Equal(4, Assert.Single(hit.Effects.Snapshot(), active => active.Parent == TempMods.ControlImmunityTimerAbility).Seconds);
        TimedEffects.RunDue(now + delay + 4.01);
        Assert.True(hit.Effects.IsEmpty);
        TimedEffects.Clear(immune);
    }

    [Fact]
    public void ConcealmentUsesItsLearnedGrantRankInsteadOfCharacterLevel()
    {
        var client = Client();
        client.playerInstance.Level.setValue((byte)19);
        AbilityPassives.SetLoaded(client, new[] { ((ushort)503, (ushort)18) }, true);

        Assert.Equal(18, AbilityProgression.GrantLevel(503, 18));
        Assert.Equal(50, AbilityProgression.GrantLevel(503, 51));
        Assert.Equal(18, AbilityPassives.Level(client, Stealth.ConcealmentGrantAbility));
        Assert.Equal(18, Stealth.AbilityLevel(client, Stealth.ConcealmentGrantAbility));
        Assert.Equal(2300, Stealth.ConcealmentWatchingRange(client));
        AbilityPassives.SetLoaded(client, new[] { ((ushort)503, (ushort)18), ((ushort)151, (ushort)30) }, true);
        Assert.Equal(1100, Stealth.ConcealmentWatchingRange(client));
        var modifiers = Stealth.ModifiersFor(Stealth.Defs[Stealth.ConcealmentGrantAbility], 18, 19);
        Assert.Contains(modifiers, modifier => modifier.Stat == 209 && modifier.Value == 18);

        AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>(), true);
        Assert.False(AbilityPassives.IsLoaded(client, Stealth.ConcealmentGrantAbility));
    }

    [Fact]
    public void ConcealmentPassivesFeedBallisticDamageAndLiveInnerStrengthTicks()
    {
        var client = Client();
        var previousClients = WorldServer.Clients;
        try
        {
            WorldServer.Clients = new();
            AssertInputAndRecovery(100, 116);
            AbilityPassives.SetLoaded(client, new[] { ((ushort)503, (ushort)50) });
            Assert.Equal(10, AbilityStats.Player(client, 1059));
            Assert.Equal(5, AbilityStats.Player(client, 1139));
            AssertInputAndRecovery(104, 117);
            AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>());
            AssertInputAndRecovery(100, 116);
        }
        finally
        {
            CombatManager.RemoveClient(client);
            WorldServer.Clients = previousClients;
        }

        void AssertInputAndRecovery(ushort ballistic, ushort secondTick)
        {
            Assert.Equal(ballistic, AbilityStats.ScaleTypedAbilityDamage(client, 1, 100));
            Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(client, 0, 100));
            client.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
            Assert.Null(AbilityHandler.RegenerateInnerStrength(client));
            client.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
            Assert.Equal((ushort?)108, AbilityHandler.RegenerateInnerStrength(client));
            Assert.Equal((ushort?)secondTick, AbilityHandler.RegenerateInnerStrength(client));
        }
    }

    [Fact]
    public void ConcealmentDeterminationShortensNewHostileControlAtTheMitigationThreshold()
    {
        var caster = Client();
        caster.playerInstance.Level.setValue((byte)1);
        var recipient = Client();
        var subject = TimedEffects.ForPlayer(recipient);
        Assert.True(TempMods.TryGet(40, out var effect));
        AssertDuration(25);

        AbilityPassives.SetLoaded(recipient, new[] { ((ushort)503, (ushort)50) });
        Assert.Equal(10, AbilityControlDurations.Determination(recipient));
        AssertDuration(23.35f);

        AbilityPassives.SetLoaded(recipient, Array.Empty<(ushort, ushort)>());
        AssertDuration(25);

        void AssertDuration(float expected)
        {
            var applied = TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds(), caster);
            Assert.Equal(expected, Assert.Single(applied, mod => mod.Target == TempMods.RootedAbility).Seconds, 2);
            TimedEffects.Clear(subject);
        }
    }

    [Fact]
    public void UnloadingConcealmentEndsOnlyItsGrantedStealthState()
    {
        var client = Client();
        (ushort, ushort) parent = (503, 19);
        (ushort, ushort) shot = (504, 19);
        AbilityPassives.SetLoaded(client, new[] { parent, shot }, true);
        var concealment = Stealth.Begin(client, Stealth.Defs[Stealth.ConcealmentGrantAbility], 19);
        try
        {
            AbilityPassives.SetLoaded(client, new[] { parent });
            Assert.Same(concealment, Stealth.Current(client));
            AbilityPassives.SetLoaded(client, new[] { shot });
            Assert.Null(Stealth.Current(client));
            Assert.Equal(0u, PvpFlag.FlagsOf(client) & (Stealth.ConcealmentFlag | Stealth.StealthedFlag));

            AbilityPassives.SetLoaded(client, new[] { parent });
            Assert.Null(Stealth.Current(client));
            var sneak = Stealth.Begin(client, Stealth.Defs[Stealth.SneakGrantAbility], 19);
            AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>());
            Assert.Same(sneak, Stealth.Current(client));
        }
        finally { Stealth.Cleared(client); }
    }

    [Fact]
    public void SniperDisciplineGrantsTheUpgradeAndFeedsBallisticAttackAndDamage()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new[] { ((ushort)508, (ushort)50) }, true);
        Assert.True(AbilityPassives.IsLoaded(client, 1136));
        Assert.Equal(2, AbilityPassives.Total(client, 996));
        Assert.Equal(2, AbilityStats.Influence(client, 171));
        Assert.Equal(1, AbilityStats.Attack(client, true));
        Assert.Equal(101, AbilityStats.ScaleTypedAbilityDamage(client, 1, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(client, 0, 100));
        AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>(), true);
        Assert.False(AbilityPassives.IsLoaded(client, 1136));
        Assert.Equal(0, AbilityStats.Attack(client, true));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(client, 1, 100));
    }

    [Theory]
    [InlineData(495, 1072, new[] { 106, 524, 171, 1010 }, new[] { 110, 6, 6, 7 })]
    [InlineData(508, 1136, new[] { 106, 524, 171, 1010 }, new[] { 60, 10, 4, 1 })]
    [InlineData(154, 1061, new[] { 106, 180, 181 }, new[] { 50, 20, 20 })]
    public void GunmanClassUpgradesApplyTheirOwnStatsAndRemoveThemOnReplacement(
        ushort parent, ushort upgrade, int[] stats, int[] values)
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new[] { (parent, (ushort)50) }, true);
        Assert.True(AbilityPassives.IsLoaded(client, upgrade));
        Assert.Same(client, Assert.Single(AbilitySupport.Targets(client, upgrade, 123)));
        AbilitySupport.Land(client, upgrade, 123);
        for (int i = 0; i < stats.Length; i++)
            Assert.Equal(values[i], AbilityBuffs.Total(client, (ushort)stats[i]));
        Assert.Equal(500 + values[0], NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1));
        if (upgrade == 1061)
        {
            Assert.Equal(20, AbilityStats.Toughness(client, false));
            Assert.Equal(20, AbilityStats.Toughness(client, true));
        }
        else
        {
            Assert.True(AbilityStats.Attack(client, true) >= values[1]);
            Assert.True(AbilityStats.Influence(client, 171) >= values[2]);
            Assert.Equal(values[3], AbilityStats.Defense(client, true));
        }

        Assert.True(AbilityBuffs.Apply(client, 1060));
        Assert.False(AbilityBuffs.IsActive(client, upgrade));
        Assert.Equal(500, NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1));
        Assert.True(AbilityBuffs.Remove(client, 1060));
    }
}
