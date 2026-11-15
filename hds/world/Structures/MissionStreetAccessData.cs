using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using E = hds.MissionInteriorEvidence;
using G = hds.MissionOpenFloorResolver;

namespace hds
{
    // Audited evidence only. No chooser, movement, publication or persistence call sites.
    internal sealed class MissionStreetAccessData
    {
        internal sealed class Edge { public int Index {get;set;} public int SourceOffset {get;set;} public string Raw {get;set;} internal byte[] Bytes; }
        internal sealed class Polygon { public int Index {get;set;} public int SourceOffset {get;set;} public string RawHeader {get;set;} public Edge[] Edges {get;set;} internal double[] Plane; }
        internal sealed class Region { public ushort Id {get;set;} public int SectionOffset {get;set;} public int SectionLength {get;set;} public int NvpsOffset {get;set;} public string RawHeader {get;set;} public int PolygonEnd {get;set;} public Polygon[] Polygons {get;set;} }
        internal sealed class Outdoor { public string RawHeader {get;set;} public string RawSectionTable {get;set;} public int SnavOffset {get;set;} public int SnavLength {get;set;} public string RawSnav {get;set;} public Region[] Regions {get;set;} }
        internal sealed class Layer { public string Kind {get;set;} public uint Rid {get;set;} public int Length {get;set;} public string Sha256 {get;set;} public int Count {get;set;} public int DoorCount {get;set;} }
        internal sealed class NativeDoor { public uint Key {get;set;} public ushort Goid {get;set;} public int Class {get;set;} public string Layer {get;set;} public uint Rid {get;set;} public int SourceOffset {get;set;} public string Raw {get;set;} public string RecordSha256 {get;set;} public ushort? Region {get;set;} public int[] Pivot {get;set;} public int? Rotation {get;set;} }
        internal sealed class Signature { public ushort Goid {get;set;} public bool Exterior {get;set;} public double[] Position {get;set;} public string Quaternion {get;set;} public double Rotation {get;set;} }
        internal sealed class LegacyRow { public string File {get;set;} public int Line {get;set;} public Signature Signature {get;set;} }
        internal sealed class Legacy { public uint Key {get;set;} public LegacyRow[] Rows {get;set;} }
        internal sealed class LegacySource { public string File {get;set;} public string Sha256 {get;set;} }
        internal sealed class Neighbor
        {public ushort World {get;set;}public ushort Sector {get;set;}public double[] ParentCorner {get;set;}public string NavPath {get;set;}public string NavSha256 {get;set;}public int NavLength {get;set;}public Outdoor Outdoor {get;set;}public string Refusal {get;set;}internal string ValidationRefusal;}
        internal sealed class Document
        {
            public int Format {get;set;} public ushort World {get;set;} public ushort Sector {get;set;} public string InteriorDigest {get;set;}
            public double[] ParentCorner {get;set;} public string NavPath {get;set;} public string NavSha256 {get;set;} public int NavLength {get;set;}
            public Outdoor Outdoor {get;set;} public Neighbor[] Neighbors {get;set;} public Layer[] Layers {get;set;} public NativeDoor[] NativeDoors {get;set;} public NativeDoor[] NativeObjects {get;set;} public Legacy[] Legacy {get;set;} public LegacySource[] LegacySources {get;set;}
        }
        private readonly Document document;
        private readonly E.Node[] networkAnchors;
        private readonly Dictionary<uint,int> nativeKeyCounts;
        private readonly Dictionary<E.Node,Polygon> polygons;
        internal readonly string Digest;
        internal ushort World => document.World;
        internal ushort Sector => document.Sector;
        internal string InteriorDigest => document.InteriorDigest;
        internal string NavDigest => document.NavSha256;
        internal double[] Corner => (double[])document.ParentCorner.Clone();
        internal bool UniqueNativeKey(uint key)=>nativeKeyCounts.TryGetValue(key,out int count)&&count==1;
        internal NativeDoor[] Doors(uint key) => document.NativeDoors.Where(d=>d.Key==key).ToArray();
        private MissionStreetAccessData(Document d,string digest)
        {document=d;Digest=digest;polygons=d.Outdoor.Regions.SelectMany(r=>r.Polygons.Select(p=>new {Node=new E.Node(r.Id,checked((ushort)p.Index)),Poly=p})).ToDictionary(p=>p.Node,p=>p.Poly);networkAnchors=ComputeNetworkAnchors();nativeKeyCounts=d.NativeObjects.GroupBy(n=>n.Key).ToDictionary(g=>g.Key,g=>g.Count());}
        private static void Need(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
        private static bool Hex(string s,int length)=>s!=null&&s.Length==length&&s.All(Uri.IsHexDigit);
        private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
        internal static MissionStreetAccessData Read(byte[] compressed,string expectedCompressed,string expectedDigest,E interior)
        {
            Need(compressed!=null&&Hex(expectedCompressed,64)&&Hash(compressed).Equals(expectedCompressed,StringComparison.OrdinalIgnoreCase),"StreetPayloadHash");
            byte[] bytes;using(var input=new MemoryStream(compressed))using(var gzip=new GZipStream(input,CompressionMode.Decompress))using(var output=new MemoryStream()){gzip.CopyTo(output);bytes=output.ToArray();}
            Need(Hex(expectedDigest,64)&&Hash(bytes).Equals(expectedDigest,StringComparison.OrdinalIgnoreCase),"StreetPayloadDigest");
            var d=JsonSerializer.Deserialize<Document>(bytes);Validate(d);Need(interior!=null&&interior.MatchesStreetSource(d),"StreetInteriorSourceBinding");
            return new MissionStreetAccessData(d,expectedDigest.ToUpperInvariant());
        }
        private static void ValidateOutdoor(Outdoor outdoor,int navLength)
        {
            byte[] header=Convert.FromHexString(outdoor.RawHeader??"");byte[] table=Convert.FromHexString(outdoor.RawSectionTable??"");
            Need(header.Length==16&&BitConverter.ToUInt32(header)==0&&BitConverter.ToUInt32(header,4)==0,"StreetNavHeader");
            uint sections=BitConverter.ToUInt32(header,8);Need(sections>0&&sections<=ushort.MaxValue&&table.Length==sections*8&&16L+table.Length<=navLength&&outdoor.Regions!=null&&outdoor.Regions.Length==sections-1,"StreetOutdoorCount");
            Need(outdoor.SnavOffset==BitConverter.ToUInt32(table)&&outdoor.SnavLength==BitConverter.ToUInt32(table,4)&&outdoor.SnavOffset>=16+table.Length&&
                (long)outdoor.SnavOffset+outdoor.SnavLength<=navLength,"StreetSnavBounds");
            for(int r=0;r<outdoor.Regions.Length;r++)
            {
                Region region=outdoor.Regions[r];Need(region!=null&&region.Id==r&&region.Polygons!=null&&region.Polygons.Length<=ushort.MaxValue,"StreetOriginalRegionOrdinal");
                Need(region.SectionOffset==BitConverter.ToUInt32(table,(r+1)*8)&&region.SectionLength==BitConverter.ToUInt32(table,(r+1)*8+4)&&
                    region.SectionOffset>=16+table.Length&&region.SectionLength>=16&&(long)region.SectionOffset+region.SectionLength<=navLength,"StreetSectionBounds");
                byte[] raw=Convert.FromHexString(region.RawHeader??"");Need(raw.Length==16&&Encoding.ASCII.GetString(raw,0,4)=="NVPS"&&BitConverter.ToUInt32(raw,8)==(BitConverter.ToUInt32(raw,4)|0x81000000u)&&
                    BitConverter.ToUInt32(raw,12)==region.Polygons.Length&&region.NvpsOffset>=region.SectionOffset&&region.NvpsOffset+16L<=region.SectionOffset+(long)region.SectionLength,"StreetNvpsHeader");
                long cursor=region.NvpsOffset+16L;
                for(int p=0;p<region.Polygons.Length;p++)
                {
                    Polygon poly=region.Polygons[p];Need(poly!=null&&poly.Index==p&&poly.SourceOffset==cursor&&poly.Edges!=null,"StreetPolygonOrdinal");
                    byte[] plane=Convert.FromHexString(poly.RawHeader??"");Need(plane.Length==20&&BitConverter.ToUInt32(plane,16)==poly.Edges.Length,"StreetPolygonRawCount");
                    poly.Plane=Enumerable.Range(0,4).Select(i=>(double)BitConverter.ToSingle(plane,i*4)).ToArray();Need(poly.Plane.All(double.IsFinite)&&poly.Plane[1]!=0,"StreetOriginalPlane");cursor+=20;
                    for(int e=0;e<poly.Edges.Length;e++)
                    {
                        Edge edge=poly.Edges[e];Need(edge!=null&&edge.Index==e&&edge.SourceOffset==cursor,"StreetEdgeOrdinal");edge.Bytes=Convert.FromHexString(edge.Raw??"");
                        Need(edge.Bytes.Length==20&&Enumerable.Range(0,4).All(i=>float.IsFinite(BitConverter.ToSingle(edge.Bytes,4+i*4))),"StreetOriginalEdge");cursor+=20;
                        ushort target=BitConverter.ToUInt16(edge.Bytes),neighbor=BitConverter.ToUInt16(edge.Bytes,2);
                        if(neighbor!=ushort.MaxValue&&target<outdoor.Regions.Length)Need(outdoor.Regions[target]!=null&&outdoor.Regions[target].Polygons!=null&&neighbor<outdoor.Regions[target].Polygons.Length,"StreetLocalReference");
                        // Interior and cross-sector references remain raw. Only a proved exact link may be traversed later.
                    }
                    Need(cursor<=region.SectionOffset+(long)region.SectionLength,"StreetPolygonBounds");
                }
                Need(cursor==region.PolygonEnd,"StreetCountedPolygonEnd");
            }
            // Preserve original sector-network boundary tables. These rows do not authorize traversal into an unavailable sector.
            byte[] snav=Convert.FromHexString(outdoor.RawSnav??"");
            Need(snav.Length==outdoor.SnavLength&&snav.Length>=44&&Encoding.ASCII.GetString(snav,0,4)=="SNAV"&&
                BitConverter.ToUInt32(snav,4)==snav.Length-32&&BitConverter.ToUInt32(snav,8)==1&&BitConverter.ToUInt32(snav,12)==1&&
                Encoding.ASCII.GetString(snav,32,4)=="SSES"&&BitConverter.ToUInt32(snav,36)==snav.Length-44&&BitConverter.ToUInt32(snav,40)==0x01000004,"StreetSnavHeader");
            int boundaryCursor=44;
            for(int side=0;side<4;side++)
            {
                Need(boundaryCursor+16L<=snav.Length&&Encoding.ASCII.GetString(snav,boundaryCursor,4)=="SSES","StreetBoundaryHeader");
                uint size=BitConverter.ToUInt32(snav,boundaryCursor+4),count=BitConverter.ToUInt32(snav,boundaryCursor+12);
                Need(size==4L+count*32L&&BitConverter.ToUInt32(snav,boundaryCursor+8)==(size|0x81000000u)&&boundaryCursor+12L+size<=snav.Length,"StreetBoundaryCount");
                for(int index=0;index<count;index++)
                {
                    int at=boundaryCursor+16+index*32;ushort region=BitConverter.ToUInt16(snav,at),poly=BitConverter.ToUInt16(snav,at+2);uint edge=BitConverter.ToUInt32(snav,at+4);
                    Need(region<outdoor.Regions.Length&&poly<outdoor.Regions[region].Polygons.Length&&edge<outdoor.Regions[region].Polygons[poly].Edges.Length&&
                        Enumerable.Range(0,6).All(i=>float.IsFinite(BitConverter.ToSingle(snav,at+8+i*4))),"StreetBoundarySourceReference");
                    byte[] original=outdoor.Regions[region].Polygons[poly].Edges[edge].Bytes;
                    Need(BitConverter.ToUInt16(original,2)==ushort.MaxValue&&BitConverter.ToUInt16(original)==(side<<12|index),"StreetBoundaryReverseReference");
                }
                boundaryCursor+=checked(12+(int)size);
            }
            Need(boundaryCursor==snav.Length,"StreetBoundaryCountedEnd");
        }
        private static Tuple<E.Node,double[][]>[] BoundaryRows(Outdoor outdoor,double[] corner)
        {
            byte[] raw=Convert.FromHexString(outdoor.RawSnav);int cursor=44;var rows=new List<Tuple<E.Node,double[][]>>();
            for(int side=0;side<4;side++)
            {
                int count=checked((int)BitConverter.ToUInt32(raw,cursor+12));
                for(int i=0;i<count;i++)
                {
                    int at=cursor+16+i*32;var node=new E.Node(BitConverter.ToUInt16(raw,at),BitConverter.ToUInt16(raw,at+2));
                    double[][] endpoints=Enumerable.Range(0,2).Select(j=>Enumerable.Range(0,3).Select(k=>(double)(float)(BitConverter.ToSingle(raw,at+8+j*12+k*4)+(float)corner[k])).ToArray()).ToArray();
                    rows.Add(Tuple.Create(node,endpoints));
                }
                cursor+=checked(12+(int)BitConverter.ToUInt32(raw,cursor+4));
            }
            return rows.ToArray();
        }
        internal E.Node[] NetworkAnchors() => (E.Node[])networkAnchors.Clone();
        private E.Node[] ComputeNetworkAnchors()
        {
            var neighbors=document.Neighbors.Where(n=>n.ValidationRefusal==null).SelectMany(n=>BoundaryRows(n.Outdoor,n.ParentCorner)).ToArray();
            return BoundaryRows(document.Outdoor,document.ParentCorner).Where(row=>neighbors.Count(other=>
                row.Item2[0].SequenceEqual(other.Item2[0])&&row.Item2[1].SequenceEqual(other.Item2[1])||
                row.Item2[0].SequenceEqual(other.Item2[1])&&row.Item2[1].SequenceEqual(other.Item2[0]))==1).Select(row=>row.Item1).Distinct().ToArray();
        }
        private static void Validate(Document d)
        {
            Need(d!=null&&d.Format==1&&d.World==1&&d.Sector<=0x7ff&&Hex(d.InteriorDigest,64)&&Hex(d.NavSha256,64)&&d.NavLength>=16&&
                d.ParentCorner!=null&&d.ParentCorner.Length==3&&d.ParentCorner.All(double.IsFinite)&&d.ParentCorner[1]==0&&d.Outdoor!=null,"StreetDocumentIdentity");
            ValidateOutdoor(d.Outdoor,d.NavLength);
            Need(d.Neighbors!=null&&d.Neighbors.Select(n=>n?.Sector).Distinct().Count()==d.Neighbors.Length,"StreetNeighborIdentity");
            foreach(var neighbor in d.Neighbors)
            {
                Need(neighbor!=null&&neighbor.World==d.World&&neighbor.Sector!=d.Sector&&neighbor.Sector<=0x7ff&&neighbor.ParentCorner?.Length==3&&
                    neighbor.ParentCorner.All(double.IsFinite)&&neighbor.ParentCorner[1]==0,"StreetNeighborSource");
                if(neighbor.Refusal!=null)
                {
                    Need(neighbor.Refusal=="MissingOriginalNeighborNav"&&neighbor.NavLength==0&&neighbor.NavSha256==null&&neighbor.Outdoor==null&&!string.IsNullOrWhiteSpace(neighbor.NavPath),"StreetUnavailableNeighborShape");
                    neighbor.ValidationRefusal=neighbor.Refusal;continue; // No graph node or anchor can be obtained from unavailable original evidence.
                }
                Need(neighbor.NavLength>=16&&Hex(neighbor.NavSha256,64)&&neighbor.Outdoor!=null,"StreetNeighborSource");
                try {ValidateOutdoor(neighbor.Outdoor,neighbor.NavLength);}
                catch(Exception ex) when(ex is InvalidDataException||ex is FormatException||ex is ArgumentException||ex is OverflowException)
                {neighbor.ValidationRefusal="UnusableOriginalNeighborNav:"+ex.GetType().Name+":"+ex.Message;}
            }
            var outdoor=d.Outdoor;
            Need(d.Layers!=null&&d.Layers.Length==3&&d.Layers.Select(l=>l?.Kind).OrderBy(s=>s).SequenceEqual(new string[]{"SID","SIDG","SIDI"}),"StreetDeclaredGoLayers");
            Need(d.Layers.All(l=>l.Rid!=0&&l.Length>=0&&l.Count>=0&&l.DoorCount>=0&&l.DoorCount<=l.Count&&Hex(l.Sha256,64))&&d.NativeDoors!=null&&
                d.Layers.All(l=>d.NativeDoors.Count(n=>n.Layer==l.Kind)==l.DoorCount),"StreetNativeCounts");
            foreach(NativeDoor door in d.NativeDoors)
            {
                Need(door!=null&&door.Class>=363&&door.Class<=365&&(door.Key>>20)==d.Sector,"StreetNativeDoorIdentity");
                Layer[] matching=d.Layers.Where(l=>l.Kind==door.Layer&&l.Rid==door.Rid).ToArray();Need(matching.Length==1,"StreetNativeDoorLayer");Layer layer=matching[0];byte[] raw=Convert.FromHexString(door.Raw??"");
                FieldInfo field=typeof(GameObjectDefinitions).GetField("Object"+door.Goid);
                Need(raw.Length==74&&BitConverter.ToUInt32(raw)==door.Goid&&BitConverter.ToUInt32(raw,4)==(door.Key&0xfffff)&&Hash(raw).Equals(door.RecordSha256,StringComparison.OrdinalIgnoreCase)&&
                    door.SourceOffset>=0&&door.SourceOffset+74L<=layer.Length&&field?.FieldType.Name=="AttributeClass"+door.Class,"StreetNativeDoorRecord");
                Need(door.Region.HasValue?door.Layer=="SIDI"&&door.Region.Value>=outdoor.Regions.Length&&door.Pivot?.Length==3&&door.Rotation>=0&&door.Rotation<=3:
                    door.Pivot==null&&!door.Rotation.HasValue,"StreetNativeDoorParent");
            }
            Need(d.NativeDoors.Select(v=>v.Layer+":"+v.SourceOffset).Distinct().Count()==d.NativeDoors.Length,"StreetDuplicateNativeRecord");
            Need(d.NativeObjects!=null&&d.NativeObjects.All(n=>n!=null)&&d.Layers.All(l=>d.NativeObjects.Count(n=>n.Layer==l.Kind)==l.Count)&&
                d.NativeObjects.Select(n=>n.Layer+":"+n.SourceOffset).Distinct().Count()==d.NativeObjects.Length,"StreetCompleteNativeObjects");
            foreach(NativeDoor native in d.NativeObjects)
            {
                byte[] raw=Convert.FromHexString(native.Raw??"");Layer[] source=d.Layers.Where(l=>l.Kind==native.Layer&&l.Rid==native.Rid).ToArray();
                Need(raw.Length==74&&BitConverter.ToUInt32(raw)==native.Goid&&BitConverter.ToUInt32(raw,4)==(native.Key&0xfffff)&&(native.Key>>20)==d.Sector&&
                    source.Length==1&&native.SourceOffset>=0&&native.SourceOffset+74L<=source[0].Length&&Hash(raw).Equals(native.RecordSha256,StringComparison.OrdinalIgnoreCase),"StreetNativeObjectRecord");
                Need(native.Region.HasValue?native.Layer=="SIDI"&&native.Region.Value>=outdoor.Regions.Length&&native.Pivot?.Length==3&&native.Rotation>=0&&native.Rotation<=3:
                    native.Pivot==null&&!native.Rotation.HasValue,"StreetNativeObjectParent");
            }
            Need(d.NativeDoors.All(n=>d.NativeObjects.Count(o=>o.Layer==n.Layer&&o.SourceOffset==n.SourceOffset&&o.Raw==n.Raw&&o.Goid==n.Goid&&o.Region==n.Region)==1),"StreetNativeDoorCompleteness");
            Need(d.LegacySources!=null&&d.LegacySources.Length>0&&d.LegacySources.All(s=>s!=null&&s.File!=null&&Hex(s.Sha256,64))&&d.Legacy!=null&&d.Legacy.Select(l=>l.Key).Distinct().Count()==d.Legacy.Length,"StreetLegacySourceIdentity");
            Need(d.Legacy.All(l=>l.Rows!=null&&l.Rows.Length>0&&l.Rows.All(v=>v!=null&&v.Line>=2&&d.LegacySources.Any(f=>f.File==v.File)&&v.Signature!=null&&v.Signature.Position?.Length==3&&
                v.Signature.Position.All(double.IsFinite)&&double.IsFinite(v.Signature.Rotation)&&Hex(v.Signature.Quaternion,32))),"StreetLegacySignatureShape");
        }
        internal bool Has(E.Node node)=>polygons.ContainsKey(node);
        internal Polygon At(E.Node node)=>polygons[node];
        internal E.Node[] Nodes=>polygons.Keys.OrderBy(n=>n.Region).ThenBy(n=>n.Polygon).ToArray();
        internal double[][] Segment(E.Node node,int edge)
        {var a=Vertex(node,edge);var b=Vertex(node,(edge+1)%At(node).Edges.Length);return new double[][]{new double[]{a.X,a.Z},new double[]{b.X,b.Z}};}
        private static bool Same(double a,double b)=>Math.Abs(a-b)<=G.Epsilon;
        private bool OutsideEdge(E.Node from,int edge,out E.Node next)
        {
            next=default(E.Node);byte[] raw=At(from).Edges[edge].Bytes;var target=new E.Node(BitConverter.ToUInt16(raw),BitConverter.ToUInt16(raw,2));
            if(!Has(target))return false;double[][] source=Segment(from,edge);
            for(int i=0;i<At(target).Edges.Length;i++)
            {
                byte[] reverse=At(target).Edges[i].Bytes;if(BitConverter.ToUInt16(reverse)!=from.Region||BitConverter.ToUInt16(reverse,2)!=from.Polygon)continue;
                double[][] other=Segment(target,i);double dx=source[1][0]-source[0][0],dz=source[1][1]-source[0][1],n=dx*dx+dz*dz;
                if(n==0||other.Any(p=>!Same((p[0]-source[0][0])*dz,(p[1]-source[0][1])*dx)))continue;
                double[] t=other.Select(p=>((p[0]-source[0][0])*dx+(p[1]-source[0][1])*dz)/n).ToArray();
                double lo=Math.Max(0,t.Min()),hi=Math.Min(1,t.Max());if((hi-lo)*Math.Sqrt(n)<NavMesh.MinOpenEdge)continue;
                if(new double[]{lo,hi}.Any(v=>!Same(Floor(from,source[0][0]+v*dx,source[0][1]+v*dz),Floor(target,source[0][0]+v*dx,source[0][1]+v*dz))))continue;
                next=target;return true;
            }
            return false;
        }
        internal bool TryOutsidePath(E.Node start,E.Node end,out E.Node[] path)
        {
            path=null;if(!Has(start)||!Has(end)||At(start).Edges.Length<3||At(end).Edges.Length<3)return false;
            var previous=new Dictionary<E.Node,E.Node?>();var queue=new Queue<E.Node>();previous.Add(start,null);queue.Enqueue(start);
            while(queue.Count!=0)
            {
                E.Node from=queue.Dequeue();if(from.Equals(end))break;
                for(int i=0;i<At(from).Edges.Length;i++)if(OutsideEdge(from,i,out E.Node next)&&!previous.ContainsKey(next)){previous.Add(next,from);queue.Enqueue(next);}
            }
            if(!previous.ContainsKey(end))return false;var result=new List<E.Node>();
            for(E.Node? at=end;at.HasValue;at=previous[at.Value])result.Add(at.Value);result.Reverse();path=result.ToArray();return true;
        }
        internal G.Position NativeOrigin(NativeDoor door)
        {
            byte[] raw=Convert.FromHexString(door.Raw);float x=BitConverter.ToSingle(raw,8),y=BitConverter.ToSingle(raw,12),z=BitConverter.ToSingle(raw,16);
            int r=door.Rotation??0;float rx=r==1?z:r==2?-x:r==3?-z:x,rz=r==1?-x:r==2?-z:r==3?x:z;
            float tx=(float)((float)(door.Pivot==null?0:door.Pivot[0]*10)+(float)document.ParentCorner[0]);
            float ty=(float)((float)(door.Pivot==null?0:door.Pivot[1]*10)+(float)document.ParentCorner[1]);
            float tz=(float)((float)(door.Pivot==null?0:door.Pivot[2]*10)+(float)document.ParentCorner[2]);
            return new G.Position((float)(rx+tx),(float)(y+ty),(float)(rz+tz));
        }
        internal NativeDoor[] NativeOnOpening(ushort interiorRegion,double[][] opening,double floor)
        {
            return document.NativeDoors.Where(d=>!d.Region.HasValue||d.Region==interiorRegion).Where(d=>
            {
                G.Position p=NativeOrigin(d);return Same(p.Y,floor)&&Same((p.X-opening[0][0])*(opening[1][1]-opening[0][1]),(p.Z-opening[0][1])*(opening[1][0]-opening[0][0]))&&
                    p.X>=Math.Min(opening[0][0],opening[1][0])&&p.X<=Math.Max(opening[0][0],opening[1][0])&&p.Z>=Math.Min(opening[0][1],opening[1][1])&&p.Z<=Math.Max(opening[0][1],opening[1][1]);
            }).ToArray();
        }
        internal G.Position Vertex(E.Node node,int edge)
        {
            byte[] raw=At(node).Edges[edge].Bytes;double x=(float)(BitConverter.ToSingle(raw,12)+(float)document.ParentCorner[0]);double z=(float)(BitConverter.ToSingle(raw,16)+(float)document.ParentCorner[2]);
            return new G.Position(x,Floor(node,x,z),z);
        }
        internal double Floor(E.Node node,double x,double z)
        {
            double[] p=At(node).Plane;
            // Existing navmesh.py pack_polygons stores an exactly flat raw plane's normalized height as f32.
            // Preserve its original coefficients; this is not a tolerance or flattening of a slope.
            if(p[0]==0&&p[2]==0)return (float)(-p[3]/p[1]);
            return -(p[0]*(x-document.ParentCorner[0])+p[2]*(z-document.ParentCorner[2])+p[3])/p[1];
        }
        internal bool LegacyMatches(StaticWorldObject raw)
        {
            if(raw==null||raw.metrId!=World||raw.sectorID!=Sector||raw.staticId>0xfffff||raw.mxoStaticId!=((uint)Sector<<20|raw.staticId)||raw.type==null||
                !(raw.type.Length==2||raw.type.Length==4&&raw.type[2]==0&&raw.type[3]==0))return false;
            Legacy group=document.Legacy.SingleOrDefault(l=>l.Key==raw.mxoStaticId);return group!=null&&group.Rows.Any(r=>r.Signature.Goid==MissionSites.Goid(raw)&&r.Signature.Exterior==raw.exterior&&
                r.Signature.Position.SequenceEqual(new double[]{raw.pos_x,raw.pos_y,raw.pos_z})&&r.Signature.Rotation==raw.rot&&r.Signature.Quaternion.Equals(raw.quat,StringComparison.OrdinalIgnoreCase));
        }
    }
    internal sealed partial class MissionInteriorEvidence
    {
        internal bool MatchesStreetSource(MissionStreetAccessData.Document d)=>d.World==document.World&&d.Sector==document.Sector&&d.InteriorDigest.Equals(Digest,StringComparison.OrdinalIgnoreCase)&&
            d.NavSha256.Equals(document.NavSha256,StringComparison.OrdinalIgnoreCase)&&d.ParentCorner.SequenceEqual(document.ParentCorner)&&d.Outdoor.Regions.Length==document.OutdoorRegions&&
            d.Layers.Single(l=>l.Kind=="SIDI").Rid==document.SourceRid&&d.Layers.Single(l=>l.Kind=="SIDI").Sha256.Equals(document.SidiSha256,StringComparison.OrdinalIgnoreCase)&&
            d.NativeObjects.Where(n=>n.Region.HasValue).All(n=>regions.TryGetValue(n.Region.Value,out Region region)&&n.Pivot.SequenceEqual(region.Pivot)&&n.Rotation==region.Rotation);
    }
}
