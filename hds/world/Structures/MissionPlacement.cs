using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    internal static class MissionPlacement
    {
        internal static bool Resolve(ushort district, double[] desired, double[] player, IEnumerable<double[]> npcSpots,
            bool besidePlayer, out NavPoint point, out bool siteFallback, IEnumerable<double[]> occupied = null, MissionSite site = null, Func<NavPoint,bool> admissible = null, Func<double[],double,NavPoint?> projector = null)
        {
            List<double[]> taken = occupied?.ToList() ?? new List<double[]>();
            bool Free(NavPoint at) => (site == null || (at.x >= site.minX && at.x <= site.maxX &&
                at.z >= site.minZ && at.z <= site.maxZ)) &&
                !taken.Any(p => Math.Abs(p[1] - at.y) < 150 && Distance(p, at.Position) < 100 * 100) && (admissible==null||admissible(at));
            bool Project(double[] target,double radius,out NavPoint placed)
            {
                if(projector==null)return NavMesh.Project(district,target[0],target[1],target[2],out placed,
                    maxDistance:radius,reachableFrom:player,requireDirect:besidePlayer);
                var projected=projector(target,radius);placed=projected??default;return projected.HasValue;
            }
            bool TryAt(double[] target, double radius, out NavPoint placed)
            {
                if(!Project(target,radius,out placed))return false;
                if (Free(placed)) return true;
                // Projection can collapse several offsets onto the same wall edge. Reserve actual projected
                // positions, including earlier members of a wave, and look locally before trying another site spot.
                NavPoint centre = placed;
                foreach (int spread in new[] { 125, 250, 375 })
                    foreach (double[] candidate in Enumerable.Range(0, 16).Select(i => new[]
                        { centre.x + spread * Math.Cos(i * Math.PI / 8), centre.y, centre.z + spread * Math.Sin(i * Math.PI / 8) })
                        .OrderBy(p => Distance(p, target)))
                        if ((projector!=null?Project(candidate,50,out placed):NavMesh.Project(district,candidate[0],candidate[1],candidate[2],out placed,
                            maxDistance:50,reachableFrom:player??centre.Position,requireDirect:besidePlayer)) && Free(placed)) return true;
                return false;
            }
            siteFallback = false;
            if (TryAt(desired, 800, out point)) return true;
            foreach (double[] fallback in npcSpots.OrderBy(s => Distance(s, desired)))
            {
                if (TryAt(fallback, 400, out point))
                {
                    siteFallback = true;
                    return true;
                }
            }
            // Staying on the player's known floor is preferable to inventing an unwalkable offset.
            if (player != null && Math.Abs(desired[1] - player[1]) <= NavMesh.MaxFloorDistance && TryAt(player, 400, out point))
            {
                return true;
            }
            return false;
        }

        internal static bool ForSpawn(WorldClient client, double x, double y, double z, bool besidePlayer, out NavPoint point,
            IEnumerable<NavPoint> reserved = null, ushort replacingNpc = 0)
        {
            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            MissionProgress progress = client.playerData.missionProgress;
            MissionSite site = progress?.site;
            ushort district = (ushort)(progress?.mission.district ?? client.playerData.getDistrictId());
            double[] player = district == client.playerData.getDistrictId() ? new[] { px, py, pz } : null;
            player = ReferenceFor(district, site, player, besidePlayer);
            List<double[]> occupied;
            lock (client.playerData.missionMobs)
                occupied = client.playerData.missionMobs.Where(m => !m.getIsDead() && m.getDistrict() == district &&
                    (replacingNpc == 0 || m.missionNpcId != replacingNpc))
                    .Select(m => new[] { m.getXPos(), m.getYPos(), m.getZPos() }).ToList();
            if (reserved != null) occupied.AddRange(reserved.Select(p => p.Position));
            bool placed = Resolve(district, new[] { x, y, z }, player,
                site?.npcSpots ?? Enumerable.Empty<double[]>(), besidePlayer, out point, out bool fallback, occupied,
                besidePlayer ? null : site,
                !besidePlayer&&site!=null&&MissionOpenFloorAdmission.Requires(progress.Phase)?new Func<NavPoint,bool>(at=>MissionInteriorSites.Contains(site,at)):null,
                !besidePlayer&&site!=null&&MissionOpenFloorAdmission.Requires(progress.Phase)?new Func<double[],double,NavPoint?>((target,radius)=>MissionInteriorSites.Project(site,target,radius)):null);
            if (fallback) Output.WriteDebugLog("[MISSION] Arrival uses a walkable site NPC spot");
            if (!placed) Output.WriteDebugLog("[MISSION] No walkable arrival position at " + x + "," + y + "," + z);
            return placed;
        }

        internal static double[] ReferenceFor(ushort district, MissionSite site, double[] player, bool besidePlayer)
        {
            if (site == null || besidePlayer || (player != null &&
                player[0] >= site.minX && player[0] <= site.maxX &&
                player[2] >= site.minZ && player[2] <= site.maxZ &&
                Math.Abs(player[1] - (site.floorY - 50)) <= NavMesh.MaxFloorDistance &&
                NavMesh.Locate(district, player[0], player[1], player[2], out _))) return player;
            // Before the player reaches the site, use the allocated floor's own component. Other parts of
            // a prefab or adjacent buildings may share its height but lie outside the mission area.
            return NavMesh.Project(district, site.CentreX, site.floorY - 50, site.CentreZ, out NavPoint anchor)
                ? anchor.Position : null;
        }

        private static double Distance(double[] a, double[] b) =>
            (a[0] - b[0]) * (a[0] - b[0]) + (a[2] - b[2]) * (a[2] - b[2]);
    }
}
