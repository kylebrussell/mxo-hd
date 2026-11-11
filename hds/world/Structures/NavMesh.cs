using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using hds.shared;

namespace hds
{
    /// <summary>Where a world position is on the client's nav mesh: the fields of retail's own-view position record.</summary>
    public struct NavLocation
    {
        public UInt16 sector;  // the static data's sector_id
        public UInt16 region;  // outdoors the sector's nav section - 1; indoors the outdoor count + the prefab placement's index
        public UInt16 polygon; // the polygon's index within the region's nav mesh
        public double floorY;  // the polygon's floor at the position; retail's y is this + 95
        public bool indoors;
        /// <summary>The sector's corner (its min x and min z in sector_properties.csv): outdoor polygons are relative to it.</summary>
        public double originX, originZ;

        /// <summary>
        /// The 7 bytes between the first byte and the position in retail's 03 02 00 01 1c &lt;u8&gt; ... &lt;x y z f32&gt;:
        /// u16 sector, u16 region, u16 polygon, u8 last. The last byte varied over time at one spot in retail
        /// (b7, bf, c9 in luizoe Recursion), so 0 is a guess.
        /// </summary>
        public byte[] RecordBytes(byte last = 0)
        {
            return new byte[]
            {
                (byte)sector, (byte)(sector >> 8), (byte)region, (byte)(region >> 8), (byte)polygon, (byte)(polygon >> 8), last
            };
        }
    }

    /// <summary>
    /// The client's nav meshes (data/navmesh/&lt;district&gt;.bin.gz, packed by research/world-data/navmesh.py; format in
    /// docs/WORLD-DATA.md). Retail's own-view record 03 02 00 01 1c names the sector, region and polygon the player stands
    /// on; the client lets a player through walls when it's wrong.
    ///   Outdoors: region = the sector .nav's section - 1, polygon = its index there (20 of 20 retail records).
    ///   Indoors: the sector's .sidi places floor prefabs (.iprf) in order; the k-th placement is region (outdoor region
    ///   count + k), and the polygon is the index in that prefab's INAV mesh, after undoing the placement's pivot and
    ///   quarter turns (all 65 retail records; one stands 0.1 unit outside its polygon, which the edge slack covers).
    /// </summary>
    public static partial class NavMesh
    {
        private class Polygon
        {
            public float minX, minZ, maxX, maxZ;
            public int[] xz;       // x0, z0, x1, z1, ... (v3 supports the large archive sectors)
            public float a, b, c, d; // plane; flat floors have a = c = 0, b = -1, d = y
            public double area2;      // twice the signed area: the winding

            public double FloorAt(double x, double z) => -(a * x + c * z + d) / b;
        }

        private class Placement
        {
            public int prefab;
            public int pivotX, pivotY, pivotZ, minX, minY, minZ, maxX, maxY, maxZ; // world units, sector-local
            public int rotation;                                                    // quarter turns
        }

        private class Sector
        {
            public List<Polygon>[] regions;
            public Placement[] placements;
            public List<Surface> projectionSurfaces;
        }

        private class District
        {
            public List<Polygon>[] prefabs;
            public Dictionary<UInt16, Sector> sectors = new Dictionary<UInt16, Sector>();
        }

        /// <summary>How far a floor may be from y - 95 and still count. Retail's records were 0 to 10 off; the wrong floor of a
        /// building is 400 or more away.</summary>
        public const double MaxFloorDistance = 200;

        /// <summary>How far outside a polygon's edge a position may be (players stand on edges).</summary>
        private const double EdgeSlack = 2;

        private static readonly object loadLock = new object();
        private static readonly Dictionary<UInt16, District> districts = new Dictionary<UInt16, District>();
        private static string directory;

        /// <summary>Points the loader at another folder (tests); districts load on first use.</summary>
        public static void UseDirectory(string path)
        {
            lock (loadLock)
            {
                directory = path;
                districts.Clear();
            }
        }

