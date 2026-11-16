using System.Text.Json;
using System.Security.Cryptography;
using hds;
namespace HeadlessClient;

// QA only: exact saved Objects-stage actor, original same-region local links. No NPC witness is minted.
public static class MachinesObjectActorApproach
{
 public sealed record Plan(MissionProgress.SavedState Before,ushort Npc,string ActorJson,string StreetRaw,
  OwnViewLifeSnapshot Life,MissionNpcInteriorProof.RouteStep[] Steps);
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static void Need(bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
 static string Actor(MissionProgress.SavedState s,ushort npc)
 {
  var rows=s.Mobs.Where(m=>m.Npc==npc).ToArray();Need(rows.Length==1,"One current saved actor");var m=rows[0];
  Need(Guid.TryParseExact(m.Identity,"N",out _)&&!m.Dead&&m.Health>0&&double.IsFinite(m.X)&&double.IsFinite(m.Y)&&double.IsFinite(m.Z),"Current live owned actor");
  return JsonSerializer.Serialize(new{m.Identity,m.Npc,m.Phase,m.Name,m.Rsi,m.Level,m.Friendly,m.X,m.Y,m.Z},Fields);
 }
 public static void RequireCurrent(Plan p,MissionProgress.SavedState state,OwnViewLifeSnapshot life)
 {
  Need(p.Life.Current&&life.Current&&life.OwnViewReady&&ReferenceEquals(p.Life.Owner,life.Owner)&&p.Life.Life==life.Life,"Objects actor route current owner/life");
  ZionAfter1212RidePlan.SameEntry(p.Before,state);
  Need(ZionAfter1212StreetEntry.ForSite(state)?.Raw.GetRawText()==p.StreetRaw&&Actor(state,p.Npc)==p.ActorJson,"Objects actor exact accepted source/pose identity");
 }
 public static Plan Read(string repository,MissionProgress.SavedState state,ushort npc,OwnViewLifeSnapshot life)
 {
  Need(life.Current&&life.OwnViewReady,"Objects actor current own life");
  var street=ZionAfter1212StreetEntry.ForSite(state)??throw new InvalidDataException("Owned Objects Street entry required");
  Need(street.Raw.GetProperty("Kind").GetString()=="Objects"&&street.Raw.GetProperty("Version").GetInt32()==1&&state.Objects.Count>0&&
   state.Objects.Any(o=>o.OpenFloor!=null),"Only existing certified free Objects stage");
  string actorJson=Actor(state,npc);var actor=state.Mobs.Single(m=>m.Npc==npc);var target=new[]{actor.X,actor.Y,actor.Z};
  var raw=JsonSerializer.Deserialize<StaticWorldObject>(street.Floor.Raw,Fields)!;var floor=street.Raw.GetProperty("Context").GetProperty("Floor");
  using var doc=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(repository,raw.sectorID,floor.GetProperty("EvidenceDigest").GetString()!));var root=doc.RootElement;
  Need(root.GetProperty("Format").GetInt32()==2&&root.GetProperty("World").GetInt32()==1&&root.GetProperty("Sector").GetInt32()==raw.sectorID,"Pinned original format2 scene identity");
  int regionId=floor.GetProperty("Node").GetProperty("Region").GetInt32(),start=floor.GetProperty("Node").GetProperty("Polygon").GetInt32();
  var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==regionId);
  Need(region.GetProperty("Available").GetBoolean(),"Original region evidence available");var polys=region.GetProperty("Polygons").EnumerateArray().ToArray();
  Need(start>=0&&start<polys.Length,"Original entry polygon ordinal");
  // Native floor identity is independent of legacy CSV; orientation is unique to selected original region.
  var group=root.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==raw.mxoStaticId);
  Need(group.GetProperty("Legacy").EnumerateArray().Any(l=>{var s=l.GetProperty("Signature");return s.GetProperty("Goid").GetUInt16()==street.Floor.Goid&&
   s.GetProperty("Position").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(new[]{raw.pos_x,raw.pos_y,raw.pos_z})&&s.GetProperty("Rotation").GetDouble()==raw.rot&&
   string.Equals(s.GetProperty("Quaternion").GetString(),raw.quat,StringComparison.OrdinalIgnoreCase);}),"Exact audited raw floor signature");
  var occurrences=group.GetProperty("Occurrences").EnumerateArray().ToArray();Need(occurrences.All(o=>o.GetProperty("ModelPosition").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(street.Floor.Position)),"Native shaft position consensus");
  var own=occurrences.Where(o=>o.GetProperty("Goid").GetUInt16()==street.Floor.Goid&&o.GetProperty("Region").GetInt32()==regionId).ToArray();
  Need(own.Length==1&&string.Equals(own[0].GetProperty("Quaternion").GetString(),street.Floor.Quaternion,StringComparison.OrdinalIgnoreCase),"Unique context own-GOID floor yaw");
  Need(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repository,"data","navmesh","1.bin.gz"))))=="2B012BFA11693F4ACD9020565F5661FA2324A7DF69BB6870F785ED0DA144F260","Pinned world1 NAV artifact");
  SectorProperties.Load(Path.Combine(repository,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(repository,"data","navmesh"));
  Need(NavMesh.Locate(1,target[0],target[1],target[2],out var nav)&&nav.indoors&&nav.sector==raw.sectorID&&nav.region==regionId&&nav.floorY+95==target[1]&&target[1]==street.Floor.Standing![1],"Exact actor NAV original region/floor; no projection");
  double[] Local(double x,double z)
  {
   var pivot=region.GetProperty("Pivot");var corner=root.GetProperty("ParentCorner");double a=x-(float)((float)(pivot[0].GetInt32()*10)+(float)corner[0].GetDouble()),b=z-(float)((float)(pivot[2].GetInt32()*10)+(float)corner[2].GetDouble());
   return region.GetProperty("Rotation").GetInt32() switch{0=>new[]{a,b},1=>new[]{-b,a},2=>new[]{-a,-b},_=>new[]{b,-a}};
  }
  double Floor(JsonElement p,double x,double z)
  {
   var bytes=Convert.FromHexString(p.GetProperty("RawHeader").GetString()!);Need(bytes.Length==20,"Original counted plane bytes");var f=Enumerable.Range(0,4).Select(i=>(double)BitConverter.ToSingle(bytes,4*i)).ToArray();Need(f.All(double.IsFinite)&&f[1]!=0,"Finite original local plane");var at=Local(x,z);
   return -(f[0]*at[0]+f[2]*at[1]+f[3])/f[1]+(float)((float)(region.GetProperty("Pivot")[1].GetInt32()*10)+(float)root.GetProperty("ParentCorner")[1].GetDouble());
  }
  bool Contains(JsonElement p,double[] point)
  {
   var edges=p.GetProperty("Edges").EnumerateArray().ToArray();if(edges.Length<3||Math.Abs(Floor(p,point[0],point[2])+95-point[1])>1e-7)return false;
   var vertices=edges.Select(e=>{var b=Convert.FromHexString(e.GetProperty("Raw").GetString()!);Need(b.Length==20,"Original raw local edge bytes");return new[]{(double)BitConverter.ToSingle(b,12),(double)BitConverter.ToSingle(b,16)};}).ToArray();var at=Local(point[0],point[2]);bool pos=true,neg=true;
   for(int i=0;i<vertices.Length;i++){var a=vertices[i];var b=vertices[(i+1)%vertices.Length];double c=(b[0]-a[0])*(at[1]-a[1])-(b[1]-a[1])*(at[0]-a[0]);pos&=c>=-1e-7;neg&=c<=1e-7;}return pos||neg;
  }
  bool Level(int n)=>n>=0&&n<polys.Length&&polys[n].GetProperty("Edges").GetArrayLength()>=3&&polys[n].GetProperty("Edges").EnumerateArray().All(e=>{var v=e.GetProperty("Vertex");return Math.Abs(Floor(polys[n],v[0].GetDouble(),v[2].GetDouble())+95-target[1])<=1e-7;});
  Need(Level(start)&&Contains(polys[start],street.Floor.Standing!),"Saved floor Standing exact containing original node");
  int end=nav.polygon;Need(Level(end)&&Contains(polys[end],target),"Exact actor containing original node");
  bool LocalEdge(JsonElement e)=>e.GetProperty("Reference").GetString()=="Local"&&e.GetProperty("RegionTag").GetInt32()==65535;
  var previous=new Dictionary<int,int>{{start,-1}};var queue=new Queue<int>();queue.Enqueue(start);
  while(queue.Count>0){int at=queue.Dequeue();if(at==end)break;foreach(var edge in polys[at].GetProperty("Edges").EnumerateArray())
   {if(!LocalEdge(edge))continue;int next=edge.GetProperty("Neighbor").GetInt32();if(previous.ContainsKey(next)||!Level(next))continue;
    if(!polys[next].GetProperty("Edges").EnumerateArray().Any(e=>LocalEdge(e)&&e.GetProperty("Neighbor").GetInt32()==at))continue;
    previous.Add(next,at);queue.Enqueue(next);}}
  Need(previous.ContainsKey(end),"No proved same-region doorless local path; portals/required doors are not inferred");var nodes=new List<int>();for(int n=end;n>=0;n=previous[n])nodes.Add(n);nodes.Reverse();
  var steps=new List<MissionNpcInteriorProof.RouteStep>{new(street.Floor.Standing!,Array.Empty<uint>(),regionId,start)};
  for(int i=1;i<nodes.Count;i++)
  {
   var edges=polys[nodes[i-1]].GetProperty("Edges").EnumerateArray().ToArray();var candidates=Enumerable.Range(0,edges.Length).Where(k=>LocalEdge(edges[k])&&edges[k].GetProperty("Neighbor").GetInt32()==nodes[i]).ToArray();Need(candidates.Length==1,"Unique reciprocal local edge");int edge=candidates[0];var a=edges[edge].GetProperty("Vertex");var b=edges[(edge+1)%edges.Length].GetProperty("Vertex");double x=(a[0].GetDouble()+b[0].GetDouble())/2,z=(a[2].GetDouble()+b[2].GetDouble())/2;var point=new[]{x,Floor(polys[nodes[i-1]],x,z)+95,z};
   Need(Contains(polys[nodes[i-1]],point)&&Contains(polys[nodes[i]],point),"Reciprocal original edge endpoints/floor overlap");steps.Add(new(point,Array.Empty<uint>(),regionId,nodes[i]));
  }
  steps.Add(new(target,Array.Empty<uint>(),regionId,end));
  return new(state,npc,actorJson,street.Raw.GetRawText(),life,steps.ToArray());
 }
}
