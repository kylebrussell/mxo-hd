using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    internal static class MissionPlacement
    {
        internal static bool Resolve(ushort district, double[] desired, double[] player, IEnumerable<double[]> npcSpots,
            bool besidePlayer, out NavPoint point, out bool siteFallback)
        {
            siteFallback = false;
            if (NavMesh.Project(district, desired[0], desired[1], desired[2], out point,
                reachableFrom: player, requireDirect: besidePlayer)) return true;
            foreach (double[] fallback in npcSpots.OrderBy(s => Distance(s, desired)))
            {
                if (NavMesh.Project(district, fallback[0], fallback[1], fallback[2], out point,
                    maxDistance: 400, reachableFrom: player))
                {
                    siteFallback = true;
                    return true;
                }
            }
            // Staying on the player's known floor is preferable to inventing an unwalkable offset.
            if (player != null && Math.Abs(desired[1] - player[1]) <= NavMesh.MaxFloorDistance &&
                NavMesh.Locate(district, player[0], player[1], player[2], out NavLocation at))
            {
                point = new NavPoint { x = player[0], y = at.floorY + 95, z = player[2], location = at };
                return true;
            }
            return false;
        }

        internal static bool ForSpawn(WorldClient client, double x, double y, double z, bool besidePlayer, out NavPoint point)
        {
            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            MissionProgress progress = client.playerData.missionProgress;
            MissionSite site = progress?.site;
            ushort district = (ushort)(progress?.mission.district ?? client.playerData.getDistrictId());
            double[] player = district == client.playerData.getDistrictId() ? new[] { px, py, pz } : null;
            bool placed = Resolve(district, new[] { x, y, z }, player,
                site?.npcSpots ?? Enumerable.Empty<double[]>(), besidePlayer, out point, out bool fallback);
            if (fallback) Output.WriteDebugLog("[MISSION] Arrival uses a walkable site NPC spot");
            if (!placed) Output.WriteDebugLog("[MISSION] No walkable arrival position at " + x + "," + y + "," + z);
            return placed;
        }

        private static double Distance(double[] a, double[] b) =>
            (a[0] - b[0]) * (a[0] - b[0]) + (a[2] - b[2]) * (a[2] - b[2]);
    }
}