        private static List<Polygon> ReadPolygons(BinaryReader r, bool wide = false)
        {
            int count = r.ReadUInt16();
            List<Polygon> list = new List<Polygon>(count);
            for (int k = 0; k < count; k++)
            {
                byte head = r.ReadByte();
                Polygon p = new Polygon();
                if ((head & 0x80) == 0)
                {
                    p.b = -1;
                    p.d = r.ReadSingle(); // floor y = -(a x + c z + d) / b = d
                }
                else
                {
                    p.a = r.ReadSingle();
                    p.b = r.ReadSingle();
                    p.c = r.ReadSingle();
                    p.d = r.ReadSingle();
                }

                int n = head & 0x7f;
                p.xz = new int[n * 2];
                p.minX = p.minZ = float.MaxValue;
                p.maxX = p.maxZ = float.MinValue;
                for (int v = 0; v < n; v++)
                {
                    int x = wide ? r.ReadInt32() : r.ReadInt16(), z = wide ? r.ReadInt32() : r.ReadInt16();
                    p.xz[v * 2] = x;
                    p.xz[v * 2 + 1] = z;
                    p.minX = Math.Min(p.minX, x);
                    p.maxX = Math.Max(p.maxX, x);
                    p.minZ = Math.Min(p.minZ, z);
                    p.maxZ = Math.Max(p.maxZ, z);
                }

                for (int v = 0; v < n; v++)
                {
                    int w = (v + 1) % n;
                    p.area2 += (double)p.xz[v * 2] * p.xz[w * 2 + 1] - (double)p.xz[w * 2] * p.xz[v * 2 + 1];
                }

                list.Add(p);
            }

            return list;
        }

