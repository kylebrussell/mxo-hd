using System.Reflection;
using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorClearanceReuseTests
{
    private const string Q="0000000000000000000000000000803f";
    private const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
    private static G.Point[] Rect(double left,double right,double bottom=-100,double top=100)=>new[]{new G.Point(left,bottom),new G.Point(right,bottom),new G.Point(right,top),new G.Point(left,top)};
    private static G.Request Input(double floor=0,double support=-10,double maxY=10,G.StaticObstacle[]? obstacles=null,bool hole=false)
    {
        var id=new G.Id(127,1,1);var surfaces=hole?new[]{new G.Surface(id,Rect(-100,-1),0,0,floor)}:
            new[]{new G.Surface(id,Rect(-100,0),0,0,floor),new G.Surface(new(127,1,2),Rect(0,100),0,0,floor)};
        return new G.Request(1,G.SourceModelPresence.Model6Absent,"literal absence",new(new(0,floor+145,0),Q,1,1415),new(new(0,floor,0),Q,1,1415),"literal source",
            new OpenFloorModelBounds(3,new(-10,support,-10),new(10,maxY,10),support,"literal envelope"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal400"),surfaces,
            new(id,new(-80,floor+95,0),"literal root"),new(-100,100,-100,100),new(1,true,new(-100,100,-100,100),floor-100,floor+1000,obstacles??Array.Empty<G.StaticObstacle>(),"literal complete scene"));
    }
    private static object Prepare(G.Request r)
    {var tuple=typeof(G).GetMethod("Prepare",Flags)!.Invoke(null,new object[]{r})!;return tuple.GetType().GetField("Item1")!.GetValue(tuple)!;}
    private static object Field(object c,string name)=>c.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!;
    private static void Equivalent(G.Request r,G.Position position,bool expected)
    {
        var c=Prepare(r);Assert.NotNull(c);var surface=r.Surfaces.Where(s=>position.X>=s.Vertices.Min(p=>p.X)&&position.X<=s.Vertices.Max(p=>p.X)).OrderBy(s=>s.Identity).First();
        var method=typeof(G).GetMethod("GeometryClearCore",Flags)!;object[] oldArgs={c,surface,position,null!,false},newArgs={c,surface,position,null!,true};
        Assert.Equal(expected,(bool)method.Invoke(null,oldArgs)!);Assert.Equal(expected,(bool)method.Invoke(null,newArgs)!);
        static long[] Bits(object shape)=>((G.Point[])shape).SelectMany(p=>new[]{BitConverter.DoubleToInt64Bits(p.X),BitConverter.DoubleToInt64Bits(p.Z)}).ToArray();
        Assert.Equal(Bits(oldArgs[3]),Bits(newArgs[3]));
    }
    [Fact] public void NavUnionSeamIsStillCoveredAndMissingNeighbourIsStillRefused()
    {Equivalent(Input(),new(0,10,0),true);Equivalent(Input(hole:true),new(-5,10,0),false);}
    [Theory] [InlineData(10,false)] [InlineData(10.0001,true)]
    public void ClosedContactAndSeparateFootprintsKeepTheLiteralPredicate(double left,bool clear)
    {Equivalent(Input(obstacles:new[]{new G.StaticObstacle("contact",99,1407,0,50,Rect(left,20,-10,10),"literal bound")}),new(0,10,0),clear);}
    [Fact] public void NativeCastYCanAddAnObstacleAbsentFromOriginalVerticalProfile()
    {
        const double floor=16777216;var r=Input(floor,-0.6,1,new[]{new G.StaticObstacle("cast-only",99,1407,floor-0.3,floor-0.2,Rect(-5,5,-5,5),"literal lower cast-only bound")});
        var c=Prepare(r);Assert.Empty((G.Point[][])Field(c,"OriginalGeometryFootprints"));Assert.Single((G.Point[][])Field(c,"CastGeometryFootprints"));
        Equivalent(r,new(0,r.Model.CentreY(floor),0),false);
    }
    [Fact] public void ToleratedCentreYThatCastsDifferentlyRecomputesTheAllSceneUnion()
    {
        const double floor=64,support=-0.000003814697265625;var r=Input(floor,support,1,new[]{new G.StaticObstacle("perturbed-cast-only",99,1407,65.000006,65.0000065,Rect(-5,5,-5,5),"literal upper cast-only bound")});
        var c=Prepare(r);Assert.Empty((G.Point[][])Field(c,"CastGeometryFootprints"));double y=r.Model.CentreY(floor)+0.00000005;
        Assert.NotEqual((float)r.Model.CentreY(floor),(float)y);Equivalent(r,new(0,y,0),false);
    }
}
