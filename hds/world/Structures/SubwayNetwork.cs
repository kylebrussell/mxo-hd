using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>One connector of a subway line, as the client's .metr NETS chunk has it (data/subway_network.csv).</summary>
    public class SubwayStation
    {
        public UInt16 worldId;      // universe.btb id: 12 Richland (the Slums), 7 Downtown, 169 International
        public UInt16 metrId;       // the server's district (MxOLocations)
        public string district = "";
        public UInt16 network;      // index of the line in the world's NETS: 81 3c's NetworkID
        public string line = "";    // st_subway_networks, e.g. "Red Line"; empty on the unnamed test line
        public UInt16 connector;    // index in the line: 81 3c's ConnectorID
        public UInt32 nameId;
        public string name = "";    // st_subway_locations, e.g. "Richland - Tabor Park [ Red Line ]"
        public double x, y, z;
        public UInt16 linkWorld;    // another world this connector leads to (its map), 0 for a station

        /// <summary>A connector that only switches the map to another district's line: the client never sends it.</summary>
        public bool IsLink => linkWorld != 0 && linkWorld != worldId;

        /// <summary>A place a ride can end: on a named line, not a link to another district.</summary>
        public bool IsStation => line.Length > 0 && !IsLink;
    }

    /// <summary>
    /// The subway lines of the three cities from the client's own .metr files (research/world-data/subway_network.py).
    /// client.dll's subway map (viewsubwaymap.cpp) reads the same chunks, numbers lines and connectors in the same order,
    /// and asks for a ride with 81 3c &lt;universe world&gt; &lt;line index&gt; &lt;connector index&gt;.
    /// </summary>
    public static class SubwayNetwork
    {
        public const UInt16 RichlandWorld = 12, DowntownWorld = 7, InternationalWorld = 169;

        private static readonly object LoadLock = new object();
        private static List<SubwayStation> all;

        public static void Load(string path = null)
        {
            lock (LoadLock)
            {
                if (all != null && path == null)
                {
                    return;
                }
                List<SubwayStation> list = new List<SubwayStation>();
                path = path ?? DataLoader.DataPath("subway_network.csv");
                if (!File.Exists(path))
                {
                    Output.WriteLine("[SUBWAY] No subway network at " + path);
                }
                else
                {
                    Dictionary<string, int> col = null;
                    foreach (string line in File.ReadLines(path))
                    {
                        string[] f = line.Split(',');
                        if (col == null)
                        {
                            col = new Dictionary<string, int>();
                            for (int i = 0; i < f.Length; i++)
                            {
                                col[f[i].Trim()] = i;
                            }
                            continue;
                        }
                        if (f.Length < col.Count)
                        {
                            continue;
                        }
                        string S(string name) => f[col[name]].Trim();
                        double D(string name) => double.Parse(S(name), CultureInfo.InvariantCulture);
                        list.Add(new SubwayStation
                        {
                            worldId = UInt16.Parse(S("world_id")),
                            metrId = UInt16.Parse(S("metr_id")),
                            district = S("district"),
                            network = UInt16.Parse(S("network")),
                            line = S("line"),
                            connector = UInt16.Parse(S("connector")),
                            nameId = UInt32.Parse(S("name_id"), NumberStyles.HexNumber),
                            name = S("name"),
                            x = D("x"), y = D("y"), z = D("z"),
                            linkWorld = UInt16.Parse(S("link_world"))
                        });
                    }
                }
                all = list;
            }
        }

        public static List<SubwayStation> All
        {
            get
            {
                Load();
                return all;
            }
        }

        public static SubwayStation Get(UInt16 worldId, UInt16 network, UInt16 connector)
        {
            return All.FirstOrDefault(s => s.worldId == worldId && s.network == network && s.connector == connector);
        }

        /// <summary>The universe world id of a district, or 0 for one without subways.</summary>
        public static UInt16 WorldOf(UInt16 district)
        {
            return All.FirstOrDefault(s => s.metrId == district)?.worldId ?? 0;
        }

        /// <summary>The station (on a named line) nearest to a point in a district, with its distance in world units.</summary>
        public static SubwayStation Nearest(UInt16 district, double x, double y, double z, out double distance)
        {
            SubwayStation best = null;
            distance = double.MaxValue;
            foreach (SubwayStation s in All.Where(s => s.metrId == district && s.IsStation))
            {
                double d = Math.Sqrt((s.x - x) * (s.x - x) + (s.y - y) * (s.y - y) + (s.z - z) * (s.z - z));
                if (d < distance)
                {
                    best = s;
                    distance = d;
                }
            }
            return best;
        }
    }
}
