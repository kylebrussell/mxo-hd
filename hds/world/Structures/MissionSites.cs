using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// A place a mission phase can be set: one floor of a building, as the world's own markup lays it out.
    ///
    /// Retail's static world data carries its mission markup: style pointers (SlumAptStylePointer, OfficeStylePointer,
    /// RedPillExtractionStylePointer, ...) mark what kind of area a floor is, "Non Player Character Spawner" objects mark where
    /// NPCs stand, and Computer/Safe/Mainframe spawners where mission objects go. The captured missions used them: Welcome to
    /// the Matrix's floor is a GenericApt cluster and The Internuncio's first is a RedPillExtraction one.
    /// </summary>
    public class MissionSite
    {
        public string style;
        public ushort metrId;
        public string neighbourhood = ""; // the client's own neighbourhood of its elevator's sector
        public double floorY;
        public double minX, maxX, minZ, maxZ;
        public readonly List<double[]> npcSpots = new List<double[]>();                   // where NPCs stand: x, y, z, rotation
        public readonly List<StaticWorldObject> containers = new List<StaticWorldObject>();  // furniture the player can search
        public readonly List<StaticWorldObject> objectSpots = new List<StaticWorldObject>(); // spawners for computers, safes, mainframes
        public readonly List<StaticWorldObject> realObjects = new List<StaticWorldObject>(); // computers, safes and mainframes already there
        public StaticWorldObject elevatorDoor; // this floor's elevator door: the mission waypoint
        public StaticWorldObject lobbyDoor;   // the same elevator's street-level door
        public List<StaticWorldObject> shaft = new List<StaticWorldObject>(); // that elevator's doors, bottom floor first

        public double CentreX => (minX + maxX) / 2;
        public double CentreZ => (minZ + maxZ) / 2;
    }

    public static class MissionSites
    {
        private static readonly Dictionary<UInt16, string> Styles = new Dictionary<UInt16, string>
        {
            { 2546, "SlumApt" }, { 2539, "GenericApt" }, { 2545, "Office" }, { 6627, "RedPillExtraction" },
            { 6626, "ZionOutpost" }, { 2540, "Derelict" }, { 6615, "Warehouse" }, { 2542, "Bank" }, { 2541, "Hotel" },
            { 2544, "Skyscraper" }, { 2548, "Default" }
        };

        /// <summary>The guide's "Normal" areas: any ordinary building.</summary>
        private static readonly string[] Normal = { "SlumApt", "GenericApt", "Office", "Derelict", "Warehouse" };

        public const UInt16 NpcSpawner = 19;
        public const UInt16 ElevatorDoor1 = 417, ElevatorDoor2 = 419;

        /// <summary>Furniture searched in place: filing cabinets, desks, dressers.</summary>
        public static readonly HashSet<UInt16> Furniture = new HashSet<UInt16>
        {
            326, 1431, 1432, 1433, 1434, 1435, 1438, 1439, 6987, 1444, 1445, 1446, 1447, 1448, 1449, 1450, 1451, 1443, 573,
            37913, 38484
        };

        /// <summary>Where retail spawned mission objects, and what goes there.</summary>
        public static readonly Dictionary<UInt16, string> ObjectSpawners = new Dictionary<UInt16, string>
        {
            { 1407, "Computer" }, { 1415, "Computer" }, { 1410, "Safe" }, { 1408, "Mainframe" }, { 1412, "Box" }
        };

        public const UInt16 PhoneGoId = 1401;

        /// <summary>Computers, safes and mainframes that stand in the world as statics, by kind.</summary>
        public static readonly Dictionary<UInt16, string> RealObjects = new Dictionary<UInt16, string>
        {
            { 3, "Computer" }, { 1416, "Safe" }, { 552, "Safe" }, { 357, "Mainframe" }
        };

        // Building floors are 400 units apart; a floor's style pointers sit 300 to 420 units above it.
        private const double LinkDistance = 900, LinkHeight = 150, Margin = 400, FloorBelowPointer = 430;

        private static readonly object buildLock = new object();
        private static readonly Dictionary<UInt16, List<MissionSite>> byDistrict = new Dictionary<UInt16, List<MissionSite>>();

        public static bool IsNormal(string style) => Normal.Contains(style);

        /// <summary>
        /// data/slums_neighbourhoods.csv: each Slums sector's neighbourhood, from the client's own world file
        /// (resource/worlds/final_world/slums_barrens_full.metr, one record per sector with its "Neighborhood"). The sector
        /// numbers are the static data's sector_id and retail's briefing header's.
        /// </summary>
        public static Dictionary<UInt16, string> LoadNeighbourhoods(string path)
        {
            Dictionary<UInt16, string> map = new Dictionary<UInt16, string>();
            if (!System.IO.File.Exists(path))
            {
                Output.WriteLine("[MISSION] No neighbourhood map at " + path);
                return map;
            }

            foreach (string line in System.IO.File.ReadLines(path).Skip(1))
            {
                string[] parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && UInt16.TryParse(parts[0], out UInt16 sector))
                {
                    map[sector] = parts[1].Trim();
                }
            }

            return map;
        }

        /// <summary>Whether a site's style answers a mission phase's site attribute ("Normal", "Any", or a style name).</summary>
        public static bool Matches(string wanted, string style)
        {
            if (string.IsNullOrEmpty(wanted) || wanted.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (wanted.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            {
                return IsNormal(style);
            }

            return wanted.Equals(style, StringComparison.OrdinalIgnoreCase);
        }

        public static List<MissionSite> For(UInt16 metrId)
        {
            lock (buildLock)
            {
                if (!byDistrict.TryGetValue(metrId, out List<MissionSite> sites))
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    sites = Build(DataLoader.getInstance().WorldObjectsDB.Where(o => o.metrId == metrId), metrId);
                    Dictionary<UInt16, string> hoods = metrId == 1 ? LoadNeighbourhoods(DataLoader.DataPath("slums_neighbourhoods.csv")) : null;
                    foreach (MissionSite site in sites)
                    {
                        if (hoods != null && site.elevatorDoor != null && hoods.TryGetValue(site.elevatorDoor.sectorID, out string hood))
                        {
                            site.neighbourhood = hood;
                        }
                    }
                    byDistrict[metrId] = sites;
                    Output.WriteLine("[MISSION] " + sites.Count + " mission sites in district " + metrId + " (" +
                                     timer.ElapsedMilliseconds + " ms)");
                }

                return sites;
            }
        }

        /// <summary>
        /// Picks a site for a phase: of the wanted style, with room for its NPCs and objects and an elevator to reach it,
        /// between <paramref name="minDistance"/> and <paramref name="maxDistance"/> from (x, z) when possible, nearer ones first.
        /// </summary>
        /// <summary>
        /// Retail's distance to a phase's site. Every captured "Normal" phase (6 of 6) was 153 to 165 m from where the player
        /// stood when it was given, though hundreds of fitting buildings were nearer (mxo-research/locations/notes.txt).
        /// </summary>
        public const double TargetDistance = 15800, DistanceBandLow = 14500, DistanceBandHigh = 17000;

        public static MissionSite Choose(IEnumerable<MissionSite> sites, string wanted, double x, double z, int npcs, int objects,
            Random rng, ICollection<MissionSite> exclude = null, ICollection<string> neighbourhoods = null)
        {
            List<(MissionSite site, double distance)> fitting = new List<(MissionSite, double)>();
            foreach (MissionSite site in sites)
            {
                if (site.elevatorDoor == null || site.npcSpots.Count < Math.Min(npcs, 3) || !Matches(wanted, site.style) ||
                    (exclude != null && exclude.Contains(site)) ||
                    site.containers.Count + site.objectSpots.Count < objects)
                {
                    continue;
                }

                double dx = site.CentreX - x, dz = site.CentreZ - z;
                fitting.Add((site, Math.Sqrt(dx * dx + dz * dz)));
            }

            if (fitting.Count == 0)
            {
                return null;
            }

            // At retail's distance, in the guide's neighbourhoods for the mission when it names some (8 of the 10 captured
            // sites were in the guide's "more likely" list); failing that the nearest to retail's distance
            bool InBand((MissionSite site, double distance) f) => f.distance >= DistanceBandLow && f.distance <= DistanceBandHigh;
            bool InHoods((MissionSite site, double distance) f) =>
                neighbourhoods == null || neighbourhoods.Count == 0 || neighbourhoods.Contains(f.site.neighbourhood);
            List<(MissionSite site, double distance)> pool = fitting.Where(f => InBand(f) && InHoods(f)).ToList();
            if (pool.Count == 0)
            {
                pool = fitting.Where(InBand).ToList();
            }

            if (pool.Count == 0)
            {
                double best = fitting.Min(f => Math.Abs(f.distance - TargetDistance));
                pool = fitting.Where(f => Math.Abs(f.distance - TargetDistance) <= best + 2000).ToList();
            }

            return pool[rng.Next(pool.Count)].site;
        }

        /// <summary>Clusters a district's style pointers into floors and collects what's on each. Public for the tests.</summary>
        public static List<MissionSite> Build(IEnumerable<StaticWorldObject> objects, UInt16 metrId)
        {
            List<StaticWorldObject> pointers = new List<StaticWorldObject>();
            List<StaticWorldObject> contents = new List<StaticWorldObject>();
            List<StaticWorldObject> elevators = new List<StaticWorldObject>();
            HashSet<(UInt16, double, double, double)> seen = new HashSet<(UInt16, double, double, double)>();

            foreach (StaticWorldObject obj in objects)
            {
                if (obj.type == null)
                {
                    continue;
                }

                UInt16 goid = NumericalUtils.ByteArrayToUint16(obj.type, 1);
                bool pointer = Styles.ContainsKey(goid);
                bool elevator = goid == ElevatorDoor1 || goid == ElevatorDoor2;
                bool content = goid == NpcSpawner || Furniture.Contains(goid) || ObjectSpawners.ContainsKey(goid) || goid == PhoneGoId ||
                               RealObjects.ContainsKey(goid);
                if (!pointer && !elevator && !content)
                {
                    continue;
                }

                // The district files repeat some rows
                if (!seen.Add((goid, obj.pos_x, obj.pos_y, obj.pos_z)))
                {
                    continue;
                }

                if (pointer)
                {
                    pointers.Add(obj);
                }
                else if (elevator)
                {
                    elevators.Add(obj);
                }
                else
                {
                    contents.Add(obj);
                }
            }

            List<List<StaticWorldObject>> clusters = Cluster(pointers);
            Grid contentGrid = new Grid(contents);
            Grid elevatorGrid = new Grid(elevators);
            Dictionary<(long, long), List<StaticWorldObject>> shafts = Shafts(elevators);

            List<MissionSite> sites = new List<MissionSite>();
            foreach (List<StaticWorldObject> cluster in clusters)
            {
                MissionSite site = new MissionSite
                {
                    style = Styles[NumericalUtils.ByteArrayToUint16(cluster[0].type, 1)],
                    metrId = metrId,
                    minX = cluster.Min(p => p.pos_x) - Margin,
                    maxX = cluster.Max(p => p.pos_x) + Margin,
                    minZ = cluster.Min(p => p.pos_z) - Margin,
                    maxZ = cluster.Max(p => p.pos_z) + Margin
                };
                double top = cluster.Min(p => p.pos_y);

                List<StaticWorldObject> onFloor = contentGrid.Within(site.minX, site.maxX, site.minZ, site.maxZ)
                    .Where(o => o.pos_y >= top - FloorBelowPointer && o.pos_y <= top + 20).ToList();
                List<StaticWorldObject> spawners = onFloor.Where(o => Goid(o) == NpcSpawner).ToList();
                if (spawners.Count == 0)
                {
                    continue;
                }

                List<double> heights = spawners.Select(s => s.pos_y).OrderBy(y => y).ToList();
                site.floorY = heights[heights.Count / 2];

                foreach (StaticWorldObject spawner in spawners)
                {
                    // Retail's NPCs stood 50 units below their spawner's marker (Welcome: spawners at 2542 to 2545, NPCs at 2495)
                    site.npcSpots.Add(new[] { spawner.pos_x, spawner.pos_y - 50, spawner.pos_z, spawner.rot });
                }

                foreach (StaticWorldObject obj in onFloor)
                {
                    UInt16 goid = Goid(obj);
                    if (Math.Abs(obj.pos_y - site.floorY) > 300)
                    {
                        continue;
                    }

                    if (Furniture.Contains(goid) || goid == PhoneGoId)
                    {
                        site.containers.Add(obj);
                    }
                    else if (RealObjects.ContainsKey(goid))
                    {
                        site.realObjects.Add(obj);
                    }
                    else if (ObjectSpawners.ContainsKey(goid))
                    {
                        site.objectSpots.Add(obj);
                    }
                }

                // The floor's elevator: within reach of the floor, at its height
                // The floor's elevator, within reach of the floor and at its height. Of a building's cars, the one that also
                // stops lowest: some start above the street (a GenericApt shaft at 18500, -7000 has no door below 545).
                StaticWorldObject door = elevatorGrid.Within(site.minX - 600, site.maxX + 600, site.minZ - 600, site.maxZ + 600)
                    .Where(e => Math.Abs(e.pos_y - site.floorY) <= 60)
                    .OrderBy(e => shafts[ShaftKey(e)][0].pos_y)
                    .ThenByDescending(e => site.npcSpots.Count(s => FlatDistance(s[0], s[2], e) <= ReachUnits))
                    .ThenBy(e => Math.Abs(e.pos_x - site.CentreX) + Math.Abs(e.pos_z - site.CentreZ))
                    .FirstOrDefault();
                if (door != null)
                {
                    site.elevatorDoor = door;
                    site.shaft = shafts[ShaftKey(door)];
                    site.lobbyDoor = site.shaft[0];

                    // Pointers of neighbouring buildings can join one cluster, and the elevator only reaches its own
                    // building: keep what's as near it as retail's mission NPCs and objects were to their door
                    // (Welcome to the Matrix 3 to 15 m, The Internuncio 12 to 20 m).
                    site.npcSpots.RemoveAll(s => FlatDistance(s[0], s[2], door) > ReachUnits);
                    site.containers.RemoveAll(c => FlatDistance(c.pos_x, c.pos_z, door) > ReachUnits);
                    site.objectSpots.RemoveAll(c => FlatDistance(c.pos_x, c.pos_z, door) > ReachUnits);
                    site.realObjects.RemoveAll(c => FlatDistance(c.pos_x, c.pos_z, door) > ReachUnits);
                    if (site.npcSpots.Count == 0)
                    {
                        site.elevatorDoor = null;
                    }
                }

                sites.Add(site);
            }

            return sites;
        }

        /// <summary>The static id as the data files and retail's 80 9e write it (its bytes in order).</summary>
        public static string DoorHex(StaticWorldObject door)
        {
            return BitConverter.ToString(BitConverter.GetBytes(door.mxoStaticId)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Whether a door or panel belongs to this site's elevator. The data files give some ids to two doors (2800100d is
        /// both 28800, 545, 14700 and 27800, 545, 13700), and the server's lookup keeps the first, so a used door counts if
        /// its id is one of the shaft's as well as if it stands there.
        /// </summary>
        public static bool InShaft(MissionSite site, StaticWorldObject used)
        {
            if (site?.elevatorDoor == null || used == null)
            {
                return false;
            }

            foreach (StaticWorldObject door in site.shaft)
            {
                if (door.mxoStaticId == used.mxoStaticId)
                {
                    return true;
                }
            }

            return InShaft(site, used.pos_x, used.pos_z);
        }

        /// <summary>Whether a door or panel at (x, z) belongs to this site's elevator.</summary>
        public static bool InShaft(MissionSite site, double x, double z)
        {
            if (site?.elevatorDoor == null)
            {
                return false;
            }

            // Panels are inside the car, a few metres from its doors; a building's two cars stand side by side.
            double dx = x - site.elevatorDoor.pos_x, dz = z - site.elevatorDoor.pos_z;
            return dx * dx + dz * dz <= 900.0 * 900.0;
        }

        public static UInt16 Goid(StaticWorldObject obj) => NumericalUtils.ByteArrayToUint16(obj.type, 1);

        /// <summary>How far from its elevator door a site's NPCs and objects can be.</summary>
        public const double ReachUnits = 2200;

        private static double FlatDistance(double x, double z, StaticWorldObject door)
        {
            double dx = x - door.pos_x, dz = z - door.pos_z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static (long, long) ShaftKey(StaticWorldObject door) => ((long)Math.Round(door.pos_x / 10), (long)Math.Round(door.pos_z / 10));

        private static Dictionary<(long, long), List<StaticWorldObject>> Shafts(IEnumerable<StaticWorldObject> elevators)
        {
            Dictionary<(long, long), List<StaticWorldObject>> shafts = new Dictionary<(long, long), List<StaticWorldObject>>();
            foreach (StaticWorldObject door in elevators)
            {
                (long, long) key = ShaftKey(door);
                if (!shafts.TryGetValue(key, out List<StaticWorldObject> list))
                {
                    shafts[key] = list = new List<StaticWorldObject>();
                }

                list.Add(door);
            }

            foreach (List<StaticWorldObject> list in shafts.Values)
            {
                list.Sort((a, b) => a.pos_y.CompareTo(b.pos_y));
            }

            return shafts;
        }

        /// <summary>Union-find over pointers of one style on one floor that are close together.</summary>
        private static List<List<StaticWorldObject>> Cluster(List<StaticWorldObject> pointers)
        {
            int[] parent = Enumerable.Range(0, pointers.Count).ToArray();
            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            Dictionary<(long, long), List<int>> cells = new Dictionary<(long, long), List<int>>();
            for (int i = 0; i < pointers.Count; i++)
            {
                (long, long) cell = ((long)Math.Floor(pointers[i].pos_x / LinkDistance), (long)Math.Floor(pointers[i].pos_z / LinkDistance));
                if (!cells.TryGetValue(cell, out List<int> list))
                {
                    cells[cell] = list = new List<int>();
                }

                list.Add(i);
            }

            for (int i = 0; i < pointers.Count; i++)
            {
                StaticWorldObject a = pointers[i];
                long cx = (long)Math.Floor(a.pos_x / LinkDistance), cz = (long)Math.Floor(a.pos_z / LinkDistance);
                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dz = -1; dz <= 1; dz++)
                    {
                        if (!cells.TryGetValue((cx + dx, cz + dz), out List<int> list))
                        {
                            continue;
                        }

                        foreach (int j in list)
                        {
                            if (j <= i)
                            {
                                continue;
                            }

                            StaticWorldObject b = pointers[j];
                            double ddx = a.pos_x - b.pos_x, ddz = a.pos_z - b.pos_z;
                            if (Goid(a) == Goid(b) && Math.Abs(a.pos_y - b.pos_y) < LinkHeight &&
                                ddx * ddx + ddz * ddz < LinkDistance * LinkDistance)
                            {
                                parent[Find(i)] = Find(j);
                            }
                        }
                    }
                }
            }

            Dictionary<int, List<StaticWorldObject>> groups = new Dictionary<int, List<StaticWorldObject>>();
            for (int i = 0; i < pointers.Count; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out List<StaticWorldObject> list))
                {
                    groups[root] = list = new List<StaticWorldObject>();
                }

                list.Add(pointers[i]);
            }

            return groups.Values.ToList();
        }

        private class Grid
        {
            private const double Cell = 1000;
            private readonly Dictionary<(long, long), List<StaticWorldObject>> cells = new Dictionary<(long, long), List<StaticWorldObject>>();

            public Grid(IEnumerable<StaticWorldObject> objects)
            {
                foreach (StaticWorldObject obj in objects)
                {
                    (long, long) key = ((long)Math.Floor(obj.pos_x / Cell), (long)Math.Floor(obj.pos_z / Cell));
                    if (!cells.TryGetValue(key, out List<StaticWorldObject> list))
                    {
                        cells[key] = list = new List<StaticWorldObject>();
                    }

                    list.Add(obj);
                }
            }

            public IEnumerable<StaticWorldObject> Within(double minX, double maxX, double minZ, double maxZ)
            {
                for (long gx = (long)Math.Floor(minX / Cell); gx <= (long)Math.Floor(maxX / Cell); gx++)
                {
                    for (long gz = (long)Math.Floor(minZ / Cell); gz <= (long)Math.Floor(maxZ / Cell); gz++)
                    {
                        if (!cells.TryGetValue((gx, gz), out List<StaticWorldObject> list))
                        {
                            continue;
                        }

                        foreach (StaticWorldObject obj in list)
                        {
                            if (obj.pos_x >= minX && obj.pos_x <= maxX && obj.pos_z >= minZ && obj.pos_z <= maxZ)
                            {
                                yield return obj;
                            }
                        }
                    }
                }
            }
        }
    }
}
