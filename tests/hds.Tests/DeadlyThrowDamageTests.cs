using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class DeadlyThrowDamageTests
{
    private static WorldClient Client(byte level = 20)
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

    [Theory]
    [InlineData(1, 21)]
    [InlineData(20, 30)]
    [InlineData(35, 37)]
    [InlineData(50, 44)]
    public void DeadlyThrowUsesShippedValueFunction3ScaledBaseWhenLegacyRangeIsIncomplete(int level, ushort expected)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith("279;", StringComparison.Ordinal)).Split(';');

        var ability = new AbilityItem();
        ability.setAbilityID(279);
        ability.setValueFrom(short.Parse(row[9]));
        ability.setValueTo(ushort.Parse(row[10]));

        Assert.True(ability.getValueFrom() <= 0 || ability.getValueTo() < ability.getValueFrom());
        Assert.Equal(7, TypedAbilityResistance.StoredType(279));
        foreach (int seed in new[] { 1, 17, 987 })
            Assert.Equal(expected, AbilityHandler.AbilityDamage(ability, new Random(seed), characterLevel: level));

        ability.setValueFrom(25);
        ability.setValueTo(25);
        Assert.Equal((ushort)25, AbilityHandler.AbilityDamage(ability, new Random(1), characterLevel: level));
    }

    [Fact]
    public void DeadlyThrowLevel20InfluenceResistanceTimersAndDebuffsMatchShippedProfile()
    {
        var caster = Client(20);
        var target = Client(20);
        // Full level-20 prerequisite closure loads Weak Points (164) alongside Wicked Aim (278) and Spy Style (1112):
        // Spy Style (+14) + Wicked Aim (+6) + Weak Points (+4) = +24% thrown influence -> trunc(30 * 1.24) = 37, Off-Balance trunc(31 * 1.24) = 38.
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (2, 20), (164, 20), (278, 20), (1112, 20) });
        CombatStyles.Select(caster, 1112);

        Assert.Equal(24, AbilityStats.Influence(caster, 1034));
        Assert.Equal((ushort)37, AbilityStats.ScaleAbilityDamage(caster, 279, 30));
        Assert.Equal(31, ConditionalThrownDamage.Value(279, 20));
        Assert.Equal((ushort)38, AbilityStats.ScaleAbilityDamage(caster, 279, 31));
        Assert.Equal((ushort)37, TypedAbilityResistance.Player(caster, target, 279, 37));

        Assert.Equal((ushort)18, AbilityCosts.Cost(279));
        Assert.Equal(2f, AbilityCatalog.Find(279).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(279, out var cd));
        Assert.Equal(10f, cd.Seconds);
        Assert.Equal((ushort)1216, cd.Group);
        Assert.Equal(3f, cd.GroupSeconds);
        Assert.True(DeadlyThrowDamage.RequiresLiveEnemy(279));
        Assert.False(AbilityTargets.HasLiveAttackSelection(caster, 0, null));

        Assert.True(TempMods.TryGet(279, out var effect));
        Assert.Contains(effect.Mods, m => m.Target == 115 && m.MinValue == -57 && m.Seconds == 15f);
        Assert.Contains(effect.Mods, m => m.Target == 877 && m.MinValue == -85 && m.Seconds == 15f);
        Assert.Contains(effect.Mods, m => m.Target == 878 && m.MinValue == -72 && m.Seconds == 15f);
        Assert.Contains(effect.Mods, m => m.Target == 519 && m.MinValue == -15 && m.Seconds == 15f);
        Assert.Contains(effect.Mods, m => m.Target == TempMods.DazeAbility && m.Seconds == 8f && Math.Abs(m.Chance - 0.2f) < 0.001f);
        Assert.Contains(effect.Mods, m => m.Target == TempMods.StaggeredAbility && m.Seconds == 8f && Math.Abs(m.Chance - 0.2f) < 0.001f);
    }
}
