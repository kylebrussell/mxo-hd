using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using G = hds.MissionOpenFloorResolver;

namespace hds
{
    /// <summary>Audited original interior evidence only. No runtime admission/restore/door call sites.</summary>
    internal sealed class MissionInteriorEvidence
    {
        internal sealed class Signature { public ushort Goid { get; set; } public double[] Position { get; set; } public string Quaternion { get; set; } public double Rotation { get; set; } }
        internal sealed class Legacy { public Signature Signature { get; set; } public string File { get; set; } public int Line { get; set; } }
        internal sealed class Occurrence
        {
            public ushort Goid { get; set; } public ushort Region { get; set; } public int Placement { get; set; }
            public double[] Position { get; set; } public double[] ModelPosition { get; set; } public string Quaternion { get; set; }
            public double[] Minimum { get; set; } public double[] Maximum { get; set; }
            public int RecordOffset { get; set; } public string RecordSha256 { get; set; } public string Raw { get; set; }
        }
        internal sealed class Group { public uint Key { get; set; } public Legacy[] Legacy { get; set; } public Occurrence[] Occurrences { get; set; } }
        internal sealed class Edge { public ushort RegionTag { get; set; } public ushort Neighbor { get; set; } public double[] Vertex { get; set; } public int SourceOffset { get; set; } }
        internal sealed class Polygon { public int Index { get; set; } public double[] Plane { get; set; } public Edge[] Edges { get; set; } }
        internal sealed class Region
        {
            public ushort Id { get; set; } public int Ordinal { get; set; } public uint PrefabRid { get; set; } public string PrefabSha256 { get; set; }
            public bool Available { get; set; } public string Refusal { get; set; } public int[] Pivot { get; set; } public int Rotation { get; set; } public Polygon[] Polygons { get; set; }
        }
        internal sealed class Portal
        {
            public ushort SourceRegion { get; set; } public ushort SourcePolygon { get; set; } public byte SourceEdge { get; set; }
            public ushort TargetRegion { get; set; } public ushort TargetPolygon { get; set; } public byte TargetEdge { get; set; }
            public double[][] Endpoints { get; set; } public int CountOffset { get; set; } public int Count { get; set; }
            public int RecordOffset { get; set; } public string RecordSha256 { get; set; } public string Raw { get; set; } public uint[] DoorKeys { get; set; }
        }
        internal sealed class Document
        {
            public int Format { get; set; } public ushort World { get; set; } public ushort Sector { get; set; } public int OutdoorRegions { get; set; }
            public uint SourceRid { get; set; } public double[] ParentCorner { get; set; } public string MetrSha256 { get; set; } public string SidiSha256 { get; set; }
            public int SidiBufferOffset { get; set; } public int SidiBufferLength { get; set; } public string NavSha256 { get; set; }
            public Region[] Regions { get; set; } public Portal[] Portals { get; set; } public Group[] Groups { get; set; }
        }
        internal sealed class Declaration { public ushort Sector { get; set; } public string Sha256 { get; set; } public int Regions { get; set; } public int Portals { get; set; } public int Groups { get; set; } }
        internal sealed class Manifest { public int Format { get; set; } public ushort World { get; set; } public Declaration[] Sectors { get; set; } }
        internal const string ManifestSha256 = "DD15FBEEE0B0359245EB4147C772FEE926E8257759ABE019A797EA3EA3FED8F4";
        private readonly Document document;
        private readonly Dictionary<ushort, Region> regions;
        private readonly Dictionary<uint, Group> groups;
        internal readonly string Digest;

