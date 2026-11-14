using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace hds
{
    /// <summary>Pure measured-model open-floor geometry. No runtime caller, chooser, SQL, views or static mutation.
    /// Port of reviewed offline planner396a3c37; point paths are not avatar/scene/use-range guarantees.</summary>
    internal static class MissionOpenFloorResolver
    {
        internal const double Epsilon = 1e-7;
        internal const int PolicyVersion = 1;
        internal readonly struct Point
        {
            public double X { get; }
            public double Z { get; }
            public Point(double x,double z) { X=x;Z=z; }
        }
        internal readonly struct Position
        {
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public Position(double x,double y,double z) { X=x;Y=y;Z=z; }
            public Point Xz => new Point(X,Z);
        }
        internal readonly struct Id : IComparable<Id>, IEquatable<Id>
        {
            public ushort Sector { get; }
            public ushort Region { get; }
            public ushort Polygon { get; }
            public Id(ushort sector,ushort region,ushort polygon) { Sector=sector;Region=region;Polygon=polygon; }
            public int CompareTo(Id b) => (Sector,Region,Polygon).CompareTo((b.Sector,b.Region,b.Polygon));
            public bool Equals(Id b) => CompareTo(b)==0;
            public override bool Equals(object other) => other is Id id && Equals(id);
            public override int GetHashCode() => HashCode.Combine(Sector,Region,Polygon);
        }
        internal sealed class Surface
        {
            public Id Identity { get; }
            public IReadOnlyList<Point> Vertices { get; }
            public double FloorX { get; }
            public double FloorZ { get; }
            public double FloorC { get; }
            public Surface(Id id,IEnumerable<Point> vertices,double floorX,double floorZ,double floorC)
            { Identity=id;Vertices=Freeze(vertices);FloorX=floorX;FloorZ=floorZ;FloorC=floorC; }
            public bool Flat => FloorX==0 && FloorZ==0;
        }
        internal sealed class RawPose
        {
            public Position Position { get; }
            public string QuaternionHex { get; }
            public uint SourceKey { get; }
            public ushort SourceGoid { get; }
            public RawPose(Position position,string quaternionHex,uint sourceKey=0,ushort sourceGoid=0)
            { Position=position;QuaternionHex=quaternionHex;SourceKey=sourceKey;SourceGoid=sourceGoid; }
        }
        internal sealed class Root
        {
            public Id Surface { get; }
            public Position Standing { get; }
            public string Evidence { get; }
            public Root(Id surface,Position standing,string evidence) { Surface=surface;Standing=standing;Evidence=evidence; }
        }
        internal readonly struct Bounds
        {
            public double MinX { get; }
            public double MaxX { get; }
            public double MinZ { get; }
            public double MaxZ { get; }
            public Bounds(double minX,double maxX,double minZ,double maxZ) { MinX=minX;MaxX=maxX;MinZ=minZ;MaxZ=maxZ; }
            public bool Valid => Finite(MinX,MaxX,MinZ,MaxZ) && MinX<MaxX && MinZ<MaxZ;
            public Point[] Polygon => new[] { new Point(MinX,MinZ),new Point(MaxX,MinZ),new Point(MaxX,MaxZ),new Point(MinX,MaxZ) };
        }
        internal enum SourceModelPresence { Unproven=0, Model6Absent=1, NoModel=Model6Absent, AuthoredStaticBounds=2, MeasuredModel=AuthoredStaticBounds }
        internal sealed class StaticObstacle
        {
            public string Identity {get;} public uint? Key {get;} public ushort? Goid {get;} public double MinY {get;} public double MaxY {get;} public string Evidence {get;}
            public IReadOnlyList<Point> Footprint {get;}
            public StaticObstacle(string identity,uint? key,ushort? goid,double minY,double maxY,IEnumerable<Point> polygon,string evidence)
            {Identity=identity;Key=key;Goid=goid;MinY=minY;MaxY=maxY;Footprint=Freeze(Canonical(polygon??Array.Empty<Point>()));Evidence=evidence;}
        }
        internal sealed class StaticScene
        {
            public ushort World {get;} public bool Complete {get;} public Bounds Coverage {get;} public double MinY {get;} public double MaxY {get;} public string Evidence {get;} public IReadOnlyList<StaticObstacle> Obstacles {get;}
            public string Fingerprint=>Hash(new{World,Complete,Coverage.MinX,Coverage.MaxX,Coverage.MinZ,Coverage.MaxZ,MinY,MaxY,Evidence,Obstacles});
            public StaticScene(ushort world,bool complete,Bounds coverage,double minY,double maxY,IEnumerable<StaticObstacle> obstacles,string evidence)
            {World=world;Complete=complete;Coverage=coverage;MinY=minY;MaxY=maxY;Obstacles=Freeze((obstacles??Array.Empty<StaticObstacle>()).OrderBy(o=>o?.Identity,StringComparer.Ordinal));Evidence=evidence;}
        }
        internal sealed class Request
        {
            public ushort World {get;} public SourceModelPresence SourceModel {get;} public string SourceModelEvidence {get;} public RawPose RawSource {get;} public RawPose Anchor {get;} public string AnchorEvidence {get;}
            public OpenFloorModelBounds Model {get;} public MissionOpenFloorUsableRange.Profile Usable {get;}
            public IReadOnlyList<Surface> Surfaces {get;} public Root Entry {get;} public Bounds SiteBounds {get;} public StaticScene Scene {get;}
            public string Fingerprint=>Hash(new{Policy=PolicyVersion,World,SourceModel,SourceModelEvidence,RawSource,Anchor,AnchorEvidence,Model,Usable,Entry,SiteBounds.MinX,SiteBounds.MaxX,SiteBounds.MinZ,SiteBounds.MaxZ,
                Scene=Scene.Fingerprint,Surfaces=Surfaces.OrderBy(s=>s.Identity).ToArray()});
            public Request(ushort world,SourceModelPresence sourceModel,string sourceModelEvidence,RawPose rawSource,RawPose anchor,string anchorEvidence,OpenFloorModelBounds model,MissionOpenFloorUsableRange.Profile usable,
                IEnumerable<Surface> surfaces,Root entry,Bounds siteBounds,StaticScene scene)
            {World=world;SourceModel=sourceModel;SourceModelEvidence=sourceModelEvidence;RawSource=rawSource;Anchor=anchor;AnchorEvidence=anchorEvidence;Model=model;Usable=usable;Surfaces=Freeze(surfaces??Array.Empty<Surface>());Entry=entry;SiteBounds=siteBounds;Scene=scene;}
        }
        internal sealed class Result
        {
            public bool HasPlan=>Refusal==null; public string Refusal {get;} public Request Input {get;} public string InputFingerprint {get;}
            public Position Centre {get;} public Position Standing {get;} public Id Surface {get;} public IReadOnlyList<Point> Base {get;}
            public IReadOnlyList<Position> Route {get;} public IReadOnlyList<Id> RoutePolygons {get;}
            public double NumericalGuard {get;} public MissionOpenFloorUsableRange.Witness Range {get;} public MissionOpenFloorUsableRange.Witness NativeCastRange {get;}
            public bool NativeWalkingProven=>false; public bool NativePickingProven=>false;
            internal Result(string refusal,Request input){Refusal=refusal;Input=input;}
            internal Result(Request input,Surface surface,Point centre,Point[] shape,Position[] route,Id[] ids,double guard,MissionOpenFloorUsableRange.Witness range)
            {Input=input;InputFingerprint=input.Fingerprint;Surface=surface.Identity;Centre=new Position(centre.X,input.Model.CentreY(surface.FloorC),centre.Z);Base=Freeze(shape);Route=Freeze(route);RoutePolygons=Freeze(ids);Standing=route.Last();NumericalGuard=guard;Range=range;NativeCastRange=MissionOpenFloorUsableRange.Test(input.Usable,Native(Cast(Standing)),Native(Cast(Centre)),new OpenFloorOrientation(input.Anchor.QuaternionHex));}
        }
        private sealed class Candidate
        {internal Surface Surface;internal Point Centre;internal Point[] Shape;internal Position[] Route;internal Id[] Path;internal double Distance;internal MissionOpenFloorUsableRange.Witness Range;}
        private sealed class Cell
        {internal Surface Surface;internal Point[] Polygon;internal int Index;}
        private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
        private static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
        private static bool Finite(params double[] values) => values.All(double.IsFinite);
        private static Point Sub(Point a,Point b) => new Point(a.X-b.X,a.Z-b.Z);
        private static double Cross(Point a,Point b) => a.X*b.Z-a.Z*b.X;
        private static double Distance(Point a,Point b) { var d=Sub(a,b);return Math.Sqrt(d.X*d.X+d.Z*d.Z); }
        private static IEnumerable<(Point A,Point B)> Edges(IReadOnlyList<Point> p)
        { for(int i=0;i<p.Count;i++)yield return (p[i],p[(i+1)%p.Count]); }
        internal static double Area(IReadOnlyList<Point> p) => Edges(p).Sum(e=>Cross(e.A,e.B))/2;
        private static bool Inside(IReadOnlyList<Point> p,Point q) => Edges(p).All(e=>Cross(Sub(e.B,e.A),Sub(q,e.A))>=-Epsilon);
        private static bool Convex(IReadOnlyList<Point> p) => p.Count>=3 && p.All(v=>Finite(v.X,v.Z)) && double.IsFinite(Area(p)) && Area(p)>Epsilon &&
            Edges(p).All(e=>Distance(e.A,e.B)>Epsilon && p.All(v=>Cross(Sub(e.B,e.A),Sub(v,e.A))>=-Epsilon));
        private static OpenFloorPoint Native(Position p)=>new OpenFloorPoint(p.X,p.Y,p.Z);
        private static Point[] Offsets(Request request)
        {
            if(request?.Anchor==null||request.RawSource==null||request.Model==null||request.Usable==null||(string.IsNullOrWhiteSpace(request.AnchorEvidence)||string.IsNullOrWhiteSpace(request.SourceModelEvidence)))throw new ArgumentException("MissingAuditedInputs");
            if(request.World==0||request.RawSource.SourceGoid==0||request.Model.Goid!=request.Usable.Goid||request.RawSource.SourceKey!=request.Anchor.SourceKey||request.RawSource.SourceGoid!=request.Anchor.SourceGoid)throw new ArgumentException("MismatchedAuditedInputs");
            if(!Finite(request.Anchor.Position.X,request.Anchor.Position.Y,request.Anchor.Position.Z,request.RawSource.Position.X,request.RawSource.Position.Y,request.RawSource.Position.Z))throw new ArgumentException("InvalidAnchor");
            var q=new OpenFloorOrientation(request.Anchor.QuaternionHex);if(!q.YawOnly)throw new ArgumentException("TiltedQuaternion");
            var corners=request.Model.Corners(new OpenFloorPoint(0,0,0),q);
            if(corners.Any(p=>!float.IsFinite((float)p.X)||!float.IsFinite((float)p.Y)||!float.IsFinite((float)p.Z)))throw new ArgumentException("UnrepresentableModelBounds");
            return Hull(corners.Select(p=>new Point(p.X,p.Z)));
        }
        private static Point[] Clip(Point[] polygon,Point a,Point b,double limit)
        {
            var result=new List<Point>();Point edge=Sub(b,a);
            foreach(var pair in Edges(polygon))
            {
                double sv=Cross(edge,Sub(pair.A,a))-limit,su=Cross(edge,Sub(pair.B,a))-limit;
                if(sv>=-Epsilon)result.Add(pair.A);
                if((sv>=0)!=(su>=0)) { double t=sv/(sv-su);result.Add(new Point(pair.A.X+t*(pair.B.X-pair.A.X),pair.A.Z+t*(pair.B.Z-pair.A.Z))); }
            }
            var clean=new List<Point>();foreach(Point p in result)if(clean.Count==0||Distance(p,clean[clean.Count-1])>Epsilon)clean.Add(p);
            if(clean.Count>1&&Distance(clean[0],clean[clean.Count-1])<=Epsilon)clean.RemoveAt(clean.Count-1);
            return clean.Count<3||Math.Abs(Area(clean))<=Epsilon?Array.Empty<Point>():clean.ToArray();
        }
        private static Point[] Erode(IReadOnlyList<Point> polygon,Point[] offsets)
        {
            Point[] result=polygon.ToArray();foreach(var e in Edges(polygon))
            { Point edge=Sub(e.B,e.A);result=Clip(result,e.A,e.B,-offsets.Min(p=>Cross(edge,p)));if(result.Length==0)break; }
            return result;
        }
        private static Point Nearest(IReadOnlyList<Point> p,Point q)
        {
            if(Inside(p,q))return q;
            return Edges(p).Select(e=>
            {
                Point d=Sub(e.B,e.A);double n=d.X*d.X+d.Z*d.Z;
                double t=Math.Clamp(((q.X-e.A.X)*d.X+(q.Z-e.A.Z)*d.Z)/n,0,1);
                return new Point(e.A.X+t*d.X,e.A.Z+t*d.Z);
            }).OrderBy(v=>Distance(v,q)).ThenBy(v=>v.X).ThenBy(v=>v.Z).First();
        }
        private static Point? Portal(Surface a,Surface b)
        {
            if(a.FloorC!=b.FloorC)return null;
            foreach(var e in Edges(a.Vertices))
            {
                Point d=Sub(e.B,e.A);double length=Distance(e.A,e.B),n=length*length;
                foreach(var f in Edges(b.Vertices))
                {
                    Point u=Sub(f.A,e.A),v=Sub(f.B,e.A);
                    if(Math.Abs(Cross(d,u))>Epsilon*length||Math.Abs(Cross(d,v))>Epsilon*length)continue;
                    double du=(u.X*d.X+u.Z*d.Z)/n,dv=(v.X*d.X+v.Z*d.Z)/n;
                    double lo=Math.Max(0,Math.Min(du,dv)),hi=Math.Min(1,Math.Max(du,dv));
                    if((hi-lo)*length>Epsilon) { double t=(lo+hi)/2;return new Point(e.A.X+t*d.X,e.A.Z+t*d.Z); }
                }
            }
            return null;
        }
        private static bool Touches(Point a,Point b,IReadOnlyList<Point> shape)
        {
            double lo=0,hi=1;foreach(var e in Edges(shape))
            {
                Point edge=Sub(e.B,e.A);double start=Cross(edge,Sub(a,e.A))+Epsilon*Math.Max(1,Distance(e.A,e.B)),change=Cross(edge,Sub(b,a));
                if(change==0) { if(start<0)return false; }
                else if(change>0)lo=Math.Max(lo,-start/change);else hi=Math.Min(hi,-start/change);
                if(lo>hi)return false;
            }
            return true;
        }
        private static bool Clear(Point[] route,Point[] shape) => route.All(p=>!Touches(p,p,shape)) &&
            Enumerable.Range(0,Math.Max(0,route.Length-1)).All(i=>!Touches(route[i],route[i+1],shape));

        private static Point[] Canonical(IEnumerable<Point> points)
        {
            var a=points.ToArray();if(a.Length==0)return a;if(Area(a)<0)Array.Reverse(a);
            int start=Enumerable.Range(0,a.Length).OrderBy(i=>a[i].X).ThenBy(i=>a[i].Z).First();
            return Enumerable.Range(0,a.Length).Select(i=>a[(start+i)%a.Length]).ToArray();
        }
        private static Point[] Hull(IEnumerable<Point> values)
        {
            var points=values.DistinctBy(p=>(p.X,p.Z)).OrderBy(p=>p.X).ThenBy(p=>p.Z).ToArray();if(points.Length<3)return Array.Empty<Point>();
            var a=new List<Point>();foreach(var p in points){while(a.Count>=2&&Cross(Sub(a[^1],a[^2]),Sub(p,a[^1]))<=0)a.RemoveAt(a.Count-1);a.Add(p);}
            int lower=a.Count;foreach(var p in points.Reverse().Skip(1)){while(a.Count>lower&&Cross(Sub(a[^1],a[^2]),Sub(p,a[^1]))<=0)a.RemoveAt(a.Count-1);a.Add(p);}a.RemoveAt(a.Count-1);return a.ToArray();
        }
        private static IEnumerable<Point[]> Subtract(Point[] region,Point[] obstacle,double guard)
        {
            // Avoid partitioning a region already strictly outside the numerical guard band.
            if(Edges(obstacle).Any(e=>region.All(p=>Cross(Sub(e.B,e.A),Sub(p,e.A)) < -guard*Distance(e.A,e.B)-Epsilon))) {yield return region;yield break;}
            Point[] remaining=region;foreach(var e in Edges(obstacle))
            {
                if(remaining.Length==0)yield break;double length=Distance(e.A,e.B);
                Point[] outside=Clip(remaining,e.B,e.A,guard*length);if(outside.Length>0)yield return outside;
                remaining=Clip(remaining,e.A,e.B,-guard*length);
            }
        }
        private static List<Point[]> Difference(IEnumerable<Point[]> regions,IEnumerable<Point[]> obstacles,double guard)
        {
            var result=regions.ToList();foreach(var obstacle in obstacles){result=result.SelectMany(r=>Subtract(r,obstacle,guard)).ToList();if(result.Count==0)break;}return result;
        }
        private static bool Overlap(Point[] a,Point[] b)=>Edges(a).Any(e=>Touches(e.A,e.B,b))||Edges(b).Any(e=>Touches(e.A,e.B,a));
        private static bool Fits(Point[] shape,Surface surface,Bounds bounds)=>shape.All(p=>Inside(surface.Vertices,p)&&Inside(bounds.Polygon,p));
        private static Point Rounded(Point p)=>new Point((float)p.X,(float)p.Z);
        private static Position Cast(Position p)=>new Position((float)p.X,(float)p.Y,(float)p.Z);
        private static double Guard(Request r)
        {
            double[] n=r.Surfaces.SelectMany(s=>s.Vertices.SelectMany(p=>new[]{p.X,p.Z})).Concat(new[]{r.Anchor.Position.X,r.Anchor.Position.Y,r.Anchor.Position.Z}).ToArray();
            double ulp=n.Max(v=>{float f=(float)v;return Math.Max(Math.Abs((double)MathF.BitIncrement(f)-f),Math.Abs((double)f-MathF.BitDecrement(f)));});
            return Math.Max(Epsilon*8,ulp*2);
        }
        private static bool RouteRelevant(StaticObstacle o,double floor)=>o.MinY<=Math.Max(floor+95,(float)(floor+95))+Epsilon&&o.MaxY>=Math.Min(floor,(float)floor)-Epsilon;
        private static bool VolumeRelevant(StaticObstacle o,double min,double max)=>o.MinY<=max+Epsilon&&o.MaxY>=min-Epsilon;
        private static List<Cell> StandingCells(Surface[] component,IEnumerable<Point[]> obstacles,double guard)
        {
            var cells=new List<Cell>();foreach(var surface in component)
                foreach(var polygon in Difference(new[]{surface.Vertices.ToArray()},obstacles,guard))cells.Add(new Cell{Surface=surface,Polygon=polygon,Index=cells.Count});
            return cells;
        }
        private static Point? CellPortal(Cell a,Cell b)
        {
            return Portal(new Surface(a.Surface.Identity,a.Polygon,0,0,a.Surface.FloorC),new Surface(b.Surface.Identity,b.Polygon,0,0,b.Surface.FloorC));
        }
        private static bool Standing(Request r,Surface[] component,Point[] shape,Position centre,double guard,out Position[] route,out Id[] path,out MissionOpenFloorUsableRange.Witness range)
        {
            route=null;path=null;range=default;double floor=r.Entry.Standing.Y-95;
            var obstacles=r.Scene.Obstacles.Where(o=>RouteRelevant(o,floor)).Select(o=>o.Footprint.ToArray()).Append(shape).ToArray();
            var cells=StandingCells(component,obstacles,guard);var start=cells.FirstOrDefault(c=>c.Surface.Identity.Equals(r.Entry.Surface)&&Inside(c.Polygon,r.Entry.Standing.Xz));if(start==null)return false;
            var previous=new Dictionary<int,int?>{{start.Index,null}};var portals=new Dictionary<int,Point>();var queue=new Queue<Cell>();queue.Enqueue(start);
            while(queue.Count>0)
            {
                var current=queue.Dequeue();foreach(var other in cells){if(previous.ContainsKey(other.Index))continue;var p=CellPortal(current,other);if(p.HasValue){previous[other.Index]=current.Index;portals[other.Index]=p.Value;queue.Enqueue(other);}}
            }
            var q=new OpenFloorOrientation(r.Anchor.QuaternionHex);var possibilities=new List<(Cell Cell,Point Point,MissionOpenFloorUsableRange.Witness Range)>();
            foreach(var cell in cells.Where(c=>previous.ContainsKey(c.Index)))
            {
                Point p=Rounded(Nearest(cell.Polygon,centre.Xz));if(!Inside(cell.Polygon,p))continue;
                var standing=new Position(p.X,floor+95,p.Z);
                var w=MissionOpenFloorUsableRange.Test(r.Usable,Native(standing),Native(centre),q);
                var cast=MissionOpenFloorUsableRange.Test(r.Usable,Native(Cast(standing)),Native(Cast(centre)),q);if(w.Within&&cast.Within)possibilities.Add((cell,p,w));
            }
            foreach(var candidate in possibilities.OrderBy(c=>Distance(c.Point,centre.Xz)).ThenBy(c=>c.Cell.Surface.Identity).ThenBy(c=>c.Point.X).ThenBy(c=>c.Point.Z).ThenBy(c=>c.Cell.Index))
            {
                var ids=new List<int>{candidate.Cell.Index};while(previous[ids[^1]].HasValue)ids.Add(previous[ids[^1]].Value);ids.Reverse();
                var points=new[]{r.Entry.Standing.Xz}.Concat(ids.Skip(1).Select(i=>Rounded(portals[i]))).Append(candidate.Point).ToArray();
                bool nav=true;for(int i=0;i<ids.Count;i++)if(!Inside(cells[ids[i]].Polygon,points[i])||!Inside(cells[ids[i]].Polygon,points[i+1]))nav=false;
                if(points.Any(p=>!Inside(r.Scene.Coverage.Polygon,p)||!Inside(r.Scene.Coverage.Polygon,Rounded(p))))continue;
                if(!nav||obstacles.Any(o=>!Clear(points,o))||obstacles.Any(o=>!Clear(points.Select(Rounded).ToArray(),o)))continue;
                route=points.Select(p=>new Position(p.X,floor+95,p.Z)).ToArray();path=ids.Select(i=>cells[i].Surface.Identity).ToArray();range=candidate.Range;return true;
            }
            return false;
        }
        internal static bool TryResolve(Request request,out Result result){result=Plan(request);return result.HasPlan;}
        private sealed class Context
        {
            internal Request Input;internal Point[] Offsets;internal Surface Start;internal Surface[] Component;
            internal double Guard,CentreY,MinY,MaxY;internal StaticObstacle[] Relevant;internal Point[][] Forbidden;
        }
        private static (Context Context,string Refusal) Prepare(Request r)
        {
            (Context,string) No(string reason)=>(null,reason);Point[] offsets;
            try{offsets=Offsets(r);}catch(ArgumentException ex){return No(ex.Message);}
            if(r.Entry==null||string.IsNullOrWhiteSpace(r.Entry.Evidence)||!Finite(r.Entry.Standing.X,r.Entry.Standing.Y,r.Entry.Standing.Z))return No("MissingFiniteRoot");
            if(!r.SiteBounds.Valid||r.Scene==null||!r.Scene.Complete||r.Scene.World!=r.World||!r.Scene.Coverage.Valid||!Finite(r.Scene.MinY,r.Scene.MaxY)||r.Scene.MinY>=r.Scene.MaxY||string.IsNullOrWhiteSpace(r.Scene.Evidence))return No("MissingStaticCoverage");
            if(!Inside(r.Scene.Coverage.Polygon,r.Entry.Standing.Xz)||r.SiteBounds.Polygon.Any(p=>!Inside(r.Scene.Coverage.Polygon,p)))return No("IncompleteStaticEnvelope");
            if(r.Surfaces==null||r.Surfaces.Any(s=>s==null||!Finite(s.FloorX,s.FloorZ,s.FloorC))||r.Surfaces.Select(s=>s.Identity).Distinct().Count()!=r.Surfaces.Count)return No("InvalidSurfaceSnapshot");
            if(r.Scene.Obstacles.Any(o=>o==null||!Finite(o.MinY,o.MaxY)||o.MinY>o.MaxY||!Convex(o.Footprint)||string.IsNullOrWhiteSpace(o.Evidence)||string.IsNullOrWhiteSpace(o.Identity))||r.Scene.Obstacles.Select(o=>o.Identity).Distinct(StringComparer.Ordinal).Count()!=r.Scene.Obstacles.Count)return No("UnprovenStaticBounds");
            if(r.SourceModel==SourceModelPresence.Unproven||(r.SourceModel==SourceModelPresence.MeasuredModel&&!r.Scene.Obstacles.Any(o=>o.Key==r.RawSource.SourceKey&&o.Goid==r.RawSource.SourceGoid)))return No("MissingSourceModelEvidence");
            Surface start=r.Surfaces.FirstOrDefault(s=>s.Identity.Equals(r.Entry.Surface));if(start==null||!start.Flat||!Convex(start.Vertices)||Math.Abs(r.Entry.Standing.Y-start.FloorC-95)>Epsilon||!Inside(start.Vertices,r.Entry.Standing.Xz))return No("InvalidStandingRoot");
            var all=r.Surfaces.Where(s=>s.Flat&&s.FloorC==start.FloorC&&Convex(s.Vertices)).OrderBy(s=>s.Identity).ToArray();var found=new HashSet<Id>{start.Identity};var queue=new Queue<Surface>();queue.Enqueue(start);
            while(queue.Count>0){var current=queue.Dequeue();foreach(var s in all)if(!found.Contains(s.Identity)&&Portal(current,s).HasValue){found.Add(s.Identity);queue.Enqueue(s);}}
            var component=all.Where(s=>found.Contains(s.Identity)).ToArray();double guard=Guard(r);if(!double.IsFinite(guard))return No("UnrepresentableCoordinates");
            double centreY=r.Model.CentreY(start.FloorC),minY=centreY+r.Model.Min.Y,maxY=centreY+r.Model.Max.Y;
            if(r.Scene.MinY>Math.Min(start.FloorC,minY)-guard||r.Scene.MaxY<Math.Max(maxY,start.FloorC+95)+guard)return No("IncompleteStaticVerticalEnvelope");
            var relevant=r.Scene.Obstacles.Where(o=>VolumeRelevant(o,minY,maxY)).ToArray();
            var forbidden=relevant.Select(o=>Hull(o.Footprint.SelectMany(p=>offsets.Select(d=>new Point(p.X-d.X,p.Z-d.Z))))).ToArray();
            return(new Context{Input=r,Offsets=offsets,Start=start,Component=component,Guard=guard,CentreY=centreY,MinY=minY,MaxY=maxY,Relevant=relevant,Forbidden=forbidden},null);
        }
        private static bool GeometryClear(Context c,Surface surface,Position position,out Point[] shape)
        {
            shape=Array.Empty<Point>();var r=c.Input;
            if(surface==null||!Finite(position.X,position.Y,position.Z)||Math.Abs(position.Y-c.CentreY)>Epsilon||
                position.X!=(double)(float)position.X||position.Z!=(double)(float)position.Z)return false;
            shape=c.Offsets.Select(p=>new Point(position.X+p.X,position.Z+p.Z)).ToArray();
            var fixedShape=shape;if(!Fits(fixedShape,surface,r.SiteBounds)||c.Relevant.Any(o=>Overlap(fixedShape,o.Footprint.ToArray())))return false;
            var cast=Cast(position);var castShape=c.Offsets.Select(p=>new Point(cast.X+p.X,cast.Z+p.Z)).ToArray();
            return Fits(castShape,surface,r.SiteBounds)&&!r.Scene.Obstacles.Where(o=>VolumeRelevant(o,Math.Min(c.MinY,cast.Y+r.Model.Min.Y),Math.Max(c.MaxY,cast.Y+r.Model.Max.Y))).Any(o=>Overlap(castShape,o.Footprint.ToArray()));
        }
        private static bool RouteValid(Context c,Position centre,Point[] shape,IReadOnlyList<Position> route)
        {
            var r=c.Input;if(route==null||route.Count<2||route.Any(p=>!Finite(p.X,p.Y,p.Z)||Math.Abs(p.Y-c.Start.FloorC-95)>Epsilon)||
                Distance(route[0].Xz,r.Entry.Standing.Xz)>Epsilon||Math.Abs(route[0].Y-r.Entry.Standing.Y)>Epsilon)return false;
            var points=route.Select(p=>p.Xz).ToArray();var cast=points.Select(Rounded).ToArray();
            if(points.Concat(cast).Any(p=>!Inside(r.Scene.Coverage.Polygon,p)))return false;
            var obstacles=r.Scene.Obstacles.Where(o=>RouteRelevant(o,c.Start.FloorC)).Select(o=>o.Footprint.ToArray()).Append(shape).ToArray();
            if(obstacles.Any(o=>!Clear(points,o)||!Clear(cast,o)))return false;
            var cells=StandingCells(c.Component,obstacles,c.Guard);
            for(int i=0;i<points.Length-1;i++)
                if(!cells.Any(cell=>Inside(cell.Polygon,points[i])&&Inside(cell.Polygon,points[i+1])&&Inside(cell.Polygon,cast[i])&&Inside(cell.Polygon,cast[i+1])))return false;
            var q=new OpenFloorOrientation(r.Anchor.QuaternionHex);
            return MissionOpenFloorUsableRange.Test(r.Usable,Native(route[^1]),Native(centre),q).Within&&
                MissionOpenFloorUsableRange.Test(r.Usable,Native(Cast(route[^1])),Native(Cast(centre)),q).Within;
        }
        // Replays fixed geometry only; never invokes nearest-centre selection or chooses a replacement.
        internal static bool ValidateFixed(Request r,string expectedInputFingerprint,Id surface,Position centre,IReadOnlyList<Position> route,out string refusal)
        {
            var p=Prepare(r);refusal=p.Refusal;if(p.Context==null)return false;var c=p.Context;
            if(string.IsNullOrEmpty(expectedInputFingerprint)||expectedInputFingerprint!=r.Fingerprint){refusal="ChangedFixedPlacementInputs";return false;}
            if(!GeometryClear(c,c.Component.FirstOrDefault(s=>s.Identity.Equals(surface)),centre,out var shape)){refusal="InvalidFixedGeometry";return false;}
            if(!RouteValid(c,centre,shape,route)){refusal="InvalidFixedStandingRouteOrRange";return false;}return true;
        }
        internal static bool ValidateSet(IReadOnlyList<Result> results,out string refusal)
        {
            refusal=null;if(results==null||results.Count==0||results.Any(r=>r==null||!r.HasPlan)){refusal="IncompletePlacementSet";return false;}
            var first=results[0].Input;
            if(results.Select(r=>r.Input.RawSource.SourceKey).Distinct().Count()!=results.Count||results.Any(r=>r.Input.World!=first.World||r.Input.Scene.Fingerprint!=first.Scene.Fingerprint||
                !r.Input.Entry.Surface.Equals(first.Entry.Surface)||r.Input.Entry.Standing.X!=first.Entry.Standing.X||r.Input.Entry.Standing.Y!=first.Entry.Standing.Y||r.Input.Entry.Standing.Z!=first.Entry.Standing.Z))
            {refusal="PlacementSetScopeMismatch";return false;}
            foreach(var r in results)if(!ValidateFixed(r.Input,r.InputFingerprint,r.Surface,r.Centre,r.Route,out refusal))return false;
            for(int i=0;i<results.Count;i++)for(int j=0;j<results.Count;j++)
            {
                if(i==j)continue;var a=results[i];var b=results[j];double minA=a.Centre.Y+a.Input.Model.Min.Y,maxA=a.Centre.Y+a.Input.Model.Max.Y,
                    minB=b.Centre.Y+b.Input.Model.Min.Y,maxB=b.Centre.Y+b.Input.Model.Max.Y;
                if(i<j&&minA<=maxB+Epsilon&&minB<=maxA+Epsilon&&Overlap(a.Base.ToArray(),b.Base.ToArray())){refusal="OverlappingResolvedObjects";return false;}
                if(minB<=a.Standing.Y+Epsilon&&maxB>=minA-Epsilon&&(!Clear(a.Route.Select(p=>p.Xz).ToArray(),b.Base.ToArray())||!Clear(a.Route.Select(p=>Rounded(p.Xz)).ToArray(),b.Base.ToArray())))
                {refusal="ResolvedObjectBlocksAnotherRoute";return false;}
            }
            return true;
        }
        internal static Result Plan(Request r)
        {
            Result No(string reason)=>new Result(reason,r);var prepared=Prepare(r);if(prepared.Context==null)return No(prepared.Refusal);
            var context=prepared.Context;var offsets=context.Offsets;var start=context.Start;var component=context.Component;double guard=context.Guard,centreY=context.CentreY,maxY=context.MaxY;
            var relevant=context.Relevant;var forbidden=context.Forbidden;
            var candidates=new List<Candidate>();foreach(var surface in component)
            {
                Point[] region=Erode(surface.Vertices,offsets);Point[] allowed=Erode(r.SiteBounds.Polygon,offsets);foreach(var e in Edges(allowed))region=Clip(region,e.A,e.B,0);
                if(allowed.Length==0||region.Length==0)continue;
                foreach(var cell in Difference(new[]{region},forbidden,guard))
                {
                    Point centre=Rounded(Nearest(cell,r.Anchor.Position.Xz));if(!Inside(cell,centre))continue;
                    var position=new Position(centre.X,centreY,centre.Z);if(!GeometryClear(context,surface,position,out var shape))continue;
                    if(!Standing(r,component,shape,position,guard,out var route,out var path,out var range)||!RouteValid(context,position,shape,route))continue;
                    candidates.Add(new Candidate{Surface=surface,Centre=centre,Shape=shape,Route=route,Path=path,Range=range,Distance=Distance(centre,r.Anchor.Position.Xz)});
                }
            }
            var best=candidates.OrderBy(c=>c.Distance).ThenBy(c=>c.Surface.Identity).ThenBy(c=>c.Centre.X).ThenBy(c=>c.Centre.Z).FirstOrDefault();
            return best==null?No("NoClearFootprintAndInRangeReachableStandingPoint"):new Result(r,best.Surface,best.Centre,best.Shape,best.Route,best.Path,guard,best.Range);
        }
    }
}
