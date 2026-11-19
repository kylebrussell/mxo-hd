using hds;

namespace hds.Tests;

public class OverhandSmashDamageTests
{
    [Fact]
    public void OverhandSmashUsesTheShippedFixedBaseWhenLegacyRangeIsMissing()
    {
        const ushort id = 145, expected = 7;
        const int type = 0;
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith($"{id};", StringComparison.Ordinal)).Split(';');
        AbilityItem item = new();
        item.setAbilityID(id);
        item.setValueFrom(short.Parse(row[9]));
        item.setValueTo(ushort.Parse(row[10]));

        Assert.Equal(0, item.getValueFrom());
        Assert.Equal(0, item.getValueTo());
        Assert.Equal(type, TypedAbilityResistance.StoredType(id));
        var damage = Assert.Single(AbilityCatalog.Find(id).Modifiers, m => m.Target == 108);
        Assert.Equal(expected, damage.Number("MinValue"));
        Assert.Equal(expected, damage.Number("MaxValue"));
        Assert.Equal(1, damage.Number("DurationType"));
        Assert.Equal(0, damage.Number("ValueFunction"));
        Assert.Equal(2, damage.Number("WhenEnabled"));
        foreach (int seed in new[] { 1, 55, 987 })
            Assert.Equal(expected, AbilityHandler.AbilityDamage(item, new Random(seed)));

        item.setValueFrom(21);
        item.setValueTo(21);
        Assert.Equal(21, AbilityHandler.AbilityDamage(item, new Random(1)));
    }
}
