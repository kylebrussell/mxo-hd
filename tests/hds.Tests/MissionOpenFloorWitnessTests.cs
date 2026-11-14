using HeadlessClient;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace hds.Tests;
public class MissionOpenFloorWitnessTests
{
    private const string Literal="""
    {"Policy":1,"World":1,"Mission":2201,"Phase":0,"ObjectId":1,"SourceKey":219152446,"SourceGoid":1410,"ConcreteGoid":552,
     "RawFingerprint":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
     "InputFingerprint":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
     "Quaternion":"00000000f304353f00000000f20435bf","Centre":[28950,2050,14950],"Standing":[28950,2095,14850],
     "Route":[28800,2095,14700,28950,2095,14850],"Surface":[209,2,8],"RootSurface":[209,2,8],"RouteSurfaces":[209,2,8]}
    """;
    private static MissionOpenFloorWitness Read(string s=Literal){using var doc=JsonDocument.Parse(s);return MissionOpenFloorWitness.Read(doc.RootElement);}
    [Fact]public void LiteralPublishedWitnessSurvivesDocumentDisposalAndKeepsExactCoordinates()
    {
        var w=Read();w.Require(2201,0,1,0x0d10003e,1410,552);
        Assert.Equal(new[]{28950d,2050d,14950d},w.Centre);Assert.Equal(new[]{28950d,2095d,14850d},w.Standing);
        Assert.Equal("00000000f304353f00000000f20435bf",w.Quaternion);
        Assert.Throws<NotSupportedException>(()=>((IList<double>)w.Centre)[0]=1);
        Assert.Contains("SourceKey",w.RawJson);
    }
    [Theory][InlineData("Policy",2)][InlineData("SourceGoid",0)][InlineData("World",0)][InlineData("Phase",-1)]
    public void UnsupportedOrZeroIdentityRefuses(string field,int value)
    {var n=JsonNode.Parse(Literal)!;n[field]=value;Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));}
    [Fact]public void WrongMissionObjectCannotBeAcceptedFromHistoricalCheckpoint()
    {
        var w=Read();Assert.Throws<FormatException>(()=>w.Require(2201,1,1,0x0d10003e,1410,552));
        Assert.Throws<FormatException>(()=>w.Require(2201,0,2,0x0d10003e,1410,552));
        Assert.Throws<FormatException>(()=>w.Require(2201,0,1,0x0d10003f,1410,552));
    }
    [Fact]public void MissingDuplicateTruncatedAndNonfiniteFieldsRefuseWithoutCoordinateFallback()
    {
        var n=JsonNode.Parse(Literal)!;n.AsObject().Remove("Standing");Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));
        n=JsonNode.Parse(Literal)!;n["Centre"]=new JsonArray(1,2);Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));
        n=JsonNode.Parse(Literal)!;n["Quaternion"]="0000807f00000000000000000000803f";Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));
        Assert.Throws<FormatException>(()=>Read(Literal.Replace("\"Policy\":1","\"Policy\":1,\"Policy\":1")));
        n=JsonNode.Parse(Literal)!;n["Standing"]=new JsonArray(28800,2095,14700);Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));
    }
    [Fact]public void CanonicalSectorZeroStaticZeroKeyRemainsValid()
    {
        var n=JsonNode.Parse(Literal)!;n["SourceKey"]=0;var w=Read(n.ToJsonString());
        w.Require(2201,0,1,0,1410,552);Assert.Equal(0u,w.SourceKey);
    }
    [Fact]public void SegmentCountAndRootIdentityAreStrictWithoutPlanningOrFallback()
    {
        var n=JsonNode.Parse(Literal)!;n["Route"]=new JsonArray(28950,2095,14850);
        Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));
        n=JsonNode.Parse(Literal)!;n["RouteSurfaces"]=new JsonArray(209,2,8,209,2,10);
        Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));
        n=JsonNode.Parse(Literal)!;n["RouteSurfaces"]=new JsonArray(209,2,10);
        Assert.Throws<FormatException>(()=>Read(n.ToJsonString()));
        // Two literal segments require three points and two surface identities.
        n=JsonNode.Parse(Literal)!;n["Route"]=new JsonArray(28800,2095,14700,28850,2095,14750,28950,2095,14850);
        n["RouteSurfaces"]=new JsonArray(209,2,8,209,2,10);
        Assert.Equal(9,Read(n.ToJsonString()).Route.Count);
    }
    [Fact]public void BoundSourceHeaderWitnessDistinguishesIdentityAndFreeProxy()
    {
        byte[] bound=Convert.FromHexString("01000882053E00100D07CDAB0004000002");
        Assert.Equal(new[]{0},MissionOpenFloorWitness.SourceBindingHeaders(bound,1410,0x0d10003e));
        Assert.Empty(MissionOpenFloorWitness.SourceBindingHeaders(bound,1407,0x0d10003e));
        Assert.Empty(MissionOpenFloorWitness.SourceBindingHeaders(bound,1410,0x0d10003f));
        bound[2]=0x0c;Assert.Empty(MissionOpenFloorWitness.SourceBindingHeaders(bound,1410,0x0d10003e));
        Assert.Empty(MissionOpenFloorWitness.SourceBindingHeaders(bound[..10],1410,0x0d10003e));
    }
}
