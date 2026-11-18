using System;
using System.Collections.Generic;
using System.Linq;
using Scene = hds.MissionOpenFloorResolver;

namespace hds
{
    internal static class MissionPhonePlacement
    {
        // Stock Blinking_Desk_Phone Model6 uses blk_speakerphone.prop (04000086).
        // Its mesh DIMS put the pivot 0.140896 units above the base, not at the centre.
        private const double HalfX = 17.9379978179932, HalfZ = 14.5889005661011;
        private const double BaseOffset = 0.140896, ModelHeight = BaseOffset + 8.809072;
        private static readonly double Radius = Math.Sqrt(HalfX * HalfX + HalfZ * HalfZ);
        private const double MaximumClutterHeight = 100, MaximumClutterWidth = 200;

        internal static MissionComputerAnchors.Pose At(StaticWorldObject raw, MissionComputerAnchors.Pose source,
            double top, MissionBoundFurnitureRange.Bounds support)
        {
            Scene.Bounds? area = support == null ? (Scene.Bounds?)null
                : new Scene.Bounds(support.Minimum[0], support.Maximum[0], support.Minimum[2], support.Maximum[2]);
            if (MissionOpenFloorScenes.TryLoad(raw.metrId, raw.sectorID, out var scene, out _) &&
                TryClearSurface(scene, raw.mxoStaticId, source, top, area, out var pose))
                return pose;

            return OnSurface(source.X, top, source.Z, source);
        }

        internal static bool TryClearSurface(Scene.StaticScene scene, uint sourceKey, MissionComputerAnchors.Pose source,
            double top, Scene.Bounds? support, out MissionComputerAnchors.Pose pose)
        {
            double reach = MissionRuntime.ObjectReachUnits;
            var nearby = scene.Obstacles.Where(obj => obj.MinY < top + MaximumClutterHeight + ModelHeight
                && obj.MaxY > top && obj.Key != sourceKey)
                .Select(obj => (Object: obj, Area: new Scene.Bounds(obj.Footprint.Min(p => p.X), obj.Footprint.Max(p => p.X),
                    obj.Footprint.Min(p => p.Z), obj.Footprint.Max(p => p.Z))))
                .Where(obj => obj.Area.MinX < source.X + reach && obj.Area.MaxX > source.X - reach
                    && obj.Area.MinZ < source.Z + reach && obj.Area.MaxZ > source.Z - reach).ToArray();

            bool Clear(double x, double y, double z)
            {
                double dx = x - source.X, dz = z - source.Z;
                return dx * dx + dz * dz <= reach * reach && Fits(scene.Coverage, x, z)
                    && !nearby.Any(obj => obj.Object.MaxY > y && obj.Object.MinY < y + ModelHeight
                        && obj.Area.MinX < x + Radius && obj.Area.MaxX > x - Radius
                        && obj.Area.MinZ < z + Radius && obj.Area.MaxZ > z - Radius);
            }

            if (Clear(source.X, top, source.Z))
            {
                pose = OnSurface(source.X, top, source.Z, source);
                return true;
            }

            if (support is Scene.Bounds area)
            {
                var obstructions = nearby.Where(obj => obj.Object.MinY < top + ModelHeight).Select(obj => obj.Area).ToArray();
                foreach (var point in SurfacePoints(area, source, obstructions))
                {
                    if (!Clear(point.X, top, point.Z)) continue;
                    pose = OnSurface(point.X, top, point.Z, source);
                    return true;
                }
            }

            // A small prop which rests on the furniture can itself support the phone. Reject walls and any
            // raised candidate whose body would touch a different obstacle, such as a shelf above the prop.
            foreach (var obj in nearby.Where(obj => !obj.Object.Key.HasValue && obj.Object.MinY <= top
                && obj.Object.MaxY <= top + MaximumClutterHeight
                && obj.Area.MaxX - obj.Area.MinX <= MaximumClutterWidth
                && obj.Area.MaxZ - obj.Area.MinZ <= MaximumClutterWidth
                && Fits(obj.Area, source.X, source.Z)).OrderBy(obj => obj.Object.MaxY))
            {
                double raisedTop = obj.Object.MaxY;
                if (!Clear(source.X, raisedTop, source.Z)) continue;
                pose = OnSurface(source.X, raisedTop, source.Z, source);
                return true;
            }

            pose = null;
            return false;
        }

        private static MissionComputerAnchors.Pose OnSurface(double x, double top, double z, MissionComputerAnchors.Pose source)
            => new MissionComputerAnchors.Pose(x, top + BaseOffset, z, source.Orientation);

        private static bool Fits(Scene.Bounds area, double x, double z)
            => x - Radius >= area.MinX && x + Radius <= area.MaxX && z - Radius >= area.MinZ && z + Radius <= area.MaxZ;

        private static IEnumerable<Scene.Point> SurfacePoints(Scene.Bounds area, MissionComputerAnchors.Pose source,
            IReadOnlyList<Scene.Bounds> obstructions)
        {
            double xMin = area.MinX + Radius, xMax = area.MaxX - Radius, zMin = area.MinZ + Radius, zMax = area.MaxZ - Radius;
            if (xMin > xMax || zMin > zMax) return Array.Empty<Scene.Point>();

            const double Clearance = 1;
            var xs = new[] { Math.Clamp(source.X, xMin, xMax), xMin, xMax }
                .Concat(obstructions.SelectMany(box => new[] { box.MinX - Radius - Clearance, box.MaxX + Radius + Clearance }))
                .Where(x => x >= xMin && x <= xMax).Distinct().ToArray();
            var zs = new[] { Math.Clamp(source.Z, zMin, zMax), zMin, zMax }
                .Concat(obstructions.SelectMany(box => new[] { box.MinZ - Radius - Clearance, box.MaxZ + Radius + Clearance }))
                .Where(z => z >= zMin && z <= zMax).Distinct().ToArray();
            return from x in xs
                   from z in zs
                   orderby (x - source.X) * (x - source.X) + (z - source.Z) * (z - source.Z)
                   select new Scene.Point(x, z);
        }
    }
}
