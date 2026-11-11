using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using hds;

// Read-only against the client/database: the checked-in city navmesh supplies indoor/outdoor context.
string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
string source = Path.Combine(root, "data", "mob_parsed.csv");
string output = Path.Combine(root, "data", "npc_spawns");
Directory.CreateDirectory(output);
SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
HashSet<string> instanceModels = File.ReadLines(Path.Combine(root, "research", "world-data", "npc_instance_models.csv"))
    .Skip(1).Select(line => line.Split(',')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
var groups = Enum.GetValues<NpcSpawnScope.Scope>().ToDictionary(s => s, _ => new List<string>());
List<object> provenance = new();
int lineNumber = 0;
foreach (string line in File.ReadLines(source))
{
    lineNumber++;
    string[] p = line.Split(';');
    if (p.Length < 10 || !ushort.TryParse(p[0], out ushort district)) continue;
    double x = double.Parse(p[7], CultureInfo.InvariantCulture), y = double.Parse(p[8], CultureInfo.InvariantCulture),
        z = double.Parse(p[9], CultureInfo.InvariantCulture);
    NavLocation nav = default;
    bool found = district is >= 1 and <= 3 && NavMesh.Locate(district, x, y, z, out nav);
    var scope = NpcSpawnScope.Classify(district, p[2], found ? nav.indoors : null, instanceModels.Contains(p[6]));
    groups[scope].Add(line);
    provenance.Add(new { line = lineNumber, scope = scope.ToString(), nav = found ? (object)new
        { nav.sector, nav.region, nav.polygon, nav.floorY, nav.indoors } : null });
}
const string header = "District;DistrictName;Name;Level;MaxHealth;Health;Rsi;X;Y;Z;Rotation;Dead;Lootable;Field13;Field14;Field15;Field16;Field17;Field18;Field19;Weapon;StealthAwareness;Field22";
foreach (var (scope, rows) in groups)
{
    string file = scope.ToString() + ".csv";
    File.WriteAllLines(Path.Combine(output, file), new[] { header }.Concat(rows));
    Console.WriteLine($"{scope}: {rows.Count}, ambient={NpcSpawnScope.LoadAmbient(scope)}");
}
var manifest = new { source = "data/mob_parsed.csv", source_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),
    policy = "NpcSpawnScope.Classify; unknown city nav is retained; historical mission/encounter samples require explicit runtime ownership.",
    counts = groups.ToDictionary(g => g.Key.ToString(), g => g.Value.Count), rows = provenance };
File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n");
