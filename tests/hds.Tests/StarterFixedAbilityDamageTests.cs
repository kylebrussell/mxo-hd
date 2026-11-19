using hds;

namespace hds.Tests;

public class StarterFixedAbilityDamageTests
{
    [Theory]
    [InlineData(36, 2, 4)]
    [InlineData(130, 1, 9)]
    [InlineData(152, 7, 28)]
    public void ShippedFixedBasesReplaceOnlyIncompleteLegacyRanges(ushort id, int type, ushort damage)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        var row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith($"{id};", StringComparison.Ordinal)).Split(';');
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setValueFrom(short.Parse(row[9]));
        ability.setValueTo(ushort.Parse(row[10]));

        Assert.True(ability.getValueFrom() <= 0 || ability.getValueTo() < ability.getValueFrom());
        Assert.Equal(type, TypedAbilityResistance.StoredType(id));
        foreach (int seed in new[] { 1, 2, 987 })
            Assert.Equal(damage, AbilityHandler.AbilityDamage(ability, new Random(seed)));

        ability.setValueFrom(19);
        ability.setValueTo(19);
        Assert.Equal(19, AbilityHandler.AbilityDamage(ability, new Random(1)));
    }
}
