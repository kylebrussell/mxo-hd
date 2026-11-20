using hds;

namespace hds.Tests;

public class StarterHackerViralDamageTests
{
    [Theory]
    [InlineData(53, 12)]
    [InlineData(57, 8)]
    [InlineData(58, 11)]
    [InlineData(59, 17)]
    [InlineData(60, 7)]
    public void IncompleteLegacyRowsUseTheShippedViralBase(ushort id, ushort amount)
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
        Assert.Equal(2, TypedAbilityResistance.StoredType(id));
        Assert.False(AbilityTargets.Retail.ContainsKey(id));
        foreach (int seed in new[] { 1, 2, 987 })
            Assert.Equal(amount, AbilityHandler.AbilityDamage(ability, new Random(seed)));

        ability.setValueFrom(23);
        ability.setValueTo(23);
        Assert.Equal(23, AbilityHandler.AbilityDamage(ability, new Random(1)));
    }

    [Fact]
    public void CapturedHighRankBarrageAndNukeRetainTheirRanges()
    {
        Assert.Equal((424, 424), ((int)AbilityTargets.Retail[330].MinDamage, (int)AbilityTargets.Retail[330].MaxDamage));
        Assert.Equal((645, 1183), ((int)AbilityTargets.Retail[359].MinDamage, (int)AbilityTargets.Retail[359].MaxDamage));
    }
}
