using System.Reflection;
using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;

public class MissionCellPortalAllocationTests
{
    private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly Type Cell=typeof(G).GetNestedType("Cell",BindingFlags.NonPublic)!;
    private static readonly MethodInfo CellPortal=typeof(G).GetMethod("CellPortal",Flags)!;
    private static readonly MethodInfo SurfacePortal=typeof(G).GetMethods(Flags).Single(m=>m.Name=="Portal"&&m.GetParameters().Length==2);
    private static G.Point[] Rect(double x,double z,double w,double h)=>new[]{new G.Point(x,z),new G.Point(x+w,z),new G.Point(x+w,z+h),new G.Point(x,z+h)};
    // Literal pre-optimization loop, independent of either production Portal overload.
    private static IEnumerable<(G.Point A,G.Point B)> OldEdges(IReadOnlyList<G.Point> p)
    {for(int i=0;i<p.Count;i++)yield return(p[i],p[(i+1)%p.Count]);}
    private static G.Point Sub(G.Point a,G.Point b)=>new(a.X-b.X,a.Z-b.Z);
    private static double Cross(G.Point a,G.Point b)=>a.X*b.Z-a.Z*b.X;
    private static G.Point? OldLoop(G.Surface a,G.Surface b)
    {
        if(a.FloorC!=b.FloorC||!(a.Indoors==b.Indoors&&(!a.Indoors||(a.Identity.Sector==b.Identity.Sector&&a.Identity.Region==b.Identity.Region))))return null;
        foreach(var e in OldEdges(a.Vertices))
        {
            G.Point d=Sub(e.B,e.A);double length=Math.Sqrt(d.X*d.X+d.Z*d.Z),n=length*length;
            foreach(var f in OldEdges(b.Vertices))
            {
                G.Point u=Sub(f.A,e.A),v=Sub(f.B,e.A);
                if(Math.Abs(Cross(d,u))>G.Epsilon*length||Math.Abs(Cross(d,v))>G.Epsilon*length)continue;
                double du=(u.X*d.X+u.Z*d.Z)/n,dv=(v.X*d.X+v.Z*d.Z)/n;
                double lo=Math.Max(0,Math.Min(du,dv)),hi=Math.Min(1,Math.Max(du,dv));
                if((hi-lo)*length>G.Epsilon){double t=(lo+hi)/2;return new(e.A.X+t*d.X,e.A.Z+t*d.Z);}
            }
        }
        return null;
    }
    private static object MakeCell(G.Surface surface,G.Point[] polygon)
    {
        object cell=Activator.CreateInstance(Cell,true)!;
        Cell.GetField("Surface",Flags)!.SetValue(cell,surface);
        Cell.GetField("Polygon",Flags)!.SetValue(cell,polygon);
        return cell;
    }
    private static void EqualToOldCopies(G.Surface a,G.Point[] ap,G.Surface b,G.Point[] bp)
    {
        var beforeA=ap.ToArray();var beforeB=bp.ToArray();
        object? expected=OldLoop(new G.Surface(a.Identity,ap,0,0,a.FloorC,a.Indoors),new G.Surface(b.Identity,bp,0,0,b.FloorC,b.Indoors));
        object? actual=CellPortal.Invoke(null,new[]{MakeCell(a,ap),MakeCell(b,bp)});
        Assert.Equal(expected,actual);Assert.Equal(beforeA,ap);Assert.Equal(beforeB,bp);
    }
    [Fact] public void Actual301206SectorGeometryMatchesTheIndependentOldLoop()
    {
        string repo=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(repo,"hds.sln")))repo=Path.GetDirectoryName(repo)!;
        SectorProperties.Load(Path.Combine(repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(repo,"data/navmesh"));
        Assert.True(NavMesh.TryOpenFloorSurfaces(1,new ushort[]{1171},out var surfaces,out string why),why);
        Assert.True(MissionOpenFloorScenes.TryLoad(1,1171,out var scene,out why),why);Assert.True(scene.Complete);
        var sample=surfaces.Where(s=>s.Flat).GroupBy(s=>(s.FloorC,s.Indoors,s.Identity.Region)).OrderByDescending(g=>g.Count()).First().Take(32).ToArray();
        int portals=0;
        foreach(var a in sample)foreach(var b in sample.Where(b=>!a.Identity.Equals(b.Identity)))
        {
            var expected=OldLoop(a,b);var actual=SurfacePortal.Invoke(null,new object[]{a,b});Assert.Equal((object?)expected,actual);
            EqualToOldCopies(a,a.Vertices.ToArray(),b,b.Vertices.ToArray());if(expected.HasValue)portals++;
        }
        Assert.True(portals>0);
    }
    [Fact] public void ClippedCellsMatchOldFrozenCopiesInBothDirectionsAtContactAndEpsilon()
    {
        var a=new G.Surface(new(209,83,1),Rect(0,0,20,20),0,0,0,true);
        var b=new G.Surface(new(209,83,2),Rect(20,0,20,20),0,0,0,true);
        foreach(double shift in new[]{-G.Epsilon,0,G.Epsilon,2*G.Epsilon,1d})
        foreach(double height in new[]{G.Epsilon/2,G.Epsilon,2*G.Epsilon,10d})
        {
            var ap=Rect(0,0,20,height);var bp=Rect(20+shift,0,20,height);
            EqualToOldCopies(a,ap,b,bp);EqualToOldCopies(b,bp,a,ap);
            EqualToOldCopies(a,ap.Reverse().ToArray(),b,bp.Reverse().ToArray());
        }
    }
    [Fact] public void IdentityFloorAndIndoorBoundariesStillMatchOldCopies()
    {
        var polygon=Rect(0,0,20,20);var neighbour=Rect(20,0,20,20);
        foreach(bool indoors in new[]{false,true})
        foreach(var id in new[]{new G.Id(209,83,2),new G.Id(209,84,2),new G.Id(210,83,2)})
        foreach(double floor in new[]{0d,G.Epsilon,95d})
        foreach(bool otherIndoors in new[]{false,true})
            EqualToOldCopies(new(new(209,83,1),polygon,0,0,0,indoors),polygon,new(id,neighbour,0,0,floor,otherIndoors),neighbour);
    }
    [Fact] public void LiteralThinAndLargeFloatCoordinatePolygonsMatchOldCopies()
    {
        var a=Rect(25939.998046875,14200,0.001953125,30);var b=Rect(25940,14200,100,30);
        EqualToOldCopies(new(new(209,83,1),a,0,0,2000,true),a,new(new(209,83,2),b,0,0,2000,true),b);
        var random=new Random(301206);
        for(int i=0;i<200;i++)
        {
            double x=random.Next(-60000,60000),z=random.Next(-60000,60000),w=random.Next(1,300),h=random.Next(1,300);
            a=Rect(x,z,w,h);b=Rect(x+w,z+random.Next(-300,300),random.Next(1,300),random.Next(1,300));
            EqualToOldCopies(new(new(209,83,1),a,0,0,0,true),a,new(new(209,83,2),b,0,0,0,true),b);
        }
    }
    [Fact] public void PairCallsAvoidTheTwoSurfaceAndFrozenPolygonCopies()
    {
        var ap=Rect(0,0,20,20);var bp=Rect(20,0,20,20);
        var a=new G.Surface(new(209,83,1),ap,0,0,0,true);var b=new G.Surface(new(209,83,2),bp,0,0,0,true);
        object[] cells={MakeCell(a,ap),MakeCell(b,bp)};object[] surfaces={a,b};
        CellPortal.Invoke(null,cells);SurfacePortal.Invoke(null,surfaces);
        const int count=1000;
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<count;i++)CellPortal.Invoke(null,cells);
        long direct=GC.GetAllocatedBytesForCurrentThread()-start;
        start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<count;i++)
        {
            surfaces[0]=new G.Surface(a.Identity,ap,0,0,a.FloorC,a.Indoors);surfaces[1]=new G.Surface(b.Identity,bp,0,0,b.FloorC,b.Indoors);
            SurfacePortal.Invoke(null,surfaces);
        }
        long copied=GC.GetAllocatedBytesForCurrentThread()-start;
        Assert.True(copied-direct>=128L*count,$"Direct {direct}; old copies {copied}");
    }
}
