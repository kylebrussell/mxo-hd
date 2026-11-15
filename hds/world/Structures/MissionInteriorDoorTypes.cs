using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace hds
{
    /// <summary>The ordinary class364 door family, independent of elevator or scene-pose authority.</summary>
    public static class MissionInteriorDoorTypes
    {
        // Read the existing generated class catalog once, without constructing its object instances.
        // Original GOB class364 supplies the common Open rule4004 and 0834/0838/0835 state machine.
        private static readonly ushort[] catalog = typeof(GameObjectDefinitions).GetFields(BindingFlags.Public|BindingFlags.Instance)
            .Where(f=>f.FieldType==typeof(AttributeClass364)&&f.Name.StartsWith("Object",StringComparison.Ordinal))
            .Select(f=>ushort.Parse(f.Name.AsSpan(6),NumberStyles.None,CultureInfo.InvariantCulture)).OrderBy(id=>id).ToArray();
        private static readonly HashSet<ushort> supported = new HashSet<ushort>(catalog);
        private static readonly IReadOnlyList<ushort> published = Array.AsReadOnly(catalog);
        public static IReadOnlyList<ushort> SupportedGoids => published;
        public static bool IsSupported(ushort goid) => supported.Contains(goid);
    }
}
