using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;
using hds;
using Door=HeadlessClient.MissionNpcInteriorProof.Door;
namespace HeadlessClient;
public static class NativeBoundFurnitureProof
{
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static void Need(bool b,string why){if(!b)throw new InvalidDataException("Furniture QA: "+why);}
 static StaticWorldObject Raw(string s)=>JsonSerializer.Deserialize<StaticWorldObject>(s,Fields)!;
 public sealed record Step(double[] Standing,uint[] Doors,int Region,int Polygon);
 public sealed record Plan(string Owner,long Started,int Phase,JsonElement Selection,JsonElement Route,Door Floor,Door Lobby,Door[] Doors,double[] Standing,ushort Region,ushort Polygon,double FloorY,double[] Centre,string Quaternion);
 public static Plan Read(string repo,MissionProgress.SavedState state)
 {
  var w=BoundFurniturePublicProof.Select(state);var e=w.GetProperty("FurnitureRoutes");Need(e.GetProperty("Version").GetInt32()==1&&e.GetProperty("Kind").GetString()=="BoundFurnitureRoutes"&&e.GetProperty("FurnitureManifest").GetString()==NativeBoundFurnitureFixture.ManifestSha256,"Separate tagged family");
  var r=e.GetProperty("Routes").EnumerateArray().Single();var o=state.Objects.Single();
  Need(r.GetProperty("ObjectId").GetInt32()==o.Id&&r.GetProperty("Goid").GetUInt16()==o.Goid&&o.Goid==1450&&o.OpenFloor==null&&o.Spot.mxoStaticId==NativeBoundFurnitureFixture.SourceKey&&r.GetProperty("RawSourceJson").GetString()==JsonSerializer.Serialize(o.Spot,Fields)&&r.GetProperty("ProfileManifestSha256").GetString()==NativeBoundFurnitureFixture.ManifestSha256,"Exact owned captured body/source/profile");
  string directory=Path.Combine(repo,"data/mission_bound_furniture_poses/1");string text=File.ReadAllText(Path.Combine(directory,"manifest.json")).Replace("\r\n","\n");Need(Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))==NativeBoundFurnitureFixture.ManifestSha256,"Audited manifest");
  using var m=JsonDocument.Parse(text);var f=m.RootElement.GetProperty("Files").EnumerateArray().Single(v=>v.GetProperty("Sector").GetUInt16()==985);byte[] packed=File.ReadAllBytes(Path.Combine(directory,f.GetProperty("File").GetString()!));Need(Convert.ToHexString(SHA256.HashData(packed)).Equals(f.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase),"Sector hash");
  using var input=new MemoryStream(packed);using var gz=new GZipStream(input,CompressionMode.Decompress);using var profile=JsonDocument.Parse(gz);var p=profile.RootElement.GetProperty("Rows").EnumerateArray().Single(v=>v.GetProperty("NativeKey").GetUInt32()==NativeBoundFurnitureFixture.SourceKey);
  double[] V(JsonElement a)=>a.EnumerateArray().Select(x=>x.GetDouble()).ToArray();var centre=V(p.GetProperty("Centre"));var q=p.GetProperty("Quaternion").GetString()!;
  Need(p.GetProperty("Goid").GetUInt16()==1450&&centre.SequenceEqual(new[]{41445d,1049.98193359375,-27900})&&q=="00000000f304353f00000000f304353f"&&V(r.GetProperty("NativePosition")).SequenceEqual(centre)&&r.GetProperty("NativeQuaternion").GetString()!.Equals(q,StringComparison.OrdinalIgnoreCase),"Independent literal original native pose");
  var standing=V(r.GetProperty("Standing"));var minimum=V(p.GetProperty("Minimum"));var maximum=V(p.GetProperty("Maximum"));var c=w.GetProperty("Context");
  Need(e.GetProperty("EvidenceDigest").GetString()==c.GetProperty("Floor").GetProperty("EvidenceDigest").GetString()&&e.GetProperty("Choices").GetArrayLength()==1&&e.GetProperty("Choices")[0].GetProperty("ObjectId").GetInt32()==o.Id&&e.GetProperty("Choices")[0].GetProperty("Goid").GetUInt16()==o.Goid&&e.GetProperty("Choices")[0].GetProperty("RawSpotJson").GetString()==JsonSerializer.Serialize(o.Spot,Fields),"Complete exact source choices");
  var nodes=r.GetProperty("Nodes").EnumerateArray().ToArray();Need(nodes.Length>0&&nodes[^1].GetRawText()==r.GetProperty("Target").GetRawText()&&r.GetProperty("Region").GetUInt16()==114&&r.GetProperty("Target").GetProperty("Region").GetUInt16()==114,"Fixed target identity");
  Need(standing.Length==3&&standing.All(double.IsFinite)&&standing.All(v=>v==(double)(float)v)&&standing.Where((v,i)=>v<minimum[i]||v>maximum[i]).Any()&&standing[1]==c.GetProperty("Floor").GetProperty("Standing")[1].GetDouble(),"Grounded outside-body fixed Standing");
  float distance=NativeDistance(standing,centre,q);Need(float.IsFinite(distance)&&distance>0&&distance<=10000&&ContainerActions.InReach(standing.SelectMany(BitConverter.GetBytes).ToArray(),centre[0],centre[1],centre[2]),"Original native100/server range");
  Need(NavMesh.Locate(1,standing[0],standing[1],standing[2],out var nav)&&nav.sector==985&&nav.region==114&&nav.floorY+95==standing[1],"Actual original target NAV");
  Door Entry(JsonElement a){var raw=Raw(a.GetProperty("RawDoorJson").GetString()!);return new(raw.mxoStaticId,MissionSites.Goid(raw),V(a.GetProperty("Position")),a.GetProperty("Quaternion").GetString()!,V(a.GetProperty("Standing")),a.GetProperty("RawDoorJson").GetString()!);}
  var doors=r.GetProperty("Doors").EnumerateArray().Select(d=>new Door(d.GetProperty("Key").GetUInt32(),d.GetProperty("Goid").GetUInt16(),V(d.GetProperty("Position")),d.GetProperty("Quaternion").GetString()!,null,d.GetProperty("RawDoorJson").GetString()!)).ToArray();
  Need(doors.Length==1&&doors[0].Key==1032848039&&doors[0].Goid==6958,"Exact own internal6958, not borrowed6959 alias");
  return new(state.Identity,state.Started,state.Phase,w.Clone(),r.Clone(),Entry(c.GetProperty("Floor")),Entry(c.GetProperty("Lobby")),doors,V(r.GetProperty("Standing")),r.GetProperty("Region").GetUInt16(),r.GetProperty("Target").GetProperty("Polygon").GetUInt16(),c.GetProperty("Floor").GetProperty("Standing")[1].GetDouble()-95,centre,q);
 }
 public static void RequireCurrent(Plan plan,MissionProgress.SavedState state)
 {Need(state.Mission==301210&&state.Identity==plan.Owner&&state.Started==plan.Started&&state.Phase==plan.Phase&&BoundFurniturePublicProof.Select(state).GetRawText()==plan.Selection.GetRawText(),"Current owned route/source");}
 public static float NativeDistance(double[] at,double[] centre,string quaternion)
 {
  var q=Convert.FromHexString(quaternion);float x=BitConverter.ToSingle(q),y=BitConverter.ToSingle(q,4),z=BitConverter.ToSingle(q,8),w=BitConverter.ToSingle(q,12),tx=2*x,ty=2*y,tz=2*z;
  float xx=tx*x,xy=ty*x,xz=tz*x,yy=ty*y,yz=tz*y,zz=tz*z,xw=tx*w,yw=ty*w,zw=tz*w;float[] b={1-(zz+yy),xy+zw,xz-yw,xy-zw,1-(zz+xx),yz+xw,xz+yw,yz-xw,1-(xx+yy)};
  float[] d={(float)(at[0]-centre[0]),(float)(at[1]-centre[1]),(float)(at[2]-centre[2])};float[] h={84,49.981971740722656f,29.19049835205078f};float sum=0;
  for(int i=0;i<3;i++){float local=(float)((double)d[0]*b[i*3]+(double)d[1]*b[i*3+1]+(double)d[2]*b[i*3+2]);float outside=local<-h[i]?(float)(local+h[i]):local>h[i]?(float)(local-h[i]):0;sum=(float)((double)outside*outside+sum);}return sum;
 }
    public static Step[] Steps(string repository,Plan plan)
    {
        var source=Raw(plan.Floor.Raw);string expectedDigest=plan.Selection.GetProperty("Context").GetProperty("Floor").GetProperty("EvidenceDigest").GetString()!;
        using var doc=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(repository,source.sectorID,expectedDigest));var root=doc.RootElement;
        bool localEvidence=root.GetProperty("Format").GetInt32()==2;
        Need(root.GetProperty("World").GetInt32()==1&&root.GetProperty("Sector").GetInt32()==source.sectorID,"Bound route QA explicit world/sector");
        void NativeDoor(Door door,JsonElement? entry,int? sourceRegion=null,int? targetRegion=null)
        {
            var raw=Raw(door.Raw);using var ownDoc=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(repository,raw.sectorID,entry?.GetProperty("EvidenceDigest").GetString()??expectedDigest));var group=ownDoc.RootElement.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==door.Key);
            Need((entry.HasValue ? group.GetProperty("Legacy").EnumerateArray().ToArray() : new[]{MissionDoorOccurrences.OriginalLegacy(group)}).Any(l=>
            {var s=l.GetProperty("Signature");return s.GetProperty("Goid").GetUInt16()==door.Goid&&s.GetProperty("Position").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(new[]{raw.pos_x,raw.pos_y,raw.pos_z})&&s.GetProperty("Rotation").GetDouble()==raw.rot&&string.Equals(s.GetProperty("Quaternion").GetString(),raw.quat,StringComparison.OrdinalIgnoreCase);}),"Bound route QA exact legacy source signature");
            int? entryRegion=entry?.GetProperty("Node").GetProperty("Region").GetInt32();
            var own=MissionDoorOccurrences.ForPortal(group,door.Goid,entryRegion??sourceRegion,entryRegion??targetRegion);
            Need(own.Length>0&&own.All(o=>o.GetProperty("ModelPosition").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(door.Position)),"Bound route QA native position consensus");
            Need(own.Length>0&&(!entry.HasValue||own.Length==1)&&own.All(o=>string.Equals(o.GetProperty("Quaternion").GetString(),door.Quaternion,StringComparison.OrdinalIgnoreCase)),"Bound route QA context-selected own-GO yaw");
        }
        NativeDoor(plan.Floor,plan.Selection.GetProperty("Context").GetProperty("Floor"));NativeDoor(plan.Lobby,plan.Selection.GetProperty("Context").GetProperty("Lobby"));
        var nodes=plan.Route.GetProperty("Nodes").EnumerateArray().ToArray();Need(nodes.Length>0,"Bound route nonempty topology path");
        int R(JsonElement n)=>n.GetProperty("Region").GetInt32();int P(JsonElement n)=>n.GetProperty("Polygon").GetInt32();
        JsonElement Polygon(JsonElement n){var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(n));Need(region.GetProperty("Available").GetBoolean(),"Bound route missing prefab is not route");return region.GetProperty("Polygons")[P(n)];}
        double[] Local(JsonElement node,double x,double z)
        {
            var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(node));var pivot=region.GetProperty("Pivot");var corner=root.GetProperty("ParentCorner");
            double a=x-(float)((float)(pivot[0].GetInt32()*10)+(float)corner[0].GetDouble()),b=z-(float)((float)(pivot[2].GetInt32()*10)+(float)corner[2].GetDouble());
            return region.GetProperty("Rotation").GetInt32() switch{0=>new[]{a,b},1=>new[]{-b,a},2=>new[]{-a,-b},_=>new[]{b,-a}};
        }
        double Floor(JsonElement poly,JsonElement node,double x,double z)
        {
            if(!localEvidence){var f=poly.GetProperty("Plane").EnumerateArray().Select(v=>v.GetDouble()).ToArray();Need(f.Length==4&&f[1]!=0,"Bound route plane shape");return -(f[0]*x+f[2]*z+f[3])/f[1];}
            byte[] raw=Convert.FromHexString(poly.GetProperty("RawHeader").GetString()!);Need(raw.Length==20,"Bound route raw local plane header");var plane=Enumerable.Range(0,4).Select(i=>(double)BitConverter.ToSingle(raw,i*4)).ToArray();var at=Local(node,x,z);
            var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(node));double y=(float)((float)(region.GetProperty("Pivot")[1].GetInt32()*10)+(float)root.GetProperty("ParentCorner")[1].GetDouble());
            return -(plane[0]*at[0]+plane[2]*at[1]+plane[3])/plane[1]+y;
        }
        bool Contains(JsonElement polygon,JsonElement node,double[] point)
        {
            if(Math.Abs(point[1]-(Floor(polygon,node,point[0],point[2])+95))>1e-7)return false;
            double[][] vertices=polygon.GetProperty("Edges").EnumerateArray().Select(e=>{if(!localEvidence){var v=e.GetProperty("Vertex");return new[]{v[0].GetDouble(),v[2].GetDouble()};}var raw=Convert.FromHexString(e.GetProperty("Raw").GetString()!);Need(raw.Length==20,"original local edge bytes");return new[]{(double)BitConverter.ToSingle(raw,12),(double)BitConverter.ToSingle(raw,16)};}).ToArray();
            var at=localEvidence?Local(node,point[0],point[2]):new[]{point[0],point[2]};bool positive=true,negative=true;
            for(int i=0;i<vertices.Length;i++){var a=vertices[i];var b=vertices[(i+1)%vertices.Length];double cross=(b[0]-a[0])*(at[1]-a[1])-(b[1]-a[1])*(at[0]-a[0]);positive&=cross>=-1e-7;negative&=cross<=1e-7;}
            return positive||negative;
        }
        Need(nodes.Select(n=>(R(n),P(n))).Distinct().Count()==nodes.Length,"no repeated/cyclic saved nodes");
        foreach(var n in nodes){var poly=Polygon(n);Need(poly.GetProperty("Edges").EnumerateArray().All(e=>{var v=e.GetProperty("Vertex");return Math.Abs(Floor(poly,n,v[0].GetDouble(),v[2].GetDouble())-plan.FloorY)<=1e-7;}),"every original polygon stays on actual floor");}
        Need(Contains(Polygon(nodes[^1]),nodes[^1],plan.Standing),"exact persisted target polygon contains fixed Standing; Locate tie cannot rewrite it");
        Need(nodes[0].GetRawText()==plan.Selection.GetProperty("Context").GetProperty("Floor").GetProperty("Node").GetRawText(),"Bound route topology begins actual entry");var steps=new List<Step>{new(plan.Floor.Standing!,Array.Empty<uint>(),R(nodes[0]),P(nodes[0]))};
        for(int i=1;i<nodes.Length;i++)
        {
            var a=Polygon(nodes[i-1]);var b=Polygon(nodes[i]);double[] x,z;uint[] doors;
            if(R(nodes[i-1])!=R(nodes[i]))
            {
                var edges=root.GetProperty("Portals").EnumerateArray().Where(q=>q.GetProperty("SourceRegion").GetInt32()==R(nodes[i-1])&&q.GetProperty("SourcePolygon").GetInt32()==P(nodes[i-1])&&q.GetProperty("TargetRegion").GetInt32()==R(nodes[i])&&q.GetProperty("TargetPolygon").GetInt32()==P(nodes[i])).ToArray();Need(edges.Length==1,"Bound route unique original portal");var edge=edges[0];if(localEvidence)Need(edge.GetProperty("Transition").GetString()=="SameFloor","Bound route route cannot cross an unproved/partial/different-floor portal");
                Need(root.GetProperty("Portals").EnumerateArray().Count(q=>q.GetProperty("SourceRegion").GetInt32()==R(nodes[i])&&q.GetProperty("SourcePolygon").GetInt32()==P(nodes[i])&&q.GetProperty("TargetRegion").GetInt32()==R(nodes[i-1])&&q.GetProperty("TargetPolygon").GetInt32()==P(nodes[i-1])&&q.GetProperty("SourceEdge").GetInt32()==edge.GetProperty("TargetEdge").GetInt32()&&q.GetProperty("TargetEdge").GetInt32()==edge.GetProperty("SourceEdge").GetInt32())==1,"Bound route reciprocal portal");
                var ends=edge.GetProperty("Endpoints");x=ends[0].EnumerateArray().Select(v=>v.GetDouble()).ToArray();z=ends[1].EnumerateArray().Select(v=>v.GetDouble()).ToArray();doors=edge.GetProperty("DoorKeys").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();foreach(uint key in doors){Need(plan.Doors.Count(d=>d.Key==key)==1,"crossing requires one saved door");NativeDoor(plan.Doors.Single(d=>d.Key==key),null,R(nodes[i-1]),R(nodes[i]));}
            }
            else
            {
                var edges=a.GetProperty("Edges").EnumerateArray().ToArray();int edge=Array.FindIndex(edges,e=>e.GetProperty("Neighbor").GetInt32()==P(nodes[i])&&e.GetProperty("RegionTag").GetInt32()==65535&&(!localEvidence||e.GetProperty("Reference").GetString()=="Local"));Need(edge>=0,"Bound route original polygon neighbor");
                var start=edges[edge].GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()).ToArray();var end=edges[(edge+1)%edges.Length].GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()).ToArray();x=new[]{start[0],start[2]};z=new[]{end[0],end[2]};doors=Array.Empty<uint>();
                Need(b.GetProperty("Edges").EnumerateArray().Any(e=>e.GetProperty("Neighbor").GetInt32()==P(nodes[i-1])&&e.GetProperty("RegionTag").GetInt32()==65535&&(!localEvidence||e.GetProperty("Reference").GetString()=="Local")),"Bound route reciprocal polygon neighbor");
            }
            double px=(x[0]+z[0])/2,pz=(x[1]+z[1])/2;double floorA=Floor(a,nodes[i-1],px,pz),floorB=Floor(b,nodes[i],px,pz);
            Need(localEvidence?Math.Abs(floorA-floorB)<=1e-7:floorA==floorB,"Bound route portal/edge floor agreement");Need(doors.All(k=>plan.Doors.Count(d=>d.Key==k)==1),"Bound route every portal door is saved own-GO proof");steps.Add(new(new[]{px,floorA+95,pz},doors,R(nodes[i]),P(nodes[i])));
        }
        Need(steps.SelectMany(s=>s.Doors).Distinct().Order().SequenceEqual(plan.Doors.Select(d=>d.Key).Order()),"every saved required door is used and every crossed door is saved");
        Need(steps.All(s=>Math.Abs(s.Standing[1]-(plan.FloorY+95))<=1e-7),"whole route stays on actual arrival floor");
        steps.Add(new(plan.Standing,Array.Empty<uint>(),plan.Region,plan.Polygon));return steps.ToArray();
    }
}
