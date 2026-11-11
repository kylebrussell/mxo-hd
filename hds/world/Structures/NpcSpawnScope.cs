using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>Context of a captured spawn, kept separate from its hostility and deduplication.</summary>
    public static class NpcSpawnScope
    {
        public enum Scope { OpenWorld, PublicInterior, Unresolved, MissionInterior, InstanceInterior, Instance, Encounter }

        // Public guards/hosts named as such in the captured population. Vendors, collectors and mission
        // contacts have their own sources. A generic interior sighting is not a permanent city spawn.
        private static readonly HashSet<string> PublicInteriorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Club Guard", "Club Tough", "Club Sphinx", "Succubus", "Blood Noble", "Lupine Lieutenant",
            "Merovingian", "The Merovingian", "Ammit", "Scarlett", "Watchman", "Alex",
            // The public Function Data population near Azuna and the second Downtown site. The indoor
            // nav classification alone does not make this collector-loot content a mission instance.
            "Override Function"
        };

        public static Scope Classify(ushort district, string name, bool? indoors, bool instanceModel = false)
        {
            // These families are target/encounter arrivals, not fixed street populations. The 4 m
            // starts noted in retail captures are evidence for the family, not a claim about every row.
            if (string.Equals(name, "Corrupted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Special Agent", StringComparison.OrdinalIgnoreCase)) return Scope.Encounter;
            if (district == 0 || district > 3) return Scope.Instance;
            if (instanceModel && !PublicInteriorNames.Contains(name)) return Scope.InstanceInterior;
            if (!indoors.HasValue) return Scope.Unresolved;
            if (!indoors.Value) return Scope.OpenWorld;
            return PublicInteriorNames.Contains(name) ? Scope.PublicInterior : Scope.MissionInterior;
        }

        public static bool LoadAmbient(Scope scope) => scope == Scope.OpenWorld || scope == Scope.PublicInterior ||
            scope == Scope.Unresolved || scope == Scope.Instance;
    }
}
