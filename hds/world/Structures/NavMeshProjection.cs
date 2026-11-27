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
            public readonly double winding, insetWinding;
            internal Surface[] sameSectorNeighbors;
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
                winding = Area(vertices) >= 0 ? 1 : -1;
                inset = Clip(vertices, vertices, SpawnInset);
                if (inset.Length < 6) inset = Clip(vertices, vertices, 1);
                if (inset.Length < 6) inset = vertices;
                insetWinding = ReferenceEquals(inset, vertices) ? winding : (Area(inset) >= 0 ? 1 : -1);
            }

            public double Floor(double x, double z) => floorX * x + floorZ * z + floorC;

            public bool Contains(double x, double z) =>
                x >= minX - 1e-7 && x <= maxX + 1e-7 && z >= minZ - 1e-7 && z <= maxZ + 1e-7 &&
                Interval(vertices, winding, x, z, x, z, out _, out _);

            public void Closest(double x, double z, out double cx, out double cz)
            {
                if (x >= minX - 1e-7 && x <= maxX + 1e-7 && z >= minZ - 1e-7 && z <= maxZ + 1e-7 &&
                    Interval(inset, insetWinding, x, z, x, z, out _, out _))
                {
                    cx = x; cz = z;
                    return;
                }
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
                cx = bx; cz = bz;
            }

            public double[] Closest(double x, double z)
            {
                Closest(x, z, out double cx, out double cz);
                return new[] { cx, cz };
            }

            public bool Connects(Surface other) => TryPortal(other, out _, out _);

            public bool TryPortal(Surface other, out double portalX, out double portalZ)
            {
                portalX = portalZ = 0;
                if (maxX + ConnectionSlack < other.minX || other.maxX + ConnectionSlack < minX ||
                    maxZ + ConnectionSlack < other.minZ || other.maxZ + ConnectionSlack < minZ ||
                    maxY + StepHeight < other.minY || other.maxY + StepHeight < minY) return false;
                double x = (Math.Max(minX, other.minX) + Math.Min(maxX, other.maxX)) / 2;
                double z = (Math.Max(minZ, other.minZ) + Math.Min(maxZ, other.maxZ)) / 2;
                if (Math.Abs(Floor(x, z) - other.Floor(x, z)) > StepHeight) return false;
                double[] overlap = Clip(vertices, other.vertices, 0);
                if (Math.Abs(Area(overlap)) > 0.1)
                {
                    for (int i = 0; i < overlap.Length; i += 2)
                    {
                        portalX += overlap[i];
                        portalZ += overlap[i + 1];
                    }
                    portalX /= overlap.Length / 2;
                    portalZ /= overlap.Length / 2;
                    return true;
                }
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
                        double start = Math.Max(0, Math.Min(a, b)), end = Math.Min(length, Math.Max(a, b));
                        if (end - start <= ConnectionSlack) continue;
                        double middle = (start + end) / (2 * length);
                        portalX = vertices[i] + middle * dx;
                        portalZ = vertices[i + 1] + middle * dz;
                        return true;
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
            double maxDistance = 800, double[] reachableFrom = null, bool requireDirect = false, bool outdoorsOnly = false)
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
            IReadOnlyList<Surface> surfaces;
            bool singleSectorCached = false;
            if (sectors.Count == 1)
            {
                ushort onlyId = 0;
                foreach (ushort id in sectors) { onlyId = id; break; }
                SectorInfo info = SectorProperties.Get(district, onlyId);
                if (info != null && data.sectors.TryGetValue(onlyId, out Sector sector))
                {
                    surfaces = outdoorsOnly ? OutdoorSurfaces(info, sector) : Surfaces(info, sector, data);
                    singleSectorCached = true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                List<Surface> combined = new List<Surface>();
                foreach (ushort id in sectors)
                {
                    SectorInfo info = SectorProperties.Get(district, id);
                    if (info != null && data.sectors.TryGetValue(id, out Sector sector))
                        combined.AddRange(outdoorsOnly ? OutdoorSurfaces(info, sector) : Surfaces(info, sector, data));
                }
                surfaces = combined;
            }
            if (!ProjectSurfacesCore(surfaces, x, y, z, maxDistance, reachableFrom, requireDirect, singleSectorCached, out point)) return false;
            // Use the same final polygon choice as ordinary player positioning, including shared-edge tie breaking.
            if (!Locate(district, point.x, point.y, point.z, out NavLocation located)) return false;
            point.location = located;
            point.y = located.floorY + 95;
            return true;
        }

        /// <summary>A safe floor when a construct has no shipped arrival coordinates. This is a server fallback,
        /// not a reconstruction of its retail spawn. Ordinary mission placement still uses same-floor Project.</summary>
        public static bool NearestEntry(ushort district, double x, double y, double z, out NavPoint point)
        {
            point = default;
            District data = Load(district);
            double best = double.MaxValue;
            bool found = false;
            foreach (var pair in data.sectors)
            {
                SectorInfo info = SectorProperties.Get(district, pair.Key);
                if (info == null) continue;
                foreach (Surface s in Surfaces(info, pair.Value, data))
                {
                    double[] p = s.Closest(x, z);
                    double floor = s.Floor(p[0], p[1]);
                    double score = Square(p[0] - x) + Square(p[1] - z) + Square(floor + 95 - y);
                    if (score >= best) continue;
                    if (!Locate(district, p[0], floor + 95, p[1], out NavLocation at)) continue;
                    best = score;
                    found = true;
                    point = new NavPoint { x = p[0], y = at.floorY + 95, z = p[1], location = at };
                }
            }
            return found;
        }

        private const double SurfaceGridCellSize = 500.0;
        private const double SurfaceGridYCellSize = 200.0;

        private static Dictionary<(int cx, int cy, int cz), List<Surface>> BuildSurfaceGrid(IReadOnlyList<Surface> surfaces)
        {
            var grid = new Dictionary<(int cx, int cy, int cz), List<Surface>>();
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface s = surfaces[i];
                int minCx = (int)Math.Floor((s.minX - ConnectionSlack) / SurfaceGridCellSize);
                int maxCx = (int)Math.Floor((s.maxX + ConnectionSlack) / SurfaceGridCellSize);
                int minCy = (int)Math.Floor((s.minY - StepHeight) / SurfaceGridYCellSize);
                int maxCy = (int)Math.Floor((s.maxY + StepHeight) / SurfaceGridYCellSize);
                int minCz = (int)Math.Floor((s.minZ - ConnectionSlack) / SurfaceGridCellSize);
                int maxCz = (int)Math.Floor((s.maxZ + ConnectionSlack) / SurfaceGridCellSize);
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    for (int cy = minCy; cy <= maxCy; cy++)
                    {
                        for (int cz = minCz; cz <= maxCz; cz++)
                        {
                            var key = (cx, cy, cz);
                            if (!grid.TryGetValue(key, out List<Surface> bucket))
                            {
                                bucket = new List<Surface>(4);
                                grid[key] = bucket;
                            }
                            bucket.Add(s);
                        }
                    }
                }
            }
            return grid;
        }

        private static void BuildSameSectorAdjacency(List<Surface> surfaces)
        {
            if (surfaces.Count == 0) return;
            var grid = BuildSurfaceGrid(surfaces);
            var neighborLists = new List<Surface>[surfaces.Count];
            var indexBySurface = new Dictionary<Surface, int>(surfaces.Count, ReferenceEqualityComparer.Instance);
            for (int i = 0; i < surfaces.Count; i++)
            {
                neighborLists[i] = new List<Surface>(4);
                indexBySurface[surfaces[i]] = i;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface s = surfaces[i];
                seen.Clear();
                int minCx = (int)Math.Floor((s.minX - ConnectionSlack) / SurfaceGridCellSize);
                int maxCx = (int)Math.Floor((s.maxX + ConnectionSlack) / SurfaceGridCellSize);
                int minCy = (int)Math.Floor((s.minY - StepHeight) / SurfaceGridYCellSize);
                int maxCy = (int)Math.Floor((s.maxY + StepHeight) / SurfaceGridYCellSize);
                int minCz = (int)Math.Floor((s.minZ - ConnectionSlack) / SurfaceGridCellSize);
                int maxCz = (int)Math.Floor((s.maxZ + ConnectionSlack) / SurfaceGridCellSize);
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    for (int cy = minCy; cy <= maxCy; cy++)
                    {
                        for (int cz = minCz; cz <= maxCz; cz++)
                        {
                            if (!grid.TryGetValue((cx, cy, cz), out List<Surface> bucket)) continue;
                            for (int b = 0; b < bucket.Count; b++)
                            {
                                Surface other = bucket[b];
                                int j = indexBySurface[other];
                                if (j <= i || !seen.Add(j)) continue;
                                if (s.Connects(other))
                                {
                                    neighborLists[i].Add(other);
                                    neighborLists[j].Add(s);
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < surfaces.Count; i++)
            {
                surfaces[i].sameSectorNeighbors = neighborLists[i].Count == 0
                    ? Array.Empty<Surface>()
                    : neighborLists[i].ToArray();
            }
        }

        private static List<Surface> OutdoorSurfaces(SectorInfo info, Sector sector)
        {
            List<Surface> fast = System.Threading.Volatile.Read(ref sector.outdoorProjectionSurfaces);
            if (fast != null) return fast;

            lock (loadLock)
            {
                if (sector.outdoorProjectionSurfaces != null) return sector.outdoorProjectionSurfaces;
                var result = new List<Surface>();
                for (int g = 0; g < sector.regions.Length; g++)
                {
                    List<Polygon> polys = sector.regions[g];
                    for (int p = 0; p < polys.Count; p++)
                    {
                        Polygon poly = polys[p];
                        if (poly.xz.Length < 6 || Math.Abs(poly.b) < 1e-10 || Math.Abs(poly.area2) < 0.001) continue;
                        double[] vertices = new double[poly.xz.Length];
                        for (int i = 0; i < vertices.Length; i += 2)
                        {
                            vertices[i] = info.minX + poly.xz[i];
                            vertices[i + 1] = info.minZ + poly.xz[i + 1];
                        }
                        double fx = -poly.a / poly.b;
                        double fz = -poly.c / poly.b;
                        result.Add(new Surface(vertices, fx, fz, -poly.d / poly.b - fx * info.minX - fz * info.minZ,
                            new NavLocation { sector = info.sector, region = (ushort)g, polygon = (ushort)p }));
                    }
                }
                BuildSameSectorAdjacency(result);
                System.Threading.Volatile.Write(ref sector.outdoorProjectionSurfaces, result);
                return result;
            }
        }

        private static List<Surface> Surfaces(SectorInfo info, Sector sector, District district)
        {
            List<Surface> fast = System.Threading.Volatile.Read(ref sector.projectionSurfaces);
            if (fast != null) return fast;

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
                BuildSameSectorAdjacency(result);
                System.Threading.Volatile.Write(ref sector.projectionSurfaces, result);
                return result;
            }
        }

        internal static bool ProjectSurfaces(IReadOnlyList<Surface> all, double x, double y, double z, double maxDistance,
            double[] from, bool requireDirect, out NavPoint point) =>
            ProjectSurfacesCore(all, x, y, z, maxDistance, from, requireDirect, singleSectorCached: false, out point);

        private static bool ProjectSurfacesCore(IReadOnlyList<Surface> all, double x, double y, double z, double maxDistance,
            double[] from, bool requireDirect, bool singleSectorCached, out NavPoint point)
        {
            point = default;
            double feet = y - 95;
            double minFloor = feet - MaxFloorDistance;
            double maxFloor = feet + MaxFloorDistance;
            double maxDistanceSq = maxDistance * maxDistance;

            var surfaces = new List<Surface>(Math.Min(all.Count, 256));
            for (int i = 0; i < all.Count; i++)
            {
                Surface s = all[i];
                if (s.maxY >= minFloor && s.minY <= maxFloor) surfaces.Add(s);
            }

            Surface reference = null;
            double height = MaxFloorDistance;
            bool hasFrom = from != null && from.Length >= 3 &&
                double.IsFinite(from[0]) && double.IsFinite(from[1]) && double.IsFinite(from[2]);
            if (hasFrom)
            {
                double fx = from[0], fy = from[1] - 95, fz = from[2];
                for (int i = 0; i < surfaces.Count; i++)
                {
                    Surface s = surfaces[i];
                    if (s.Contains(fx, fz))
                    {
                        double err = Math.Abs(s.Floor(fx, fz) - fy);
                        if (err <= height)
                        {
                            height = err;
                            reference = s;
                        }
                    }
                }
            }

            Surface root = reference;
            if (root == null)
            {
                // With no player polygon on this floor, choose the nearest height first. A closer horizontal polygon
                // belonging to a mezzanine must not pull an arrival off the requested floor.
                double floorError = double.MaxValue, nearest = double.MaxValue;
                for (int i = 0; i < surfaces.Count; i++)
                {
                    Surface s = surfaces[i];
                    if (x < s.minX - maxDistance || x > s.maxX + maxDistance ||
                        z < s.minZ - maxDistance || z > s.maxZ + maxDistance) continue;
                    s.Closest(x, z, out double px, out double pz);
                    double distance = Square(px - x) + Square(pz - z), error = Math.Abs(s.Floor(px, pz) - feet);
                    if (distance <= maxDistanceSq && (error < floorError - 0.01 ||
                        (error <= floorError + 0.01 && distance < nearest)))
                    { root = s; floorError = error; nearest = distance; }
                }
            }

            if (root != null)
            {
                var reached = new HashSet<Surface>(ReferenceEqualityComparer.Instance) { root };
                var queue = new Queue<Surface>();
                queue.Enqueue(root);

                if (singleSectorCached && root.sameSectorNeighbors != null)
                {
                    while (queue.Count > 0)
                    {
                        Surface current = queue.Dequeue();
                        Surface[] neighbors = current.sameSectorNeighbors;
                        for (int i = 0; i < neighbors.Length; i++)
                        {
                            Surface other = neighbors[i];
                            if (other.maxY >= minFloor && other.minY <= maxFloor && reached.Add(other))
                            {
                                queue.Enqueue(other);
                            }
                        }
                    }
                }
                else
                {
                    var grid = BuildSurfaceGrid(surfaces);
                    while (queue.Count > 0)
                    {
                        Surface current = queue.Dequeue();
                        if (current.sameSectorNeighbors != null)
                        {
                            Surface[] same = current.sameSectorNeighbors;
                            for (int i = 0; i < same.Length; i++)
                            {
                                Surface other = same[i];
                                if (other.maxY >= minFloor && other.minY <= maxFloor && reached.Add(other))
                                {
                                    queue.Enqueue(other);
                                }
                            }
                        }

                        int minCx = (int)Math.Floor((current.minX - ConnectionSlack) / SurfaceGridCellSize);
                        int maxCx = (int)Math.Floor((current.maxX + ConnectionSlack) / SurfaceGridCellSize);
                        int minCy = (int)Math.Floor((current.minY - StepHeight) / SurfaceGridYCellSize);
                        int maxCy = (int)Math.Floor((current.maxY + StepHeight) / SurfaceGridYCellSize);
                        int minCz = (int)Math.Floor((current.minZ - ConnectionSlack) / SurfaceGridCellSize);
                        int maxCz = (int)Math.Floor((current.maxZ + ConnectionSlack) / SurfaceGridCellSize);
                        for (int cx = minCx; cx <= maxCx; cx++)
                        {
                            for (int cy = minCy; cy <= maxCy; cy++)
                            {
                                for (int cz = minCz; cz <= maxCz; cz++)
                                {
                                    if (!grid.TryGetValue((cx, cy, cz), out List<Surface> bucket)) continue;
                                    for (int b = 0; b < bucket.Count; b++)
                                    {
                                        Surface other = bucket[b];
                                        if (reached.Contains(other)) continue;
                                        if (current.sameSectorNeighbors != null && other.sameSectorNeighbors != null &&
                                            current.location.sector == other.location.sector) continue;
                                        if (current.Connects(other))
                                        {
                                            reached.Add(other);
                                            queue.Enqueue(other);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                var filtered = new List<Surface>(reached.Count);
                for (int i = 0; i < surfaces.Count; i++)
                {
                    if (reached.Contains(surfaces[i])) filtered.Add(surfaces[i]);
                }
                surfaces = filtered;
            }

            double best = maxDistanceSq;
            double bestHeight = double.MaxValue;
            bool found = false;
            NavPoint bestPoint = default;

            void Consider(Surface s, double px, double pz)
            {
                double distance = Square(px - x) + Square(pz - z);
                if (distance > best) return;
                double floor = s.Floor(px, pz);
                double floorErr = Math.Abs(floor - feet);
                if (floorErr > MaxFloorDistance) return;
                if (found && Math.Abs(distance - best) < 1e-7 && floorErr > bestHeight) return;
                if (reference != null && requireDirect && !Covered(surfaces, from[0], from[2], px, pz)) return;
                NavLocation at = s.location;
                at.floorY = floor;
                bestPoint = new NavPoint { x = px, y = floor + 95, z = pz, location = at };
                found = true;
                best = distance;
                bestHeight = floorErr;
            }

            bool checkExtra = reference != null && requireDirect;
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface s = surfaces[i];
                if (x < s.minX - maxDistance || x > s.maxX + maxDistance ||
                    z < s.minZ - maxDistance || z > s.maxZ + maxDistance) continue;

                s.Closest(x, z, out double cx, out double cz);
                Consider(s, cx, cz);
                if (checkExtra)
                {
                    Consider(s, s.centreX, s.centreZ);
                    s.Closest(from[0], from[2], out double fx, out double fz);
                    Consider(s, fx, fz);
                    double[] inset = s.inset;
                    for (int k = 0; k < inset.Length; k += 2)
                    {
                        Consider(s, inset[k], inset[k + 1]);
                    }
                }
            }

            point = bestPoint;
            return found;
        }

        private static bool Covered(List<Surface> surfaces, double x, double z, double tx, double tz)
        {
            double segMinX = Math.Min(x, tx) - 1e-7, segMaxX = Math.Max(x, tx) + 1e-7;
            double segMinZ = Math.Min(z, tz) - 1e-7, segMaxZ = Math.Max(z, tz) + 1e-7;
            List<(double start, double end)> intervals = new List<(double, double)>(8);
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface s = surfaces[i];
                if (s.maxX < segMinX || s.minX > segMaxX || s.maxZ < segMinZ || s.minZ > segMaxZ) continue;
                if (Interval(s.vertices, s.winding, x, z, tx, tz, out double start, out double end))
                {
                    intervals.Add((start, end));
                }
            }
            double covered = 0, slack = ConnectionSlack / Math.Max(1, Math.Sqrt(Square(tx - x) + Square(tz - z)));
            intervals.Sort((a, b) => a.start.CompareTo(b.start));
            for (int i = 0; i < intervals.Count; i++)
            {
                var interval = intervals[i];
                if (interval.start > covered + slack) return false;
                covered = Math.Max(covered, interval.end);
                if (covered >= 1 - 1e-8) return true;
            }
            return false;
        }

        // Clip a segment to a convex polygon, retaining its parameter interval.
        private static bool Interval(double[] polygon, double winding, double x, double z, double tx, double tz, out double start, out double end)
        {
            start = 0; end = 1;
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
