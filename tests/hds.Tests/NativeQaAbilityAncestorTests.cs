using System.Text.Json;
using NativeQa;

namespace hds.Tests;

public sealed class NativeQaAbilityAncestorTests
{
    [Fact]
    public void SweepAndChoreographyLoadoutsIncludeTheStockRequiredDisciplines()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "data", "ability_catalog.json")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "data", "ability_catalog.json")));
        var names = catalog.RootElement.GetProperty("abilities").EnumerateArray()
            .ToDictionary(row => row.GetProperty("id").GetUInt16(), row => row.GetProperty("name").GetString());
        var loaded = new HashSet<string?>();
        foreach (string mode in new[] { "ability-sweep-martial-artist", "ability-sweep-gunman", "style-choreography" })
        {
            var plan = NativeQaModes.Find(mode).Plan;
            Assert.True(plan.LoadAbilityAncestors);
            var ancestors = NativeQaFixtureSeeder.RequiredAncestors(catalog.RootElement, plan.Abilities);
            Assert.Empty(ancestors.Intersect(plan.Abilities));
            loaded.UnionWith(plan.Abilities.Concat(ancestors).Select(id => names[id]));
        }

        foreach (string discipline in new[] { "KarateMaster", "AikidoGrandmaster", "KungFuGrandmaster",
            "MartialArtsInitiate", "Gunman", "Gunslinger", "Rifleman", "Scattershot", "Soldier" })
            Assert.Contains(discipline + "Ability", loaded);
        Assert.False(NativeQaModes.Find("").Plan.LoadAbilityAncestors);
    }
}
