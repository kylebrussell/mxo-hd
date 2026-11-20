using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class CripplingThrowTests
{
    private static WorldClient Client(byte level = 25)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void CripplingThrowUsesShippedFixedThrownBaseWhenLegacyRangeIsIncomplete()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith("280;", StringComparison.Ordinal)).Split(';');

        var ability = new AbilityItem();
        ability.setAbilityID(280);
        ability.setValueFrom(short.Parse(row[9]));
        ability.setValueTo(ushort.Parse(row[10]));

        Assert.True(ability.getValueFrom() <= 0 || ability.getValueTo() < ability.getValueFrom());
        Assert.Equal(7, TypedAbilityResistance.StoredType(280));
        var damage = Assert.Single(AbilityCatalog.Find(280).Modifiers, m => m.Target == 108);
        Assert.Equal(1, damage.Number("MinValue"));
        Assert.Equal(1, damage.Number("MaxValue"));
        Assert.Equal(1, damage.Number("DurationType"));
        Assert.Equal(2, damage.Number("WhenEnabled"));

        foreach (int seed in new[] { 1, 42, 987 })
            Assert.Equal((ushort)1, AbilityHandler.AbilityDamage(ability, new Random(seed)));

        ability.setValueFrom(19);
        ability.setValueTo(19);
        Assert.Equal((ushort)19, AbilityHandler.AbilityDamage(ability, new Random(1)));
    }

    [Fact]
    public void CripplingThrowKeepsTypedInfluenceResistanceAndTimers()
    {
        var caster = Client(25);
        var target = Client(25);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (2, 25), (278, 25), (1112, 25) });
        CombatStyles.Select(caster, 1112);

        // Rank-25 Spy Style (+16) + Wicked Aim (+7) = +23% thrown influence -> trunc(1 * 1.23) = 1.
        Assert.Equal(23, AbilityStats.Influence(caster, 1034));
        ushort scaled = AbilityStats.ScaleAbilityDamage(caster, 280, 1);
        Assert.Equal((ushort)1, scaled);
        Assert.Equal((ushort)1, TypedAbilityResistance.Player(caster, target, 280, scaled));

        Assert.Equal((ushort)20, AbilityCosts.Cost(280));
        Assert.Equal(2f, AbilityCatalog.Find(280).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(280, out var cd));
        Assert.Equal(10f, cd.Seconds);
        Assert.Equal((ushort)1215, cd.Group);
        Assert.Equal(3f, cd.GroupSeconds);
        Assert.True(SpyAbilityDamage.RequiresLiveEnemy(280));
        Assert.False(AbilityTargets.HasLiveAttackSelection(caster, 0, null));
    }

    [Fact]
    public void CripplingThrowRetainsRootedAndForceCombatDebuffs()
    {
        Assert.True(TempMods.TryGet(280, out var effect));
        Assert.Equal((ushort)99, TempMods.RootedAbility);
        var root = Assert.Single(effect.Mods, m => m.Target == TempMods.RootedAbility);
        Assert.Equal(15f, root.Seconds);
        Assert.Equal(TempMods.BreakRule.Chance, root.Break);
        Assert.True(root.ProtectFirstHealthChange);

        // Rank-25 Wicked Aim (278) adds +5% Root Duration Bonus -> 15 * 1.05 = 15.75 s.
        var caster = Client(25);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (278, 25) });
        Assert.Equal(15.75f, AbilityControlDurations.Scale(caster, TempMods.RootedAbility, root.Seconds), 4);

        var forceCombat = Assert.Single(effect.Mods, m => m.Target == 519);
        Assert.Equal(-40, forceCombat.MinValue);
        Assert.Equal(15f, forceCombat.Seconds);
    }
}