        private static District Load(UInt16 metrId)
        {
            lock (loadLock)
            {
                if (districts.TryGetValue(metrId, out District loaded))
                {
                    return loaded;
                }

                District district = new District { prefabs = new List<Polygon>[0] };
                string path = Path.Combine(directory ?? DataLoader.DataPath("navmesh"), metrId + ".bin.gz");
                if (File.Exists(path))
                {
                    MemoryStream raw = new MemoryStream();
                    using (GZipStream gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
                    {
                        gz.CopyTo(raw);
                    }

                    raw.Position = 0;
                    using (BinaryReader r = new BinaryReader(raw))
                    {
                        uint magic = r.ReadUInt32();
                        ushort version = r.ReadUInt16();
                        if (magic != 0x564e584d || version != 2 && version != 3)
                        {
                            throw new InvalidDataException("Not a supported nav mesh file: " + path);
                        }

                        r.ReadUInt16();
                        district.prefabs = new List<Polygon>[r.ReadUInt32()];
                        for (int i = 0; i < district.prefabs.Length; i++)
                        {
                            r.ReadUInt32(); // the prefab's resource id
                            district.prefabs[i] = ReadPolygons(r, version == 3);
                        }

                        UInt32 count = r.ReadUInt32();
                        for (UInt32 i = 0; i < count; i++)
                        {
                            UInt16 id = r.ReadUInt16();
                            Sector s = new Sector { regions = new List<Polygon>[r.ReadUInt16()] };
                            for (int g = 0; g < s.regions.Length; g++)
                            {
                                s.regions[g] = ReadPolygons(r, version == 3);
                            }

                            s.placements = new Placement[r.ReadUInt16()];
                            for (int k = 0; k < s.placements.Length; k++)
                            {
                                Placement p = new Placement { prefab = r.ReadUInt16() };
                                p.pivotX = r.ReadInt16() * 10;
                                p.pivotY = r.ReadInt16() * 10;
                                p.pivotZ = r.ReadInt16() * 10;
                                p.minX = r.ReadInt16() * 10;
                                p.minY = r.ReadInt16() * 10;
                                p.minZ = r.ReadInt16() * 10;
                                p.maxX = r.ReadInt16() * 10;
                                p.maxY = r.ReadInt16() * 10;
                                p.maxZ = r.ReadInt16() * 10;
                                p.rotation = r.ReadByte();
                                s.placements[k] = p;
                            }

                            district.sectors[id] = s;
                        }
                    }
                }
                else
                {
                    Output.WriteLine("[NAV] No nav mesh at " + path);
                }

                districts[metrId] = district;
                return district;
            }
        }

        /// <summary>
        /// The polygon under a world position, outdoors or in a building: of the polygons containing (x, z), the one whose
        /// floor is nearest to y - 95 (retail's positions stand 95 above the floor), within MaxFloorDistance. False where
        /// there's none or no data.
        /// </summary>
        public static bool Locate(UInt16 metrId, double x, double y, double z, out NavLocation location)
        {
            location = new NavLocation();
            SectorInfo info = SectorProperties.At(metrId, x, z);
            District district = Load(metrId);
            if (info == null || !district.sectors.TryGetValue(info.sector, out Sector sector))
            {
                return false;
            }

            double lx = x - info.minX, lz = z - info.minZ, feet = y - 95;
            double best = MaxFloorDistance, bestOutside = EdgeSlack;
            bool found = false;
            for (int g = 0; g < sector.regions.Length; g++)
            {
                List<Polygon> polys = sector.regions[g];
                for (int k = 0; k < polys.Count; k++)
                {
                    Polygon p = polys[k];
                    double outside = Outside(p, lx, lz);
                    if (outside > EdgeSlack)
                    {
                        continue;
                    }

                    double floor = p.FloorAt(lx, lz), distance = Math.Abs(floor - feet);
                    if (Better(distance, outside, ref best, ref bestOutside))
                    {
                        found = true;
                        location = new NavLocation
                        {
                            sector = info.sector, region = (UInt16)g, polygon = (UInt16)k, floorY = floor, originX = info.minX, originZ = info.minZ
                        };
                    }
                }
            }

            for (int k = 0; k < sector.placements.Length; k++)
            {
                Placement pl = sector.placements[k];
                if (lx < pl.minX - EdgeSlack || lx > pl.maxX + EdgeSlack || lz < pl.minZ - EdgeSlack || lz > pl.maxZ + EdgeSlack ||
                    feet < pl.minY - MaxFloorDistance || feet > pl.maxY + MaxFloorDistance)
                {
                    continue;
                }

                // Into the prefab's own frame: undo the pivot, then the quarter turns
                double dx = lx - pl.pivotX, dz = lz - pl.pivotZ, u, v;
                switch (pl.rotation)
                {
                    case 1: u = -dz; v = dx; break;
                    case 2: u = -dx; v = -dz; break;
                    case 3: u = dz; v = -dx; break;
                    default: u = dx; v = dz; break;
                }

                List<Polygon> polys = district.prefabs[pl.prefab];
                for (int i = 0; i < polys.Count; i++)
                {
                    Polygon p = polys[i];
                    double outside = Outside(p, u, v);
                    if (outside > EdgeSlack)
                    {
                        continue;
                    }

                    double floor = p.FloorAt(u, v) + pl.pivotY, distance = Math.Abs(floor - feet);
                    if (Better(distance, outside, ref best, ref bestOutside))
                    {
                        found = true;
                        location = new NavLocation
                        {
                            sector = info.sector, region = (UInt16)(sector.regions.Length + k), polygon = (UInt16)i, floorY = floor,
                            indoors = true, originX = info.minX, originZ = info.minZ
                        };
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Whether a walker can step straight from one polygon to the other: the same polygon, or two polygons of one mesh
        /// with an edge in common for at least <see cref="MinOpenEdge"/> units. One mesh is the outdoors (all outdoor
        /// regions, across sector sides too: the client's edges cross them) or one floor prefab placement. An edge with no polygon on its other side is a wall. The
        /// packed meshes keep no neighbour list (the client's .nav/INAV edges name one; docs/WORLD-DATA.md), so the shared
        /// edge is found from the outlines. Different indoor regions are different prefabs (a room and an elevator car,
        /// two floors): never open, since doors and portals join them.
        /// </summary>
        public static bool OpenBetween(UInt16 metrId, NavLocation a, NavLocation b)
        {
            if (a.indoors != b.indoors || (a.indoors && (a.sector != b.sector || a.region != b.region)))
            {
                return false;
            }
            if (a.sector == b.sector && a.region == b.region && a.polygon == b.polygon)
            {
                return true;
            }
            District district = Load(metrId);
            if (!district.sectors.TryGetValue(a.sector, out Sector sa) || !district.sectors.TryGetValue(b.sector, out Sector sb))
            {
                return false;
            }
            Polygon pa = PolygonOf(district, sa, a), pb = PolygonOf(district, sb, b);
            // Outdoors into world coordinates (sectors differ in origin); one prefab's polygons share its frame
            double dx = a.indoors ? 0 : b.originX - a.originX, dz = a.indoors ? 0 : b.originZ - a.originZ;
            return pa != null && pb != null && SharedEdgeLength(pa, pb, dx, dz) >= MinOpenEdge;
        }

        /// <summary>How much edge two polygons must share to be walked between (half a body's width).</summary>
        public const double MinOpenEdge = 30;

        private static Polygon PolygonOf(District district, Sector sector, NavLocation at)
        {
            List<Polygon> polys;
            if (!at.indoors)
            {
                if (at.region >= sector.regions.Length) return null;
                polys = sector.regions[at.region];
            }
            else
            {
                int k = at.region - sector.regions.Length;
                if (k < 0 || k >= sector.placements.Length) return null;
                polys = district.prefabs[sector.placements[k].prefab];
            }
            return at.polygon < polys.Count ? polys[at.polygon] : null;
        }

        /// <summary>The longest stretch along which an edge of one polygon lies on an edge of the other (collinear, overlapping).</summary>
        private static double SharedEdgeLength(Polygon p, Polygon q, double qdx, double qdz)
        {
            double best = 0;
            int n = p.xz.Length / 2, m = q.xz.Length / 2;
            for (int i = 0; i < n; i++)
            {
                double ax = p.xz[i * 2], az = p.xz[i * 2 + 1];
                double bx = p.xz[(i + 1) % n * 2], bz = p.xz[(i + 1) % n * 2 + 1];
                double ex = bx - ax, ez = bz - az, length = Math.Sqrt(ex * ex + ez * ez);
                if (length < 1e-6) continue;
                ex /= length;
                ez /= length;
                for (int j = 0; j < m; j++)
                {
                    double cx = q.xz[j * 2] + qdx, cz = q.xz[j * 2 + 1] + qdz;
                    double dx = q.xz[(j + 1) % m * 2] + qdx, dz = q.xz[(j + 1) % m * 2 + 1] + qdz;
                    // Both ends of q's edge on p's edge line (within the slack players stand outside edges by)
                    if (Math.Abs(ex * (cz - az) - ez * (cx - ax)) > EdgeSlack || Math.Abs(ex * (dz - az) - ez * (dx - ax)) > EdgeSlack)
                    {
                        continue;
                    }
                    double t0 = ex * (cx - ax) + ez * (cz - az), t1 = ex * (dx - ax) + ez * (dz - az);
                    double overlap = Math.Min(length, Math.Max(t0, t1)) - Math.Max(0, Math.Min(t0, t1));
                    best = Math.Max(best, overlap);
                }
            }
            return best;
        }

        /// <summary>The nearer floor wins; on the same floor, the polygon the position is further inside, and on a shared edge or
        /// corner the later polygon (retail's tutorial spawns sit on corners: 32 of 5, 20, 31, 32; 82 of 59, 82).</summary>
        private static bool Better(double distance, double outside, ref double best, ref double bestOutside)
        {
            if (distance < best - 0.01 || (distance <= best + 0.01 && outside <= bestOutside + 0.01))
            {
                best = distance;
                bestOutside = outside;
                return true;
            }

            return false;
        }

        /// <summary>How far outside a convex polygon a point is, in units: at most 0 inside, large when far off.</summary>
        private static double Outside(Polygon p, double x, double z)
        {
            if (x < p.minX - EdgeSlack || x > p.maxX + EdgeSlack || z < p.minZ - EdgeSlack || z > p.maxZ + EdgeSlack)
            {
                return double.MaxValue;
            }

            int[] xz = p.xz;
            int n = xz.Length / 2;
            double winding = p.area2 >= 0 ? 1 : -1, worst = double.MinValue;
            for (int j = 0; j < n; j++)
            {
                int k = (j + 1) % n;
                double ex = xz[k * 2] - xz[j * 2], ez = xz[k * 2 + 1] - xz[j * 2 + 1];
                double length = Math.Sqrt(ex * ex + ez * ez);
                if (length < 1e-6)
                {
                    continue;
                }

                worst = Math.Max(worst, -winding * (ex * (z - xz[j * 2 + 1]) - ez * (x - xz[j * 2])) / length);
            }

            return worst;
        }
    }
}
