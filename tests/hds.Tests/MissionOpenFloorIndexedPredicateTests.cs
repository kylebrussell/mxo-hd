using System.Reflection;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorIndexedPredicateTests
{
    private const double Epsilon=1e-7;
    private static G.Point Sub(G.Point a,G.Point b)=>new(a.X-b.X,a.Z-b.Z);
    private static double Cross(G.Point a,G.Point b)=>a.X*b.Z-a.Z*b.X;
    private static double Distance(G.Point a,G.Point b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
    private static IEnumerable<(G.Point A,G.Point B)> Edges(IReadOnlyList<G.Point> p)
    {for(int i=0;i<p.Count;i++)yield return(p[i],p[(i+1)%p.Count]);}
    // Literal pre-optimization predicates, including IEEE/empty-sequence behavior.
    private static bool OldInside(IReadOnlyList<G.Point> p,G.Point q)=>Edges(p).All(e=>Cross(Sub(e.B,e.A),Sub(q,e.A))>=-Epsilon);
    private static bool OldTouches(G.Point a,G.Point b,IReadOnlyList<G.Point> shape)
    {
        double lo=0,hi=1;foreach(var e in Edges(shape))
        {
            G.Point edge=Sub(e.B,e.A);double start=Cross(edge,Sub(a,e.A))+Epsilon*Math.Max(1,Distance(e.A,e.B)),change=Cross(edge,Sub(b,a));
            if(change==0){if(start<0)return false;}
            else if(change>0)lo=Math.Max(lo,-start/change);else hi=Math.Min(hi,-start/change);
            if(lo>hi)return false;
        }
        return true;
    }
    private static bool OldOverlap(G.Point[] a,G.Point[] b)=>Edges(a).Any(e=>OldTouches(e.A,e.B,b))||Edges(b).Any(e=>OldTouches(e.A,e.B,a));
    [Fact] public void IndexedPredicatesMatchLiteralLegacyBoundaryShortEdgeAndIeeeBehavior()
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var inside=typeof(G).GetMethod("Inside",flags)!.CreateDelegate<Func<IReadOnlyList<G.Point>,G.Point,bool>>();
        var touches=typeof(G).GetMethod("Touches",flags)!.CreateDelegate<Func<G.Point,G.Point,IReadOnlyList<G.Point>,bool>>();
        var overlap=typeof(G).GetMethod("Overlap",flags)!.CreateDelegate<Func<G.Point[],G.Point[],bool>>();
        G.Point[][] shapes={Array.Empty<G.Point>(),new[]{new G.Point(0,0)},new[]{new G.Point(-1,-1),new G.Point(1,-1),new G.Point(1,1),new G.Point(-1,1)},
            new[]{new G.Point(0,0),new G.Point(1e-10,0),new G.Point(1e-10,1),new G.Point(0,1)},
            new[]{new G.Point(double.NaN,0),new G.Point(1,0),new G.Point(0,1)},new[]{new G.Point(double.PositiveInfinity,0),new G.Point(1,0),new G.Point(0,1)}};
        G.Point[] points={new(0,0),new(1,0),new(1+1e-8,0),new(1+1e-5,0),new(-2,0),new(double.NaN,0),new(double.PositiveInfinity,1)};
        foreach(var shape in shapes)foreach(var a in points)
        {
            Assert.Equal(OldInside(shape,a),inside(shape,a));
            foreach(var b in points){Assert.Equal(OldTouches(a,b,shape),touches(a,b,shape));Assert.Equal(OldTouches(b,a,shape),touches(b,a,shape));}
        }
        foreach(var a in shapes)foreach(var b in shapes)Assert.Equal(OldOverlap(a,b),overlap(a,b));
    }
}
