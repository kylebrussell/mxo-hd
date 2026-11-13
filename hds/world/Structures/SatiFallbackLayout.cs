using System;

namespace hds
{
    /// <summary>OURS: separate the Sati traveller and generated returner inside one sourced walkable polygon.
    /// This does not recover retail arrival coordinates or change the returner's native bounds/rules.</summary>
    public static class SatiFallbackLayout
    {
        public const ushort Map = 9;
        public const double ArrivalX = 14225, ArrivalY = 95, ArrivalZ = -5225;
        public const double ReturnerX = 14225, ReturnerY = 95, ReturnerZ = -4925;
        // Raw38271 Model/HalfExtents; used only for footprint validation, never serialized as an override.
        public const double HalfX = 25, HalfY = 95, HalfZ = 95;

        private static bool OnSourceFloor(NavPoint point) =>
            NavMesh.Locate(Map, point.x, point.y, point.z, out var at) &&
            at.sector == 2 && at.region == 3 && at.polygon == 6 && !at.indoors && Math.Abs(at.floorY) < .001;

        public static bool TryGet(out NavPoint arrival, out NavPoint returner)
        {
            returner = default;
            if (!NavMesh.Project(Map, ArrivalX, ArrivalY, ArrivalZ, out arrival, 0) || !OnSourceFloor(arrival) ||
                arrival.x != ArrivalX || arrival.y != ArrivalY || arrival.z != ArrivalZ) return false;
            if (!NavMesh.Project(Map, ReturnerX, ReturnerY, ReturnerZ, out returner, 0,
                    new[] { arrival.x, arrival.y, arrival.z }, requireDirect: true) || !OnSourceFloor(returner) ||
                returner.x != ReturnerX || returner.y != ReturnerY || returner.z != ReturnerZ) return false;
            if (Math.Abs(returner.y - HalfY - returner.location.floorY) > .001) return false;
            foreach (double x in new[] { returner.x - HalfX, returner.x + HalfX })
                foreach (double z in new[] { returner.z - HalfZ, returner.z + HalfZ })
                    if (!OnSourceFloor(new NavPoint { x = x, y = returner.y, z = z })) return false;
            return true;
        }
    }
}
