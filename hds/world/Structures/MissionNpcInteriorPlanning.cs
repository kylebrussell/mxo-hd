using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using G = hds.MissionOpenFloorResolver;
using E = hds.MissionInteriorEvidence;

namespace hds
{
    internal interface IMissionNpcInteriorEvidenceProvider
    {
        E Evidence(ushort world, ushort sector);
        StaticWorldObject[] Markers(ushort world, ushort sector);
        StaticWorldObject Door(ushort world, uint key);
        MissionOpenFloorEntry.Result Entry(ushort world, G.Position door, double centreX, double centreZ, string evidence);
    }
    internal sealed class MissionNpcInteriorEvidenceProvider : IMissionNpcInteriorEvidenceProvider
    {
        internal static readonly MissionNpcInteriorEvidenceProvider Instance = new MissionNpcInteriorEvidenceProvider();
        public E Evidence(ushort world, ushort sector) => E.LoadAudited(DataLoader.DataPath("mission_interior_evidence", world.ToString(System.Globalization.CultureInfo.InvariantCulture)), world, sector);
        public StaticWorldObject[] Markers(ushort world, ushort sector) => DataLoader.getInstance().WorldObjectsDB.Where(o => o.metrId == world && o.sectorID == sector && o.type != null && o.type.Length >= 2 && MissionSites.Goid(o) == 19).ToArray();
        public StaticWorldObject Door(ushort world, uint key) => DataLoader.getInstance().getObjectValues(key, world);
        public MissionOpenFloorEntry.Result Entry(ushort world, G.Position door, double centreX, double centreZ, string evidence) => MissionOpenFloorEntry.Calculate(world, door, centreX, centreZ, evidence);
    }
    public static partial class MissionStaging
    {
        // Pure view of the SAME template predicate used by Stage; no spawn/publication call.
        internal static bool NpcInteriorEnemyTemplate(string enemy) => Enemies.ContainsKey(enemy ?? "");
    }
    internal sealed class MissionNpcInteriorBefore
    {
        private readonly ClientData owner;
        private readonly MissionProgress before, target;
        private readonly Mission active;
        private readonly MissionSite beforeSite, targetSite;
        private readonly MissionNpcInteriorPending beforePending, targetPending;
        private readonly int beforeSites, targetSites;
        private readonly int beforePhase, targetCurrentPhase, world, life, mission, phase;
        private readonly uint character, district;
        private readonly long transport, started;
        private readonly string identity;
        internal MissionNpcInteriorBefore(WorldClient client, MissionProgress target, int phase)
        {
            owner = client.playerData; before = owner.missionProgress; active = owner.activeMission;
            this.target = target; beforePhase = before == null ? -1 : before.phaseIndex; targetCurrentPhase = target.phaseIndex;
            beforeSite = before?.site; targetSite = target.site; beforePending = before?.npcInteriorPending; targetPending = target.npcInteriorPending;
            beforeSites = before == null ? -1 : before.sites.Count; targetSites = target.sites.Count;
            world = owner.worldGeneration; life = owner.lifeGeneration; character = owner.getCharID(); district = owner.getDistrictId();
            transport = client.TransportFence.Capture(); mission = target.mission.id; started = target.startedAt; identity = target.persistenceId; this.phase = phase;
        }
        // Observation only. The future caller still owns missionSync and final transaction/publication checks.
        internal bool Current(WorldClient client,MissionNpcInteriorPending installed=null) => client != null && ReferenceEquals(client.playerData, owner) && client.Alive && owner.getOnWorld() &&
            !owner.isDead && !owner.worldChangePending && !owner.waitForRPCShutDown && (before == null || before.restoreRefusal == null) &&
            owner.getCharID() == character && owner.getDistrictId() == district && district == target.mission.district && owner.worldGeneration == world && owner.lifeGeneration == life &&
            client.TransportFence.IsCurrent(transport) && ReferenceEquals(owner.missionProgress, before) && ReferenceEquals(owner.activeMission, active) &&
            (before == null || before.phaseIndex == beforePhase) && target.phaseIndex == targetCurrentPhase && target.mission.id == mission &&
            target.persistenceId == identity && target.startedAt == started && phase >= 0 && phase < target.mission.phases.Count &&
            (before == null || ReferenceEquals(before.site, beforeSite) && (ReferenceEquals(before.npcInteriorPending, beforePending)||installed!=null&&ReferenceEquals(before,target)&&ReferenceEquals(before.npcInteriorPending,installed)) && before.sites.Count == beforeSites) &&
            ReferenceEquals(target.site, targetSite) && (ReferenceEquals(target.npcInteriorPending, targetPending)||installed!=null&&ReferenceEquals(target.npcInteriorPending,installed)) && target.sites.Count == targetSites &&
            phase == (ReferenceEquals(before, target) ? beforePhase + 1 : 0);
    }
    internal static class MissionNpcInteriorPlanning
    {
        internal const string Kind = "NpcInterior";
        internal sealed class Role
        {
            internal readonly string Kind; internal readonly int Id, Index; internal readonly byte WirePhase; internal readonly bool Deferred;
            internal Role(string kind, int id, int index, byte wirePhase, bool deferred) { Kind = kind; Id = id; Index = index; WirePhase = wirePhase; Deferred = deferred; }
            internal bool Matches(MissionNpcInteriorActor actor) => actor != null && actor.Kind == Kind && actor.Id == Id && actor.Index == Index && actor.WirePhase == WirePhase && actor.Deferred == Deferred;
        }
        internal sealed class Plan
        {
            internal readonly MissionNpcInteriorPending Pending;
            private readonly MissionNpcInteriorBefore before;
            private readonly MissionSite site;
            private readonly Mission mission;
            internal Plan(MissionNpcInteriorPending pending, MissionNpcInteriorBefore before, MissionSite site, Mission mission) { Pending = pending; this.before = before; this.site = site; this.mission = mission; }
            internal bool BeforeCurrent(WorldClient client) => before.Current(client) && Pending.Selection.Site.Fingerprint == MissionOpenFloorSite.CatalogFingerprint(site) &&
                Roles(mission, Pending.Selection.Phase, out Role[] roles, out _) && roles.Length == Pending.Selection.Actors.Length && roles.Select((r, i) => r.Matches(Pending.Selection.Actors[i])).All(v => v);
        }
        internal static bool Roles(Mission mission, int phaseIndex, out Role[] roles, out string refusal)
        {
            roles = null; refusal = "NotNpcOnlyGeneratedPhase";
            if (mission == null || phaseIndex < 0 || phaseIndex >= mission.phases.Count) return false;
            MissionPhase phase = mission.phases[phaseIndex];
            if (phase.site == null || phase.objects.Count != 0) return false;
            List<Role> selected = new List<Role>();
            foreach (MissionNpc npc in mission.npcs.Where(n => n.at == phaseIndex + 1))
            {
                if (npc.arrival == null || npc.arrival.at != null) { refusal = "UnsupportedNpcRelativeArrival"; return false; }
                selected.Add(new Role("Npc", npc.id, 0, npc.phase, npc.arrival.Late));
            }
            foreach (MissionWave wave in phase.Waves())
            {
                if (wave.count <= 0 || !MissionStaging.NpcInteriorEnemyTemplate(wave.enemy)) continue;
                if (wave.arrival == null || wave.arrival.at != null) { refusal = "UnsupportedNpcWaveRelativeArrival"; return false; }
                for (int i = 0; i < wave.count; i++) selected.Add(new Role("Wave", wave.id, i, 0, wave.arrival.Late));
            }
            if (selected.Count == 0) { refusal = "MissingNpcSiteRole"; return false; }
            if (selected.Select(r => r.Kind + ":" + r.Id + ":" + r.Index).Distinct().Count() != selected.Count) { refusal = "DuplicateNpcSiteRole"; return false; }
            roles = selected.ToArray(); refusal = null; return true;
        }
        internal static bool RawShape(StaticWorldObject raw, ushort world, ushort sector, ushort goid) => raw != null && MissionOpenFloorAdmission.ValidSpot(raw) && !raw.exterior &&
            raw.metrId == world && raw.sectorID == sector && raw.staticId <= 0xfffff && raw.mxoStaticId == ((uint)sector << 20 | raw.staticId) &&
            (raw.type.Length == 2 || raw.type[2] == 0 && raw.type[3] == 0) && MissionSites.Goid(raw) == goid && raw.quat.Length == 32 && raw.quat.All(Uri.IsHexDigit) &&
            Enumerable.Range(0, 4).All(i => float.IsFinite(BitConverter.ToSingle(Convert.FromHexString(raw.quat), i * 4)));
        internal static string RawJson(StaticWorldObject raw) => JsonSerializer.Serialize(raw, MissionProgress.SaveJson);
        internal static StaticWorldObject ReadRaw(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.EnumerateObject().Select(p => p.Name).Distinct().Count() != doc.RootElement.EnumerateObject().Count())
                throw new InvalidDataException("InvalidOrDuplicateNpcRawFields");
            return JsonSerializer.Deserialize<StaticWorldObject>(json, MissionProgress.SaveJson);
        }
        private static string MarkerIdentity(StaticWorldObject raw) => JsonSerializer.Serialize(new { raw.metrId, raw.sectorID, raw.mxoStaticId, raw.staticId, raw.pos_x, raw.pos_y, raw.pos_z, raw.rot, raw.quat });
        internal static bool Select(MissionProgress progress, int phase, MissionSite site, StaticWorldObject[] markers, out MissionNpcInteriorSelection selection, out string refusal)
        {
            selection = null;
            if (!Roles(progress?.mission, phase, out Role[] roles, out refusal)) return false;
            if (!MissionOpenFloorAdmission.ValidSite(site) || markers == null) { refusal = "InvalidNpcSite"; return false; }
            if (site.npcSpots.Any(s => s == null || s.Length != 4 || s.Any(v => !double.IsFinite(v)))) { refusal = "InvalidNpcSiteSpotShape"; return false; }
            double[][] spots = site.npcSpots.OrderByDescending(s => (s[0] - site.elevatorDoor.pos_x) * (s[0] - site.elevatorDoor.pos_x) + (s[2] - site.elevatorDoor.pos_z) * (s[2] - site.elevatorDoor.pos_z)).ToArray();
            if (roles.Length > spots.Length) { refusal = "UncertifiedNpcSyntheticLap"; return false; }
            List<MissionNpcInteriorActor> actors = new List<MissionNpcInteriorActor>();
            for (int i = 0; i < roles.Length; i++)
            {
                double[] spot = spots[i];
                if (spot == null || spot.Length != 4 || spot.Any(v => !double.IsFinite(v))) { refusal = "InvalidPlannedNpcStanding"; return false; }
                StaticWorldObject[] found = markers.Where(raw => RawShape(raw, site.metrId, site.elevatorDoor.sectorID, 19) && raw.pos_x == spot[0] && raw.pos_y - 50 == spot[1] && raw.pos_z == spot[2] && raw.rot == spot[3])
                    .GroupBy(MarkerIdentity).Select(g => g.First()).ToArray();
                if (found.Length != 1) { refusal = "MissingOrAmbiguousPlannedNpcMarker"; return false; }
                Role role = roles[i]; actors.Add(new MissionNpcInteriorActor(role.Kind, role.Id, role.Index, role.WirePhase, role.Deferred, RawJson(found[0]), spot));
            }
            selection = new MissionNpcInteriorSelection(progress.mission.id, progress.persistenceId, progress.startedAt, phase, MissionOpenFloorSite.From(site), actors.ToArray()); refusal = null; return true;
        }
        private static MissionNpcInteriorEntry Entry(StaticWorldObject raw, E index, E.Entry entry) => new MissionNpcInteriorEntry(RawJson(raw), index.Digest, entry.Door.Quaternion,
            new MissionNpcInteriorNode(entry.Node.Region, entry.Node.Polygon), new double[] { entry.Door.Position.X, entry.Door.Position.Y, entry.Door.Position.Z }, new double[] { entry.Standing.X, entry.Standing.Y, entry.Standing.Z });
        private static bool RefuseBefore(MissionNpcInteriorBefore before, WorldClient client, ref MissionNpcInteriorPending selected, ref string refusal)
        {
            if (!before.Current(client)) { selected = null; refusal = "StaleNpcBeforeState"; }
            return false;
        }
        internal static bool Prepare(WorldClient client, MissionProgress target, int phase, MissionSite selectedSite, IMissionNpcInteriorEvidenceProvider provider,
            out Plan plan, out MissionNpcInteriorPending selected, out string refusal)
        {
            plan = null; selected = null; refusal = "StaleNpcBeforeState";
            if (client?.playerData == null || client.TransportFence == null || target == null) return false;
            if (provider == null || !MissionOpenFloorAdmission.ValidSite(selectedSite)) { refusal = "InvalidNpcSiteProvider"; return false; }
            MissionNpcInteriorBefore before = new MissionNpcInteriorBefore(client, target, phase);
            if (!before.Current(client)) return false;
            if (target.npcInteriorPending != null) { selected = target.npcInteriorPending; refusal = selected.Refusal ?? "CommittedNpcPendingRequiresExplicitReplay"; return false; }
            try
            {
                StaticWorldObject[] rows = provider.Markers(selectedSite.metrId, selectedSite.elevatorDoor.sectorID);
                if (rows == null) throw new InvalidDataException("MissingNpcMarkerRows");
                StaticWorldObject[] markers = rows.Select(raw => ReadRaw(RawJson(raw))).ToArray();
                if (!Select(target, phase, selectedSite, markers, out MissionNpcInteriorSelection selection, out refusal)) return false;
                selected = new MissionNpcInteriorPending(2, Kind, selection, null, "NpcEvidenceNotYetValidated");
                E floor = provider.Evidence(selectedSite.metrId, selectedSite.elevatorDoor.sectorID), lobby = provider.Evidence(selectedSite.metrId, selectedSite.lobbyDoor.sectorID);
                if (!floor.TryEntry(selectedSite.elevatorDoor, p => provider.Entry(selectedSite.metrId, p, selectedSite.CentreX, selectedSite.CentreZ, "original NPC floor entry"), out E.Entry floorEntry, out refusal) ||
                    !lobby.TryEntry(selectedSite.lobbyDoor, p => provider.Entry(selectedSite.metrId, p, selectedSite.CentreX, selectedSite.CentreZ, "independent original NPC lobby entry"), out E.Entry lobbyEntry, out refusal))
                { selected = selected.WithRefusal(refusal); return RefuseBefore(before, client, ref selected, ref refusal); }
                List<MissionNpcInteriorActorProof> proofs = new List<MissionNpcInteriorActorProof>(); Dictionary<uint, MissionNpcInteriorDoor> doors = new Dictionary<uint, MissionNpcInteriorDoor>();
                foreach (MissionNpcInteriorActor actor in selection.Actors)
                {
                    Dictionary<uint, StaticWorldObject> rawDoors = new Dictionary<uint, StaticWorldObject>();
                    StaticWorldObject Lookup(uint key) { StaticWorldObject raw = provider.Door(selectedSite.metrId, key); if (raw == null) return null; raw = ReadRaw(RawJson(raw)); rawDoors[key] = raw; return raw; }
                    if (!floor.TryNpcRoute(floorEntry, ReadRaw(actor.RawMarkerJson), actor.Standing, Lookup, out E.NpcRoute route, out refusal)) { selected = selected.WithRefusal(refusal); return RefuseBefore(before, client, ref selected, ref refusal); }
                    proofs.Add(new MissionNpcInteriorActorProof(actor.Key, new MissionNpcInteriorNode(route.Target.Region, route.Target.Polygon), route.Nodes.Select(n => new MissionNpcInteriorNode(n.Region, n.Polygon)).ToArray()));
                    foreach (E.DoorPose door in route.RequiredDoors)
                    {
                        MissionNpcInteriorDoor proof = new MissionNpcInteriorDoor(door.Key, door.Goid, RawJson(rawDoors[door.Key]), door.Quaternion, new double[] { door.Position.X, door.Position.Y, door.Position.Z });
                        if (doors.TryGetValue(door.Key, out MissionNpcInteriorDoor old) && JsonSerializer.Serialize(old) != JsonSerializer.Serialize(proof)) { refusal = "ConflictingPlannedNpcDoor"; selected = selected.WithRefusal(refusal); return RefuseBefore(before, client, ref selected, ref refusal); }
                        doors[door.Key] = proof;
                    }
                }
                MissionNpcInteriorWitness witness = new MissionNpcInteriorWitness(2, Kind, selection, Entry(selectedSite.elevatorDoor, floor, floorEntry), Entry(selectedSite.lobbyDoor, lobby, lobbyEntry), proofs.ToArray(), doors.OrderBy(d => d.Key).Select(d => d.Value).ToArray());
                selected = new MissionNpcInteriorPending(2, Kind, selection, witness, null); Plan prepared = new Plan(selected, before, selectedSite, target.mission);
                if (!prepared.BeforeCurrent(client)) { selected = null; refusal = "StaleNpcBeforeStateOrSelection"; return false; }
                plan = prepared; refusal = null; return true;
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            { refusal = "NpcEvidenceUnavailable:" + ex.GetType().Name; if (selected != null) selected = selected.WithRefusal(refusal); return RefuseBefore(before, client, ref selected, ref refusal); }
        }
    }
}
