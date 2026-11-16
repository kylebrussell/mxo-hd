using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
using G = hds.MissionOpenFloorResolver;
using E = hds.MissionInteriorEvidence;
namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionInteriorEvidenceTests
{
    private static string Repo { get { string p = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(p, "hds", "Hardline Dreams MxO server.csproj"))) p = Path.GetDirectoryName(p)!; return p; } }
    private static string DirectoryPath => Path.Combine(Repo, "data", "mission_interior_evidence", "1");
    private static readonly Lazy<byte[]> TaborJson = new Lazy<byte[]>(() => Json(550));
    private static byte[] Json(ushort sector) { using var f = File.OpenRead(Path.Combine(DirectoryPath, sector + ".json.gz")); using var g = new GZipStream(f, CompressionMode.Decompress); using var o = new MemoryStream(); g.CopyTo(o); return o.ToArray(); }
    private static E.Document Doc() => JsonSerializer.Deserialize<E.Document>(TaborJson.Value)!;
    private static E Index(E.Document d) => E.Read(JsonSerializer.SerializeToUtf8Bytes(d), d.World, d.Sector);
    private static E Tabor() => E.LoadAudited(DirectoryPath, 1, 550);
    private static E.Group Group(E.Document d, uint key) => d.Groups.Single(g => g.Key == key);
    private static StaticWorldObject Raw(uint key, ushort goid, double x, double y, double z, double rotation, string quaternion) => new StaticWorldObject()
    { metrId = 1, sectorID = (ushort)(key >> 20), staticId = key & 0xfffff, mxoStaticId = key, type = BitConverter.GetBytes((uint)goid), pos_x = x, pos_y = y, pos_z = z, rot = rotation, quat = quaternion };
    private static StaticWorldObject FloorDoor() => Raw(0x22602817, 419, 40300, 6145, 25800, 90, "0000000000000000000000000000803f");
    private static StaticWorldObject OphiuchiMarker() => Raw(0x226005f5, 19, 38490, 6145, 26980, 148.999967603, "00000000e946773f00000000f083843e");
    private static double[] Planned() => new[] { 38490d, 6095, 26980, 148.999967603 };
    private static StaticWorldObject? Wood(uint key) => key switch
    {
        0x2260056b => Raw(key, 346, 40800, 6145, 28100, 88.9999883795, "00000000f204353f00000000f304353f"),
        0x226013f3 => Raw(key, 346, 38000, 6145, 23500, 88.9999883795, "00000000f204353f00000000f304353f"),
        _ => null
    };
    private static MissionOpenFloorEntry.Result Calculate(G.Position door) => MissionOpenFloorEntry.Calculate(door, 39125, 25800, "literal original125/poly30 Tabor control", _ => true,
        _ => new NavLocation { sector = 550, region = 125, polygon = 30, floorY = 6000, indoors = true });
    private static E.Entry Entry(E index) { Assert.True(index.TryEntry(FloorDoor(), Calculate, out var entry, out var why), why); return entry; }
    private static string Refuses(E.Document d) => Assert.Throws<InvalidDataException>(() => Index(d)).Message;
    private static void Rehash(E.Portal p)
    {
        byte[] b = Convert.FromHexString(p.Raw); BitConverter.GetBytes(p.SourceRegion).CopyTo(b, 0); BitConverter.GetBytes(p.SourcePolygon).CopyTo(b, 2); b[4] = p.SourceEdge;
        BitConverter.GetBytes(p.TargetRegion).CopyTo(b, 5); BitConverter.GetBytes(p.TargetPolygon).CopyTo(b, 7); b[9] = p.TargetEdge;
        for (int i = 0; i < 2; i++) { BitConverter.GetBytes((float)(p.Endpoints[i][0] - 36000)).CopyTo(b, 10 + i * 8); BitConverter.GetBytes((float)(p.Endpoints[i][1] - 16000)).CopyTo(b, 14 + i * 8); }
        p.Raw = Convert.ToHexString(b); p.RecordSha256 = Convert.ToHexString(SHA256.HashData(b));
    }
    [Fact] public void LiteralTaborReciprocalPortalAndActualRawNpcHaveAnAuthoredInteriorRoute()
    {
        E index = Tabor(); var marker = OphiuchiMarker(); string before = JsonSerializer.Serialize(marker, MissionProgress.SaveJson); int calls = 0;
        Assert.True(index.TryEntry(FloorDoor(), p => { calls++; Assert.Equal((40000d, 6145d, 25500d), (p.X, p.Y, p.Z)); return Calculate(p); }, out var entry, out var why), why);
        Assert.Equal(1, calls); Assert.Equal((ushort)125, entry.Node.Region);
        Assert.Equal(39810.81081081081, entry.Standing.X); Assert.Equal(25564.864864864863, entry.Standing.Z);
        Assert.True(index.TryNpcRoute(entry, marker, Planned(), Wood, out var proof, out why), why);
        Assert.Equal(new E.Node(43, 245), proof.Target); Assert.Contains(new E.Node(125, 0), proof.Nodes); Assert.Contains(new E.Node(43, 39), proof.Nodes);
        Assert.Equal(27, proof.Nodes.Length); var door = Assert.Single(proof.RequiredDoors);
        Assert.Equal(0x2260056bu, door.Key); Assert.Equal((ushort)346, door.Goid);
        Assert.Equal((38500d, 6145d, 25800d), (door.Position.X, door.Position.Y, door.Position.Z)); Assert.Equal("00000000f30435b300000000ffff7f3f", door.Quaternion);
        Assert.Equal(before, proof.RawMarkerJson); Assert.Equal(before, JsonSerializer.Serialize(marker, MissionProgress.SaveJson));
        var exposed = proof.Nodes; exposed[0] = default; Assert.NotEqual(exposed[0], proof.Nodes[0]);
        var exposedDoors = proof.RequiredDoors; exposedDoors[0] = null!; Assert.NotNull(proof.RequiredDoors[0]);
    }
    [Fact] public void LiteralOriginalLinkHasExactCountOffsetEdgeAndEndpointEvidence()
    {
        var d = Doc(); var p = d.Portals.Single(p => p.SourceRegion == 125 && p.TargetRegion == 43);
        Assert.Equal(1895227, p.CountOffset); Assert.Equal(4, p.Count); Assert.Equal((ushort)0, p.SourcePolygon); Assert.Equal((byte)2, p.SourceEdge);
        Assert.Equal((ushort)39, p.TargetPolygon); Assert.Equal((byte)1, p.TargetEdge);
        Assert.Equal(new[] { 38470d, 25800 }, p.Endpoints[0]); Assert.Equal(new[] { 38530d, 25800 }, p.Endpoints[1]);
        Assert.StartsWith("7d000000022b00270001", p.Raw); Assert.Equal(new[] { 0x2260056bu }, p.DoorKeys);
        Assert.NotNull(Index(d));
    }
    [Fact] public void LiteralNonintegerMarkerPreservesNativeTranslationFirstFloatBits()
    {
        // Actual counted GO19 key22601ef4, region85, pivot460/360/1320, rotation3.
        // Primary native transform stores translation13200+16000 before adding localX.
        var d = Doc(); var source = Group(d, 0x22601ef4).Occurrences.Single();
        Assert.StartsWith("13000000f41e0000585ac043000000004c4b4d45", source.Raw);
        Assert.Equal((ushort)85, source.Region); Assert.Equal(new[] { 37315.29296875, 3600d, 29584.705078125 }, source.Position);
        Assert.Equal(0x46e72169u, BitConverter.SingleToUInt32Bits((float)source.Position[2]));
        float localX = BitConverter.ToSingle(Convert.FromHexString("585ac043"));
        Assert.Equal(0x46e72169u, BitConverter.SingleToUInt32Bits(localX + (13200f + 16000f)));
        Assert.Equal(0x46e7216au, BitConverter.SingleToUInt32Bits((localX + 13200f) + 16000f));
        Assert.NotNull(Index(d)); // Production record replay must agree with these exact primary bits.
    }
    [Fact] public void RetailOpen346UsesItsOwnNativeQuaternionAndNeverBorrowsPaired343Yaw()
    {
        // Recursion 2009-07-05 19:57:25 request321; matching next-second bound3460834 creation.
        byte[] rpc = Convert.FromHexString("80C84B0150540300"); uint key = BitConverter.ToUInt32(rpc, 2); Assert.Equal(0x5450014bu, key);
        E index = E.LoadAudited(DirectoryPath, 1, 1349);
        var push = Raw(key, 346, 59400, -55, 11000, 90.0000116205, "00000000f20435bf00000000f304353f");
        var pull = Raw(key, 343, 59400, -55, 11000, 269.99998838, "00000000f204353f00000000f304353f");
        Assert.True(index.TryWoodDoor(push, out var a, out var why), why); Assert.True(index.TryWoodDoor(pull, out var b, out why), why);
        Assert.Equal((59400d, -55d, 11000d), (a.Position.X, a.Position.Y, a.Position.Z));
        Assert.Equal("00000000f304353f00000000f204353f", a.Quaternion); Assert.Equal("00000000f304353f00000000f20435bf", b.Quaternion); Assert.NotEqual(a.Quaternion, b.Quaternion);
        Assert.Equal((ushort)346, a.Goid); Assert.Equal((ushort)343, b.Goid);
        const string retail = "0100085a014b01505447cdab038400000000f304353f00000000f204353f41000000000001ed400000000000804bc000000000007cc54034080000380000";
        byte[] record = Convert.FromHexString(retail); var creation = Assert.Single(WireFormat.FindCreations(record));
        Assert.Equal(key, BitConverter.ToUInt32(record, 5)); Assert.Equal((ushort)346, creation.Goid);
        Assert.Equal(DoorLifecycle.StateOpen, BitConverter.ToUInt32(creation.Attributes["CurrentState"]));
        byte[] position = creation.Attributes["Position"];
        Assert.Equal((a.Position.X, a.Position.Y, a.Position.Z), (BitConverter.ToDouble(position, 0), BitConverter.ToDouble(position, 8), BitConverter.ToDouble(position, 16)));
        Assert.Equal(a.Quaternion, Convert.ToHexString(creation.Attributes["Orientation"]).ToLowerInvariant());
    }
    [Fact] public void ContextSelectsOneEntryYawAndNeverSearchesForAConvenientRegion()
    {
        E index = Tabor(); int calls = 0;
        Assert.False(index.TryEntry(FloorDoor(), p => { calls++; var standing = new G.Position(40370.71067811865, 6095, 25870.710678118656); return new MissionOpenFloorEntry.Result(standing, standing, true, true, true, new G.Root(new G.Id(550, 43, 312), standing, "legacy43"), null); }, out _, out var why));
        Assert.Equal(1, calls); Assert.Equal("MissingOrAmbiguousInteriorEntryOrientation", why);
    }
    [Fact] public void MissingReciprocalIsRejectedEvenWhenTheRemainingCountedTableIsCoherent()
    {
        var d = Doc(); var reverse = d.Portals.Single(p => p.SourceRegion == 43 && p.TargetRegion == 125);
        // The source43 table has one record; removing it leaves a valid empty table rather than an inflated count.
        d.Portals = d.Portals.Where(p => !ReferenceEquals(p, reverse)).ToArray();
        Assert.Equal("InteriorPortalReciprocal", Refuses(d));
    }
    [Fact] public void WrongReferencedEdgeIsRejectedWithReciprocityAndRawRecordIdentityIntact()
    {
        var d = Doc(); var a = d.Portals.Single(p => p.SourceRegion == 125 && p.TargetRegion == 43); var b = d.Portals.Single(p => p.SourceRegion == 43 && p.TargetRegion == 125);
        a.TargetEdge = 255; b.SourceEdge = 255; Rehash(a); Rehash(b); Assert.Equal("InteriorPortalEdge", Refuses(d));
    }
    [Fact] public void EndpointsMustBeTheOriginalReferencedInavEdges()
    {
        var d = Doc(); var a = d.Portals.Single(p => p.SourceRegion == 125 && p.TargetRegion == 43); a.Endpoints[0][0]++; Rehash(a);
        Assert.Equal("InteriorPortalEndpoints", Refuses(d));
    }
    [Fact] public void CoherentOtherFloorCannotJoinTheInteriorComponent()
    {
        var d = Doc(); var r = d.Regions.Single(r => r.Id == 43);
        foreach (var p in r.Polygons) { p.Plane[3] += 400; foreach (var e in p.Edges) e.Vertex[1] += 400; }
        Assert.Equal("InteriorPortalFloor", Refuses(d));
    }
    [Fact] public void InflatedCountAndTruncatedRawRecordAreRejected()
    {
        var d = Doc(); d.Portals.Single(p => p.SourceRegion == 43 && p.TargetRegion == 125).Count++;
        Assert.Equal("InteriorPortalCount", Refuses(d));
        d = Doc(); d.Portals[0].Raw = d.Portals[0].Raw.Substring(2); Assert.Equal("InteriorPortalTableBounds", Refuses(d));
    }
    [Fact] public void SourceRecordHashAndNativeParentTransformAreBothRequired()
    {
        var d = Doc(); Group(d, 0x226005f5).Occurrences[0].RecordSha256 = new string('0', 64); Assert.Equal("InteriorNativeRecordIdentity", Refuses(d));
        d = Doc(); Group(d, 0x226005f5).Occurrences[0].Position[0]++; Assert.Equal("InteriorNativeParentTransform", Refuses(d));
    }
    [Fact] public void WrongWorldTypeRawSignatureAndMissingRequiredDoorCannotAuthorizeAnNpc()
    {
        Assert.Throws<InvalidDataException>(() => E.Read(TaborJson.Value, 2, 550)); E index = Tabor(); var entry = Entry(index);
        var marker = OphiuchiMarker(); marker.metrId = 2; Assert.False(index.TryNpcRoute(entry, marker, Planned(), Wood, out _, out _));
        marker = OphiuchiMarker(); marker.type = BitConverter.GetBytes(346u); Assert.False(index.TryNpcRoute(entry, marker, Planned(), Wood, out _, out _));
        marker = OphiuchiMarker(); marker.pos_x++; Assert.False(index.TryNpcRoute(entry, marker, Planned(), Wood, out _, out _));
        Assert.False(index.TryNpcRoute(entry, OphiuchiMarker(), Planned(), _ => null!, out _, out var why)); Assert.StartsWith("UnsupportedRequiredPortalDoor:2260056b", why);
        Assert.False(index.TryNpcRoute(Entry(Tabor()), OphiuchiMarker(), Planned(), Wood, out _, out why)); Assert.Equal("InvalidInteriorEntryOwner", why);
    }
    [Fact] public void ExactPlannedMarkerIdentityRefusesAmbiguousRawVariantsButDeduplicatesIdenticalRows()
    {
        var d = Doc(); var g = Group(d, 0x226005f5); var extra = JsonSerializer.Deserialize<E.Legacy>(JsonSerializer.Serialize(g.Legacy[0]))!; extra.Signature.Quaternion = "0000000000000000000000000000803f"; g.Legacy = g.Legacy.Append(extra).ToArray();
        E index = Index(d); var a = OphiuchiMarker(); var b = OphiuchiMarker(); b.quat = extra.Signature.Quaternion;
        Assert.False(index.TryMarkerForSpot(new[] { a, b }, Planned(), out _, out var why)); Assert.Equal("MissingOrAmbiguousPlannedNpcMarker", why);
        Assert.True(index.TryMarkerForSpot(new[] { a, OphiuchiMarker() }, Planned(), out var selected, out why), why); Assert.Same(a, selected);
    }
    [Fact] public void WrongRoomStandingDoesNotBorrowAnotherConnectedRoom()
    {
        var d = Doc(); var g = Group(d, 0x226005f5); g.Legacy[0].Signature.Position = new[] { 39850d, 6145, 25550 };
        E index = Index(d); var marker = OphiuchiMarker(); marker.pos_x = 39850; marker.pos_z = 25550;
        Assert.False(index.TryNpcRoute(Entry(index), marker, new[] { 39850d, 6095, 25550, marker.rot }, Wood, out _, out var why)); Assert.Equal("NpcStandingOutsideOriginalRoom", why);
    }
    [Fact] public void RetainedMissingPrefabBoundaryPreservesOrdinalButNeverBecomesARoute()
    {
        // The actual world2 preflight retained missingRID8a000000; this is an explicit synthetic missing-region control, not a claim Tabor's43 is absent.
        var d = Doc(); var r = d.Regions.Single(r => r.Id == 43); int ordinal = r.Ordinal;
        r.Available = false; r.Refusal = "MissingPrefab"; r.PrefabRid = 0x8a000000; r.PrefabSha256 = null!; r.Polygons = Array.Empty<E.Polygon>();
        E index = Index(d); Assert.Equal(8, ordinal); Assert.Equal(157, d.Regions.Length); Assert.Equal((ushort)125, Entry(index).Node.Region);
        Assert.False(index.TryNpcRoute(Entry(index), OphiuchiMarker(), Planned(), Wood, out _, out var why)); Assert.Equal("MissingOrAmbiguousNpcOriginalRegion", why);
    }
    [Fact] public void OwnGoidCannotBorrowAPairedOccurrenceWhenItsOwnRecordIsAbsent()
    {
        var d = Doc(); var g = Group(d, 0x2260056b); g.Occurrences = g.Occurrences.Where(o => o.Goid != 346).ToArray(); E index = Index(d);
        Assert.False(index.TryWoodDoor(Wood(0x2260056b)!, out _, out var why)); Assert.Equal("AmbiguousOwnGoidDoorOrientation", why);
    }
    [Fact] public void PayloadAndManifestIntegrityRefuseMissingTruncatedAndCoherentlyReplacedData()
    {
        byte[] compressed = File.ReadAllBytes(Path.Combine(DirectoryPath, "1349.json.gz")); string hash = Convert.ToHexString(SHA256.HashData(compressed));
        byte[] changed = (byte[])compressed.Clone(); changed[^1] ^= 1; Assert.Throws<InvalidDataException>(() => E.LoadVerified(changed, hash, 1, 1349));
        Assert.ThrowsAny<Exception>(() => E.LoadVerified(compressed[..20], Convert.ToHexString(SHA256.HashData(compressed[..20])), 1, 1349));
        string tempRoot = Path.GetFullPath(Path.GetTempPath()), temp = Path.Combine(tempRoot, "mxo-interior-evidence-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            File.Copy(Path.Combine(DirectoryPath, "manifest.json"), Path.Combine(temp, "manifest.json"));
            Assert.Throws<FileNotFoundException>(() => E.LoadAudited(temp, 1, 550));
            File.WriteAllText(Path.Combine(temp, "manifest.json"), File.ReadAllText(Path.Combine(temp, "manifest.json")).Replace("\"World\": 1", "\"World\": 2"));
            Assert.Throws<InvalidDataException>(() => E.LoadAudited(temp, 2, 550));
        }
        finally { Assert.StartsWith(tempRoot, Path.GetFullPath(temp), StringComparison.OrdinalIgnoreCase); Directory.Delete(temp, true); }
    }
}
