using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    public static partial class NavMesh
    {
        private const int PathSearchMargin = 1200;
        private const int PathSearchLimit = 384;
        private const double DirectWalkStep = 10;
        private const double DirectWalkStepHeight = 40;

        /// <summary>Trace a short walk over open polygon edges, at most 40 units up or down per sample.</summary>
        internal static bool WalkableLine(ushort district, NavLocation from, double x, double z, double tx, double tz,
            out NavLocation end)
        {
            end = from;
            int steps = (int)Math.Ceiling(Math.Sqrt((tx - x) * (tx - x) + (tz - z) * (tz - z)) / DirectWalkStep);
            for (int i = 1; i <= steps; i++)
            {
                double px = x + (tx - x) * i / steps, pz = z + (tz - z) * i / steps;
                if (!Locate(district, px, end.floorY + 95, pz, out NavLocation step) ||
                    Math.Abs(step.floorY - end.floorY) > DirectWalkStepHeight || !OpenBetween(district, end, step))
                {
                    return false;
                }
                end = step;
            }
            return true;
        }

        /// <summary>Find waypoints through walkable portals on the current floor near the pursuit.</summary>
        internal static NavPoint[] WalkingPath(ushort world, double[] from, double[] to)
        {
            if (!Locate(world, from[0], from[1], from[2], out var origin) ||
                !Locate(world, to[0], to[1], to[2], out var destination) ||
                Math.Abs(origin.floorY - destination.floorY) > 150)
                return Array.Empty<NavPoint>();

            double lowX = Math.Min(from[0], to[0]) - PathSearchMargin, highX = Math.Max(from[0], to[0]) + PathSearchMargin;
            double lowZ = Math.Min(from[2], to[2]) - PathSearchMargin, highZ = Math.Max(from[2], to[2]) + PathSearchMargin;
            var district = Load(world);
            var visitedSectors = new HashSet<ushort>();
            var candidates = new List<Surface>();
            for (int x = (int)Math.Floor(lowX / 1000); x <= (int)Math.Floor(highX / 1000); x++)
                for (int z = (int)Math.Floor(lowZ / 1000); z <= (int)Math.Floor(highZ / 1000); z++)
                {
                    var info = SectorProperties.At(world, x * 1000d + 500, z * 1000d + 500);
                    if (info != null && visitedSectors.Add(info.sector) && district.sectors.TryGetValue(info.sector, out var sector))
                        candidates.AddRange(Surfaces(info, sector, district).Where(s =>
                            s.maxX >= lowX && s.minX <= highX && s.maxZ >= lowZ && s.minZ <= highZ &&
                            s.maxY >= origin.floorY - 150 && s.minY <= origin.floorY + 150));
                }
            return WalkingPath(candidates, origin, destination, to);
        }

        internal static NavPoint[] WalkingPath(IReadOnlyList<Surface> surfaces, NavLocation origin, NavLocation destination,
            double[] to)
        {
            bool Matches(Surface surface, NavLocation location) =>
                surface.location.sector == location.sector && surface.location.region == location.region &&
                surface.location.polygon == location.polygon;
            var start = surfaces.FirstOrDefault(s => Matches(s, origin));
            var goal = surfaces.FirstOrDefault(s => Matches(s, destination));
            if (start == null || goal == null) return Array.Empty<NavPoint>();

            var entries = new Dictionary<Surface, (Surface Parent, NavPoint Portal, double Cost)>
                { [start] = (null, default, 0) };
            var known = new HashSet<Surface>();
            var open = new PriorityQueue<Surface, double>();
            open.Enqueue(start, 0);
            while (open.TryDequeue(out var current, out _) && known.Count < PathSearchLimit)
            {
                if (!known.Add(current)) continue;
                if (current == goal)
                {
                    var path = new List<NavPoint> { new NavPoint { x = to[0], y = destination.floorY + 95, z = to[2], location = destination } };
                    for (var node = goal; node != start; node = entries[node].Parent)
                        path.Add(entries[node].Portal);
                    path.Reverse();
                    return path.ToArray();
                }

                foreach (var next in surfaces)
                {
                    if (known.Contains(next) || !current.TryPortal(next, out double x, out double z)) continue;
                    double cost = entries[current].Cost + CentreDistance(current, next);
                    if (entries.TryGetValue(next, out var prior) && prior.Cost <= cost) continue;
                    // Follow the seam from the current polygon, then ground the next movement tick normally.
                    var at = current.location;
                    at.floorY = current.Floor(x, z);
                    if (Math.Abs(at.floorY - origin.floorY) > 150 || Math.Abs(at.floorY - next.Floor(x, z)) > StepHeight)
                        continue;
                    entries[next] = (current, new NavPoint { x = x, y = at.floorY + 95, z = z, location = at }, cost);
                    open.Enqueue(next, cost + CentreDistance(next, goal));
                }
            }
            return Array.Empty<NavPoint>();
        }

        private static double CentreDistance(Surface a, Surface b) =>
            Math.Sqrt(Square(a.centreX - b.centreX) + Square(a.centreZ - b.centreZ));
    }
}
