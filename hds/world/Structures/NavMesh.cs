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
        public UInt16 region;  // outdoors: the sector's nav section - 1; interiors (not covered here) use the space id
        public UInt16 polygon; // the polygon's index within the region
        public double floorY;  // the polygon's floor at the position; retail's y is this + 95

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
    /// The outdoor nav meshes of the client's sector .nav files (data/navmesh/&lt;district&gt;.bin.gz, packed by
    /// research/world-data/navmesh.py; format in docs/WORLD-DATA.md). Retail's own-view record 03 02 00 01 1c names the
    /// sector, region and polygon the player stands on; outdoors these are the sector, nav section - 1 and the polygon's
    /// index, which matched all 24 outdoor records in the repo captures. Interior spaces (building floors, elevator
    /// spaces) have their nav meshes in the prefabs and aren't covered: Locate returns false there.
    /// </summary>
    public static class NavMesh
    {
        private class Polygon
        {
            public float minX, minZ, maxX, maxZ;
            public short[] xz;     // x0, z0, x1, z1, ...
            public float a, b, c, d; // plane; flat floors have a = c = 0, b = -1, d = y
        }

        private class Sector
        {
            public UInt16 id;
            public List<Polygon>[] regions;
        }

        private static readonly object loadLock = new object();
        private static readonly Dictionary<UInt16, Dictionary<UInt16, Sector>> districts = new Dictionary<UInt16, Dictionary<UInt16, Sector>>();
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

        private static Dictionary<UInt16, Sector> District(UInt16 metrId)
        {
            lock (loadLock)
            {
                if (districts.TryGetValue(metrId, out Dictionary<UInt16, Sector> loaded))
                {
                    return loaded;
                }

                Dictionary<UInt16, Sector> sectors = new Dictionary<UInt16, Sector>();
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
                        if (new string(r.ReadChars(4)) != "MXNV" || r.ReadUInt16() != 1)
                        {
                            throw new InvalidDataException("Not a nav mesh file: " + path);
                        }

                        r.ReadUInt16();
                        UInt32 count = r.ReadUInt32();
                        for (UInt32 i = 0; i < count; i++)
                        {
                            Sector s = new Sector { id = r.ReadUInt16() };
                            s.regions = new List<Polygon>[r.ReadUInt16()];
                            for (int g = 0; g < s.regions.Length; g++)
                            {
                                int polys = r.ReadUInt16();
                                List<Polygon> list = new List<Polygon>(polys);
                                for (int k = 0; k < polys; k++)
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
                                    p.xz = new short[n * 2];
                                    p.minX = p.minZ = float.MaxValue;
                                    p.maxX = p.maxZ = float.MinValue;
                                    for (int v = 0; v < n; v++)
                                    {
                                        short x = r.ReadInt16(), z = r.ReadInt16();
                                        p.xz[v * 2] = x;
                                        p.xz[v * 2 + 1] = z;
                                        p.minX = Math.Min(p.minX, x);
                                        p.maxX = Math.Max(p.maxX, x);
                                        p.minZ = Math.Min(p.minZ, z);
                                        p.maxZ = Math.Max(p.maxZ, z);
                                    }

                                    list.Add(p);
                                }

                                s.regions[g] = list;
                            }

                            sectors[s.id] = s;
                        }
                    }
                }
                else
                {
                    Output.WriteLine("[NAV] No nav mesh at " + path);
                }

                districts[metrId] = sectors;
                return sectors;
            }
        }

        /// <summary>How far a floor may be from y - 95 and still count. Retail's outdoor records were 0 to 10 off; inside a
        /// building the nearest outdoor polygon is its roof or the street, 400 or more away.</summary>
        public const double MaxFloorDistance = 200;

        /// <summary>
        /// The outdoor polygon under a world position: of those containing (x, z), the one whose floor is nearest to
        /// y - 95 (retail's positions stand 95 above the floor), within MaxFloorDistance. False inside buildings and where
        /// there's no data.
        /// </summary>
        public static bool Locate(UInt16 metrId, double x, double y, double z, out NavLocation location)
        {
            location = new NavLocation();
            SectorInfo info = SectorProperties.At(metrId, x, z);
            if (info == null || !District(metrId).TryGetValue(info.sector, out Sector sector))
            {
                return false;
            }

            double lx = x - info.minX, lz = z - info.minZ, feet = y - 95;
            double best = MaxFloorDistance;
            bool found = false;
            for (int g = 0; g < sector.regions.Length; g++)
            {
                List<Polygon> polys = sector.regions[g];
                for (int k = 0; k < polys.Count; k++)
                {
                    Polygon p = polys[k];
                    if (lx < p.minX - 0.5 || lx > p.maxX + 0.5 || lz < p.minZ - 0.5 || lz > p.maxZ + 0.5 || !Inside(p.xz, lx, lz))
                    {
                        continue;
                    }

                    double floor = -(p.a * lx + p.c * lz + p.d) / p.b;
                    double distance = Math.Abs(floor - feet);
                    if (distance <= best)
                    {
                        best = distance;
                        found = true;
                        location = new NavLocation { sector = info.sector, region = (UInt16)g, polygon = (UInt16)k, floorY = floor };
                    }
                }
            }

            return found;
        }

        /// <summary>Convex polygon test, either winding, with half a unit of slack for the rounded vertices.</summary>
        private static bool Inside(short[] xz, double x, double z)
        {
            int n = xz.Length / 2;
            bool anyPositive = false, anyNegative = false;
            for (int j = 0; j < n; j++)
            {
                int k = (j + 1) % n;
                double ex = xz[k * 2] - xz[j * 2], ez = xz[k * 2 + 1] - xz[j * 2 + 1];
                double cross = ex * (z - xz[j * 2 + 1]) - ez * (x - xz[j * 2]);
                double length = Math.Sqrt(ex * ex + ez * ez);
                if (cross > 0.5 * length)
                {
                    anyPositive = true;
                }
                else if (cross < -0.5 * length)
                {
                    anyNegative = true;
                }

                if (anyPositive && anyNegative)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
