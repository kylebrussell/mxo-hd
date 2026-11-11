using hds;
using System.Security.Cryptography;
using System.Text.Json;

namespace hds.Tests;

public class NpcSpawnScopeTests
{
    [Fact]
    public void CatalogIsALosslessPartitionOfTheCaptureAndMatchesItsManifest()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "mob_parsed.csv"))) root = Path.GetDirectoryName(root)!;
        string source = Path.Combine(root, "data", "mob_parsed.csv");
        string folder = Path.Combine(root, "data", "npc_spawns");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        byte[] canonicalSource = System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(source).Replace("\r\n", "\n"));
        Assert.Equal("UTF-8, LF line endings", manifest.RootElement.GetProperty("source_hash_format").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(canonicalSource)).ToLowerInvariant(),
            manifest.RootElement.GetProperty("source_sha256").GetString());
        var rows = new List<string>();
        foreach (var scope in Enum.GetValues<NpcSpawnScope.Scope>())
        {
            string[] partition = File.ReadAllLines(Path.Combine(folder, scope + ".csv"));
            Assert.StartsWith("District;", partition[0]);
            Assert.Equal(manifest.RootElement.GetProperty("counts").GetProperty(scope.ToString()).GetInt32(), partition.Length - 1);
            rows.AddRange(partition.Skip(1));
        }
        // Sort, retaining duplicate sightings: neither a dropped row nor a repeated row can hide here.
        Assert.Equal(File.ReadAllLines(source).Order(StringComparer.Ordinal), rows.Order(StringComparer.Ordinal));
        Assert.Equal(13141, manifest.RootElement.GetProperty("rows").GetArrayLength());
    }

    [Theory]
    [InlineData("Corrupted")]
    [InlineData("Special Agent")]
    public void EncounterFamiliesNeverBecomePermanentStreetSpawns(string name)
    {
        foreach (bool indoors in new[] { true, false })
            Assert.Equal(NpcSpawnScope.Scope.Encounter, NpcSpawnScope.Classify(1, name, indoors));
        Assert.False(NpcSpawnScope.LoadAmbient(NpcSpawnScope.Scope.Encounter));
    }

    [Fact]
    public void PublicIndoorHostsAreRetainedButUnassignedInteriorSightingsAreNotAmbient()
    {
        Assert.Equal(NpcSpawnScope.Scope.PublicInterior, NpcSpawnScope.Classify(1, "Club Guard", true));
        Assert.Equal(NpcSpawnScope.Scope.PublicInterior, NpcSpawnScope.Classify(2, "Override Function", true));
        Assert.Equal(NpcSpawnScope.Scope.PublicInterior, NpcSpawnScope.Classify(2, "Override Function", true, true));
        Assert.Equal(NpcSpawnScope.Scope.MissionInterior, NpcSpawnScope.Classify(1, "Building Security", true));
        Assert.Equal(NpcSpawnScope.Scope.OpenWorld, NpcSpawnScope.Classify(1, "Building Security", false));
        Assert.False(NpcSpawnScope.LoadAmbient(NpcSpawnScope.Scope.MissionInterior));
    }

    [Fact]
    public void ConstructPopulationsKeepTheirDistrictAndPrivateInteriorSamplesKeepTheirScope()
    {
        Assert.Equal(NpcSpawnScope.Scope.Instance, NpcSpawnScope.Classify(8, "Zero One Soldier", null));
        Assert.True(NpcSpawnScope.LoadAmbient(NpcSpawnScope.Scope.Instance));
        Assert.Equal(NpcSpawnScope.Scope.InstanceInterior, NpcSpawnScope.Classify(1, "Watcher Snoop", true, true));
        Assert.False(NpcSpawnScope.LoadAmbient(NpcSpawnScope.Scope.InstanceInterior));
        Assert.Equal(NpcSpawnScope.Scope.Unresolved, NpcSpawnScope.Classify(1, "Randy", null));
        Assert.True(NpcSpawnScope.LoadAmbient(NpcSpawnScope.Scope.Unresolved));
    }

    [Fact]
    public void SameNameOnAnotherFloorIsNotARepeat()
    {
        var seen = new MobSpawnFilter.Seen();
        Assert.False(seen.IsRepeat(1, "Club Guard", 100, 200, 95));
        Assert.True(seen.IsRepeat(1, "Club Guard", 102, 202, 100));
        Assert.False(seen.IsRepeat(1, "Club Guard", 100, 200, 495));
    }
}
