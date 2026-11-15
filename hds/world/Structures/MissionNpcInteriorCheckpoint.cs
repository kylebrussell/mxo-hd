using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using G = hds.MissionOpenFloorResolver;
using E = hds.MissionInteriorEvidence;

namespace hds
{
    public partial class MissionProgress
    {
        internal MissionNpcInteriorPending npcInteriorPending;
        internal int? npcInteriorResolvedPhase;
        internal readonly Dictionary<int, MissionNpcInteriorWitness> npcInteriorWitnesses = new Dictionary<int, MissionNpcInteriorWitness>();
        internal MissionNpcInteriorEnvelope CaptureNpcInterior() => npcInteriorPending == null && !npcInteriorResolvedPhase.HasValue && npcInteriorWitnesses.Count == 0 ? null :
            new MissionNpcInteriorEnvelope(2, MissionNpcInteriorPlanning.Kind, npcInteriorPending, npcInteriorResolvedPhase, npcInteriorWitnesses.OrderBy(w => w.Key).Select(w => w.Value).ToArray());
    }
    internal static class MissionNpcInteriorCheckpoint
    {
        private static void Require(bool ok, string why) { if (!ok) throw new InvalidDataException(why); }
        private static bool Vector(double[] a, int length) => a != null && a.Length == length && a.All(double.IsFinite);
        private static bool Hex(string value, int n) => value != null && value.Length == n && value.All(Uri.IsHexDigit);
        private static bool Quaternion(string value) => Hex(value, 32) && Enumerable.Range(0, 4).All(i => float.IsFinite(BitConverter.ToSingle(Convert.FromHexString(value), i * 4)));
        private static bool Same<T>(T a, T b) => JsonSerializer.Serialize(a, MissionProgress.SaveJson) == JsonSerializer.Serialize(b, MissionProgress.SaveJson);
        private static void Fields(JsonElement obj, params string[] names)
        {
            Require(obj.ValueKind == JsonValueKind.Object, "NpcMetadataObject");
            Require(obj.EnumerateObject().Select(p => p.Name).Distinct().Count() == obj.EnumerateObject().Count(), "NpcMetadataDuplicateField");
            Require(names.All(n => obj.TryGetProperty(n, out _)), "NpcMetadataMissingField");
        }
        private static void NodeShape(JsonElement e) => Fields(e, "Region", "Polygon");
        private static void SelectionShape(JsonElement e)
        {
            Fields(e, "Mission", "Identity", "Started", "Phase", "Site", "Actors"); Require(e.GetProperty("Actors").ValueKind == JsonValueKind.Array, "NpcActorArray");
            foreach (JsonElement actor in e.GetProperty("Actors").EnumerateArray()) Fields(actor, "Kind", "Id", "Index", "WirePhase", "Deferred", "RawMarkerJson", "Standing");
        }
        private static void WitnessShape(JsonElement e)
        {
            Fields(e, "Version", "Kind", "Selection", "Floor", "Lobby", "Actors", "Doors"); SelectionShape(e.GetProperty("Selection"));
            foreach (string name in new string[] { "Floor", "Lobby" }) { JsonElement entry = e.GetProperty(name); Fields(entry, "RawDoorJson", "EvidenceDigest", "Quaternion", "Node", "Position", "Standing"); NodeShape(entry.GetProperty("Node")); }
            Require(e.GetProperty("Actors").ValueKind == JsonValueKind.Array && e.GetProperty("Doors").ValueKind == JsonValueKind.Array, "NpcWitnessArrays");
            foreach (JsonElement actor in e.GetProperty("Actors").EnumerateArray())
            {
                Fields(actor, "Role", "Target", "Nodes"); NodeShape(actor.GetProperty("Target")); Require(actor.GetProperty("Nodes").ValueKind == JsonValueKind.Array, "NpcPathArray");
                foreach (JsonElement node in actor.GetProperty("Nodes").EnumerateArray()) NodeShape(node);
            }
            foreach (JsonElement door in e.GetProperty("Doors").EnumerateArray()) Fields(door, "Key", "Goid", "RawDoorJson", "Quaternion", "Position");
        }
        private static void EnvelopeShape(JsonElement e)
        {
            Fields(e, "Version", "Kind", "Pending", "CurrentPhase", "Witnesses"); Require(e.GetProperty("Witnesses").ValueKind == JsonValueKind.Array, "NpcWitnessHistoryArray");
            if (e.GetProperty("Pending").ValueKind != JsonValueKind.Null)
            {
                JsonElement pending = e.GetProperty("Pending"); Fields(pending, "Version", "Kind", "Selection", "Witness", "Refusal"); SelectionShape(pending.GetProperty("Selection"));
                if (pending.GetProperty("Witness").ValueKind != JsonValueKind.Null) WitnessShape(pending.GetProperty("Witness"));
            }
            foreach (JsonElement witness in e.GetProperty("Witnesses").EnumerateArray()) WitnessShape(witness);
        }
        private static MissionSite SelectionIdentity(MissionProgress p, MissionNpcInteriorSelection selection, IReadOnlyList<MissionSite> catalog)
        {
            Require(selection != null && selection.Mission == p.mission.id && selection.Identity == p.persistenceId && !string.IsNullOrWhiteSpace(selection.Identity) && selection.Started == p.startedAt && selection.Phase >= 0 && selection.Phase < p.mission.phases.Count && selection.Site != null && selection.Site.District == p.mission.district && selection.Actors != null, "NpcSelectionIdentity");
            MissionSite site = selection.Site.Resolve(catalog); Require(site != null, "NpcSelectionSiteIdentity");
            return site;
        }
        private static MissionSite Selection(MissionProgress p, MissionNpcInteriorSelection selection, IReadOnlyList<MissionSite> catalog)
        {
            MissionSite site=SelectionIdentity(p,selection,catalog);
            Require(selection.Actors.Length>0,"NpcSelectionActorPrefix");
            Require(selection.Actors.All(a => a != null && Vector(a.Standing, 4)), "NpcSelectedActorShape");
            StaticWorldObject[] markers = selection.Actors.Select(a => MissionNpcInteriorPlanning.ReadRaw(a.RawMarkerJson)).ToArray();
            Require(MissionNpcInteriorPlanning.Select(p, selection.Phase, site, markers, out MissionNpcInteriorSelection expected, out string refusal), refusal ?? "NpcSelectedRoles");
            Require(Same(expected, selection), "ChangedNpcPlannedPrefix"); return site;
        }
        private static void EntryShape(MissionNpcInteriorEntry entry, StaticWorldObject expected)
        {
            Require(entry != null && Vector(entry.Position, 3) && Vector(entry.Standing, 3) && Quaternion(entry.Quaternion) && Hex(entry.EvidenceDigest, 64), "NpcEntryShape");
            StaticWorldObject raw = MissionNpcInteriorPlanning.ReadRaw(entry.RawDoorJson);
            Require(raw != null && Same(raw, expected), "NpcEntryRawIdentity");
        }
        private static void Witness(MissionProgress p, MissionNpcInteriorWitness witness, IReadOnlyList<MissionSite> catalog)
        {
            Require(witness != null && witness.Version == 2 && witness.Kind == MissionNpcInteriorPlanning.Kind, "NpcWitnessVersionKind");
            MissionSite site = Selection(p, witness.Selection, catalog); EntryShape(witness.Floor, site.elevatorDoor); EntryShape(witness.Lobby, site.lobbyDoor);
            Require(witness.Actors != null && witness.Doors != null && witness.Actors.Length == witness.Selection.Actors.Length && witness.Actors.All(a => a != null && a.Nodes != null && a.Nodes.Length > 0), "NpcWitnessActorCapacity");
            Require(witness.Actors.Select(a => a.Role).SequenceEqual(witness.Selection.Actors.Select(a => a.Key)), "NpcWitnessRoleOrder");
            foreach (MissionNpcInteriorActorProof proof in witness.Actors) Require(proof.Nodes[0].Equals(witness.Floor.Node) && proof.Nodes[proof.Nodes.Length - 1].Equals(proof.Target), "NpcWitnessPathIdentity");
            Require(witness.Doors.All(d => d != null) && witness.Doors.Select(d => d.Key).Distinct().Count() == witness.Doors.Length, "NpcWitnessDoorIdentity");
            foreach (MissionNpcInteriorDoor door in witness.Doors)
            {
                StaticWorldObject raw = MissionNpcInteriorPlanning.ReadRaw(door.RawDoorJson);
                Require(Vector(door.Position, 3) && Quaternion(door.Quaternion) && (door.Goid == 343 || door.Goid == 346) && MissionNpcInteriorPlanning.RawShape(raw, p.mission.district, site.elevatorDoor.sectorID, door.Goid) && raw.mxoStaticId == door.Key, "NpcWitnessOwnGoidDoor");
            }
        }
        internal static MissionNpcInteriorEnvelope PrepareRestore(MissionProgress p, MissionProgress.SavedState state, IReadOnlyList<MissionSite> catalog)
        {
            if (state.NpcInterior.ValueKind == JsonValueKind.Undefined) return null;
            try
            {
                Require(state.Version == 2 && state.OpenFloorSites != null && state.OpenFloorCurrentSiteIndex.HasValue && state.OpenFloorCurrentSiteIndex >= -1 && state.OpenFloorCurrentSiteIndex < state.OpenFloorSites.Length && !string.IsNullOrWhiteSpace(state.Identity), "NpcRequiresVersion2ExactHistory");
                JsonElement raw = state.NpcInterior; EnvelopeShape(raw);
                MissionNpcInteriorEnvelope envelope = JsonSerializer.Deserialize<MissionNpcInteriorEnvelope>(raw.GetRawText(), MissionProgress.SaveJson);
                Require(envelope != null && envelope.Version == 2 && envelope.Kind == MissionNpcInteriorPlanning.Kind && envelope.Witnesses != null && (envelope.Pending != null || envelope.Witnesses.Length > 0), "NpcEnvelopeVersionKind");
                catalog = catalog ?? MissionSites.For(p.mission.district);
                Require(state.OpenFloorSites.All(s => s != null && s.Resolve(catalog) != null), "NpcExactHistorySiteIdentity");
                Require(envelope.Witnesses.All(w => w != null) && envelope.Witnesses.Select(w => w.Selection?.Phase).Distinct().Count() == envelope.Witnesses.Length, "NpcWitnessHistoryIdentity");
                foreach (MissionNpcInteriorWitness witness in envelope.Witnesses)
                {
                    Witness(p, witness, catalog); Require(witness.Selection.Phase <= p.phaseIndex && state.OpenFloorSites.Count(s => s.Fingerprint == witness.Selection.Site.Fingerprint) == 1, "NpcWitnessNotInExactHistory");
                }
                if (envelope.CurrentPhase.HasValue)
                {
                    Require(envelope.CurrentPhase == p.phaseIndex && state.OpenFloorCurrentSiteIndex >= 0 && envelope.Witnesses.Count(w => w.Selection.Phase == p.phaseIndex && w.Selection.Site.Fingerprint == state.OpenFloorSites[state.OpenFloorCurrentSiteIndex.Value].Fingerprint) == 1, "NpcCurrentWitnessIdentity");
                }
                if (envelope.Pending != null)
                {
                    MissionNpcInteriorPending pending = envelope.Pending;
                    Require(pending.Version == 2 && pending.Kind == MissionNpcInteriorPlanning.Kind && pending.Selection != null && (pending.Selection.Phase == p.phaseIndex || pending.Selection.Phase == p.phaseIndex + 1), "NpcPendingVersionPhase");
                    // A zero-actor refused selection is NOT placement authority. It records the one
                    // selected site when proof could not even materialize a safe role prefix.
                    if(pending.Witness==null&&pending.Selection.Actors?.Length==0)SelectionIdentity(p,pending.Selection,catalog);
                    else Selection(p, pending.Selection, catalog);
                    if (pending.Witness == null) Require(!string.IsNullOrWhiteSpace(pending.Refusal), "NpcPendingMissingProof");
                    else { Witness(p, pending.Witness, catalog); Require(Same(pending.Selection, pending.Witness.Selection), "NpcPendingWitnessSelection"); }
                }
                return envelope;
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is JsonException)
            { throw new MissionPlacementRestoreException(p, "Invalid recognized NPC-v2 metadata:" + ex.Message); }
        }
        internal static void Apply(MissionProgress p, MissionNpcInteriorEnvelope envelope)
        {
            if (envelope == null) return;
            p.npcInteriorPending = envelope.Pending; p.npcInteriorResolvedPhase = envelope.CurrentPhase;
            foreach (MissionNpcInteriorWitness witness in envelope.Witnesses) p.npcInteriorWitnesses.Add(witness.Selection.Phase, witness);
        }
        private static E.Entry SavedEntry(E index, MissionNpcInteriorEntry saved, StaticWorldObject incoming = null)
        {
            Require(index.Digest.Equals(saved.EvidenceDigest, StringComparison.OrdinalIgnoreCase), "ChangedNpcEvidenceDigest");
            StaticWorldObject selected = MissionNpcInteriorPlanning.ReadRaw(saved.RawDoorJson);
            StaticWorldObject raw = incoming ?? selected;
            Require(raw.metrId == selected.metrId && raw.sectorID == selected.sectorID && raw.mxoStaticId == selected.mxoStaticId && MissionSites.Goid(raw) == MissionSites.Goid(selected), "ChangedNpcDoorRawIdentityOrGoid");
            G.Position standing = new G.Position(saved.Standing[0], saved.Standing[1], saved.Standing[2]);
            Require(index.TryEntry(raw, native => new MissionOpenFloorEntry.Result(standing, standing, false, false, false,
                new G.Root(new G.Id(raw.sectorID, saved.Node.Region, saved.Node.Polygon), standing, "exact persisted NPC entry only"), null), out E.Entry entry, out string why), why ?? "InvalidSavedNpcEntry");
            Require(saved.Position.SequenceEqual(new double[] { entry.Door.Position.X, entry.Door.Position.Y, entry.Door.Position.Z }) && saved.Quaternion.Equals(entry.Door.Quaternion, StringComparison.OrdinalIgnoreCase), "ChangedNpcSavedDoorPose"); return entry;
        }
        private static void Geometry(MissionProgress p, MissionNpcInteriorWitness witness, IMissionNpcInteriorEvidenceProvider provider)
        {
            StaticWorldObject floorRaw = MissionNpcInteriorPlanning.ReadRaw(witness.Floor.RawDoorJson), lobbyRaw = MissionNpcInteriorPlanning.ReadRaw(witness.Lobby.RawDoorJson);
            E floor = provider.Evidence(p.mission.district, floorRaw.sectorID), lobby = provider.Evidence(p.mission.district, lobbyRaw.sectorID);
            E.Entry entry = SavedEntry(floor, witness.Floor); SavedEntry(lobby, witness.Lobby);
            StaticWorldObject[] markers = provider.Markers(p.mission.district, floorRaw.sectorID); Dictionary<uint, MissionNpcInteriorDoor> expectedDoors = new Dictionary<uint, MissionNpcInteriorDoor>();
            for (int i = 0; i < witness.Selection.Actors.Length; i++)
            {
                MissionNpcInteriorActor actor = witness.Selection.Actors[i]; StaticWorldObject raw = MissionNpcInteriorPlanning.ReadRaw(actor.RawMarkerJson);
                Require(markers.Any(m => Same(m, raw)), "ChangedNpcRawMarkerCatalog"); Dictionary<uint, StaticWorldObject> selected = new Dictionary<uint, StaticWorldObject>();
                StaticWorldObject Lookup(uint key) { StaticWorldObject door = provider.Door(p.mission.district, key); if (door != null) { door = MissionNpcInteriorPlanning.ReadRaw(MissionNpcInteriorPlanning.RawJson(door)); selected[key] = door; } return door; }
                Require(floor.TryNpcRoute(entry, raw, actor.Standing, Lookup, out E.NpcRoute route, out string why), why ?? "InvalidSavedNpcRoute");
                MissionNpcInteriorActorProof proof = witness.Actors[i];
                Require(proof.Target.Equals(new MissionNpcInteriorNode(route.Target.Region, route.Target.Polygon)) && proof.Nodes.SequenceEqual(route.Nodes.Select(n => new MissionNpcInteriorNode(n.Region, n.Polygon))), "ChangedNpcSavedInteriorRoute");
                foreach (E.DoorPose door in route.RequiredDoors) expectedDoors[door.Key] = new MissionNpcInteriorDoor(door.Key, door.Goid, MissionNpcInteriorPlanning.RawJson(selected[door.Key]), door.Quaternion, new double[] { door.Position.X, door.Position.Y, door.Position.Z });
            }
            Require(Same(expectedDoors.OrderBy(d => d.Key).Select(d => d.Value).ToArray(), witness.Doors), "ChangedNpcSavedPortalDoors");
        }
        // An indexed alias is authority only for its exact audited signature, never for a new entry/context.
        internal static void ValidateOverlay(MissionProgress p, MissionNpcInteriorWitness witness, MissionNpcInteriorEntry entry, MissionNpcInteriorDoor wood, StaticWorldObject raw, IMissionNpcInteriorEvidenceProvider provider)
        {
            try
            {
                Geometry(p, witness, provider);
                E index = provider.Evidence(raw.metrId, raw.sectorID);
                if (entry != null) SavedEntry(index, entry, raw);
                else
                {
                    StaticWorldObject selected = MissionNpcInteriorPlanning.ReadRaw(wood.RawDoorJson);
                    Require(raw.metrId == selected.metrId && raw.sectorID == selected.sectorID && raw.mxoStaticId == selected.mxoStaticId && MissionSites.Goid(raw) == wood.Goid, "ChangedNpcDoorRawIdentityOrGoid");
                    Require(index.Digest.Equals(witness.Floor.EvidenceDigest, StringComparison.OrdinalIgnoreCase), "ChangedNpcEvidenceDigest");
                    Require(index.TryWoodDoor(raw, out E.DoorPose pose, out string why), why ?? "InvalidNpcWoodAlias");
                    Require(wood.Position.SequenceEqual(new double[] { pose.Position.X, pose.Position.Y, pose.Position.Z }) && wood.Quaternion.Equals(pose.Quaternion, StringComparison.OrdinalIgnoreCase), "ChangedNpcSavedDoorPose");
                }
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            { throw new MissionPlacementRestoreException(p, "Invalid fixed NPC-v2 overlay evidence:" + ex.Message); }
        }
        internal static void ValidateGeometry(MissionProgress p, MissionNpcInteriorEnvelope envelope, IMissionNpcInteriorEvidenceProvider provider)
        {
            if (envelope == null) return;
            try
            {
                foreach (MissionNpcInteriorWitness witness in envelope.Witnesses) Geometry(p, witness, provider);
                if (envelope.Pending?.Witness != null) Geometry(p, envelope.Pending.Witness, provider);
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            { throw new MissionPlacementRestoreException(p, "Invalid fixed NPC-v2 evidence:" + ex.Message); }
        }
    }
}