        internal readonly struct Node : IEquatable<Node>
        {
            internal readonly ushort Region, Polygon;
            internal Node(ushort region, ushort polygon) { Region = region; Polygon = polygon; }
            public bool Equals(Node other) => Region == other.Region && Polygon == other.Polygon;
            public override bool Equals(object obj) => obj is Node other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Region, Polygon);
        }
        internal sealed class DoorPose
        {
            internal readonly uint Key; internal readonly ushort Goid, Region; internal readonly G.Position Position; internal readonly string Quaternion;
            internal DoorPose(uint key, Occurrence o) { Key = key; Goid = o.Goid; Region = o.Region; Position = Point(o.ModelPosition); Quaternion = o.Quaternion; }
        }
        internal sealed class Entry
        {
            internal readonly MissionInteriorEvidence Owner; internal readonly DoorPose Door; internal readonly Node Node; internal readonly G.Position Standing;
            internal Entry(MissionInteriorEvidence owner, DoorPose door, Node node, G.Position standing) { Owner = owner; Door = door; Node = node; Standing = standing; }
        }
        internal sealed class NpcRoute
        {
            internal readonly string RawMarkerJson;
            internal readonly Node Target;
            private readonly Node[] nodes;
            private readonly DoorPose[] doors;
            internal Node[] Nodes => (Node[])nodes.Clone();
            internal DoorPose[] RequiredDoors => (DoorPose[])doors.Clone();
            // Authored topology and supported presentation evidence; not a native walk/collision certificate.
            internal NpcRoute(string raw, Node target, Node[] path, DoorPose[] required) { RawMarkerJson = raw; Target = target; nodes = (Node[])path.Clone(); doors = (DoorPose[])required.Clone(); }
        }
        private MissionInteriorEvidence(Document doc, string digest)
        { document = doc; Digest = digest; regions = doc.Regions.ToDictionary(r => r.Id); groups = doc.Groups.ToDictionary(g => g.Key); Validate(); }
        private static bool Hex(string s, int n) => s != null && s.Length == n && s.All(Uri.IsHexDigit);
        private static bool Vector(double[] a, int n) => a != null && a.Length == n && a.All(double.IsFinite);
        private static bool Family(ushort g) => g == 19 || g == 343 || g == 346 || g == 417 || g == 419;
        private static bool Quaternion(string q) => Hex(q, 32) && Enumerable.Range(0, 4).All(i => float.IsFinite(BitConverter.ToSingle(Convert.FromHexString(q), i * 4)));
        private static void Require(bool valid, string why) { if (!valid) throw new InvalidDataException(why); }
        private static G.Position Point(double[] a) => new G.Position(a[0], a[1], a[2]);
        private static bool Same(double a, double b) => Math.Abs(a - b) <= G.Epsilon;
        private static double Floor(Polygon p, double x, double z) => -(p.Plane[0] * x + p.Plane[2] * z + p.Plane[3]) / p.Plane[1];
        private static double[] Turn(double x, double y, double z, int turn)
        {
            x = (float)x; y = (float)y; z = (float)z;
            switch (turn) { case 0: return new double[] { x, y, z }; case 1: return new double[] { z, y, -x }; case 2: return new double[] { -x, y, -z }; default: return new double[] { -z, y, x }; }
        }
        private double[] NativePoint(byte[] raw, int offset, Region region)
        {
            double[] v = Turn(BitConverter.ToSingle(raw, offset), BitConverter.ToSingle(raw, offset + 4), BitConverter.ToSingle(raw, offset + 8), region.Rotation);
            // Native624685b0/62045e10 stores the composed translation BEFORE62111670/624cf0cb
            // adds the rotated point. Reassociation changes noninteger source positions by one float ULP.
            return Enumerable.Range(0, 3).Select(i => (double)(float)((float)v[i] + (float)((float)(region.Pivot[i] * 10) + (float)document.ParentCorner[i]))).ToArray();
        }
        private static string NativeQuaternion(byte[] raw, Region region)
        {
            double h = (float)Math.Sqrt(.5); double[][] rotations = new double[][] { new double[] { 0, 0, 0, 1 }, new double[] { 0, h, 0, h }, new double[] { 0, 1, 0, 0 }, new double[] { 0, -h, 0, h } };
            double[] p = rotations[region.Rotation], q = Enumerable.Range(0, 4).Select(i => (double)BitConverter.ToSingle(raw, 20 + i * 4)).ToArray();
            double a = p[0], b = p[1], c = p[2], d = p[3], e = q[0], f = q[1], g = q[2], k = q[3];
            float[] value = new float[] { (float)(d * e + a * k + b * g - c * f), (float)(d * f - a * g + b * k + c * e), (float)(d * g + a * f - b * e + c * k), (float)(d * k - a * e - b * f - c * g) };
            return Convert.ToHexString(value.SelectMany(v => BitConverter.GetBytes(v)).ToArray());
        }
        private bool Known(ushort r) => r < document.OutdoorRegions || regions.ContainsKey(r);
        private bool Available(ushort r) => regions.TryGetValue(r, out Region found) && found.Available;
        private Polygon Poly(Node n) => regions[n.Region].Polygons[n.Polygon];
        private static bool EqualPoint(double[] a, double[] b) => Same(a[0], b[0]) && Same(a[1], b[1]);
        private static bool EqualSegment(double[][] a, double[][] b) => (EqualPoint(a[0], b[0]) && EqualPoint(a[1], b[1])) || (EqualPoint(a[0], b[1]) && EqualPoint(a[1], b[0]));
        private double[][] Segment(ushort region, ushort polygon, byte edge)
        {
            Require(Available(region) && polygon < regions[region].Polygons.Length, "InteriorPortalPolygon");
            Polygon p = regions[region].Polygons[polygon]; Require(edge < p.Edges.Length, "InteriorPortalEdge");
            double[] a = p.Edges[edge].Vertex, b = p.Edges[(edge + 1) % p.Edges.Length].Vertex;
            return new double[][] { new double[] { a[0], a[2] }, new double[] { b[0], b[2] } };
        }
        private void Validate()
        {
            Document d = document;
            Require(d.Format == 1 && d.World > 0 && d.World <= 2 && d.Sector <= 0x7ff && d.OutdoorRegions >= 0 && d.SourceRid != 0 && Vector(d.ParentCorner, 3) && d.ParentCorner[1] == 0 &&
                Hex(d.MetrSha256, 64) && Hex(d.SidiSha256, 64) && Hex(d.NavSha256, 64) && d.SidiBufferOffset >= 0 && d.SidiBufferLength > 0, "InteriorEvidenceHeader");
            Require(d.Regions.Select(r => r.Ordinal).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, d.Regions.Length)), "InteriorOriginalOrdinals");
            foreach (Region r in d.Regions)
            {
                Require(r.Id == d.OutdoorRegions + r.Ordinal && r.PrefabRid != 0 && r.Pivot != null && r.Pivot.Length == 3 && r.Rotation >= 0 && r.Rotation < 4 && r.Polygons != null, "InteriorRegionIdentity");
                if (!r.Available) { Require(!string.IsNullOrWhiteSpace(r.Refusal) && r.Polygons.Length == 0, "InteriorMissingResourceEvidence"); continue; }
                Require(Hex(r.PrefabSha256, 64) && r.Polygons.Length > 0 && r.Polygons.Select(p => p.Index).SequenceEqual(Enumerable.Range(0, r.Polygons.Length)), "InteriorPolygonIdentity");
                foreach (Polygon p in r.Polygons)
                {
                    Require(Vector(p.Plane, 4) && Math.Abs(p.Plane[1]) > G.Epsilon && p.Edges != null && p.Edges.Length >= 3, "InteriorPolygonPlane");
                    foreach (Edge e in p.Edges)
                        Require(Vector(e.Vertex, 3) && e.SourceOffset >= 0 && e.RegionTag == 0xffff && (e.Neighbor == 0xffff || e.Neighbor < r.Polygons.Length) &&
                            (float)e.Vertex[1] == (float)Floor(p, e.Vertex[0], e.Vertex[2]), "InteriorINAVReference");
                }
            }
            foreach (Group g in d.Groups)
            {
                Require(g.Key >> 20 == d.Sector && g.Legacy != null && g.Legacy.Length > 0 && g.Occurrences != null && g.Occurrences.Length > 0, "InteriorSourceGroup");
                Require(g.Occurrences.All(o => o != null) && g.Occurrences.Select(o => o.RecordOffset).Distinct().Count() == g.Occurrences.Length, "InteriorDuplicateNativeRecord");
                foreach (Legacy raw in g.Legacy) Require(raw != null && raw.Signature != null && Family(raw.Signature.Goid) && Vector(raw.Signature.Position, 3) && Quaternion(raw.Signature.Quaternion) && double.IsFinite(raw.Signature.Rotation) && raw.Line >= 2 && !string.IsNullOrWhiteSpace(raw.File), "InteriorLegacySignature");
                foreach (Occurrence o in g.Occurrences)
                {
                    Require(o != null && Family(o.Goid) && regions.TryGetValue(o.Region, out Region r) && o.Placement == r.Ordinal && Vector(o.Position, 3) && Vector(o.ModelPosition, 3) && Quaternion(o.Quaternion) && Vector(o.Minimum, 3) && Vector(o.Maximum, 3) && Enumerable.Range(0, 3).All(i => o.Minimum[i] <= o.Maximum[i]), "InteriorNativeOccurrence");
                    Require(o.RecordOffset >= 0 && (long)o.RecordOffset + 74 <= d.SidiBufferLength && Hex(o.Raw, 148) && Hex(o.RecordSha256, 64), "InteriorNativeRecordBounds");
                    byte[] bytes = Convert.FromHexString(o.Raw);
                    Require(Convert.ToHexString(SHA256.HashData(bytes)).Equals(o.RecordSha256, StringComparison.OrdinalIgnoreCase) && BitConverter.ToUInt32(bytes, 0) == o.Goid && BitConverter.ToUInt32(bytes, 4) == (g.Key & 0xfffff), "InteriorNativeRecordIdentity");
                    Require(o.Position.SequenceEqual(NativePoint(bytes, 8, regions[o.Region])) && o.Quaternion.Equals(NativeQuaternion(bytes, regions[o.Region]), StringComparison.OrdinalIgnoreCase), "InteriorNativeParentTransform");
                    Require(Enumerable.Range(0, 3).All(i => (double)((float)((float)o.Minimum[i] + (float)o.Maximum[i]) * .5f) == o.ModelPosition[i]), "InteriorNativeBoundMidpoint");
                }
            }
            foreach (var table in d.Portals.GroupBy(p => p.CountOffset))
                Require(table.All(p => p.Count == table.First().Count && p.SourceRegion == table.First().SourceRegion) &&
                    table.Count() == table.First().Count && table.Select(p => p.RecordOffset).OrderBy(i => i)
                    .SequenceEqual(Enumerable.Range(0, table.First().Count).Select(i => table.Key + 4 + i * 26)), "InteriorPortalCount");
            foreach (Portal p in d.Portals)
            {
                Require(p != null && Known(p.SourceRegion) && Known(p.TargetRegion) && p.Endpoints != null && p.Endpoints.Length == 2 && p.Endpoints.All(v => Vector(v, 2)) && !EqualPoint(p.Endpoints[0], p.Endpoints[1]) && p.DoorKeys != null && p.DoorKeys.Distinct().Count() == p.DoorKeys.Length, "InteriorPortalIdentity");
                Require(p.Count > 0 && p.CountOffset >= 0 && (long)p.CountOffset + 4 + 26L * p.Count <= d.SidiBufferLength && p.RecordOffset >= p.CountOffset + 4 && (p.RecordOffset - p.CountOffset - 4) % 26 == 0 && p.RecordOffset < p.CountOffset + 4 + 26L * p.Count && Hex(p.Raw, 52) && Hex(p.RecordSha256, 64), "InteriorPortalTableBounds");
                byte[] b = Convert.FromHexString(p.Raw);
                Require(Convert.ToHexString(SHA256.HashData(b)).Equals(p.RecordSha256, StringComparison.OrdinalIgnoreCase) && BitConverter.ToUInt16(b, 0) == p.SourceRegion && BitConverter.ToUInt16(b, 2) == p.SourcePolygon && b[4] == p.SourceEdge && BitConverter.ToUInt16(b, 5) == p.TargetRegion && BitConverter.ToUInt16(b, 7) == p.TargetPolygon && b[9] == p.TargetEdge &&
                    p.Endpoints[0][0] == d.ParentCorner[0] + BitConverter.ToSingle(b, 10) && p.Endpoints[0][1] == d.ParentCorner[2] + BitConverter.ToSingle(b, 14) && p.Endpoints[1][0] == d.ParentCorner[0] + BitConverter.ToSingle(b, 18) && p.Endpoints[1][1] == d.ParentCorner[2] + BitConverter.ToSingle(b, 22), "InteriorPortalRecordIdentity");
                // Missing declared prefab or exterior evidence is retained, never promoted to a walk edge.
                if (!Available(p.SourceRegion) || !Available(p.TargetRegion)) continue;
                Require(d.Portals.Count(q => q.SourceRegion == p.TargetRegion && q.SourcePolygon == p.TargetPolygon && q.SourceEdge == p.TargetEdge && q.TargetRegion == p.SourceRegion && q.TargetPolygon == p.SourcePolygon && q.TargetEdge == p.SourceEdge) == 1, "InteriorPortalReciprocal");
                Require(EqualSegment(Segment(p.SourceRegion, p.SourcePolygon, p.SourceEdge), p.Endpoints) && EqualSegment(Segment(p.TargetRegion, p.TargetPolygon, p.TargetEdge), p.Endpoints), "InteriorPortalEndpoints");
                foreach (double[] at in p.Endpoints)
                    Require(Same(Floor(Poly(new Node(p.SourceRegion, p.SourcePolygon)), at[0], at[1]), Floor(Poly(new Node(p.TargetRegion, p.TargetPolygon)), at[0], at[1])), "InteriorPortalFloor");
                uint[] expectedDoors = d.Groups.Where(g => g.Occurrences.Any(o => o.Goid != 19 && (o.Region == p.SourceRegion || o.Region == p.TargetRegion) && OnSegment(o.Position, p.Endpoints))).Select(g => g.Key).OrderBy(k => k).ToArray();
                Require(expectedDoors.SequenceEqual(p.DoorKeys.OrderBy(k => k)), "InteriorPortalDoorAssociation");
            }
        }
        private static bool OnSegment(double[] v, double[][] s) => Same((v[0] - s[0][0]) * (s[1][1] - s[0][1]), (v[2] - s[0][1]) * (s[1][0] - s[0][0])) && v[0] >= Math.Min(s[0][0], s[1][0]) && v[0] <= Math.Max(s[0][0], s[1][0]) && v[2] >= Math.Min(s[0][1], s[1][1]) && v[2] <= Math.Max(s[0][1], s[1][1]);
        internal static MissionInteriorEvidence Read(byte[] json, ushort world, ushort sector)
        {
            Document d = JsonSerializer.Deserialize<Document>(json);
            Require(d != null && d.World == world && d.Sector == sector && d.Regions != null && d.Portals != null && d.Groups != null && d.Regions.All(r => r != null) && d.Portals.All(p => p != null) && d.Groups.All(g => g != null), "InteriorEvidenceWorldSector");
            Require(d.Regions.Select(r => r.Id).Distinct().Count() == d.Regions.Length && d.Groups.Select(g => g.Key).Distinct().Count() == d.Groups.Length, "InteriorEvidenceDuplicates");
            return new MissionInteriorEvidence(d, Convert.ToHexString(SHA256.HashData(json)));
        }
        internal static MissionInteriorEvidence LoadVerified(byte[] compressed, string hash, ushort world, ushort sector)
        {
            Require(Hex(hash, 64) && Convert.ToHexString(SHA256.HashData(compressed)).Equals(hash, StringComparison.OrdinalIgnoreCase), "InteriorEvidencePayloadHash");
            using var input = new MemoryStream(compressed); using var gzip = new GZipStream(input, CompressionMode.Decompress); using var output = new MemoryStream(); gzip.CopyTo(output);
            return Read(output.ToArray(), world, sector);
        }
        private static Manifest AuditedManifest(string directory, ushort world)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, "manifest.json"));
            string normalized = new UTF8Encoding(false, true).GetString(bytes).Replace("\r\n", "\n");
            Require(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).Equals(ManifestSha256, StringComparison.OrdinalIgnoreCase), "InteriorEvidenceManifestHash");
            Manifest m = JsonSerializer.Deserialize<Manifest>(bytes);
            Require(m != null && m.Format == 1 && m.World == world && m.Sectors != null && m.Sectors.All(s => s != null) && m.Sectors.Select(s => s.Sector).Distinct().Count() == m.Sectors.Length, "InteriorEvidenceManifestWorld");
            return m;
        }
        internal static bool Declared(string directory, ushort world, ushort sector) => AuditedManifest(directory, world).Sectors.Any(s => s.Sector == sector);
        internal static MissionInteriorEvidence LoadAudited(string directory, ushort world, ushort sector)
        {
            Manifest m = AuditedManifest(directory, world);
            Declaration row = m.Sectors.SingleOrDefault(s => s.Sector == sector); Require(row != null, "UndeclaredInteriorEvidenceSector");
            MissionInteriorEvidence index = LoadVerified(File.ReadAllBytes(Path.Combine(directory, sector + ".json.gz")), row.Sha256, world, sector);
            Require(index.document.Regions.Length == row.Regions && index.document.Portals.Length == row.Portals && index.document.Groups.Length == row.Groups, "InteriorEvidenceManifestCounts");
            return index;
        }
        private bool Raw(StaticWorldObject raw, out Group group)
        {
            group = null;
            return raw != null && raw.metrId == document.World && raw.sectorID == document.Sector && !raw.exterior && raw.staticId <= 0xfffff && raw.mxoStaticId == ((uint)raw.sectorID << 20 | raw.staticId) && raw.type != null && (raw.type.Length == 2 || raw.type.Length == 4 && raw.type[2] == 0 && raw.type[3] == 0) && groups.TryGetValue(raw.mxoStaticId, out group) &&
                group.Legacy.Any(l => l.Signature.Goid == MissionSites.Goid(raw) && l.Signature.Position.SequenceEqual(new double[] { raw.pos_x, raw.pos_y, raw.pos_z }) && string.Equals(l.Signature.Quaternion, raw.quat, StringComparison.OrdinalIgnoreCase) && l.Signature.Rotation == raw.rot);
        }
        private bool Position(StaticWorldObject raw, out Group group, out G.Position position, out string refusal)
        {
            position = default; refusal = "UnlistedInteriorDoorSignature";
            if (!Raw(raw, out group) || MissionSites.Goid(raw) == 19) return false;
            double[] first = group.Occurrences[0].ModelPosition;
            if (group.Occurrences.Any(o => !o.ModelPosition.SequenceEqual(first))) { refusal = "InteriorDoorPositionDisagreement"; return false; }
            position = Point(first); refusal = null; return true;
        }
        internal bool TryWoodDoor(StaticWorldObject raw, out DoorPose pose, out string refusal)
        {
            pose = null;
            if (!Position(raw, out Group group, out _, out refusal)) return false;
            ushort goid = MissionSites.Goid(raw);
            if (goid != 343 && goid != 346) { refusal = "UnsupportedInteriorWoodDoorType"; return false; }
            Occurrence[] own = group.Occurrences.Where(o => o.Goid == goid).ToArray();
            if (own.Length == 0 || own.Any(o => o.Quaternion != own[0].Quaternion)) { refusal = "AmbiguousOwnGoidDoorOrientation"; return false; }
            if (own.Any(o => !Available(o.Region))) { refusal = "MissingOwnGoidDoorPrefabEvidence"; return false; }
            pose = new DoorPose(raw.mxoStaticId, own[0]); refusal = null; return true;
        }
        internal bool TryEntry(StaticWorldObject raw, Func<G.Position, MissionOpenFloorEntry.Result> calculate, out Entry entry, out string refusal)
        {
            entry = null;
            if (!Position(raw, out Group group, out G.Position position, out refusal)) return false;
            ushort type = MissionSites.Goid(raw);
            if ((type != 417 && type != 419) || calculate == null) { refusal = "UnsupportedInteriorEntryDoor"; return false; }
            // Exactly one existing ordinary position calculation; yaw is selected by its actual context, never scanned.
            MissionOpenFloorEntry.Result result = calculate(position);
            if (result?.Root == null) { refusal = result?.Refusal ?? "MissingInteriorEntryRoot"; return false; }
            G.Id at = result.Root.Surface;
            if (at.Sector != document.Sector || !Available(at.Region)) { refusal = "UnavailableInteriorEntryRegion"; return false; }
            Occurrence[] selected = group.Occurrences.Where(o => o.Goid == type && o.Region == at.Region).ToArray();
            if (selected.Length != 1) { refusal = "MissingOrAmbiguousInteriorEntryOrientation"; return false; }
            Node node = new Node(at.Region, at.Polygon);
            if (node.Polygon >= regions[node.Region].Polygons.Length || !Contains(Poly(node), result.Published)) { refusal = "InteriorEntryStandingMismatch"; return false; }
            entry = new Entry(this, new DoorPose(raw.mxoStaticId, selected[0]), node, result.Published); refusal = null; return true;
        }
        private static bool Contains(Polygon polygon, G.Position point)
        {
            if (!Same(point.Y, Floor(polygon, point.X, point.Z) + 95)) return false;
            return Inside(polygon, point);
        }
        private static bool Inside(Polygon polygon, G.Position point)
        {
            bool positive = true, negative = true;
            for (int i = 0; i < polygon.Edges.Length; i++)
            {
                double[] a = polygon.Edges[i].Vertex, b = polygon.Edges[(i + 1) % polygon.Edges.Length].Vertex;
                double cross = (b[0] - a[0]) * (point.Z - a[2]) - (b[2] - a[2]) * (point.X - a[0]);
                positive &= cross >= -G.Epsilon; negative &= cross <= G.Epsilon;
            }
            return positive || negative;
        }
        internal bool TryMarkerForSpot(IEnumerable<StaticWorldObject> rawMarkers, double[] planned, out StaticWorldObject marker, out string refusal)
        {
            marker = null; refusal = "MissingOrAmbiguousPlannedNpcMarker";
            if (!Vector(planned, 4) || rawMarkers == null) return false;
            StaticWorldObject[] found = rawMarkers.Where(raw => Raw(raw, out _) && MissionSites.Goid(raw) == 19 && raw.pos_x == planned[0] && raw.pos_y - 50 == planned[1] && raw.pos_z == planned[2] && raw.rot == planned[3])
                .GroupBy(raw => JsonSerializer.Serialize(new { raw.metrId, raw.sectorID, raw.mxoStaticId, raw.staticId, raw.pos_x, raw.pos_y, raw.pos_z, raw.rot, raw.quat })).Select(g => g.First()).ToArray();
            if (found.Length != 1) return false;
            marker = found[0]; refusal = null; return true;
        }
        private sealed class Step { internal Node Before; internal Portal Portal; internal Step(Node before, Portal portal) { Before = before; Portal = portal; } }
        private bool Level(Node node, double floor) => Available(node.Region) && node.Polygon < regions[node.Region].Polygons.Length && Poly(node).Edges.All(e => Same(Floor(Poly(node), e.Vertex[0], e.Vertex[2]), floor));
        internal bool TryNpcRoute(Entry entry, StaticWorldObject marker, double[] planned, Func<uint, StaticWorldObject> selectedDoor, out NpcRoute proof, out string refusal)
        {
            proof = null; refusal = "InvalidInteriorEntryOwner";
            if (entry == null || !ReferenceEquals(entry.Owner, this)) return false;
            if (!TryMarkerForSpot(new StaticWorldObject[] { marker }, planned, out _, out refusal) || !Raw(marker, out Group group)) return false;
            ushort[] original = group.Occurrences.Where(o => o.Goid == 19).Select(o => o.Region).Distinct().ToArray();
            if (original.Length != 1 || !Available(original[0])) { refusal = "MissingOrAmbiguousNpcOriginalRegion"; return false; }
            Region room = regions[original[0]]; G.Position standing = new G.Position(planned[0], planned[1], planned[2]);
            if (group.Occurrences.Where(o => o.Goid == 19).Any(o => !room.Polygons.Any(p => Inside(p, Point(o.Position)))))
            { refusal = "NpcOriginalMarkerOutsideDeclaredRoom"; return false; }
            Polygon target = room.Polygons.LastOrDefault(p => Contains(p, standing));
            if (target == null) { refusal = "NpcStandingOutsideOriginalRoom"; return false; }
            Node end = new Node(room.Id, (ushort)target.Index); double floor = entry.Standing.Y - 95;
            if (!Level(entry.Node, floor) || !Level(end, floor)) { refusal = "NoAuthoredSameFloorInteriorRoute"; return false; }
            Queue<Node> queue = new Queue<Node>(); Dictionary<Node, Step> previous = new Dictionary<Node, Step>(); previous.Add(entry.Node, null); queue.Enqueue(entry.Node);
            while (queue.Count != 0)
            {
                Node node = queue.Dequeue(); if (node.Equals(end)) break;
                if (!Level(node, floor)) continue;
                foreach (Edge e in Poly(node).Edges)
                    if (e.Neighbor != 0xffff) Add(new Node(node.Region, e.Neighbor), null, node);
                foreach (Portal portal in document.Portals.Where(p => p.SourceRegion == node.Region && p.SourcePolygon == node.Polygon))
                    if (Available(portal.TargetRegion)) Add(new Node(portal.TargetRegion, portal.TargetPolygon), portal, node);
            }
            void Add(Node next, Portal portal, Node before) { if (!previous.ContainsKey(next) && Level(next, floor)) { previous.Add(next, new Step(before, portal)); queue.Enqueue(next); } }
            if (!previous.ContainsKey(end)) { refusal = "NoAuthoredSameFloorInteriorRoute"; return false; }
            List<Node> path = new List<Node>(); List<Portal> crossings = new List<Portal>(); Node cursor = end;
            while (true) { path.Add(cursor); Step step = previous[cursor]; if (step == null) break; if (step.Portal != null) crossings.Add(step.Portal); cursor = step.Before; }
            path.Reverse(); List<DoorPose> doors = new List<DoorPose>();
            foreach (uint key in crossings.SelectMany(p => p.DoorKeys).Distinct())
            {
                StaticWorldObject raw = selectedDoor?.Invoke(key);
                if (raw == null || raw.mxoStaticId != key || !TryWoodDoor(raw, out DoorPose pose, out refusal)) { refusal = "UnsupportedRequiredPortalDoor:" + key.ToString("x8") + ":" + (refusal ?? "MissingSelectedRawDoor"); return false; }
                doors.Add(pose);
            }
            proof = new NpcRoute(JsonSerializer.Serialize(marker, MissionProgress.SaveJson), end, path.ToArray(), doors.ToArray()); refusal = null; return true;
        }
    }
}
