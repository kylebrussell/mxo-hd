using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;
using hds;
using Door=HeadlessClient.MissionNpcInteriorProof.Door;
namespace HeadlessClient;

// Observation only: independent pinned data and immutable saved route. Never projects or selects a source.
public static class MissionBoundFurnitureRouteProof
{
 public const string FurnitureManifest="ECAE0AEE396F5754B30818EC73E9A652A42BE42B6584CB5F46BA5C63524AA13E";
 public const string CabinetManifest="95657513146AF308F60D6E532624E143A572B649A1CAD98C4601B9EB8AC1C234";
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static void Need(bool b,string why){if(!b)throw new InvalidDataException("Furniture route QA: "+why);}
 static StaticWorldObject Raw(string s)=>JsonSerializer.Deserialize<StaticWorldObject>(s,Fields)??throw new InvalidDataException("Missing raw source");
 static double[] V(JsonElement e){Need(e.ValueKind==JsonValueKind.Array,"Point array");var p=e.EnumerateArray().Select(x=>x.GetDouble()).ToArray();Need(p.Length==3&&p.All(double.IsFinite),"Finite triple");return p;}
 static bool Hash(string? a,string? b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
 static void Shape(JsonElement e,params string[] names)=>Need(e.ValueKind==JsonValueKind.Object&&e.EnumerateObject().Count()==names.Length&&names.All(n=>e.TryGetProperty(n,out _)),"Exact recognized fields");
 static readonly ushort[] NewGoids={573,1431,1432,1433,1434,1435,1438,1439,1443,1444,1445,1446,1447,1448,1449,1450,1451,6987,37913,38484};
 static bool Supported(ushort goid)=>goid==326||NewGoids.Contains(goid);
 public static bool SupportsGoid(ushort goid)=>Supported(goid);
 public sealed record Step(double[] Standing,uint[] Doors,int Region,int Polygon);
 public sealed record Plan(int Mission,ushort Instance,int ObjectId,ushort Goid,string Owner,long Started,int Phase,string Sources,JsonElement Selection,JsonElement Route,Door Floor,Door Lobby,Door[] Doors,double[] Standing,ushort Region,ushort Polygon,double FloorY,double[] Centre,string Quaternion);

 // Shape dispatch only; Read independently proves current profile/geometry before any object-route use.
 public static void SelectionVersion(JsonElement w)
 {
  try
  {
   Shape(w,"Version","Mission","Identity","Started","Phase","Site","Kind","Context","Refusal","FurnitureRoutes");
   Need(w.GetProperty("Version").GetInt32()==3&&w.GetProperty("Kind").GetString()=="Objects"&&w.GetProperty("Refusal").ValueKind==JsonValueKind.Null,"Tagged StreetV3");
   var e=w.GetProperty("FurnitureRoutes");Shape(e,"Version","Kind","EvidenceDigest","FurnitureManifest","Choices","Routes");
   Need(e.GetProperty("Version").GetInt32()==1&&e.GetProperty("Kind").GetString()=="BoundFurnitureRoutes"&&Hash(e.GetProperty("FurnitureManifest").GetString(),FurnitureManifest)&&e.GetProperty("EvidenceDigest").GetString()==w.GetProperty("Context").GetProperty("Floor").GetProperty("EvidenceDigest").GetString(),"V3 envelope type/profile/context");
   var choices=e.GetProperty("Choices").EnumerateArray().ToArray();Need(choices.Length>0&&choices.Select(c=>c.GetProperty("ObjectId").GetUInt16()).Distinct().Count()==choices.Length,"V3 unique complete choices");
   foreach(var c in choices){Shape(c,"ObjectId","Goid","RawSpotJson");Need(c.GetProperty("ObjectId").GetUInt16()>0&&(Supported(c.GetProperty("Goid").GetUInt16())||c.GetProperty("Goid").GetUInt16()==1401)&&!string.IsNullOrWhiteSpace(c.GetProperty("RawSpotJson").GetString()),"V3 choice shape");}
   var routes=e.GetProperty("Routes").EnumerateArray().ToArray();Need(routes.Length==choices.Count(c=>Supported(c.GetProperty("Goid").GetUInt16()))&&routes.Length>0&&routes.Any(r=>NewGoids.Contains(r.GetProperty("Goid").GetUInt16()))&&routes.Select(r=>r.GetProperty("ObjectId").GetUInt16()).Distinct().Count()==routes.Length,"V3 distinct typed routes");
   foreach(var r in routes)
   {
    Shape(r,"ObjectId","Goid","ProfileManifestSha256","RawSourceJson","Region","NativePosition","NativeQuaternion","Standing","Target","Nodes","Doors");ushort goid=r.GetProperty("Goid").GetUInt16();int id=r.GetProperty("ObjectId").GetUInt16();
    Need(id>0&&Supported(goid)&&Hash(r.GetProperty("ProfileManifestSha256").GetString(),goid==326?CabinetManifest:FurnitureManifest)&&choices.Count(c=>c.GetProperty("ObjectId").GetUInt16()==id&&c.GetProperty("Goid").GetUInt16()==goid&&c.GetProperty("RawSpotJson").GetString()==r.GetProperty("RawSourceJson").GetString())==1,"V3 exact typed choice association");
    _=V(r.GetProperty("NativePosition"));_=V(r.GetProperty("Standing"));CheckQuaternion(r.GetProperty("NativeQuaternion").GetString()!);Shape(r.GetProperty("Target"),"Region","Polygon");
    var nodes=r.GetProperty("Nodes").EnumerateArray().ToArray();Need(nodes.Length>0&&nodes[^1].GetRawText()==r.GetProperty("Target").GetRawText()&&nodes.Select(n=>n.GetRawText()).Distinct().Count()==nodes.Length,"V3 saved noncyclic target");foreach(var n in nodes)Shape(n,"Region","Polygon");
    var doors=r.GetProperty("Doors").EnumerateArray().ToArray();Need(doors.Select(d=>d.GetProperty("Key").GetUInt32()).Distinct().Count()==doors.Length,"V3 unique doors");foreach(var d in doors){Shape(d,"Key","Goid","RawDoorJson","Quaternion","Position");Need(MissionInteriorDoorTypes.IsSupported(d.GetProperty("Goid").GetUInt16()),"V3 supported own door type");_=V(d.GetProperty("Position"));CheckQuaternion(d.GetProperty("Quaternion").GetString()!);}
   }
  }
  catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException or FormatException){throw new InvalidDataException("Malformed V3 selection shape",e);}
 }
 // Null grants no new authority: old/unenrolled selections must still use their existing strict validator. Malformed V3 throws.
 public static Plan? Read(string repo,MissionProgress.SavedState state,int objectId)
 {
  try{return ReadCore(repo,state,objectId);}
  catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException or IndexOutOfRangeException or FormatException or FileNotFoundException or DirectoryNotFoundException)
  {throw new InvalidDataException("Malformed recognized furniture QA route",e);}
 }
 static JsonElement? Selection(MissionProgress.SavedState s)
 {
  Need(s.Version is 1 or 2,"SavedState version remains1/2");
  if(s.StreetAccess.ValueKind==JsonValueKind.Undefined)return null;
  var e=s.StreetAccess;Need(s.Version==2&&s.Mission>0&&s.Instance>0&&s.Phase>=0&&s.Started>0&&Guid.TryParseExact(s.Identity,"N",out _)&&s.Flags!=null&&!s.Flags.GetValueOrDefault("failed"),"Accepted owner");
  Need(e.ValueKind==JsonValueKind.Object&&e.GetProperty("Version").GetInt32()==1&&e.GetProperty("Kind").GetString()=="StreetAccess"&&e.GetProperty("Enabled").GetBoolean(),"Street envelope");
  Need(!e.TryGetProperty("Pending",out var pending)||pending.ValueKind==JsonValueKind.Null,"Current committed selection, not pending");
  Need(s.OpenFloorCurrentSiteIndex.HasValue&&s.OpenFloorSites!=null,"Current selected site");int index=s.OpenFloorCurrentSiteIndex!.Value;
  Need(index>=0&&index<s.OpenFloorSites!.Length&&index<s.SiteDoors.Length,"Current selected site index");var site=s.OpenFloorSites[index];var raw=s.SiteDoors[index];
  var all=e.GetProperty("Witnesses").EnumerateArray().ToArray();
  foreach(var w in all)
  {
   int version=w.GetProperty("Version").GetInt32();Need(version is 1 or 2 or 3,"Known Street selection version");
   Need(w.GetProperty("Mission").GetInt32()==s.Mission&&w.GetProperty("Identity").GetString()==s.Identity&&w.GetProperty("Started").GetInt64()==s.Started&&w.GetProperty("Phase").GetInt32()>=0&&w.GetProperty("Phase").GetInt32()<=s.Phase,"Exact owner/history");
   if(version!=3)Need(!w.TryGetProperty("FurnitureRoutes",out _),"Old selection cannot carry new metadata");
  }
  var matches=all.Where(w=>w.GetProperty("Phase").GetInt32()==s.Phase).ToArray();Need(matches.Length<=1,"Unique current selection");
  if(matches.Length==0)return null;var row=matches[0];if(row.GetProperty("Version").GetInt32()!=3)return null;
  SelectionVersion(row);
  Need(row.GetProperty("Kind").GetString()=="Objects"&&row.GetProperty("Refusal").ValueKind==JsonValueKind.Null,"Owned object route");
  var selected=row.GetProperty("Site");Need(site.District==1&&raw.metrId==1&&selected.GetProperty("District").GetInt32()==1&&selected.GetProperty("Fingerprint").GetString()==site.Fingerprint&&selected.GetProperty("DoorKey").GetUInt32()==site.DoorKey&&site.DoorKey==raw.mxoStaticId&&
   selected.GetProperty("DoorX").GetDouble()==site.DoorX&&selected.GetProperty("DoorY").GetDouble()==site.DoorY&&selected.GetProperty("DoorZ").GetDouble()==site.DoorZ&&new[]{raw.pos_x,raw.pos_y,raw.pos_z}.SequenceEqual(new[]{site.DoorX,site.DoorY,site.DoorZ}),"Exact world/site/raw door");
  var c=row.GetProperty("Context");Need(c.GetProperty("Refusal").ValueKind==JsonValueKind.Null&&c.GetProperty("Kind").GetString()=="Objects","Valid context");
  var floor=Entry(c.GetProperty("Floor"));var lobby=Entry(c.GetProperty("Lobby"));
  Need(floor.Raw==JsonSerializer.Serialize(raw,Fields)&&floor.Key==site.DoorKey&&s.Waypoints.Length==1&&s.Exits.Length==1&&s.Waypoints[0].doorHex!=null&&BitConverter.ToUInt32(Convert.FromHexString(s.Waypoints[0].doorHex))==floor.Key&&
   new[]{s.Waypoints[0].x,s.Waypoints[0].y,s.Waypoints[0].z}.SequenceEqual(floor.Position)&&s.Exits[0].SequenceEqual(lobby.Position),"Persisted floor/lobby context");return row;
 }
 static Door Entry(JsonElement e)
 {
  string rawJson=e.GetProperty("RawDoorJson").GetString()!;var raw=Raw(rawJson);Need(raw.metrId==1&&raw.mxoStaticId==((uint)raw.sectorID<<20|raw.staticId)&&MissionSites.Goid(raw) is 417 or 419,"Own elevator identity");
  string q=e.GetProperty("Quaternion").GetString()!;CheckQuaternion(q);return new(raw.mxoStaticId,MissionSites.Goid(raw),V(e.GetProperty("Position")),q,V(e.GetProperty("Standing")),rawJson);
 }
 static string Sources(MissionProgress.SavedState s)=>JsonSerializer.Serialize(s.Objects.OrderBy(o=>o.Id).Select(o=>new{o.Id,o.Goid,o.Spot,o.OpenFloor}),Fields);
 static Plan? ReadCore(string repo,MissionProgress.SavedState state,int objectId)
 {
  var selected=Selection(state);if(!selected.HasValue)return null;var w=selected.Value;var e=w.GetProperty("FurnitureRoutes");
  Shape(e,"Version","Kind","EvidenceDigest","FurnitureManifest","Choices","Routes");Need(e.GetProperty("Version").GetInt32()==1&&e.GetProperty("Kind").GetString()=="BoundFurnitureRoutes"&&Hash(e.GetProperty("FurnitureManifest").GetString(),FurnitureManifest),"Typed V3 family");
  var c=w.GetProperty("Context");Need(e.GetProperty("EvidenceDigest").GetString()==c.GetProperty("Floor").GetProperty("EvidenceDigest").GetString(),"Exact evidence digest");
  var objects=state.Objects.ToArray();Need(objects.Length>0&&objects.Select(o=>o.Id).Distinct().Count()==objects.Length,"Current distinct objects");
  var choices=e.GetProperty("Choices").EnumerateArray().ToArray();Need(choices.Length==objects.Length&&choices.Select(x=>x.GetProperty("ObjectId").GetInt32()).Distinct().Count()==choices.Length,"Complete unique choices");
  foreach(var choice in choices){Shape(choice,"ObjectId","Goid","RawSpotJson");var o=objects.Single(x=>x.Id==choice.GetProperty("ObjectId").GetInt32());Need(o.Id>0&&o.Id<=ushort.MaxValue&&o.OpenFloor==null&&o.Spot!=null&&(Supported(o.Goid)||o.Goid==1401)&&o.Goid==MissionSites.Goid(o.Spot)&&choice.GetProperty("Goid").GetUInt16()==o.Goid&&choice.GetProperty("RawSpotJson").GetString()==JsonSerializer.Serialize(o.Spot,Fields),"Exact immutable choice/source");}
  var routes=e.GetProperty("Routes").EnumerateArray().ToArray();var required=objects.Where(o=>Supported(o.Goid)).ToArray();Need(required.Length==routes.Length&&routes.Length>0&&required.Any(o=>o.Goid!=326)&&routes.Select(r=>r.GetProperty("ObjectId").GetInt32()).Distinct().Count()==routes.Length,"Complete mixed typed routes");
  Plan? requested=null;
  foreach(var r in routes)
  {
   Shape(r,"ObjectId","Goid","ProfileManifestSha256","RawSourceJson","Region","NativePosition","NativeQuaternion","Standing","Target","Nodes","Doors");
   int id=r.GetProperty("ObjectId").GetInt32();var o=required.Single(o=>o.Id==id);Need(r.GetProperty("Goid").GetUInt16()==o.Goid&&r.GetProperty("RawSourceJson").GetString()==JsonSerializer.Serialize(o.Spot,Fields),"Typed route source");
   Need(o.Spot.metrId==1&&o.Spot.sectorID==Raw(Entry(c.GetProperty("Floor")).Raw).sectorID&&o.Spot.mxoStaticId==((uint)o.Spot.sectorID<<20|o.Spot.staticId)&&!o.Spot.exterior,"Source world/key/sector");
   var native=Profile(repo,o);Need(Hash(r.GetProperty("ProfileManifestSha256").GetString(),o.Goid==326?CabinetManifest:FurnitureManifest),"Per-type profile authority");
   var centre=V(native.Row.GetProperty("Centre"));string q=native.Row.GetProperty("Quaternion").GetString()!;CheckQuaternion(q);
   Need(V(r.GetProperty("NativePosition")).SequenceEqual(centre)&&Hash(r.GetProperty("NativeQuaternion").GetString(),q)&&r.GetProperty("Region").GetUInt16()==native.Row.GetProperty("Region").GetUInt16(),"Independent native pose/region");
   var floor=Entry(c.GetProperty("Floor"));var lobby=Entry(c.GetProperty("Lobby"));var minimum=V(native.Row.GetProperty("Minimum"));var maximum=V(native.Row.GetProperty("Maximum"));var standing=V(r.GetProperty("Standing"));
   Need(Enumerable.Range(0,3).All(i=>minimum[i]<maximum[i]&&centre[i]==(double)(float)((float)((float)minimum[i]+(float)maximum[i])*.5f)),"Native bounds midpoint");
   Need(standing.All(v=>v==(double)(float)v)&&standing.Where((v,i)=>v<minimum[i]||v>maximum[i]).Any()&&standing[1]==floor.Standing![1],"Fixed grounded outside-body standing");
   float distance=NativeDistance(standing,centre,q,native.Half);Need(float.IsFinite(distance)&&distance>0&&distance<=10000&&ContainerActions.InReach(standing.SelectMany(BitConverter.GetBytes).ToArray(),centre[0],centre[1],centre[2]),"Primary native100/server range");
   var target=r.GetProperty("Target");Shape(target,"Region","Polygon");Need(target.GetProperty("Region").GetUInt16()==r.GetProperty("Region").GetUInt16(),"Fixed target original region");
   var nodes=r.GetProperty("Nodes").EnumerateArray().ToArray();Need(nodes.Length>0&&nodes[^1].GetRawText()==target.GetRawText(),"Fixed path endpoint");foreach(var node in nodes)Shape(node,"Region","Polygon");
   var doors=r.GetProperty("Doors").EnumerateArray().Select(d=>{Shape(d,"Key","Goid","RawDoorJson","Quaternion","Position");var raw=Raw(d.GetProperty("RawDoorJson").GetString()!);ushort goid=d.GetProperty("Goid").GetUInt16();string dq=d.GetProperty("Quaternion").GetString()!;CheckQuaternion(dq);Need(MissionInteriorDoorTypes.IsSupported(goid)&&raw.metrId==1&&MissionSites.Goid(raw)==goid&&raw.mxoStaticId==d.GetProperty("Key").GetUInt32()&&raw.mxoStaticId==((uint)raw.sectorID<<20|raw.staticId)&&raw.sectorID==o.Spot.sectorID,"Required own-GO door source");return new Door(raw.mxoStaticId,goid,V(d.GetProperty("Position")),dq,null,d.GetProperty("RawDoorJson").GetString()!);}).ToArray();
   Need(doors.Select(d=>d.Key).Distinct().Count()==doors.Length,"Unique required doors");
   var plan=new Plan(state.Mission,state.Instance,id,o.Goid,state.Identity,state.Started,state.Phase,Sources(state),w.Clone(),r.Clone(),floor,lobby,doors,standing,target.GetProperty("Region").GetUInt16(),target.GetProperty("Polygon").GetUInt16(),floor.Standing![1]-95,centre,q);
   using(var evidence=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(repo,o.Spot.sectorID,e.GetProperty("EvidenceDigest").GetString()!)))Need(Hash(evidence.RootElement.GetProperty("NavSha256").GetString(),native.Row.GetProperty("NavSha256").GetString()),"Scene and native source same NAV");
   _=Steps(repo,plan);if(id==objectId)requested=plan;
  }
  Need(requested!=null,"Requested supported object has one route");return requested;
 }
 static (JsonElement Row,float[] Half) Profile(string repo,MissionProgress.SavedObject o)
 {
  string folder=o.Goid==326?"mission_bound_poses":"mission_bound_furniture_poses",pin=o.Goid==326?CabinetManifest:FurnitureManifest;
  string directory=Path.Combine(repo,"data",folder,"1");string text=File.ReadAllText(Path.Combine(directory,"manifest.json")).Replace("\r\n","\n");Need(Hash(Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))),pin),"Pinned profile manifest");using var m=JsonDocument.Parse(text);var root=m.RootElement;
  Need(root.GetProperty("Format").GetInt32()==1&&root.GetProperty("World").GetInt32()==1,"Profile world/format");
  float[] half;
  if(o.Goid==326){Need(root.GetProperty("Goid").GetUInt16()==326,"Original cabinet profile");half=new[]{25f,80f,30f};}
  else{Need(root.GetProperty("Kind").GetString()=="NativeBoundFurniture"&&root.GetProperty("Goids").EnumerateArray().Any(v=>v.GetUInt16()==o.Goid),"Furniture type catalog");var model=root.GetProperty("ModelContracts").GetProperty("Models").EnumerateArray().Single(v=>v.GetProperty("Goid").GetUInt16()==o.Goid);Need(model.GetProperty("OpenMaxDistance").GetInt32()==100,"Proven Open100 contract");byte[] bytes=Convert.FromHexString(model.GetProperty("ModelHalfExtentsHex").GetString()!);Need(bytes.Length==12,"Model half shape");half=Enumerable.Range(0,3).Select(i=>BitConverter.ToSingle(bytes,i*4)).ToArray();}
  Need(half.All(v=>float.IsFinite(v)&&v>0),"Positive model half");var file=root.GetProperty("Files").EnumerateArray().Single(v=>v.GetProperty("Sector").GetUInt16()==o.Spot.sectorID);string name=file.GetProperty("File").GetString()!;Need(name==o.Spot.sectorID+".json.gz","Declared sector filename");byte[] packed=File.ReadAllBytes(Path.Combine(directory,name));Need(Hash(Convert.ToHexString(SHA256.HashData(packed)),file.GetProperty("Sha256").GetString()),"Declared payload hash");
  using var stream=new MemoryStream(packed);using var gz=new GZipStream(stream,CompressionMode.Decompress);using var doc=JsonDocument.Parse(gz);var data=doc.RootElement;Need(data.GetProperty("World").GetInt32()==1&&data.GetProperty("Sector").GetUInt16()==o.Spot.sectorID&&data.GetProperty("Format").GetInt32()==1,"Payload world/sector");
  var rows=data.GetProperty("Rows").EnumerateArray().ToArray();Need(rows.Length==file.GetProperty("Rows").GetInt32(),"Counted payload rows");var row=rows.Single(r=>r.GetProperty("NativeKey").GetUInt32()==o.Spot.mxoStaticId);
  Need(row.GetProperty("Goid").GetUInt16()==o.Goid&&row.GetProperty("Static").GetUInt32()==o.Spot.staticId&&V(row.GetProperty("LegacyPosition")).SequenceEqual(new[]{o.Spot.pos_x,o.Spot.pos_y,o.Spot.pos_z})&&row.GetProperty("LegacyRotation").GetDouble()==o.Spot.rot&&Hash(row.GetProperty("LegacyQuaternion").GetString(),o.Spot.quat),"Exact audited raw source signature");
  return(row.Clone(),half);
 }
 static void CheckQuaternion(string text){byte[] b=Convert.FromHexString(text);Need(b.Length==16,"Quaternion shape");var q=Enumerable.Range(0,4).Select(i=>BitConverter.ToSingle(b,i*4)).ToArray();Need(q.All(float.IsFinite)&&Math.Abs(q.Sum(v=>(double)v*v)-1)<1e-5,"Unit finite quaternion");}
 public static void RequireArrival(Plan plan,double[] actualRoot,ushort sector,ushort region,double floorY)
 {
  var raw=Raw(plan.Floor.Raw);var node=plan.Selection.GetProperty("Context").GetProperty("Floor").GetProperty("Node");
  Need(actualRoot.Length==3&&actualRoot.All(double.IsFinite)&&raw.metrId==1&&sector==raw.sectorID&&region==node.GetProperty("Region").GetUInt16()&&floorY==plan.FloorY&&actualRoot[1]==floorY+95&&
   Enumerable.Range(0,3).All(i=>BitConverter.SingleToInt32Bits((float)actualRoot[i])==BitConverter.SingleToInt32Bits((float)plan.Floor.Standing![i])),"Actual captured ordinary RideRoot equals fixed current floor entry");
 }
 public static void RequireCurrent(Plan plan,MissionProgress.SavedState state)
 {var selected=Selection(state);Need(selected.HasValue&&state.Mission==plan.Mission&&state.Instance==plan.Instance&&state.Identity==plan.Owner&&state.Started==plan.Started&&state.Phase==plan.Phase&&Sources(state)==plan.Sources&&selected.Value.GetRawText()==plan.Selection.GetRawText(),"Current accepted/source/route identity");}
 public static float NativeDistance(double[] at,double[] centre,string quaternion,float[] h)
 {
  CheckQuaternion(quaternion);Need(at.Length==3&&centre.Length==3&&at.All(double.IsFinite)&&centre.All(double.IsFinite)&&h.Length==3&&h.All(v=>float.IsFinite(v)&&v>0),"Range shape");var q=Convert.FromHexString(quaternion);float x=BitConverter.ToSingle(q),y=BitConverter.ToSingle(q,4),z=BitConverter.ToSingle(q,8),w=BitConverter.ToSingle(q,12),tx=2*x,ty=2*y,tz=2*z;
  float xx=tx*x,xy=ty*x,xz=tz*x,yy=ty*y,yz=tz*y,zz=tz*z,xw=tx*w,yw=ty*w,zw=tz*w;float[] b={1-(zz+yy),xy+zw,xz-yw,xy-zw,1-(zz+xx),yz+xw,xz+yw,yz-xw,1-(xx+yy)};
  float[] d={(float)(at[0]-centre[0]),(float)(at[1]-centre[1]),(float)(at[2]-centre[2])};float sum=0;for(int i=0;i<3;i++){float local=(float)((double)d[0]*b[i*3]+(double)d[1]*b[i*3+1]+(double)d[2]*b[i*3+2]);float outside=local<-h[i]?(float)(local+h[i]):local>h[i]?(float)(local-h[i]):0;sum=(float)((double)outside*outside+sum);}return sum;
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
        Need(Contains(Polygon(nodes[0]),nodes[0],plan.Floor.Standing!),"saved entry polygon contains exact original standing");
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
            // Original authored neighbor/portal replay has no generated open-floor clearance threshold.
            double px=(x[0]+z[0])/2,pz=(x[1]+z[1])/2;double floorA=Floor(a,nodes[i-1],px,pz),floorB=Floor(b,nodes[i],px,pz);
            Need(localEvidence?Math.Abs(floorA-floorB)<=1e-7:floorA==floorB,"Bound route portal/edge floor agreement");Need(doors.All(k=>plan.Doors.Count(d=>d.Key==k)==1),"Bound route every portal door is saved own-GO proof");steps.Add(new(new[]{px,floorA+95,pz},doors,R(nodes[i]),P(nodes[i])));
        }
        Need(steps.SelectMany(s=>s.Doors).Distinct().Order().SequenceEqual(plan.Doors.Select(d=>d.Key).Order()),"every saved required door is used and every crossed door is saved");
        Need(steps.All(s=>Math.Abs(s.Standing[1]-(plan.FloorY+95))<=1e-7),"whole route stays on actual arrival floor");
        steps.Add(new(plan.Standing,Array.Empty<uint>(),plan.Region,plan.Polygon));return steps.ToArray();
    }
}
