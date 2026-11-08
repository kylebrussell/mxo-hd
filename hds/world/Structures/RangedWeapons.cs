using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace hds
{
    /// <summary>
    /// Guns and their stats. data/weapons.csv was decoded from the 7.6005 client's own game object database
    /// (resource/GameObjects/gameobjects.gob in packmaps/gameobjects.pkb): category FourCC, Tool/BaseDamage,
    /// Tool/Range (m), Tool/ReuseTimer (s), requirements and Tool/NoiseGenerated for 632 guns.
    ///
    /// The attack class byte in shot records (<c>01 0c &lt;class&gt;</c>) follows the category: client.dll switches on
    /// IWRf/IWHg/IWSm/IWDH/IWDS in that order (0x47..0x4b there), and retail captures show 0x37 for rifles and
    /// shotguns (IWRf), 0x38 for handguns (Revolver0, SemiAutoPistolSystem) and 0x39 for SMGs, so dual handguns are
    /// 0x3a and dual SMGs 0x3b.
    /// </summary>
    public static class RangedWeapons
    {
        public const byte ClassRifle = 0x37;
        public const byte ClassPistol = 0x38;
        public const byte ClassSubmachinegun = 0x39;
        public const byte ClassDualPistol = 0x3a;
        public const byte ClassDualSubmachinegun = 0x3b;

        public sealed class Stats
        {
            public UInt32 Goid;
            public string Name;
            public string Category;
            public byte AttackClass;
            public int BaseDamage;
            public float RangeMetres;
            public float ReuseSeconds;
            public int Noise;
        }

        private static readonly object loadLock = new object();
        private static Dictionary<UInt32, Stats> statsByGoid;
        private static readonly ConcurrentDictionary<UInt32, byte> classByName = new ConcurrentDictionary<UInt32, byte>();

        /// <summary>Attack class of a category FourCC from the client data, 0 when it isn't a gun category.</summary>
        public static byte AttackClassForCategory(string category)
        {
            switch (category)
            {
                case "IWRf": return ClassRifle;
                case "IWHg": return ClassPistol;
                case "IWSm": return ClassSubmachinegun;
                case "IWDH": return ClassDualPistol;
                case "IWDS": return ClassDualSubmachinegun;
                default: return 0;
            }
        }

        /// <summary>Fallback for items missing from weapons.csv: the class from the internal name, 0 when not a gun.</summary>
        public static byte AttackClassForName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }
            string n = name.ToLowerInvariant();
            if (n.Contains("dualpistol"))
            {
                return ClassDualPistol;
            }
            if (n.Contains("dualsubmachinegun"))
            {
                return ClassDualSubmachinegun;
            }
            if (n.Contains("submachinegun") || n.Contains("subgun"))
            {
                return ClassSubmachinegun;
            }
            if (n.Contains("rifle") || n.Contains("shotgun"))
            {
                return ClassRifle;
            }
            if (n.Contains("pistol") || n.Contains("revolver"))
            {
                return ClassPistol;
            }
            return 0;
        }

        /// <summary>Parses weapons.csv lines (comment lines start with #, then a header row).</summary>
        public static Dictionary<UInt32, Stats> ParseStats(IEnumerable<string> lines)
        {
            var result = new Dictionary<UInt32, Stats>();
            bool header = true;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }
                if (header)
                {
                    header = false;
                    continue;
                }
                string[] c = line.Split(',');
                if (c.Length < 10 || !UInt32.TryParse(c[0], out UInt32 goid))
                {
                    continue;
                }
                var s = new Stats
                {
                    Goid = goid,
                    Name = c[1],
                    Category = c[2],
                    AttackClass = AttackClassForCategory(c[2]),
                    BaseDamage = ParseInt(c[3]),
                    RangeMetres = ParseFloat(c[4]),
                    ReuseSeconds = ParseFloat(c[5]),
                    Noise = ParseInt(c[9])
                };
                if (s.AttackClass != 0)
                {
                    result[goid] = s;
                }
            }
            return result;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private static Dictionary<UInt32, Stats> AllStats()
        {
            if (statsByGoid != null)
            {
                return statsByGoid;
            }
            lock (loadLock)
            {
                if (statsByGoid == null)
                {
                    try
                    {
                        statsByGoid = ParseStats(File.ReadAllLines(DataLoader.DataPath("weapons.csv")));
                    }
                    catch (Exception ex)
                    {
                        Output.WriteDebugLog("[COMBAT] weapons.csv not loaded: " + ex.Message);
                        statsByGoid = new Dictionary<UInt32, Stats>();
                    }
                }
            }
            return statsByGoid;
        }

        /// <summary>The client's stats for a gun, or null when it isn't in weapons.csv.</summary>
        public static Stats GetStats(UInt32 goid)
        {
            return goid != 0 && AllStats().TryGetValue(goid, out Stats s) ? s : null;
        }

        /// <summary>Attack class of a weapon by goid, 0 when it isn't a gun.</summary>
        public static byte AttackClass(UInt32 goid)
        {
            if (goid == 0)
            {
                return 0;
            }
            Stats s = GetStats(goid);
            if (s != null)
            {
                return s.AttackClass;
            }
            return classByName.GetOrAdd(goid, g =>
            {
                GameObjectItem item = DataLoader.getInstance().GODB.Find(i => (UInt32)i.getGOID() == g);
                return AttackClassForName(item?.getName());
            });
        }

        public static bool IsGun(UInt32 goid)
        {
            return AttackClass(goid) != 0;
        }

        /// <summary>
        /// Typical values per class in weapons.csv, for guns missing from it: handguns 16 m / 4.5 s, dual handguns
        /// 16 m / 4.0 s, SMGs 20 m / 3.0 s, rifles 22 m / 3.5 s, dual SMGs 20 m / 2.5 s. They agree with the intervals
        /// measured in retail captures (rifles ~4 s, handguns ~5 s, SMGs 3-4 s, dual SMGs ~3 s at one-second resolution).
        /// </summary>
        public static float DefaultReuseSeconds(byte attackClass)
        {
            switch (attackClass)
            {
                case ClassRifle: return 3.5f;
                case ClassSubmachinegun: return 3.0f;
                case ClassDualPistol: return 4.0f;
                case ClassDualSubmachinegun: return 2.5f;
                default: return 4.5f;
            }
        }

        public static float DefaultRangeMetres(byte attackClass)
        {
            switch (attackClass)
            {
                case ClassRifle: return 22f;
                case ClassSubmachinegun:
                case ClassDualSubmachinegun: return 20f;
                default: return 16f;
            }
        }

        /// <summary>Time between shots with this gun (Tool/ReuseTimer).</summary>
        public static int FireIntervalMs(UInt32 goid)
        {
            Stats s = GetStats(goid);
            float seconds = s != null && s.ReuseSeconds > 0 ? s.ReuseSeconds : DefaultReuseSeconds(AttackClassOrPistol(goid));
            return (int)Math.Round(seconds * 1000);
        }

        /// <summary>How far this gun reaches, in metres (Tool/Range).</summary>
        public static float RangeMetres(UInt32 goid)
        {
            Stats s = GetStats(goid);
            return s != null && s.RangeMetres > 0 ? s.RangeMetres : DefaultRangeMetres(AttackClassOrPistol(goid));
        }

        /// <summary>
        /// NoiseLevel a shot sets (Tool/NoiseGenerated). Retail's first shot with the Redpill Special set NoiseLevel
        /// 0x32 = 50, the Redpill's NoiseGenerated.
        /// </summary>
        public static byte Noise(UInt32 goid)
        {
            Stats s = GetStats(goid);
            int noise = s != null && s.Noise > 0 ? s.Noise : 50;
            return (byte)Math.Min(255, noise);
        }

        /// <summary>
        /// NoiseLevel after a shot: the first shot sets the gun's noise, each further shot closes half the gap to 100.
        /// Retail's tutorial fight with the Redpill Special (noise 50) sent exactly 50, 75, 87, 93, 96
        /// (0x32, 0x4b, 0x57, 0x5d, 0x60).
        /// </summary>
        public static byte NextNoiseLevel(byte current, byte weaponNoise)
        {
            if (current == 0)
            {
                return weaponNoise;
            }
            return current >= 100 ? current : (byte)(current + (100 - current) / 2);
        }

        /// <summary>
        /// Damage factor from Tool/BaseDamage. In the client data BaseDamage is a small tier (1 for the starter
        /// Redpill Special up to 8 and beyond for high-level guns) that rises with the level requirement; how retail
        /// turned it into hit points is unknown. This guess adds 15% per tier above 1.
        /// </summary>
        public static float DamageFactor(UInt32 goid)
        {
            Stats s = GetStats(goid);
            int tier = s != null ? Math.Max(1, s.BaseDamage) : 1;
            return 1f + 0.15f * (tier - 1);
        }

        private static byte AttackClassOrPistol(UInt32 goid)
        {
            byte c = AttackClass(goid);
            return c == 0 ? ClassPistol : c;
        }
    }
}
