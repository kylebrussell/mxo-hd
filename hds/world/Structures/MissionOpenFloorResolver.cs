using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        internal const int PolicyVersion = 3;
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
            private readonly Lazy<string> fingerprint;
            public string Fingerprint=>fingerprint.Value;
            public StaticScene(ushort world,bool complete,Bounds coverage,double minY,double maxY,IEnumerable<StaticObstacle> obstacles,string evidence)
            {World=world;Complete=complete;Coverage=coverage;MinY=minY;MaxY=maxY;Obstacles=Freeze((obstacles??Array.Empty<StaticObstacle>()).OrderBy(o=>o?.Identity,StringComparer.Ordinal));Evidence=evidence;fingerprint=new Lazy<string>(()=>Hash(new{World,Complete,Coverage.MinX,Coverage.MaxX,Coverage.MinZ,Coverage.MaxZ,MinY,MaxY,Evidence,Obstacles}));}
        }
        internal sealed class Reservation
        {
            public ushort World {get;} public ushort ObjectId {get;} public uint SourceKey {get;} public string SceneFingerprint {get;} public string PriorInputFingerprint {get;}
            public double MinY {get;} public double MaxY {get;} public double Floor {get;} public Id RootSurface {get;} public Position Root {get;}
            public IReadOnlyList<Point> Base {get;} public IReadOnlyList<Position> Route {get;} public bool ProtectRoute {get;}
            private Reservation(Result result,ushort objectId,bool protectRoute)
            {
                World=result.Input.World;ObjectId=objectId;SourceKey=result.Input.RawSource.SourceKey;SceneFingerprint=result.Input.Scene.Fingerprint;PriorInputFingerprint=result.InputFingerprint;
                MinY=Math.Min(result.Centre.Y,(float)result.Centre.Y)+result.Input.Model.Min.Y;MaxY=Math.Max(result.Centre.Y,(float)result.Centre.Y)+result.Input.Model.Max.Y;
                Floor=result.Input.Entry.Standing.Y-95;RootSurface=result.Input.Entry.Surface;Root=result.Input.Entry.Standing;Base=Freeze(result.Base);ProtectRoute=protectRoute;Route=Freeze(protectRoute?result.Route:Array.Empty<Position>());
            }
            private Reservation(BodyCandidate body,ushort objectId)
            {
                World=body.Input.World;ObjectId=objectId;SourceKey=body.Input.RawSource.SourceKey;SceneFingerprint=body.Input.Scene.Fingerprint;PriorInputFingerprint=body.Input.Fingerprint;
                MinY=Math.Min(body.Centre.Y,(float)body.Centre.Y)+body.Input.Model.Min.Y;MaxY=Math.Max(body.Centre.Y,(float)body.Centre.Y)+body.Input.Model.Max.Y;
                Floor=body.Input.Entry.Standing.Y-95;RootSurface=body.Input.Entry.Surface;Root=body.Input.Entry.Standing;Base=Freeze(body.Base);Route=Freeze(Array.Empty<Position>());ProtectRoute=false;
            }
            internal static Reservation FromBody(BodyCandidate body,ushort objectId)
            {if(body==null)throw new ArgumentException("MissingBodyCandidate");return new Reservation(body,objectId);}
            internal static Reservation From(Result result,ushort objectId,bool protectRoute=true)
            {if(result?.HasPlan!=true)throw new ArgumentException("MissingPrefixPlan");return new Reservation(result,objectId,protectRoute);}
        }
        internal sealed class Request
        {
            public ushort World {get;} public SourceModelPresence SourceModel {get;} public string SourceModelEvidence {get;} public RawPose RawSource {get;} public RawPose Anchor {get;} public string AnchorEvidence {get;}
            public OpenFloorModelBounds Model {get;} public MissionOpenFloorUsableRange.Profile Usable {get;}
            public IReadOnlyList<Surface> Surfaces {get;} public Root Entry {get;} public Bounds SiteBounds {get;} public StaticScene Scene {get;}
            public IReadOnlyList<Reservation> Reservations {get;}
            private readonly Lazy<string> fingerprint;
            public string Fingerprint=>fingerprint.Value;
            public Request(ushort world,SourceModelPresence sourceModel,string sourceModelEvidence,RawPose rawSource,RawPose anchor,string anchorEvidence,OpenFloorModelBounds model,MissionOpenFloorUsableRange.Profile usable,
                IEnumerable<Surface> surfaces,Root entry,Bounds siteBounds,StaticScene scene,IEnumerable<Reservation> reservations=null)
            {World=world;SourceModel=sourceModel;SourceModelEvidence=sourceModelEvidence;RawSource=rawSource;Anchor=anchor;AnchorEvidence=anchorEvidence;Model=model;Usable=usable;Surfaces=Freeze(surfaces??Array.Empty<Surface>());Entry=entry;SiteBounds=siteBounds;Scene=scene;Reservations=Freeze(reservations??Array.Empty<Reservation>());fingerprint=new Lazy<string>(()=>Hash(new{Policy=PolicyVersion,World,SourceModel,SourceModelEvidence,RawSource,Anchor,AnchorEvidence,Model,Usable,Entry,SiteBounds.MinX,SiteBounds.MaxX,SiteBounds.MinZ,SiteBounds.MaxZ,Scene=Scene.Fingerprint,Surfaces=Surfaces.OrderBy(s=>s.Identity).ToArray(),Reservations}));}
        }
        internal static Request WithReservations(Request r,IEnumerable<Reservation> reservations)=>new Request(r.World,r.SourceModel,r.SourceModelEvidence,r.RawSource,r.Anchor,r.AnchorEvidence,
            r.Model,r.Usable,r.Surfaces,r.Entry,r.SiteBounds,r.Scene,reservations);
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
        internal sealed class BodyCandidate
        {
            internal Request Input {get;} internal Id Surface {get;} internal Position Centre {get;} internal IReadOnlyList<Point> Base {get;} internal double Distance {get;} internal bool RootReachableCentre {get;}
            internal BodyCandidate(Request input,Surface surface,Point centre,Point[] shape,double distance,bool rootReachable=false)
            {Input=input;Surface=surface.Identity;Centre=new Position(centre.X,input.Model.CentreY(surface.FloorC),centre.Z);Base=Freeze(shape);Distance=distance;RootReachableCentre=rootReachable;}
        }
        private sealed class Candidate
        {internal Surface Surface;internal Point Centre;internal Point[] Shape;internal double Distance;internal bool RootReachable;internal int Proof;internal BodyCandidate Body;}
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
        private static bool Inside(IReadOnlyList<Point> p,Point q)
        {
            for(int i=0;i<p.Count;i++)
                if(!(Cross(Sub(p[(i+1)%p.Count],p[i]),Sub(q,p[i]))>=-Epsilon))return false;
            return true;
        }
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
            double lo=0,hi=1;for(int i=0;i<shape.Count;i++)
            {
                var e=(A:shape[i],B:shape[(i+1)%shape.Count]);
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
        private static bool Overlap(Point[] a,Point[] b)
        {
            for(int i=0;i<a.Length;i++)if(Touches(a[i],a[(i+1)%a.Length],b))return true;
            for(int i=0;i<b.Length;i++)if(Touches(b[i],b[(i+1)%b.Length],a))return true;
            return false;
        }
        private static bool Fits(Point[] shape,Surface surface,Bounds bounds)=>shape.All(p=>Inside(surface.Vertices,p)&&Inside(bounds.Polygon,p));
        private static Point Rounded(Point p)=>new Point((float)p.X,(float)p.Z);
        private static Point? RepresentableNear(Point[] cell,Point target)
        {
            // A nearest boundary point can round outside the closed cell. Try adjacent native-f32 values
            // around that point, actual vertices and the convex-cell centroid; never relax Inside/final clearance.
            Point nearest=Nearest(cell,target),rounded=Rounded(nearest);if(Inside(cell,rounded))return rounded;
            var seeds=new[]{nearest,new Point(cell.Average(p=>p.X),cell.Average(p=>p.Z))}.Concat(cell);
            var candidates=new List<Point>();foreach(var seed in seeds)
            {
                float x=(float)seed.X,z=(float)seed.Z;
                foreach(float a in new[]{MathF.BitDecrement(x),x,MathF.BitIncrement(x)})
                    foreach(float b in new[]{MathF.BitDecrement(z),z,MathF.BitIncrement(z)})
                    {var p=new Point(a,b);if(Finite(a,b)&&Inside(cell,p))candidates.Add(p);}
            }
            return candidates.OrderBy(p=>Distance(p,target)).ThenBy(p=>p.X).ThenBy(p=>p.Z).Select(p=>(Point?)p).FirstOrDefault();
        }
        private static bool Covered(Point[] shape,Surface[] component)=>Difference(new[]{shape},component.Select(s=>s.Vertices.ToArray()),0).Count==0;
        private static Position Cast(Position p)=>new Position((float)p.X,(float)p.Y,(float)p.Z);
        private static double Guard(Request r)
        {
            double[] n=r.Surfaces.SelectMany(s=>s.Vertices.SelectMany(p=>new[]{p.X,p.Z})).Concat(new[]{r.Anchor.Position.X,r.Anchor.Position.Y,r.Anchor.Position.Z}).ToArray();
            double ulp=n.Max(v=>{float f=(float)v;return Math.Max(Math.Abs((double)MathF.BitIncrement(f)-f),Math.Abs((double)f-MathF.BitDecrement(f)));});
            return Math.Max(Epsilon*8,ulp*2);
        }
        // Exact broad phase only: every candidate base and route is contained in this NAV component.
        // Keep closed contact and the full numerical guard; never use a locality radius or source-sector filter.
        private static Bounds ComponentBounds(Surface[] component)=>new Bounds(component.Min(s=>s.Vertices.Min(p=>p.X)),component.Max(s=>s.Vertices.Max(p=>p.X)),
            component.Min(s=>s.Vertices.Min(p=>p.Z)),component.Max(s=>s.Vertices.Max(p=>p.Z)));
        private static bool ComponentRelevant(StaticObstacle o,Bounds bounds,double guard)=>
            o.Footprint.Max(p=>p.X)>=bounds.MinX-guard&&o.Footprint.Min(p=>p.X)<=bounds.MaxX+guard&&
            o.Footprint.Max(p=>p.Z)>=bounds.MinZ-guard&&o.Footprint.Min(p=>p.Z)<=bounds.MaxZ+guard;
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
        private sealed class StandingGraph
        {
            internal Point[][] Obstacles; internal List<Cell> Cells;
            internal Dictionary<int,int?> Previous; internal Dictionary<int,Point> Portals;
        }
        private static StandingGraph BuildStandingGraph(Request r,Surface[] component,IEnumerable<Point[]> bodies,double guard)
        {
            double floor=r.Entry.Standing.Y-95;
            Bounds envelope=ComponentBounds(component);
            var obstacles=r.Scene.Obstacles.Where(o=>RouteRelevant(o,floor)&&ComponentRelevant(o,envelope,guard)).Select(o=>o.Footprint.ToArray()).Concat(bodies).ToArray();
            var cells=StandingCells(component,obstacles,guard);var start=cells.FirstOrDefault(c=>c.Surface.Identity.Equals(r.Entry.Surface)&&Inside(c.Polygon,r.Entry.Standing.Xz));if(start==null)return null;
            var previous=new Dictionary<int,int?>{{start.Index,null}};var portals=new Dictionary<int,Point>();var queue=new Queue<Cell>();queue.Enqueue(start);
            while(queue.Count>0)
            {
                var current=queue.Dequeue();foreach(var other in cells){if(previous.ContainsKey(other.Index))continue;var p=CellPortal(current,other);if(p.HasValue){previous[other.Index]=current.Index;portals[other.Index]=p.Value;queue.Enqueue(other);}}
            }
            return new StandingGraph{Obstacles=obstacles,Cells=cells,Previous=previous,Portals=portals};
        }
        private static bool Standing(Request r,Surface[] component,Point[] shape,Position centre,double guard,out Position[] route,out Id[] path,out MissionOpenFloorUsableRange.Witness range,StandingGraph shared=null)
        {
            route=null;path=null;range=default;double floor=r.Entry.Standing.Y-95;
            var graph=shared??BuildStandingGraph(r,component,r.Reservations.Where(o=>o.MinY<=floor+95+Epsilon&&o.MaxY>=floor-Epsilon).Select(o=>o.Base.ToArray()).Append(shape),guard);
            if(graph==null)return false;var obstacles=graph.Obstacles;var cells=graph.Cells;var previous=graph.Previous;var portals=graph.Portals;
            var q=new OpenFloorOrientation(r.Anchor.QuaternionHex);var possibilities=new List<(Cell Cell,Point Point,MissionOpenFloorUsableRange.Witness Range)>();
            foreach(var cell in cells.Where(c=>previous.ContainsKey(c.Index)))
            {
                Point? represented=RepresentableNear(cell.Polygon,centre.Xz);if(!represented.HasValue)continue;Point p=represented.Value;
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
        private static readonly ConcurrentDictionary<string,Lazy<Surface[]>> topologyCache=new ConcurrentDictionary<string,Lazy<Surface[]>>(StringComparer.Ordinal);
        private static Surface[] ConnectedComponent(Request r,Surface start)
        {
            // Content key, not mutable NavMesh/global-path identity. Only immutable numeric surfaces are cached.
            string key=Hash(new{r.World,Start=start.Identity,Surfaces=r.Surfaces.OrderBy(s=>s.Identity).ToArray()});
            return topologyCache.GetOrAdd(key,_=>new Lazy<Surface[]>(()=>
            {
                var all=r.Surfaces.Where(s=>s.Flat&&s.FloorC==start.FloorC&&Convex(s.Vertices)).OrderBy(s=>s.Identity).ToArray();
                var found=new HashSet<Id>{start.Identity};var queue=new Queue<Surface>();queue.Enqueue(start);
                while(queue.Count>0){var current=queue.Dequeue();foreach(var surface in all)if(!found.Contains(surface.Identity)&&Portal(current,surface).HasValue){found.Add(surface.Identity);queue.Enqueue(surface);}}
                return all.Where(s=>found.Contains(s.Identity)).ToArray();
            })).Value;
        }
        private sealed class Context
        {
            internal Request Input;internal Point[] Offsets;internal Surface Start;internal Surface[] Component;
            internal double Guard,CentreY,MinY,MaxY;internal Bounds Envelope;internal StaticObstacle[] Relevant;internal Point[][] Forbidden;
            internal double CastCentreY;internal Point[][] OriginalGeometryFootprints,CastGeometryFootprints;internal int GeometryEvaluations;
            internal readonly Dictionary<(double X,double Z),Surface> Carriers=new Dictionary<(double X,double Z),Surface>();
        }
        private static (Context Context,string Refusal) Prepare(Request r)
        {
            (Context,string) No(string reason)=>(null,reason);Point[] offsets;
            try{offsets=Offsets(r);}catch(ArgumentException ex){return No(ex.Message);}
            if(r.Entry==null||string.IsNullOrWhiteSpace(r.Entry.Evidence)||!Finite(r.Entry.Standing.X,r.Entry.Standing.Y,r.Entry.Standing.Z))return No("MissingFiniteRoot");
            if(!r.SiteBounds.Valid||r.Scene==null||!r.Scene.Complete||r.Scene.World!=r.World||!r.Scene.Coverage.Valid||!Finite(r.Scene.MinY,r.Scene.MaxY)||r.Scene.MinY>=r.Scene.MaxY||string.IsNullOrWhiteSpace(r.Scene.Evidence))return No("MissingStaticCoverage");
            if(!Inside(r.Scene.Coverage.Polygon,r.Entry.Standing.Xz)||r.SiteBounds.Polygon.Any(p=>!Inside(r.Scene.Coverage.Polygon,p)))return No("IncompleteStaticEnvelope");
            if(r.Surfaces==null||r.Surfaces.Any(s=>s==null||!Finite(s.FloorX,s.FloorZ,s.FloorC)||s.Vertices.Any(p=>!Finite(p.X,p.Z)))||r.Surfaces.Select(s=>s.Identity).Distinct().Count()!=r.Surfaces.Count)return No("InvalidSurfaceSnapshot");
            if(r.Scene.Obstacles.Any(o=>o==null||!Finite(o.MinY,o.MaxY)||o.MinY>o.MaxY||!Convex(o.Footprint)||string.IsNullOrWhiteSpace(o.Evidence)||string.IsNullOrWhiteSpace(o.Identity))||r.Scene.Obstacles.Select(o=>o.Identity).Distinct(StringComparer.Ordinal).Count()!=r.Scene.Obstacles.Count)return No("UnprovenStaticBounds");
            if(r.SourceModel==SourceModelPresence.Unproven||(r.SourceModel==SourceModelPresence.MeasuredModel&&!r.Scene.Obstacles.Any(o=>o.Key==r.RawSource.SourceKey&&o.Goid==r.RawSource.SourceGoid)))return No("MissingSourceModelEvidence");
            Surface start=r.Surfaces.FirstOrDefault(s=>s.Identity.Equals(r.Entry.Surface));if(start==null||!start.Flat||!Convex(start.Vertices)||Math.Abs(r.Entry.Standing.Y-start.FloorC-95)>Epsilon||!Inside(start.Vertices,r.Entry.Standing.Xz))return No("InvalidStandingRoot");
            if(r.Reservations.Any(p=>p==null||p.World!=r.World||p.SceneFingerprint!=r.Scene.Fingerprint||p.Floor!=start.FloorC||!p.RootSurface.Equals(r.Entry.Surface)||
                p.Root.X!=r.Entry.Standing.X||p.Root.Y!=r.Entry.Standing.Y||p.Root.Z!=r.Entry.Standing.Z||!Convex(p.Base)||!Finite(p.MinY,p.MaxY)||p.MinY>p.MaxY)||
                r.Reservations.Select(p=>p.ObjectId).Distinct().Count()!=r.Reservations.Count||r.Reservations.Select(p=>p.SourceKey).Distinct().Count()!=r.Reservations.Count)return No("InvalidPrefixReservation");
            var component=ConnectedComponent(r,start);double guard=Guard(r);if(!double.IsFinite(guard))return No("UnrepresentableCoordinates");
            double centreY=r.Model.CentreY(start.FloorC),minY=centreY+r.Model.Min.Y,maxY=centreY+r.Model.Max.Y;
            if(r.Scene.MinY>Math.Min(start.FloorC,minY)-guard||r.Scene.MaxY<Math.Max(maxY,start.FloorC+95)+guard)return No("IncompleteStaticVerticalEnvelope");
            Bounds envelope=ComponentBounds(component);
            var relevant=r.Scene.Obstacles.Where(o=>VolumeRelevant(o,minY,maxY)&&ComponentRelevant(o,envelope,guard)).ToArray();
            var forbidden=relevant.Select(o=>Hull(o.Footprint.SelectMany(p=>offsets.Select(d=>new Point(p.X-d.X,p.Z-d.Z))))).ToList();
            foreach(var prior in r.Reservations)
            {
                if(prior.MinY<=maxY+Epsilon&&prior.MaxY>=minY-Epsilon)forbidden.Add(Hull(prior.Base.SelectMany(p=>offsets.Select(d=>new Point(p.X-d.X,p.Z-d.Z)))));
                if(prior.ProtectRoute&&minY<=prior.Floor+95+Epsilon&&maxY>=prior.Floor-Epsilon)
                    foreach(var points in new[]{prior.Route.Select(p=>p.Xz).ToArray(),prior.Route.Select(p=>Rounded(p.Xz)).ToArray()})
                        for(int i=0;i<points.Length-1;i++)forbidden.Add(Hull(new[]{points[i],points[i+1]}.SelectMany(p=>offsets.Select(d=>new Point(p.X-d.X,p.Z-d.Z)))));
            }
            double castCentreY=(float)centreY;
            return(new Context{Input=r,Offsets=offsets,Start=start,Component=component,Guard=guard,CentreY=centreY,MinY=minY,MaxY=maxY,Envelope=envelope,Relevant=relevant,Forbidden=forbidden.ToArray(),
                CastCentreY=castCentreY,OriginalGeometryFootprints=relevant.Select(o=>o.Footprint.ToArray()).ToArray(),
                CastGeometryFootprints=r.Scene.Obstacles.Where(o=>VolumeRelevant(o,Math.Min(minY,castCentreY+r.Model.Min.Y),Math.Max(maxY,castCentreY+r.Model.Max.Y))).Select(o=>o.Footprint.ToArray()).ToArray()},null);
        }
        private static Surface Carrier(Context c,Point point)
        {
            if(!c.Carriers.TryGetValue((point.X,point.Z),out var carrier))
            {carrier=c.Component.Where(s=>Inside(s.Vertices,point)).OrderBy(s=>s.Identity).FirstOrDefault();c.Carriers.Add((point.X,point.Z),carrier);}
            return carrier;
        }
        private static bool GeometryClear(Context c,Surface surface,Position position,out Point[] shape)
            =>GeometryClearCore(c,surface,position,out shape,true);
        // Reference mode retains the prior literal predicates for clearance-equivalence tests.
        private static bool GeometryClearCore(Context c,Surface surface,Position position,out Point[] shape,bool reuse)
        {
            shape=Array.Empty<Point>();var r=c.Input;
            if(surface==null||!Finite(position.X,position.Y,position.Z)||Math.Abs(position.Y-c.CentreY)>Epsilon||
                position.X!=(double)(float)position.X||position.Z!=(double)(float)position.Z)return false;
            var carrier=reuse?Carrier(c,position.Xz):c.Component.Where(s=>Inside(s.Vertices,position.Xz)).OrderBy(s=>s.Identity).FirstOrDefault();
            if(carrier==null||!carrier.Identity.Equals(surface.Identity))return false;
            shape=c.Offsets.Select(p=>new Point(position.X+p.X,position.Z+p.Z)).ToArray();
            var fixedShape=shape;
            if(r.Reservations.Any(prior=>(prior.MinY<=c.MaxY+Epsilon&&prior.MaxY>=c.MinY-Epsilon&&Overlap(fixedShape,prior.Base.ToArray()))||
                (prior.ProtectRoute&&c.MinY<=prior.Floor+95+Epsilon&&c.MaxY>=prior.Floor-Epsilon&&(!Clear(prior.Route.Select(p=>p.Xz).ToArray(),fixedShape)||!Clear(prior.Route.Select(p=>Rounded(p.Xz)).ToArray(),fixedShape)))))return false;
            if(!(fixedShape.All(p=>Inside(r.SiteBounds.Polygon,p))&&Covered(fixedShape,c.Component)))return false;
            if(reuse?c.OriginalGeometryFootprints.Any(o=>Overlap(fixedShape,o)):c.Relevant.Any(o=>Overlap(fixedShape,o.Footprint.ToArray())))return false;
            var cast=Cast(position);var castShape=c.Offsets.Select(p=>new Point(cast.X+p.X,cast.Z+p.Z)).ToArray();
            bool sameXY=BitConverter.DoubleToInt64Bits(cast.X)==BitConverter.DoubleToInt64Bits(position.X)&&BitConverter.DoubleToInt64Bits(cast.Z)==BitConverter.DoubleToInt64Bits(position.Z);
            if(!reuse||!sameXY)if(!(castShape.All(p=>Inside(r.SiteBounds.Polygon,p))&&Covered(castShape,c.Component)))return false;
            // Keep the original ALL-scene cast-volume predicate. A tolerated Y perturbation can cast differently.
            var castFootprints=reuse&&cast.Y==c.CastCentreY?c.CastGeometryFootprints:r.Scene.Obstacles.Where(o=>VolumeRelevant(o,Math.Min(c.MinY,cast.Y+r.Model.Min.Y),Math.Max(c.MaxY,cast.Y+r.Model.Max.Y))).Select(o=>o.Footprint.ToArray());
            return !castFootprints.Any(o=>Overlap(castShape,o));
        }
        private static bool RouteValid(Context c,Position centre,Point[] shape,IReadOnlyList<Position> route)
        {
            var r=c.Input;if(route==null||route.Count<2||route.Any(p=>!Finite(p.X,p.Y,p.Z)||Math.Abs(p.Y-c.Start.FloorC-95)>Epsilon)||
                Distance(route[0].Xz,r.Entry.Standing.Xz)>Epsilon||Math.Abs(route[0].Y-r.Entry.Standing.Y)>Epsilon)return false;
            var points=route.Select(p=>p.Xz).ToArray();var cast=points.Select(Rounded).ToArray();
            if(points.Concat(cast).Any(p=>!Inside(r.Scene.Coverage.Polygon,p)))return false;
            var obstacles=r.Scene.Obstacles.Where(o=>RouteRelevant(o,c.Start.FloorC)&&ComponentRelevant(o,c.Envelope,c.Guard)).Select(o=>o.Footprint.ToArray()).Concat(r.Reservations.Where(o=>o.MinY<=c.Start.FloorC+95+Epsilon&&o.MaxY>=c.Start.FloorC-Epsilon).Select(o=>o.Base.ToArray())).Append(shape).ToArray();
            if(obstacles.Any(o=>!Clear(points,o)||!Clear(cast,o)))return false;
            var cells=StandingCells(c.Component,obstacles,c.Guard);
            for(int i=0;i<points.Length-1;i++)
                if(!SegmentCovered(points[i],points[i+1],cells.Select(cell=>cell.Polygon))||!SegmentCovered(cast[i],cast[i+1],cells.Select(cell=>cell.Polygon)))return false;
            var q=new OpenFloorOrientation(r.Anchor.QuaternionHex);
            return MissionOpenFloorUsableRange.Test(r.Usable,Native(route[^1]),Native(centre),q).Within&&
                MissionOpenFloorUsableRange.Test(r.Usable,Native(Cast(route[^1])),Native(Cast(centre)),q).Within;
        }
        // CSG subdivisions are not physical boundaries. Cover the entire segment with convex-cell intervals;
        // gaps remain gaps and closed static/body contact is checked independently before this test.
        internal static bool SegmentCovered(Point a,Point b,IEnumerable<Point[]> polygons)
        {
            var intervals=new List<(double Lo,double Hi)>();
            foreach(var polygon in polygons)
            {
                double lo=0,hi=1;bool valid=true;
                foreach(var edge in Edges(polygon))
                {
                    var d=Sub(edge.B,edge.A);double start=Cross(d,Sub(a,edge.A))+Epsilon,change=Cross(d,Sub(b,a));
                    if(change==0){if(start<0){valid=false;break;}}
                    else if(change>0)lo=Math.Max(lo,-start/change);else hi=Math.Min(hi,-start/change);
                    if(lo>hi){valid=false;break;}
                }
                if(valid&&lo<=hi)intervals.Add((lo,hi));
            }
            double covered=0;foreach(var interval in intervals.OrderBy(x=>x.Lo).ThenBy(x=>x.Hi))
            {if(interval.Lo>covered)return false;covered=Math.Max(covered,interval.Hi);if(covered>=1)return true;}return false;
        }
        // Replays fixed geometry only; never invokes nearest-centre selection or chooses a replacement.
        internal static bool ValidateFixed(Request r,string expectedInputFingerprint,Id surface,Position centre,IReadOnlyList<Position> route,out string refusal)
        {
            var p=Prepare(r);refusal=p.Refusal;if(p.Context==null)return false;var c=p.Context;
            if(string.IsNullOrEmpty(expectedInputFingerprint)||expectedInputFingerprint!=r.Fingerprint){refusal="ChangedFixedPlacementInputs";return false;}
            if(!GeometryClear(c,c.Component.FirstOrDefault(s=>s.Identity.Equals(surface)),centre,out var shape)){refusal="InvalidFixedGeometry";return false;}
            if(!RouteValid(c,centre,shape,route)){refusal="InvalidFixedStandingRouteOrRange";return false;}return true;
        }
        // Initial whole-phase search only. The body stays fixed; all routes are solved against the complete body set.
        // Admitted identity is the canonical sourced request, never the ephemeral search reservation order.
        internal static bool RerouteFixedBody(Request canonical,Id surface,Position centre,IEnumerable<Reservation> bodies,out Result result,out string refusal)
        {
            result=null;var working=WithReservations(canonical,bodies);var prepared=Prepare(working);refusal=prepared.Refusal;if(prepared.Context==null)return false;
            var context=prepared.Context;var carrier=context.Component.FirstOrDefault(s=>s.Identity.Equals(surface));
            if(!GeometryClear(context,carrier,centre,out var shape)){refusal="InvalidWholePhaseBody";return false;}
            if(!Standing(working,context.Component,shape,centre,context.Guard,out var route,out var ids,out var range)||!RouteValid(context,centre,shape,route))
            {refusal="NoWholePhaseStandingRoute";return false;}
            result=new Result(canonical,carrier,centre.Xz,shape,route,ids,context.Guard,range);return true;
        }
        internal static bool BodiesClear(BodyCandidate a,BodyCandidate b)
        {
            double minA=a.Centre.Y+a.Input.Model.Min.Y,maxA=a.Centre.Y+a.Input.Model.Max.Y,minB=b.Centre.Y+b.Input.Model.Min.Y,maxB=b.Centre.Y+b.Input.Model.Max.Y;
            return minA>maxB+Epsilon||minB>maxA+Epsilon||!Overlap(a.Base.ToArray(),b.Base.ToArray());
        }
        internal static bool RouteBodySet(IReadOnlyList<BodyCandidate> bodies,out Result[] results,out string refusal)
        {
            results=null;refusal="IncompletePlacementSet";if(bodies==null||bodies.Count==0)return false;
            var first=Prepare(bodies[0].Input);if(first.Context==null){refusal=first.Refusal;return false;}
            var graph=BuildStandingGraph(bodies[0].Input,first.Context.Component,bodies.Select(b=>b.Base.ToArray()),first.Context.Guard);
            if(graph==null){refusal="BlockedWholePhaseRoot";return false;}
            var output=new List<Result>();
            foreach(var body in bodies)
            {
                var r=body.Input;var reservations=bodies.Where(b=>!ReferenceEquals(b,body)).Select((b,i)=>Reservation.FromBody(b,(ushort)(i+1)));
                var prepared=Prepare(WithReservations(r,reservations));if(prepared.Context==null){refusal=prepared.Refusal;return false;}var c=prepared.Context;
                var carrier=c.Component.FirstOrDefault(s=>s.Identity.Equals(body.Surface));
                if(!GeometryClear(c,carrier,body.Centre,out var shape)){refusal="InvalidWholePhaseBody";return false;}
                if(!Standing(c.Input,c.Component,shape,body.Centre,c.Guard,out var route,out var ids,out var range,graph)||!RouteValid(c,body.Centre,shape,route)){refusal="NoWholePhaseStandingRoute:"+(output.Count+1);return false;}
                output.Add(new Result(r,carrier,body.Centre.Xz,shape,route,ids,c.Guard,range));
            }
            results=output.ToArray();return ValidateSet(results,out refusal);
        }
        internal static bool TryReplay(Request r,string fingerprint,Id surface,Position centre,IReadOnlyList<Position> route,IReadOnlyList<Id> ids,out Result result,out string refusal)
        {
            result=null;
            if(!ValidateFixed(r,fingerprint,surface,centre,route,out refusal)||!ValidateRouteIds(r,ids,route)){refusal=refusal??"InvalidFixedRouteIdentity";return false;}
            var p=Prepare(r);var c=p.Context;var carrier=c.Component.Single(s=>s.Identity.Equals(surface));
            if(!GeometryClear(c,carrier,centre,out var shape)){refusal="InvalidFixedGeometry";return false;}
            var range=MissionOpenFloorUsableRange.Test(r.Usable,Native(route.Last()),Native(centre),new OpenFloorOrientation(r.Anchor.QuaternionHex));
            result=new Result(r,carrier,centre.Xz,shape,route.ToArray(),ids.ToArray(),c.Guard,range);return true;
        }
        internal static bool ValidateRouteIds(Request r,IReadOnlyList<Id> ids,IReadOnlyList<Position> route)
        {
            if(r?.Entry==null||ids==null||route==null||route.Count!=ids.Count+1||ids.Count==0||!ids[0].Equals(r.Entry.Surface))return false;
            for(int i=0;i<ids.Count;i++)
            {
                var surface=r.Surfaces.SingleOrDefault(s=>s.Identity.Equals(ids[i]));
                if(surface==null||!surface.Flat||Math.Abs(route[i].Y-surface.FloorC-95)>Epsilon||Math.Abs(route[i+1].Y-surface.FloorC-95)>Epsilon||
                    !Inside(surface.Vertices,route[i].Xz)||!Inside(surface.Vertices,route[i+1].Xz)||!Inside(surface.Vertices,Rounded(route[i].Xz))||!Inside(surface.Vertices,Rounded(route[i+1].Xz)))return false;
            }
            return true;
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
        private static List<Candidate> GeometricCandidates(Context context,bool expanded)=>GeometricCandidatesCore(context,expanded,true);
        private static List<Candidate> GeometricCandidatesCore(Context context,bool expanded,bool deduplicateEarly)
            =>EnumerateGeometry(context,expanded,deduplicateEarly).DistinctBy(c=>(c.Centre.X,c.Centre.Z)).ToList();
        private static IEnumerable<Candidate> EnumerateGeometry(Context context,bool expanded,bool deduplicateEarly=true)
        {
            foreach(var candidate in RepresentedProposals(context,expanded,deduplicateEarly))
                if(ProveCandidate(context,candidate,expanded))yield return candidate;
        }
        private static bool ProveCandidate(Context context,Candidate candidate,bool expanded)
        {
            if(candidate.Proof!=0)return candidate.Proof>0;context.GeometryEvaluations++;
            bool clear=GeometryClear(context,candidate.Surface,new Position(candidate.Centre.X,context.CentreY,candidate.Centre.Z),out var shape);
            candidate.Shape=shape;
            if(clear&&expanded)clear=Clear(new[]{context.Input.Entry.Standing.Xz},shape)&&Clear(new[]{Rounded(context.Input.Entry.Standing.Xz)},shape);
            candidate.Proof=clear?1:-1;return clear;
        }
        private static List<Candidate> RepresentedProposals(Context context,bool expanded,bool deduplicateEarly=true)
        {
            var r=context.Input;var offsets=context.Offsets;var component=context.Component;double guard=context.Guard,centreY=context.CentreY;var forbidden=context.Forbidden;
            // Erode the UNION, not individual NAV subdivisions. Complement cells retain actual holes/walls;
            // Minkowski expansion excludes every centre whose full footprint would meet that complement.
            Point[] allowed=Erode(r.SiteBounds.Polygon,offsets);
            if(allowed.Length==0)return new List<Candidate>();
            var outside=Difference(new[]{r.SiteBounds.Polygon},component.Select(s=>s.Vertices.ToArray()),0);
            var outsideForbidden=outside.Select(cell=>Hull(cell.SelectMany(p=>offsets.Select(d=>new Point(p.X-d.X,p.Z-d.Z))))).ToArray();
            var candidates=new List<Candidate>();var representedCentres=new HashSet<(double X,double Z)>();
            foreach(var cell in Difference(new[]{allowed},outsideForbidden.Concat(forbidden),guard))
            {
                IEnumerable<Point> seeds=expanded?new[]{Nearest(cell,r.Anchor.Position.Xz),Nearest(cell,r.Entry.Standing.Xz),new Point(cell.Average(p=>p.X),cell.Average(p=>p.Z))}
                    .Concat(cell).Concat(Edges(cell).Select(e=>new Point((e.A.X+e.B.X)/2,(e.A.Z+e.B.Z)/2))):new[]{r.Anchor.Position.Xz};
                foreach(var seed in seeds)
                {
                    Point? represented=RepresentableNear(cell,seed);if(!represented.HasValue)continue;Point centre=represented.Value;
                    // Same immutable context and deterministic centre->carrier mapping: duplicate successes AND
                    // failures have identical geometry. Preserve the first occurrence, as the final DistinctBy did.
                    if(deduplicateEarly&&!representedCentres.Add((centre.X,centre.Z)))continue;
                    var surface=Carrier(context,centre);if(surface==null)continue;
                    candidates.Add(new Candidate{Surface=surface,Centre=centre,Distance=Distance(centre,r.Anchor.Position.Xz)});
                }
            }            return candidates.OrderBy(c=>c.Distance).ThenBy(c=>c.Surface.Identity).ThenBy(c=>c.Centre.X).ThenBy(c=>c.Centre.Z).ToList();
        }
        internal sealed class ReplayableBodies : IEnumerable<BodyCandidate>
        {
            private readonly Func<bool,IEnumerable<BodyCandidate>> enumerate;private readonly Func<int> evaluations;
            internal ReplayableBodies(Func<bool,IEnumerable<BodyCandidate>> enumerate,Func<int> evaluations){this.enumerate=enumerate;this.evaluations=evaluations;}
            internal int GeometryEvaluations=>evaluations();
            internal IEnumerable<BodyCandidate> Enumerate(bool rootOnly)=>enumerate(rootOnly);
            public IEnumerator<BodyCandidate> GetEnumerator()=>enumerate(false).GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        internal static bool TryBodySequence(Request r,out ReplayableBodies sequence,out string refusal,out bool staticRootGraphBuilt)
        {
            sequence=null;staticRootGraphBuilt=false;var p=Prepare(r);refusal=p.Refusal;if(p.Context==null)return false;
            var context=p.Context;var proposals=RepresentedProposals(context,true);
            // Match eager refusal order: establish at least one valid body BEFORE the static-root graph.
            if(!proposals.Any(candidate=>ProveCandidate(context,candidate,true))){refusal="NoGeometricBodyCandidates";return false;}
            staticRootGraphBuilt=true;var graph=BuildStandingGraph(r,context.Component,Array.Empty<Point[]>(),context.Guard);
            if(graph==null){refusal="BlockedStaticRoot";return false;}
            var reachable=graph.Cells.Where(c=>graph.Previous.ContainsKey(c.Index)).Select(c=>c.Polygon).ToArray();
            foreach(var candidate in proposals)candidate.RootReachable=reachable.Any(polygon=>Inside(polygon,candidate.Centre));
            var ordered=proposals.OrderBy(c=>c.RootReachable?0:1).ThenBy(c=>c.Distance).ThenBy(c=>c.Surface.Identity).ThenBy(c=>c.Centre.X).ThenBy(c=>c.Centre.Z).ToArray();
            IEnumerable<BodyCandidate> Enumerate(bool rootOnly)
            {
                foreach(var candidate in ordered)
                {
                    if(rootOnly&&!candidate.RootReachable)yield break;
                    if(!ProveCandidate(context,candidate,true))continue;
                    if(candidate.Body==null)candidate.Body=new BodyCandidate(r,candidate.Surface,candidate.Centre,candidate.Shape,candidate.Distance,candidate.RootReachable);
                    yield return candidate.Body;
                }
            }
            sequence=new ReplayableBodies(Enumerate,()=>context.GeometryEvaluations);refusal=null;return true;
        }
        internal static IReadOnlyList<BodyCandidate> BodyCandidates(Request r,out string refusal)=>BodyCandidates(r,out refusal,out _);
        internal static IReadOnlyList<BodyCandidate> BodyCandidates(Request r,out string refusal,out bool staticRootGraphBuilt)
        {
            if(!TryBodySequence(r,out var sequence,out refusal,out staticRootGraphBuilt))return Freeze(Array.Empty<BodyCandidate>());
            return Freeze(sequence);
        }
        internal static Result Plan(Request r)=>Plan(r,int.MaxValue,int.MaxValue,out _,out _);
        internal static Result Plan(Request r,int maxBodyNodes,int maxRouteGraphs,out int bodyNodes,out int routeGraphs)
        {
            bodyNodes=0;routeGraphs=0;
            Result No(string reason)=>new Result(reason,r);var prepared=Prepare(r);if(prepared.Context==null)return No(prepared.Refusal);
            var context=prepared.Context;var offsets=context.Offsets;var start=context.Start;var component=context.Component;double guard=context.Guard,centreY=context.CentreY,maxY=context.MaxY;
            var relevant=context.Relevant;var forbidden=context.Forbidden;
            var candidates=EnumerateGeometry(context,false);
            // The same total candidate order as the original minimum selection; defer expensive route work.
            foreach(var candidate in candidates)
            {
                if(bodyNodes>=maxBodyNodes)return No("SearchExhausted:BodyNodes");bodyNodes++;
                var position=new Position(candidate.Centre.X,centreY,candidate.Centre.Z);
                if(routeGraphs>=maxRouteGraphs)return No("SearchExhausted:RouteGraphs");routeGraphs++;
                if(Standing(r,component,candidate.Shape,position,guard,out var route,out var path,out var range)&&RouteValid(context,position,candidate.Shape,route))
                    return new Result(r,candidate.Surface,candidate.Centre,candidate.Shape,route,path,guard,range);
            }
            return No("NoClearFootprintAndInRangeReachableStandingPoint");
        }
    }
}
