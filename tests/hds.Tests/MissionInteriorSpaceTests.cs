using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionInteriorSpaceTests
{
    private const string Q="0000000000000000000000000000803f";
    private static G.Point[] Rect(double a,double b)=>new[]{new G.Point(a,-300),new G.Point(b,-300),new G.Point(b,300),new G.Point(a,300)};
    private static G.Request Input(bool indoors)
    {
        var left=new G.Surface(new(127,1,1),Rect(-500,0),0,0,0,indoors);
        var right=new G.Surface(new(127,2,1),Rect(0,500),0,0,0,indoors);
        return new G.Request(1,G.SourceModelPresence.Model6Absent,"literal absence",new(new(100,145,0),Q,1,1415),new(new(100,0,0),Q,1,1415),"literal anchor",
            new OpenFloorModelBounds(3,new(-10,-10,-10),new(10,10,10),"literal body"),
            MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal range"),new[]{left,right},
            new(left.Identity,new(-300,95,0),"literal root"),new(30,500,-300,300),new(1,true,new(-500,500,-300,300),-100,1000,Array.Empty<G.StaticObstacle>(),"literal complete scene"));
    }
    [Fact] public void TouchingIndoorPrefabsDoNotMakeAWalkablePortalOrFixedRoute()
    {
        var r=Input(true);Assert.False(G.Plan(r).HasPlan);
        Assert.True(G.TryReachableRegion(r.World,r.Surfaces,r.Entry,r.Scene,out var region,out string why),why);
        Assert.True(region.Contains(new(-100,95,0)));Assert.False(region.Contains(new(100,95,0)));
        Assert.False(G.ValidateRouteIds(r,new[]{r.Surfaces[0].Identity,r.Surfaces[1].Identity},new[]{new G.Position(-300,95,0),new(0,95,0),new(100,95,0)}));
        var outdoors=Input(false);Assert.True(G.Plan(outdoors).HasPlan);
    }
    [Fact] public void OccupiedSpawnSpreadUsesTheSameConstrainedProjector()
    {
        int calls=0;NavPoint? Project(double[] p,double radius){calls++;return new NavPoint{x=p[0],y=95,z=p[2]};}
        Assert.True(MissionPlacement.Resolve(1,new[]{0d,95,0},null,Array.Empty<double[]>(),false,out var point,out _,
            new[]{new[]{0d,95,0}},admissible:p=>Math.Abs(p.x)<=500&&Math.Abs(p.z)<=500,projector:Project));
        Assert.True(calls>1);Assert.True(point.x*point.x+point.z*point.z>=10000);
    }
    [Fact] public void RepeatedMarkersCannotReserveAnOccupiedPointAsAdditionalCapacity()
    {
        NavPoint? Constant(double[] p,double radius)=>new NavPoint{x=0,y=95,z=0};
        Assert.False(MissionPlacement.Resolve(1,new[]{0d,95,0},null,Array.Empty<double[]>(),false,out _,out _,
            new[]{new[]{0d,95,0}},projector:Constant));
    }
}
