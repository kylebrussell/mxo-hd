using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>The same canonical definition authority for live offers, persisted progress and completion.</summary>
    public static class MissionDefinitions
    {
        public static Mission Find(int id) => Find(id, DataLoader.getInstance().missions);

        internal static Mission Find(int id, IEnumerable<Mission> authored)
        {
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            if (id <= 0) return null;
            var files = authored.Where(m => m.id == id).Take(2).ToArray();
            bool genericId = NeighbourhoodMissions.HasCanonicalId(id);
            // Never silently prefer an authored collision over an existing generated identity (or vice versa).
            if (files.Length > 1 || files.Length == 1 && genericId) return null;
            return files.Length == 1 ? files[0] : NeighbourhoodMissions.FindCanonical(id);
        }
    }
}
