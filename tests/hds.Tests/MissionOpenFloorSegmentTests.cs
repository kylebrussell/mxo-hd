using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorSegmentTests
{
    private static G.Point[] Rect(double x,double right,double bottom=0,double top=10)=>new[]{new G.Point(x,bottom),new G.Point(right,bottom),new G.Point(right,top),new G.Point(x,top)};
    [Fact] public void ArtificialSubdivisionSeamsCoverBothDirectionsAndNativeCast()
    {
        var cells=new[]{Rect(0,5),Rect(5,10)};G.Point a=new(1,3),b=new(9,7);
        Assert.True(G.SegmentCovered(a,b,cells));Assert.True(G.SegmentCovered(b,a,cells));
        Assert.True(G.SegmentCovered(new((float)a.X,(float)a.Z),new((float)b.X,(float)b.Z),cells));
    }
    [Theory] [InlineData(10,0.00001)] [InlineData(10000000,0.01)]
    public void RealGapIsNotBridgedByNormalizedParameterTolerance(double length,double gap)
    {
        var cells=new[]{Rect(0,length/2),Rect(length/2+gap,length)};
        Assert.False(G.SegmentCovered(new(1,5),new(length-1,5),cells));
        Assert.False(G.SegmentCovered(new(length-1,5),new(1,5),cells));
    }
    [Fact] public void HoleAndDisconnectedSpaceRemainUncovered()
    {
        var cells=new[]{Rect(0,4),Rect(6,10),Rect(4,6,0,4),Rect(4,6,6,10)};
        Assert.False(G.SegmentCovered(new(1,5),new(9,5),cells));
        Assert.True(G.SegmentCovered(new(1,2),new(9,2),cells));
        Assert.False(G.SegmentCovered(new(1,5),new(9,5),Array.Empty<G.Point[]>()));
    }
}
