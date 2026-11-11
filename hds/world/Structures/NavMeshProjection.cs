using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    public struct NavPoint
    {
        public double x, y, z;
        public NavLocation location;
        public double[] Position => new[] { x, y, z };
    }

    public static partial class NavMesh
    {
        // Keep the centre off a wall edge. Narrow polygons use a smaller inset rather than disappearing.
        internal const double SpawnInset = 20;
        private const double ConnectionSlack = 2;
        private const double StepHeight = 30;

        internal sealed class Surface
        {
            public readonly double[] vertices, inset;
            public readonly NavLocation location;
            public readonly double minX, minZ, maxX, maxZ, centreX, centreZ, minY, maxY;
            private readonly double floorX, floorZ, floorC;

            internal Surface(double[] vertices, double floorX, double floorZ, double floorC, NavLocation location)
            {
                this.vertices = vertices;
                this.floorX = floorX;
                this.floorZ = floorZ;
                this.floorC = floorC;
                this.location = location;
                minX = minZ = minY = double.MaxValue;
                maxX = maxZ = maxY = double.MinValue;
                for (int i = 0; i < vertices.Length; i += 2)
                {
                    double x = vertices[i], z = vertices[i + 1], y = Floor(x, z);
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    centreX += x; centreZ += z;
                }
                centreX /= vertices.Length / 2;
                centreZ /= vertices.Length / 2;
                inset = Clip(vertices, vertices, SpawnInset);
                if (inset.Length < 6) inset = Clip(vertices, vertices, 1);
                if (inset.Length < 6) inset = vertices;
            }

            public double Floor(double x, double z) => floorX * x + floorZ * z + floorC;

            public bool Contains(double x, double z) => Interval(vertices, x, z, x, z, out _, out _);

            public double[] Closest(double x, double z)
            {
                if (Interval(inset, x, z, x, z, out _, out _)) return new[] { x, z };
                double best = double.MaxValue, bx = centreX, bz = centreZ;
                for (int i = 0; i < inset.Length; i += 2)
                {
                    int j = (i + 2) % inset.Length;
                    double dx = inset[j] - inset[i], dz = inset[j + 1] - inset[i + 1];
                    double length = dx * dx + dz * dz;
                    double t = length == 0 ? 0 : Math.Clamp(((x - inset[i]) * dx + (z - inset[i + 1]) * dz) / length, 0, 1);
                    double px = inset[i] + t * dx, pz = inset[i + 1] + t * dz;
                    double distance = Square(px - x) + Square(pz - z);
                    if (distance < best) { best = distance; bx = px; bz = pz; }
                }
                return new[] { bx, bz };
            }

            public bool Connects(Surface other)
            {
                if (maxX + ConnectionSlack < other.minX || other.maxX + ConnectionSlack < minX ||
                    maxZ + ConnectionSlack < other.minZ || other.maxZ + ConnectionSlack < minZ) return false;
                double x = (Math.Max(minX, other.minX) + Math.Min(maxX, other.maxX)) / 2;
                double z = (Math.Max(minZ, other.minZ) + Math.Min(maxZ, other.maxZ)) / 2;
                if (Math.Abs(Floor(x, z) - other.Floor(x, z)) > StepHeight) return false;
                double[] overlap = Clip(vertices, other.vertices, 0);
                if (Math.Abs(Area(overlap)) > 0.1) return true;
                // Partial shared edges join room meshes and T junctions; touching at a corner does not.
                for (int i = 0; i < vertices.Length; i += 2)
                {
                    int ni = (i + 2) % vertices.Length;
                    double dx = vertices[ni] - vertices[i], dz = vertices[ni + 1] - vertices[i + 1];
                    double length = Math.Sqrt(dx * dx + dz * dz);
                    if (length < 0.001) continue;
                    for (int j = 0; j < other.vertices.Length; j += 2)
                    {
                        int nj = (j + 2) % other.vertices.Length;
                        double ax = other.vertices[j] - vertices[i], az = other.vertices[j + 1] - vertices[i + 1];
                        double bx = other.vertices[nj] - vertices[i], bz = other.vertices[nj + 1] - vertices[i + 1];
                        if (Math.Abs(dx * az - dz * ax) > ConnectionSlack * length ||
                            Math.Abs(dx * bz - dz * bx) > ConnectionSlack * length) continue;
                        double a = (ax * dx + az * dz) / length, b = (bx * dx + bz * dz) / length;
                        if (Math.Min(length, Math.Max(a, b)) - Math.Max(0, Math.Min(a, b)) > ConnectionSlack) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Nearest walkable point within maxDistance, on the requested floor. When the player's polygon is known on
        /// this floor, only its connected component is used. Player-relative arrivals can also require a straight
        /// walkable route, preventing a three-metre offset from putting them in the next room through a wall.
        /// </summary>
        public static bool Project(ushort district, double x, double y, double z, out NavPoint point,
            double maxDistance = 800, double[] reachableFrom = null, bool requireDirect = false)
        {
            point = default;
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) ||
                !double.IsFinite(maxDistance) || maxDistance < 0) return false;
            District data = Load(district);
            HashSet<ushort> sectors = new HashSet<ushort>();
            for (int cx = (int)Math.Floor((x - maxDistance) / 1000); cx <= (int)Math.Floor((x + maxDistance) / 1000); cx++)
                for (int cz = (int)Math.Floor((z - maxDistance) / 1000); cz <= (int)Math.Floor((z + maxDistance) / 1000); cz++)
                {
                    SectorInfo info = SectorProperties.At(district, cx * 1000.0 + 500, cz * 1000.0 + 500);
                    if (info != null) sectors.Add(info.sector);
                }
            SectorInfo target = SectorProperties.At(district, x, z);
            if (target != null) sectors.Add(target.sector);
            if (reachableFrom != null && reachableFrom.Length >= 3)
            {
                SectorInfo from = SectorProperties.At(district, reachableFrom[0], reachableFrom[2]);
                // Across distant sectors we lack the intervening path. Do not claim those are disconnected.
                if (from != null && (from.sector == target?.sector ||
                    Square(reachableFrom[0] - x) + Square(reachableFrom[2] - z) <= Square(maxDistance * 2))) sectors.Add(from.sector);
                else reachableFrom = null;
            }
            List<Surface> surfaces = new List<Surface>();
            foreach (ushort id in sectors)
            {
                SectorInfo info = SectorProperties.Get(district, id);
                if (info != null && data.sectors.TryGetValue(id, out Sector sector)) surfaces.AddRange(Surfaces(info, sector, data));
            }
            if (!ProjectSurfaces(surfaces, x, y, z, maxDistance, reachableFrom, requireDirect, out point)) return false;
            // Use the same final polygon choice as ordinary player positioning, including shared-edge tie breaking.
            if (!Locate(district, point.x, point.y, point.z, out NavLocation located)) return false;
            point.location = located;
            point.y = located.floorY + 95;
            return true;
        }

        private static List<Surface> Surfaces(SectorInfo info, Sector sector, District district)
        {
            lock (loadLock)
            {
                if (sector.projectionSurfaces != null) return sector.projectionSurfaces;
                var result = new List<Surface>();
                void Add(Polygon p, double ox, double oy, double oz, int rotation, NavLocation at)
                {
                    if (p.xz.Length < 6 || Math.Abs(p.b) < 1e-10 || Math.Abs(p.area2) < 0.001) return;
                    double[] vertices = new double[p.xz.Length];
                    for (int i = 0; i < vertices.Length; i += 2)
                    {
                        double u = p.xz[i], v = p.xz[i + 1];
                        vertices[i] = ox + (rotation == 1 ? v : rotation == 2 ? -u : rotation == 3 ? -v : u);
                        vertices[i + 1] = oz + (rotation == 1 ? -u : rotation == 2 ? -v : rotation == 3 ? u : v);
                    }
                    double fx = (rotation == 1 ? -p.c : rotation == 2 ? p.a : rotation == 3 ? p.c : -p.a) / p.b;
                    double fz = (rotation == 1 ? p.a : rotation == 2 ? p.c : rotation == 3 ? -p.a : -p.c) / p.b;
                    result.Add(new Surface(vertices, fx, fz, oy - p.d / p.b - fx * ox - fz * oz, at));
                }
                for (int g = 0; g < sector.regions.Length; g++)
                    for (int p = 0; p < sector.regions[g].Count; p++)
                        Add(sector.regions[g][p], info.minX, 0, info.minZ, 0,
                            new NavLocation { sector = info.sector, region = (ushort)g, polygon = (ushort)p });
                for (int k = 0; k < sector.placements.Length; k++)
                {
                    Placement pl = sector.placements[k];
                    for (int p = 0; p < district.prefabs[pl.prefab].Count; p++)
                        Add(district.prefabs[pl.prefab][p], info.minX + pl.pivotX, pl.pivotY, info.minZ + pl.pivotZ, pl.rotation,
                            new NavLocation { sector = info.sector, region = (ushort)(sector.regions.Length + k), polygon = (ushort)p, indoors = true });
                }
                return sector.projectionSurfaces = result;
            }
        }

        internal static bool ProjectSurfaces(IReadOnlyList<Surface> all, double x, double y, double z, double maxDistance,
            double[] from, bool requireDirect, out NavPoint point)
        {
            point = default;
            double feet = y - 95;
            List<Surface> surfaces = all.Where(s => s.maxY >= feet - MaxFloorDistance && s.minY <= feet + MaxFloorDistance).ToList();
            Surface reference = null;
            double height = MaxFloorDistance;
            if (from != null && from.Length >= 3 && from.All(double.IsFinite))
                foreach (Surface s in surfaces)
                    if (s.Contains(from[0], from[2]) && Math.Abs(s.Floor(from[0], from[2]) - (from[1] - 95)) <= height)
                    {
                        height = Math.Abs(s.Floor(from[0], from[2]) - (from[1] - 95));
                        reference = s;
                    }
            Surface root = reference;
            if (root == null)
            {
                // With no player polygon on this floor, choose the nearest height first. A closer horizontal polygon
                // belonging to a mezzanine must not pull an arrival off the requested floor.
                double floorError = double.MaxValue, nearest = double.MaxValue;
                foreach (Surface s in surfaces)
                {
                    double[] p = s.Closest(x, z);
                    double distance = Square(p[0] - x) + Square(p[1] - z), error = Math.Abs(s.Floor(p[0], p[1]) - feet);
                    if (distance <= Square(maxDistance) && (error < floorError - 0.01 ||
                        (error <= floorError + 0.01 && distance < nearest)))
                    { root = s; floorError = error; nearest = distance; }
                }
            }
            if (root != null)
            {
                HashSet<Surface> reached = new HashSet<Surface> { root };
                Queue<Surface> queue = new Queue<Surface>();
                queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    Surface current = queue.Dequeue();
                    foreach (Surface other in surfaces)
                        if (!reached.Contains(other) && current.Connects(other)) { reached.Add(other); queue.Enqueue(other); }
                }
                surfaces = surfaces.Where(reached.Contains).ToList();
            }
            double best = maxDistance * maxDistance;
            double bestHeight = double.MaxValue;
            bool found = false;
            foreach (Surface s in surfaces)
            {
                IEnumerable<double[]> candidates = new[] { s.Closest(x, z) };
                if (reference != null && requireDirect)
                    candidates = candidates.Concat(new[] { new[] { s.centreX, s.centreZ }, s.Closest(from[0], from[2]) })
                        .Concat(Enumerable.Range(0, s.inset.Length / 2).Select(i => new[] { s.inset[i * 2], s.inset[i * 2 + 1] }));
                foreach (double[] candidate in candidates)
                {
                    double px = candidate[0], pz = candidate[1], floor = s.Floor(px, pz);
                    double distance = Square(px - x) + Square(pz - z);
                    if (distance > best || Math.Abs(floor - feet) > MaxFloorDistance) continue;
                    if (found && Math.Abs(distance - best) < 1e-7 && Math.Abs(floor - feet) > bestHeight) continue;
                    if (reference != null && requireDirect && !Covered(surfaces, from[0], from[2], px, pz)) continue;
                    NavLocation at = s.location;
                    at.floorY = floor;
                    point = new NavPoint { x = px, y = floor + 95, z = pz, location = at };
                    found = true;
                    best = distance;
                    bestHeight = Math.Abs(floor - feet);
                }
            }
            return found;
        }

        private static bool Covered(List<Surface> surfaces, double x, double z, double tx, double tz)
        {
            List<(double start, double end)> intervals = new List<(double, double)>();
            foreach (Surface s in surfaces)
                if (Interval(s.vertices, x, z, tx, tz, out double start, out double end)) intervals.Add((start, end));
            double covered = 0, slack = ConnectionSlack / Math.Max(1, Math.Sqrt(Square(tx - x) + Square(tz - z)));
            foreach (var interval in intervals.OrderBy(i => i.start))
            {
                if (interval.start > covered + slack) return false;
                covered = Math.Max(covered, interval.end);
                if (covered >= 1 - 1e-8) return true;
            }
            return false;
        }

        // Clip a segment to a convex polygon, retaining its parameter interval.
        private static bool Interval(double[] polygon, double x, double z, double tx, double tz, out double start, out double end)
        {
            start = 0; end = 1;
            double winding = Area(polygon) >= 0 ? 1 : -1;
            for (int i = 0; i < polygon.Length; i += 2)
            {
                int j = (i + 2) % polygon.Length;
                double ex = polygon[j] - polygon[i], ez = polygon[j + 1] - polygon[i + 1];
                double a = winding * (ex * (z - polygon[i + 1]) - ez * (x - polygon[i]));
                double b = winding * (ex * (tz - z) - ez * (tx - x));
                if (Math.Abs(b) < 1e-9) { if (a < -1e-7) return false; }
                else if (b > 0) start = Math.Max(start, -a / b);
                else end = Math.Min(end, -a / b);
                if (start > end + 1e-9) return false;
            }
            return true;
        }

        private static double[] Clip(double[] subject, double[] boundary, double inset)
        {
            List<double> points = new List<double>(subject);
            double winding = Area(boundary) >= 0 ? 1 : -1;
            for (int i = 0; i < boundary.Length && points.Count >= 6; i += 2)
            {
                int j = (i + 2) % boundary.Length;
                double ex = boundary[j] - boundary[i], ez = boundary[j + 1] - boundary[i + 1];
                double threshold = inset * Math.Sqrt(ex * ex + ez * ez);
                double Side(double x, double z) => winding * (ex * (z - boundary[i + 1]) - ez * (x - boundary[i])) - threshold;
                List<double> output = new List<double>();
                for (int p = 0; p < points.Count; p += 2)
                {
                    int q = (p + 2) % points.Count;
                    double ax = points[p], az = points[p + 1], bx = points[q], bz = points[q + 1];
                    double a = Side(ax, az), b = Side(bx, bz);
                    if (a >= -1e-7) { output.Add(ax); output.Add(az); }
                    if ((a >= 0) != (b >= 0))
                    {
                        double t = a / (a - b);
                        output.Add(ax + (bx - ax) * t); output.Add(az + (bz - az) * t);
                    }
                }
                points = output;
            }
            return points.Count >= 6 ? points.ToArray() : Array.Empty<double>();
        }

        private static double Area(double[] vertices)
        {
            double area = 0;
            for (int i = 0; i < vertices.Length; i += 2)
            {
                int j = (i + 2) % vertices.Length;
                area += vertices[i] * vertices[j + 1] - vertices[j] * vertices[i + 1];
            }
            return area;
        }

        private static double Square(double value) => value * value;
    }
}
