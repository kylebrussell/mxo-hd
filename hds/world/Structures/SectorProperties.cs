using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using hds.shared;

namespace hds
{
    /// <summary>
    /// One sector of a district as the client's world file describes it (data/sector_properties.csv, made from the
    /// .metr files by research/world-data/metr_sectors.py; see docs/WORLD-DATA.md).
    /// </summary>
    public class SectorInfo
    {
        public UInt16 metrId;
        public UInt16 sector;
        public string file = "";
        public int minX, minZ, maxX, maxZ;
        public string name = "";          // the designers' label: a neighbourhood, "Facade", "No name"
        public UInt32 neighbourhoodId;    // st_neighborhoodnames id, 0 on scenery and interiors
        public string neighbourhood = ""; // its text
        public int level;                 // the area's level; retail's NPC levels per neighbourhood centre on it
        public int contactId;             // a neighbourhood contact's id in sponsors.xml, 0 for none
        public string contact = "";
        public int gangType;              // -1 none (all of Richland), 0 unset
        public bool pvpZone;
        public bool missionAreas;         // "Enable Mission Areas"
        public string musicGroup = "";

        /// <summary>The per-neighbourhood part of Gang Type. A guess at its meaning: see docs/WORLD-DATA.md.</summary>
        public int GangArea => gangType > 0 ? gangType % 200 : 0;

        /// <summary>Gang Type / 200: 0 to 3. Retail NPCs ran a little higher in higher tiers. A guess, as above.</summary>
        public int GangTier => gangType > 0 ? gangType / 200 : 0;

        /// <summary>The neighbourhood's text, or the designers' label where the sector has no neighbourhood id.</summary>
        public string NeighbourhoodOrName => neighbourhood.Length > 0 ? neighbourhood : name;

        public double CentreX => (minX + maxX) / 2.0;
        public double CentreZ => (minZ + maxZ) / 2.0;
    }

    /// <summary>
    /// Per-sector designer properties of every district (level, neighbourhood, contact, gang type, PvP zone, mission areas),
    /// looked up by sector or by position. Sector boxes tile a district on a 1000-unit grid without overlapping, so a
    /// position is in at most one sector.
    /// </summary>
    public static class SectorProperties
    {
        private const int Cell = 1000;
        private static readonly object loadLock = new object();
        private static Dictionary<(UInt16, UInt16), SectorInfo> bySector;
        private static Dictionary<(UInt16, int, int), SectorInfo> byCell;
        private static Dictionary<int, List<SectorInfo>> byContact;

        public static void Load(string path = null)
        {
            lock (loadLock)
            {
                if (bySector != null && path == null)
                {
                    return;
                }

                var sectors = new Dictionary<(UInt16, UInt16), SectorInfo>();
                var cells = new Dictionary<(UInt16, int, int), SectorInfo>();
                var contacts = new Dictionary<int, List<SectorInfo>>();
                path = path ?? DataLoader.DataPath("sector_properties.csv");
                if (!File.Exists(path))
                {
                    Output.WriteLine("[WORLD] No sector properties at " + path);
                    bySector = sectors;
                    byCell = cells;
                    byContact = contacts;
                    return;
                }

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
                    int I(string name) => int.TryParse(S(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

                    SectorInfo s = new SectorInfo
                    {
                        metrId = (UInt16)I("metr_id"),
                        sector = (UInt16)I("sector"),
                        file = S("file"),
                        minX = I("min_x"), minZ = I("min_z"), maxX = I("max_x"), maxZ = I("max_z"),
                        name = S("name"),
                        neighbourhoodId = S("neighbourhood_id").Length > 0 ? Convert.ToUInt32(S("neighbourhood_id"), 16) : 0,
                        neighbourhood = S("neighbourhood"),
                        level = I("level"),
                        contactId = I("contact_type"),
                        contact = S("contact"),
                        gangType = I("gang_type"),
                        pvpZone = I("pvp_zone") == 1,
                        missionAreas = I("enable_mission_areas") == 1,
                        musicGroup = S("music_group")
                    };
                    sectors[(s.metrId, s.sector)] = s;
                    for (int cx = s.minX / Cell; cx < s.maxX / Cell; cx++)
                    {
                        for (int cz = s.minZ / Cell; cz < s.maxZ / Cell; cz++)
                        {
                            cells[(s.metrId, cx, cz)] = s;
                        }
                    }

                    if (s.contactId > 0)
                    {
                        if (!contacts.TryGetValue(s.contactId, out List<SectorInfo> list))
                        {
                            contacts[s.contactId] = list = new List<SectorInfo>();
                        }

                        list.Add(s);
                    }
                }

                bySector = sectors;
                byCell = cells;
                byContact = contacts;
            }
        }

        /// <summary>A sector by its index (the static data's sector_id), or null.</summary>
        public static SectorInfo Get(UInt16 metrId, UInt16 sector)
        {
            Load();
            return bySector.TryGetValue((metrId, sector), out SectorInfo s) ? s : null;
        }

        /// <summary>The sector a world position falls in, or null outside every sector.</summary>
        public static SectorInfo At(UInt16 metrId, double x, double z)
        {
            Load();
            int cx = (int)Math.Floor(x / Cell), cz = (int)Math.Floor(z / Cell);
            return byCell.TryGetValue((metrId, cx, cz), out SectorInfo s) ? s : null;
        }

        /// <summary>The area level at a position (the sector's Level), or 0 where the data has none.</summary>
        public static int LevelAt(UInt16 metrId, double x, double z)
        {
            return At(metrId, x, z)?.level ?? 0;
        }

        /// <summary>Whether a position is in a sector marked PVP Zone (only the archive and combat constructs are).</summary>
        public static bool IsPvpZone(UInt16 metrId, double x, double z)
        {
            return At(metrId, x, z)?.pvpZone ?? false;
        }

        /// <summary>
        /// Whether missions may be set in a sector ("Enable Mission Areas"). In the cities only the scenery ring and a few
        /// unnamed sectors are off. Unknown sectors count as allowed, so a missing data file changes nothing.
        /// </summary>
        public static bool MissionAreasEnabled(UInt16 metrId, UInt16 sector)
        {
            return Get(metrId, sector)?.missionAreas ?? true;
        }

        /// <summary>The sectors marked with a neighbourhood contact (sponsors.xml id): where that contact belongs.</summary>
        public static IReadOnlyList<SectorInfo> SectorsOfContact(int contactId)
        {
            Load();
            return byContact.TryGetValue(contactId, out List<SectorInfo> list) ? list : (IReadOnlyList<SectorInfo>)Array.Empty<SectorInfo>();
        }
    }
}
