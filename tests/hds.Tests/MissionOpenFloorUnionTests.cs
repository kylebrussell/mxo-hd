using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorUnionTests
{
    private const string Q="0000000000000000000000000000803f";
    private static G.Point[] Rect(double x1,double z1,double x2,double z2)=>new[]{new G.Point(x1,z1),new G.Point(x2,z1),new G.Point(x2,z2),new G.Point(x1,z2)};
    private static G.Surface Floor(ushort id,double x1,double z1,double x2,double z2,double floor=0)=>new(new(127,1,id),Rect(x1,z1,x2,z2),0,0,floor);
    private static G.Request Request(G.Surface[] surfaces,double halfX=60,double halfZ=20)=>new(1,G.SourceModelPresence.Model6Absent,"literal positive absent Model6",new(new(100,145,0),Q,1,1415),new(new(100,0,0),Q,1,1415),"literal audited source",
        new OpenFloorModelBounds(3,new(-halfX,-10,-halfZ),new(halfX,10,halfZ),"literal support and footprint"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal initial native profile"),surfaces,new(new(127,1,1),new(25,95,-70),"literal standing root"),new(0,200,-100,100),new(1,true,new(0,200,-100,100),-100,1000,Array.Empty<G.StaticObstacle>(),"complete literal domains only"));
    [Fact]
    public void FullFootprintCanCrossARealCoplanarConnectedSubdivisionAndFixedReplayRetainsIt()
    {
        var request=Request(new[]{Floor(1,0,-100,100,100),Floor(2,100,-100,200,100)});var result=G.Plan(request);
        Assert.True(result.HasPlan,result.Refusal);Assert.Equal(100,result.Centre.X);
        Assert.True(result.Base.Min(p=>p.X)<100&&result.Base.Max(p=>p.X)>100);
        // Independent rectangle union: every footprint point and the whole convex rectangle lie in the gapless floor.
        Assert.All(result.Base,p=>{Assert.InRange(p.X,0,200);Assert.InRange(p.Z,-100,100);});
        Assert.True(G.ValidateFixed(request,result.InputFingerprint,result.Surface,result.Centre,result.Route,out string refusal),refusal);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void MissingStripOrDifferentFloorCannotBeBridgedByTheFootprint(bool differentFloor)
    {
        var request=Request(new[]{Floor(1,0,-100,95,100),Floor(2,differentFloor?95:105,-100,200,100,differentFloor?1:0)});
        Assert.False(G.Plan(request).HasPlan);
    }
    [Fact]
    public void ConnectedFloorAroundAnInteriorHoleDoesNotCertifyTheHoleAsCovered()
    {
        var request=Request(new[]{Floor(1,0,-100,95,100),Floor(2,95,-100,105,-5),Floor(3,95,5,105,100),Floor(4,105,-100,200,100)},65,65);
        Assert.False(G.Plan(request).HasPlan);
    }
    [Fact]
    public void LiteralSafeCellFindsAdjacentRepresentableInteriorWithoutRelaxingItsBoundary()
    {
        var cell=Rect(28439.400628754905,14898.586512409138,28503.413482125026,14899.865045067421);
        var method=typeof(G).GetMethod("RepresentableNear",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        var value=(G.Point?)method.Invoke(null,new object[]{cell,new G.Point(29100,14900)});Assert.True(value.HasValue);
        Assert.InRange(value.Value.X,28439.400628754905,28503.413482125026);Assert.InRange(value.Value.Z,14898.586512409138,14899.865045067421);
        Assert.Equal((double)(float)value.Value.X,value.Value.X);Assert.Equal((double)(float)value.Value.Z,value.Value.Z);
        Assert.True((double)(float)28503.413482125026>28503.413482125026); // retained actual nearest-rounding failure
    }
}
