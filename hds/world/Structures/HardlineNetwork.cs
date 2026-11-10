using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>One hardline as the client's .hdl file has it (data/hardline_network.csv).</summary>
    public class Hardline
    {
        public UInt16 metrId;
        public UInt16 hid;          // the id in 6c, 80 fb, 81 8e and 80 ff
        public string name = "";    // st_hardlinenames text, e.g. "Richland.Mara (Central)"; empty for the unnamed test entries
        public string address = ""; // the booth's own name from the cooked_cod, e.g. "908 Bojangles Boulevard"
        public double x, y, z;      // the booth
        public double spotX, spotY, spotZ; // booth + Offset: where retail put a player arriving here
        public double yaw;          // the Offset's direction, atan2(x, z)
        public int flags;
        public List<UInt16> links = new List<UInt16>(); // Portals that land on another hardline (not used by the server)
    }

    /// <summary>
    /// The hardline network of the three cities from the client's own .hdl files. The client draws its hardline map from
    /// the same files, so these ids, names and places are the ones the player picks from.
    ///
    /// Retail put a player arriving by 81 8e exactly at the booth plus its Offset in x and z, for all 62 hardlines whose
    /// arrival the repo captures show (crashover2k proxy_023_teleports_*, RajkoPacketsNearTheEnd_PacketsAgentsAndInt*,
    /// entilsar lastday2, ...). The y there is the ground, up to 10 units below the booth's y + 95. The old
    /// data_hardlines rows were players' positions near the booth, 3 to 1,169 units off (median 113), and missed 8 hardlines
    /// (Slums 83, 143, 154; Downtown 7, 30, 32, 65, 72).
    /// </summary>
    public static class HardlineNetwork
    {
        /// <summary>The districts the network covers and their keys in the districts table.</summary>
        private static readonly Dictionary<UInt16, string> Cities = new Dictionary<UInt16, string>
        {
            { (UInt16)MxOLocations.SLUMS, "slums" }, { (UInt16)MxOLocations.DOWNTOWN, "downtown" },
            { (UInt16)MxOLocations.INTERNATIONAL, "international" }
        };

        private static readonly object loadLock = new object();
        private static Dictionary<(UInt16, UInt16), Hardline> byId;
        private static Dictionary<UInt16, List<Hardline>> byDistrict;

        public static void Load(string path = null)
        {
            lock (loadLock)
            {
                if (byId != null && path == null)
                {
                    return;
                }

                var ids = new Dictionary<(UInt16, UInt16), Hardline>();
                var districts = new Dictionary<UInt16, List<Hardline>>();
                path = path ?? DataLoader.DataPath("hardline_network.csv");
                if (!File.Exists(path))
                {
                    Output.WriteLine("[HARDLINE] No hardline network at " + path);
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

                        Hardline h = new Hardline
                        {
                            metrId = UInt16.Parse(S("metr_id")),
                            hid = UInt16.Parse(S("hid")),
                            name = S("name"),
                            address = S("address"),
                            x = D("x"), y = D("y"), z = D("z"),
                            spotX = D("spot_x"), spotY = D("spot_y"), spotZ = D("spot_z"),
                            yaw = D("yaw"),
                            flags = int.Parse(S("flags")),
                            links = S("links").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(UInt16.Parse).ToList()
                        };
                        ids[(h.metrId, h.hid)] = h;
                        if (!districts.TryGetValue(h.metrId, out List<Hardline> list))
                        {
                            districts[h.metrId] = list = new List<Hardline>();
                        }

                        list.Add(h);
                    }
                }

                byId = ids;
                byDistrict = districts;
            }
        }

        /// <summary>Whether this district's hardlines come from the network (the three cities) rather than data_hardlines.</summary>
        public static bool Covers(UInt16 district)
        {
            Load();
            return Cities.ContainsKey(district) && byDistrict.ContainsKey(district);
        }

        public static Hardline Get(UInt16 district, UInt16 hid)
        {
            Load();
            return byId.TryGetValue((district, hid), out Hardline h) ? h : null;
        }

        /// <summary>
        /// The hardlines a player can pick in a district, in id order: the named ones. Downtown 30 and 32 (one place,
        /// flagged, no name) and Slums 154 (a no-model hardline with no name) are left out.
        /// </summary>
        public static List<UInt16> Ids(UInt16 district)
        {
            Load();
            return byDistrict.TryGetValue(district, out List<Hardline> list)
                ? list.Where(h => h.name.Length > 0).Select(h => h.hid).OrderBy(id => id).ToList()
                : new List<UInt16>();
        }

        /// <summary>The named hardline whose booth is nearest to (x, z), or 0 when the district has none.</summary>
        public static UInt16 Nearest(UInt16 district, double x, double z)
        {
            Load();
            if (!byDistrict.TryGetValue(district, out List<Hardline> list))
            {
                return 0;
            }

            Hardline best = null;
            double bestDistance = double.MaxValue;
            foreach (Hardline h in list)
            {
                double dx = h.x - x, dz = h.z - z, d = dx * dx + dz * dz;
                if (h.name.Length > 0 && d < bestDistance)
                {
                    best = h;
                    bestDistance = d;
                }
            }

            return best?.hid ?? 0;
        }

        /// <summary>
        /// Sets the player's district and position to a city hardline's arrival spot and saves it, as updateLocationByHL
        /// does with data_hardlines. False (nothing changed) for a district or hardline the network doesn't know.
        /// </summary>
        public static bool MoveTo(WorldClient client, UInt16 district, UInt16 hid)
        {
            Hardline h = Get(district, hid);
            if (h == null || !Cities.TryGetValue(district, out string key))
            {
                return false;
            }

            client.playerData.setDistrict(key);
            client.playerData.setDistrictId(district);
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(h.spotX, h.spotY, h.spotZ));
            Store.dbManager.WorldDbHandler.SavePlayer(client);
            Output.WriteDebugLog("[HARDLINE] To " + h.name + " (" + district + "/" + hid + ") at " + h.spotX + "," + h.spotY + "," + h.spotZ);
            return true;
        }
    }
}
