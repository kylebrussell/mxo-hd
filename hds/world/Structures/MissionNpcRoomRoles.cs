using System;
using System.Collections.Generic;
using System.Linq;
using E = hds.MissionInteriorEvidence;
namespace hds
{
    // Fresh allocation only. Persisted selections are exact authority, not a request to allocate again.
    internal static class MissionNpcRoomRoles
    {
        internal sealed class Candidate
        {
            internal readonly StaticWorldObject Marker;
            internal readonly double[] Standing;
            internal readonly E.NpcRoute Route;
            internal readonly int Order;
            internal Candidate(StaticWorldObject marker, double[] standing, E.NpcRoute route, int order)
            { Marker = marker; Standing = (double[])standing.Clone(); Route = route; Order = order; }
        }
        private static string PositionKey(double[] point) => string.Join("|", point.Take(3).Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        internal static Candidate[] Allocate(IEnumerable<Candidate> candidates, int count)
        {
            var unique = candidates.GroupBy(c => PositionKey(c.Standing)).Select(g => g.OrderBy(c => c.Order).First());
            var rooms = unique.GroupBy(c => c.Route.Target.Region).OrderBy(g => g.Min(c => c.Route.Nodes.Length)).ThenBy(g => g.Key).ToArray();
            var complete = rooms.FirstOrDefault(g => g.Count() >= count);
            return (complete != null ? complete.OrderBy(c => c.Order) : rooms.SelectMany(g => g.OrderBy(c => c.Order))).Take(count).ToArray();
        }
        internal static bool Select(MissionProgress p, int phase, MissionSite site, StaticWorldObject[] markers, E evidence, E.Entry entry,
            IMissionNpcInteriorEvidenceProvider provider, out MissionNpcInteriorSelection selection, out string refusal)
        {
            selection = null;
            if (!MissionNpcInteriorPlanning.Roles(p.mission, phase, out MissionNpcInteriorPlanning.Role[] roles, out refusal)) return false;
            var candidates = new List<Candidate>();
            var spots = site.npcSpots.OrderByDescending(s => (s[0]-site.elevatorDoor.pos_x)*(s[0]-site.elevatorDoor.pos_x)+(s[2]-site.elevatorDoor.pos_z)*(s[2]-site.elevatorDoor.pos_z)).ToArray();
            for (int i = 0; i < spots.Length; i++)
            {
                double[] spot = spots[i];
                
                var matching = markers.Where(m => MissionNpcInteriorPlanning.RawShape(m, site.metrId, site.elevatorDoor.sectorID, 19) &&
                    m.pos_x == spot[0] && m.pos_y-50 == spot[1] && m.pos_z == spot[2] && m.rot == spot[3])
                    .GroupBy(m => MissionNpcInteriorPlanning.RawJson(m)).Select(g => g.First()).ToArray();
                if (matching.Length != 1) continue;
                if (evidence.TryMeasuredMarker(matching[0], out double[] measured, out _) &&
                    evidence.TryMeasuredNpcRoute(entry, matching[0], measured, key => provider.Door(site.metrId, key), out E.NpcRoute route, out _))
                    candidates.Add(new Candidate(matching[0], measured, route, i));
            }
            Candidate[] chosen = Allocate(candidates, roles.Length);
            if (chosen.Length != roles.Length) { refusal = "InsufficientCertifiedNpcRoomSpots"; return false; }
            var actors = roles.Select((r,i) => new MissionNpcInteriorActor(r.Kind,r.Id,r.Index,r.WirePhase,r.Deferred,MissionNpcInteriorPlanning.RawJson(chosen[i].Marker),chosen[i].Standing)).ToArray();
            selection = new MissionNpcInteriorSelection(p.mission.id,p.persistenceId,p.startedAt,phase,MissionOpenFloorSite.From(site),actors,1); refusal = null; return true;
        }
        internal static bool ValidSelection(MissionProgress p, MissionSite site, MissionNpcInteriorSelection selection)
        {
            if (!MissionNpcInteriorPlanning.Roles(p.mission,selection.Phase,out MissionNpcInteriorPlanning.Role[] roles,out _) || roles.Length != selection.Actors.Length) return false;
            var used = new HashSet<string>();
            for(int i=0;i<roles.Length;i++)
            {
                var actor=selection.Actors[i]; if(!roles[i].Matches(actor))return false;
                var raw=MissionNpcInteriorPlanning.ReadRaw(actor.RawMarkerJson);
                if(!MissionNpcInteriorPlanning.RawShape(raw,site.metrId,site.elevatorDoor.sectorID,19) || actor.Standing.Length != 4 || actor.Standing[3] != raw.rot ||
                    !site.npcSpots.Any(s=>s.SequenceEqual(new double[]{raw.pos_x,raw.pos_y-50,raw.pos_z,raw.rot})) || !used.Add(PositionKey(actor.Standing)))return false;
            }
            return true;
        }
    }
}
