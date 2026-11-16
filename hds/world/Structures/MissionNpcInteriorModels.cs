using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace hds
{
    // Separate tagged NPC authority. These types never reinterpret v1 object Sources.
    internal static class MissionNpcInteriorModels
    {
        internal static T[] Copy<T>(T[] values) => values == null ? null : (T[])values.Clone();
    }
    internal readonly struct MissionNpcInteriorNode : IEquatable<MissionNpcInteriorNode>
    {
        public ushort Region { get; } public ushort Polygon { get; }
        [JsonConstructor] public MissionNpcInteriorNode(ushort region, ushort polygon) { Region = region; Polygon = polygon; }
        public bool Equals(MissionNpcInteriorNode other) => Region == other.Region && Polygon == other.Polygon;
        public override bool Equals(object obj) => obj is MissionNpcInteriorNode other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Region, Polygon);
    }
    internal sealed class MissionNpcInteriorActor
    {
        public string Kind { get; } public int Id { get; } public int Index { get; } public byte WirePhase { get; } public bool Deferred { get; }
        public string RawMarkerJson { get; }
        private readonly double[] standing;
        public double[] Standing => MissionNpcInteriorModels.Copy(standing);
        [JsonConstructor] public MissionNpcInteriorActor(string kind, int id, int index, byte wirePhase, bool deferred, string rawMarkerJson, double[] standing)
        { Kind = kind; Id = id; Index = index; WirePhase = wirePhase; Deferred = deferred; RawMarkerJson = rawMarkerJson; this.standing = MissionNpcInteriorModels.Copy(standing); }
        internal string Key => Kind + ":" + Id + ":" + Index;
    }
    internal sealed class MissionNpcInteriorSelection
    {
        public int Mission { get; } public string Identity { get; } public long Started { get; } public int Phase { get; } public MissionOpenFloorSite Site { get; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int RolePlan { get; }
        private readonly MissionNpcInteriorActor[] actors;
        public MissionNpcInteriorActor[] Actors => MissionNpcInteriorModels.Copy(actors);
        [JsonConstructor] public MissionNpcInteriorSelection(int mission, string identity, long started, int phase, MissionOpenFloorSite site, MissionNpcInteriorActor[] actors, int rolePlan = 0)
        { RolePlan = rolePlan; Mission = mission; Identity = identity; Started = started; Phase = phase; Site = site; this.actors = MissionNpcInteriorModels.Copy(actors); }
    }
    internal sealed class MissionNpcInteriorEntry
    {
        public string RawDoorJson { get; } public string EvidenceDigest { get; } public string Quaternion { get; }
        public MissionNpcInteriorNode Node { get; }
        private readonly double[] position, standing;
        public double[] Position => MissionNpcInteriorModels.Copy(position); public double[] Standing => MissionNpcInteriorModels.Copy(standing);
        [JsonConstructor] public MissionNpcInteriorEntry(string rawDoorJson, string evidenceDigest, string quaternion, MissionNpcInteriorNode node, double[] position, double[] standing)
        { RawDoorJson = rawDoorJson; EvidenceDigest = evidenceDigest; Quaternion = quaternion; Node = node; this.position = MissionNpcInteriorModels.Copy(position); this.standing = MissionNpcInteriorModels.Copy(standing); }
    }
    internal sealed class MissionNpcInteriorDoor
    {
        public uint Key { get; } public ushort Goid { get; } public string RawDoorJson { get; } public string Quaternion { get; }
        private readonly double[] position;
        public double[] Position => MissionNpcInteriorModels.Copy(position);
        [JsonConstructor] public MissionNpcInteriorDoor(uint key, ushort goid, string rawDoorJson, string quaternion, double[] position)
        { Key = key; Goid = goid; RawDoorJson = rawDoorJson; Quaternion = quaternion; this.position = MissionNpcInteriorModels.Copy(position); }
    }
    internal sealed class MissionNpcInteriorActorProof
    {
        public string Role { get; } public MissionNpcInteriorNode Target { get; }
        private readonly MissionNpcInteriorNode[] nodes;
        public MissionNpcInteriorNode[] Nodes => MissionNpcInteriorModels.Copy(nodes);
        [JsonConstructor] public MissionNpcInteriorActorProof(string role, MissionNpcInteriorNode target, MissionNpcInteriorNode[] nodes)
        { Role = role; Target = target; this.nodes = MissionNpcInteriorModels.Copy(nodes); }
    }
    internal sealed class MissionNpcInteriorWitness
    {
        public int Version { get; } public string Kind { get; } public MissionNpcInteriorSelection Selection { get; }
        public MissionNpcInteriorEntry Floor { get; } public MissionNpcInteriorEntry Lobby { get; }
        private readonly MissionNpcInteriorActorProof[] actors;
        private readonly MissionNpcInteriorDoor[] doors;
        public MissionNpcInteriorActorProof[] Actors => MissionNpcInteriorModels.Copy(actors); public MissionNpcInteriorDoor[] Doors => MissionNpcInteriorModels.Copy(doors);
        [JsonConstructor] public MissionNpcInteriorWitness(int version, string kind, MissionNpcInteriorSelection selection, MissionNpcInteriorEntry floor, MissionNpcInteriorEntry lobby, MissionNpcInteriorActorProof[] actors, MissionNpcInteriorDoor[] doors)
        { Version = version; Kind = kind; Selection = selection; Floor = floor; Lobby = lobby; this.actors = MissionNpcInteriorModels.Copy(actors); this.doors = MissionNpcInteriorModels.Copy(doors); }
    }
    internal sealed class MissionNpcInteriorPending
    {
        public int Version { get; } public string Kind { get; } public MissionNpcInteriorSelection Selection { get; } public MissionNpcInteriorWitness Witness { get; } public string Refusal { get; }
        [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public JsonElement Street {get;}
        [JsonConstructor] public MissionNpcInteriorPending(int version, string kind, MissionNpcInteriorSelection selection, MissionNpcInteriorWitness witness, string refusal,JsonElement street=default)
        { Version = version; Kind = kind; Selection = selection; Witness = witness; Refusal = refusal; Street=street.ValueKind==JsonValueKind.Undefined?default:street.Clone(); }
        internal MissionNpcInteriorPending WithRefusal(string refusal) => new MissionNpcInteriorPending(Version, Kind, Selection, Witness, refusal,Street);
        internal MissionNpcInteriorPending WithStreet(JsonElement street)=>new MissionNpcInteriorPending(Version,Kind,Selection,Witness,Refusal,street);
    }
    internal sealed class MissionNpcInteriorEnvelope
    {
        public int Version { get; } public string Kind { get; } public MissionNpcInteriorPending Pending { get; } public int? CurrentPhase { get; }
        private readonly MissionNpcInteriorWitness[] witnesses;
        public MissionNpcInteriorWitness[] Witnesses => MissionNpcInteriorModels.Copy(witnesses);
        [JsonConstructor] public MissionNpcInteriorEnvelope(int version, string kind, MissionNpcInteriorPending pending, int? currentPhase, MissionNpcInteriorWitness[] witnesses)
        { Version = version; Kind = kind; Pending = pending; CurrentPhase = currentPhase; this.witnesses = MissionNpcInteriorModels.Copy(witnesses); }
    }
}
