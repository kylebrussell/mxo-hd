using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Which NPC list a server loads, and the removal of rows that are one NPC seen again. mob_parsed.csv was built from
    /// retail logs, so an NPC seen in several logs has a row per sighting, each where it happened to stand: 8,125 of its
    /// 13,141 rows have a same-named NPC within 5 m. Loaded as they are, they stand in dense rows in the street. 5 m is a
    /// judgement from looking at the result in the client; retail's real spawn points aren't in the data.
    /// </summary>
    public static class MobSpawnFilter
    {
        /// <summary>Same name in the same district, closer than this (units, 100 per metre): the same NPC.</summary>
        public const double SameNpcUnits = 500;

        /// <summary>
        /// MXO_MOBS=test loads the small list the client regression's fixed screen positions are measured against.
        /// Anything else loads the whole world.
        /// </summary>
        public static bool UseTestList(string setting)
        {
            return string.Equals(setting, "test", StringComparison.OrdinalIgnoreCase);
        }

        private const double Cell = SameNpcUnits;

        /// <summary>Remembers kept NPCs per district and name, and says whether a new row is one of them seen again.</summary>
        public sealed class Seen
        {
            private readonly Dictionary<(ushort District, string Name, long X, long Z), List<(double X, double Z)>> cells =
                new Dictionary<(ushort, string, long, long), List<(double, double)>>();

            public bool IsRepeat(ushort district, string name, double x, double z)
            {
                long cx = (long)Math.Floor(x / Cell), cz = (long)Math.Floor(z / Cell);
                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dz = -1; dz <= 1; dz++)
                    {
                        if (!cells.TryGetValue((district, name, cx + dx, cz + dz), out var points))
                        {
                            continue;
                        }
                        foreach ((double X, double Z) p in points)
                        {
                            double ax = p.X - x, az = p.Z - z;
                            if (ax * ax + az * az < SameNpcUnits * SameNpcUnits)
                            {
                                return true;
                            }
                        }
                    }
                }

                var key = (district, name, cx, cz);
                if (!cells.TryGetValue(key, out var list))
                {
                    list = new List<(double, double)>();
                    cells[key] = list;
                }
                list.Add((x, z));
                return false;
            }
        }
    }
}
