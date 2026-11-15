using System.IO.Compression;
using System.Text.Json;
using hds;
using E = hds.MissionInteriorEvidence;
namespace hds.Tests;

public class MissionInteriorFormat2Tests
{
    private static string Repo { get { string p = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(p, "hds", "Hardline Dreams MxO server.csproj"))) p = Path.GetDirectoryName(p)!; return p; } }
    private static E.Document Document(ushort sector)
    {
        using var file = File.OpenRead(Path.Combine(Repo, "data", "mission_interior_evidence_v2", "1", sector + ".json.gz"));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<E.Document>(gzip)!;
    }
    private static E Read(E.Document document) => E.Read(JsonSerializer.SerializeToUtf8Bytes(document), document.World, document.Sector);
    [Fact] public void Sector20RetainsNativeVertexBitsInsteadOfReevaluatingRoundedWorldPlane()
    {
        E.Document d = Document(20); E.Polygon p = d.Regions.Single(r => r.Id == 14).Polygons[10];
        E.Edge edge = p.Edges.Single(e => e.Vertex.SequenceEqual(new double[] { -55730, -4001.304443359375, -30986.95703125 }));
        double worldFloor = -(p.Plane[0] * edge.Vertex[0] + p.Plane[2] * edge.Vertex[2] + p.Plane[3]) / p.Plane[1];
        Assert.Equal(0xc57a14dfu, BitConverter.SingleToUInt32Bits((float)edge.Vertex[1]));
        Assert.Equal(0xc57a14deu, BitConverter.SingleToUInt32Bits((float)worldFloor));
        Read(d); // Raw local plane, edge and exact native arithmetic prove this vertex.
        edge.Vertex[1] = (float)worldFloor;
        Assert.Equal("InteriorRawVertexTransform", Assert.Throws<InvalidDataException>(() => Read(d)).Message);
    }
    [Fact] public void Sector81PreservesAuthoredSubsegmentWithoutAuthorizingAFullEdge()
    {
        E.Document d = Document(81); E.Portal portal = d.Portals.Single(p => p.RecordOffset == 476797);
        Assert.Equal((31, 15, 0, 43, 1, 2), ((int)portal.SourceRegion, (int)portal.SourcePolygon, (int)portal.SourceEdge, (int)portal.TargetRegion, (int)portal.TargetPolygon, (int)portal.TargetEdge));
        Assert.Equal("1f000f00002b00010002afc5e9450080a2450070e9450080a245", portal.Raw);
        Assert.Equal("PartialPortal", portal.Transition); Read(d);
        portal.Transition = "SameFloor";
        Assert.Equal("InteriorPortalClassification", Assert.Throws<InvalidDataException>(() => Read(d)).Message);
    }
    [Fact] public void CountedEmptyInavRemainsAnOriginalZeroCapacityRegion()
    {
        E.Document d = Document(20); E.Region region = d.Regions.Single(r => r.Id == 29);
        Assert.Equal(16, region.Ordinal); Assert.Equal(2449475864u, region.PrefabRid);
        Assert.Equal("494e4156040000000400008100000000", region.RawInavHeader);
        Assert.Empty(region.Polygons); Read(d);
        byte[] raw = Convert.FromHexString(region.RawInavHeader); raw[12] = 1; region.RawInavHeader = Convert.ToHexString(raw);
        Assert.Equal("InteriorRawINAVCount", Assert.Throws<InvalidDataException>(() => Read(d)).Message);
    }
    [Fact] public void EmptyRegionPortalIsUnusableRatherThanAnIndexingFailure()
    {
        E.Document d = Document(20);
        E.Portal portal = d.Portals.First(p => p.DoorKeys.Length == 0 && p.SourceRegion != 29 && p.TargetRegion != 29);
        d.Portals = new[] { portal };
        foreach (E.Region region in d.Regions)
        {
            region.PortalCount = region.Id == portal.SourceRegion ? 1 : 0;
            region.RawPortalCount = Convert.ToHexString(BitConverter.GetBytes((uint)region.PortalCount));
        }
        portal.Count = 1; portal.RecordOffset = portal.CountOffset + 4;
        portal.TargetRegion = 29; portal.TargetPolygon = 0; portal.TargetEdge = 0;
        byte[] raw = Convert.FromHexString(portal.Raw); BitConverter.GetBytes(portal.TargetRegion).CopyTo(raw, 5); BitConverter.GetBytes((ushort)0).CopyTo(raw, 7); raw[9] = 0;
        portal.Raw = Convert.ToHexString(raw); portal.RecordSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw));
        portal.Transition = "EmptyRegion"; Read(d);
        portal.Transition = "SameFloor";
        Assert.Equal("InteriorPortalClassification", Assert.Throws<InvalidDataException>(() => Read(d)).Message);
    }
    [Theory] [InlineData("Offset")] [InlineData("Header")] [InlineData("Index")] [InlineData("Transform")]
    public void RawSourceAndTransformTamperingRefuses(string mutation)
    {
        E.Document d = Document(20); E.Region region = d.Regions.First(r => r.Available); E.Polygon p = region.Polygons[0];
        switch (mutation)
        {
            case "Offset": p.SourceOffset++; break;
            case "Header": p.RawHeader = p.RawHeader.Substring(2); break;
            case "Index": p.Edges[0].Neighbor = ushort.MaxValue; p.Edges[0].RegionTag ^= 1; break;
            case "Transform": region.Pivot[0]++; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(d));
    }
}
